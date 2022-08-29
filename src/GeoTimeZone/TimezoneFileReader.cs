using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;

namespace GeoTimeZone
{
    internal static class TimezoneFileReader
    {
        private const int LineLength = 8;
        private const int LineEndLength = 1;

#if NETSTANDARD1_1
        private static readonly object Locker = new object();
#endif

        private static readonly Lazy<MemoryStream> LazyData = new Lazy<MemoryStream>(LoadData);
        private static readonly Lazy<int> LazyCount = new Lazy<int>(GetCount);

        private static MemoryStream LoadData()
        {
            var ms = new MemoryStream();

#if NETSTANDARD1_1
            Assembly assembly = typeof(TimezoneFileReader).GetTypeInfo().Assembly;
#else
            Assembly assembly = typeof(TimezoneFileReader).Assembly;
#endif

            using Stream compressedStream = assembly.GetManifestResourceStream("GeoTimeZone.TZ.dat.gz");
            using var stream = new GZipStream(compressedStream!, CompressionMode.Decompress);
            if (stream == null)
                throw new InvalidOperationException();

            stream.CopyTo(ms);

            return ms;
        }

        private static int GetCount()
        {
            MemoryStream ms = LazyData.Value;
            return (int)(ms.Length / (LineLength + LineEndLength));
        }

        public static int Count => LazyCount.Value;

        public static
#if NETSTANDARD2_1
            ReadOnlySpan<byte>
#else
            byte[]
#endif
            GetGeohash(int line)
        {
            return GetLine(line, 0, Geohash.Precision);
        }

        public static int GetLineNumber(int line)
        {
            var digits = GetLine(line, Geohash.Precision, LineLength - Geohash.Precision);
            return GetDigit(digits[2]) + ((GetDigit(digits[1]) + (GetDigit(digits[0]) * 10)) * 10);

            static int GetDigit(byte b) => b - '0';
        }

        private static
#if NETSTANDARD2_1_OR_GREATER
            ReadOnlySpan<byte>
#else
            byte[]
#endif
            GetLine(int line, int start, int count)
        {
            int index = (LineLength + LineEndLength) * (line - 1) + start;

            MemoryStream stream = LazyData.Value;

#if NETSTANDARD2_1_OR_GREATER
            return new ReadOnlySpan<byte>(stream.GetBuffer(), index, count);
#elif !NETSTANDARD1_1
            var buffer = new byte[count];
            Array.Copy(stream.GetBuffer(), index, buffer, 0, count);

            return buffer;
#else
            var buffer = new byte[count];

            lock (Locker)
            {
                stream.Position = index;
                stream.Read(buffer, 0, count);
            }

            return buffer;
#endif
        }
    }
}