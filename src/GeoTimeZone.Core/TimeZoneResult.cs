using System.Collections.ObjectModel;

namespace GeoTimeZone.Core;

/// <summary>
/// Contains the result of a time zone lookup operation.
/// </summary>
public class TimeZoneResult
{
    internal TimeZoneResult(List<string> timeZones)
    {
        Result = timeZones[0];
        AlternativeResults = new ReadOnlyCollection<string>(timeZones.GetRange(1, timeZones.Count - 1));
    }

    internal TimeZoneResult(string timeZone)
    {
        Result = timeZone;
        AlternativeResults = new ReadOnlyCollection<string>(new List<string>());
    }

    /// <summary>
    /// Gets the primary result of the time zone lookup operation.
    /// </summary>
    public string Result { get; }

    /// <summary>
    /// Gets any alternative results of the time zone lookup operation.
    /// This usually happens very close to borders between time zones.
    /// </summary>
    public ReadOnlyCollection<string> AlternativeResults { get; }
}
