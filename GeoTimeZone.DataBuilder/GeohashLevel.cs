using System.Collections.Generic;
using System.Linq;
using GeoAPI.Geometries;
using NetTopologySuite.Geometries;

namespace GeoTimeZone.DataBuilder
{
    public class GeohashLevel
    {
        public string Geohash { get; set; }

        public Envelope Envelope { get; set; }

        private IGeometry _geometry;
        public IGeometry Geometry { get { return _geometry ?? (_geometry = GeometryFactory.Default.ToGeometry(Envelope));  } }

        private List<GeohashLevel> _children; 
        public IEnumerable<GeohashLevel> GetChildren()
        {
            return _children ?? (_children = GeohashLevelList.GetNextLevel(Geohash, Envelope).ToList());
        }
    }
}