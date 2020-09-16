using NetTopologySuite.Geometries;

namespace GeoTimeZone.DataBuilder
{
    public class TimeZoneFeature
    {
        public string TzName { get; set; }
        public Geometry Geometry { get; set; }
        public int MultiPolyIndex { get; set; } = -1;
    }
}