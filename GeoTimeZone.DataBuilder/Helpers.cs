using System.Collections.Generic;

namespace GeoTimeZone.DataBuilder
{
    public static class Helpers
    {
        static readonly Dictionary<string, string> TzNames = new Dictionary<string, string>
            {
                { "Africa/Asmera", "Africa/Asmara" },
                { "America/Buenos_Aires", "America/Argentina/Buenos_Aires" },
                { "America/Catamarca", "America/Argentina/Catamarca" },
                { "America/Cordoba", "America/Argentina/Cordoba" },
                { "America/Jujuy", "America/Argentina/Jujuy" },
                { "America/Mendoza", "America/Argentina/Mendoza" },
                { "America/Indianapolis", "America/Indiana/Indianapolis" },
                { "America/Louisville", "America/Kentucky/Louisville" },
                { "Asia/Calcutta", "Asia/Kolkata" },
                { "Asia/Katmandu", "Asia/Kathmandu" },
            };  

        // This method fixes mispelt IANA time zone names
        public static string CleanseTimeZoneName(string tzName)
        {
            return TzNames.ContainsKey(tzName) ? TzNames[tzName] : tzName;
        }
    }
}
