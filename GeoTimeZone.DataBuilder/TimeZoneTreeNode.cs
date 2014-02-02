using System.Collections.Generic;

namespace GeoTimeZone.DataBuilder
{
    public class TimeZoneTreeNode
    {
        public readonly Dictionary<string, double> TimeZones = new Dictionary<string, double>(); 
        public readonly Dictionary<char, TimeZoneTreeNode> ChildNodes = new Dictionary<char, TimeZoneTreeNode>();
    }
}