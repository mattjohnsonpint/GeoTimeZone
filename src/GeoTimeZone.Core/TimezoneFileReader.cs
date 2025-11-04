using System.IO.Compression;

namespace GeoTimeZone.Core;

internal static class TimezoneFileReader
{
    public static Lazy<Stream> CompressedDataStream;

    private static int LineLength => Geohash.Precision + 3;

    private const int LineEndLength = 1;

    private static readonly Lazy<MemoryStream> LazyData = new(LoadData);
    private static readonly Lazy<int> LazyCount = new(GetCount);

    private static MemoryStream LoadData()
    {
        using var stream = new GZipStream(CompressedDataStream!.Value, CompressionMode.Decompress);

        var ms = new MemoryStream();
        stream.CopyTo(ms);
        ms.Seek(0, SeekOrigin.Begin);
        return ms;
    }

    private static int GetCount() => (int)(LazyData.Value.Length / (LineLength + LineEndLength));

    public static int Count => LazyCount.Value;

#if NET6_0_OR_GREATER || NETSTANDARD2_1
    public static ReadOnlySpan<byte> GetGeohash(int line) => GetLine(line, 0, Geohash.Precision);
#else
    public static byte[] GetGeohash(int line) => GetLine(line, 0, Geohash.Precision);
#endif

    public static int GetLineNumber(int line)
    {
        var digits = GetLine(line, Geohash.Precision, LineLength - Geohash.Precision);
        return GetDigit(digits[2]) + ((GetDigit(digits[1]) + (GetDigit(digits[0]) * 10)) * 10);
    }

    private static int GetDigit(byte b) => b - '0';

#if NET6_0_OR_GREATER || NETSTANDARD2_1
    private static ReadOnlySpan<byte> GetLine(int line, int start, int count)
#else
    private static byte[] GetLine(int line, int start, int count)
#endif
    {
        var index = ((LineLength + LineEndLength) * (line - 1)) + start;
        var stream = LazyData.Value;

#if NET6_0_OR_GREATER || NETSTANDARD2_1
        return new ReadOnlySpan<byte>(stream.GetBuffer(), index, count);
#else
        var buffer = new byte[count];
        Array.Copy(stream.GetBuffer(), index, buffer, 0, count);
        return buffer;
#endif
    }
}
