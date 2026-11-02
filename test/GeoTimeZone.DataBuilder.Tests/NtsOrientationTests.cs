using NetTopologySuite.Geometries;

namespace GeoTimeZone.DataBuilder.Tests;

public class NtsOrientationTests
{
    [Fact]
    public void Agrees_With_Nts_And_Reversing_The_Edge_Reverses_The_Sign()
    {
        var random = new Random(4716);
        for (var i = 0; i < 20000; i++)
        {
            var a = new Coordinate(random.NextDouble() * 360 - 180, random.NextDouble() * 180 - 90);
            var b = new Coordinate(random.NextDouble() * 360 - 180, random.NextDouble() * 180 - 90);
            var q = new Coordinate(random.NextDouble() * 360 - 180, random.NextDouble() * 180 - 90);
            var actual = GeoTimeZone.Internal.Orientation.Sign(a.X, a.Y, b.X, b.Y, q.X, q.Y);
            Assert.Equal((int)NetTopologySuite.Algorithm.Orientation.Index(a, b, q), actual);
            Assert.Equal(-actual, GeoTimeZone.Internal.Orientation.Sign(b.X, b.Y, a.X, a.Y, q.X, q.Y));
        }
    }
}
