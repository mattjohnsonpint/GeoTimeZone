using System.Collections.Generic;
using System.Linq;
using NetTopologySuite.Geometries;

namespace GeoTimeZone.DataBuilder
{
    public class GeohashTreeNode
    {
        public string Geohash { get; set; }

        public Envelope Envelope { get; set; }

        private Geometry _geometry;
        public Geometry Geometry => _geometry ?? (_geometry = GeometryFactory.Default.ToGeometry(Envelope));

        private List<GeohashTreeNode> _children; 
        public List<GeohashTreeNode> GetChildren()
        {
            return _children ?? (_children = GeohashTree.GetNextLevel(Geohash, Envelope).ToList());
        }
    }
}