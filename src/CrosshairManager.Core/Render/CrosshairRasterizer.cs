using CrosshairManager.Core.Geometry;

namespace CrosshairManager.Core.Render;

/// <summary>
/// Rasterizes a <see cref="CrosshairGeometry"/> into premultiplied BGRA (rules in docs/contracts.md "래스터라이저").
/// Multiples of 90 degrees take one sample at each pixel center (exact, no anti-aliasing);
/// 45/135/225/315 degrees use 4x4 uniform supersampling. Each sample is rotated by -RotationDeg about P
/// and classified dot -> line -> outline -> transparent.
/// </summary>
public static class CrosshairRasterizer
{
    private static readonly double H = Math.Sqrt(0.5);
    private static readonly double[] Cos = { 1, H, 0, -H, -1, -H, 0, H };
    private static readonly double[] Sin = { 0, H, 1, H, 0, -H, -1, -H };

    public static PixelBuffer Render(CrosshairGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        if (geometry.CanvasW <= 0 || geometry.CanvasH <= 0)
        {
            throw new ArgumentException("Geometry has an empty canvas; nothing to render.", nameof(geometry));
        }

        return Render(geometry, new IntRect(0, 0, geometry.CanvasW, geometry.CanvasH));
    }

    /// <summary>
    /// Renders the canvas pixels inside <paramref name="canvasRegion"/> (canvas coordinates; may extend past the canvas).
    /// Output pixel (0,0) is canvas pixel (region.X, region.Y).
    /// </summary>
    public static PixelBuffer Render(CrosshairGeometry geometry, IntRect canvasRegion)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        if (canvasRegion.IsEmpty)
        {
            throw new ArgumentException("Region must be non-empty.", nameof(canvasRegion));
        }

        var steps = geometry.RotationDeg / 45;
        if (steps != Math.Round(steps))
        {
            throw new ArgumentException($"Rotation must be a multiple of 45 degrees: {geometry.RotationDeg}", nameof(geometry));
        }

        var k = (((int)steps % 8) + 8) % 8;
        var cos = Cos[k];
        var sin = Sin[k];
        var n = k % 2 == 0 ? 1 : 4;
        var samples = n * n;

        var dotAlpha = geometry.DotAlpha;
        var lineAlpha = geometry.LineAlpha;
        var outlineAlpha = geometry.OutlineAlpha;
        var color = geometry.Color;
        var outlineColor = geometry.OutlineColor;

        var buffer = new PixelBuffer(canvasRegion.W, canvasRegion.H);
        for (int j = 0; j < canvasRegion.H; j++)
        {
            for (int i = 0; i < canvasRegion.W; i++)
            {
                int nDot = 0, nLine = 0, nOutline = 0;
                for (int b = 0; b < n; b++)
                {
                    for (int a = 0; a < n; a++)
                    {
                        var dx = canvasRegion.X + i + ((a + 0.5) / n) - geometry.PivotX;
                        var dy = canvasRegion.Y + j + ((b + 0.5) / n) - geometry.PivotY;
                        var x = geometry.PivotX + ((dx * cos) + (dy * sin));
                        var y = geometry.PivotY + ((-dx * sin) + (dy * cos));
                        switch (Classify(geometry, x, y))
                        {
                            case Part.Dot: nDot++; break;
                            case Part.Line: nLine++; break;
                            case Part.Outline: nOutline++; break;
                        }
                    }
                }

                if (nDot + nLine + nOutline == 0)
                {
                    continue;
                }

                // Weighted sums: alpha = round(sum(n_k * a_k) / N); c_pm = round(sum(n_k * a_k * c_k) / (N * 255)).
                long wDot = nDot * dotAlpha, wLine = nLine * lineAlpha, wOutline = nOutline * outlineAlpha;
                var fillW = wDot + wLine;
                var alpha = Round(fillW + wOutline, samples);
                buffer.SetPixel(i, j, new PixelBuffer.Pixel(
                    Channel(fillW, color.B, wOutline, outlineColor.B, samples),
                    Channel(fillW, color.G, wOutline, outlineColor.G, samples),
                    Channel(fillW, color.R, wOutline, outlineColor.R, samples),
                    (byte)alpha));
            }
        }

        return buffer;
    }

    private enum Part
    {
        None,
        Dot,
        Line,
        Outline,
    }

    private static Part Classify(CrosshairGeometry g, double x, double y)
    {
        foreach (var f in g.Fills)
        {
            if (f.Role == ShapeRole.Dot && Inside(f.Rect, x, y, 0))
            {
                return Part.Dot;
            }
        }

        foreach (var f in g.Fills)
        {
            if (f.Role != ShapeRole.Dot && Inside(f.Rect, x, y, 0))
            {
                return Part.Line;
            }
        }

        if (g.OutlineThickness > 0)
        {
            foreach (var f in g.Fills)
            {
                if (Inside(f.Rect, x, y, g.OutlineThickness))
                {
                    return Part.Outline;
                }
            }
        }

        return Part.None;
    }

    private static bool Inside(IntRect r, double x, double y, int grow) =>
        x >= r.X - grow && x < r.X + r.W + grow && y >= r.Y - grow && y < r.Y + r.H + grow;

    private static long Round(long sum, int divisor) => ((2 * sum) + divisor) / (2 * divisor);

    private static byte Channel(long fillWeight, byte fill, long outlineWeight, byte outline, int samples) =>
        (byte)Round((fillWeight * fill) + (outlineWeight * outline), samples * 255);
}
