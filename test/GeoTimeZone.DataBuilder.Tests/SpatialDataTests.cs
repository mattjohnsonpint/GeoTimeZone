using System.IO.Compression;
using System.Globalization;
using GeoTimeZone.Internal;
using NetTopologySuite.Geometries;

namespace GeoTimeZone.DataBuilder.Tests;

public class SpatialDataTests
{
    [Fact]
    public void Classifies_Full_Empty_And_Partial_Single_Zone_Cells_Conservatively()
    {
        var (index, geometry) = Build(new TimeZoneFeature("A", Rectangle(-2, 2, -2, 2)));
        Assert.Equal(1, index.Find(1, 1));
        Assert.Equal(0, index.Find(10, 10));
        var boundary = index.Find(2.01, 1.99);
        Assert.True(boundary < 0);
        Assert.Empty(Matches(index, geometry, 2.01, 1.99));
        Assert.Equal(new[] { "A" }, Matches(index, geometry, 1.99, 1.99));
    }

    [Fact]
    public void A_Small_Hole_Cannot_Be_Missed_By_A_Coverage_Sampling_Heuristic()
    {
        var polygon = new Polygon((LinearRing)Rectangle(0, 1, 0, 1).ExteriorRing,
            [(LinearRing)Rectangle(0.021, 0.022, 0.031, 0.032).ExteriorRing]);
        var (index, geometry) = Build(new TimeZoneFeature("A", polygon));
        Assert.True(index.Find(0.0315, 0.0215) < 0);
        Assert.Empty(Matches(index, geometry, 0.0315, 0.0215));
        Assert.Equal(new[] { "A" }, Matches(index, geometry, 0.031, 0.021));
    }

    [Fact]
    public void Does_Not_Jump_Across_A_Thin_Off_Grid_Strip_Due_To_Coordinate_Translation()
    {
        // Adding 180 to this longitude rounds to exactly 180. Without the
        // original-coordinate correction, the index would jump to B's fully
        // covered cell and never run the geometry test that could fix it.
        var (index, geometry) = Build(
            new TimeZoneFeature("A", Rectangle(-1e-15, -1e-17, -1, 1)),
            new TimeZoneFeature("B", Rectangle(0, 1, -1, 1)));
        Assert.Equal(new[] { "A" }, Matches(index, geometry, 0, -5e-17));
    }

    [Fact]
    public void Returns_True_Overlaps_And_Shared_Edges_Without_Duplicate_Zones()
    {
        var (index, geometry) = Build(
            new TimeZoneFeature("B", Rectangle(0, 1, 0, 1)),
            new TimeZoneFeature("A", Rectangle(0, 0.5, 0, 1)),
            new TimeZoneFeature("B", Rectangle(1, 2, 0, 1)));
        Assert.Equal(new[] { "A", "B" }, Matches(index, geometry, 0.25, 0.25));
        Assert.Equal(new[] { "A", "B" }, Matches(index, geometry, 0.25, 0.5));
        Assert.Equal(new[] { "B" }, Matches(index, geometry, 0.25, 1));
        Assert.Equal(new[] { "B" }, Matches(index, geometry, 0.25, 1.25));
    }

    [Fact]
    public void Never_Loses_Polygons_At_Grid_Seams_Or_World_Endpoints()
    {
        var west = new TimeZoneFeature("A", Rectangle(-180, 0, -90, 90));
        var east = new TimeZoneFeature("B", Rectangle(0, 180, -90, 90));
        var (index, geometry) = Build(west, east);
        foreach (var y in new[] { -90.0, -0.75, -0.1875, 0, 0.1875, 0.75, 90 })
        {
            Assert.Equal(new[] { "A" }, Matches(index, geometry, y, -180));
            Assert.Equal(new[] { "B" }, Matches(index, geometry, y, 180));
            Assert.Equal(new[] { "A", "B" }, Matches(index, geometry, y, 0));
            Assert.Equal(new[] { "A" }, Matches(index, geometry, y, Math.BitDecrement(0.0)));
            Assert.Equal(new[] { "B" }, Matches(index, geometry, y, Math.BitIncrement(0.0)));
        }
    }

    [Fact]
    public void Serialized_Builder_Output_Agrees_With_Nts_On_Adversarial_Synthetic_Geography()
    {
        var random = new Random(80371);
        var features = new List<TimeZoneFeature>();
        for (var i = 0; i < 16; i++)
        {
            var x = random.NextDouble() * 4 - 2;
            var y = random.NextDouble() * 4 - 2;
            // Alternating radii make simple, concave stars. Off-grid vertex
            // coordinates exercise float64 escapes as well as grid deltas.
            var points = Enumerable.Range(0, 17).Select(j =>
            {
                var angle = j * Math.PI * 2 / 17;
                var radius = j % 2 == 0 ? 0.8 : 0.3;
                return new Coordinate(x + radius * Math.Cos(angle), y + radius * Math.Sin(angle));
            }).ToList();
            points.Add(points[0].Copy());
            features.Add(new TimeZoneFeature("Zone/" + (i % 4), new Polygon(new LinearRing(points.ToArray()))));
        }
        features.Add(new TimeZoneFeature("Zone/Hole", new Polygon(
            (LinearRing)Rectangle(-1, 1, -1, 1).ExteriorRing,
            [(LinearRing)Rectangle(-0.01, 0.01, -0.02, 0.02).ExteriorRing])));
        features.Add(new TimeZoneFeature("Zone/Islands", new MultiPolygon(
            [Rectangle(-3, -2.5, -3, -2.5), Rectangle(2.5, 3, 2.5, 3)])));
        var (index, geometry) = Build(features.ToArray());
        var lazy = new Lazy<GeometryArchive>(() => geometry);

        for (var i = 0; i < 20000; i++) Check(random.NextDouble() * 8 - 4, random.NextDouble() * 8 - 4);
        foreach (var feature in features)
        {
            var vertices = feature.Geometry.Coordinates;
            for (var i = 0; i < vertices.Length; i++)
            {
                CheckNeighborhood(vertices[i].X, vertices[i].Y);
                if (i > 0) CheckNeighborhood((vertices[i - 1].X + vertices[i].X) / 2,
                    (vertices[i - 1].Y + vertices[i].Y) / 2);
            }
        }
        // Exact root/child seams and their adjacent doubles stress both the
        // certified path and candidate selection, not just point-in-polygon.
        for (var x = -16; x <= 16; x++)
        for (var y = -16; y <= 16; y++) CheckNeighborhood(x * SpatialFormat.ChildWidth, y * SpatialFormat.ChildWidth);

        void CheckNeighborhood(double x, double y)
        {
            foreach (var qx in new[] { TestDoubles.Decrement(x), x, TestDoubles.Increment(x) })
            foreach (var qy in new[] { TestDoubles.Decrement(y), y, TestDoubles.Increment(y) }) Check(qx, qy);
        }

        void Check(double x, double y)
        {
            var expected = features.Where(f => f.Geometry.Covers(new Point(x, y))).Select(f => f.TimeZoneName)
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            var result = TimeZoneLookup.Resolve(index, lazy, y, x);
            var actual = new[] { result.Result }.Concat(result.AlternativeResults).ToArray();
            if (expected.Length == 0)
            {
                Assert.Equal("UTC", result.Result); // synthetic data stays within +/-7.5 longitude
                Assert.Empty(result.AlternativeResults);
            }
            else Assert.True(expected.SequenceEqual(actual),
                $"({x:R}, {y:R}): expected {string.Join(",", expected)}, actual {string.Join(",", actual)}");
        }
    }

    [Fact]
    public void Zone_Order_And_Results_Do_Not_Depend_On_Current_Culture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            var features = new[] { "ı", "i", "İ", "I" }
                .Select(zone => new TimeZoneFeature(zone, Rectangle(0, 1, 0, 1))).ToArray();
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var english = Write(features);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            var turkish = Write(features);
            Assert.Equal(english.Index, turkish.Index);
            Assert.Equal(english.Geometry, turkish.Geometry);
            var (index, geometry) = Build(features);
            Assert.Equal(new[] { "I", "i", "İ", "ı" }, Matches(index, geometry, 0.25, 0.25));
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [Fact]
    public void Data_Is_Reproducible_And_The_Builder_Is_Reentrant()
    {
        var input = new[] { new TimeZoneFeature("A", Rectangle(-1, 1, -1, 1)) };
        var first = Write(input);
        var second = Write(input);
        Assert.Equal(first.Index, second.Index);
        Assert.Equal(first.Geometry, second.Geometry);
    }

    [Fact]
    public void Invalid_Later_Component_Cannot_Overwrite_An_Existing_Valid_Resource_Pair()
    {
        var directory = Path.Combine(Path.GetTempPath(), "GeoTimeZone.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            TimeZoneDataBuilder.CreateData([new TimeZoneFeature("A", Rectangle(-1, 1, -1, 1))], directory);
            var index = File.ReadAllBytes(Path.Combine(directory, "TZIndex.dat.gz"));
            var geometry = File.ReadAllBytes(Path.Combine(directory, "TZGeometry.dat"));
            var bowtie = new Polygon(new LinearRing([new(0, 0), new(1, 1), new(1, 0), new(0, 1), new(0, 0)]));
            Assert.Throws<InvalidDataException>(() => TimeZoneDataBuilder.CreateData(
                [new TimeZoneFeature("A", new MultiPolygon([Rectangle(-1, 1, -1, 1), bowtie]))], directory));
            Assert.Equal(index, File.ReadAllBytes(Path.Combine(directory, "TZIndex.dat.gz")));
            Assert.Equal(geometry, File.ReadAllBytes(Path.Combine(directory, "TZGeometry.dat")));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void Expands_All_Islands_And_Normalizes_Legacy_Names_Without_Duplicate_Results()
    {
        var (index, geometry) = Build(
            new TimeZoneFeature("Europe/Kiev", new MultiPolygon([Rectangle(-2, -1, 0, 1), Rectangle(1, 2, 0, 1)])),
            new TimeZoneFeature("Europe/Kyiv", Rectangle(1.5, 2.5, 0, 1)));
        Assert.Equal(new[] { "Europe/Kyiv" }, index.Zones);
        Assert.Equal(3, index.PolygonCount);
        foreach (var x in new[] { -1.5, 1.25, 1.75, 2.25 })
            Assert.Equal(new[] { "Europe/Kyiv" }, Matches(index, geometry, 0.25, x));
        Assert.Empty(Matches(index, geometry, 0.25, 0));
    }

    [Fact]
    public void Rejects_Invalid_Geometry_Rather_Than_Publishing_Incomplete_Candidates()
    {
        var bowtie = new Polygon(new LinearRing([new(0, 0), new(1, 1), new(1, 0), new(0, 1), new(0, 0)]));
        Assert.Throws<InvalidDataException>(() => Write([new TimeZoneFeature("A", bowtie)]));
    }

    [Fact]
    public void Rejects_Invalid_Child_References_And_Candidate_Order()
    {
        var roots = new int[SpatialFormat.Columns * SpatialFormat.Rows];
        roots[0] = int.MinValue;
        Assert.Throws<InvalidDataException>(() => new SpatialIndex(["A"], new byte[32], 1, roots, [], [0], []));
        roots[0] = -1;
        Assert.Throws<InvalidDataException>(() => new SpatialIndex(["A"], new byte[32], 1, roots, [], [0], []));
        var children = new int[SpatialFormat.ChildrenPerCell];
        children[0] = -1;
        Assert.Throws<InvalidDataException>(() => new SpatialIndex(["A"], new byte[32], 2, roots, children, [0, 2], [1, 0]));
    }

    [Fact]
    public void Rejects_Unknown_Index_Versions_And_Truncated_Data()
    {
        var files = Write([new TimeZoneFeature("A", Rectangle(-1, 1, -1, 1))]);
        var bytes = SpatialFormat.Decompress(new MemoryStream(files.Index));
        bytes[4] = 99;
        Assert.Throws<InvalidDataException>(() => SpatialIndex.Read(Compress(bytes)));
        Assert.ThrowsAny<IOException>(() => SpatialIndex.Read(Compress(bytes[..4])));
    }

    private static Polygon Rectangle(double minX, double maxX, double minY, double maxY) =>
        (Polygon)GeometryFactory.Default.ToGeometry(new Envelope(minX, maxX, minY, maxY));

    private static (SpatialIndex Index, GeometryArchive Geometry) Build(params TimeZoneFeature[] input)
    {
        var files = Write(input);
        var index = SpatialIndex.Read(new MemoryStream(files.Index));
        var geometry = new GeometryArchive(() => new MemoryStream(files.Geometry, false), index.GeometryHash, index.Zones.Length, index.PolygonCount);
        return (index, geometry);
    }

    private static (byte[] Index, byte[] Geometry) Write(TimeZoneFeature[] input)
    {
        var directory = Path.Combine(Path.GetTempPath(), "GeoTimeZone.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            TimeZoneDataBuilder.CreateData(input, directory);
            return (File.ReadAllBytes(Path.Combine(directory, "TZIndex.dat.gz")), File.ReadAllBytes(Path.Combine(directory, "TZGeometry.dat")));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    private static string[] Matches(SpatialIndex index, GeometryArchive geometry, double latitude, double longitude)
    {
        var result = TimeZoneLookup.Resolve(index, new Lazy<GeometryArchive>(() => geometry), latitude, longitude);
        return new[] { result.Result }.Concat(result.AlternativeResults)
            .Where(zone => Array.IndexOf(index.Zones, zone) >= 0).ToArray();
    }

    private static MemoryStream Compress(byte[] bytes)
    {
        var result = new MemoryStream();
        using (var gzip = new GZipStream(result, CompressionLevel.Optimal, true)) gzip.Write(bytes);
        result.Position = 0;
        return result;
    }
}
