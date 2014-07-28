using System.Collections.Generic;

namespace GeoTimeZone
{
    public class TimeZoneResult
    {
        public TimeZoneResult()
        {
            AlternativeResults = new List<string>();
        }

        public string Result { get; set; }
        public List<string> AlternativeResults { get; set; }
    }
}