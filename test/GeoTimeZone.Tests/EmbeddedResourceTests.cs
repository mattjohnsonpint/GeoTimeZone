using GeoTimeZone.Internal;

namespace GeoTimeZone.Tests;

public class EmbeddedResourceTests
{
    [Fact]
    public void Shipped_Resources_Are_Compatible_And_Geometry_Matches_Index_Fingerprint()
    {
        var data = DataBundle.ReadEmbedded();
        var index = data.Index;
        var geometry = data.Geometry.Value;
        // Validate the directory AND every independently loaded component.
        // A normal lookup intentionally leaves unused components unread.
        for (var polygon = 0; polygon < index.PolygonCount; polygon++)
        {
            Assert.InRange(geometry.Zone(polygon), 0, index.Zones.Length - 1);
            Assert.Equal(geometry.Zone(polygon), geometry.GetPolygon(polygon).Zone(0));
            geometry.GetPolygon(polygon).ValidateEncoding(CancellationToken.None);
        }
        Assert.InRange(geometry.CachedBytes, 0, GeometryArchive.DefaultCacheBytes);
    }
}
