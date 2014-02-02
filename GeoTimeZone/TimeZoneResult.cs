using System.Collections.Generic;

namespace GeoTimeZone
{
    public class TimeZoneResult
    {
        public TimeZoneDetails Result { get; set; }
        public List<TimeZoneDetails> AlternativeResults { get; set; }
    }
}