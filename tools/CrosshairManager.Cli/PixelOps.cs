using CrosshairManager.Core.Model;
using CrosshairManager.Core.Render;

namespace CrosshairManager.Cli;

internal static class PixelOps
{
    /// <summary>Source-over onto an opaque color: <c>out = p + round(bg * (255 - a) / 255)</c> (half up), alpha 255.</summary>
    public static PixelBuffer Over(PixelBuffer premul, Rgb bg)
    {
        var result = new PixelBuffer(premul.Width, premul.Height);
        for (var y = 0; y < premul.Height; y++)
        {
            for (var x = 0; x < premul.Width; x++)
            {
                var p = premul.GetPixel(x, y);
                var rest = 255 - p.A;
                result.SetPixel(x, y, new PixelBuffer.Pixel(
                    Channel(p.B, bg.B, rest), Channel(p.G, bg.G, rest), Channel(p.R, bg.R, rest), 255));
            }
        }

        return result;
    }

    public static PixelBuffer Solid(int width, int height, Rgb color)
    {
        var result = new PixelBuffer(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                result.SetPixel(x, y, new PixelBuffer.Pixel(color.B, color.G, color.R, 255));
            }
        }

        return result;
    }

    /// <summary>Nearest-neighbor enlargement.</summary>
    public static PixelBuffer Zoom(PixelBuffer source, int factor)
    {
        if (factor == 1)
        {
            return source;
        }

        var result = new PixelBuffer(source.Width * factor, source.Height * factor);
        for (var y = 0; y < result.Height; y++)
        {
            for (var x = 0; x < result.Width; x++)
            {
                result.SetPixel(x, y, source.GetPixel(x / factor, y / factor));
            }
        }

        return result;
    }

    /// <summary>Counts pixels where any channel differs by more than <paramref name="tolerance"/>; both buffers must be the same size.</summary>
    public static Difference Diff(PixelBuffer expected, PixelBuffer actual, int tolerance)
    {
        var count = 0;
        (int X, int Y)? first = null;
        for (var y = 0; y < expected.Height; y++)
        {
            for (var x = 0; x < expected.Width; x++)
            {
                var e = expected.GetPixel(x, y);
                var a = actual.GetPixel(x, y);
                if (Math.Abs(e.B - a.B) > tolerance || Math.Abs(e.G - a.G) > tolerance ||
                    Math.Abs(e.R - a.R) > tolerance || Math.Abs(e.A - a.A) > tolerance)
                {
                    count++;
                    first ??= (x, y);
                }
            }
        }

        return new Difference(count, first, first is { } f ? expected.GetPixel(f.X, f.Y) : null, first is { } g ? actual.GetPixel(g.X, g.Y) : null);
    }

    public static string Describe(PixelBuffer.Pixel p) => $"#{p.R:X2}{p.G:X2}{p.B:X2}/{p.A:X2}";

    public static string Summarize(Difference d) =>
        $"{d.Count} pixel(s) differ, first at ({d.First!.Value.X},{d.First.Value.Y}) expected {Describe(d.Expected!.Value)} actual {Describe(d.Actual!.Value)}";

    private static byte Channel(byte source, byte backdrop, int rest) => (byte)Math.Min(255, source + (((backdrop * rest) + 127) / 255));

    internal readonly record struct Difference(int Count, (int X, int Y)? First, PixelBuffer.Pixel? Expected, PixelBuffer.Pixel? Actual);
}
