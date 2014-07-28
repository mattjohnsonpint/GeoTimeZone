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
            var tz = TimeZoneLookup.GetTimeZone(41.7325, -49.9469).Result;
            Assert.AreEqual("Etc/GMT+3", tz);
        }

        [Test]
        public void Can_Lookup_Offset_SsFortLeeWreck()
        {
            var tz = TimeZoneLookup.GetTimeZone(-27.583333, 83.183333).Result;
            Assert.AreEqual("Etc/GMT-6", tz);
        }

        [Test]
        public void Can_Lookup_Offset_SsLulworthHillWreck()
        {
            var tz = TimeZoneLookup.GetTimeZone(-10.166667, 1).Result;
            Assert.AreEqual("UTC", tz);
        }

        [Test]
        public void Can_Lookup_Offset_Extreme_East()
        {
            var tz = TimeZoneLookup.GetTimeZone(80, 179).Result;
            Assert.AreEqual("Etc/GMT-12", tz);
        }
    }
}
