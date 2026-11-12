using CrosshairManager.Core.Geometry;
using CrosshairManager.Core.Model;
using CrosshairManager.Core.Render;
using CrosshairManager.TestSupport.Core;
using static CrosshairManager.Core.Tests.Geometry.GeometryTestSpecs;

namespace CrosshairManager.Core.Tests.Render;

public class RasterizerGoldenTests
{
    private static readonly LineSpec Off = new(false, 6, 2, 3);

    private static CrosshairSpec DotOnly(int size) => AllLines(CrosshairSpec.Default, Off) with
    {
        Dot = new DotSpec(true, size),
        Outline = new OutlineSpec(Enabled: false),
    };

    private static CrosshairSpec TStyle => CrosshairSpec.Default with { Top = Off };

    private static readonly Dictionary<string, (CrosshairSpec Spec, bool OddW, bool OddH, IntRect? Region)> Cases = new()
    {
        ["default_even"] = (CrosshairSpec.Default, false, false, null),
        ["default_odd"] = (CrosshairSpec.Default, true, true, null),
        ["dot_only_1"] = (DotOnly(1), false, false, null),
        ["dot_only_2"] = (DotOnly(2), false, false, null),
        ["dot_only_3"] = (DotOnly(3), false, false, null),
        ["classic1px"] = (Classic1px, true, false, null),
        ["classic1px_even_even"] = (Classic1px, false, false, null),
        ["classic1px_odd_odd"] = (Classic1px, true, true, null),
        ["t_style"] = (TStyle, false, false, null),
        ["gap_0"] = (AllLines(Cross, Line(6, 2, 0)), false, false, null),
        ["gap_minus2"] = (AllLines(Cross, Line(6, 2, -2)), false, false, null),
        ["outline_3"] = (CrosshairSpec.Default with { Outline = new OutlineSpec(Thickness: 3) }, false, false, null),
        ["outline_fills_gap"] = (AllLines(CrosshairSpec.Default, Line(2, 2, 2)), false, false, null),
        ["clipped_region"] = (CrosshairSpec.Default, false, false, new IntRect(-3, 6, 12, 6)),
        ["t_style_rot90"] = (TStyle with { RotationDeg = 90 }, false, false, null),
        ["cross_rot45"] = (Cross with { RotationDeg = 45 }, false, false, null),
    };

    public static TheoryData<string> Names => new(Cases.Keys);

    private static string GoldenDir => Path.Combine(AppContext.BaseDirectory, "Goldens");

    [Theory]
    [MemberData(nameof(Names))]
    public void Render_MatchesGolden(string name)
    {
        var (spec, oddW, oddH, region) = Cases[name];
        var g = Layout(spec, oddW, oddH);
        var image = region is { } r ? CrosshairRasterizer.Render(g, r) : CrosshairRasterizer.Render(g);

        var diff = GoldenComparer.Compare(GoldenComparer.LoadGolden(Path.Combine(GoldenDir, name + ".txt")), AsciiArt.From(image));

        Assert.True(diff is null, $"{name}: {diff}");
    }

    [Fact]
    public void EveryGoldenFile_HasACase()
    {
        var files = Directory.GetFiles(GoldenDir, "*.txt").Select(Path.GetFileNameWithoutExtension).Order().ToArray();
        Assert.Equal(Cases.Keys.Order().ToArray(), files);
    }

    [Fact]
    public void GoldenComparer_ReportsFirstDifferingRowAndColumn()
    {
        var golden = GoldenComparer.LoadGolden(Path.Combine(GoldenDir, "default_even.txt"));
        var rows = golden.Split('\n');
        var chars = rows[9].ToCharArray();
        chars[4] = '.';
        rows[9] = new string(chars);

        var diff = GoldenComparer.Compare(golden, string.Join("\n", rows));

        Assert.NotNull(diff);
        Assert.Equal((9, 4, "'X'", "'.'"), (diff.Row, diff.Column, diff.Expected, diff.Actual));
        Assert.Contains("row 9, column 4", diff.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void GoldenComparer_ReportsMissingRowAndShortRow()
    {
        var missing = GoldenComparer.Compare("..\n.X\n..", "..\n.X");
        Assert.Equal((2, 0, "'.'", "<no row>"), (missing!.Row, missing.Column, missing.Expected, missing.Actual));

        var shortRow = GoldenComparer.Compare("...\n.X.", "...\n.X");
        Assert.Equal((1, 2, "'.'", "<end of row>"), (shortRow!.Row, shortRow.Column, shortRow.Expected, shortRow.Actual));
    }

    [Fact]
    public void GoldenComparer_IgnoresLineEndingsAndTrailingNewline()
    {
        Assert.Null(GoldenComparer.Compare("..\r\n.X\r\n", "..\n.X"));
    }

    [Fact]
    public void AsciiArt_MapsFillOutlineEmptyAndPartial()
    {
        var b = new PixelBuffer(4, 1);
        b.SetPixel(0, 0, AsciiArt.DefaultFill);
        b.SetPixel(1, 0, AsciiArt.DefaultOutline);
        b.SetPixel(3, 0, new PixelBuffer.Pixel(0, 64, 0, 64));

        Assert.Equal("Xo.+", AsciiArt.From(b));
    }
}
