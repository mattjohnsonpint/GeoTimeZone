using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;

namespace GeoTimeZone
{
    internal static class TimezoneFileReader
    {
        private const int LineLength = 8;
        private const int LineEndLength = 1;

        private static readonly object Locker = new object();
        private static readonly Lazy<MemoryStream> LazyData = new Lazy<MemoryStream>(LoadData);
        private static readonly Lazy<long> LazyCount = new Lazy<long>(GetCount);

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

        private static long GetCount()
        {
            MemoryStream ms = LazyData.Value;
            return ms.Length/(LineLength + LineEndLength);
        }

        public static long Count => LazyCount.Value;

        public static string GetLine(long line)
        {
            long index = (LineLength + LineEndLength) * (line - 1);

            var buffer = new byte[LineLength];

            lock (Locker)
            {
                MemoryStream stream = LazyData.Value;
                stream.Position = index;
                stream.Read(buffer, 0, LineLength);
            }

            return Encoding.UTF8.GetString(buffer, 0, buffer.Length);
        }
    }
}