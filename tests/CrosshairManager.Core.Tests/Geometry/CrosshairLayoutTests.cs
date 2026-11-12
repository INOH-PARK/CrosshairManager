using CrosshairManager.Core.Geometry;
using CrosshairManager.Core.Model;
using static CrosshairManager.Core.Tests.Geometry.GeometryTestSpecs;

namespace CrosshairManager.Core.Tests.Geometry;

public class CrosshairLayoutTests
{
    [Fact]
    public void Default_even_even_layout()
    {
        var g = Layout(CrosshairSpec.Default, false, false);

        Assert.Equal((20, 20), (g.CanvasW, g.CanvasH));
        Assert.Equal((10, 10), (g.AnchorX, g.AnchorY));
        Assert.Equal((10.0, 10.0), (g.PivotX, g.PivotY));
        Assert.Equal(
            new[]
            {
                R(ShapeRole.Dot, 9, 9, 2, 2),
                R(ShapeRole.Top, 9, 1, 2, 6),
                R(ShapeRole.Bottom, 9, 13, 2, 6),
                R(ShapeRole.Left, 1, 9, 6, 2),
                R(ShapeRole.Right, 13, 9, 6, 2),
            },
            g.Fills);
    }

    [Fact]
    public void Default_odd_odd_layout()
    {
        var g = Layout(CrosshairSpec.Default, true, true);

        Assert.Equal((23, 23), (g.CanvasW, g.CanvasH));
        Assert.Equal((11, 11), (g.AnchorX, g.AnchorY));
        Assert.Equal((11.5, 11.5), (g.PivotX, g.PivotY));
        Assert.Equal(
            new[]
            {
                R(ShapeRole.Dot, 11, 11, 2, 2),
                R(ShapeRole.Top, 11, 3, 2, 6),
                R(ShapeRole.Bottom, 11, 15, 2, 6),
                R(ShapeRole.Left, 3, 11, 6, 2),
                R(ShapeRole.Right, 15, 11, 6, 2),
            },
            g.Fills);
    }

    // Dot only, outline off: P = 0 (even) or 0.5 (odd) relative; dot [half(s,P), +s); e = ceil(reach).
    [Theory]
    [InlineData(1, false, 1, 2, 1)] // [0,1) reach 1 -> e 1, canvas 2, rect x 1
    [InlineData(2, false, 1, 2, 0)] // [-1,1) reach 1 -> e 1, canvas 2, rect x 0
    [InlineData(3, false, 2, 4, 1)] // [-1,2) reach 2 -> e 2, canvas 4, rect x 1
    [InlineData(4, false, 2, 4, 0)] // [-2,2) reach 2 -> e 2, canvas 4, rect x 0
    [InlineData(1, true, 1, 3, 1)] // [0,1) reach 0.5 -> e 1, canvas 3, rect x 1
    [InlineData(2, true, 2, 5, 2)] // [0,2) reach 1.5 -> e 2, canvas 5, rect x 2
    [InlineData(3, true, 2, 5, 1)] // [-1,2) reach 1.5 -> e 2, canvas 5, rect x 1
    [InlineData(4, true, 3, 7, 2)] // [-1,3) reach 2.5 -> e 3, canvas 7, rect x 2
    public void Dot_only_sizes_1_to_4_both_parities(int size, bool odd, int anchor, int canvas, int pos)
    {
        var spec = CrosshairSpec.Default with
        {
            Dot = new DotSpec(true, size, 100),
            Top = new LineSpec(Enabled: false),
            Bottom = new LineSpec(Enabled: false),
            Left = new LineSpec(Enabled: false),
            Right = new LineSpec(Enabled: false),
            Outline = new OutlineSpec(Enabled: false),
        };

        var g = Layout(spec, odd, odd);

        Assert.Equal((canvas, canvas), (g.CanvasW, g.CanvasH));
        Assert.Equal((anchor, anchor), (g.AnchorX, g.AnchorY));
        Assert.Equal(anchor + (odd ? 0.5 : 0), g.PivotX);
        Assert.Equal(new[] { R(ShapeRole.Dot, pos, pos, size, size) }, g.Fills);
        Assert.Equal(0, g.OutlineThickness);
    }

    [Fact]
    public void T_style_has_no_top_rect_and_keeps_band()
    {
        var g = Layout(CrosshairSpec.Default with { Top = new LineSpec(Enabled: false) }, false, false);

        Assert.Equal((20, 20), (g.CanvasW, g.CanvasH));
        Assert.Equal(
            new[]
            {
                R(ShapeRole.Dot, 9, 9, 2, 2),
                R(ShapeRole.Bottom, 9, 13, 2, 6),
                R(ShapeRole.Left, 1, 9, 6, 2),
                R(ShapeRole.Right, 13, 9, 6, 2),
            },
            g.Fills);
    }

    [Fact]
    public void Gap_zero_lines_touch_the_center()
    {
        var g = Layout(AllLines(CrosshairSpec.Default, Line(6, 2, 0)), false, false);

        Assert.Equal((14, 14), (g.CanvasW, g.CanvasH));
        Assert.Equal(
            new[]
            {
                R(ShapeRole.Dot, 6, 6, 2, 2),
                R(ShapeRole.Top, 6, 1, 2, 6),
                R(ShapeRole.Bottom, 6, 7, 2, 6),
                R(ShapeRole.Left, 1, 6, 6, 2),
                R(ShapeRole.Right, 7, 6, 6, 2),
            },
            g.Fills);
    }

    [Fact]
    public void Gap_minus_two_lines_cross_the_center()
    {
        var g = Layout(AllLines(CrosshairSpec.Default, Line(6, 2, -2)), false, false);

        Assert.Equal((10, 10), (g.CanvasW, g.CanvasH));
        Assert.Equal(
            new[]
            {
                R(ShapeRole.Dot, 4, 4, 2, 2),
                R(ShapeRole.Top, 4, 1, 2, 6),
                R(ShapeRole.Bottom, 4, 3, 2, 6),
                R(ShapeRole.Left, 1, 4, 6, 2),
                R(ShapeRole.Right, 3, 4, 6, 2),
            },
            g.Fills);
    }

    [Fact]
    public void Scale_200_doubles_sizes_but_not_outline()
    {
        var g = Layout(CrosshairSpec.Default with { ScalePercent = 200 }, false, false);

        Assert.Equal((38, 38), (g.CanvasW, g.CanvasH));
        Assert.Equal(1, g.OutlineThickness);
        Assert.Equal(
            new[]
            {
                R(ShapeRole.Dot, 17, 17, 4, 4),
                R(ShapeRole.Top, 17, 1, 4, 12),
                R(ShapeRole.Bottom, 17, 25, 4, 12),
                R(ShapeRole.Left, 1, 17, 12, 4),
                R(ShapeRole.Right, 25, 17, 12, 4),
            },
            g.Fills);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void All_parts_off_gives_empty_canvas(bool odd)
    {
        var spec = CrosshairSpec.Default with
        {
            Dot = new DotSpec(Enabled: false),
            Top = new LineSpec(Enabled: false),
            Bottom = new LineSpec(Enabled: false),
            Left = new LineSpec(Enabled: false),
            Right = new LineSpec(Enabled: false),
        };

        var g = Layout(spec, odd, odd);

        Assert.Equal((0, 0), (g.CanvasW, g.CanvasH));
        Assert.Empty(g.Fills);
    }

    [Fact]
    public void Zero_length_lines_make_no_rect_and_do_not_set_the_band()
    {
        // Left/Right have length 0 -> no rects; horizontal band falls back to the dot (size 4).
        var spec = CrosshairSpec.Default with
        {
            Dot = new DotSpec(true, 4, 100),
            Left = new LineSpec(true, 0, 2, 3),
            Right = new LineSpec(true, 0, 2, 3),
            Outline = new OutlineSpec(Enabled: false),
        };

        var g = Layout(spec, false, false);

        Assert.DoesNotContain(g.Fills, f => f.Role is ShapeRole.Left or ShapeRole.Right);
        Assert.Equal(new IntRect(-1 + g.AnchorX, -9 + g.AnchorY, 2, 6), Of(g, ShapeRole.Top));
    }

    [Fact]
    public void Odd_thickness_classic1px_even_even_relative_rects()
    {
        var g = Layout(Classic1px, false, false);

        // P = 0; band row/column 0 ([0,1)), center 0.5; topEnd = snap(-1.5) = -1.
        Assert.Equal((6, 6), (g.AnchorX, g.AnchorY));
        Assert.Equal((12, 12), (g.CanvasW, g.CanvasH));
        Assert.Equal(
            new[]
            {
                R(ShapeRole.Top, 6, 1, 1, 4),
                R(ShapeRole.Bottom, 6, 8, 1, 4),
                R(ShapeRole.Left, 1, 6, 4, 1),
                R(ShapeRole.Right, 8, 6, 4, 1),
            },
            g.Fills);
    }

    [Fact]
    public void Alphas_combine_part_and_global_opacity_and_colors_pass_through()
    {
        var spec = CrosshairSpec.Default with
        {
            Opacity = 50,
            Color = Rgb.Parse("#FF8800"),
            Dot = new DotSpec(true, 2, 50),
            Outline = new OutlineSpec(true, 3, Rgb.Parse("#123456"), 100),
            RotationDeg = 90,
        };

        var g = Layout(spec, false, false);

        Assert.Equal((byte)64, g.DotAlpha); // 255 * 0.5 * 0.5 = 63.75
        Assert.Equal((byte)128, g.LineAlpha); // 127.5 rounds up
        Assert.Equal((byte)128, g.OutlineAlpha);
        Assert.Equal(Rgb.Parse("#FF8800"), g.Color);
        Assert.Equal(Rgb.Parse("#123456"), g.OutlineColor);
        Assert.Equal(3, g.OutlineThickness);
        Assert.Equal(90.0, g.RotationDeg);
        Assert.Equal((24, 24), (g.CanvasW, g.CanvasH)); // reach 9 + outline 3, no 45-degree growth
    }

    [Theory]
    [InlineData(45.0, false)]
    [InlineData(135.0, true)]
    [InlineData(315.0, false)]
    public void Rotation_45_canvas_covers_the_diagonal(double rotation, bool odd)
    {
        var flat = Layout(Cross, odd, odd);
        var rotated = Layout(Cross with { RotationDeg = rotation }, odd, odd);

        var min = (int)Math.Ceiling(flat.CanvasW * 1.41);
        Assert.True(rotated.CanvasW >= min, $"{rotated.CanvasW} < {min}");
        Assert.True(rotated.CanvasH >= (int)Math.Ceiling(flat.CanvasH * 1.41));

        // Same rects, just moved by the extra half-width.
        var shift = rotated.AnchorX - flat.AnchorX;
        Assert.Equal(
            flat.Fills.Select(f => f with { Rect = f.Rect with { X = f.Rect.X + shift, Y = f.Rect.Y + shift } }),
            rotated.Fills);
        Assert.Equal(rotated.CanvasW, 2 * rotated.PivotX);
    }
}
