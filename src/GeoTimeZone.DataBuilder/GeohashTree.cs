using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Prepared;

namespace GeoTimeZone.DataBuilder;

public class GeohashTree : List<GeohashTreeNode>
{
    public readonly int Precision;

    internal const string Base32 = "0123456789bcdefghjkmnpqrstuvwxyz";

    public GeohashTree(int precision = 5)
    {
        Precision = precision;
        AddRange(GetNextLevel());
    }

    public List<string> GetGeohashes(IPreparedGeometry geometry) => this.SelectMany(level => GetGeohashes(geometry, level)).ToList();

    private IEnumerable<string> GetGeohashes(IPreparedGeometry geometry, GeohashTreeNode level)
    {
        try
        {
            var envelope = level.GetGeometry();
            if (geometry.Contains(envelope))
            {
                return new[] { level.Geohash };
            }

            if (!geometry.Intersects(envelope))
            {
                return Array.Empty<string>();
            }

            if (level.Geohash.Length == Precision)
            {
                return new[] { level.Geohash };
            }

            return level.GetChildren().SelectMany(child => GetGeohashes(geometry, child));
        }
        catch
        {
            // Ignore errors caused by invalid geometry
            return Array.Empty<string>();
        }
    }

    public GeohashTreeNode? GetTreeNode(string geohash)
    {
        GeohashTreeNode? result = null;
        foreach (var c in geohash)
        {
            if (c == '-')
            {
                return result;
            }

            var index = Base32.IndexOf(c);
            result = result == null ? this[index] : result.GetChildren()[index];
        }

        return result;
    }

    public IEnumerable<GeohashTreeNode> GetNextLevel(string geohash = "", Envelope? envelope = null)
    {
        if (geohash == string.Empty || envelope == null)
        {
            geohash = "";
            envelope = new Envelope(-180, 180, -90, 90);
        }

        var even = geohash.Length % 2 == 0;

        return SplitEnvelope2(envelope, even)
            .SelectMany(x => SplitEnvelope4(x, even))
            .SelectMany(x => SplitEnvelope4(x, even))
            .Select((envelope1, index) => new GeohashTreeNode(this, envelope1, geohash + Base32[index]));
    }

    public static IEnumerable<Envelope> SplitEnvelope2(Envelope envelope, bool even)
    {
        if (even)
        {

            var midX = envelope.MinX + envelope.Width / 2;
            yield return new Envelope(envelope.MinX, midX, envelope.MinY, envelope.MaxY);
            yield return new Envelope(midX, envelope.MaxX, envelope.MinY, envelope.MaxY);
        }
        else
        {
            var midY = envelope.MinY + envelope.Height / 2;
            yield return new Envelope(envelope.MinX, envelope.MaxX, envelope.MinY, midY);
            yield return new Envelope(envelope.MinX, envelope.MaxX, midY, envelope.MaxY);
        }
    }

    public static IEnumerable<Envelope> SplitEnvelope4(Envelope envelope, bool even)
    {
        var minX = envelope.MinX;
        var minY = envelope.MinY;

        var stepX = envelope.Width / 2;
        var stepY = envelope.Height / 2;

        if (even)
        {
            for (var y = 0; y < 2; y++)
            {
                for (var x = 0; x < 2; x++)
                {
                    var x1 = minX + (stepX * x);
                    var y1 = minY + (stepY * y);
                    yield return new Envelope(x1, x1 + stepX, y1, y1 + stepY);
                }
            }
        }
        else
        {
            for (var x = 0; x < 2; x++)
            {
                for (var y = 0; y < 2; y++)
                {
                    var x1 = minX + (stepX * x);
                    var y1 = minY + (stepY * y);
                    yield return new Envelope(x1, x1 + stepX, y1, y1 + stepY);
                }
            }
        }
    }
}
