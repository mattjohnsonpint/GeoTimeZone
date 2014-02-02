using System;
using NUnit.Framework;

namespace GeoTimeZone.Tests
{
    [TestFixture]
    public class NauticalTimeZoneLookupTests
    {
        [Test]
        public void Can_Lookup_Offset_TitanticWreck()
        {
            var tz = TimeZoneLookup.GetTimeZone(41.7325, -49.9469);
            Assert.AreEqual("Etc/GMT+3", tz.IanaTimeZoneId);
            Assert.AreEqual("UTC-3", tz.StandardAbbreviation);
            Assert.AreEqual(TimeSpan.FromHours(-3), tz.StandardOffset);
        }

        [Test]
        public void Can_Lookup_Offset_SsFortLeeWreck()
        {
            var tz = TimeZoneLookup.GetTimeZone(-27.583333, 83.183333);
            Assert.AreEqual("Etc/GMT-6", tz.IanaTimeZoneId);
            Assert.AreEqual("UTC+6", tz.StandardAbbreviation);
            Assert.AreEqual(TimeSpan.FromHours(6), tz.StandardOffset);
        }

        [Test]
        public void Can_Lookup_Offset_SsLulworthHillWreck()
        {
            var tz = TimeZoneLookup.GetTimeZone(-10.166667, 1);
            Assert.AreEqual("UTC", tz.IanaTimeZoneId);
            Assert.AreEqual("UTC", tz.StandardAbbreviation);
            Assert.AreEqual(TimeSpan.FromHours(0), tz.StandardOffset);
        }

        [Test]
        public void Can_Lookup_Offset_Extreme_East()
        {
            var tz = TimeZoneLookup.GetTimeZone(80, 179);
            Assert.AreEqual("Etc/GMT-12", tz.IanaTimeZoneId);
            Assert.AreEqual("UTC+12", tz.StandardAbbreviation);
            Assert.AreEqual(TimeSpan.FromHours(12), tz.StandardOffset);
        }

        [Test]
        public void Can_Lookup_Offset_Extreme_West()
        {
            var tz = TimeZoneLookup.GetTimeZone(80, -179);
            Assert.AreEqual("Etc/GMT+12", tz.IanaTimeZoneId);
            Assert.AreEqual("UTC-12", tz.StandardAbbreviation);
            Assert.AreEqual(TimeSpan.FromHours(-12), tz.StandardOffset);
        }
    }
}
