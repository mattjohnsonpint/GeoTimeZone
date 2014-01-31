using System.Collections.Generic;

namespace GeoTimeZone.DataBuilder
{
    public class TimeZoneTreeNode
    {
        public string TimeZone;
        public readonly Dictionary<char, TimeZoneTreeNode> ChildNodes = new Dictionary<char, TimeZoneTreeNode>();
    }
}