using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeoAPI.Geometries;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using NetTopologySuite.Simplify;

namespace GeoTimeZone.DataBuilder
{
    public class TimeZoneShapeFileReader
    {
        private readonly string _countryFile;
        private readonly string _zoneTabFile;
        private readonly string _shapeFile;

        public TimeZoneShapeFileReader(string countryFile, string zoneTabFile, string shapeFile)
        {
            _countryFile = countryFile;
            _zoneTabFile = zoneTabFile;
            _shapeFile = shapeFile;
        }

        private IEnumerable<string[]> ReadCountries()
        {
            using (var reader = new StreamReader(_countryFile))
            {
                string line = reader.ReadLine(); // skip first line
                while ((line = reader.ReadLine()) != null)
                {
                    yield return line.Split(';');
                }
            }
        }

        private IEnumerable<string[]> ReadZoneTab()
        {
            using (var reader = new StreamReader(_zoneTabFile))
            {
                string line = null;
                while ((line = reader.ReadLine()) != null)
                {
                    if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
                    {
                        yield return line.Split('\t');
                    }
                }
            }
        }

        public IEnumerable<TimeZoneFeature> ReadShapeFile()
        {
            var countries = ReadCountries().ToList();
            var zoneTab = ReadZoneTab().ToList();

            var factory = new GeometryFactory();
            using (var reader = new ShapefileDataReader(_shapeFile, factory))
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

                    string country2, country3;

                    if (zone == "America/Montreal" || zone == "America/Coral_Harbour")
                    {
                        // these timezones are not listed in zone.tab
                        country2 = "CA";
                        country3 = "CAN";
                    }
                    else
                    {
                        var zT = zoneTab.SingleOrDefault(x => x[2] == zone);
                        if (zT == null)
                        {
                            throw new Exception("Could not find " + zone + " in zone.tab");
                        }
                        country2 = zT[0];
                        country3 = countries.Single(x => x[1] == country2)[2];
                    }



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

                    yield return new TimeZoneFeature
                        {
                            TzName = zone,
                            Geometry = simplified,
                            ThreeLetterIsoCountryCode = country3,
                            TwoLetterIsoCountryCode = country2
                        };
                }
            }
        }
    }
}