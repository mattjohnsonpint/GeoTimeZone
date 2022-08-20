using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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
#if NETSTANDARD2_1_OR_GREATER
            Span<byte> geohash = stackalloc byte[Geohash.Precision];
#else
            byte[] geohash = new byte[Geohash.Precision];
#endif
            Geohash.Encode(latitude, longitude, geohash);

            int[] lineNumbers = GetTzDataLineNumbers(geohash);
            if (lineNumbers.Length != 0)
            {
                List<string> timeZones = GetTzsFromData(lineNumbers);
                return new TimeZoneResult(timeZones);
            }

            int offsetHours = CalculateOffsetHoursFromLongitude(longitude);
            return new TimeZoneResult(GetTimeZoneId(offsetHours));
        }

        private static int[] GetTzDataLineNumbers(
#if NETSTANDARD2_1_OR_GREATER
            ReadOnlySpan<byte> geohash
#else
            byte[] geohash
#endif
            )
        {
            int seeked = SeekTimeZoneFile(geohash);
            if (seeked == 0)
                return new int[0];

            int min = seeked, max = seeked;
            var seekedGeohash = TimezoneFileReader.GetGeohash(seeked);

            while (true)
            {
                var prevGeohash = TimezoneFileReader.GetGeohash(min - 1);
                if (GeohashEquals(seekedGeohash, prevGeohash))
                    min--;
                else
                    break;
            }

            while (true)
            {
                var nextGeohash = TimezoneFileReader.GetGeohash(max + 1);
                if (GeohashEquals(seekedGeohash, nextGeohash))
                    max++;
                else
                    break;
            }

            var lineNumbers = new int[max - min + 1];
            for (int i = 0; i < lineNumbers.Length; i++)
            {
                lineNumbers[i] = TimezoneFileReader.GetLineNumber(i + min);
            }

            return lineNumbers;
        }

        private static bool GeohashEquals
#if NETSTANDARD2_1_OR_GREATER
            (ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
#else
            (byte[] a, byte[] b)
#endif
        {
            bool equals = true;
            for (int i = Geohash.Precision - 1; i >= 0; i--)
            {
                equals &= a[i] == b[i];
            }

            return equals;
        }

        private static int SeekTimeZoneFile(
#if NETSTANDARD2_1_OR_GREATER
            ReadOnlySpan<byte> hash
#else
            byte[] hash
#endif
            )
        {
            int min = 1;
            int max = TimezoneFileReader.Count;
            bool converged = false;

            while (true)
            {
                int mid = ((max - min) / 2) + min;
                var midLine = TimezoneFileReader.GetGeohash(mid);

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

        private static List<string> GetTzsFromData(int[] lineNumbers)
        {
            IList<string> lookupData = LookupData.Value;
            var timezones = new List<string>(lineNumbers.Length);
            Array.Sort(lineNumbers);

            foreach (var lineNumber in lineNumbers)
            {
                timezones.Add(lookupData[lineNumber - 1]);
            }

            return timezones;
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
