using System;
using System.Diagnostics;
using System.Globalization;

namespace GeoTimeZone.DataBuilder
{
    public class ConsoleOutput
    {
        readonly Stopwatch _sw = new Stopwatch();

        public void Start()
        {
            _sw.Start();
        }

        public void Stop()
        {
            _sw.Stop();
            Console.WriteLine();
            Console.WriteLine("Work completed. Hit any key to exit.");
            Console.ReadKey();
        }

        public void WriteProgress(int progress)
        {
            var polygons = progress.ToString(CultureInfo.InvariantCulture).PadLeft(5);

            var line = Console.CursorTop;
            Console.WriteLine("{0} polygons completed. {1:c}", polygons, _sw.Elapsed);
            Console.SetCursorPosition(0, line);
        }
    }
}