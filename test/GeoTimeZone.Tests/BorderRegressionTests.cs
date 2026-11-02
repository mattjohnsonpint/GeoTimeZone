namespace GeoTimeZone.Tests;

public class BorderRegressionTests
{
    [Theory]
    // #34, #47, #55, #58, and the 26 coordinates from #60.
    [InlineData(59.470977, 24.708448, "Europe/Tallinn")]
    [InlineData(32.4681, -85.00083, "America/New_York")]
    [InlineData(59.37, 28.19, "Europe/Tallinn")]
    [InlineData(35.0959, -114.598, "America/Phoenix")]
    [InlineData(35.115556, -114.588611, "America/Phoenix")]
    [InlineData(74.667, 112.5, "Asia/Yakutsk")]
    [InlineData(35.109183, -114.598155, "America/Phoenix")]
    [InlineData(35.103325, -114.597998, "America/Phoenix")]
    [InlineData(35.108012, -114.610273, "America/Phoenix")]
    [InlineData(32.698297, -114.581565, "America/Phoenix")]
    [InlineData(35.082686, -114.596978, "America/Phoenix")]
    [InlineData(35.088242, -114.598138, "America/Phoenix")]
    [InlineData(34.503053, -114.350817, "America/Phoenix")]
    [InlineData(32.698399, -114.623759, "America/Phoenix")]
    [InlineData(24.100513, 98.312637, "Asia/Yangon")]
    [InlineData(25.846584, -97.507195, "America/Matamoros")]
    [InlineData(29.328777, -100.966927, "America/Matamoros")]
    [InlineData(-40.824215, -62.982861, "America/Argentina/Salta")]
    [InlineData(19.550138, -71.703903, "America/Santo_Domingo")]
    [InlineData(3.380263, -59.796837, "America/Guyana")]
    [InlineData(-7.333665, -47.462345, "America/Fortaleza")]
    [InlineData(-6.392829, -48.562874, "America/Belem")]
    [InlineData(-11.184197, -38.000431, "America/Maceio")]
    [InlineData(54.43792, 66.071091, "Asia/Qostanay")]
    [InlineData(45.454208, 28.277309, "Europe/Kyiv")]
    [InlineData(48.016441, 23.570456, "Europe/Kyiv")]
    [InlineData(-28.362768, 17.412973, "Africa/Windhoek")]
    [InlineData(-15.616667, 30.416668, "Africa/Maputo")]
    [InlineData(3.166003, -52.334019, "America/Cayenne")]
    [InlineData(3.890881, -51.801823, "America/Cayenne")]
    [InlineData(-6.558501, -47.441559, "America/Fortaleza")]
    [InlineData(-4.455852, -47.529423, "America/Fortaleza")]
    [InlineData(-10.713698, -38.17741, "America/Maceio")]
    [InlineData(-6.344639, -47.397381, "America/Fortaleza")]
    [InlineData(48.448013, 27.796194, "Europe/Kyiv")]
    [InlineData(53.7281, 52.253399, "Europe/Samara")]
    [InlineData(50.543941, 45.568398, "Europe/Volgograd")]
    [InlineData(53.904999, 49.7775, "Europe/Ulyanovsk")]
    [InlineData(63.837921, 145.603546, "Asia/Ust-Nera")]
    [InlineData(50.483898, 47.6278, "Europe/Saratov")]
    public void Matches_Source_Polygon(double latitude, double longitude, string expected)
    {
        var result = TimeZoneLookup.GetTimeZone(latitude, longitude);
        Assert.Equal(expected, result.Result);
        Assert.Empty(result.AlternativeResults);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-90.0001)]
    [InlineData(90.0001)]
    public void Rejects_Invalid_Latitude(double latitude)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TimeZoneLookup.GetTimeZone(latitude, 0));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-180.0001)]
    [InlineData(180.0001)]
    public void Rejects_Invalid_Longitude(double longitude)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TimeZoneLookup.GetTimeZone(0, longitude));
    }

    [Fact]
    public void A_True_Shared_Boundary_Returns_All_Matching_Zones()
    {
        var result = TimeZoneLookup.GetTimeZone(69.8, -141);
        Assert.Equal("America/Anchorage", result.Result);
        Assert.Equal(new[] { "America/Dawson" }, result.AlternativeResults);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)result.AlternativeResults).Add("UTC"));
    }

    [Fact]
    public void Concurrent_Lookups_Do_Not_Share_Decoder_State()
    {
        Parallel.For(0, 1000, i =>
        {
            var swiss = i % 2 == 0;
            var result = swiss ? TimeZoneLookup.GetTimeZone(47.58735, 7.588967) : TimeZoneLookup.GetTimeZone(47.589983, 7.587417);
            Assert.Equal(swiss ? "Europe/Zurich" : "Europe/Paris", result.Result);
            Assert.Empty(result.AlternativeResults);
        });
    }
}
