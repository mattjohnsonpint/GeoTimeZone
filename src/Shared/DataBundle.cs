using System.IO;
using System.IO.Compression;
using System.Resources;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GeoTimeZone.Internal;

internal sealed class SpatialData(SpatialIndex index, Func<Stream> openGeometry)
{
    internal SpatialIndex Index { get; } = index;
    internal Lazy<GeometryArchive> Geometry { get; } = new(() => new GeometryArchive(openGeometry,
        index.GeometryHash, index.Zones.Length, index.PolygonCount));

    internal void Validate(CancellationToken cancellationToken)
    {
        // Validate the complete override before publishing any state, but do
        // not fill the runtime LRU with data the application may never query.
        var validator = new GeometryArchive(openGeometry, Index.GeometryHash, Index.Zones.Length, Index.PolygonCount, 0);
        for (var polygon = 0; polygon < Index.PolygonCount; polygon++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            validator.GetPolygon(polygon).ValidateEncoding(cancellationToken);
        }
    }
}

internal static class DataBundle
{
    internal const string FileName = "GeoTimeZoneData.zip";
    internal const string ResourceName = "GeoTimeZone.GeoTimeZoneData.zip";
    internal const string Manifest = "GeoTimeZone/1";
    internal const string IndexName = "TZIndex.dat";
    internal const string GeometryName = "TZGeometry.dat";
    internal const int MaximumSize = 128 * 1024 * 1024;
    private const int MaximumIndexSize = SpatialIndex.MaximumImageBytes;

    internal static SpatialData ReadEmbedded() => Read(() => typeof(DataBundle).Assembly.GetManifestResourceStream(ResourceName)
        ?? throw new MissingManifestResourceException(ResourceName));

    internal static SpatialData Read(Func<Stream> open, bool copyGeometry = false)
    {
        using var source = open();
        // Validate bounded classic-ZIP directory metadata before ZipArchive
        // allocates entry objects. Embedded geometry must be stored, not outer-deflated:
        // its polygon members already carry their own compression and hashes.
        var geometry = StoredZipEntry.Find(source, GeometryName, allowCompressed: copyGeometry);
        source.Position = 0;
        using var archive = new ZipArchive(source, ZipArchiveMode.Read, true);
        var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
        foreach (var entry in archive.Entries)
        {
            if (entries.ContainsKey(entry.FullName)) throw new InvalidDataException("Duplicate bundle entry: " + entry.FullName);
            entries.Add(entry.FullName, entry);
        }
        if (!entries.TryGetValue("manifest.txt", out var manifest) || manifest.Length > 128)
            throw new InvalidDataException("The bundle requires manifest.txt.");
        using (var input = manifest.Open())
        using (var buffer = Buffer(input, 128))
        using (var reader = new StreamReader(buffer, new UTF8Encoding(false, true)))
            if (reader.ReadToEnd().Trim() != Manifest) throw new InvalidDataException("Unsupported time zone data bundle format.");
        if (!entries.TryGetValue(IndexName, out var indexEntry) || indexEntry.Length > MaximumIndexSize)
            throw new InvalidDataException("The bundle requires " + IndexName + ".");
        using var indexInput = indexEntry.Open();
        using var indexBuffer = Buffer(indexInput, MaximumIndexSize);
        var index = SpatialIndex.Read(indexBuffer);
        if (copyGeometry)
        {
            using var entry = entries[GeometryName].Open();
            using var buffer = Buffer(entry);
            return CreateOwnedData(index, buffer.ToArray());
        }
        return new SpatialData(index, () => geometry!.Open(open));
    }

    // A separate closure must not capture the ZIP source factory as well as
    // the extracted archive, which would retain two copies of the geometry.
    private static SpatialData CreateOwnedData(SpatialIndex index, byte[] geometry) =>
        new(index, () => new MemoryStream(geometry, false));

    internal static MemoryStream Buffer(Stream source, int maximumSize = MaximumSize)
    {
        ValidateStream(source);
        var destination = new MemoryStream();
        try
        {
            var buffer = new byte[81920];
            int count;
            while ((count = source.Read(buffer, 0, buffer.Length)) != 0) Append(destination, buffer, count, maximumSize);
            destination.Position = 0;
            return destination;
        }
        catch { destination.Dispose(); throw; }
    }

    internal static async Task<MemoryStream> BufferAsync(Stream source, CancellationToken cancellationToken, int maximumSize = MaximumSize)
    {
        ValidateStream(source);
        var destination = new MemoryStream();
        try
        {
            var buffer = new byte[81920];
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var count =
#if NET8_0_OR_GREATER || NETSTANDARD2_1
                    await source.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
#else
                    await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
#endif
                if (count == 0) break;
                Append(destination, buffer, count, maximumSize);
            }
            destination.Position = 0;
            return destination;
        }
        catch { destination.Dispose(); throw; }
    }

    private static void ValidateStream(Stream source)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(source);
#else
        if (source == null) throw new ArgumentNullException(nameof(source));
#endif
        if (!source.CanRead) throw new ArgumentException("The data stream must be readable.", nameof(source));
    }

    private static void Append(MemoryStream destination, byte[] buffer, int count, int maximumSize)
    {
        if (destination.Length + count > maximumSize) throw new InvalidDataException("Time zone data exceeds its size limit.");
        destination.Write(buffer, 0, count);
    }

    internal static byte[] Write(byte[] index, byte[] geometry)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            WriteEntry("manifest.txt", Encoding.UTF8.GetBytes(Manifest));
            WriteEntry(IndexName, index);
            WriteEntry(GeometryName, geometry);

            void WriteEntry(string name, byte[] bytes)
            {
                var entry = archive.CreateEntry(name, name == GeometryName ? CompressionLevel.NoCompression : CompressionLevel.Optimal);
                entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
                using var stream = entry.Open();
                stream.Write(bytes, 0, bytes.Length);
            }
        }
        return output.ToArray();
    }
}
