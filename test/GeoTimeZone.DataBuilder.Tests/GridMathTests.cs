using GeoTimeZone.Internal;

namespace GeoTimeZone.DataBuilder.Tests;

public class GridMathTests
{
    [Theory]
    [InlineData(-180, SpatialFormat.Columns)]
    [InlineData(-90, SpatialFormat.Rows)]
    public void Every_Root_And_Child_Seam_Owns_Its_Exact_And_Adjacent_Doubles(double worldOrigin, int cells)
    {
        for (var i = 1; i < cells; i++)
        {
            var seam = worldOrigin + i * SpatialFormat.CellWidth;
            Assert.Equal(i - 1, SpatialFormat.Cell(TestDoubles.Decrement(seam), worldOrigin, SpatialFormat.CellWidth, cells));
            Assert.Equal(i, SpatialFormat.Cell(seam, worldOrigin, SpatialFormat.CellWidth, cells));
            Assert.Equal(i, SpatialFormat.Cell(TestDoubles.Increment(seam), worldOrigin, SpatialFormat.CellWidth, cells));
        }

        for (var root = 0; root < cells; root++)
        for (var child = 1; child < SpatialFormat.Refinement; child++)
        {
            var origin = worldOrigin + root * SpatialFormat.CellWidth;
            var seam = origin + child * SpatialFormat.ChildWidth;
            Assert.Equal(child - 1, SpatialFormat.Cell(TestDoubles.Decrement(seam), origin, SpatialFormat.ChildWidth, SpatialFormat.Refinement));
            Assert.Equal(child, SpatialFormat.Cell(seam, origin, SpatialFormat.ChildWidth, SpatialFormat.Refinement));
            Assert.Equal(child, SpatialFormat.Cell(TestDoubles.Increment(seam), origin, SpatialFormat.ChildWidth, SpatialFormat.Refinement));
        }
    }

    [Fact]
    public void Does_Not_Lose_Subnormal_Offsets_When_Translating_To_World_Coordinates()
    {
        Assert.Equal(239, SpatialFormat.Cell(-double.Epsilon, -180, SpatialFormat.CellWidth, SpatialFormat.Columns));
        Assert.Equal(240, SpatialFormat.Cell(0, -180, SpatialFormat.CellWidth, SpatialFormat.Columns));
        Assert.Equal(240, SpatialFormat.Cell(double.Epsilon, -180, SpatialFormat.CellWidth, SpatialFormat.Columns));
    }

    [Fact]
    public void World_Endpoints_Belong_To_First_And_Last_Cells()
    {
        Assert.Equal(0, SpatialFormat.Cell(-180, -180, SpatialFormat.CellWidth, SpatialFormat.Columns));
        Assert.Equal(479, SpatialFormat.Cell(180, -180, SpatialFormat.CellWidth, SpatialFormat.Columns));
        Assert.Equal(0, SpatialFormat.Cell(-90, -90, SpatialFormat.CellWidth, SpatialFormat.Rows));
        Assert.Equal(239, SpatialFormat.Cell(90, -90, SpatialFormat.CellWidth, SpatialFormat.Rows));
    }
}
