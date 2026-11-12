using CrosshairManager.Core.Geometry;
using CrosshairManager.Core.Model;
using static CrosshairManager.Core.Tests.Geometry.GeometryTestSpecs;

namespace CrosshairManager.Core.Tests.Geometry;

public class GeometryPropertyTests
{
    [Fact]
    public void Classic1px_on_1920x1080_primary_has_exact_absolute_rects()
    {
        var g = Layout(Classic1px, false, false);
        var (window, region) = OverlayPlacement.Compute(g, new IntRect(0, 0, 1920, 1080), 0, 0);

        Assert.Equal(new IntRect(0, 0, g.CanvasW, g.CanvasH), region);
        IntRect Abs(ShapeRole role)
        {
            var r = Of(g, role);
            return r with { X = r.X + window.X, Y = r.Y + window.Y };
        }

        Assert.Equal(new IntRect(960, 535, 1, 4), Abs(ShapeRole.Top));
        Assert.Equal(new IntRect(960, 542, 1, 4), Abs(ShapeRole.Bottom));
        Assert.Equal(new IntRect(955, 540, 4, 1), Abs(ShapeRole.Left));
        Assert.Equal(new IntRect(962, 540, 4, 1), Abs(ShapeRole.Right));
    }

    [Theory]
    [InlineData("classic1px", false)]
    [InlineData("classic1px", true)]
    [InlineData("cross", false)]
    [InlineData("cross", true)]
    public void Builtin_presets_have_equal_gaps_in_all_four_directions(string preset, bool odd)
    {
        var spec = preset == "cross" ? Cross : Classic1px;

        var gaps = Gaps(Layout(spec, odd, odd));

        Assert.Equal(gaps.Top, gaps.Bottom);
        Assert.Equal(gaps.Top, gaps.Left);
        Assert.Equal(gaps.Top, gaps.Right);
        Assert.Equal(preset == "cross" ? 2 : 1, gaps.Top);
    }

    [Fact]
    public void Random_specs_with_equal_axis_thickness_have_equal_gaps()
    {
        var rng = new Random(12345);
        for (var i = 0; i < 200; i++)
        {
            var t = rng.Next(1, 17);
            var gap = rng.Next(-8, 41);
            LineSpec L() => new(true, rng.Next(1, 41), t, gap);
            var spec = CrosshairSpec.Default with
            {
                Dot = new DotSpec(rng.Next(2) == 0, rng.Next(1, 17), 100),
                Top = L(),
                Bottom = L(),
                Left = L(),
                Right = L(),
                Outline = new OutlineSpec(rng.Next(2) == 0, rng.Next(1, 5)),
            };

            foreach (var odd in new[] { false, true })
            {
                var gaps = Gaps(CrosshairLayout.Compute(spec, odd, odd));
                var message = $"#{i} t={t} gap={gap} odd={odd}: {gaps}";
                Assert.True(
                    gaps.Top == gaps.Bottom && gaps.Top == gaps.Left && gaps.Top == gaps.Right,
                    message);
            }
        }
    }

    [Fact]
    public void Even_parity_even_thickness_symmetric_spec_is_mirror_symmetric_about_P()
    {
        var spec = AllLines(CrosshairSpec.Default, Line(7, 4, 5)) with { Dot = new DotSpec(true, 6, 100) };

        var g = Layout(spec, false, false);

        Assert.Equal(g.CanvasW, 2 * g.PivotX);
        Assert.Equal(g.CanvasH, 2 * g.PivotY);
        var mirrorX = (int)(2 * g.PivotX);
        var mirrorY = (int)(2 * g.PivotY);
        var set = g.Fills.ToHashSet();
        foreach (var f in g.Fills)
        {
            var h = new GeomRect(
                f.Role switch { ShapeRole.Left => ShapeRole.Right, ShapeRole.Right => ShapeRole.Left, var r => r },
                f.Rect with { X = mirrorX - f.Rect.X - f.Rect.W });
            var v = new GeomRect(
                f.Role switch { ShapeRole.Top => ShapeRole.Bottom, ShapeRole.Bottom => ShapeRole.Top, var r => r },
                f.Rect with { Y = mirrorY - f.Rect.Y - f.Rect.H });
            Assert.Contains(h, set);
            Assert.Contains(v, set);
        }
    }
}
