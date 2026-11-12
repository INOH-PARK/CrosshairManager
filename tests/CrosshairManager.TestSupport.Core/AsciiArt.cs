using CrosshairManager.Core.Render;

namespace CrosshairManager.TestSupport.Core;

/// <summary>
/// Renders a <see cref="PixelBuffer"/> as text, one row per line: <c>X</c> = exact fill pixel,
/// <c>o</c> = exact outline pixel, <c>.</c> = fully transparent, <c>+</c> = anything else (partial coverage or blend).
/// </summary>
public static class AsciiArt
{
    /// <summary>Opaque #00FF00 (the default crosshair color) in premultiplied BGRA.</summary>
    public static PixelBuffer.Pixel DefaultFill { get; } = new(0, 255, 0, 255);

    /// <summary>Opaque #000000 (the default outline color).</summary>
    public static PixelBuffer.Pixel DefaultOutline { get; } = new(0, 0, 0, 255);

    public static string From(PixelBuffer image) => From(image, DefaultFill, DefaultOutline);

    public static string From(PixelBuffer image, PixelBuffer.Pixel fill, PixelBuffer.Pixel outline)
    {
        ArgumentNullException.ThrowIfNull(image);
        var rows = new string[image.Height];
        var row = new char[image.Width];
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                var p = image.GetPixel(x, y);
                row[x] = p == fill ? 'X' : p == outline ? 'o' : p.A == 0 && p.R == 0 && p.G == 0 && p.B == 0 ? '.' : '+';
            }

            rows[y] = new string(row);
        }

        return string.Join("\n", rows);
    }
}
