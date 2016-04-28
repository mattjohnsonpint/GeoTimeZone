//
//  GeoHash implementation by Sharon Lourduraj.
//  See accompanying LICENCE file for details.
//

using System;

namespace GeoTimeZone
{
    internal static class Geohash
    {
        private const string Base32 = "0123456789bcdefghjkmnpqrstuvwxyz";
        private static readonly int[] Bits = { 16, 8, 4, 2, 1 };

        public static String Encode(double latitude, double longitude, int precision = 12)
        {
            bool even = true;
            int bit = 0;
            int ch = 0;
            int length = 0;
            char[] geohash = new char[precision];

            double[] lat = { -90.0, 90.0 };
            double[] lon = { -180.0, 180.0 };

            if (precision < 1 || precision > 20)
                precision = 12;

            while (length < precision)
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
                    geohash[length] = Base32[ch];
                    length++;
                    bit = 0;
                    ch = 0;
                }
            }

            return new string(geohash);
        }
    }
}