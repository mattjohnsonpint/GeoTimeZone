using System;
using System.IO;

namespace GeoTimeZone
{
    public static class TimeZoneLookup
    {
        public static TimeZoneDetails GetTimeZone(double latitude, double longitude)
        {
            var hash = Geohash.Encode(latitude, longitude, 5);

            var lineNumber = GetTzDataLineNumber(hash);

            var timeZone = GetTzFromData(lineNumber);
            if (timeZone != null)
                return GetTimeZoneDetails(timeZone);

            var offsetHours = CalculateOffsetHoursFromLongitude(longitude);
            return GetTimeZoneDetails(offsetHours);
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
                            return int.Parse(midLine.Substring(5));
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
                            return int.Parse(midLine.Substring(5));
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
