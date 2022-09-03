namespace GeoTimeZone.DataBuilder;

public class TimeZoneMeta
{
    public TimeZoneMeta(int lineNumber, string ianaTimeZoneId)
    {
        LineNumber = lineNumber;
        IanaTimeZoneId = ianaTimeZoneId;
    }

    public int LineNumber { get; }
    public string IanaTimeZoneId { get; }
}
