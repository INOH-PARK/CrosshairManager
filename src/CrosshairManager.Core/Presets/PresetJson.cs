using System.Text.Json.Serialization;
using CrosshairManager.Core.Model;

namespace CrosshairManager.Core.Presets;

/// <summary>On-disk shape of <c>presets/{id}.json</c>; every field is optional so missing values take defaults.</summary>
internal sealed class PresetDocument
{
    public int SchemaVersion { get; set; }
    public string? Id { get; set; }
    public string? Name { get; set; }
    public DateTime? CreatedUtc { get; set; }
    public DateTime? ModifiedUtc { get; set; }
    public CrosshairDocument? Crosshair { get; set; }
}

internal sealed class CrosshairDocument
{
    public string? Color { get; set; }
    public int? Opacity { get; set; }
    public int? ScalePercent { get; set; }
    public double? RotationDeg { get; set; }
    public int? OffsetX { get; set; }
    public int? OffsetY { get; set; }
    public DotDocument? Dot { get; set; }
    public LineDocument? Top { get; set; }
    public LineDocument? Bottom { get; set; }
    public LineDocument? Left { get; set; }
    public LineDocument? Right { get; set; }
    public OutlineDocument? Outline { get; set; }

    public static CrosshairDocument From(CrosshairSpec s) => new()
    {
        Color = s.Color.ToString(),
        Opacity = s.Opacity,
        ScalePercent = s.ScalePercent,
        RotationDeg = s.RotationDeg,
        OffsetX = s.OffsetX,
        OffsetY = s.OffsetY,
        Dot = new DotDocument { Enabled = s.Dot.Enabled, Size = s.Dot.Size, Opacity = s.Dot.Opacity },
        Top = LineDocument.From(s.Top),
        Bottom = LineDocument.From(s.Bottom),
        Left = LineDocument.From(s.Left),
        Right = LineDocument.From(s.Right),
        Outline = new OutlineDocument
        {
            Enabled = s.Outline.Enabled,
            Thickness = s.Outline.Thickness,
            Color = s.Outline.Color.ToString(),
            Opacity = s.Outline.Opacity,
        },
    };

    /// <exception cref="FormatException">A color is not "#RRGGBB".</exception>
    public CrosshairSpec ToSpec()
    {
        var d = CrosshairSpec.Default;
        var dot = Dot ?? new DotDocument();
        var outline = Outline ?? new OutlineDocument();
        return new CrosshairSpec
        {
            Color = ParseColor("crosshair.color", Color, d.Color),
            Opacity = Opacity ?? d.Opacity,
            ScalePercent = ScalePercent ?? d.ScalePercent,
            RotationDeg = RotationDeg ?? d.RotationDeg,
            OffsetX = OffsetX ?? d.OffsetX,
            OffsetY = OffsetY ?? d.OffsetY,
            Dot = new DotSpec(dot.Enabled ?? d.Dot.Enabled, dot.Size ?? d.Dot.Size, dot.Opacity ?? d.Dot.Opacity),
            Top = LineDocument.ToSpec(Top, d.Top),
            Bottom = LineDocument.ToSpec(Bottom, d.Bottom),
            Left = LineDocument.ToSpec(Left, d.Left),
            Right = LineDocument.ToSpec(Right, d.Right),
            Outline = new OutlineSpec(
                outline.Enabled ?? d.Outline.Enabled,
                outline.Thickness ?? d.Outline.Thickness,
                ParseColor("crosshair.outline.color", outline.Color, d.Outline.Color),
                outline.Opacity ?? d.Outline.Opacity),
        };
    }

    private static Rgb ParseColor(string path, string? text, Rgb fallback) =>
        text is null ? fallback
        : text.StartsWith('#') && text.Length == 7 && Rgb.TryParse(text, out var rgb) ? rgb
        : throw new FormatException($"{path} must be \"#RRGGBB\", got \"{text}\"");
}

internal sealed class DotDocument
{
    public bool? Enabled { get; set; }
    public int? Size { get; set; }
    public int? Opacity { get; set; }
}

internal sealed class LineDocument
{
    public bool? Enabled { get; set; }
    public int? Length { get; set; }
    public int? Thickness { get; set; }
    public int? Gap { get; set; }

    public static LineDocument From(LineSpec s) => new() { Enabled = s.Enabled, Length = s.Length, Thickness = s.Thickness, Gap = s.Gap };

    public static LineSpec ToSpec(LineDocument? doc, LineSpec d) => doc is null
        ? d
        : new LineSpec(doc.Enabled ?? d.Enabled, doc.Length ?? d.Length, doc.Thickness ?? d.Thickness, doc.Gap ?? d.Gap);
}

internal sealed class OutlineDocument
{
    public bool? Enabled { get; set; }
    public int? Thickness { get; set; }
    public string? Color { get; set; }
    public int? Opacity { get; set; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    AllowDuplicateProperties = false)]
[JsonSerializable(typeof(PresetDocument))]
internal sealed partial class PresetJson : JsonSerializerContext;
