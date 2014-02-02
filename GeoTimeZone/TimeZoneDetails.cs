using System;

namespace GeoTimeZone
{
    public class TimeZoneDetails
    {
        internal TimeZoneDetails() { }

        public string IanaTimeZoneId { get; internal set; }
        public string WindowsTimeZoneId { get; internal set; }

        public TimeSpan StandardOffset { get; internal set; }
        public TimeSpan? DaylightOffset { get; internal set; }

        public string TwoLetterIsoCountryCode { get; internal set; }
        public string ThreeLetterIsoCountryCode { get; internal set; }

        public string GeneralEnglishName { get; internal set; }
        public string StandardEnglishName { get; internal set; }
        public string DaylightEnglishName { get; internal set; }

        public string GeneralAbbreviation { get; internal set; }
        public string StandardAbbreviation { get; internal set; }
        public string DaylightAbbreviation { get; internal set; }
    }
}
