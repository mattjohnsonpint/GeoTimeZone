using GeoTimeZone.Internal;
using GeoTimeZone.DataBuilder.Tests;

namespace GeoTimeZone.Tests;

public class GeometryRobustnessTests
{
    [Theory]
    [InlineData(-1074)]
    [InlineData(-1022)]
    [InlineData(-500)]
    [InlineData(-140)]
    [InlineData(-10)]
    [InlineData(0)]
    public void Triangle_Coverage_Is_Exact_Even_When_Every_Determinant_Underflows(int exponent)
    {
        var step = TestDoubles.PowerOfTwo(exponent);
        foreach (var reverse in new[] { false, true })
        {
            double[] x = [0, 16 * step, 0, 0];
            double[] y = [0, 0, 16 * step, 0];
            if (reverse) { Array.Reverse(x); Array.Reverse(y); }
            var geometry = Pack([new GeometryRing(x, y)]);
            // Integer lattice oracle: no tolerance and no floating-point
            // determinant in the expected result. This includes all vertices,
            // all three edges, interior/exterior and subnormal coordinates.
            for (var ix = -1; ix <= 17; ix++)
            for (var iy = -1; iy <= 17; iy++)
                Assert.Equal(ix >= 0 && iy >= 0 && ix + iy <= 16,
                    geometry.Covers(0, iy * step, ix * step));
        }
    }

    [Theory]
    [InlineData(127)]
    [InlineData(128)]
    [InlineData(129)]
    [InlineData(257)]
    public void Many_Block_Rectangle_With_A_Hole_Matches_Analytic_Closed_Set(int edgesPerSide)
    {
        foreach (var reverse in new[] { false, true })
        {
            var shell = Rectangle(-2, 2, edgesPerSide);
            var hole = Rectangle(-1, 1, edgesPerSide);
            if (reverse)
            {
                Array.Reverse(shell.X); Array.Reverse(shell.Y);
                Array.Reverse(hole.X); Array.Reverse(hole.Y);
            }
            var geometry = Pack([shell, hole]);
            foreach (var x in new[] { -2.0, -1, 0, 1, 2 })
            foreach (var y in new[] { -2.0, -1, 0, 1, 2 })
            foreach (var qx in new[] { TestDoubles.Decrement(x), x, TestDoubles.Increment(x) })
            foreach (var qy in new[] { TestDoubles.Decrement(y), y, TestDoubles.Increment(y) })
            {
                var expected = qx >= -2 && qx <= 2 && qy >= -2 && qy <= 2
                    && !(qx > -1 && qx < 1 && qy > -1 && qy < 1);
                Assert.Equal(expected, geometry.Covers(0, qy, qx));
            }
        }
    }

    [Fact]
    public void Rejects_Every_Truncated_Geometry_Image_Without_Array_Or_Arithmetic_Failures()
    {
        var bytes = GeometryWriter.Serialize([new GeometryPolygon(0, [Rectangle(0, 1, 1)])]);
        for (var length = 0; length < bytes.Length; length++)
        {
            var truncated = bytes.Take(length).ToArray();
            var error = Record.Exception(() => new PackedGeometry(truncated, SpatialFormat.Hash(truncated), 1, 1));
            Assert.True(error is EndOfStreamException or InvalidDataException,
                $"Truncation at {length} bytes: expected a data/EOF error, got {error?.GetType().Name ?? "no error"}.");
        }
    }

    [Theory]
    // Single polygon/ring/block fixture: header=36, polygon=44, ring=40,
    // block=26 bytes. Recompute SHA256 so failures exercise structural
    // validation, rather than all stopping at the fingerprint guard.
    [InlineData(0, 0)] // magic
    [InlineData(4, 99)] // version
    [InlineData(8, 0)] // block size
    [InlineData(20, -1)] // polygon count
    [InlineData(24, int.MaxValue)] // ring count
    [InlineData(28, 0)] // block count
    [InlineData(32, int.MaxValue)] // payload length
    [InlineData(36, 1)] // zone outside dictionary
    [InlineData(72, 1)] // first ring is not contiguous
    [InlineData(76, 0)] // no rings
    [InlineData(112, 1)] // first block is not contiguous
    [InlineData(116, 0)] // no blocks
    [InlineData(136, 1)] // payload offset skips bytes
    [InlineData(140, -1)] // negative block length
    [InlineData(144, 0)] // zero edges (also changes first payload bytes)
    public void Rejects_Malformed_Metadata_Even_With_A_Matching_Fingerprint(int offset, int value)
    {
        var bytes = GeometryWriter.Serialize([new GeometryPolygon(0, [Rectangle(0, 1, 1)])]);
        using (var stream = new MemoryStream(bytes))
        using (var writer = new BinaryWriter(stream))
        {
            stream.Position = offset;
            writer.Write(value);
        }
        Assert.Throws<InvalidDataException>(() => new PackedGeometry(bytes, SpatialFormat.Hash(bytes), 1, 1));
    }

    private static PackedGeometry Pack(GeometryRing[] rings)
    {
        GeometryPolygon[] source = [new(0, rings)];
        var bytes = GeometryWriter.Serialize(source);
        var packed = new PackedGeometry(bytes, SpatialFormat.Hash(bytes), 1, 1);
        packed.ValidateCoordinates(source);
        return packed;
    }

    private static GeometryRing Rectangle(double min, double max, int edgesPerSide)
    {
        var x = new List<double>();
        var y = new List<double>();
        for (var side = 0; side < 4; side++)
        for (var edge = 0; edge < edgesPerSide; edge++)
        {
            var along = min + (max - min) * edge / edgesPerSide;
            x.Add(side == 0 ? along : side == 1 ? max : side == 2 ? -along + min + max : min);
            y.Add(side == 0 ? min : side == 1 ? along : side == 2 ? max : -along + min + max);
        }
        x.Add(x[0]); y.Add(y[0]);
        return new GeometryRing(x.ToArray(), y.ToArray());
    }
}
