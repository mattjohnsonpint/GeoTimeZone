using System;
using System.Diagnostics;

namespace GeoTimeZone.DataBuilder
{
    public class ConsoleOutput
    {
        readonly Stopwatch _sw = new Stopwatch();

        public void Start()
        {
            _sw.Start();
            Console.WriteLine("Started at: {0:g}", DateTime.Now.TimeOfDay);
            Console.WriteLine();
        }

        public void Stop()
        {
            _sw.Stop();
            Console.WriteLine();
            Console.WriteLine("Finished at: {0:g}", DateTime.Now.TimeOfDay);
            Console.WriteLine("Total time: {0:c}", _sw.Elapsed);
            Console.WriteLine();
            Console.WriteLine("Hit any key to exit.");
            Console.ReadKey();
        }

        public void WriteMessage(string message)
        {
            Console.WriteLine("{0:c} - {1}", _sw.Elapsed, message);
        }
    }
}