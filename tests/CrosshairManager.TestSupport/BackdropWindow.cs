using CrosshairManager.Core.Geometry;
using CrosshairManager.Core.Model;
using CrosshairManager.Platform.Capture;

namespace CrosshairManager.TestSupport;

/// <summary>
/// Test backdrop: a non-topmost solid popup (Platform <see cref="Backdrop"/>) placed under the overlay so other
/// windows on the desktop cannot pollute captured pixels.
/// </summary>
public sealed class BackdropWindow : IDisposable
{
    /// <summary>Default backdrop color #FF00FF.</summary>
    public static readonly Rgb Magenta = new(0xFF, 0x00, 0xFF);

    /// <summary>Margin added on every side of the overlay canvas.</summary>
    public const int Margin = 8;

    private readonly Backdrop _backdrop;

    public BackdropWindow(IntRect rect, Rgb color)
    {
        Rect = rect;
        Color = color;
        _backdrop = new Backdrop(rect, color);
    }

    public IntRect Rect { get; }

    public Rgb Color { get; }

    public nint Handle => _backdrop.Handle;

    /// <summary>#FF00FF backdrop covering <paramref name="canvas"/> plus <see cref="Margin"/> px on each side.</summary>
    public static BackdropWindow Around(IntRect canvas) =>
        new(new IntRect(canvas.X - Margin, canvas.Y - Margin, canvas.W + 2 * Margin, canvas.H + 2 * Margin), Magenta);

    /// <summary>Brings the backdrop to the foreground (Must have rule); true when it is the foreground window.</summary>
    public bool BringToFront() => _backdrop.BringToFront();

    /// <summary>
    /// Brings the backdrop to front, then fails with the reason unless <see cref="ScreenProbe.WindowAt"/> at
    /// (<paramref name="x"/>, <paramref name="y"/>) is this backdrop.
    /// </summary>
    public void BringToFrontAndAssertAt(int x, int y)
    {
        var foreground = BringToFront();
        var at = ScreenProbe.WindowAt(x, y);
        if (at != Handle)
        {
            throw new InvalidOperationException(
                $"Window at ({x},{y}) is 0x{at:X} but the backdrop is 0x{Handle:X} (backdrop foreground: {foreground}).");
        }
    }

    public void Dispose() => _backdrop.Dispose();
}
