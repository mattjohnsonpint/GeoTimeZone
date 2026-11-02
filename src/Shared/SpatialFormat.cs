using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;

namespace GeoTimeZone.Internal;

internal static class SpatialFormat
{
    internal const int Version = 1;
    internal const int IndexMagic = 0x495A5447;
    internal const int GeometryMagic = 0x475A5447;
    internal const int Columns = 480;
    internal const int Rows = 240;
    internal const int Refinement = 4;
    internal const int ChildrenPerCell = Refinement * Refinement;
    internal const double CellWidth = 360.0 / Columns;
    internal const double ChildWidth = CellWidth / Refinement;
    internal const int BlockSize = 128;
    internal const int MaxDecompressedBytes = 512 * 1024 * 1024;
    internal const double CoordinateScale = 1_000_000;
    // Serialized widths, not CLR struct sizes (which can include padding).
    internal const int PolygonRecordBytes = sizeof(int) + 4 * sizeof(double) + 2 * sizeof(int);
    internal const int RingRecordBytes = 4 * sizeof(double) + 2 * sizeof(int);
    internal const int BlockRecordBytes = 2 * sizeof(double) + 2 * sizeof(int) + sizeof(ushort);
    internal const string IndexResource = "GeoTimeZone.TZIndex.dat.gz";
    internal const string GeometryResource = "GeoTimeZone.TZGeometry.dat.gz";

    internal static int ReadCount(BinaryReader reader, int maximum)
    {
        var count = reader.ReadInt32();
        if (count < 0 || count > maximum) throw new InvalidDataException("Invalid spatial data count.");
        return count;
    }

    internal static void Require(bool condition)
    {
        if (!condition) throw new InvalidDataException("Invalid or incompatible spatial data.");
    }

    internal static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    internal static byte[] Decompress(Stream compressed)
    {
        // Our resources are single gzip members. Their final ISIZE word is
        // the uncompressed byte count modulo 2^32. Since the format is capped
        // below that, use it to allocate once instead of growing a MemoryStream
        // and retaining a second full copy during the first boundary lookup.
        if (compressed.CanSeek && compressed.Length - compressed.Position >= 18)
        {
            var position = compressed.Position;
            compressed.Position = compressed.Length - 4;
            uint length = 0;
            for (var i = 0; i < 4; i++)
            {
                var next = compressed.ReadByte();
                Require(next >= 0);
                length |= (uint)next << (8 * i);
            }

            compressed.Position = position;
            Require(length <= MaxDecompressedBytes);
            var bytes = new byte[(int)length];
            using var member = new GZipStream(compressed, CompressionMode.Decompress, true);
            var offset = 0;
            while (offset < bytes.Length)
            {
                var read = member.Read(bytes, offset, bytes.Length - offset);
                Require(read > 0);
                offset += read;
            }

            Require(member.ReadByte() == -1); // finish CRC checking; reject extra decompressed data
            return bytes;
        }

        using var gzip = new GZipStream(compressed, CompressionMode.Decompress, true);
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int count;
        while ((count = gzip.Read(buffer, 0, buffer.Length)) != 0)
        {
            Require(output.Length + count <= MaxDecompressedBytes);
            output.Write(buffer, 0, count);
        }

        return output.ToArray();
    }

    internal static byte[] Hash(byte[] data)
    {
#if NET6_0_OR_GREATER
        return SHA256.HashData(data);
#else
        using var sha = SHA256.Create();
        return sha.ComputeHash(data);
#endif
    }

    internal static void WriteCompressed(string path, byte[] bytes)
    {
        using var file = File.Create(path);
        using var gzip = new GZipStream(file, CompressionLevel.Optimal);
        gzip.Write(bytes, 0, bytes.Length);
    }

    internal static int Cell(double value, double origin, double width, int count)
    {
        // Widths 3/4 and 3/16 and their world-aligned boundaries are exact
        // binary fractions. The initial subtraction can nevertheless swallow
        // a tiny offset (e.g. -double.Epsilon + 180 rounds to 180). Correct a
        // possible one-cell rounding error against the ORIGINAL coordinate,
        // not the translated value, before accepting a geometry-free answer.
        // Clamping accommodates the closed upper endpoint of the world.
        var cell = (int)Math.Floor((value - origin) / width);
        cell = Math.Min(Math.Max(cell, 0), count - 1);
        if (cell > 0 && value < origin + cell * width) cell--;
        else if (cell < count - 1 && value >= origin + (cell + 1) * width) cell++;
        return cell;
    }
}
