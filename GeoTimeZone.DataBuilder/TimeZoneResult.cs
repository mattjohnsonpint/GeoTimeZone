using System.Collections.Generic;

namespace GeoTimeZone.DataBuilder
{
    public class TimeZoneResult
    {
        public string TimeZone;
        public readonly Dictionary<char, TimeZoneResult> Results = new Dictionary<char, TimeZoneResult>();
    }
}