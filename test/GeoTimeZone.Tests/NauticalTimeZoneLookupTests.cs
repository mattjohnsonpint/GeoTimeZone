using Xunit;

namespace GeoTimeZone.Tests
{
    public class NauticalTimeZoneLookupTests
    {
        [Fact]
        public void Can_Lookup_Offset_TitanticWreck()
        {
            var tz = TimeZoneLookup.GetTimeZone(41.7325, -49.9469).Result;
            Assert.Equal("Etc/GMT+3", tz);
        }

        [Fact]
        public void Can_Lookup_Offset_SsFortLeeWreck()
        {
            var tz = TimeZoneLookup.GetTimeZone(-27.583333, 83.183333).Result;
            Assert.Equal("Etc/GMT-6", tz);
        }

        [Fact]
        public void Can_Lookup_Offset_SsLulworthHillWreck()
        {
            var tz = TimeZoneLookup.GetTimeZone(-10.166667, 1).Result;
            Assert.Equal("UTC", tz);
        }

        [Fact]
        public void Can_Lookup_Offset_Extreme_East()
        {
            var tz = TimeZoneLookup.GetTimeZone(80, 179).Result;
            Assert.Equal("Etc/GMT-12", tz);
        }
    }
}
