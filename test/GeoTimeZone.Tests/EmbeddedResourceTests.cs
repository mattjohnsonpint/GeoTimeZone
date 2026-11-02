using GeoTimeZone.Internal;

namespace GeoTimeZone.Tests;

public class EmbeddedResourceTests
{
    [Fact]
    public void Shipped_Resources_Are_Compatible_And_Geometry_Matches_Index_Fingerprint()
    {
        var assembly = typeof(TimeZoneLookup).Assembly;
        using var indexStream = assembly.GetManifestResourceStream(SpatialFormat.IndexResource);
        Assert.NotNull(indexStream);
        var index = SpatialIndex.Read(indexStream);
        using var geometryStream = assembly.GetManifestResourceStream(SpatialFormat.GeometryResource);
        Assert.NotNull(geometryStream);
        var bytes = SpatialFormat.Decompress(geometryStream);
        // Unlike an interior lookup, explicitly constructing geometry checks
        // its SHA256 binding and every metadata record, even if no other test
        // has yet taken a boundary path.
        var geometry = new PackedGeometry(bytes, index.GeometryHash, index.Zones.Length, index.PolygonCount);
        for (var polygon = 0; polygon < index.PolygonCount; polygon++)
            Assert.InRange(geometry.Zone(polygon), 0, index.Zones.Length - 1);
    }
}
