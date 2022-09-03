using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Prepared;

namespace GeoTimeZone.DataBuilder;

public class TimeZoneFeature
{
    internal static readonly PreparedGeometryFactory PreparedGeometryFactory = new();
    private IPreparedGeometry? _preparedGeometry;
    
    public TimeZoneFeature(string timeZoneName, Geometry geometry, int multiPolyIndex = -1)
    {
        TimeZoneName = timeZoneName;
        Geometry = geometry;
        MultiPolyIndex = multiPolyIndex;
    }

    public string TimeZoneName { get; }
    public Geometry Geometry { get; }
    public int MultiPolyIndex { get; }

    public IPreparedGeometry PreparedGeometry => _preparedGeometry ??= PreparedGeometryFactory.Create(Geometry);
}
