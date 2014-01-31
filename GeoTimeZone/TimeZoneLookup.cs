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
    }
}
