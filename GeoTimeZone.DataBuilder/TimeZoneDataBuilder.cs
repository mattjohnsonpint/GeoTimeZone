using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GeoAPI.Geometries;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using NetTopologySuite.Simplify;

namespace GeoTimeZone.DataBuilder
{
    public static class TimeZoneDataBuilder
    {
        private const string LineEnding = "\n";
        private const int GeohashLength = 5;
        
        private static readonly TimeZoneResult Result = new TimeZoneResult();
        private static readonly Dictionary<string, int> TimeZones = new Dictionary<string, int>();

        private const string DataFileName = "TZ.dat";
        private const string LookupFileName = "TZL.dat";
        
        private static void WriteLookup(string outputPath)
        {
            var arr = TimeZones.OrderBy(x => x.Value).Select(x => x.Key).ToArray();
            var path = Path.Combine(outputPath, LookupFileName);
            File.WriteAllText(path, string.Join(LineEnding, arr), Encoding.UTF8);
        }

        private static void WriteResult(string idFormat, string outputPath)
        {
            var sb = new StringBuilder();
            WriteSwitch(sb, Result, idFormat);
            var path = Path.Combine(outputPath, DataFileName);
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }

        private static void WriteSwitch(StringBuilder sb, TimeZoneResult result, string idFormat, string hash = "")
        {
            foreach (var result1 in result.Results.OrderBy(x => x.Key))
            {
                if (result1.Value.TimeZone != null)
                {
                    var h = (hash + result1.Key).PadRight(GeohashLength, '-');
                    sb.Append(h + "|" + TimeZones[result1.Value.TimeZone].ToString(idFormat) + LineEnding);
                }
                else if (result1.Value.Results.Count > 0)
                {
                    WriteSwitch(sb, result1.Value, idFormat, hash + result1.Key);
                }
            }
        }

        private static void AddResult(string geohash, string tz, ref int timeZoneCount)
        {
            int id;
            if (!TimeZones.TryGetValue(tz, out id))
                TimeZones.Add(tz, ++timeZoneCount);

            var c = geohash.ToCharArray();
            var r = Result;

            for (int i = 0; i < c.Length; i++)
            {
                var ch = c[i];
                TimeZoneResult r2;
                if (!r.Results.TryGetValue(ch, out r2))
                {
                    r2 = r.Results[ch] = new TimeZoneResult();
                }

                r = r2;

                var last = i == c.Length - 1;

                if (last)
                {
                    r.TimeZone = tz;
                    break;
                }
            }
        }

        public static void CreateGeohashData(string inputShapefile, string outputPath)
        {
            var features = ReadShapeFile(inputShapefile);

            var geohashList = new GeohashLevelList();

            int featuresProcessed = 0;
            int timeZoneCount = 0;
            foreach (var feature in features)
            {
                featuresProcessed++;

                var name = (string)feature.Attributes["TZID"];

                var hashes = geohashList
                    .SelectMany(x => GetGeohashes(feature, x))
                    .OrderBy(x => x)
                    .ToList();

                foreach (var hash in hashes)
                    AddResult(hash, name, ref timeZoneCount);

                Console.WriteLine(featuresProcessed);
            }

            var idFormat = "";
            for (int i = 0; i < timeZoneCount.ToString(CultureInfo.InvariantCulture).Length; i++)
            {
                idFormat += "0";
            }

            WriteResult(idFormat, outputPath);
            WriteLookup(outputPath);
        }

        private static IEnumerable<string> GetGeohashes(Feature feature, GeohashLevel level)
        {
            var env = level.Geometry;

            if (feature.Geometry.Contains(env))
            {
                return new[] { level.Geohash };
            }

            if (!feature.Geometry.Intersects(env))
            {
                return new string[0];
            }

            if (level.Geohash.Length == GeohashLength)
            {
                return new[] { level.Geohash };
            }
                
            return level.GetChildren().SelectMany(child => GetGeohashes(feature, child));
        }

        private static IEnumerable<Feature> ReadShapeFile(string path)
        {
            var factory = new GeometryFactory();
            using (var reader = new ShapefileDataReader(path, factory))
            {
                var header = reader.DbaseHeader;

                while (reader.Read())
                {
                    var attributes = new AttributesTable();
                    for (int i = 0; i < header.NumFields; i++)
                    {
                        var name = header.Fields[i].Name;
                        var value = reader.GetValue(i);
                        attributes.AddAttribute(name, value);
                    }

                    // skip uninhabited areas
                    var zone = (string)attributes["TZID"];
                    if (zone.Equals("uninhabited", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var geometry = reader.Geometry;

                    // Simplify the geometry.
                    IGeometry simplified = null;
                    if (geometry.Area < 0.1)
                    {
                        // For very small regions, use a convex hull.
                        simplified = geometry.ConvexHull();
                    }
                    else
                    {
                        // Simplify the polygon if necessary. Reduce the tolerance incrementally until we have a valid polygon.
                        var tolerance = 0.05;
                        while (!(simplified is Polygon) || !simplified.IsValid || simplified.IsEmpty)
                        {
                            simplified = TopologyPreservingSimplifier.Simplify(geometry, tolerance);
                            tolerance -= 0.005;
                        }
                    }

                    yield return new Feature(simplified, attributes);
                }
            }
        }
    }
}
