using System.IO;
using System.IO.Compression;
using System.Text;

namespace GeoTimeZone.Internal;

internal interface IPolygonGeometry
{
    int Zone(int polygon);
    bool Covers(int polygon, double latitude, double longitude);
}

internal sealed class GeometryArchive : IPolygonGeometry
{
    internal const long DefaultCacheBytes = 8 * 1024 * 1024;
    private readonly Func<Stream> _open;
    private readonly Entry[] _entries;
    private readonly int _zoneCount;
    private readonly long _payload;
    private readonly long _archiveLength;
    private readonly long _capacity;
    private readonly object _gate = new();
    private readonly Dictionary<int, LinkedListNode<CachedPolygon>> _cache = new();
    private readonly LinkedList<CachedPolygon> _recent = new();
    private readonly Dictionary<int, Lazy<PackedGeometry>> _loading = new();
    private long _cachedBytes;

    internal GeometryArchive(Func<Stream> open, byte[] directoryHash, int zoneCount, int polygonCount,
        long cacheBytes = DefaultCacheBytes)
    {
#if NET8_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfNegative(cacheBytes);
#else
        if (cacheBytes < 0) throw new ArgumentOutOfRangeException(nameof(cacheBytes));
#endif
        _open = open;
        _zoneCount = zoneCount;
        _capacity = cacheBytes;
        using var stream = open();
        SpatialFormat.Require(stream.CanSeek);
        using var reader = new BinaryReader(stream, Encoding.UTF8, true);
        SpatialFormat.Require(reader.ReadInt32() == SpatialFormat.ArchiveMagic && reader.ReadInt32() == SpatialFormat.Version);
        var count = SpatialFormat.ReadCount(reader, 1_000_000);
        var directoryLength = reader.ReadInt32();
        SpatialFormat.Require(count > 0 && count == polygonCount && zoneCount > 0
            && directoryLength == (long)count * SpatialFormat.DirectoryRecordBytes
            && directoryLength <= stream.Length - stream.Position);
        var directory = reader.ReadBytes(directoryLength);
        // The grid binds this directory, which in turn binds every component's
        // uncompressed image. No whole-archive read/hash is needed at startup.
        SpatialFormat.Require(directory.Length == directoryLength && SpatialFormat.Hash(directory).SequenceEqual(directoryHash));
        _payload = stream.Position;
        _archiveLength = stream.Length;
        _entries = new Entry[count];
        using var metadata = new BinaryReader(new MemoryStream(directory, false));
        long nextOffset = 0;
        for (var i = 0; i < count; i++)
        {
            var zone = metadata.ReadInt32();
            var west = metadata.ReadDouble();
            var east = metadata.ReadDouble();
            var south = metadata.ReadDouble();
            var north = metadata.ReadDouble();
            var offset = metadata.ReadInt64();
            var compressed = metadata.ReadInt32();
            var raw = metadata.ReadInt32();
            var hash = metadata.ReadBytes(32);
            SpatialFormat.Require(zone >= 0 && zone < zoneCount && (i == 0 || zone >= _entries[i - 1].Zone)
                && SpatialFormat.IsFinite(west) && SpatialFormat.IsFinite(east) && west >= -180 && east <= 180 && west <= east
                && SpatialFormat.IsFinite(south) && SpatialFormat.IsFinite(north) && south >= -90 && north <= 90 && south <= north
                && offset == nextOffset && compressed >= 18 && raw > 0 && raw <= SpatialFormat.MaxDecompressedBytes
                && compressed <= stream.Length - _payload - offset && hash.Length == 32);
            _entries[i] = new Entry(zone, west, east, south, north, offset, compressed, raw, hash);
            nextOffset += compressed;
        }
        SpatialFormat.Require(_payload + nextOffset == stream.Length);
    }

    public int Zone(int polygon) => _entries[polygon].Zone;

    public bool Covers(int polygon, double latitude, double longitude)
    {
        var entry = _entries[polygon];
        // Reject candidates from their source bounds before opening a stream,
        // allocating an image or touching the LRU. Bounds are closed so exact
        // boundary points still reach the robust polygon predicate.
        return longitude >= entry.West && longitude <= entry.East && latitude >= entry.South && latitude <= entry.North
            && GetPolygon(polygon).Covers(0, latitude, longitude);
    }

    internal long CachedBytes { get { lock (_gate) return _cachedBytes; } }
    internal int CachedCount { get { lock (_gate) return _cache.Count; } }

    internal PackedGeometry GetPolygon(int polygon)
    {
        Lazy<PackedGeometry> loading;
        lock (_gate)
        {
            if (_cache.TryGetValue(polygon, out var node))
            {
                _recent.Remove(node);
                _recent.AddFirst(node);
                return node.Value.Geometry;
            }
            if (!_loading.TryGetValue(polygon, out loading!))
            {
                loading = CreateLoading(polygon);
                _loading.Add(polygon, loading);
            }
        }
        // Decompression happens outside the cache lock: unrelated polygons
        // can load concurrently. Lazy coalesces simultaneous loads of one ID.
        try { return loading.Value; }
        finally
        {
            lock (_gate)
                if (_loading.TryGetValue(polygon, out var pending) && ReferenceEquals(pending, loading)) _loading.Remove(polygon);
        }
    }

    // Keep the capturing lambda out of GetPolygon: otherwise its display
    // class can be allocated even for an already-cached component.
    private Lazy<PackedGeometry> CreateLoading(int polygon) => new(() => LoadAndCache(polygon));

    private PackedGeometry LoadAndCache(int polygon)
    {
        var entry = _entries[polygon];
        // Each miss owns a fresh seekable stream; there is no shared cursor or
        // permanently open handle. Embedded resources are read directly rather
        // than copied into a second, globally retained compressed byte array.
        using var source = _open();
        SpatialFormat.Require(source.CanSeek && source.Length == _archiveLength);
        source.Position = _payload + entry.Offset;
        using var bounded = new ComponentStream(source, entry.CompressedBytes);
        using var gzip = new GZipStream(bounded, CompressionMode.Decompress, true);
        var image = new byte[entry.RawBytes];
        var cursor = 0;
        while (cursor < image.Length)
        {
            var read = gzip.Read(image, cursor, image.Length - cursor);
            SpatialFormat.Require(read > 0);
            cursor += read;
        }
        SpatialFormat.Require(gzip.ReadByte() == -1);
        var geometry = new PackedGeometry(image, entry.Hash, _zoneCount, 1);
        SpatialFormat.Require(geometry.Zone(0) == entry.Zone);
        geometry.ValidateBounds(0, entry.West, entry.East, entry.South, entry.North);
        var cost = geometry.RetainedBytes + 128; // LRU node, entry and dictionary allowance
        lock (_gate)
        {
            // Oversized components are usable but not retained. The limit
            // bounds cached images/records, not temporary concurrent loads or
            // geometry still referenced by queries after eviction.
            if (cost <= _capacity)
            {
                while (_cachedBytes > _capacity - cost)
                {
                    var oldest = _recent.Last!;
                    _cache.Remove(oldest.Value.Polygon);
                    _cachedBytes -= oldest.Value.Cost;
                    _recent.RemoveLast();
                }
                var cached = new CachedPolygon(polygon, geometry, cost);
                _cache.Add(polygon, _recent.AddFirst(cached));
                _cachedBytes += cost;
            }
        }
        return geometry;
    }

    private sealed class CachedPolygon(int polygon, PackedGeometry geometry, long cost)
    {
        internal int Polygon { get; } = polygon;
        internal PackedGeometry Geometry { get; } = geometry;
        internal long Cost { get; } = cost;
    }

    private sealed class Entry(int zone, double west, double east, double south, double north,
        long offset, int compressed, int raw, byte[] hash)
    {
        internal int Zone { get; } = zone;
        internal double West { get; } = west;
        internal double East { get; } = east;
        internal double South { get; } = south;
        internal double North { get; } = north;
        internal long Offset { get; } = offset;
        internal int CompressedBytes { get; } = compressed;
        internal int RawBytes { get; } = raw;
        internal byte[] Hash { get; } = hash;
    }

    // GZipStream may read ahead or decode concatenated members. Confine it to
    // one component so it can never consume/decompress the next polygon.
    private sealed class ComponentStream(Stream source, long length) : Stream
    {
        private long _remaining = length;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = source.Read(buffer, offset, (int)Math.Min(count, _remaining));
            _remaining -= read;
            return read;
        }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
