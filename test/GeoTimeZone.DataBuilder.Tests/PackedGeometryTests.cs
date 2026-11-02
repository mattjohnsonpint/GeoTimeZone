using GeoTimeZone.Internal;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Prepared;

namespace GeoTimeZone.DataBuilder.Tests;

public class PackedGeometryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Matches_Closed_Polygon_And_Hole_Edges_In_Either_Winding(bool reverse)
    {
        var shell = Ring((-2, -2), (2, -2), (2, 2), (-2, 2), (-2, -2));
        var hole = Ring((-1, -1), (-1, 1), (1, 1), (1, -1), (-1, -1));
        var polygon = new Polygon(reverse ? (LinearRing)shell.Reverse() : shell,
            [reverse ? (LinearRing)hole.Reverse() : hole]);
        var packed = Pack(polygon);
        for (var xi = -9; xi <= 9; xi++)
        for (var yi = -9; yi <= 9; yi++)
            Assert.Equal(polygon.Covers(new Point(xi / 4.0, yi / 4.0)), packed.Covers(0, yi / 4.0, xi / 4.0));
        Assert.True(packed.Covers(0, 0, 1)); // hole boundary belongs to the polygon
        Assert.False(packed.Covers(0, 0, 0)); // hole interior does not
        Assert.False(packed.Covers(0, 0, Math.BitDecrement(1.0)));
        Assert.True(packed.Covers(0, 0, Math.BitIncrement(1.0)));
        Assert.True(packed.Covers(0, 0, 2));
        Assert.False(packed.Covers(0, 0, Math.BitIncrement(2.0)));
    }

    [Fact]
    public void Handles_Concavity_Horizontal_Edges_Vertices_And_Repeated_Vertices()
    {
        var polygon = new Polygon(Ring((0, 0), (3, 0), (3, 3), (2, 3), (2, 1), (2, 1), (1, 1), (1, 3), (0, 3), (0, 0)));
        var packed = Pack(polygon);
        for (var xi = -1; xi <= 13; xi++)
        for (var yi = -1; yi <= 13; yi++)
            Assert.Equal(polygon.Covers(new Point(xi / 4.0, yi / 4.0)), packed.Covers(0, yi / 4.0, xi / 4.0));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(127)]
    [InlineData(128)]
    [InlineData(129)]
    [InlineData(255)]
    [InlineData(256)]
    [InlineData(257)]
    [InlineData(1025)]
    public void Block_Bridges_And_Closing_Edges_Agree_With_Unblocked_Nts(int edges)
    {
        // Non-grid circle coordinates also exercise float64 escapes mixed
        // with integer deltas. A new block must not inherit the last block's
        // predictor, and its final edge must include the bridging vertex.
        var points = Enumerable.Range(0, edges).Select(i =>
            new Coordinate(Math.Cos(i * Math.PI * 2 / edges), Math.Sin(i * Math.PI * 2 / edges))).ToList();
        points.Add(points[0].Copy());
        var polygon = new Polygon(new LinearRing(points.ToArray()));
        var prepared = PreparedGeometryFactory.Prepare(polygon);
        var packed = Pack(polygon);
        var random = new Random(edges);
        foreach (var vertex in points) Check(vertex.X, vertex.Y);
        for (var i = 0; i < 2000; i++) Check(random.NextDouble() * 3 - 1.5, random.NextDouble() * 3 - 1.5);
        for (var i = 0; i < edges; i++)
        {
            var x = (points[i].X + points[i + 1].X) / 2;
            var y = (points[i].Y + points[i + 1].Y) / 2;
            Check(x, y);
            Check(Math.BitIncrement(x), Math.BitIncrement(y));
            Check(Math.BitDecrement(x), Math.BitDecrement(y));
        }

        return;
        void Check(double x, double y) => Assert.Equal(prepared.Covers(new Point(x, y)), packed.Covers(0, y, x));
    }

    [Fact]
    public void Does_Not_Round_Off_Grid_Boundaries_Onto_The_Integer_Grid()
    {
        var west = Math.BitIncrement(1.0);
        var polygon = new Polygon(Ring((west, 0), (2, 0), (2, 1), (west, 1), (west, 0)));
        var packed = Pack(polygon);
        Assert.False(packed.Covers(0, 0.5, 1));
        Assert.True(packed.Covers(0, 0.5, west));
        Assert.True(packed.Covers(0, 0.5, Math.BitIncrement(west)));
    }

    [Fact]
    public void Preserves_Dateline_And_Polar_Coordinates()
    {
        var polygon = new Polygon(Ring((-180, -90), (180, -90), (180, 90), (-180, 90), (-180, -90)));
        var packed = Pack(polygon);
        foreach (var x in new[] { -180.0, 0, 180 })
        foreach (var y in new[] { -90.0, 0, 90 }) Assert.True(packed.Covers(0, y, x));
    }

    [Fact]
    public void Rejects_Damaged_Mixed_Or_Truncated_Geometry()
    {
        var polygon = new Polygon(Ring((0, 0), (1, 0), (1, 1), (0, 0)));
        var bytes = Encode(polygon);
        var hash = SpatialFormat.Hash(bytes);
        var damaged = (byte[])bytes.Clone();
        damaged[damaged.Length - 1] ^= 1;
        Assert.Throws<InvalidDataException>(() => new PackedGeometry(damaged, hash, 1, 1));
        Assert.Throws<InvalidDataException>(() => new PackedGeometry(bytes, new byte[32], 1, 1));
        var truncated = bytes[..^1];
        Assert.Throws<InvalidDataException>(() => new PackedGeometry(truncated, SpatialFormat.Hash(truncated), 1, 1));
        var incompatible = (byte[])bytes.Clone();
        incompatible[4] = 99;
        Assert.Throws<InvalidDataException>(() => new PackedGeometry(incompatible, SpatialFormat.Hash(incompatible), 1, 1));
        Assert.Throws<InvalidDataException>(() => new PackedGeometry(bytes, hash, 1, 2));
        var wrongScale = (byte[])bytes.Clone();
        BitConverter.GetBytes(SpatialFormat.CoordinateScale * 10).CopyTo(wrongScale, 12);
        Assert.Throws<InvalidDataException>(() => new PackedGeometry(wrongScale, SpatialFormat.Hash(wrongScale), 1, 1));
    }

    [Fact]
    public void Exhaustive_Round_Trip_Check_Rejects_Any_Changed_Source_Vertex()
    {
        var ring = new GeometryRing([0, 1, 1, 0], [0, 0, 1, 0]);
        GeometryPolygon[] source = [new(0, [ring])];
        var bytes = GeometryWriter.Serialize(source);
        var packed = new PackedGeometry(bytes, SpatialFormat.Hash(bytes), 1, 1);
        packed.ValidateCoordinates(source);
        ring.X[1] = Math.BitIncrement(ring.X[1]);
        Assert.Throws<InvalidDataException>(() => packed.ValidateCoordinates(source));
    }

    [Fact]
    public void Rejects_Invalid_Rings_Instead_Of_Silently_Dropping_Them()
    {
        Assert.Throws<ArgumentException>(() => GeometryWriter.Serialize([new GeometryPolygon(0, [new GeometryRing([0, 1, 2], [0, 1, 2])])]));
        Assert.Throws<ArgumentException>(() => GeometryWriter.Serialize([new GeometryPolygon(0, [new GeometryRing([0, 1, double.NaN, 0], [0, 0, 1, 0])])]));
    }

    private static LinearRing Ring(params (double X, double Y)[] points) => new(points.Select(p => new Coordinate(p.X, p.Y)).ToArray());

    private static PackedGeometry Pack(Polygon polygon)
    {
        var bytes = Encode(polygon);
        return new PackedGeometry(bytes, SpatialFormat.Hash(bytes), 1, 1);
    }

    private static byte[] Encode(Polygon polygon) => GeometryWriter.Serialize([new GeometryPolygon(0,
        new[] { polygon.ExteriorRing }.Concat(polygon.InteriorRings).Select(r => new GeometryRing(
            r.Coordinates.Select(c => c.X).ToArray(), r.Coordinates.Select(c => c.Y).ToArray())).ToArray())]);
}
