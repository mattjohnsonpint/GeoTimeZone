namespace GeoTimeZone.DataBuilder;

internal static class Program
{
    private static void Main()
    {
        ConsoleOutput.Start();

        var projectPath = Path.GetFullPath(".");
        while (!File.Exists(Path.Combine(projectPath, "GeoTimeZone.sln")))
        {
            projectPath = Path.GetFullPath(Path.Combine(projectPath, ".."));
        }

        var shapeFile = Path.Combine(projectPath, "data", "combined-shapefile.shp");
        var tzShapeReader = new TimeZoneShapeFileReader(shapeFile);

        var outputPath = Path.Combine(projectPath, "src", "GeoTimeZone");
        TimeZoneDataBuilder.CreateGeohashData(tzShapeReader, outputPath);

        ConsoleOutput.Stop();
    }
}
