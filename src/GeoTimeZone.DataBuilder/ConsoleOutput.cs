using System.Diagnostics;

namespace GeoTimeZone.DataBuilder;

public static class ConsoleOutput
{
    private static readonly Stopwatch Stopwatch = new();

    public static void Start()
    {
        Stopwatch.Start();
        Console.WriteLine("Started at: {0}", GetLocalDisplayTime());
        Console.WriteLine();
    }

    public static void Stop()
    {
        Stopwatch.Stop();
        Console.WriteLine();
        Console.WriteLine("Finished at: {0}", GetLocalDisplayTime());
        Console.WriteLine("Total elapsed time: {0:hh\\:mm\\:ss\\.fff}", Stopwatch.Elapsed);
        Console.WriteLine();
    }

    public static void WriteMessage(string message)
    {
        Console.WriteLine("{0} - {1}", GetLocalDisplayTime(), message);
    }

    private static string GetLocalDisplayTime()
    {
        return DateTime.Now.ToString("HH:mm:ss.fff");
    }
}
