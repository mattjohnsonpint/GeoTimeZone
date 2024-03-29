using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace GeoTimeZone.DataBuilder;

public class TimeZoneShapeFileReader
{
    private readonly string _shapeFile;

    public TimeZoneShapeFileReader(string shapeFile)
    {
        _shapeFile = shapeFile;
    }

    public IEnumerable<TimeZoneFeature> ReadShapeFile()
    {
        using var reader = new ShapefileDataReader(_shapeFile, GeometryFactory.Default);
        var header = reader.DbaseHeader;

        while (reader.Read())
        {
            var attributes = new AttributesTable();
            for (var i = 0; i < header.NumFields; i++)
            {
                var name = header.Fields[i].Name;
                var value = reader.GetValue(i + 1);
                attributes.Add(name, value);
            }

            // skip uninhabited areas
            var zone = (string) attributes["tzid"];
            if (zone.Equals("uninhabited", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return new TimeZoneFeature(zone, reader.Geometry);
        }
    }
}
