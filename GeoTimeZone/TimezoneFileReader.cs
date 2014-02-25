using System;
using System.IO;
using System.Text;
using GeoTimeZone.Imports.Ionic.Zlib;

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

            using (var stream = typeof(TimezoneFileReader).Assembly.GetManifestResourceStream("GeoTimeZone.TZ.dat.gz"))
            using (var gzip = new GZipStream(stream, CompressionMode.Decompress))
            {
                gzip.CopyTo(ms);
            }

            return ms;
        }

        private static long GetCount()
        {
            var ms = LazyData.Value;
            return ms.Length/(LineLength + LineEndLength);
        }

        public static long Count
        {
            get { return LazyCount.Value; }
        }

        public static string GetLine(long line)
        {
            var index = (LineLength + LineEndLength) * (line - 1);

            var buffer = new byte[LineLength];

            lock (Locker)
            {
                var stream = LazyData.Value;
                stream.Position = index;
                stream.Read(buffer, 0, LineLength);
            }

            return Encoding.UTF8.GetString(buffer, 0, buffer.Length);
        }
    }
}