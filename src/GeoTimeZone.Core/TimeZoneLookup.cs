using System.IO.Compression;

namespace GeoTimeZone.Core;

/// <summary>
/// Provides the time zone lookup functionality.
/// </summary>
internal static class TimeZoneLookup
{
    public static Lazy<Stream> CompressedDataStream;

    /// <summary>
    /// Determines the IANA time zone for given location coordinates.
    /// </summary>
    /// <param name="latitude">The latitude of the location.</param>
    /// <param name="longitude">The longitude of the location.</param>
    /// <returns>A <see cref="TimeZoneResult"/> object, which contains the result(s) of the operation.</returns>
    public static TimeZoneResult GetTimeZone(double latitude, double longitude)
    {
#if NET6_0_OR_GREATER || NETSTANDARD2_1
        Span<byte> geohash = stackalloc byte[Geohash.Precision];
#else
        var geohash = new byte[Geohash.Precision];
#endif
        Geohash.Encode(latitude, longitude, geohash);

        var lineNumbers = GetTzDataLineNumbers(geohash);
        if (lineNumbers.Length != 0)
        {
            var timeZones = GetTimeZonesFromData(lineNumbers, CompressedDataStream);
            return new TimeZoneResult(timeZones);
        }

        var offsetHours = CalculateOffsetHoursFromLongitude(longitude);
        return new TimeZoneResult(GetTimeZoneId(offsetHours));
    }

#if NET6_0_OR_GREATER || NETSTANDARD2_1
    private static int[] GetTzDataLineNumbers(ReadOnlySpan<byte> geohash)
#else
    private static int[] GetTzDataLineNumbers(byte[] geohash)
#endif
    {
        var seeked = SeekTimeZoneFile(geohash);
        if (seeked == 0)
        {
            return Array.Empty<int>();
        }

        int min = seeked, max = seeked;
        var seekedGeohash = TimezoneFileReader.GetGeohash(seeked);

        while (true)
        {
            var prevGeohash = TimezoneFileReader.GetGeohash(min - 1);
            if (GeohashEquals(seekedGeohash, prevGeohash))
            {
                min--;
            }
            else
            {
                break;
            }
        }

        while (true)
        {
            var nextGeohash = TimezoneFileReader.GetGeohash(max + 1);
            if (GeohashEquals(seekedGeohash, nextGeohash))
            {
                max++;
            }
            else
            {
                break;
            }
        }

        var lineNumbers = new int[max - min + 1];
        for (var i = 0; i < lineNumbers.Length; i++)
        {
            lineNumbers[i] = TimezoneFileReader.GetLineNumber(i + min);
        }

        return lineNumbers;
    }

#if NET6_0_OR_GREATER || NETSTANDARD2_1
    private static bool GeohashEquals(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
#else
    private static bool GeohashEquals(byte[] a, byte[] b)
#endif
    {
        var equals = true;
        for (var i = Geohash.Precision - 1; i >= 0; i--)
        {
            equals &= a[i] == b[i];
        }

        return equals;
    }

#if NET6_0_OR_GREATER || NETSTANDARD2_1
    private static int SeekTimeZoneFile(ReadOnlySpan<byte> hash)
#else
    private static int SeekTimeZoneFile(byte[] hash)
#endif
    {
        var min = 1;
        var max = TimezoneFileReader.Count;
        var converged = false;

        while (true)
        {
            var mid = ((max - min) / 2) + min;
            var midLine = TimezoneFileReader.GetGeohash(mid);

            for (var i = 0; i < hash.Length; i++)
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
                {
                    break;
                }

                converged = true;
            }
        }

        return 0;
    }

    private static IList<string> LookupData;

    private static IList<string> LoadLookupData(Lazy<Stream> compressedDataStream)
    {
        using var stream = new GZipStream(compressedDataStream.Value, CompressionMode.Decompress);
        using var reader = new StreamReader(stream);

        var list = new List<string>();
        while (reader.ReadLine() is { } line)
        {
            list.Add(line);
        }

        return list;
    }

    private static List<string> GetTimeZonesFromData(int[] lineNumbers, Lazy<Stream> compressedLookupDataStream)
    {
        LookupData ??= LoadLookupData(compressedLookupDataStream);
        var timezones = new List<string>(lineNumbers.Length);
        Array.Sort(lineNumbers);

        foreach (var lineNumber in lineNumbers)
        {
            timezones.Add(LookupData[lineNumber - 1]);
        }

        return timezones;
    }

    private static int CalculateOffsetHoursFromLongitude(double longitude)
    {
        var dir = longitude < 0 ? -1 : 1;
        var posNo = Math.Abs(longitude);
        if (posNo <= 7.5)
        {
            return 0;
        }

        posNo -= 7.5;
        var offset = posNo / 15;
        if (posNo % 15 > 0)
        {
            offset++;
        }

        return dir * (int)Math.Floor(offset);
    }

    private static string GetTimeZoneId(int offsetHours)
    {
        if (offsetHours == 0)
        {
            return "UTC";
        }

        var reversed = (offsetHours >= 0 ? "-" : "+") + Math.Abs(offsetHours);
        return "Etc/GMT" + reversed;
    }
}
