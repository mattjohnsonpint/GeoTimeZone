using System;
using System.IO;
using System.Text;

namespace GeoTimeZone
{
    public static class TimeZoneLookup
    {
        public static string GetTimeZone(double latitude, double longitude)
        {
            var hash = Geohash.Encode(latitude, longitude, 5);
            var no = GetTzLineNumber(hash);

            if (no == 0)
                return null;

            using (var stream = typeof(TimezoneFileReader).Assembly.GetManifestResourceStream("GeoTimeZone.TZL.dat"))
            using (var reader = new StreamReader(stream))
            {
                string result = null;
                for (int i = 0; i < no; i++)
                {
                    result = reader.ReadLine();
                }
                return result;
            }

        }

        private static int GetTzLineNumber(string hash)
        {
            using (var stream = new TimezoneFileReader())
            {
                var min = 1l;
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
                        {
                            break;
                        }
                        else
                        {
                            converged = true;
                        }
                    }
                }

            }
            return 0;
        }
    }

    internal class TimezoneFileReader : IDisposable
    {
        private const int LineLength = 9;
        private const int LineEndLength = 1;

        private readonly Stream _stream;

        public TimezoneFileReader()
        {
            _stream = typeof (TimezoneFileReader).Assembly.GetManifestResourceStream("GeoTimeZone.TZ.dat");
            Count = _stream.Length / (LineLength + LineEndLength);
        }

        public long Count { get; private set; }

        public string GetLine(long line)
        {
            var index = ((LineLength + LineEndLength)) * (line - 1);

            _stream.Position = index;

            var buffer = new byte[LineLength];

            _stream.Read(buffer, 0, buffer.Length);

            return Encoding.UTF8.GetString(buffer, 0 , buffer.Length);
        }

        public void Dispose()
        {
            _stream.Dispose();
        }
    }
}
