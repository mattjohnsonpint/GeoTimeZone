using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GeoAPI.Geometries;
using GeoTimeZone.DataBuilder;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using NetTopologySuite.Simplify;

namespace GeoTimeZone
{
    public class Result
    {
        public string tz;
        public Dictionary<char, Result> Results = new Dictionary<char, Result>();
    }

    public static class TimeZoneDataBuilder
    {
        private const string LineEnding = "\n";
        private static IList<Feature> _features;
        private static readonly GeohashLevelList geohashes = new GeohashLevelList();

        private static readonly Result Result = new Result();
        private static readonly Dictionary<string, int> TZ = new Dictionary<string, int>();
        private static int TZcount = 0;

        private static void WriteLookup()
        {
            var arr = TZ.OrderBy(x => x.Value).Select(x => x.Key).ToArray();
            File.WriteAllText(@"..\..\..\GeoTimeZone\TZL.txt", string.Join(LineEnding, arr), Encoding.UTF8);
        }

        private static void WriteResult(string idFormat)
        {
            var sb = new StringBuilder();
            WriteSwitch(sb, Result, idFormat);
            File.WriteAllText(@"..\..\..\GeoTimeZone\TZ.txt", sb.ToString(), Encoding.UTF8);
        }

        private static void WriteSwitch(StringBuilder sb, Result result, string idFormat, string hash = "")
        {
            foreach (var result1 in result.Results.OrderBy(x => x.Key))
            {
                if (result1.Value.tz != null)
                {
                    var h = hash + result1.Key;
                    while (h.Length < 5)
                    {
                        h += "-";
                    }
                    sb.Append(h + "|" + TZ[result1.Value.tz].ToString(idFormat) + LineEnding);
                }
                else if (result1.Value.Results.Count > 0)
                {
                    WriteSwitch(sb, result1.Value, idFormat, hash + result1.Key);
                }
            }
        }

        private static void AddResult(string geohash, string tz)
        {
            int id;
            if (!TZ.TryGetValue(tz, out id))
                TZ.Add(tz, ++TZcount);

            var c = geohash.ToCharArray();
            var r = Result;

            for (int i = 0; i < c.Length; i++)
            {
                var ch = c[i];
                Result r2;
                if (!r.Results.TryGetValue(ch, out r2))
                {
                    r2 = r.Results[ch] = new Result();
                }

                r = r2;

                var last = i == c.Length - 1;

                if (last)
                {
                    r.tz = tz;
                    break;
                }
            }
        }

        public static void Geohash()
        {
            _features = ReadShapeFile(@".\Data\tz_world.shp").ToList();

            int f = 0;
            foreach (var feature in _features)
            {
                f++;

                var name = (string)feature.Attributes["TZID"];

                var hashes = Geohashes(feature).OrderBy(x => x).ToList();
                foreach (var hash in hashes)
                {
                    AddResult(hash, name);
                }

                Console.WriteLine(f);
            }

            var idFormat = "";
            for (int i = 0; i < TZcount.ToString(CultureInfo.InvariantCulture).Length; i++)
            {
                idFormat += "0";
            }

            WriteResult(idFormat);
            WriteLookup();
        }

        private static IEnumerable<string> Geohashes(Feature feature)
        {
            return geohashes.SelectMany(x => Geohashes(feature, x));
        }

        private static IEnumerable<string> Geohashes(Feature feature, GeohashLevel level)
        {
            var env = level.Geometry;

            if (feature.Geometry.Contains(env))
            {
                return new[] { level.Geohash };
            }

            if (feature.Geometry.Intersects(env))
            {
                if (level.Geohash.Length == 5)
                    return new[] {level.Geohash};
                else
                    return level.GetChildren().SelectMany(child => Geohashes(feature, child));
            }

            return new string[0];
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

                    // skip uninhabeted areas
                    var zone = (string) attributes["TZID"];
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
                        while (simplified == null || !(simplified is Polygon) || !simplified.IsValid || simplified.IsEmpty)
                        {
                            simplified = TopologyPreservingSimplifier.Simplify(geometry, tolerance);
                            tolerance -= 0.005;
                        }
                    }

                    var feature = new Feature(simplified, attributes);
                    yield return feature;
                }
            }
        }
    }
}
