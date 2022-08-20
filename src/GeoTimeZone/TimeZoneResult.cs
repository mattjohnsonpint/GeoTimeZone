using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace GeoTimeZone
{
    /// <summary>
    /// Contains the result of a time zone lookup operation.
    /// </summary>
    public class TimeZoneResult
    {
        internal TimeZoneResult(string[] timeZones)
        {
            this.Result = timeZones[0];
            this.AlternativeResults = new ReadOnlyCollection<string>(timeZones.Skip(1).ToList());
        }

        internal TimeZoneResult(string timeZone)
        {
            this.Result = timeZone;
            this.AlternativeResults = new ReadOnlyCollection<string>(new List<string>());
        }

        /// <summary>
        /// Gets the primary result of the time zone lookup operation.
        /// </summary>
        public string Result { get; }

        /// <summary>
        /// Gets any alternative results of the time zone lookup operation.
        /// This usually happens very close to borders between time zones.
        /// </summary>
        public ReadOnlyCollection<string> AlternativeResults { get; }
    }
}