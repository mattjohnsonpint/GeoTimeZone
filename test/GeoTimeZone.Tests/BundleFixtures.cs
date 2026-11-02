using System.IO.Compression;
using GeoTimeZone.Internal;

namespace GeoTimeZone.Tests;

internal static class BundleFixtures
{
    internal static byte[] Create(string prefix = "Test", Action<Dictionary<string, byte[]>>? change = null)
    {
        GeometryPolygon[] polygons = Enumerable.Range(0, 2).Select(zone => new GeometryPolygon(zone,
            [new GeometryRing([0, 1, 1, 0, 0], [0, 0, 1, 1, 0])])).ToArray();
        var geometry = GeometryArchiveWriter.Serialize(polygons);
        var index = new SpatialIndex([prefix + "/A", prefix + "/B"], geometry.DirectoryHash, 2,
            Enumerable.Repeat(-1, SpatialFormat.Columns * SpatialFormat.Rows).ToArray(),
            Enumerable.Repeat(-1, SpatialFormat.ChildrenPerCell).ToArray(), [0, 2], [0, 1]);
        var entries = new Dictionary<string, byte[]>
        {
            ["manifest.txt"] = System.Text.Encoding.UTF8.GetBytes(DataBundle.Manifest),
            [DataBundle.IndexName] = index.Serialize(),
            [DataBundle.GeometryName] = geometry.Image
        };
        change?.Invoke(entries);
        return Zip(entries);
    }

    internal static byte[] Zip(Dictionary<string, byte[]> entries, bool compressGeometry = false, bool duplicate = false)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            foreach (var item in entries)
            {
                var entry = archive.CreateEntry(item.Key, item.Key == DataBundle.GeometryName && !compressGeometry
                    ? CompressionLevel.NoCompression : CompressionLevel.Optimal);
                entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
                using var stream = entry.Open();
                stream.Write(item.Value, 0, item.Value.Length);
            }
            if (duplicate)
            {
                using var writer = new StreamWriter(archive.CreateEntry("manifest.txt").Open());
                writer.Write(DataBundle.Manifest);
            }
        }
        return output.ToArray();
    }

    internal static Dictionary<string, byte[]> Entries(byte[] zip)
    {
        using var archive = new ZipArchive(new MemoryStream(zip, false), ZipArchiveMode.Read);
        return archive.Entries.ToDictionary(entry => entry.FullName, entry =>
        {
            using var stream = entry.Open();
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            return copy.ToArray();
        });
    }
}
