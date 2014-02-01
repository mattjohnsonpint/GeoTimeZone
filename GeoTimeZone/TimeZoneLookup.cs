using System;
using System.IO;

namespace GeoTimeZone
{
    public static class TimeZoneLookup
    {
        public static string GetTimeZone(double latitude, double longitude)
        {
            var hash = Geohash.Encode(latitude, longitude, 5);
            
            var lineNumber = GetTzDataLineNumber(hash);
            
            var timeZone = GetTzFromData(lineNumber);

            if (string.IsNullOrWhiteSpace(timeZone))
            {
                timeZone = CalculateOffsetFromLongitude(longitude);
            }
            
            return timeZone;
        }

        private static int GetTzDataLineNumber(string hash)
        {
            using (var stream = new TimezoneFileReader())
            {
                var min = 1L;
                var max = stream.Count;
                var converged = false;

                while (true)
                {
                    var mid = ((max - min) / 2) + min;
                    var midLine = stream.GetLine(mid);

                    for (int i = 0; i < hash.Length; i++)
                    {
                        if (midLine[i] == '-')
                        {
                            return int.Parse(midLine.Split('|')[1]);
                        }

                        if (midLine[i] > hash[i])
                        {
                            max = mid;
                            break;
                        }
                        if (midLine[i] < hash[i])
                        {
                            min = mid;
                            break;
                        }

                        if (i == 4)
                        {
                            return int.Parse(midLine.Split('|')[1]);
                        }

                        if (min == mid)
                        {
                            min = max;
                            break;
                        }
                    }

                    if (min == max)
                    {
                        if (converged)
                            break;

                        converged = true;
                    }
                }

            }
            return 0;
        }

        private static string GetTzFromData(int lineNumber)
        {
            if (lineNumber == 0)
                return null;

            using (var stream = typeof(TimezoneFileReader).Assembly.GetManifestResourceStream("GeoTimeZone.TZL.dat"))
            using (var reader = new StreamReader(stream))
            {
                string result = null;
                for (int i = 0; i < lineNumber; i++)
                {
                    result = reader.ReadLine();
                }
                return result;
            }
        }

        private static string CalculateOffsetFromLongitude(double longitude)
        {
            var dir = longitude < 0 ? -1 : 1;
            var posNo = Math.Sqrt(Math.Pow(longitude, 2));
            if (posNo <= 7.5)
            {
                return "UTC";
            }
            else
            {
                posNo -= 7.5;
                var offset = posNo / 15;
                if (posNo % 15 > 0)
                {
                    offset++;
                }

                offset = (int) Math.Floor(offset);
                return string.Format("UTC{0}{1:00}", dir == 1 ? "+" : "-", offset);
            }
        }
    }
}
