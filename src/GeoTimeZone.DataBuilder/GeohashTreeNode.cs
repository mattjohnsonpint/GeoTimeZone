using NetTopologySuite.Geometries;

namespace GeoTimeZone.DataBuilder;

public class GeohashTreeNode
{
    private List<GeohashTreeNode>? _children;
    private Geometry? _geometry;

    public GeohashTreeNode(Envelope envelope, string geohash)
    {
        Envelope = envelope;
        Geohash = geohash;
    }

    public Envelope Envelope { get; }

    public string Geohash { get; }

    public List<GeohashTreeNode> GetChildren() => _children ??= GeohashTree.GetNextLevel(Geohash, Envelope).ToList();

    public Geometry GetGeometry() => _geometry ??= GeometryFactory.Default.ToGeometry(Envelope);
}
