using System.IO;
using System.Text;

namespace GeoTimeZone.Internal;

internal sealed class SpatialIndex
{
    internal const int MaximumImageBytes = 64 * 1024 * 1024;
    internal string[] Zones { get; }
    // SHA256 of the archive directory, whose entries bind individual images.
    internal byte[] GeometryHash { get; }
    internal int PolygonCount { get; }
    private readonly int[] _roots;
    private readonly int[] _children;
    private readonly int[] _offsets;
    private readonly int[] _candidates;

    internal SpatialIndex(string[] zones, byte[] geometryHash, int polygonCount, int[] roots, int[] children, int[] offsets, int[] candidates)
    {
        Zones = zones;
        GeometryHash = geometryHash;
        PolygonCount = polygonCount;
        _roots = roots;
        _children = children;
        _offsets = offsets;
        _candidates = candidates;
        Validate();
    }

    internal int Find(double latitude, double longitude)
    {
        // Both grids use half-open cells [west,east) x [south,north), with
        // +180/+90 clamped into the last cell. Build-time intersection uses
        // CLOSED rectangles, conservatively including touching polygons on
        // both sides of each seam. Consequently either seam ownership is
        // safe: the runtime cannot miss a polygon covering a boundary point.
        var column = SpatialFormat.Cell(longitude, -180, SpatialFormat.CellWidth, SpatialFormat.Columns);
        var row = SpatialFormat.Cell(latitude, -90, SpatialFormat.CellWidth, SpatialFormat.Rows);
        var value = _roots[row * SpatialFormat.Columns + column];
        // 0 = no polygon; positive = zone ID + 1. A negative root points to
        // a 16-entry child block; a negative child points to a candidate list.
        if (value >= 0) return value;
        var x = SpatialFormat.Cell(longitude, -180 + column * SpatialFormat.CellWidth, SpatialFormat.ChildWidth, SpatialFormat.Refinement);
        var y = SpatialFormat.Cell(latitude, -90 + row * SpatialFormat.CellWidth, SpatialFormat.ChildWidth, SpatialFormat.Refinement);
        return _children[(-value - 1) * SpatialFormat.ChildrenPerCell + y * SpatialFormat.Refinement + x];
    }

    internal int CandidateStart(int value) => _offsets[-value - 1];
    internal int CandidateEnd(int value) => _offsets[-value];
    internal int Candidate(int position) => _candidates[position];

    internal byte[] Serialize()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(SpatialFormat.IndexMagic);
        writer.Write(SpatialFormat.Version);
        writer.Write(SpatialFormat.Columns);
        writer.Write(SpatialFormat.Rows);
        writer.Write(SpatialFormat.Refinement);
        writer.Write(GeometryHash);
        writer.Write(PolygonCount);
        writer.Write(Zones.Length);
        foreach (var zone in Zones)
        {
            var bytes = Encoding.UTF8.GetBytes(zone);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        WriteArray(writer, _roots);
        WriteArray(writer, _children);
        WriteArray(writer, _offsets);
        WriteArray(writer, _candidates);
        writer.Flush();
        return stream.ToArray();
    }

    internal static SpatialIndex Read(Stream stream)
    {
        SpatialFormat.Require(stream.CanSeek && stream.Length - stream.Position <= MaximumImageBytes);
        using var reader = new BinaryReader(stream, Encoding.UTF8, true);
        SpatialFormat.Require(reader.ReadInt32() == SpatialFormat.IndexMagic && reader.ReadInt32() == SpatialFormat.Version);
        SpatialFormat.Require(reader.ReadInt32() == SpatialFormat.Columns && reader.ReadInt32() == SpatialFormat.Rows
            && reader.ReadInt32() == SpatialFormat.Refinement);
        var hash = reader.ReadBytes(32);
        SpatialFormat.Require(hash.Length == 32);
        var polygonCount = SpatialFormat.ReadCount(reader, 1_000_000);
        var zones = new string[SpatialFormat.ReadCount(reader, 10000)];
        for (var i = 0; i < zones.Length; i++)
        {
            var length = SpatialFormat.ReadCount(reader, 512);
            var name = reader.ReadBytes(length);
            SpatialFormat.Require(name.Length == length);
            zones[i] = new UTF8Encoding(false, true).GetString(name);
        }

        var roots = ReadArray(reader, SpatialFormat.Columns * SpatialFormat.Rows);
        var children = ReadArray(reader, SpatialFormat.Columns * SpatialFormat.Rows * SpatialFormat.ChildrenPerCell);
        var offsets = ReadArray(reader, 10_000_000);
        var candidates = ReadArray(reader, 10_000_000);
        SpatialFormat.Require(stream.Position == stream.Length);
        return new SpatialIndex(zones, hash, polygonCount, roots, children, offsets, candidates);
    }

    private void Validate()
    {
        SpatialFormat.Require(GeometryHash.Length == 32 && Zones.Length > 0 && PolygonCount > 0
            && SpatialFormat.Rows * SpatialFormat.CellWidth == 180
            && _roots.Length == SpatialFormat.Columns * SpatialFormat.Rows && _children.Length % SpatialFormat.ChildrenPerCell == 0
            && _offsets.Length > 0 && _offsets[0] == 0 && _offsets[_offsets.Length - 1] == _candidates.Length);
        for (var i = 0; i < Zones.Length; i++)
            SpatialFormat.Require(Zones[i].Length > 0 && (i == 0 || StringComparer.Ordinal.Compare(Zones[i - 1], Zones[i]) < 0));
        foreach (var value in _roots)
            SpatialFormat.Require(value >= 0 ? value <= Zones.Length : value != int.MinValue && -value <= _children.Length / SpatialFormat.ChildrenPerCell);
        foreach (var value in _children)
            SpatialFormat.Require(value >= 0 ? value <= Zones.Length : value != int.MinValue && -value < _offsets.Length);
        for (var i = 1; i < _offsets.Length; i++) SpatialFormat.Require(_offsets[i] > _offsets[i - 1]);
        foreach (var candidate in _candidates) SpatialFormat.Require(candidate >= 0 && candidate < PolygonCount);
        for (var list = 0; list < _offsets.Length - 1; list++)
        for (var i = _offsets[list] + 1; i < _offsets[list + 1]; i++) SpatialFormat.Require(_candidates[i - 1] < _candidates[i]);
    }

    private static void WriteArray(BinaryWriter writer, int[] values)
    {
        writer.Write(values.Length);
        foreach (var value in values) writer.Write(value);
    }

    private static int[] ReadArray(BinaryReader reader, int maximum)
    {
        var count = SpatialFormat.ReadCount(reader, maximum);
        SpatialFormat.Require(count <= (reader.BaseStream.Length - reader.BaseStream.Position) / sizeof(int));
        var values = new int[count];
        for (var i = 0; i < values.Length; i++) values[i] = reader.ReadInt32();
        return values;
    }
}
