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

            var timeZones = GetTzsFromData(lineNumber).Select(GetTimeZoneDetails).ToList();

            if (timeZones.Count == 1)
            {
                return new TimeZoneResult { Result = timeZones[0] };
            }
            else if (timeZones.Count > 1)
            {
                return new TimeZoneResult { Result = timeZones[0], AlternativeResults = timeZones.Skip(1).ToList() };
            }
            else
            {
                var offsetHours = CalculateOffsetHoursFromLongitude(longitude);
                return new TimeZoneResult { Result = GetTimeZoneDetails(offsetHours) };
            }
        }

        private static List<long> GetTzDataLineNumbers(string geohash)
        {
            using (var reader = new TimezoneFileReader())
            {
                var seeked = SeekTimeZoneFile(reader, geohash);
                if (seeked == 0)
                    return new List<long>();

                long min = seeked, max = seeked;
                var seekedGeohash = reader.GetLine(seeked).Substring(0, 5);

                while (true)
                {
                    var prevGeohash = reader.GetLine(min - 1).Substring(0, 5);
                    if (seekedGeohash == prevGeohash)
                        min--;
                    else
                        break;
                }

                while (true)
                {
                    var nextGeohash = reader.GetLine(max + 1).Substring(0, 5);
                    if (seekedGeohash == nextGeohash)
                        max++;
                    else
                        break;
                }

                var lineNumbers = new List<long>();
                for (var i = min; i <= max; i++)
                {
                    var lineNumber = int.Parse(reader.GetLine(i).Substring(5));
                    lineNumbers.Add(lineNumber);
                }

                return lineNumbers;
            }
        }

        private static long SeekTimeZoneFile(TimezoneFileReader reader, string hash)
        {
            var min = 1L;
            var max = reader.Count;
            var converged = false;

            while (true)
            {
                var mid = ((max - min) / 2) + min;
                var midLine = reader.GetLine(mid);

                for (int i = 0; i < hash.Length; i++)
                {
                    if (midLine[i] == '-')
                    {
                        return mid;
                    }

                    if (midLine[i] > hash[i])
                    {
                        if (max == mid)
                            max = min; // make sure we don't get stuck in a loop
                        else
                            max = mid;
                        break;
                    }
                    if (midLine[i] < hash[i])
                    {
                        if (min == mid)
                            min = max; // make sure we don't get stuck in a loop
                        else
                            min = mid;
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

        private static List<string> GetTzsFromData(List<long> lineNumbers)
        {
            if (lineNumbers.Count == 0)
                return new List<string>();

            lineNumbers.Sort();

            using (var stream = typeof(TimezoneFileReader).Assembly.GetManifestResourceStream("GeoTimeZone.TZL.dat"))
            using (var reader = new StreamReader(stream))
            {
                var results = new List<string>();

                for (long lineNo = 1; lineNo <= lineNumbers[lineNumbers.Count - 1]; lineNo++)
                {
                    var text = reader.ReadLine();
                    if (lineNumbers.Contains(lineNo))
                    {
                        results.Add(text);
                    }
                }
                return results;
            }
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

        private static TimeZoneDetails GetTimeZoneDetails(string timeZone)
        {
            return new TimeZoneDetails
            {
                IanaTimeZoneId = timeZone,

                // TODO: Extend the TZL.dat file to include these fields
                WindowsTimeZoneId = null,
                StandardOffset = TimeSpan.Zero,
                DaylightOffset = null,
                TwoLetterIsoCountryCode = null,
                ThreeLetterIsoCountryCode = null,
                GeneralEnglishName = null,
                StandardEnglishName = null,
                DaylightEnglishName = null,
                GeneralAbbreviation = null,
                StandardAbbreviation = null,
                DaylightAbbreviation = null
            };
        }

        private static TimeZoneDetails GetTimeZoneDetails(int offsetHours)
        {
            if (offsetHours == 0)
                return GetTimeZoneDetailsForUtc();

            var normal = (offsetHours >= 0 ? "+" : "-") + Math.Abs(offsetHours);
            var reversed = (offsetHours >= 0 ? "-" : "+") + Math.Abs(offsetHours);

            return new TimeZoneDetails
            {
                IanaTimeZoneId = "Etc/GMT" + reversed,
                WindowsTimeZoneId = null,
                StandardOffset = TimeSpan.FromHours(offsetHours),
                DaylightOffset = null,
                TwoLetterIsoCountryCode = "ZZ",
                ThreeLetterIsoCountryCode = "ZZZ",
                GeneralEnglishName = "UTC" + normal,
                StandardEnglishName = "UTC" + normal,
                DaylightEnglishName = null,
                GeneralAbbreviation = "UTC" + normal,
                StandardAbbreviation = "UTC" + normal,
                DaylightAbbreviation = null
            };
        }

        private static TimeZoneDetails GetTimeZoneDetailsForUtc()
        {
            return new TimeZoneDetails
            {
                IanaTimeZoneId = "UTC",
                WindowsTimeZoneId = "UTC",
                StandardOffset = TimeSpan.Zero,
                DaylightOffset = null,
                TwoLetterIsoCountryCode = "ZZ",
                ThreeLetterIsoCountryCode = "ZZZ",
                GeneralEnglishName = "Universal Coordinated Time",
                StandardEnglishName = "Universal Coordinated Time",
                DaylightEnglishName = null,
                GeneralAbbreviation = "UTC",
                StandardAbbreviation = "UTC",
                DaylightAbbreviation = null
            };
        }
    }
}
