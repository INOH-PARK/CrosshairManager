using CrosshairManager.Core.Model;
using CrosshairManager.Core.Render;

namespace CrosshairManager.TestSupport;

/// <summary>Expected screen pixels: premultiplied BGRA composited (source-over) onto an opaque backdrop color.</summary>
public static class Composite
{
    /// <summary>
    /// Per channel <c>out = p + round(bg * (255 - a) / 255)</c> (half rounds up), clamped to 255; alpha is always 255.
    /// Returns a new buffer of the same size; the input is not modified.
    /// </summary>
    public static PixelBuffer Over(PixelBuffer premul, Rgb backdrop)
    {
        ArgumentNullException.ThrowIfNull(premul);
        var result = new PixelBuffer(premul.Width, premul.Height);
        for (var y = 0; y < premul.Height; y++)
        {
            for (var x = 0; x < premul.Width; x++)
            {
                var p = premul.GetPixel(x, y);
                var rest = 255 - p.A;
                result.SetPixel(x, y, new PixelBuffer.Pixel(
                    Channel(p.B, backdrop.B, rest),
                    Channel(p.G, backdrop.G, rest),
                    Channel(p.R, backdrop.R, rest),
                    255));
            }
        }

        return result;
    }

    private static byte Channel(byte source, byte backdrop, int rest) =>
        (byte)Math.Min(255, source + (backdrop * rest + 127) / 255);
}
