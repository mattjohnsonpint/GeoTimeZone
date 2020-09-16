using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;

namespace GeoTimeZone
{
    /// <summary>
    /// Provides the time zone lookup functionality.
    /// </summary>
    public static class TimeZoneLookup
    {
        /// <summary>
        /// Determines the IANA time zone for given location coordinates.
        /// </summary>
        /// <param name="latitude">The latitude of the location.</param>
        /// <param name="longitude">The longitude of the location.</param>
        /// <returns>A <see cref="TimeZoneResult"/> object, which contains the result(s) of the operation.</returns>
        public static TimeZoneResult GetTimeZone(double latitude, double longitude)
        {
            string geohash = Geohash.Encode(latitude, longitude, 5);
            IEnumerable<int> lineNumber = GetTzDataLineNumbers(geohash);
            string[] timeZones = GetTzsFromData(lineNumber).ToArray();
            if (timeZones.Length != 0)
                return new TimeZoneResult(timeZones);

            int offsetHours = CalculateOffsetHoursFromLongitude(longitude);
            return new TimeZoneResult(GetTimeZoneId(offsetHours));
        }

        private static IEnumerable<int> GetTzDataLineNumbers(string geohash)
        {
            int seeked = SeekTimeZoneFile(geohash);
            if (seeked == 0)
                return new List<int>();

            int min = seeked, max = seeked;
            string seekedGeohash = TimezoneFileReader.GetLine(seeked).Substring(0, 5);

            while (true)
            {
                string prevGeohash = TimezoneFileReader.GetLine(min - 1).Substring(0, 5);
                if (seekedGeohash == prevGeohash)
                    min--;
                else
                    break;
            }

            while (true)
            {
                string nextGeohash = TimezoneFileReader.GetLine(max + 1).Substring(0, 5);
                if (seekedGeohash == nextGeohash)
                    max++;
                else
                    break;
            }

            var lineNumbers = new List<int>();
            for (int i = min; i <= max; i++)
            {
                int lineNumber = int.Parse(TimezoneFileReader.GetLine(i).Substring(5));
                lineNumbers.Add(lineNumber);
            }

            return lineNumbers;
        }

        private static int SeekTimeZoneFile(string hash)
        {
            int min = 1;
            int max = TimezoneFileReader.Count;
            bool converged = false;

            while (true)
            {
                int mid = ((max - min) / 2) + min;
                string midLine = TimezoneFileReader.GetLine(mid);

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

#if NETSTANDARD1_1
            Assembly assembly = typeof(TimeZoneLookup).GetTypeInfo().Assembly;
#else
            Assembly assembly = typeof(TimeZoneLookup).Assembly;
#endif

            using Stream compressedStream = assembly.GetManifestResourceStream("GeoTimeZone.TZL.dat.gz");
            using var stream = new GZipStream(compressedStream!, CompressionMode.Decompress);
            if (stream == null)
                throw new InvalidOperationException();

            using var reader = new StreamReader(stream);
            var list = new List<string>();

            string line;
            while ((line = reader.ReadLine()) != null)
            {
                list.Add(line);
            }

            return list;
        }

        private static IEnumerable<string> GetTzsFromData(IEnumerable<int> lineNumbers)
        {
            IList<string> lookupData = LookupData.Value;
            return lineNumbers.OrderBy(x => x).Select(x => lookupData[x - 1]);
        }

        private static int CalculateOffsetHoursFromLongitude(double longitude)
        {
            int dir = longitude < 0 ? -1 : 1;
            double posNo = Math.Sqrt(Math.Pow(longitude, 2));
            if (posNo <= 7.5)
                return 0;

            posNo -= 7.5;
            double offset = posNo / 15;
            if (posNo % 15 > 0)
                offset++;

            return dir * (int)Math.Floor(offset);
        }

        private static string GetTimeZoneId(int offsetHours)
        {
            if (offsetHours == 0)
                return "UTC";

            string reversed = (offsetHours >= 0 ? "-" : "+") + Math.Abs(offsetHours);
            return "Etc/GMT" + reversed;
        }
    }
}
