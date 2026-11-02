using System.IO;
using System.Text;

namespace GeoTimeZone.Internal;

internal sealed class GeometryRing(double[] x, double[] y)
{
    internal double[] X { get; } = x;
    internal double[] Y { get; } = y;
}

internal sealed class GeometryPolygon(int zone, GeometryRing[] rings)
{
    internal int Zone { get; } = zone;
    internal GeometryRing[] Rings { get; } = rings;
}

internal static class GeometryWriter
{
    internal static byte[] Serialize(GeometryPolygon[] polygons)
    {
        using var polygonData = new MemoryStream();
        using var ringData = new MemoryStream();
        using var blockData = new MemoryStream();
        using var payload = new MemoryStream();
        using var polygonWriter = new BinaryWriter(polygonData, Encoding.UTF8, true);
        using var ringWriter = new BinaryWriter(ringData, Encoding.UTF8, true);
        using var blockWriter = new BinaryWriter(blockData, Encoding.UTF8, true);
        using var coordinates = new BinaryWriter(payload, Encoding.UTF8, true);
        var ringCount = 0;
        var blockCount = 0;
        foreach (var polygon in polygons)
        {
            if (polygon.Zone < 0 || polygon.Rings.Length == 0) throw new ArgumentException("Invalid polygon.", nameof(polygons));
            polygonWriter.Write(polygon.Zone);
            WriteBounds(polygonWriter, polygon.Rings[0]);
            polygonWriter.Write(ringCount);
            polygonWriter.Write(polygon.Rings.Length);
            foreach (var ring in polygon.Rings)
            {
                Validate(ring);
                var edges = ring.X.Length - 1;
                WriteBounds(ringWriter, ring);
                ringWriter.Write(blockCount);
                ringWriter.Write((edges + SpatialFormat.BlockSize - 1) / SpatialFormat.BlockSize);
                ringCount++;
                for (var start = 0; start < edges; start += SpatialFormat.BlockSize)
                {
                    var end = Math.Min(start + SpatialFormat.BlockSize, edges);
                    var min = ring.Y[start];
                    var max = ring.Y[start];
                    var offset = checked((int)payload.Position);
                    int previousX = 0, previousY = 0;
                    // Include the bridging vertex: this block owns every edge
                    // leaving its first 128 vertices, including its last edge.
                    for (var i = start; i <= end; i++)
                    {
                        min = Math.Min(min, ring.Y[i]);
                        max = Math.Max(max, ring.Y[i]);
                        CoordinateCodec.Write(coordinates, ring.X[i], ref previousX);
                        CoordinateCodec.Write(coordinates, ring.Y[i], ref previousY);
                    }

                    blockWriter.Write(min);
                    blockWriter.Write(max);
                    blockWriter.Write(offset);
                    blockWriter.Write(checked((int)payload.Position - offset));
                    blockWriter.Write(checked((ushort)(end - start)));
                    blockCount++;
                }
            }
        }

        using var result = new MemoryStream();
        using var writer = new BinaryWriter(result, Encoding.UTF8, true);
        writer.Write(SpatialFormat.GeometryMagic);
        writer.Write(SpatialFormat.Version);
        writer.Write(SpatialFormat.BlockSize);
        writer.Write(SpatialFormat.CoordinateScale);
        writer.Write(polygons.Length);
        writer.Write(ringCount);
        writer.Write(blockCount);
        writer.Write(checked((int)payload.Length));
        polygonData.WriteTo(result);
        ringData.WriteTo(result);
        blockData.WriteTo(result);
        payload.WriteTo(result);
        return result.ToArray();
    }

    private static void Validate(GeometryRing ring)
    {
        if (ring.X.Length < 4 || ring.X.Length != ring.Y.Length || ring.X[0] != ring.X[ring.X.Length - 1]
            || ring.Y[0] != ring.Y[ring.Y.Length - 1]) throw new ArgumentException("Polygon rings must be closed.", nameof(ring));
        for (var i = 0; i < ring.X.Length; i++)
            if (!SpatialFormat.IsFinite(ring.X[i]) || !SpatialFormat.IsFinite(ring.Y[i]) || Math.Abs(ring.X[i]) > 180 || Math.Abs(ring.Y[i]) > 90)
                throw new ArgumentException("Invalid polygon coordinates.", nameof(ring));
    }

    private static void WriteBounds(BinaryWriter writer, GeometryRing ring)
    {
        Validate(ring);
        writer.Write(ring.X.Min());
        writer.Write(ring.X.Max());
        writer.Write(ring.Y.Min());
        writer.Write(ring.Y.Max());
    }

}
