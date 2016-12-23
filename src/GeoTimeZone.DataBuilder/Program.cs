namespace GeoTimeZone.DataBuilder
{
    static class Program
    {
        static void Main()
        {
            var console = new ConsoleOutput();
            console.Start();

            var tzShapeReader = new TimeZoneShapeFileReader(@"..\..\Data\tz_world.shp");

            TimeZoneDataBuilder.CreateGeohashData(console, tzShapeReader, @"..\..\src\GeoTimeZone\");
            
            console.Stop();
        }
    }
}
