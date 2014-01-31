namespace GeoTimeZone.DataBuilder
{
    static class Program
    {
        static void Main()
        {
            TimeZoneDataBuilder.CreateGeohashData(@".\Data\tz_world.shp", @"..\..\..\GeoTimeZone\");
        }
    }
}
