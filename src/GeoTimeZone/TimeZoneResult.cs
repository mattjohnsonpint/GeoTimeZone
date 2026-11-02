using System.Collections.ObjectModel;

namespace GeoTimeZone;

/// <summary>
/// Contains the result of a time zone lookup operation.
/// </summary>
public class TimeZoneResult
{
    private static readonly ReadOnlyCollection<string> EmptyResults = new(Array.Empty<string>());

    internal TimeZoneResult(List<string> timeZones)
    {
        Result = timeZones[0];
        AlternativeResults = timeZones.Count == 1 ? EmptyResults : new ReadOnlyCollection<string>(timeZones.GetRange(1, timeZones.Count - 1));
    }

    internal TimeZoneResult(string timeZone)
    {
        Result = timeZone;
        AlternativeResults = EmptyResults;
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
