using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace GeoTimeZone
{
    public class TimeZoneResult
    {
        internal TimeZoneResult(params string[] timeZones)
        {
            if (timeZones.Length == 0)
            {
                throw new ArgumentException("There must be at least one value provided.", nameof(timeZones));
            }

            this.Result = timeZones[0];
            this.AlternativeResults = new ReadOnlyCollection<string>(timeZones.Skip(1).ToList());
        }

        public string Result { get; }
        public ReadOnlyCollection<string> AlternativeResults { get; }
    }
}