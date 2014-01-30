using System;
using System.Collections.Generic;
using System.Linq;
using GeoAPI.Geometries;

namespace GeoTimeZone.DataBuilder
{
    public class GeohashLevelList : List<GeohashLevel>
    {
        public GeohashLevelList()
        {
            AddRange(GetNextLevel());
        }

        private static char[] chars = new[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'j', 'k', 'm', 'n', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z' };

        public static IEnumerable<GeohashLevel> GetNextLevel(string geohash = "", Envelope envelope = null)
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
                .Select((envelope1, index) => new GeohashLevel { Envelope = envelope1, Geohash = geohash + chars[index] });
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
                for (int y = 0; y < 2; y++)
                    for (int x = 0; x < 2; x++)
                    {
                        var x1 = minX + (stepX * x);
                        var y1 = minY + (stepY * y);
                        yield return new Envelope(x1, x1 + stepX, y1, y1 + stepY);
                    }
            }
            else
            {
                for (int x = 0; x < 2; x++)
                    for (int y = 0; y < 2; y++)
                    {
                        var x1 = minX + (stepX * x);
                        var y1 = minY + (stepY * y);
                        yield return new Envelope(x1, x1 + stepX, y1, y1 + stepY);
                    }
            }
        }
    }
}