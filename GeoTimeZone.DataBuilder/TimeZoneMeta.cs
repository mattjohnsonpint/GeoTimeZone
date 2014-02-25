namespace GeoTimeZone.DataBuilder
{
    public class TimeZoneMeta
    {
        public int LineNumber { get; set; }

        public string IanaTimeZoneId { get; set; }
        public string WindowsTimeZoneId { get; set; }

        public string TwoLetterIsoCountryCode { get; set; }
        public string ThreeLetterIsoCountryCode { get; set; }

        public override string ToString()
        {
            return string.Join("|", new[]
                {
                    IanaTimeZoneId,
                    TwoLetterIsoCountryCode,
                    ThreeLetterIsoCountryCode,
                    WindowsTimeZoneId  ?? string.Empty
                });
        }
    }
}