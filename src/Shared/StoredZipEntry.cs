using System.IO;
using System.Text;

namespace GeoTimeZone.Internal;

// ZipArchiveEntry.Open() is nonseekable even for stored entries. Resolve the
// stored geometry's byte range once, then give each polygon load its own
// seekable, bounded view. Never buffer all embedded geometry just to seek it.
internal sealed class StoredZipEntry(long offset, long length, long bundleLength)
{
    internal Stream Open(Func<Stream> open)
    {
        var source = open();
        try
        {
            SpatialFormat.Require(source.CanSeek && source.Length == bundleLength);
            return new WindowStream(source, offset, length);
        }
        catch { source.Dispose(); throw; }
    }

    internal static StoredZipEntry? Find(Stream source, string name, bool allowCompressed = false)
    {
        SpatialFormat.Require(source.CanSeek && source.Length >= 22 && source.Length <= DataBundle.MaximumSize);
        // A classic ZIP ends with a 22-byte EOCD plus at most 65535 bytes of
        // comment. ZIP64/multi-volume archives are unnecessary under our size
        // cap and are explicitly rejected, rather than silently truncating offsets.
        var tailLength = (int)Math.Min(source.Length, 22 + ushort.MaxValue);
        source.Position = source.Length - tailLength;
        using var reader = new BinaryReader(source, Encoding.UTF8, true);
        var tail = reader.ReadBytes(tailLength);
        SpatialFormat.Require(tail.Length == tailLength);
        var end = -1;
        for (var i = tail.Length - 22; i >= 0; i--)
            if (tail[i] == 0x50 && tail[i + 1] == 0x4b && tail[i + 2] == 5 && tail[i + 3] == 6
                && i + 22 + tail[i + 20] + (tail[i + 21] << 8) == tail.Length) { end = i; break; }
        SpatialFormat.Require(end >= 0);
        var endPosition = source.Length - tailLength + end;
        source.Position = endPosition + 4;
        SpatialFormat.Require(reader.ReadUInt16() == 0 && reader.ReadUInt16() == 0);
        var diskCount = reader.ReadUInt16();
        var count = reader.ReadUInt16();
        var directoryLength = reader.ReadUInt32();
        var directoryOffset = reader.ReadUInt32();
        SpatialFormat.Require(count > 0 && count <= 16 && diskCount == count
            && (long)directoryOffset + directoryLength == endPosition);
        source.Position = directoryOffset;
        StoredZipEntry? found = null;
        uint localOffset = 0;
        ushort flags = 0;
        var compressedGeometry = false;
        for (var i = 0; i < count; i++)
        {
            SpatialFormat.Require(source.Position + 46 <= endPosition && reader.ReadUInt32() == 0x02014b50);
            source.Position += 4; // made-by / required versions
            var entryFlags = reader.ReadUInt16();
            var method = reader.ReadUInt16();
            source.Position += 8; // timestamp and CRC (component hashes validate geometry)
            var compressed = reader.ReadUInt32();
            var raw = reader.ReadUInt32();
            var nameLength = reader.ReadUInt16();
            var extraLength = reader.ReadUInt16();
            var commentLength = reader.ReadUInt16();
            SpatialFormat.Require(reader.ReadUInt16() == 0); // starting disk
            source.Position += 6; // attributes
            var start = reader.ReadUInt32();
            SpatialFormat.Require(nameLength <= 512 && source.Position + nameLength + extraLength + commentLength <= endPosition
                && (entryFlags & 1) == 0 && (long)start + 30 <= directoryOffset);
            var entryName = Encoding.UTF8.GetString(reader.ReadBytes(nameLength));
            source.Position += extraLength + commentLength;
            if (entryName != name) continue;
            SpatialFormat.Require(found == null && raw > 0 && raw <= DataBundle.MaximumSize
                && (method == 0 ? compressed == raw : allowCompressed && method == 8)
                && (long)start + 30 + compressed <= directoryOffset);
            found = new StoredZipEntry(0, raw, source.Length);
            compressedGeometry = method != 0;
            localOffset = start;
            flags = entryFlags;
        }
        SpatialFormat.Require(source.Position == endPosition && found != null);
        // Override streams are already being copied into owned storage. Their
        // geometry can be extracted through ZipArchive even if outer-deflated.
        // Embedded geometry must remain stored to support direct random access.
        if (compressedGeometry) return null;
        source.Position = localOffset;
        SpatialFormat.Require(reader.ReadUInt32() == 0x04034b50);
        source.Position += 2; // required version
        SpatialFormat.Require(reader.ReadUInt16() == flags && reader.ReadUInt16() == 0);
        source.Position += 8; // timestamp / CRC
        var localCompressed = reader.ReadUInt32();
        var localRaw = reader.ReadUInt32();
        var localNameLength = reader.ReadUInt16();
        var localExtraLength = reader.ReadUInt16();
        SpatialFormat.Require(localNameLength <= 512 && source.Position + localNameLength + localExtraLength <= directoryOffset);
        SpatialFormat.Require(Encoding.UTF8.GetString(reader.ReadBytes(localNameLength)) == name);
        // With a data descriptor, sizes follow the payload instead. The
        // central directory remains authoritative; without one they must agree.
        SpatialFormat.Require((flags & 8) != 0 || (localCompressed == found!.Length && localRaw == found.Length));
        var payload = source.Position + localExtraLength;
        SpatialFormat.Require(payload + found!.Length <= directoryOffset);
        return new StoredZipEntry(payload, found.Length, source.Length);
    }

    private long Length => length;

    private sealed class WindowStream(Stream source, long start, long length) : Stream
    {
        private long _position;
        public override bool CanRead => source.CanRead;
        public override bool CanSeek => source.CanSeek;
        public override bool CanWrite => false;
        public override long Length => length;
        public override long Position
        {
            get => _position;
            set
            {
                if (value < 0 || value > length) throw new IOException("Seek outside ZIP entry.");
                _position = value;
            }
        }
        public override int Read(byte[] buffer, int offset, int count)
        {
            source.Position = start + _position;
            var read = source.Read(buffer, offset, (int)Math.Min(count, length - _position));
            _position += read;
            return read;
        }
        public override long Seek(long offset, SeekOrigin origin)
        {
            Position = checked((origin == SeekOrigin.Begin ? 0 : origin == SeekOrigin.Current ? Position
                : origin == SeekOrigin.End ? Length : throw new ArgumentOutOfRangeException(nameof(origin))) + offset);
            return Position;
        }
        protected override void Dispose(bool disposing) { if (disposing) source.Dispose(); base.Dispose(disposing); }
        public override void Flush() => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
