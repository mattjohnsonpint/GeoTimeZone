namespace GeoTimeZone.Tests;

public class TimeZoneLookupTests
{
    [Theory]
    // Historical border reports: #24, #42, #45, #46, #54, and #66.
    [InlineData(49.0427211, -122.8466416, "America/Vancouver")]
    [InlineData(35.8894012908865, -5.30445597754788, "Africa/Ceuta")]
    [InlineData(34.15, -114.26, "America/Phoenix")]
    [InlineData(-28.1978, 153.5346, "Australia/Sydney")]
    [InlineData(23.88, 91.25, "Asia/Kolkata")]
    [InlineData(69.147532, 29.2426967, "Europe/Oslo")]
    // Airport examples from discussion #73.
    [InlineData(34.528889, -114.431972, "America/Los_Angeles")]
    [InlineData(45.671778, -116.305722, "America/Los_Angeles")]
    public void Resolves_Border_Points_Without_Returning_Nearby_Zones(
        double latitude, double longitude, string primary)
    {
        var result = TimeZoneLookup.GetTimeZone(latitude, longitude);

        Assert.Equal(primary, result.Result);
        Assert.Empty(result.AlternativeResults);
    }

    [Fact]
    public void Resolves_The_Indiana_Runway_From_Three_Candidate_Zones()
    {
        // Indiana runway from discussion #73: the other two candidate zones
        // intersect its cell, but do not contain the runway itself.
        var result = TimeZoneLookup.GetTimeZone(38.694506, -85.198286);

        Assert.Equal("America/Indiana/Vevay", result.Result);
        Assert.Empty(result.AlternativeResults);
    }

    [Fact]
    public void Distinguishes_French_And_Swiss_Points_That_Shared_A_Geohash_Cell()
    {
        // Original regression from #14, previously impossible to resolve by
        // assigning one result to their shared geohash cell.
        var france = TimeZoneLookup.GetTimeZone(47.589983, 7.587417);
        var switzerland = TimeZoneLookup.GetTimeZone(47.58735, 7.588967);
        Assert.Equal("Europe/Paris", france.Result);
        Assert.Equal("Europe/Zurich", switzerland.Result);
        Assert.Empty(france.AlternativeResults);
        Assert.Empty(switzerland.AlternativeResults);
    }

    [Fact]
    public void Single_Candidate_Has_No_Alternatives()
    {
        var result = TimeZoneLookup.GetTimeZone(33.45, -112.0667);

        Assert.Equal("America/Phoenix", result.Result);
        Assert.Empty(result.AlternativeResults);
    }

    [Fact]
    public void Nautical_Fallback_Has_No_Alternatives()
    {
        var result = TimeZoneLookup.GetTimeZone(41.7325, -49.9469);

        Assert.Equal("Etc/GMT+3", result.Result);
        Assert.Empty(result.AlternativeResults);
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

    [Fact]
    public void Can_Lookup_TimeZone_Kyiv()
    {
        var tz = TimeZoneLookup.GetTimeZone(50.45, 30.523333).Result;
        Assert.Equal("Europe/Kyiv", tz);
    }

    [Fact]
    public void Can_Lookup_TimeZone_Ciudad_Juarez()
    {
        var tz = TimeZoneLookup.GetTimeZone(31.738581, -106.487014).Result;
        Assert.Equal("America/Ciudad_Juarez", tz);
    }

    [Fact]
    public void Can_Lookup_TimeZone_Copenhagen()
    {
        var tz = TimeZoneLookup.GetTimeZone(55.6712398, 12.5114246).Result;
        Assert.Equal("Europe/Copenhagen", tz);
    }
}
