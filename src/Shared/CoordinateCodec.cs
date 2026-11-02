using System.IO;

namespace GeoTimeZone.Internal;

internal static class CoordinateCodec
{
    internal static void Write(BinaryWriter writer, double value, ref int previous)
    {
        var scaled = checked((int)Math.Round(value * SpatialFormat.CoordinateScale));
        // This is a representation choice, NOT rounding the source geometry.
        // Integer encoding is permitted only when decoding produces exactly
        // the input value. Finer/off-grid values escape as float64 and leave
        // the previous integer predictor unchanged.
        if (scaled / SpatialFormat.CoordinateScale != value)
        {
            writer.Write((byte)0);
            writer.Write(value);
            return;
        }

        var delta = scaled - previous;
        // Zigzag maps signed deltas 0,-1,1,-2,... to 0,1,2,3,... . Add one
        // to reserve zero for float64 escapes. Base-128 groups carry the low
        // seven bits first; bit 7 announces another byte follows.
        var code = (uint)(((delta << 1) ^ (delta >> 31)) + 1);
        while (code >= 128)
        {
            writer.Write((byte)((code & 0x7f) | 0x80));
            code >>= 7;
        }

        writer.Write((byte)code);
        previous = scaled;
    }

    internal static double Read(byte[] image, ref int cursor, int end, ref int previous, double limit)
    {
        var code = Unsigned(image, ref cursor, end);
        double value;
        if (code == 0)
        {
            SpatialFormat.Require(end - cursor >= sizeof(double));
            // BinaryWriter stores little-endian values. Rebuild the bits
            // explicitly rather than depending on the host byte order.
            long bits = 0;
            for (var i = 0; i < 8; i++) bits |= (long)image[cursor++] << (i * 8);
            value = BitConverter.Int64BitsToDouble(bits);
        }
        else
        {
            var zigzag = code - 1;
            var delta = (zigzag >> 1) ^ -(zigzag & 1);
            previous = checked(previous + delta);
            value = previous / SpatialFormat.CoordinateScale;
        }

        SpatialFormat.Require(SpatialFormat.IsFinite(value) && value >= -limit && value <= limit);
        return value;
    }

    private static int Unsigned(byte[] image, ref int cursor, int end)
    {
        uint value = 0;
        for (var shift = 0; shift < 35; shift += 7)
        {
            SpatialFormat.Require(cursor < end);
            var next = image[cursor++];
            // Five groups suffice for our nonnegative int32 codes. The last
            // group may use only bits 0..2 and must terminate; otherwise a
            // malformed stream could overflow or consume the next block.
            SpatialFormat.Require(shift != 28 || (next & 0xf8) == 0);
            value |= (uint)(next & 0x7f) << shift;
            if ((next & 0x80) == 0) return checked((int)value);
        }

        throw new InvalidDataException("Invalid coordinate encoding.");
    }
}
