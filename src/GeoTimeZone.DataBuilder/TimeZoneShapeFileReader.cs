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
            using var reader = new ShapefileDataReader(_shapeFile, factory);
            DbaseFileHeader header = reader.DbaseHeader;

            while (reader.Read())
            {
                var attributes = new AttributesTable();
                for (int i = 0; i < header.NumFields; i++)
                {
                    string name = header.Fields[i].Name;
                    object value = reader.GetValue(i + 1);
                    attributes.Add(name, value);
                }

                // skip uninhabited areas
                string zone = (string)attributes["tzid"];
                if (zone.Equals("uninhabited", StringComparison.OrdinalIgnoreCase))
                    continue;

                Geometry geometry = reader.Geometry;

                yield return new TimeZoneFeature
                {
                    TzName = zone,
                    Geometry = geometry,
                };
            }
        }
    }
}