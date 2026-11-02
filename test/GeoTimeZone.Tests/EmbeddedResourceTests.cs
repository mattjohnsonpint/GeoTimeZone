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
        var geometry = new GeometryArchive(() => assembly.GetManifestResourceStream(SpatialFormat.GeometryResource)!,
            index.GeometryHash, index.Zones.Length, index.PolygonCount);
        // Validate the directory AND every independently loaded component.
        // A normal lookup intentionally leaves unused components unread.
        for (var polygon = 0; polygon < index.PolygonCount; polygon++)
        {
            Assert.InRange(geometry.Zone(polygon), 0, index.Zones.Length - 1);
            Assert.Equal(geometry.Zone(polygon), geometry.GetPolygon(polygon).Zone(0));
        }
        Assert.InRange(geometry.CachedBytes, 0, GeometryArchive.DefaultCacheBytes);
    }
}
