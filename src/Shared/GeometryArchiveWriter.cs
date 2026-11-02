using System.IO;
using System.IO.Compression;
using System.Text;

namespace GeoTimeZone.Internal;

internal sealed class GeometryArchiveImage(byte[] image, byte[] directoryHash)
{
    internal byte[] Image { get; } = image;
    internal byte[] DirectoryHash { get; } = directoryHash;
}

internal static class GeometryArchiveWriter
{
    internal static GeometryArchiveImage Serialize(GeometryPolygon[] polygons)
    {
        if (polygons.Length == 0) throw new ArgumentException("No polygons supplied.", nameof(polygons));
        using var directory = new MemoryStream();
        using var payload = new MemoryStream();
        using var metadata = new BinaryWriter(directory, Encoding.UTF8, true);
        var zones = checked(polygons.Max(p => p.Zone) + 1);
        foreach (var polygon in polygons)
        {
            var image = GeometryWriter.Serialize([polygon]);
            var hash = SpatialFormat.Hash(image);
            // Verify EVERY original coordinate through the actual runtime
            // decoder before this component is eligible for publication.
            new PackedGeometry(image, hash, zones, 1).ValidateCoordinates([polygon]);
            var start = payload.Position;
            using (var gzip = new GZipStream(payload, CompressionLevel.Optimal, true)) gzip.Write(image, 0, image.Length);
            var shell = polygon.Rings[0];
            metadata.Write(polygon.Zone);
            metadata.Write(shell.X.Min());
            metadata.Write(shell.X.Max());
            metadata.Write(shell.Y.Min());
            metadata.Write(shell.Y.Max());
            metadata.Write(start);
            metadata.Write(checked((int)(payload.Position - start)));
            metadata.Write(image.Length);
            metadata.Write(hash);
        }
        metadata.Flush();
        var entries = directory.ToArray();
        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output, Encoding.UTF8, true);
        writer.Write(SpatialFormat.ArchiveMagic);
        writer.Write(SpatialFormat.Version);
        writer.Write(polygons.Length);
        writer.Write(entries.Length);
        writer.Write(entries);
        payload.WriteTo(output);
        writer.Flush();
        var bytes = output.ToArray();
        var fingerprint = SpatialFormat.Hash(entries);
        _ = new GeometryArchive(() => new MemoryStream(bytes, false), fingerprint, zones, polygons.Length, 0);
        return new GeometryArchiveImage(bytes, fingerprint);
    }
}
