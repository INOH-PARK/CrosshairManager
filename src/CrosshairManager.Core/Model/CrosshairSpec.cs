using System.Globalization;

namespace CrosshairManager.Core.Model;

public sealed record CrosshairSpec
{
    public static CrosshairSpec Default { get; } = new();

    public Rgb Color { get; init; } = Rgb.Parse("#00FF00");
    public int Opacity { get; init; } = 100;
    public int ScalePercent { get; init; } = 100;
    public double RotationDeg { get; init; }
    public int OffsetX { get; init; }
    public int OffsetY { get; init; }
    public DotSpec Dot { get; init; } = new();
    public LineSpec Top { get; init; } = new();
    public LineSpec Bottom { get; init; } = new();
    public LineSpec Left { get; init; } = new();
    public LineSpec Right { get; init; } = new();
    public OutlineSpec Outline { get; init; } = new();

    public (CrosshairSpec Spec, IReadOnlyList<string> Warnings) Normalize()
    {
        var warnings = new List<string>();

        int Clamp(string path, int value, int min, int max)
        {
            var clamped = Math.Clamp(value, min, max);
            if (clamped != value)
            {
                warnings.Add($"{path}: {value} clamped to {clamped}");
            }

            return clamped;
        }

        LineSpec Line(string name, LineSpec line) => line with
        {
            Length = Clamp($"{name}.Length", line.Length, 0, 512),
            Thickness = Clamp($"{name}.Thickness", line.Thickness, 1, 64),
            Gap = Clamp($"{name}.Gap", line.Gap, -32, 256),
        };

        var spec = this with
        {
            Opacity = Clamp("Opacity", Opacity, 0, 100),
            ScalePercent = Clamp("ScalePercent", ScalePercent, 25, 400),
            RotationDeg = NormalizeRotation(RotationDeg, warnings),
            OffsetX = Clamp("OffsetX", OffsetX, -4096, 4096),
            OffsetY = Clamp("OffsetY", OffsetY, -4096, 4096),
            Dot = Dot with
            {
                Size = Clamp("Dot.Size", Dot.Size, 1, 64),
                Opacity = Clamp("Dot.Opacity", Dot.Opacity, 0, 100),
            },
            Top = Line("Top", Top),
            Bottom = Line("Bottom", Bottom),
            Left = Line("Left", Left),
            Right = Line("Right", Right),
            Outline = Outline with
            {
                Thickness = Clamp("Outline.Thickness", Outline.Thickness, 1, 16),
                Opacity = Clamp("Outline.Opacity", Outline.Opacity, 0, 100),
            },
        };

        return (spec, warnings);
    }

    public CrosshairSpec ApplyScale()
    {
        int S(int value) => Scale(value, ScalePercent);

        LineSpec Line(LineSpec line) => new(
            line.Enabled,
            S(line.Length),
            Math.Max(1, S(line.Thickness)),
            S(line.Gap));

        return this with
        {
            Dot = Dot with { Size = Math.Max(1, S(Dot.Size)) },
            Top = Line(Top),
            Bottom = Line(Bottom),
            Left = Line(Left),
            Right = Line(Right),
        };
    }

    private static double NormalizeRotation(double value, List<string> warnings)
    {
        if (!double.IsFinite(value))
        {
            warnings.Add($"RotationDeg: {value.ToString(CultureInfo.InvariantCulture)} clamped to 0");
            return 0;
        }

        var snapped = Math.Floor(value / 45 + 0.5) * 45;
        var result = ((snapped % 360) + 360) % 360;
        if (result != value)
        {
            warnings.Add(
                $"RotationDeg: {value.ToString(CultureInfo.InvariantCulture)} clamped to {result.ToString(CultureInfo.InvariantCulture)}");
        }

        return result;
    }

    private static int Scale(int value, int percent)
    {
        var product = (long)value * percent;
        return (int)(product >= 0 ? (product + 50) / 100 : -((-product + 50) / 100));
    }
}
