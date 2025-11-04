//
//  GeoHash implementation originally by Sharon Lourduraj.
//  See accompanying LICENSE file for details.
//

namespace GeoTimeZone.Core;

internal static class Geohash
{
    public static int Precision;

    private const string Base32 = "0123456789bcdefghjkmnpqrstuvwxyz";
    private static readonly int[] Bits = { 16, 8, 4, 2, 1 };

#if NET6_0_OR_GREATER || NETSTANDARD2_1
    public static void Encode(double latitude, double longitude, Span<byte> geohash)
#else
    public static void Encode(double latitude, double longitude, byte[] geohash)
#endif
    {
        var even = true;
        var bit = 0;
        var ch = 0;
        var length = 0;

#if NET6_0_OR_GREATER || NETSTANDARD2_1
        Span<double> lat = stackalloc[] { -90.0, 90.0 };
        Span<double> lon = stackalloc[] { -180.0, 180.0 };
#else
        double[] lat = {-90.0, 90.0};
        double[] lon = {-180.0, 180.0};
#endif

        while (length < Precision)
        {
            if (even)
            {
                var mid = (lon[0] + lon[1]) / 2;
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
                var mid = (lat[0] + lat[1]) / 2;
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
