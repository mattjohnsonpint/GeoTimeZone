using System;
using System.Collections.Generic;
using System.Linq;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace GeoTimeZone
{
    public static class TimeZoneLookup
    {
        private static bool _isInitialized;

        private static IList<Feature> _features;

        public static void Initialize()
        {
            // note: Initialization is split out so we can measure timings in unit tests of the lookup separate from the data loading.
            //       It does not need to stay that way when we release.

            if (_isInitialized) return;

            // For now, the data is in a hardcoded path and copied alongside the dll, because it needs access to the other files that make up the shapefile data.
            // Preferably, it should be converted to a format we can keep as an embedded resource instead.
            _features = ReadShapeFile(@".\Data\tz_world.shp").ToList();
            _isInitialized = true;
        }

        public static string GetTimeZone(double latitude, double longitude)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("The time zone lookup data is not loaded.  You must call TimeZoneLookup.Initialize() in your application startup.");

            var point = new Point(longitude, latitude);

            // see if the point is exactly within the geometry
            var feature = _features.FirstOrDefault(x => x.Geometry.Intersects(point));
            if (feature != null) return (string)feature.Attributes["TZID"];

            // see if the point is in territorial waters of a known time zone (<= 22.2km)
            feature = _features.FirstOrDefault(x => x.Geometry.IsWithinDistance(point, 0.0222));
            if (feature != null) return (string)feature.Attributes["TZID"];

            // todo: handle Antartica zones (individual points in separate file)

            // todo: calculate a nautical time zone if nothing was found

            return null;
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

                    // For very small regions, use a convex hull.
                    var geometry = reader.Geometry;
                    if (geometry.Area < 0.1)
                        geometry = geometry.ConvexHull();

                    var feature = new Feature(geometry, attributes);
                    yield return feature;
                }
            }
        }
    }
}
