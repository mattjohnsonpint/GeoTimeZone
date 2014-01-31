using System;
using System.Diagnostics;

namespace GeoTimeZone.DataBuilder
{
    static class Program
    {
        static void Main()
        {
            var sw = new Stopwatch();
            sw.Start();
            TimeZoneDataBuilder.CreateGeohashData(@".\Data\tz_world.shp", @"..\..\..\GeoTimeZone\");
            sw.Stop();
            Console.WriteLine("Elapsed Time: {0:g}", sw.Elapsed);
        }
    }
}
