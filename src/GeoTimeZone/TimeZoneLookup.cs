using GeoTimeZone.Core;

namespace GeoTimeZone;

/// <summary>
/// Provides the time zone lookup functionality.
/// </summary>
public static partial class TimeZoneLookup
{
    static TimeZoneLookup()
    {
        Geohash.Precision = Precision;
        TimezoneFileReader.CompressedDataStream = new Lazy<Stream>(() => typeof(TimeZoneLookup).Assembly.GetManifestResourceStream(DataFilename));
        Core.TimeZoneLookup.CompressedDataStream = new Lazy<Stream>(() => typeof(TimeZoneLookup).Assembly.GetManifestResourceStream(LookupDataFilename));
    }

    /// <summary>
    /// Determines the IANA time zone for given location coordinates.
    /// </summary>
    /// <param name="latitude">The latitude of the location.</param>
    /// <param name="longitude">The longitude of the location.</param>
    /// <returns>A <see cref="TimeZoneResult"/> object, which contains the result(s) of the operation.</returns>
    public static TimeZoneResult GetTimeZone(double latitude, double longitude)
        => Core.TimeZoneLookup.GetTimeZone(latitude, longitude);
}
