using System.Linq;
using GeoAPI.Geometries;
using GeoTimeZone.DataBuilder;
using NUnit.Framework;

namespace GeoTimeZone.Tests
{
    [TestFixture]
    public class GeohashListTests
    {
        [Test]
        public void SplitEnvelope2_1()
        {
            var envelopes = GeohashLevelList.SplitEnvelope2(new Envelope(-180, 180, -90, 90), true).ToList();

            Assert.AreEqual(new Envelope(-180, 0, -90, 90), envelopes[0]);
            Assert.AreEqual(new Envelope(0, 180, -90, 90), envelopes[1]);
        }

        [Test]
        public void SplitEnvelope2_2()
        {
            var envelopes = GeohashLevelList.SplitEnvelope2(new Envelope(-180, 180, -90, 90), false).ToList();

            Assert.AreEqual(new Envelope(-180, 180, -90, 0), envelopes[0]);
            Assert.AreEqual(new Envelope(-180, 180, 0, 90), envelopes[1]);
        }

        [Test]
        public void SplitEnvelope4_1()
        {
            var envelopes = GeohashLevelList.SplitEnvelope4(new Envelope(-180, 0, -90, 90), true).ToList();

            Assert.AreEqual(new Envelope(-180, -90, -90, 0), envelopes[0]);
            Assert.AreEqual(new Envelope(-90, 0, -90, 0), envelopes[1]);
            Assert.AreEqual(new Envelope(-180, -90, 0, 90), envelopes[2]);
            Assert.AreEqual(new Envelope(-90, 0, 0, 90), envelopes[3]);
        }

        [Test]
        public void SplitEnvelope4_2()
        {
            var envelopes = GeohashLevelList.SplitEnvelope4(new Envelope(-180, 0, -90, 90), false).ToList();

            Assert.AreEqual(new Envelope(-180, -90, -90, 0), envelopes[0]);
            Assert.AreEqual(new Envelope(-180, -90, 0, 90), envelopes[1]);
            Assert.AreEqual(new Envelope(-90, 0, -90, 0), envelopes[2]);
            Assert.AreEqual(new Envelope(-90, 0, 0, 90), envelopes[3]);
        }

        [Test]
        public void Temp2()
        {
            var envelopes = GeohashLevelList.GetNextLevel();

            Assert.AreEqual(32, envelopes.Count());

            var envelopes2 = new GeohashLevelList();

            Assert.AreEqual(32, envelopes2.Count);
        }

        //[Test]
        //public void Temp3()
        //{
        //    var sw = new Stopwatch();
        //    sw.Start();
        //    TimeZoneLookup.Geohash();
        //    //Console.WriteLine(.Count);
        //    sw.Stop();
        //    Console.WriteLine(sw.ElapsedMilliseconds);
        //    Assert.True(true);
        //}
    }
}