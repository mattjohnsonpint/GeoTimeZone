using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NetTopologySuite.Geometries;

namespace GeoTimeZone.DataBuilder
{
    public static class TimeZoneDataBuilder
    {
        private static readonly GeohashTree GeohashTree = new GeohashTree();
        private static readonly TimeZoneTreeNode WorldBoundsTreeNode = new TimeZoneTreeNode();
        private static readonly Dictionary<string, TimeZoneMeta> TimeZones = new Dictionary<string, TimeZoneMeta>();


        private const string DataFileName = "TZ.dat.gz";
        private const string LookupFileName = "TZL.dat.gz";
        
        private static void WriteLookup(string outputPath)
        {

            var path = Path.Combine(outputPath, LookupFileName);

            using (var fileStream = File.Create(path))
            using (var compressedStream = new GZipStream(fileStream, CompressionMode.Compress))
            using (var writer = new StreamWriter(compressedStream))
            {
                writer.NewLine = "\n";
                var timeZones = TimeZones.Values.OrderBy(x => x.LineNumber);
                foreach (var timeZone in timeZones)
                {
                    writer.WriteLine(timeZone.IanaTimeZoneId);
                }
            }
        }

        private static void WriteGeohashDataFile(string outputPath)
        {
            var path = Path.Combine(outputPath, DataFileName);

            using (var fileStream = File.Create(path))
            using (var compressedStream = new GZipStream(fileStream, CompressionMode.Compress))
            using (var writer = new StreamWriter(compressedStream))
            {
                writer.NewLine = "\n";
                WriteTreeNode(writer, WorldBoundsTreeNode);
            }
        }

        private static void WriteGeohash(TextWriter writer, string tz, string geohash)
        {
            var h = geohash.PadRight(5, '-');
            var p = TimeZones[tz].LineNumber.ToString("D3");
            writer.WriteLine(h + p);
        }

        private static void WriteTreeNode(TextWriter writer, TimeZoneTreeNode node, string geohash = "")
        {
            foreach (var childNode in node.ChildNodes.OrderBy(x => x.Key))
            {
                var childHash = geohash + childNode.Key;

                if (childNode.Value.TimeZones.Count > 0)
                {
                    var groupedTimeZones = childNode.Value.TimeZones.GroupBy(x => x.TzName).ToList();

                    if (groupedTimeZones.Count > 1)
                    {
                        var env = GeohashTree.GetTreeNode(childHash).Geometry;

                        var tzs = groupedTimeZones.Select(x => new
                        {
                            TimeZone = x.Key,
                            Area = x.Sum(c =>
                            {
                                var intersection = c.Geometry.Intersection(env);
                                return intersection.Area / env.Area;
                            })
                        })
                            .OrderByDescending(x => x.Area);

                        foreach (var timeZone in tzs)
                        {
                            WriteGeohash(writer, timeZone.TimeZone, childHash);
                        }
                    }
                    else
                    {
                        foreach (var timeZone in groupedTimeZones)
                        {
                            WriteGeohash(writer, timeZone.Key, childHash);
                        }
                    }
                }
                else if (childNode.Value.ChildNodes.Count > 0)
                {
                    WriteTreeNode(writer, childNode.Value, childHash);
                }
            }
        }

        private static void AddResult(string geohash, TimeZoneFeature tz)
        {
            var currentNode = WorldBoundsTreeNode;

            for (int i = 0; i < geohash.Length; i++)
            {
                var geohashChar = geohash[i];
                if (!currentNode.ChildNodes.TryGetValue(geohashChar, out var childNode))
                {
                    childNode = currentNode.ChildNodes[geohashChar] = new TimeZoneTreeNode();
                }

                currentNode = childNode;

                var last = i == geohash.Length - 1;

                if (last)
                {
                    currentNode.TimeZones.Add(tz);
                    break;
                }
            }
        }

        public static void CreateGeohashData(ConsoleOutput console, TimeZoneShapeFileReader inputShapefile, string outputPath)
        {
            console.WriteMessage("Loading polygons...");

            var features = inputShapefile.ReadShapeFile()
                .SelectMany(x =>
                {
                    // Expand MultiPolygons to individual Polygons
                    var mp = x.Geometry as MultiPolygon;
                    return mp != null
                        ? mp.Geometries.Select((y, i) => new TimeZoneFeature {TzName = x.TzName, Geometry = y, MultiPolyIndex = i})
                        : new[] {x};
                })
                .Where(x=> x.Geometry.Area > 0)
                .OrderBy(x=> x.TzName).ThenBy(x=> x.MultiPolyIndex)
                .ToList();

            PreLoadTimeZones(features);

            console.WriteMessage("Polygons loaded.");

            var geohashes = features
                .AsParallel()
                .Select(x =>
                {
                    var indexString = x.MultiPolyIndex >= 0 ? $"[{x.MultiPolyIndex}]" : "";
                    console.WriteMessage($"Generating geohash for {x.TzName}" + indexString);
                    return new
                    {
                        TimeZone = x,
                        Geohashes = GeohashTree.GetGeohashes(x.Geometry)
                    };
                })
                .ToList();

            console.WriteMessage("Geohashes generated for polygons.");

            foreach (var hash in geohashes)
                foreach (var g in hash.Geohashes)
                    AddResult(g, hash.TimeZone);

            console.WriteMessage("Geohash tree built.");

            WorldBoundsTreeNode.PrepareForOutput();

            console.WriteMessage("Geohash tree preparing for output.");

            WriteGeohashDataFile(outputPath);
            console.WriteMessage("Data file written.");

            WriteLookup(outputPath);
            console.WriteMessage("Lookup file written.");

            console.WriteMessage("Done!");
        }

        private static void PreLoadTimeZones(IEnumerable<TimeZoneFeature> features)
        {
            var zones = features.GroupBy(x => x.TzName).Select(x => x.First()).OrderBy(x => x.TzName).Distinct();
            
            int i = 0;
            foreach (var zone in zones)
            {
               TimeZones.Add(zone.TzName, new TimeZoneMeta
                {
                    LineNumber = ++i,

                    IanaTimeZoneId = zone.TzName,
                });
            }
        }
    }
}
