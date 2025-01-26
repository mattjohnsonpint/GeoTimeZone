using NetTopologySuite.IO.Esri;

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
        foreach (var feature in Shapefile.ReadAllFeatures(_shapeFile))
        {
            var zone = (string) feature.Attributes["tzid"];

            // skip uninhabited areas
            if (zone.Equals("uninhabited", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return new TimeZoneFeature(zone, feature.Geometry);
        }
    }
}
