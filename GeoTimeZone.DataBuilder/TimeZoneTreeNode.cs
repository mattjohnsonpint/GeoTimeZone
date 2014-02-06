using System.Collections.Generic;

namespace GeoTimeZone.DataBuilder
{
    public class TimeZoneTreeNode
    {
        public readonly HashSet<TimeZoneFeature> TimeZones = new HashSet<TimeZoneFeature>(); 
        public readonly Dictionary<char, TimeZoneTreeNode> ChildNodes = new Dictionary<char, TimeZoneTreeNode>();
    }
}