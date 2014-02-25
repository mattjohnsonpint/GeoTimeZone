using System;

namespace GeoTimeZone.DataBuilder
{
    public class TimeZoneMeta
    {
        public int LineNumber { get; set; }

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

        public override string ToString()
        {
            return string.Join("|", new[]
            {
                IanaTimeZoneId,
                TwoLetterIsoCountryCode, ThreeLetterIsoCountryCode,
                FormatTimeSpan(StandardOffset), FormatTimeSpan(DaylightOffset),
                WindowsTimeZoneId ?? string.Empty,
                GeneralAbbreviation, StandardAbbreviation, DaylightAbbreviation,
                GeneralEnglishName, StandardEnglishName, DaylightEnglishName
            });
        }

        private static string FormatTimeSpan(TimeSpan? ts)
        {
            if (!ts.HasValue) return string.Empty;
            return (ts.Value < TimeSpan.Zero ? "-" : "") + ts.Value.ToString("hhmm");
        }
    }
}