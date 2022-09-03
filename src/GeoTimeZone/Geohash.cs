//
//  GeoHash implementation by Sharon Lourduraj.
//  See accompanying LICENSE file for details.
//

namespace GeoTimeZone
{
    internal static class Geohash
    {
        private const string Base32 = "0123456789bcdefghjkmnpqrstuvwxyz";
        private static readonly int[] Bits = { 16, 8, 4, 2, 1 };

        internal const int Precision = 5;

        public static void Encode(double latitude, double longitude,
#if NET6_0_OR_GREATER || NETSTANDARD2_1
            Span<byte> geohash
#else
            byte[] geohash
#endif
        )
        {
            bool even = true;
            int bit = 0;
            int ch = 0;
            int length = 0;

#if NET6_0_OR_GREATER || NETSTANDARD2_1
            Span<double> lat = stackalloc[] { -90.0, 90.0 };
            Span<double> lon = stackalloc[] { -180.0, 180.0 };
#else
            double[] lat = { -90.0, 90.0 };
            double[] lon = { -180.0, 180.0 };
#endif

            while (length < Precision)
            {
                if (even)
                {
                    double mid = (lon[0] + lon[1]) / 2;
                    if (longitude > mid)
                    {
                        ch |= Bits[bit];
                        lon[0] = mid;
                    }
                    else
                    {
                        lon[1] = mid;
                    }
                }
                else
                {
                    double mid = (lat[0] + lat[1]) / 2;
                    if (latitude > mid)
                    {
                        ch |= Bits[bit];
                        lat[0] = mid;
                    }
                    else
                    {
                        lat[1] = mid;
                    }
                }

                even = !even;

                if (bit < 4)
                {
                    bit++;
                }
                else
                {
                    geohash[length] = (byte)Base32[ch];
                    length++;
                    bit = 0;
                    ch = 0;
                }
            }
        }
    }
}