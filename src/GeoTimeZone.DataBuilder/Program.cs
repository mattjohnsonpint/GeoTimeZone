using System.IO.Compression;
using System.Net.Http;
using System.Reflection;

namespace GeoTimeZone.DataBuilder;

internal static class Program
{
    private static async Task Main()
    {
        ConsoleOutput.Start();

        ConsoleOutput.WriteMessage("Downloading Time Zone Boundaries Shapefile");
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()[..8]);
        var filePath = await DownloadDataAsync("2023b", tempDir);

        ConsoleOutput.WriteMessage("Extracting contents...");
        ZipFile.ExtractToDirectory(filePath, tempDir);

        ConsoleOutput.WriteMessage("Processing data...");
        var shapeFile = Path.Combine(tempDir, "combined-shapefile.shp");
        var outputPath = Path.Combine(GetSolutionDir(), "src", "GeoTimeZone");
        var tzShapeReader = new TimeZoneShapeFileReader(shapeFile);
        TimeZoneDataBuilder.CreateGeohashData(tzShapeReader, outputPath);

        ConsoleOutput.WriteMessage("Done!");
        ConsoleOutput.Stop();
    }

    private static async Task<string> DownloadDataAsync(string version, string path)
    {
        var url = $"https://github.com/evansiroky/timezone-boundary-builder/releases/download/{version}/timezones.shapefile.zip";

        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        var filename = url[(url.LastIndexOf('/') + 1)..];
        var httpClient = new HttpClient();
        using var result = await httpClient.GetAsync(url);
        if (!result.IsSuccessStatusCode)
        {
            ConsoleOutput.WriteMessage($"Error downloading data file: {result.StatusCode}");
            Environment.Exit(1);
            return null;
        }

        var filePath = Path.Combine(path, filename);
        await using var fs = File.Create(filePath);
        await result.Content.CopyToAsync(fs);

        return filePath;
    }

    private static string GetSolutionDir()
    {
        var solutionDir = Assembly.GetExecutingAssembly().Location;
        while (!File.Exists(Path.Combine(solutionDir, "GeoTimeZone.sln")))
        {
            solutionDir = Path.GetFullPath(Path.Combine(solutionDir, ".."));
        }

        return solutionDir;
    }
}
