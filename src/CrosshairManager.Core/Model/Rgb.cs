using System.Globalization;

namespace CrosshairManager.Core.Model;

public readonly record struct Rgb(byte R, byte G, byte B)
{
    public static Rgb Parse(string text) =>
        TryParse(text, out var rgb) ? rgb : throw new FormatException($"Invalid color: {text}");

    public static bool TryParse(string? text, out Rgb rgb)
    {
        rgb = default;
        if (text is null)
        {
            return false;
        }

        var s = text.StartsWith('#') ? text[1..] : text;
        if (s.Length == 3)
        {
            s = $"{s[0]}{s[0]}{s[1]}{s[1]}{s[2]}{s[2]}";
        }

        if (s.Length != 6 || !s.All(Uri.IsHexDigit))
        {
            return false;
        }

        rgb = new Rgb(
            byte.Parse(s[0..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(s[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(s[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
        return true;
    }

    public override string ToString() => $"#{R:X2}{G:X2}{B:X2}";
}
