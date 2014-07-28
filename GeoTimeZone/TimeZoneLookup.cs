using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GeoTimeZone
{
    public static class TimeZoneLookup
    {
        public static TimeZoneResult GetTimeZone(double latitude, double longitude)
        {
            var geohash = Geohash.Encode(latitude, longitude, 5);

            var lineNumber = GetTzDataLineNumbers(geohash);

            var timeZones = GetTzsFromData(lineNumber).ToList();

            if (timeZones.Count == 1)
            {
                return new TimeZoneResult { Result = timeZones[0] };
            }

            if (timeZones.Count > 1)
            {
                return new TimeZoneResult { Result = timeZones[0], AlternativeResults = timeZones.Skip(1).ToList() };
            }

            var offsetHours = CalculateOffsetHoursFromLongitude(longitude);
            return new TimeZoneResult { Result = GetTimeZoneId(offsetHours) };
        }

        private static IEnumerable<int> GetTzDataLineNumbers(string geohash)
        {
            var seeked = SeekTimeZoneFile(geohash);
            if (seeked == 0)
                return new List<int>();

            long min = seeked, max = seeked;
            var seekedGeohash = TimezoneFileReader.GetLine(seeked).Substring(0, 5);

            while (true)
            {
                var prevGeohash = TimezoneFileReader.GetLine(min - 1).Substring(0, 5);
                if (seekedGeohash == prevGeohash)
                    min--;
                else
                    break;
            }

            while (true)
            {
                var nextGeohash = TimezoneFileReader.GetLine(max + 1).Substring(0, 5);
                if (seekedGeohash == nextGeohash)
                    max++;
                else
                    break;
            }

            var lineNumbers = new List<int>();
            for (var i = min; i <= max; i++)
            {
                var lineNumber = int.Parse(TimezoneFileReader.GetLine(i).Substring(5));
                lineNumbers.Add(lineNumber);
            }

            return lineNumbers;
        }

        private static long SeekTimeZoneFile(string hash)
        {
            var min = 1L;
            var max = TimezoneFileReader.Count;
            var converged = false;

            while (true)
            {
                var mid = ((max - min) / 2) + min;
                var midLine = TimezoneFileReader.GetLine(mid);

                for (int i = 0; i < hash.Length; i++)
                {
                    if (midLine[i] == '-')
                    {
                        return mid;
                    }

                    if (midLine[i] > hash[i])
                    {
                        max = mid == max ? min : mid;
                        break;
                    }
                    if (midLine[i] < hash[i])
                    {
                        min = mid == min ? max : mid;
                        break;
                    }

                    if (i == 4)
                    {
                        return mid;
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
            return 0;
        }

        private static readonly Lazy<IList<string>> LookupData = new Lazy<IList<string>>(LoadLookupData);

        private static IList<string> LoadLookupData()
        {
            var assembly = typeof(TimezoneFileReader).Assembly;
            using (var stream = assembly.GetManifestResourceStream("GeoTimeZone.TZL.dat"))
            {
                if (stream == null)
                    throw new InvalidOperationException();

                using (var reader = new StreamReader(stream))
                {
                    var list = new List<string>();

                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        list.Add(line);
                    }

                    return list;
                }
            }
        }

        private static IEnumerable<string> GetTzsFromData(IEnumerable<int> lineNumbers)
        {
            var lookupData = LookupData.Value;
            return lineNumbers.OrderBy(x => x).Select(x => lookupData[x - 1]);
        }

        private static int CalculateOffsetHoursFromLongitude(double longitude)
        {
            var dir = longitude < 0 ? -1 : 1;
            var posNo = Math.Sqrt(Math.Pow(longitude, 2));
            if (posNo <= 7.5)
                return 0;

            posNo -= 7.5;
            var offset = posNo / 15;
            if (posNo % 15 > 0)
                offset++;

            return dir * (int)Math.Floor(offset);
        }

        private static string GetTimeZoneId(int offsetHours)
        {
            if (offsetHours == 0)
                return "UTC";

            var reversed = (offsetHours >= 0 ? "-" : "+") + Math.Abs(offsetHours);
            return "Etc/GMT" + reversed;
        }
    }
}
