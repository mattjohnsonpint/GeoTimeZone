namespace GeoTimeZone.DataBuilder
{
    static class Program
    {
        static void Main()
        {
            var console = new ConsoleOutput();
            console.Start();

            var tzShapeReader = new TimeZoneShapeFileReader(@".\Data\countries.txt", @".\Data\zone.tab", @".\Data\tz_world.shp");

            TimeZoneDataBuilder.CreateGeohashData(console, tzShapeReader, @"..\..\..\GeoTimeZone\");
            
            console.Stop();
        }
    }
}
