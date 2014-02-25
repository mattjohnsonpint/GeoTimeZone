using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ionic.Zlib;

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
            
            if (timeZones.Count > 1)
            {
                return new TimeZoneResult { Result = timeZones[0], AlternativeResults = timeZones.Skip(1).ToList() };
            }
            
            var offsetHours = CalculateOffsetHoursFromLongitude(longitude);
            return new TimeZoneResult { Result = GetTimeZoneDetails(offsetHours) };
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
            using (var stream = typeof(TimezoneFileReader).Assembly.GetManifestResourceStream("GeoTimeZone.TZL.dat.gz"))
            using (var gzip = new GZipStream(stream, CompressionMode.Decompress))
            using (var reader = new StreamReader(gzip))
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

        private static TimeZoneDetails GetTimeZoneDetails(string timeZone)
        {
            var parts = timeZone.Split('|');

            return new TimeZoneDetails
            {
                IanaTimeZoneId = parts[0],

                // TODO: Extend the TZL.dat file to include these fields
                WindowsTimeZoneId = string.IsNullOrEmpty(parts[3]) ? null : parts[3],
                StandardOffset = TimeSpan.Zero,
                DaylightOffset = null,
                TwoLetterIsoCountryCode = parts[1],
                ThreeLetterIsoCountryCode = parts[2],
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
