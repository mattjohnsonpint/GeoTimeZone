using System.IO.Compression;
using NetTopologySuite.Geometries;

namespace GeoTimeZone.DataBuilder;

public static class TimeZoneDataBuilder
{
    private static readonly GeohashTree GeohashTree = new();
    private static readonly TimeZoneTreeNode WorldBoundsTreeNode = new();
    private static readonly Dictionary<string, TimeZoneMeta> TimeZones = new();

    private const string DataFileName = "TZ.dat.gz";
    private const string LookupFileName = "TZL.dat.gz";

    private static void WriteLookup(string outputPath)
    {

        var path = Path.Combine(outputPath, LookupFileName);

        using var fileStream = File.Create(path);
        using var compressedStream = new GZipStream(fileStream, CompressionMode.Compress);
        using var writer = new StreamWriter(compressedStream) {NewLine = "\n"};

        var timeZones = TimeZones.Values.OrderBy(x => x.LineNumber);
        foreach (var timeZone in timeZones)
        {
            writer.WriteLine(timeZone.IanaTimeZoneId);
        }
    }

    private static void WriteGeohashDataFile(string outputPath)
    {
        var path = Path.Combine(outputPath, DataFileName);

        using var fileStream = File.Create(path);
        using var compressedStream = new GZipStream(fileStream, CompressionMode.Compress);
        using var writer = new StreamWriter(compressedStream) {NewLine = "\n"};
        WriteTreeNode(writer, WorldBoundsTreeNode);
    }

    private static void WriteGeohash(TextWriter writer, string tz, string geohash)
    {
        var h = geohash.PadRight(GeohashTree.Precision, '-');
        var p = TimeZones[tz].LineNumber.ToString("D3");
        writer.WriteLine(h + p);
    }

    private static void WriteTreeNode(TextWriter writer, TimeZoneTreeNode node, string geohash = "")
    {
        var childNodes = node.ChildNodes
            .OrderBy(x => x.Key);

        foreach (var (childChar, childNode) in childNodes)
        {
            var childHash = geohash + childChar;
            if (childNode.TimeZones.Count > 0)
            {
                var timeZones = childNode.TimeZones.GroupBy(x => x.Feature.TimeZoneName)
                    .OrderByDescending(x => x.Sum(y => y.PctOfNode))
                    .Select(x => x.Key);

                foreach (var timeZone in timeZones)
                {
                    WriteGeohash(writer, timeZone, childHash);
                }
            }
            else if (childNode.ChildNodes.Count > 0)
            {
                WriteTreeNode(writer, childNode, childHash);
            }
        }
    }

    private static double CalculatePctOfNode(TimeZoneFeature feature, GeohashTreeNode node)
    {
        var preparedGeometry = feature.PreparedGeometry;
        var nodeGeometry = node.GetGeometry();
        if (!preparedGeometry.Intersects(nodeGeometry))
        {
            return 0;
        }

        if (preparedGeometry.Contains(nodeGeometry))
        {
            return 1;
        }

        // Area is too expensive, and we only need an approximation.
        // Test a set of evenly distributed points within the envelope.
        var points = GetTestPoints(node.Envelope);
        var hits = points.Count(p => preparedGeometry.Contains(p));
        return (double) hits / points.Length;

        // Here's how we can do it with area, but it's very slow.
        // return feature.Geometry.Intersection(nodeGeometry).Area / node.Envelope.Area;
    }

    private static Point[] GetTestPoints(Envelope envelope)
    {
        const int step = 5;
        var stepX = envelope.Width / (step - 1);
        var stepY = envelope.Height / (step - 1);

        var i = 0;
        var points = new Point[step * step];
        for (var x = envelope.MinX; x <= envelope.MaxX; x += stepX)
        {
            for (var y = envelope.MinY; y <= envelope.MaxY; y += stepY)
            {
                points[i] = new Point(x, y);
                i++;
            }
        }

        return points;
    }

    private static void AddResult(string geohash, TimeZoneFeature feature)
    {
        var currentNode = WorldBoundsTreeNode;

        for (var i = 0; i < geohash.Length; i++)
        {
            var geohashChar = geohash[i];
            if (!currentNode.ChildNodes.TryGetValue(geohashChar, out var childNode))
            {
                childNode = currentNode.ChildNodes[geohashChar] = new TimeZoneTreeNode();
            }

            currentNode = childNode;

            if (i == geohash.Length - 1)
            {
                var node = GeohashTree.GetTreeNode(geohash)!;
                var pct = CalculatePctOfNode(feature, node);
                currentNode.TimeZones.Add((feature, pct));
            }
        }
    }

    public static void CreateGeohashData(TimeZoneShapeFileReader inputShapefile, string outputPath)
    {
        ConsoleOutput.WriteMessage("Loading polygons...");

        var features = inputShapefile.ReadShapeFile()
            .SelectMany(x =>
            {
                if (x.Geometry is MultiPolygon mp)
                {
                    // Expand MultiPolygons to individual Polygons
                    return mp.Geometries.Select((geometry, index) =>
                        new TimeZoneFeature(x.TimeZoneName, geometry, index));
                }

                return new[] {x};
            })
            .Where(x => x.Geometry.Area > 0)
            .OrderBy(x => x.TimeZoneName).ThenBy(x => x.MultiPolyIndex)
            .ToList();

        PreLoadTimeZones(features);

        ConsoleOutput.WriteMessage("Polygons loaded.");

        var featuresWithGeohashes = features
            .AsParallel()
            .Select(feature =>
            {
                var indexString = feature.MultiPolyIndex >= 0 ? $"[{feature.MultiPolyIndex}]" : "";
                ConsoleOutput.WriteMessage($"Generating geohash for {feature.TimeZoneName}" + indexString);
                var geohashes = GeohashTree.GetGeohashes(feature.PreparedGeometry);
                return (Feature: feature, Geohashes: geohashes);
            })
            .ToList();

        ConsoleOutput.WriteMessage("Geohashes generated for polygons.  Building tree...");
        foreach (var (feature, geohashes) in featuresWithGeohashes)
        {
            foreach (var geohash in geohashes)
            {
                AddResult(geohash, feature);
            }
        }

        ConsoleOutput.WriteMessage("Tree built.  Preparing tree for output...");

        WorldBoundsTreeNode.PrepareForOutput();
        ConsoleOutput.WriteMessage("Tree prepared.  Writing output...");

        WriteGeohashDataFile(outputPath);
        ConsoleOutput.WriteMessage("Data file written.");

        WriteLookup(outputPath);
        ConsoleOutput.WriteMessage("Lookup file written.");

        ConsoleOutput.WriteMessage("Done!");
    }

    private static void PreLoadTimeZones(IEnumerable<TimeZoneFeature> features)
    {
        var zones = features
            .GroupBy(x => x.TimeZoneName)
            .Select(x => x.First())
            .OrderBy(x => x.TimeZoneName)
            .Distinct();

        var i = 0;
        foreach (var zone in zones)
        {
            TimeZones.Add(zone.TimeZoneName, new TimeZoneMeta(++i, zone.TimeZoneName));
        }
    }
}
