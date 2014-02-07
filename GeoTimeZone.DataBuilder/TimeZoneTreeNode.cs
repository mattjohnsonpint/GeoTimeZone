using System.Collections.Generic;
using System.Linq;

namespace GeoTimeZone.DataBuilder
{
    public class TimeZoneTreeNode
    {
        public List<string> TimeZoneNames
        {
            get
            {
                return TimeZones.Select(x => x.TzName).Distinct().OrderBy(x => x).ToList();
            }
        } 

        public readonly HashSet<TimeZoneFeature> TimeZones = new HashSet<TimeZoneFeature>(); 
        public readonly Dictionary<char, TimeZoneTreeNode> ChildNodes = new Dictionary<char, TimeZoneTreeNode>();


        public void RollTimeZonesUp()
        {
            RollTimeZonesUpInternal();
        }

        private bool RollTimeZonesUpInternal()
        {
            var allRolledUp = true;

            if (ChildNodes.Count == 0)
                return true;

            foreach (var childNode in ChildNodes)
            {
                var temp = childNode.Value.RollTimeZonesUpInternal();
                if (!temp)
                    allRolledUp = false;
            }

            if (allRolledUp)
            {
                var canRollup = ChildNodes.Count == 32;// && ChildNodes.All(x => x.Value.TimeZoneNames.SequenceEqual(tzIds));

                if (canRollup)
                {
                    List<string> tzIds =null;
                    foreach (var childNode in ChildNodes)
                    {
                        if (tzIds == null)
                            tzIds = childNode.Value.TimeZoneNames;
                        else
                        {
                            var temp = childNode.Value.TimeZoneNames.SequenceEqual(tzIds);
                            if (!temp)
                            {
                                canRollup = false;
                                break;
                            }
                        }
                    }
                }

                if (canRollup)
                {
                    var timeZones = ChildNodes.SelectMany(x => x.Value.TimeZones).ToList();
                    foreach (var timeZone in timeZones)
                    {
                        TimeZones.Add(timeZone);
                    }
                    foreach (var childNode in ChildNodes)
                    {
                        childNode.Value.TimeZones.Clear();
                    }
                }
                else
                {
                    allRolledUp = false;
                }
            }

            return allRolledUp;
        }
    }
}