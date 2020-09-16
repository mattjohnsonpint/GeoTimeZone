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

#if !NETSTANDARD2_1
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
            return (int) (ms.Length / (LineLength + LineEndLength));
        }

        public static int Count => LazyCount.Value;

        public static string GetLine(int line)
        {
            int index = (LineLength + LineEndLength) * (line - 1);

            MemoryStream stream = LazyData.Value;

#if NETSTANDARD2_1
            var span = new ReadOnlySpan<byte>(stream.GetBuffer(), index, LineLength);
            return Encoding.UTF8.GetString(span);
#else
            var buffer = new byte[LineLength];

            lock (Locker)
            {
                stream.Position = index;
                stream.Read(buffer, 0, LineLength);
            }

            return Encoding.UTF8.GetString(buffer, 0, buffer.Length);
#endif
        }
    }
}