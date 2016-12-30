namespace GeoTimeZone.DataBuilder
{
    static class Program
    {
        static void Main()
        {
            var console = new ConsoleOutput();
            console.Start();

            var tzShapeReader = new TimeZoneShapeFileReader(@"..\..\Data\combined_shapefile.shp");

            TimeZoneDataBuilder.CreateGeohashData(console, tzShapeReader, @"..\..\src\GeoTimeZone\");
            
            console.Stop();
        }
    }
}
