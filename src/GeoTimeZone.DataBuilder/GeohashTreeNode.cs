using NetTopologySuite.Geometries;

namespace GeoTimeZone.DataBuilder;

public class GeohashTreeNode
{
    private readonly GeohashTree Tree;
    private List<GeohashTreeNode>? _children;
    private Geometry? _geometry;

    public GeohashTreeNode(GeohashTree tree, Envelope envelope, string geohash)
    {
        Tree = tree;
        Envelope = envelope;
        Geohash = geohash;
    }

    public Envelope Envelope { get; }

    public string Geohash { get; }

    public List<GeohashTreeNode> GetChildren() => _children ??= Tree.GetNextLevel(Geohash, Envelope).ToList();

    public Geometry GetGeometry() => _geometry ??= GeometryFactory.Default.ToGeometry(Envelope);
}
