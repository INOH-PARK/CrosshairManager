namespace CrosshairManager.Core.Geometry;

public static class OverlayPlacement
{
    /// <summary>
    /// Places the canvas so that P lands on (Left + W/2 + offsetX, Top + H/2 + offsetY) and clips it to the monitor.
    /// Returns the on-screen window and the matching canvas sub-rectangle; both are empty when nothing is visible.
    /// </summary>
    public static (IntRect Window, IntRect CanvasRegion) Compute(
        CrosshairGeometry g, IntRect monitorPhysical, int offsetX, int offsetY)
    {
        ArgumentNullException.ThrowIfNull(g);
        var m = monitorPhysical;
        if (g.PivotX - g.AnchorX != (m.W % 2 != 0 ? 0.5 : 0) || g.PivotY - g.AnchorY != (m.H % 2 != 0 ? 0.5 : 0))
        {
            throw new ArgumentException(
                $"Geometry parity does not match monitor size {m.W}x{m.H}; recompute the layout.", nameof(g));
        }

        var x = m.X + (m.W / 2) + offsetX - g.AnchorX;
        var y = m.Y + (m.H / 2) + offsetY - g.AnchorY;
        var x0 = Math.Max(x, m.X);
        var y0 = Math.Max(y, m.Y);
        var x1 = Math.Min(x + g.CanvasW, m.X + m.W);
        var y1 = Math.Min(y + g.CanvasH, m.Y + m.H);
        if (x1 <= x0 || y1 <= y0)
        {
            return (default, default);
        }

        return (new IntRect(x0, y0, x1 - x0, y1 - y0), new IntRect(x0 - x, y0 - y, x1 - x0, y1 - y0));
    }
}
