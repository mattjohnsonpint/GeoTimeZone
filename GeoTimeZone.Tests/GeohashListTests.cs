using System.Linq;
using GeoAPI.Geometries;
using GeoTimeZone.DataBuilder;
using Xunit;

namespace GeoTimeZone.Tests
{
    public class GeohashListTests
    {
        [Fact]
        public void SplitEnvelope2_1()
        {
            var envelopes = GeohashTree.SplitEnvelope2(new Envelope(-180, 180, -90, 90), true).ToList();

            Assert.Equal(new Envelope(-180, 0, -90, 90), envelopes[0]);
            Assert.Equal(new Envelope(0, 180, -90, 90), envelopes[1]);
        }

        [Fact]
        public void SplitEnvelope2_2()
        {
            var envelopes = GeohashTree.SplitEnvelope2(new Envelope(-180, 180, -90, 90), false).ToList();

            Assert.Equal(new Envelope(-180, 180, -90, 0), envelopes[0]);
            Assert.Equal(new Envelope(-180, 180, 0, 90), envelopes[1]);
        }

        [Fact]
        public void SplitEnvelope4_1()
        {
            var envelopes = GeohashTree.SplitEnvelope4(new Envelope(-180, 0, -90, 90), true).ToList();

            Assert.Equal(new Envelope(-180, -90, -90, 0), envelopes[0]);
            Assert.Equal(new Envelope(-90, 0, -90, 0), envelopes[1]);
            Assert.Equal(new Envelope(-180, -90, 0, 90), envelopes[2]);
            Assert.Equal(new Envelope(-90, 0, 0, 90), envelopes[3]);
        }

        [Fact]
        public void SplitEnvelope4_2()
        {
            var envelopes = GeohashTree.SplitEnvelope4(new Envelope(-180, 0, -90, 90), false).ToList();

            Assert.Equal(new Envelope(-180, -90, -90, 0), envelopes[0]);
            Assert.Equal(new Envelope(-180, -90, 0, 90), envelopes[1]);
            Assert.Equal(new Envelope(-90, 0, -90, 0), envelopes[2]);
            Assert.Equal(new Envelope(-90, 0, 0, 90), envelopes[3]);
        }

        [Fact]
        public void Temp2()
        {
            var envelopes = GeohashTree.GetNextLevel();

            Assert.Equal(32, envelopes.Count());

            var envelopes2 = new GeohashTree();

            Assert.Equal(32, envelopes2.Count);
        }

        //[Fact]
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