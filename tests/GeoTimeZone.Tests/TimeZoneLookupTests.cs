﻿using Xunit;

namespace GeoTimeZone.Tests
{

    public class TimeZoneLookupTests
    {
        [Fact]
        public void Can_Lookup_Where_Two_Countries_Meet()
        {
            var france = TimeZoneLookup.GetTimeZone(47.589983, 7.587417);
            Assert.Equal("Europe/Paris", france.Result);
            Assert.Equal("Europe/Zurich", france.AlternativeResults[0]);

            var switzerland = TimeZoneLookup.GetTimeZone(47.58735, 7.588967);
            Assert.Equal("Europe/Zurich", switzerland.Result);
            Assert.Equal("Europe/Paris", switzerland.AlternativeResults[0]);
        }

        [Fact]
        public void Can_Lookup_TimeZone_PaigntonPier()
        {
            var tz = TimeZoneLookup.GetTimeZone(50.4372, -3.5559).Result;
            Assert.Equal("Europe/London", tz);
        }

        [Fact]
        public void Can_Lookup_TimeZone_Phoenix()
        {
            var tz = TimeZoneLookup.GetTimeZone(33.45, -112.0667).Result;
            Assert.Equal("America/Phoenix", tz);
        }

        [Fact]
        public void Can_Lookup_TimeZone_NewYork()
        {
            var tz = TimeZoneLookup.GetTimeZone(40.67, -73.94).Result;
            Assert.Equal("America/New_York", tz);
        }

        [Fact]
        public void Can_Lookup_TimeZone_LosAngeles()
        {
            var tz = TimeZoneLookup.GetTimeZone(34.0500, -118.25).Result;
            Assert.Equal("America/Los_Angeles", tz);
        }

        [Fact]
        public void Can_Lookup_TimeZone_Honolulu()
        {
            var tz = TimeZoneLookup.GetTimeZone(21.3, -157.8167).Result;
            Assert.Equal("Pacific/Honolulu", tz);
        }

        [Fact]
        public void Can_Lookup_TimeZone_London()
        {
            var tz = TimeZoneLookup.GetTimeZone(51.5072, -0.1275).Result;
            Assert.Equal("Europe/London", tz);
        }

        [Fact]
        public void Can_Lookup_TimeZone_SaoPaulo()
        {
            var tz = TimeZoneLookup.GetTimeZone(-23.55, -46.6333).Result;
            Assert.Equal("America/Sao_Paulo", tz);
        }

        [Fact]
        public void Can_Lookup_TimeZone_Sydney()
        {
            var tz = TimeZoneLookup.GetTimeZone(-33.86, 151.2111).Result;
            Assert.Equal("Australia/Sydney", tz);
        }

        [Fact]
        public void Can_Lookup_TimeZone_LordHoweIsland()
        {
            var tz = TimeZoneLookup.GetTimeZone(-31.55, 159.0833).Result;
            Assert.Equal("Australia/Lord_Howe", tz);
        }

        [Fact]
        public void Can_Lookup_TimeZone_Mazatlan()
        {
            var tz = TimeZoneLookup.GetTimeZone(23.22, -106.42).Result;
            Assert.Equal("America/Mazatlan", tz);
        }
    }
}
