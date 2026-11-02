using GeoTimeZone.Internal;

namespace GeoTimeZone;

/// <summary>
/// Provides the time zone lookup functionality.
/// </summary>
public static class TimeZoneLookup
{
    private static readonly Lazy<SpatialIndex> Index = new(LoadIndex);
    private static readonly Lazy<GeometryArchive> Geometry = new(LoadGeometry);

    /// <summary>
    /// Determines the IANA time zone for given location coordinates.
    /// </summary>
    /// <param name="latitude">The latitude of the location.</param>
    /// <param name="longitude">The longitude of the location.</param>
    /// <returns>A <see cref="TimeZoneResult"/> object, which contains the result(s) of the operation.</returns>
    public static TimeZoneResult GetTimeZone(double latitude, double longitude)
    {
        if (!(latitude >= -90 && latitude <= 90)) throw new ArgumentOutOfRangeException(nameof(latitude));
        if (!(longitude >= -180 && longitude <= 180)) throw new ArgumentOutOfRangeException(nameof(longitude));
        var index = Index.Value;
        return Resolve(index, Geometry, latitude, longitude);
    }

    // Keep data injection internal: tests exercise the production aggregation
    // and lazy-loading path without replacing process-wide resource state.
    internal static TimeZoneResult Resolve<TGeometry>(SpatialIndex index, Lazy<TGeometry> packedGeometry, double latitude, double longitude)
        where TGeometry : IPolygonGeometry
    {
        var cell = index.Find(latitude, longitude);
        if (cell > 0) return new TimeZoneResult(index.Zones[cell - 1]);
        if (cell < 0)
        {
            var geometry = packedGeometry.Value;
            List<string>? matches = null;
            var matchedZone = -1;
            for (var i = index.CandidateStart(cell); i < index.CandidateEnd(cell); i++)
            {
                var polygon = index.Candidate(i);
                var zone = geometry.Zone(polygon);
                if (zone != matchedZone && geometry.Covers(polygon, latitude, longitude))
                {
                    // Most boundary queries still have exactly one match.
                    // Allocate a list only for a genuine second match.
                    if (matchedZone >= 0)
                    {
                        matches ??= new List<string> { index.Zones[matchedZone] };
                        matches.Add(index.Zones[zone]);
                    }
                    matchedZone = zone;
                }
            }

            // Candidates are grouped by zone in ordinal name order. Only true
            // overlaps/shared boundaries produce alternatives; nearby polygons
            // that do not cover this point are never returned.
            if (matches != null) return new TimeZoneResult(matches);
            if (matchedZone >= 0) return new TimeZoneResult(index.Zones[matchedZone]);
        }

        var offsetHours = CalculateOffsetHoursFromLongitude(longitude);
        return new TimeZoneResult(GetTimeZoneId(offsetHours));
    }

    private static SpatialIndex LoadIndex()
    {
        using var stream = typeof(TimeZoneLookup).Assembly.GetManifestResourceStream(SpatialFormat.IndexResource)
            ?? throw new InvalidOperationException("Time zone index resource is missing.");
        return SpatialIndex.Read(stream);
    }

    private static GeometryArchive LoadGeometry()
    {
        var index = Index.Value;
        return new GeometryArchive(() => typeof(TimeZoneLookup).Assembly.GetManifestResourceStream(SpatialFormat.GeometryResource)
            ?? throw new InvalidOperationException("Time zone geometry resource is missing."),
            index.GeometryHash, index.Zones.Length, index.PolygonCount);
    }

    private static int CalculateOffsetHoursFromLongitude(double longitude)
    {
        var dir = longitude < 0 ? -1 : 1;
        var posNo = Math.Abs(longitude);
        if (posNo <= 7.5) return 0;
        posNo -= 7.5;
        var offset = posNo / 15;
        if (posNo % 15 > 0) offset++;
        return dir * (int)Math.Floor(offset);
    }

    private static string GetTimeZoneId(int offsetHours)
    {
        if (offsetHours == 0) return "UTC";
        var reversed = (offsetHours >= 0 ? "-" : "+") + Math.Abs(offsetHours);
        return "Etc/GMT" + reversed;
    }
}
