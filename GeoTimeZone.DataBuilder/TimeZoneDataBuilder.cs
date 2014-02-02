using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeoAPI.Geometries;
using NetTopologySuite.Geometries;
using NetTopologySuite.Simplify;

namespace GeoTimeZone.DataBuilder
{
    public static class TimeZoneDataBuilder
    {
        private const string LineEnding = "\n";
        private const int GeohashLength = 5;
        
        private static readonly TimeZoneTreeNode WorldBoundsTreeNode = new TimeZoneTreeNode();
        private static readonly Dictionary<string, int> TimeZones = new Dictionary<string, int>();

        private const string DataFileName = "TZ.dat";
        private const string LookupFileName = "TZL.dat";
        
        private static void WriteLookup(string outputPath)
        {
            var path = Path.Combine(outputPath, LookupFileName);
            
            using (var writer = File.CreateText(path))
            {
                writer.NewLine = LineEnding;
                var timeZones = TimeZones.OrderBy(x => x.Value).Select(x => x.Key);
                foreach (var timeZone in timeZones)
                    writer.WriteLine(timeZone);
            }
        }

        private static void WriteGeohashDataFile(string outputPath)
        {
            var path = Path.Combine(outputPath, DataFileName);
            using (var writer = File.CreateText(path))
            {
                writer.NewLine = LineEnding;
                WriteTreeNode(writer, WorldBoundsTreeNode);
            }
        }

        private static void WriteTreeNode(StreamWriter writer, TimeZoneTreeNode node, string hash = "")
        {
            foreach (var childNode in node.ChildNodes.OrderBy(x => x.Key))
            {
                if (childNode.Value.TimeZone != null)
                {
                    var h = (hash + childNode.Key).PadRight(GeohashLength, '-');
                    var p = TimeZones[childNode.Value.TimeZone].ToString("D3");
                    writer.WriteLine(h + p);
                }
                else if (childNode.Value.ChildNodes.Count > 0)
                {
                    WriteTreeNode(writer, childNode.Value, hash + childNode.Key);
                }
            }
        }

        private static void AddResult(string geohash, string tz)
        {
            var currentNode = WorldBoundsTreeNode;

            for (int i = 0; i < geohash.Length; i++)
            {
                var childNodeKey = geohash[i];
                TimeZoneTreeNode childNode;
                if (!currentNode.ChildNodes.TryGetValue(childNodeKey, out childNode))
                {
                    childNode = currentNode.ChildNodes[childNodeKey] = new TimeZoneTreeNode();
                }

                currentNode = childNode;

                var last = i == geohash.Length - 1;

                if (last)
                {
                    currentNode.TimeZone = tz;
                    break;
                }
            }
        }

        public static void CreateGeohashData(ConsoleOutput console, TimeZoneShapeFileReader inputShapefile, string outputPath)
        {
            var features = inputShapefile.ReadShapeFile().ToList();

            PreLoadTimeZones(features);

            var levels = new GeohashLevelList();

            int featuresProcessed = 0;
            foreach (var feature in features)
            {
                var geometry = feature.Geometry.Simplify();

                var hashes = levels
                    .AsParallel()
                    .SelectMany(level => GetGeohashes(geometry, level))
                    .ToList();

                foreach (var hash in hashes)
                    AddResult(hash, feature.TzName);

                console.WriteProgress(++featuresProcessed);
            }

            WriteGeohashDataFile(outputPath);
            WriteLookup(outputPath);
        }

        private static IEnumerable<string> GetGeohashes(IGeometry geometry, GeohashLevel level)
        {
            var env = level.Geometry;

            if (geometry.Contains(env))
            {
                return new[] { level.Geohash };
            }

            if (!geometry.Intersects(env))
            {
                return new string[0];
            }

            if (level.Geohash.Length == GeohashLength)
            {
                return new[] { level.Geohash };
            }
                
            return level.GetChildren().SelectMany(child => GetGeohashes(geometry, child));
        }

        private static IGeometry Simplify(this IGeometry geometry)
        {
            // Simplify the geometry.
            if (geometry.Area < 0.1)
            {
                // For very small regions, use a convex hull.
                return geometry.ConvexHull();
            }

            // Simplify the polygon if necessary. Reduce the tolerance incrementally until we have a valid polygon.
            var tolerance = 0.05;
            var result = geometry;
            while (true)
            {
                if (result is Polygon && result.IsValid && !result.IsEmpty)
                    return result;

                result = TopologyPreservingSimplifier.Simplify(geometry, tolerance);
                tolerance -= 0.005;
            }
        }

        private static void PreLoadTimeZones(IEnumerable<TimeZoneFeature> features)
        {
            var zones = features.Select(x => x.TzName).OrderBy(x => x).Distinct();

            int i = 0;
            foreach (var zone in zones)
                TimeZones.Add(zone, ++i);
        }
    }
}
