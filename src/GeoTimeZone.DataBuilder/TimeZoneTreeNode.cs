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
        
        public void PrepareForOutput()
        {
            RollDownTimeZones();
            RollUpTimeZones();
        }

        private bool RollUpTimeZones()
        {
            bool allRolledUp = true;

            if (ChildNodes.Count == 0)
                return true;

            foreach (KeyValuePair<char, TimeZoneTreeNode> childNode in ChildNodes)
            {
                bool temp = childNode.Value.RollUpTimeZones();
                if (!temp)
                    allRolledUp = false;
            }

            if (allRolledUp)
            {
                bool canRollup = ChildNodes.Count == 32;

                if (canRollup)
                {
                    List<string> tzIds = null;
                    foreach ((char _, TimeZoneTreeNode childNode) in ChildNodes)
                    {
                        if (tzIds == null)
                            tzIds = childNode.TimeZoneNames;
                        else
                        {
                            bool temp = childNode.TimeZoneNames.SequenceEqual(tzIds);
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
                    foreach (TimeZoneFeature timeZone in timeZones)
                    {
                        TimeZones.Add(timeZone);
                    }

                    ChildNodes.Clear();
                }
                else
                {
                    allRolledUp = false;
                }
            }

            return allRolledUp;
        }


        private void RollDownTimeZones()
        {
            if (TimeZones.Count > 0 && ChildNodes.Count > 0)
            {
                foreach (char hashChar in GeohashTree.Base32)
                {
                    if (!ChildNodes.TryGetValue(hashChar, out TimeZoneTreeNode childNode))
                    {
                        childNode = ChildNodes[hashChar] = new TimeZoneTreeNode();
                    }

                    foreach (TimeZoneFeature timeZone in TimeZones)
                    {
                        childNode.TimeZones.Add(timeZone);
                    }
                }

                TimeZones.Clear();
            }

            foreach (KeyValuePair<char, TimeZoneTreeNode> childNode in ChildNodes)
            {
                childNode.Value.RollDownTimeZones();
            }
        }
    }
}