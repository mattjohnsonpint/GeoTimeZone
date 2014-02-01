namespace GeoTimeZone.DataBuilder
{
    static class Program
    {
        static void Main()
        {
            var console = new ConsoleOutput();
            console.Start();
            TimeZoneDataBuilder.CreateGeohashData(console, @".\Data\tz_world.shp", @"..\..\..\GeoTimeZone\");
            console.Stop();
        }
    }
}
