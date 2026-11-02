using GeoTimeZone.Internal;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Prepared;
using NetTopologySuite.Index.Strtree;

namespace GeoTimeZone.DataBuilder;

public static class TimeZoneDataBuilder
{
    public static void CreateData(TimeZoneShapeFileReader inputShapefile, string outputPath) => CreateData(inputShapefile.ReadShapeFile(), outputPath);

    public static void CreateData(IEnumerable<TimeZoneFeature> input, string outputPath)
    {
        ConsoleOutput.WriteMessage("Loading and validating polygons...");
        var features = input.SelectMany(feature => Expand(feature.Geometry).Select((polygon, index) =>
                new TimeZoneFeature(feature.TimeZoneName == "Europe/Kiev" ? "Europe/Kyiv" : feature.TimeZoneName, polygon, index)))
            .OrderBy(f => f.TimeZoneName, StringComparer.Ordinal).ThenBy(f => f.MultiPolyIndex).ToArray();
        if (features.Length == 0) throw new ArgumentException("No polygons supplied.", nameof(input));
        foreach (var feature in features)
            if (!feature.Geometry.IsValid || feature.Geometry.IsEmpty || feature.Geometry.Area <= 0)
                throw new InvalidDataException("Invalid source polygon: " + feature.TimeZoneName);

        var zones = features.Select(f => f.TimeZoneName).Distinct(StringComparer.Ordinal).ToArray();
        var polygons = features.Select(feature =>
        {
            var polygon = (Polygon)feature.Geometry;
            var rings = new[] { polygon.ExteriorRing }.Concat(polygon.InteriorRings).Select(ring =>
            {
                var coordinates = ring.Coordinates;
                return new GeometryRing(coordinates.Select(c => c.X).ToArray(), coordinates.Select(c => c.Y).ToArray());
            }).ToArray();
            return new GeometryPolygon(Array.BinarySearch(zones, feature.TimeZoneName, StringComparer.Ordinal), rings);
        }).ToArray();

        ConsoleOutput.WriteMessage("Encoding shared geometry...");
        var geometry = GeometryWriter.Serialize(polygons);
        var prepared = features.Select(f => PreparedGeometryFactory.Prepare(f.Geometry)).ToArray();
        var tree = new STRtree<int>();
        for (var i = 0; i < features.Length; i++) tree.Insert(features[i].Geometry.EnvelopeInternal, i);
        tree.Build();

        ConsoleOutput.WriteMessage("Building refined grid...");
        var roots = new int[SpatialFormat.Columns * SpatialFormat.Rows];
        var children = new List<int>();
        var offsets = new List<int> { 0 };
        var candidates = new List<int>();
        var lists = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var y = 0; y < SpatialFormat.Rows; y++)
        for (var x = 0; x < SpatialFormat.Columns; x++)
        {
            var west = -180 + x * SpatialFormat.CellWidth;
            var south = -90 + y * SpatialFormat.CellWidth;
            var cell = Classify(west, south, SpatialFormat.CellWidth);
            if (cell.Candidates.Length == 0) roots[y * SpatialFormat.Columns + x] = cell.Zone;
            else
            {
                roots[y * SpatialFormat.Columns + x] = -(children.Count / SpatialFormat.ChildrenPerCell + 1);
                for (var cy = 0; cy < SpatialFormat.Refinement; cy++)
                for (var cx = 0; cx < SpatialFormat.Refinement; cx++)
                {
                    var child = Classify(west + cx * SpatialFormat.ChildWidth, south + cy * SpatialFormat.ChildWidth, SpatialFormat.ChildWidth);
                    if (child.Candidates.Length == 0) children.Add(child.Zone);
                    else
                    {
                        var key = string.Join(",", child.Candidates);
                        if (!lists.TryGetValue(key, out var list))
                        {
                            list = lists.Count;
                            lists.Add(key, list);
                            candidates.AddRange(child.Candidates);
                            offsets.Add(candidates.Count);
                        }

                        children.Add(-list - 1);
                    }
                }
            }
        }

        var index = new SpatialIndex(zones, SpatialFormat.Hash(geometry), polygons.Length, roots, children.ToArray(), offsets.ToArray(), candidates.ToArray());
        // Validate the geometry metadata and its pairing before publishing files.
        var packed = new PackedGeometry(geometry, index.GeometryHash, zones.Length, polygons.Length);
        packed.ValidateCoordinates(polygons);
        Directory.CreateDirectory(outputPath);
        SpatialFormat.WriteCompressed(Path.Combine(outputPath, "TZGeometry.dat.gz"), geometry);
        SpatialFormat.WriteCompressed(Path.Combine(outputPath, "TZIndex.dat.gz"), index.Serialize());
        ConsoleOutput.WriteMessage($"Wrote {zones.Length} zones, {polygons.Length} polygons, {children.Count / SpatialFormat.ChildrenPerCell} refined cells, and {lists.Count} candidate lists.");
        return;

        (int Zone, int[] Candidates) Classify(double west, double south, double width)
        {
            var envelope = new Envelope(west, west + width, south, south + width);
            var rectangle = GeometryFactory.Default.ToGeometry(envelope);
            var ids = tree.Query(envelope).Where(i => prepared[i].Intersects(rectangle)).Order().ToArray();
            if (ids.Length == 0) return (0, Array.Empty<int>());
            var zone = polygons[ids[0]].Zone;
            // A single candidate is insufficient: partial coastal cells and
            // holes still require geometry. Never infer coverage from samples.
            if (ids.All(i => polygons[i].Zone == zone && prepared[i].Covers(rectangle)))
                return (zone + 1, Array.Empty<int>());
            return (0, ids);
        }
    }

    private static IEnumerable<Polygon> Expand(Geometry geometry)
    {
        if (geometry is Polygon polygon) yield return polygon;
        else if (geometry is MultiPolygon multiPolygon)
            foreach (var child in multiPolygon.Geometries) yield return (Polygon)child;
        else throw new InvalidDataException("Expected polygon or multipolygon source data.");
    }
}
