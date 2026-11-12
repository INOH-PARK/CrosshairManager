using CrosshairManager.Core.Model;

namespace CrosshairManager.Core.Geometry;

/// <summary>
/// Pixel-exact layout of a normalized, scaled <see cref="CrosshairSpec"/> (rules in docs/contracts.md "도형 규칙").
/// Only the monitor size parity matters: P sits on a pixel boundary (even) or a pixel center (odd).
/// </summary>
public static class CrosshairLayout
{
    private const double DiagonalGrowth = 0.4143;

    public static CrosshairGeometry Compute(CrosshairSpec scaled, bool oddWidth, bool oddHeight)
    {
        ArgumentNullException.ThrowIfNull(scaled);

        // P relative to the integer anchor pixel boundary.
        var px = oddWidth ? 0.5 : 0;
        var py = oddHeight ? 0.5 : 0;

        var dotSize = scaled.Dot.Enabled ? scaled.Dot.Size : 0;
        var ty = BandThickness(scaled.Left, scaled.Right, dotSize);
        var tx = BandThickness(scaled.Top, scaled.Bottom, dotSize);
        var cy = ty > 0 ? Half(ty, py) + (ty / 2.0) : py;
        var cx = tx > 0 ? Half(tx, px) + (tx / 2.0) : px;

        var rel = new List<GeomRect>(5);
        if (dotSize > 0)
        {
            rel.Add(new(ShapeRole.Dot, new IntRect(Half(dotSize, px), Half(dotSize, py), dotSize, dotSize)));
        }

        if (IsVisible(scaled.Top))
        {
            var l = scaled.Top;
            var topEnd = Snap(cy - l.Gap);
            rel.Add(new(ShapeRole.Top, new IntRect(Half(l.Thickness, px), topEnd - l.Length, l.Thickness, l.Length)));
        }

        if (IsVisible(scaled.Bottom))
        {
            var l = scaled.Bottom;
            var bottomStart = (int)(2 * cy) - Snap(cy - l.Gap);
            rel.Add(new(ShapeRole.Bottom, new IntRect(Half(l.Thickness, px), bottomStart, l.Thickness, l.Length)));
        }

        if (IsVisible(scaled.Left))
        {
            var l = scaled.Left;
            var leftEnd = Snap(cx - l.Gap);
            rel.Add(new(ShapeRole.Left, new IntRect(leftEnd - l.Length, Half(l.Thickness, py), l.Length, l.Thickness)));
        }

        if (IsVisible(scaled.Right))
        {
            var l = scaled.Right;
            var rightStart = (int)(2 * cx) - Snap(cx - l.Gap);
            rel.Add(new(ShapeRole.Right, new IntRect(rightStart, Half(l.Thickness, py), l.Length, l.Thickness)));
        }

        var outline = scaled.Outline.Enabled ? scaled.Outline.Thickness : 0;
        var e = 0;
        if (rel.Count > 0)
        {
            double reach = 0;
            foreach (var r in rel.Select(f => f.Rect))
            {
                reach = Math.Max(reach, Math.Max(
                    Math.Max(Math.Abs(r.X - px), Math.Abs(r.X + r.W - px)),
                    Math.Max(Math.Abs(r.Y - py), Math.Abs(r.Y + r.H - py))));
            }

            var e0 = (int)Math.Ceiling(reach) + outline;
            e = scaled.RotationDeg % 90 == 0 ? e0 : e0 + (int)Math.Ceiling(e0 * DiagonalGrowth) + 1;
        }

        // Canvas spans [floor(P - e), ceil(P + e)); its origin is the anchor boundary minus e.
        var fills = rel
            .Select(f => f with { Rect = f.Rect with { X = f.Rect.X + e, Y = f.Rect.Y + e } })
            .ToArray();
        var canvasW = rel.Count > 0 ? (2 * e) + (oddWidth ? 1 : 0) : 0;
        var canvasH = rel.Count > 0 ? (2 * e) + (oddHeight ? 1 : 0) : 0;

        return new CrosshairGeometry(
            canvasW,
            canvasH,
            e + px,
            e + py,
            e,
            e,
            fills,
            outline,
            scaled.RotationDeg,
            scaled.Color,
            scaled.Outline.Color,
            Alpha(scaled.Dot.Opacity, scaled.Opacity),
            Alpha(100, scaled.Opacity),
            Alpha(scaled.Outline.Opacity, scaled.Opacity));
    }

    private static bool IsVisible(LineSpec line) => line.Enabled && line.Length > 0;

    private static int BandThickness(LineSpec a, LineSpec b, int dotSize)
    {
        var t = Math.Max(IsVisible(a) ? a.Thickness : 0, IsVisible(b) ? b.Thickness : 0);
        return t > 0 ? t : dotSize;
    }

    private static int Snap(double x) => (int)Math.Floor(x + 0.5);

    private static int Half(int n, double c) => Snap(c - (n / 2.0));

    private static byte Alpha(int partOpacity, int opacity) =>
        (byte)(((255 * partOpacity * opacity) + 5000) / 10000);
}
