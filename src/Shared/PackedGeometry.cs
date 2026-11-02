using System.IO;

namespace GeoTimeZone.Internal;

internal sealed class PackedGeometry : IPolygonGeometry
{
    private readonly byte[] _image;
    private readonly int _payload;
    private readonly PolygonRecord[] _polygons;
    private readonly RingRecord[] _rings;
    private readonly BlockRecord[] _blocks;

    internal PackedGeometry(byte[] image, byte[] expectedHash, int zoneCount, int polygonCount)
    {
        SpatialFormat.Require(image.Length <= SpatialFormat.MaxDecompressedBytes && SpatialFormat.Hash(image).SequenceEqual(expectedHash));
        _image = image;
        using var stream = new MemoryStream(image, false);
        using var reader = new BinaryReader(stream);
        SpatialFormat.Require(reader.ReadInt32() == SpatialFormat.GeometryMagic && reader.ReadInt32() == SpatialFormat.Version
            && reader.ReadInt32() == SpatialFormat.BlockSize && reader.ReadDouble() == SpatialFormat.CoordinateScale);
        var polygons = SpatialFormat.ReadCount(reader, 1_000_000);
        var rings = SpatialFormat.ReadCount(reader, 1_000_000);
        var blocks = SpatialFormat.ReadCount(reader, 10_000_000);
        var payloadLength = SpatialFormat.ReadCount(reader, int.MaxValue);
        SpatialFormat.Require(polygons == polygonCount && polygons > 0 && rings >= polygons && blocks >= rings
            && (long)polygons * SpatialFormat.PolygonRecordBytes + (long)rings * SpatialFormat.RingRecordBytes
                + (long)blocks * SpatialFormat.BlockRecordBytes + payloadLength == stream.Length - stream.Position);
        _polygons = new PolygonRecord[polygons];
        _rings = new RingRecord[rings];
        _blocks = new BlockRecord[blocks];
        var nextRing = 0;
        for (var i = 0; i < polygons; i++)
        {
            var zone = reader.ReadInt32();
            var bounds = ReadBounds(reader);
            var first = reader.ReadInt32();
            var count = reader.ReadInt32();
            SpatialFormat.Require(zone >= 0 && zone < zoneCount && first == nextRing && count > 0 && count <= rings - first);
            SpatialFormat.Require(i == 0 || zone >= _polygons[i - 1].Zone);
            _polygons[i] = new PolygonRecord(zone, bounds, first, count);
            nextRing += count;
        }

        SpatialFormat.Require(nextRing == rings);
        var nextBlock = 0;
        for (var i = 0; i < rings; i++)
        {
            var bounds = ReadBounds(reader);
            var first = reader.ReadInt32();
            var count = reader.ReadInt32();
            SpatialFormat.Require(first == nextBlock && count > 0 && count <= blocks - first);
            _rings[i] = new RingRecord(bounds, first, count);
            nextBlock += count;
        }

        SpatialFormat.Require(nextBlock == blocks);
        var nextOffset = 0;
        for (var i = 0; i < blocks; i++)
        {
            var min = reader.ReadDouble();
            var max = reader.ReadDouble();
            var offset = reader.ReadInt32();
            var length = reader.ReadInt32();
            var edges = reader.ReadUInt16();
            SpatialFormat.Require(SpatialFormat.IsFinite(min) && SpatialFormat.IsFinite(max) && min >= -90 && max <= 90 && min <= max
                && offset == nextOffset && length > 0 && length <= payloadLength - offset
                && edges > 0 && edges <= SpatialFormat.BlockSize);
            _blocks[i] = new BlockRecord(min, max, offset, length, edges);
            nextOffset += length;
        }

        SpatialFormat.Require(nextOffset == payloadLength);
        _payload = checked((int)stream.Position);
    }

    public int Zone(int polygon) => _polygons[polygon].Zone;

    // Conservatively account for the retained image, padded record arrays,
    // array headers and this object. The cache adds its own entry overhead.
    internal long RetainedBytes => _image.LongLength + (long)_polygons.Length * 48
        + (long)_rings.Length * 40 + (long)_blocks.Length * 32 + 256;

    internal void ValidateCoordinates(GeometryPolygon[] source)
    {
        // Build-time exhaustive round trip, separate from sampled geographic
        // tests: every source vertex (including duplicated bridging vertices)
        // must decode to the original numeric coordinate before publication.
        SpatialFormat.Require(source.Length == _polygons.Length);
        for (var polygon = 0; polygon < source.Length; polygon++)
        {
            var entry = _polygons[polygon];
            SpatialFormat.Require(entry.Zone == source[polygon].Zone && entry.Rings == source[polygon].Rings.Length);
            for (var ring = 0; ring < entry.Rings; ring++)
            {
                var expected = source[polygon].Rings[ring];
                var record = _rings[entry.FirstRing + ring];
                var firstVertex = 0;
                for (var blockIndex = record.FirstBlock; blockIndex < record.FirstBlock + record.Blocks; blockIndex++)
                {
                    var block = _blocks[blockIndex];
                    var cursor = _payload + block.Offset;
                    var end = cursor + block.Length;
                    int previousX = 0, previousY = 0;
                    for (var point = 0; point <= block.Edges; point++)
                    {
                        SpatialFormat.Require(firstVertex + point < expected.X.Length);
                        var x = CoordinateCodec.Read(_image, ref cursor, end, ref previousX, 180);
                        var y = CoordinateCodec.Read(_image, ref cursor, end, ref previousY, 90);
                        SpatialFormat.Require(x == expected.X[firstVertex + point] && y == expected.Y[firstVertex + point]);
                    }

                    SpatialFormat.Require(cursor == end);
                    firstVertex += block.Edges;
                }

                SpatialFormat.Require(firstVertex == expected.X.Length - 1);
            }
        }
    }

    public bool Covers(int polygon, double latitude, double longitude)
    {
        var entry = _polygons[polygon];
        if (!entry.Bounds.Covers(latitude, longitude) || Locate(entry.FirstRing, latitude, longitude) == 0) return false;
        for (var i = 1; i < entry.Rings; i++)
            if (Locate(entry.FirstRing + i, latitude, longitude) == 1) return false;
        return true;
    }

    // Exterior = 0, interior = 1, boundary = 2. Both outer and hole boundaries
    // are included, matching polygon Covers semantics rather than Contains.
    private int Locate(int ring, double y, double x)
    {
        var entry = _rings[ring];
        if (!entry.Bounds.Covers(y, x)) return 0;
        var inside = false;
        for (var i = entry.FirstBlock; i < entry.FirstBlock + entry.Blocks; i++)
        {
            var block = _blocks[i];
            // An edge can affect a horizontal ray only if it spans the query
            // latitude. Bounds include the bridging endpoint, so skipping a
            // block cannot omit an edge crossing or a point on its boundary.
            if (y < block.MinY || y > block.MaxY) continue;
            var cursor = _payload + block.Offset;
            var end = cursor + block.Length;
            int previousX = 0, previousY = 0;
            var x1 = CoordinateCodec.Read(_image, ref cursor, end, ref previousX, 180);
            var y1 = CoordinateCodec.Read(_image, ref cursor, end, ref previousY, 90);
            for (var edge = 0; edge < block.Edges; edge++)
            {
                var x2 = CoordinateCodec.Read(_image, ref cursor, end, ref previousX, 180);
                var y2 = CoordinateCodec.Read(_image, ref cursor, end, ref previousY, 90);
                if (y >= Math.Min(y1, y2) && y <= Math.Max(y1, y2) && x <= Math.Max(x1, x2))
                {
                    if (y1 == y2)
                    {
                        // Horizontal edges never toggle parity, but their
                        // points belong to the polygon's closed boundary.
                        if (x >= Math.Min(x1, x2)) return 2;
                    }
                    else if (x < Math.Min(x1, x2))
                    {
                        // Both endpoints are strictly to the right: a
                        // straddling edge crosses the ray without division.
                        // (y1>y)!=(y2>y) counts just one edge at a vertex;
                        // using inclusive comparisons on both ends would
                        // double-count local extrema and shared vertices.
                        if ((y1 > y) != (y2 > y)) inside = !inside;
                    }
                    else
                    {
                        var sign = Orientation.Sign(x1, y1, x2, y2, x, y);
                        if (sign == 0) return 2;
                        // For an upward edge, a positive orientation puts Q
                        // left of the edge's intersection with the ray. For
                        // a downward edge the sign reverses. This is the
                        // usual ray/segment test without dividing by dy.
                        if ((y1 > y) != (y2 > y) && (y2 > y1 ? sign > 0 : sign < 0)) inside = !inside;
                    }
                }

                x1 = x2;
                y1 = y2;
            }

            SpatialFormat.Require(cursor == end);
        }

        return inside ? 1 : 0;
    }

    private static Bounds ReadBounds(BinaryReader reader)
    {
        var minX = reader.ReadDouble();
        var maxX = reader.ReadDouble();
        var minY = reader.ReadDouble();
        var maxY = reader.ReadDouble();
        SpatialFormat.Require(SpatialFormat.IsFinite(minX) && SpatialFormat.IsFinite(maxX) && SpatialFormat.IsFinite(minY) && SpatialFormat.IsFinite(maxY)
            && minX >= -180 && maxX <= 180 && minY >= -90 && maxY <= 90 && minX <= maxX && minY <= maxY);
        return new Bounds(minX, maxX, minY, maxY);
    }

    private readonly struct Bounds(double minX, double maxX, double minY, double maxY)
    {
        internal bool Covers(double y, double x) => x >= minX && x <= maxX && y >= minY && y <= maxY;
    }

    private readonly struct PolygonRecord(int zone, Bounds bounds, int firstRing, int rings)
    {
        internal readonly int Zone = zone;
        internal readonly Bounds Bounds = bounds;
        internal readonly int FirstRing = firstRing;
        internal readonly int Rings = rings;
    }

    private readonly struct RingRecord(Bounds bounds, int firstBlock, int blocks)
    {
        internal readonly Bounds Bounds = bounds;
        internal readonly int FirstBlock = firstBlock;
        internal readonly int Blocks = blocks;
    }

    private readonly struct BlockRecord(double minY, double maxY, int offset, int length, int edges)
    {
        internal readonly double MinY = minY;
        internal readonly double MaxY = maxY;
        internal readonly int Offset = offset;
        internal readonly int Length = length;
        internal readonly int Edges = edges;
    }
}
