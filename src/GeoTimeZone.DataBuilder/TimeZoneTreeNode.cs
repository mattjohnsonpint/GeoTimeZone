namespace GeoTimeZone.DataBuilder;

public class TimeZoneTreeNode
{
    public readonly List<(TimeZoneFeature Feature, double PctOfNode)> TimeZones = new();
    public readonly Dictionary<char, TimeZoneTreeNode> ChildNodes = new();

    public void PrepareForOutput()
    {
        RollDownTimeZones();
        RollUpTimeZones();
    }

    private bool RollUpTimeZones()
    {
        // If there are no child nodes, we are already rolled up
        if (ChildNodes.Count == 0)
        {
            return true;
        }

        // Recurse to roll up every child node
        var allChildrenRolledUp = true;
        foreach (var childNode in ChildNodes.Values)
        {
            if (!childNode.RollUpTimeZones())
            {
                allChildrenRolledUp = false;
            }
        }

        // We can only roll up this node if there are 32 child nodes and they are all rolled up
        if (ChildNodes.Count != 32 || !allChildrenRolledUp)
        {
            return false;
        }

        // All child nodes must have the same time zones, ordered by pct of node
        List<string>? tzIds = null;
        foreach (var childNode in ChildNodes.Values)
        {
            if (tzIds == null)
            {
                tzIds = childNode.GetTimeZonesOrderedByPctOfNode().ToList();
            }
            else if (!tzIds.SequenceEqual(childNode.GetTimeZonesOrderedByPctOfNode()))
            {
                return false;
            }
        }

        // Roll up the time zone features into this node and clear the children
        var timeZones = ChildNodes.SelectMany(x => x.Value.TimeZones);
        TimeZones.AddRange(timeZones);
        ChildNodes.Clear();

        return true;
    }

    private IEnumerable<string> GetTimeZonesOrderedByPctOfNode() => TimeZones
        .GroupBy(x => x.Feature.TimeZoneName)
        .OrderByDescending(x => x.Sum(y => y.PctOfNode))
        .Select(x => x.Key);

    private void RollDownTimeZones()
    {
        if (TimeZones.Count > 0 && ChildNodes.Count > 0)
        {
            foreach (var hashChar in GeohashTree.Base32)
            {
                if (!ChildNodes.TryGetValue(hashChar, out var childNode))
                {
                    childNode = ChildNodes[hashChar] = new TimeZoneTreeNode();
                }

                childNode.TimeZones.AddRange(TimeZones);
            }

            TimeZones.Clear();
        }

        foreach (var childNode in ChildNodes)
        {
            childNode.Value.RollDownTimeZones();
        }
    }
}
