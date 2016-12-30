using System;
using System.Collections.Generic;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace GeoTimeZone.DataBuilder
{
    public class TimeZoneShapeFileReader
    {
        private readonly string _shapeFile;

        public TimeZoneShapeFileReader(string shapeFile)
        {
            _shapeFile = shapeFile;
        }

        public IEnumerable<TimeZoneFeature> ReadShapeFile()
        {
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
                    var zone = (string)attributes["tzid"];
                    if (zone.Equals("uninhabited", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var geometry = reader.Geometry;

                    yield return new TimeZoneFeature
                        {
                            TzName = zone,
                            Geometry = geometry,
                        };
                }
            }
        }
    }
}