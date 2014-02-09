using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace GeoTimeZone.DataBuilder
{
    public class WindowsTimeZoneFileReader
    {
        private readonly string _cdlrFilePath;

        public WindowsTimeZoneFileReader(string cdlrFilePath)
        {
            _cdlrFilePath = cdlrFilePath;
        }

        static readonly Dictionary<string, string> Fixes = new Dictionary<string, string>
            {
				{ "America/Adak", "Hawaiian Standard Time" },
				{ "America/Atikokan", "Eastern Standard Time" },
				{ "America/Metlakatla", "Pacific Standard Time" },
				{ "America/Miquelon", "" },
				{ "Asia/Gaza", "" },
				{ "Asia/Hebron", "" },
				{ "Asia/Ho_Chi_Minh", "" },
				{ "Atlantic/Faroe", "W. Europe Standard Time" },
				{ "Australia/Eucla", "" },
				{ "Australia/Lord_Howe", "" },
				{ "Pacific/Chatham", "" },
				{ "Pacific/Chuuk", "" },
				{ "Pacific/Easter", "" },
				{ "Pacific/Gambier", "" },
				{ "Pacific/Kiritimati", "" },
				{ "Pacific/Marquesas", "" },
				{ "Pacific/Norfolk", "" },
				{ "Pacific/Pitcairn", "" },
				{ "Pacific/Pohnpei", "" },
            };  

        public Dictionary<string, string> Read()
        {
            var doc = XDocument.Load(_cdlrFilePath);

            var tzs = doc.Root.Descendants("windowsZones").Single().Descendants("mapTimezones").Single().Descendants();

            var results = new Dictionary<string, string>();

            foreach (var tz in tzs)
            {
                var winTz = tz.Attribute("other").Value;
                var tzNames = tz.Attribute("type").Value.Split(' ');

                foreach (var key in tzNames)
                {
                    var key1 = Helpers.CleanseTimeZoneName(key);

                    string u;
                    if (results.TryGetValue(key1, out u))
                    {
                        if (u != winTz)
                            throw new Exception("Problem with CLDR data file; IANA time zone duplicated.");
                    }
                    else
                    {
                        results[key1] = winTz;
                    }
                }
            }

            // Not all time zones are in the CLDR
            // Apply fixes for those time zones we want to fix
            foreach (var fix in Fixes)
            {
                if (!results.ContainsKey(fix.Key))
                    results[fix.Key] = fix.Value;
            }

            return results;
        }
    }
}
