using System.Numerics;

namespace GeoTimeZone.Internal;

internal static class Orientation
{
    internal static int Sign(double ax, double ay, double bx, double by, double x, double y)
    {
        // orient(A, B, Q) = (Ax-Qx)(By-Qy) - (Ay-Qy)(Bx-Qx).
        // Its sign says which side of directed edge A->B contains Q; zero
        // means collinear. Translate to Q before multiplying to reduce error.
        var left = (ax - x) * (by - y);
        var right = (ay - y) * (bx - x);
        var determinant = left - right;
        // IEEE binary64 unit roundoff u = 2^-53. The first-stage orient2d
        // error bound is (3 + 16u)u times the sum of product magnitudes
        // (Shewchuk, "Adaptive Precision Floating-Point Arithmetic and Fast
        // Robust Geometric Predicates", 1997). Only a determinant strictly
        // outside that bound is trusted. Cancellation, zero, and underflow
        // fall through to exact arithmetic, not an arbitrary epsilon test.
        var magnitude = Math.Abs(left) + Math.Abs(right);
        var error = magnitude * 3.3306690738754716e-16;
        // The relative-error proof assumes no underflow in its bound. Route
        // extremely small products to exact arithmetic as well; at 1e-280
        // the error bound remains comfortably above the normal-double floor.
        if (magnitude >= 1e-280 && Math.Abs(determinant) > error) return determinant > 0 ? 1 : -1;

        // Resolve uncertain floating-point determinants exactly. Each finite
        // double is an integer times a power of two, so no tolerance or border
        // displacement is needed, including for points exactly on an edge.
        var exponent = Math.Min(Math.Min(Exponent(ax), Exponent(ay)),
            Math.Min(Math.Min(Exponent(bx), Exponent(by)), Math.Min(Exponent(x), Exponent(y))));
        var px = Integer(x, exponent);
        var py = Integer(y, exponent);
        return ((Integer(ax, exponent) - px) * (Integer(by, exponent) - py)
            - (Integer(ay, exponent) - py) * (Integer(bx, exponent) - px)).Sign;
    }

    private static int Exponent(double value)
    {
        // A normal double is (+/-)(2^52 + fraction) * 2^(biasedExponent-1075).
        // Subnormals have no implicit leading bit and use exponent -1074.
        var bits = BitConverter.DoubleToInt64Bits(value);
        // Zero is divisible by every power of two: do not let it force all
        // other coordinates onto the subnormal (-1074) grid unnecessarily.
        if ((bits & 0x7fffffffffffffffL) == 0) return int.MaxValue;
        var exponent = (int)((bits >> 52) & 0x7ff);
        return exponent == 0 ? -1074 : exponent - 1075;
    }

    private static BigInteger Integer(double value, int exponent)
    {
        var bits = BitConverter.DoubleToInt64Bits(value);
        if ((bits & 0x7fffffffffffffffL) == 0) return BigInteger.Zero;
        var mantissa = bits & 0x000fffffffffffffL;
        if (((bits >> 52) & 0x7ff) != 0) mantissa |= 1L << 52;
        var integer = new BigInteger(bits < 0 ? -mantissa : mantissa);
        // Align all six coordinates to the smallest binary exponent. The
        // shift is nonnegative and preserves every bit; the determinant's
        // common power-of-two factor is positive and cannot change its sign.
        return integer << (Exponent(value) - exponent);
    }
}
