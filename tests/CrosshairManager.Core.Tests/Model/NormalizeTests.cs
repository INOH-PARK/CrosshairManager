using System.Globalization;
using CrosshairManager.Core.Model;

namespace CrosshairManager.Core.Tests.Model;

public class NormalizeTests
{
    [Fact]
    public void Default_is_unchanged_and_has_no_warnings()
    {
        var (spec, warnings) = CrosshairSpec.Default.Normalize();

        Assert.Equal(CrosshairSpec.Default, spec);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Default_matches_the_model_table()
    {
        var d = CrosshairSpec.Default;

        Assert.Equal(Rgb.Parse("#00FF00"), d.Color);
        Assert.Equal(100, d.Opacity);
        Assert.Equal(100, d.ScalePercent);
        Assert.Equal(0, d.RotationDeg);
        Assert.Equal(0, d.OffsetX);
        Assert.Equal(0, d.OffsetY);
        Assert.Equal(new DotSpec(true, 2, 100), d.Dot);
        Assert.Equal(new LineSpec(true, 6, 2, 3), d.Top);
        Assert.Equal(new LineSpec(true, 6, 2, 3), d.Bottom);
        Assert.Equal(new LineSpec(true, 6, 2, 3), d.Left);
        Assert.Equal(new LineSpec(true, 6, 2, 3), d.Right);
        Assert.Equal(new OutlineSpec(true, 1, Rgb.Parse("#000000"), 100), d.Outline);
    }

    [Theory]
    [InlineData("Opacity", 0, 100)]
    [InlineData("ScalePercent", 25, 400)]
    [InlineData("OffsetX", -4096, 4096)]
    [InlineData("OffsetY", -4096, 4096)]
    [InlineData("Dot.Size", 1, 64)]
    [InlineData("Dot.Opacity", 0, 100)]
    [InlineData("Top.Length", 0, 512)]
    [InlineData("Top.Thickness", 1, 64)]
    [InlineData("Top.Gap", -32, 256)]
    [InlineData("Bottom.Length", 0, 512)]
    [InlineData("Bottom.Thickness", 1, 64)]
    [InlineData("Bottom.Gap", -32, 256)]
    [InlineData("Left.Length", 0, 512)]
    [InlineData("Left.Thickness", 1, 64)]
    [InlineData("Left.Gap", -32, 256)]
    [InlineData("Right.Length", 0, 512)]
    [InlineData("Right.Thickness", 1, 64)]
    [InlineData("Right.Gap", -32, 256)]
    [InlineData("Outline.Thickness", 1, 16)]
    [InlineData("Outline.Opacity", 0, 100)]
    public void Each_numeric_field_clamps_just_outside_both_bounds(string path, int min, int max)
    {
        var low = WithField(CrosshairSpec.Default, path, min - 1).Normalize();
        Assert.Equal(WithField(CrosshairSpec.Default, path, min), low.Spec);
        Assert.Equal(new[] { $"{path}: {min - 1} clamped to {min}" }, low.Warnings.ToArray());

        var high = WithField(CrosshairSpec.Default, path, max + 1).Normalize();
        Assert.Equal(WithField(CrosshairSpec.Default, path, max), high.Spec);
        Assert.Equal(new[] { $"{path}: {max + 1} clamped to {max}" }, high.Warnings.ToArray());
    }

    [Theory]
    [InlineData("Opacity", 0, 100)]
    [InlineData("ScalePercent", 25, 400)]
    [InlineData("OffsetX", -4096, 4096)]
    [InlineData("OffsetY", -4096, 4096)]
    [InlineData("Dot.Size", 1, 64)]
    [InlineData("Dot.Opacity", 0, 100)]
    [InlineData("Top.Length", 0, 512)]
    [InlineData("Top.Thickness", 1, 64)]
    [InlineData("Top.Gap", -32, 256)]
    [InlineData("Bottom.Length", 0, 512)]
    [InlineData("Bottom.Thickness", 1, 64)]
    [InlineData("Bottom.Gap", -32, 256)]
    [InlineData("Left.Length", 0, 512)]
    [InlineData("Left.Thickness", 1, 64)]
    [InlineData("Left.Gap", -32, 256)]
    [InlineData("Right.Length", 0, 512)]
    [InlineData("Right.Thickness", 1, 64)]
    [InlineData("Right.Gap", -32, 256)]
    [InlineData("Outline.Thickness", 1, 16)]
    [InlineData("Outline.Opacity", 0, 100)]
    public void Each_numeric_field_keeps_both_bounds_without_warning(string path, int min, int max)
    {
        foreach (var value in new[] { min, max })
        {
            var (spec, warnings) = WithField(CrosshairSpec.Default, path, value).Normalize();
            Assert.Equal(WithField(CrosshairSpec.Default, path, value), spec);
            Assert.Empty(warnings);
        }
    }

    [Theory]
    [InlineData(30, 45)]
    [InlineData(22, 0)]
    [InlineData(22.5, 45)]
    [InlineData(350, 0)]
    [InlineData(-45, 315)]
    [InlineData(-22.5, 0)]
    public void Rotation_snaps_to_nearest_45_degrees_with_ties_going_up(double input, double expected)
    {
        var (spec, warnings) = (CrosshairSpec.Default with { RotationDeg = input }).Normalize();

        Assert.Equal(expected, spec.RotationDeg);
        Assert.Equal(
            new[]
            {
                $"RotationDeg: {Format(input)} clamped to {Format(expected)}",
            },
            warnings.ToArray());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(45)]
    [InlineData(90)]
    [InlineData(135)]
    [InlineData(180)]
    [InlineData(225)]
    [InlineData(270)]
    [InlineData(315)]
    public void Rotation_values_on_the_grid_are_kept_without_warning(double angle)
    {
        var (spec, warnings) = (CrosshairSpec.Default with { RotationDeg = angle }).Normalize();

        Assert.Equal(angle, spec.RotationDeg);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Non_finite_rotation_falls_back_to_zero_with_warning()
    {
        var (spec, warnings) = (CrosshairSpec.Default with { RotationDeg = double.NaN }).Normalize();

        Assert.Equal(0, spec.RotationDeg);
        Assert.Equal(new[] { "RotationDeg: NaN clamped to 0" }, warnings.ToArray());
    }

    [Fact]
    public void Normalize_is_idempotent_for_200_seeded_random_specs()
    {
        var random = new Random(12345);

        for (var i = 0; i < 200; i++)
        {
            var once = RandomSpec(random).Normalize();
            var twice = once.Spec.Normalize();

            Assert.Equal(once.Spec, twice.Spec);
            Assert.Empty(twice.Warnings);
        }
    }

    private static string Format(double value) => value.ToString(CultureInfo.InvariantCulture);

    private static CrosshairSpec RandomSpec(Random r)
    {
        int I(int min, int max) => r.Next(min, max + 1);
        LineSpec L() => new(r.Next(2) == 0, I(-5, 600), I(-5, 80), I(-40, 300));
        Rgb C() => new((byte)I(0, 255), (byte)I(0, 255), (byte)I(0, 255));

        return new CrosshairSpec
        {
            Color = C(),
            Opacity = I(-20, 120),
            ScalePercent = I(-50, 500),
            RotationDeg = r.NextDouble() * 900 - 450,
            OffsetX = I(-5000, 5000),
            OffsetY = I(-5000, 5000),
            Dot = new DotSpec(r.Next(2) == 0, I(-5, 80), I(-5, 120)),
            Top = L(),
            Bottom = L(),
            Left = L(),
            Right = L(),
            Outline = new OutlineSpec(r.Next(2) == 0, I(-3, 20), C(), I(-5, 120)),
        };
    }

    private static CrosshairSpec WithField(CrosshairSpec s, string path, int value) => path switch
    {
        "Opacity" => s with { Opacity = value },
        "ScalePercent" => s with { ScalePercent = value },
        "OffsetX" => s with { OffsetX = value },
        "OffsetY" => s with { OffsetY = value },
        "Dot.Size" => s with { Dot = s.Dot with { Size = value } },
        "Dot.Opacity" => s with { Dot = s.Dot with { Opacity = value } },
        "Top.Length" => s with { Top = s.Top with { Length = value } },
        "Top.Thickness" => s with { Top = s.Top with { Thickness = value } },
        "Top.Gap" => s with { Top = s.Top with { Gap = value } },
        "Bottom.Length" => s with { Bottom = s.Bottom with { Length = value } },
        "Bottom.Thickness" => s with { Bottom = s.Bottom with { Thickness = value } },
        "Bottom.Gap" => s with { Bottom = s.Bottom with { Gap = value } },
        "Left.Length" => s with { Left = s.Left with { Length = value } },
        "Left.Thickness" => s with { Left = s.Left with { Thickness = value } },
        "Left.Gap" => s with { Left = s.Left with { Gap = value } },
        "Right.Length" => s with { Right = s.Right with { Length = value } },
        "Right.Thickness" => s with { Right = s.Right with { Thickness = value } },
        "Right.Gap" => s with { Right = s.Right with { Gap = value } },
        "Outline.Thickness" => s with { Outline = s.Outline with { Thickness = value } },
        "Outline.Opacity" => s with { Outline = s.Outline with { Opacity = value } },
        _ => throw new ArgumentException($"Unknown field: {path}", nameof(path)),
    };
}
