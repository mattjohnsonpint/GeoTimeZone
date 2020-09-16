using System.IO;

namespace GeoTimeZone.DataBuilder
{
    static class Program
    {
        static void Main()
        {
            var console = new ConsoleOutput();
            console.Start();

            string projectPath = Path.GetFullPath(".");
            while (!File.Exists(Path.Combine(projectPath, "GeoTimeZone.sln")))
                projectPath = Path.GetFullPath(Path.Combine(projectPath, ".."));

            string shapeFile = Path.Combine(projectPath, "data", "combined-shapefile.shp");
            var tzShapeReader = new TimeZoneShapeFileReader(shapeFile);

            string outputPath = Path.Combine(projectPath, "src", "GeoTimeZone");
            TimeZoneDataBuilder.CreateGeohashData(console, tzShapeReader, outputPath);
            
            console.Stop();
        }
    }
}
