using GeoAPI.Geometries;

namespace GeoTimeZone.DataBuilder
{
    public class TimeZoneFeature
    {
        public string TzName { get; set; }
        public IGeometry Geometry { get; set; }
        public int MultiPolyIndex { get; set; } = -1;
    }
}