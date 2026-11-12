using CrosshairManager.Core.Geometry;
using CrosshairManager.Core.Model;
using static CrosshairManager.Core.Tests.Geometry.GeometryTestSpecs;

namespace CrosshairManager.Core.Tests.Geometry;

public class OverlayPlacementTests
{
    private static readonly IntRect Secondary = new(1920, 0, 1920, 1080);

    [Fact]
    public void Centers_P_on_secondary_monitor()
    {
        var g = Layout(CrosshairSpec.Default, false, false);

        var (window, region) = OverlayPlacement.Compute(g, Secondary, 0, 0);

        Assert.Equal(new IntRect(2870, 530, 20, 20), window);
        Assert.Equal(new IntRect(0, 0, 20, 20), region);
        Assert.Equal((2880.0, 540.0), (window.X + g.PivotX, window.Y + g.PivotY));
    }

    [Fact]
    public void Odd_monitor_puts_P_on_the_half_pixel()
    {
        var g = Layout(CrosshairSpec.Default, true, true);

        var (window, _) = OverlayPlacement.Compute(g, new IntRect(-1601, 10, 1601, 901), 3, -2);

        Assert.Equal((-1601 + 800.5 + 3, 10 + 450.5 - 2), (window.X + g.PivotX, window.Y + g.PivotY));
    }

    [Fact]
    public void Offset_pushing_half_the_canvas_out_clips_the_region()
    {
        var g = Layout(CrosshairSpec.Default, false, false);

        var (window, region) = OverlayPlacement.Compute(g, Secondary, -960, 0);

        Assert.Equal(new IntRect(1920, 530, 10, 20), window);
        Assert.Equal(new IntRect(10, 0, 10, 20), region);
    }

    [Fact]
    public void Clipping_on_bottom_right_edges()
    {
        var g = Layout(CrosshairSpec.Default, false, false);

        var (window, region) = OverlayPlacement.Compute(g, Secondary, 955, 535);

        Assert.Equal(new IntRect(3825, 1065, 15, 15), window);
        Assert.Equal(new IntRect(0, 0, 15, 15), region);
    }

    [Fact]
    public void Canvas_entirely_left_of_monitor_gives_empty_window_and_region()
    {
        var g = Layout(CrosshairSpec.Default, false, false);

        var (window, region) = OverlayPlacement.Compute(g, Secondary, -970, 0);

        Assert.True(window.IsEmpty);
        Assert.True(region.IsEmpty);
        Assert.Equal(default, window);
        Assert.Equal(default, region);
    }

    [Fact]
    public void Empty_canvas_gives_empty_window()
    {
        var spec = CrosshairSpec.Default with
        {
            Dot = new DotSpec(Enabled: false),
            Top = new LineSpec(Enabled: false),
            Bottom = new LineSpec(Enabled: false),
            Left = new LineSpec(Enabled: false),
            Right = new LineSpec(Enabled: false),
        };

        var (window, region) = OverlayPlacement.Compute(Layout(spec, false, false), Secondary, 0, 0);

        Assert.Equal(default, window);
        Assert.Equal(default, region);
    }

    [Fact]
    public void Geometry_computed_for_other_parity_is_rejected()
    {
        var g = Layout(CrosshairSpec.Default, false, false);

        Assert.Throws<ArgumentException>(() => OverlayPlacement.Compute(g, new IntRect(0, 0, 1921, 1080), 0, 0));
        Assert.Throws<ArgumentException>(() => OverlayPlacement.Compute(g, new IntRect(0, 0, 1920, 1081), 0, 0));
    }
}
