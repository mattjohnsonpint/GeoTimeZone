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
            var hash = Geohash.Encode(latitude, longitude, 5);

            var lineNumber = GetTzDataLineNumbers(hash);

            var timeZones = GetTzsFromData(lineNumber);

            var details = timeZones.Select(GetTimeZoneDetails).ToList();

            if (details.Count == 1)
            {
                return new TimeZoneResult {Result = details[0]};
            }
            else if (details.Count > 1)
            {
                return new TimeZoneResult { Result = details[0], AlternativeResults = details.Skip(1).ToList() };
            }
            else
            {
                var offsetHours = CalculateOffsetHoursFromLongitude(longitude);
                return new TimeZoneResult { Result = GetTimeZoneDetails(offsetHours) };
            }
        }

        private static List<long> GetTzDataLineNumbers(string hash)
        {
            using (var reader = new TimezoneFileReader())
            {
                var seeked = SeekTimeZoneFile(reader, hash);
                if (seeked == 0)
                    return new List<long>();

                long min = seeked, max = seeked;
                var geohash = reader.GetLine(seeked).Substring(0, 5);

                while (true)
                {
                    var hash2 = reader.GetLine(min - 1).Substring(0, 5);
                    if (geohash == hash2)
                        min--;
                    else
                        break;
                }

                while (true)
                {
                    var hash2 = reader.GetLine(max + 1).Substring(0, 5);
                    if (geohash == hash2)
                        max++;
                    else
                        break;
                }

                var lines = new List<long>();
                for (var i = min; i <= max; i++)
                {
                    var l = int.Parse(reader.GetLine(i).Substring(5));
                    lines.Add(l);
                }

                return lines;
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
                        return mid; int.Parse(midLine.Substring(5));
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
                        return mid; int.Parse(midLine.Substring(5));
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

                for (long line = 1; line <= lineNumbers[lineNumbers.Count - 1]; line++)
                {
                    var text = reader.ReadLine();
                    if (lineNumbers.Contains(line))
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
