using GeoAPI.Geometries;

namespace GeoTimeZone.DataBuilder
{
    public class TimeZoneFeature
    {
        public string TzName { get; set; }
        public string TwoLetterIsoCountryCode { get; set; }
        public string ThreeLetterIsoCountryCode { get; set; }
        public IGeometry Geometry { get; set; }
    }
}