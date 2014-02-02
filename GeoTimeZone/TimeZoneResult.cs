using System.Collections.Generic;

namespace GeoTimeZone
{
    public class TimeZoneResult
    {
        public TimeZoneResult()
        {
            AlternativeResults = new List<TimeZoneDetails>();
        }

        public TimeZoneDetails Result { get; set; }
        public List<TimeZoneDetails> AlternativeResults { get; set; }
    }
}