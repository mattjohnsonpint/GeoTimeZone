using GeoTimeZone.Internal;

namespace GeoTimeZone.Tests;

public class LookupRobustnessTests
{
    [Fact]
    public void Deduplicates_Three_Overlapping_Zones_In_Ordinal_Order_Despite_Nonmatching_Components()
    {
        // Every zone has a miss before and after covering components. A second
        // covering component must not duplicate the zone or reset aggregation.
        var polygons = Enumerable.Range(0, 3).SelectMany(zone => new[]
        {
            Rectangle(zone, 0.6, 0.7), Rectangle(zone, 0, 0.5),
            Rectangle(zone, 0.1, 0.4), Rectangle(zone, 0.6, 0.7)
        }).ToArray();
        var (index, geometry) = CandidateCell(["A", "B", "a"], polygons);
        for (var i = 0; i < 100; i++)
        {
            var result = TimeZoneLookup.Resolve(index, geometry, 0.25, 0.25);
            Assert.Equal("A", result.Result);
            Assert.Equal(new[] { "B", "a" }, result.AlternativeResults);
            Assert.Throws<NotSupportedException>(() => ((IList<string>)result.AlternativeResults).Clear());
        }
    }

    [Theory]
    [InlineData(0.25, "A", 2)] // all three overlap
    [InlineData(0.5, "A", 2)] // closed shared boundary
    [InlineData(0.625, "a", 0)] // first two candidates miss
    [InlineData(0.74, "UTC", 0)] // candidates exist, but none covers
    public void Selects_Only_Actual_Matches_Or_Nautical_Fallback(double longitude, string primary, int alternatives)
    {
        var (index, geometry) = CandidateCell(["A", "B", "a"],
            [Rectangle(0, 0, 0.5), Rectangle(1, 0, 0.5), Rectangle(2, 0, 0.7)]);
        var result = TimeZoneLookup.Resolve(index, geometry, 0.25, longitude);
        Assert.Equal(primary, result.Result);
        Assert.Equal(alternatives, result.AlternativeResults.Count);
    }

    [Theory]
    [InlineData(0, "UTC")]
    [InlineData(1, "A")]
    public void Empty_And_Certified_Cells_Never_Load_Geometry(int value, string expected)
    {
        var roots = Enumerable.Repeat(value, SpatialFormat.Columns * SpatialFormat.Rows).ToArray();
        var index = new SpatialIndex(["A"], new byte[32], 1, roots, [], [0], []);
        var geometry = new Lazy<PackedGeometry>(() => throw new InvalidOperationException("Geometry must stay lazy."));
        Assert.Equal(expected, TimeZoneLookup.Resolve(index, geometry, 0.25, 0.25).Result);
        Assert.False(geometry.IsValueCreated);
    }

    [Fact]
    public void Concurrent_First_Boundary_Lookups_Publish_Geometry_Exactly_Once()
    {
        var (index, source) = CandidateCell(["A", "B"], [Rectangle(0, 0, 0.5), Rectangle(1, 0, 0.5)]);
        var loads = 0;
        var geometry = new Lazy<PackedGeometry>(() =>
        {
            Interlocked.Increment(ref loads);
            return source.Value;
        });
        Parallel.For(0, 1000, _ =>
        {
            var result = TimeZoneLookup.Resolve(index, geometry, 0.25, 0.25);
            Assert.Equal("A", result.Result);
            Assert.Equal(new[] { "B" }, result.AlternativeResults);
        });
        Assert.Equal(1, loads);
    }

    private static GeometryPolygon Rectangle(int zone, double west, double east) => new(zone,
        [new GeometryRing([west, east, east, west, west], [0, 0, 0.75, 0.75, 0])]);

    private static (SpatialIndex Index, Lazy<PackedGeometry> Geometry) CandidateCell(string[] zones, GeometryPolygon[] polygons)
    {
        var bytes = GeometryWriter.Serialize(polygons);
        var hash = SpatialFormat.Hash(bytes);
        // All roots share one refined block; all its children share the full
        // candidate list. Queries deliberately stay within the synthetic cell.
        var index = new SpatialIndex(zones, hash, polygons.Length,
            Enumerable.Repeat(-1, SpatialFormat.Columns * SpatialFormat.Rows).ToArray(),
            Enumerable.Repeat(-1, SpatialFormat.ChildrenPerCell).ToArray(),
            [0, polygons.Length], Enumerable.Range(0, polygons.Length).ToArray());
        return (index, new Lazy<PackedGeometry>(() => new PackedGeometry(bytes, hash, zones.Length, polygons.Length)));
    }
}
