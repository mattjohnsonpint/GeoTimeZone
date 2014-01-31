using System;
using System.IO;
using System.Text;

namespace GeoTimeZone
{
    internal class TimezoneFileReader : IDisposable
    {
        private const int LineLength = 9;
        private const int LineEndLength = 1;

        private readonly Stream _stream;

        public TimezoneFileReader()
        {
            _stream = typeof (TimezoneFileReader).Assembly.GetManifestResourceStream("GeoTimeZone.TZ.dat");
            Count = _stream.Length / (LineLength + LineEndLength);
        }

        public long Count { get; private set; }

        public string GetLine(long line)
        {
            var index = ((LineLength + LineEndLength)) * (line - 1);

            _stream.Position = index;

            var buffer = new byte[LineLength];

            _stream.Read(buffer, 0, buffer.Length);

            return Encoding.UTF8.GetString(buffer, 0 , buffer.Length);
        }

        public void Dispose()
        {
            _stream.Dispose();
        }
    }
}