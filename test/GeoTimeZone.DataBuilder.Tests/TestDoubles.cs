namespace GeoTimeZone.DataBuilder.Tests;

internal static class TestDoubles
{
    // Portable adjacent-binary64 helpers: .NET Framework lacks Math.BitIncrement.
    // Keep explicit signed-zero handling because the seam tests depend on it.
    internal static double Increment(double value)
    {
        if (double.IsNaN(value) || value == double.PositiveInfinity) return value;
        if (value == 0) return double.Epsilon;
        var bits = BitConverter.DoubleToInt64Bits(value);
        return BitConverter.Int64BitsToDouble(bits + (value > 0 ? 1 : -1));
    }

    internal static double Decrement(double value) => -Increment(-value);

    internal static double PowerOfTwo(int exponent) => exponent >= -1022
        ? BitConverter.Int64BitsToDouble((long)(exponent + 1023) << 52)
        : BitConverter.Int64BitsToDouble(1L << (exponent + 1074));
}
