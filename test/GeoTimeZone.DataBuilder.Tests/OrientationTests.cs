using GeoTimeZone.Internal;
using System.Numerics;

namespace GeoTimeZone.DataBuilder.Tests;

public class OrientationTests
{
    [Theory]
    [InlineData(0, 0, 1, 0, 0, 1, 1)]
    [InlineData(0, 0, 1, 0, 0, -1, -1)]
    [InlineData(0, 0, 1, 1, 0.5, 0.5, 0)]
    [InlineData(1, 1, 1, 1, 0, 0, 0)]
    [InlineData(0, 0, 0, 0, 0, 0, 0)]
    [InlineData(-180, -90, 180, 90, 0, 0, 0)]
    [InlineData(-180, -90, 180, 90, 0, 1, 1)]
    public void Has_Expected_Sign(double ax, double ay, double bx, double by, double x, double y, int expected)
    {
        Assert.Equal(expected, GeoTimeZone.Internal.Orientation.Sign(ax, ay, bx, by, x, y));
    }

    [Fact]
    public void Resolves_Cancellation_One_Ulp_Either_Side_Of_A_Diagonal()
    {
        Assert.Equal(1, GeoTimeZone.Internal.Orientation.Sign(0, 0, 1, 1, 0.5, TestDoubles.Increment(0.5)));
        Assert.Equal(-1, GeoTimeZone.Internal.Orientation.Sign(0, 0, 1, 1, 0.5, TestDoubles.Decrement(0.5)));
        Assert.Equal(0, GeoTimeZone.Internal.Orientation.Sign(0, 0, 1, 1, 0.5, 0.5));
    }

    [Fact]
    public void Resolves_Subnormal_Determinants_That_Underflow_In_Double_Arithmetic()
    {
        var step = double.Epsilon;
        Assert.Equal(1, GeoTimeZone.Internal.Orientation.Sign(0, 0, step, 0, 0, step));
        Assert.Equal(-1, GeoTimeZone.Internal.Orientation.Sign(0, 0, 0, step, step, 0));
        Assert.Equal(0, GeoTimeZone.Internal.Orientation.Sign(-step, -step, step, step, 0, 0));
        Assert.Equal(0, GeoTimeZone.Internal.Orientation.Sign(-0.0, 0, 0, -0.0, step, step));
    }

    [Fact]
    public void Resolves_One_Ulp_Offsets_Across_Translated_Dyadic_Scales()
    {
        foreach (var origin in new[] { -64.0, -1, 0, 1, 64 })
        for (var power = 0; power <= 40; power++)
        {
            var distance = TestDoubles.PowerOfTwo(-power);
            var middle = origin + distance / 2;
            Assert.Equal(0, GeoTimeZone.Internal.Orientation.Sign(origin, origin, origin + distance, origin + distance, middle, middle));
            Assert.Equal(1, GeoTimeZone.Internal.Orientation.Sign(origin, origin, origin + distance, origin + distance, middle, TestDoubles.Increment(middle)));
            Assert.Equal(-1, GeoTimeZone.Internal.Orientation.Sign(origin, origin, origin + distance, origin + distance, middle, TestDoubles.Decrement(middle)));
        }
    }

    [Fact]
    public void Exact_Fallback_Preserves_Signs_Throughout_The_Subnormal_Exponent_Range()
    {
        for (var exponent = -1074; exponent <= 0; exponent += 17)
        {
            var distance = TestDoubles.PowerOfTwo(exponent);
            // The determinant is distance^2, strictly positive even where
            // multiplication in binary64 rounds that product down to zero.
            Assert.Equal(1, GeoTimeZone.Internal.Orientation.Sign(0, 0, distance, 0, 0, distance));
            Assert.Equal(-1, GeoTimeZone.Internal.Orientation.Sign(0, 0, 0, distance, distance, 0));
        }
    }

    [Fact]
    public void Filtered_Predicate_Agrees_With_Independent_Fixed_Scale_Integer_Oracle()
    {
        var random = new Random(93617);
        for (var i = 0; i < 5000; i++)
        {
            var ax = Next(); var ay = Next();
            var bx = Next(); var by = Next();
            var x = Next(); var y = Next();
            // Include exact and nearly collinear cases, not just easily
            // separated points. Mixing exponents stresses cancellation and
            // underflow in the floating-point filter.
            if (i % 3 == 0) { bx = ax; by = ay; }
            if (i % 3 == 1) { ay = ax; by = bx; y = TestDoubles.Increment(x); }
            var expected = ((Fixed(ax) - Fixed(x)) * (Fixed(by) - Fixed(y))
                - (Fixed(ay) - Fixed(y)) * (Fixed(bx) - Fixed(x))).Sign;
            Assert.Equal(expected, Orientation.Sign(ax, ay, bx, by, x, y));
            Assert.Equal(-expected, Orientation.Sign(bx, by, ax, ay, x, y));
            Assert.Equal(expected, Orientation.Sign(x, y, ax, ay, bx, by));
        }

        double Next() => (random.NextDouble() * 2 - 1) * TestDoubles.PowerOfTwo(random.Next(-1074, 8));
    }

    private static BigInteger Fixed(double value)
    {
        // Independent oracle: represent EVERY double on the fixed 2^-1074
        // lattice, rather than choosing the production predicate's smallest
        // exponent. Normal significands shift by biasedExponent-1; subnormal
        // fractions already count units of 2^-1074. No floating arithmetic.
        var bits = unchecked((ulong)BitConverter.DoubleToInt64Bits(value));
        var exponent = (int)((bits >> 52) & 2047);
        var fraction = bits & ((1UL << 52) - 1);
        var integer = exponent == 0 ? new BigInteger(fraction)
            : new BigInteger(fraction | (1UL << 52)) << (exponent - 1);
        return (bits >> 63) == 0 ? integer : -integer;
    }
}
