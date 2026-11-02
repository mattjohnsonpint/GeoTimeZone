using GeoTimeZone.Internal;

namespace GeoTimeZone.DataBuilder.Tests;

public class CoordinateCodecTests
{
    [Fact]
    public void Round_Trips_Signed_Deltas_Across_Base128_Length_Boundaries()
    {
        var values = new List<double> { 0, -0.0, 180, -180, 90, -90 };
        foreach (var units in new[] { 1, 63, 64, 127, 128, 8191, 8192, 1048575, 1048576, 134217727, 134217728 })
        {
            values.Add(units / SpatialFormat.CoordinateScale);
            values.Add(-units / SpatialFormat.CoordinateScale);
            values.Add(0);
        }

        AssertRoundTrip(values);
    }

    [Fact]
    public void Float64_Escapes_Do_Not_Change_The_Integer_Predictor()
    {
        AssertRoundTrip([1, TestDoubles.Increment(1.0), 1.000001, Math.PI, -1, -Math.PI, -1.000001, double.Epsilon, 0]);
    }

    [Fact]
    public void Round_Trips_Random_Source_Grid_And_Off_Grid_Values()
    {
        var random = new Random(128);
        var values = Enumerable.Range(0, 10000).Select(i => i % 2 == 0
            ? random.Next(-180000000, 180000001) / SpatialFormat.CoordinateScale
            : random.NextDouble() * 360 - 180).ToArray();
        AssertRoundTrip(values);
    }

    [Theory]
    [InlineData(new byte[] { })]
    [InlineData(new byte[] { 128 })]
    [InlineData(new byte[] { 255, 255, 255, 255, 8 })]
    [InlineData(new byte[] { 128, 128, 128, 128, 128, 0 })]
    [InlineData(new byte[] { 0, 1, 2, 3 })]
    public void Rejects_Truncated_Overflowing_Or_Unterminated_Values(byte[] bytes)
    {
        var cursor = 0;
        var previous = 0;
        Assert.Throws<InvalidDataException>(() => CoordinateCodec.Read(bytes, ref cursor, bytes.Length, ref previous, 180));
    }

    [Fact]
    public void Rejects_Nonfinite_And_Out_Of_Range_Decoded_Coordinates()
    {
        foreach (var value in new[] { double.NaN, double.NegativeInfinity, double.PositiveInfinity, -181, 181 })
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
            {
                writer.Write((byte)0);
                writer.Write(value);
            }

            var bytes = stream.ToArray();
            int cursor = 0, previous = 0;
            Assert.Throws<InvalidDataException>(() => CoordinateCodec.Read(bytes, ref cursor, bytes.Length, ref previous, 180));
        }
    }

    [Fact]
    public void Preserves_Arbitrary_Finite_Bit_Patterns_Across_The_Geographic_Range()
    {
        var random = new Random(65023);
        var values = new List<double>();
        var bits = new byte[8];
        while (values.Count < 10000)
        {
            random.NextBytes(bits);
            var value = BitConverter.ToDouble(bits, 0);
            if (!double.IsNaN(value) && value >= -180 && value <= 180) values.Add(value);
        }
        AssertRoundTrip(values);
    }

    [Fact]
    public void Every_Truncation_Of_A_Multibyte_Value_Is_Rejected()
    {
        foreach (var value in new[] { 180.0, -180, Math.PI, double.Epsilon })
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
            {
                var predictor = 0;
                CoordinateCodec.Write(writer, value, ref predictor);
            }
            var bytes = stream.ToArray();
            for (var end = 0; end < bytes.Length; end++)
            {
                int cursor = 0, predictor = 0;
                var boundary = end;
                Assert.Throws<InvalidDataException>(() => CoordinateCodec.Read(bytes, ref cursor, boundary, ref predictor, 180));
                Assert.True(cursor <= boundary);
            }
        }
    }

    [Fact]
    public void Cannot_Read_Into_The_Next_Independent_Block()
    {
        byte[] bytes = [128, 1];
        var cursor = 0;
        var previous = 0;
        Assert.Throws<InvalidDataException>(() => CoordinateCodec.Read(bytes, ref cursor, 1, ref previous, 180));
    }

    private static void AssertRoundTrip(IEnumerable<double> input)
    {
        var values = input.ToArray();
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
        {
            var previous = 0;
            foreach (var value in values) CoordinateCodec.Write(writer, value, ref previous);
        }

        var bytes = stream.ToArray();
        int cursor = 0, predictor = 0;
        foreach (var expected in values) Assert.Equal(expected, CoordinateCodec.Read(bytes, ref cursor, bytes.Length, ref predictor, 180));
        Assert.Equal(bytes.Length, cursor);
    }
}
