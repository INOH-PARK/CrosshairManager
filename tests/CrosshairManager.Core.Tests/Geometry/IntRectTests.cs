using CrosshairManager.Core.Geometry;

namespace CrosshairManager.Core.Tests.Geometry;

public class IntRectTests
{
    [Theory]
    [InlineData(0, 5)]
    [InlineData(5, 0)]
    [InlineData(-1, 3)]
    public void IsEmpty_is_true_when_width_or_height_is_not_positive(int w, int h)
    {
        Assert.True(new IntRect(0, 0, w, h).IsEmpty);
    }

    [Fact]
    public void IsEmpty_is_false_for_positive_area()
    {
        Assert.False(new IntRect(-3, 7, 1, 1).IsEmpty);
    }
}
