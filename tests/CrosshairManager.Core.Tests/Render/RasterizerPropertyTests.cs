using CrosshairManager.Core.Geometry;
using CrosshairManager.Core.Model;
using CrosshairManager.Core.Render;
using static CrosshairManager.Core.Tests.Geometry.GeometryTestSpecs;

namespace CrosshairManager.Core.Tests.Render;

public class RasterizerPropertyTests
{
    private static readonly double[] Rotations = { 0, 45, 90, 135, 180, 225, 270, 315 };
    private static readonly (bool OddW, bool OddH)[] Parities = { (false, false), (true, true), (true, false), (false, true) };

    private static IEnumerable<CrosshairSpec> Variety()
    {
        yield return CrosshairSpec.Default;
        yield return Cross;
        yield return Classic1px;
        yield return CrosshairSpec.Default with { Top = new LineSpec(false, 6, 2, 3) };
        yield return AllLines(CrosshairSpec.Default, Line(12, 3, -4)) with { Outline = new OutlineSpec(Thickness: 4) };
        yield return CrosshairSpec.Default with
        {
            Color = Rgb.Parse("#FF8800"),
            Opacity = 37,
            Dot = new DotSpec(true, 5, 60),
            Outline = new OutlineSpec(true, 2, Rgb.Parse("#123456"), 80),
        };
        yield return AllLines(CrosshairSpec.Default, new LineSpec(false, 6, 2, 3)) with { Dot = new DotSpec(true, 7) };
    }

    private static PixelBuffer Render(CrosshairSpec spec, bool oddW, bool oddH) =>
        CrosshairRasterizer.Render(Layout(spec, oddW, oddH));

    [Theory]
    [InlineData(6, 2, 3, 2, 1)]
    [InlineData(10, 4, 5, 4, 3)]
    [InlineData(4, 2, 0, 2, 2)]
    [InlineData(8, 6, -2, 0, 1)]
    public void EvenThicknessSymmetricSpec_EvenParity_IsMirrorSymmetric(int length, int thickness, int gap, int dot, int outline)
    {
        var spec = AllLines(CrosshairSpec.Default, Line(length, thickness, gap)) with
        {
            Dot = new DotSpec(dot > 0, Math.Max(dot, 1)),
            Outline = new OutlineSpec(Thickness: outline),
        };
        var b = Render(spec, false, false);

        for (int y = 0; y < b.Height; y++)
            for (int x = 0; x < b.Width; x++)
            {
                Assert.Equal(b.GetPixel(x, y), b.GetPixel(b.Width - 1 - x, y));
                Assert.Equal(b.GetPixel(x, y), b.GetPixel(x, b.Height - 1 - y));
            }
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(1, 1)]
    [InlineData(3, 2)]
    [InlineData(5, 4)]
    [InlineData(2, 3)]
    public void SameThicknessPerAxis_FourGapsAreEqual_ForEveryParity(int thickness, int gap)
    {
        var spec = AllLines(Classic1px, Line(4, thickness, gap));
        foreach (var (oddW, oddH) in Parities)
        {
            var b = Render(spec, oddW, oddH);
            var gaps = PixelGaps(b);
            Assert.True(gaps.Top == gaps.Bottom && gaps.Bottom == gaps.Left && gaps.Left == gaps.Right,
                $"T{thickness} G{gap} odd=({oddW},{oddH}): gaps {gaps}");
        }
    }

    [Theory]
    [InlineData("default", false)]
    [InlineData("cross", false)]
    [InlineData("classic1px", true)]
    [InlineData("odd3", true)]
    public void Rotation90_OfSymmetricSpec_EqualsTransposeOfRotation0(string name, bool odd)
    {
        var spec = name switch
        {
            "default" => CrosshairSpec.Default,
            "cross" => Cross,
            "classic1px" => Classic1px,
            _ => AllLines(CrosshairSpec.Default, Line(5, 3, 2)) with { Dot = new DotSpec(true, 3) },
        };
        var r0 = Render(spec, odd, odd);
        var r90 = Render(spec with { RotationDeg = 90 }, odd, odd);

        Assert.Equal((r0.Width, r0.Height), (r90.Height, r90.Width));
        for (int y = 0; y < r90.Height; y++)
            for (int x = 0; x < r90.Width; x++)
                Assert.Equal(r0.GetPixel(y, x), r90.GetPixel(x, y));
    }

    [Fact]
    public void Alpha_NeverExceedsGlobalOpacity_AndColorsArePremultiplied()
    {
        foreach (var opacity in new[] { 0, 1, 37, 64, 100 })
            foreach (var rot in Rotations)
                foreach (var spec in Variety())
                {
                    var b = Render(spec with { Opacity = opacity, RotationDeg = rot }, true, false);
                    var cap = (int)Math.Round(255 * opacity / 100.0, MidpointRounding.AwayFromZero);
                    for (int y = 0; y < b.Height; y++)
                        for (int x = 0; x < b.Width; x++)
                        {
                            var p = b.GetPixel(x, y);
                            Assert.True(p.A <= cap, $"opacity {opacity} rot {rot}: alpha {p.A} > {cap} at ({x},{y})");
                            Assert.True(p.R <= p.A && p.G <= p.A && p.B <= p.A, $"not premultiplied at ({x},{y}): {p}");
                        }
                }
    }

    [Fact]
    public void NonTransparentPixels_AreAllInsideTheCanvas()
    {
        foreach (var rot in Rotations)
            foreach (var (oddW, oddH) in Parities)
                foreach (var spec in Variety())
                {
                    var g = Layout(spec with { RotationDeg = rot }, oddW, oddH);
                    const int m = 8;
                    var b = CrosshairRasterizer.Render(g, new IntRect(-m, -m, g.CanvasW + (2 * m), g.CanvasH + (2 * m)));
                    var inside = 0;
                    for (int y = 0; y < b.Height; y++)
                        for (int x = 0; x < b.Width; x++)
                        {
                            bool inCanvas = x >= m && x < m + g.CanvasW && y >= m && y < m + g.CanvasH;
                            var p = b.GetPixel(x, y);
                            if (p.A != 0 && inCanvas) inside++;
                            Assert.True(inCanvas || p == default, $"rot {rot} odd=({oddW},{oddH}): pixel outside canvas at ({x - m},{y - m})");
                        }

                    Assert.True(inside > 0, $"rot {rot}: nothing rendered");
                }
    }

    [Fact]
    public void Rotation45_IsByteIdenticalAcrossRuns()
    {
        foreach (var rot in new double[] { 45, 135, 225, 315 })
            foreach (var spec in Variety())
            {
                var a = Render(spec with { RotationDeg = rot }, false, true);
                var b = Render(spec with { RotationDeg = rot }, false, true);
                Assert.True(a.Bytes.SequenceEqual(b.Bytes), $"rot {rot} differs between runs");
            }
    }

    [Fact]
    public void MultiplesOf90_UseOnlyExactPartAlphas_While45HasPartialPixels()
    {
        var spec = CrosshairSpec.Default with { Opacity = 50, Dot = new DotSpec(true, 3, 70), Outline = new OutlineSpec(true, 2, default, 40) };
        foreach (var rot in Rotations)
        {
            var g = Layout(spec with { RotationDeg = rot }, false, false);
            var allowed = new HashSet<byte> { 0, g.DotAlpha, g.LineAlpha, g.OutlineAlpha };
            var b = CrosshairRasterizer.Render(g);
            var alphas = Enumerable.Range(0, b.Width * b.Height).Select(i => b.GetPixel(i % b.Width, i / b.Width).A).ToHashSet();
            if (rot % 90 == 0)
                Assert.Subset(allowed, alphas);
            else
                Assert.NotEmpty(alphas.Except(allowed));
        }
    }

    [Fact]
    public void LinePixel_UsesPremultipliedFormula()
    {
        var spec = Cross with { Color = Rgb.Parse("#FF8800"), Opacity = 50, Outline = new OutlineSpec(true, 1, Rgb.Parse("#FFFFFF"), 50) };
        var g = Layout(spec, false, false);
        var b = CrosshairRasterizer.Render(g);
        var top = Of(g, ShapeRole.Top);

        // alpha = round(255*0.5) = 128; R = round(255*128/255) = 128; G = round(136*128/255) = 68.
        Assert.Equal(new PixelBuffer.Pixel(0, 68, 128, 128), b.GetPixel(top.X, top.Y));
        // outline alpha = round(255*0.5*0.5) = 64; white premultiplied = 64.
        Assert.Equal(new PixelBuffer.Pixel(64, 64, 64, 64), b.GetPixel(top.X - 1, top.Y));
    }

    [Fact]
    public void RegionRender_IsACropOfTheFullRender()
    {
        var g = Layout(Cross with { RotationDeg = 45 }, true, false);
        var full = CrosshairRasterizer.Render(g);
        Assert.Equal(full, CrosshairRasterizer.Render(g, new IntRect(0, 0, g.CanvasW, g.CanvasH)));

        var region = new IntRect(3, 5, 9, 7);
        var crop = CrosshairRasterizer.Render(g, region);
        for (int y = 0; y < region.H; y++)
            for (int x = 0; x < region.W; x++)
                Assert.Equal(full.GetPixel(region.X + x, region.Y + y), crop.GetPixel(x, y));
    }

    [Fact]
    public void InvalidInputs_Throw()
    {
        var g = Layout(CrosshairSpec.Default, false, false);
        Assert.Throws<ArgumentException>(() => CrosshairRasterizer.Render(g with { CanvasW = 0, CanvasH = 0 }));
        Assert.Throws<ArgumentException>(() => CrosshairRasterizer.Render(g, new IntRect(0, 0, 0, 5)));
        Assert.Throws<ArgumentException>(() => CrosshairRasterizer.Render(g with { RotationDeg = 30 }));
        Assert.Throws<ArgumentNullException>(() => CrosshairRasterizer.Render(null!));
    }

    /// <summary>Empty pixels between the center band and each arm, measured on the image (dot off, gap &gt; 0).</summary>
    private static (int Top, int Bottom, int Left, int Right) PixelGaps(PixelBuffer b)
    {
        bool F(int x, int y) => b.GetPixel(x, y).A != 0;
        int minX = int.MaxValue, minY = int.MaxValue;
        for (int y = 0; y < b.Height; y++)
            for (int x = 0; x < b.Width; x++)
                if (F(x, y)) { minX = Math.Min(minX, x); minY = Math.Min(minY, y); }

        var bandRows = Enumerable.Range(0, b.Height).Where(y => F(minX, y)).ToArray();
        var bandCols = Enumerable.Range(0, b.Width).Where(x => F(x, minY)).ToArray();
        int row = bandRows[0], col = bandCols[0];

        int leftEnd = minX; while (F(leftEnd, row)) leftEnd++;
        int rightStart = bandCols[^1] + 1; while (!F(rightStart, row)) rightStart++;
        int topEnd = minY; while (F(col, topEnd)) topEnd++;
        int bottomStart = bandRows[^1] + 1; while (!F(col, bottomStart)) bottomStart++;

        return (bandRows[0] - topEnd, bottomStart - bandRows[^1] - 1, bandCols[0] - leftEnd, rightStart - bandCols[^1] - 1);
    }
}
