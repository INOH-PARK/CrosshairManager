using CrosshairManager.Core.Model;

namespace CrosshairManager.Core.Tests.Model;

public class ApplyScaleTests
{
    [Fact]
    public void Scale_200_doubles_size_length_thickness_and_gap()
    {
        var scaled = (CrosshairSpec.Default with { ScalePercent = 200 }).ApplyScale();

        Assert.Equal(4, scaled.Dot.Size);
        Assert.Equal(new LineSpec(true, 12, 4, 6), scaled.Top);
        Assert.Equal(new LineSpec(true, 12, 4, 6), scaled.Bottom);
        Assert.Equal(new LineSpec(true, 12, 4, 6), scaled.Left);
        Assert.Equal(new LineSpec(true, 12, 4, 6), scaled.Right);
        Assert.Equal(100, scaled.Opacity);
        Assert.Equal(new OutlineSpec(true, 1, Rgb.Parse("#000000"), 100), scaled.Outline);
    }

    [Fact]
    public void Scale_100_leaves_the_spec_unchanged()
    {
        Assert.Equal(CrosshairSpec.Default, CrosshairSpec.Default.ApplyScale());
    }

    [Fact]
    public void Length_512_at_200_percent_is_1024_and_not_clamped_again()
    {
        var spec = CrosshairSpec.Default with
        {
            ScalePercent = 200,
            Top = new LineSpec(Length: 512),
        };

        Assert.Equal(1024, spec.ApplyScale().Top.Length);
    }

    [Fact]
    public void Thickness_1_at_25_percent_stays_1()
    {
        var spec = CrosshairSpec.Default with
        {
            ScalePercent = 25,
            Top = new LineSpec(Thickness: 1),
        };

        Assert.Equal(1, spec.ApplyScale().Top.Thickness);
    }

    [Theory]
    [InlineData(50, 5, 3)]
    [InlineData(50, 4, 2)]
    [InlineData(25, 6, 2)]
    public void Scaled_values_round_half_away_from_zero(int percent, int length, int expected)
    {
        var spec = CrosshairSpec.Default with
        {
            ScalePercent = percent,
            Top = new LineSpec(Length: length),
        };

        Assert.Equal(expected, spec.ApplyScale().Top.Length);
    }

    [Fact]
    public void Negative_gap_rounds_half_away_from_zero()
    {
        var spec = CrosshairSpec.Default with
        {
            ScalePercent = 50,
            Left = new LineSpec(Gap: -3),
        };

        Assert.Equal(-2, spec.ApplyScale().Left.Gap);
    }
}
