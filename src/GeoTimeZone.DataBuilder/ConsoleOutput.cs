using System;
using System.Diagnostics;

namespace GeoTimeZone.DataBuilder
{
    public class ConsoleOutput
    {
        private readonly Stopwatch _sw = new Stopwatch();

        public void Start()
        {
            _sw.Start();
            Console.WriteLine("Started at: {0}", GetLocalDisplayTime());
            Console.WriteLine();
        }

        public void Stop()
        {
            _sw.Stop();
            Console.WriteLine();
            Console.WriteLine("Finished at: {0}", GetLocalDisplayTime());
            Console.WriteLine("Total elapsed time: {0:hh\\:mm\\:ss\\.fff}", _sw.Elapsed);
            Console.WriteLine();
            Console.WriteLine("Hit any key to exit.");
            Console.ReadKey();
        }

        public void WriteMessage(string message)
        {
            Console.WriteLine("{0} - {1}", GetLocalDisplayTime(), message);
        }

        private static string GetLocalDisplayTime()
        {
            return DateTime.Now.ToString("HH:mm:ss.fff");
        }
    }
}
