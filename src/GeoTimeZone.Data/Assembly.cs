using System.Reflection;

namespace GeoTimeZone
{
    /// <summary>
    /// Holds a reference to the assembly containing the embedded time zone data.
    /// </summary>
    public static class Data
    {
        public static readonly Assembly Assembly = typeof(Data).Assembly;
    }
}
