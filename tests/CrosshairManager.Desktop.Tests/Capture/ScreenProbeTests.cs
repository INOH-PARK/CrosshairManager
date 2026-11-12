using CrosshairManager.Core.Geometry;
using CrosshairManager.Core.Model;
using CrosshairManager.Core.Render;
using CrosshairManager.Platform.Capture;
using CrosshairManager.TestSupport;

namespace CrosshairManager.Desktop.Tests.Capture;

[Trait("Category", "Desktop")]
public class ScreenProbeTests
{
    private static readonly TimeSpan CaptureTimeout = TimeSpan.FromSeconds(2);
    private static readonly IntRect Canvas = new(956, 536, 8, 8);

    private static bool IsAll(PixelBuffer buffer, Rgb color)
    {
        for (var y = 0; y < buffer.Height; y++)
        {
            for (var x = 0; x < buffer.Width; x++)
            {
                if (buffer.GetPixel(x, y) != new PixelBuffer.Pixel(color.B, color.G, color.R, 255))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Argument validation only: no window, no capture.</summary>
    [Fact]
    public void ScreenProbe_Capture_rejects_an_empty_rectangle()
    {
        Assert.Throws<ArgumentException>(() => ScreenProbe.Capture(new IntRect(0, 0, 0, 8)));
        Assert.Throws<ArgumentException>(() => ScreenProbe.Capture(new IntRect(0, 0, 8, -1)));
    }

    /// <summary>Argument validation only: no window is created.</summary>
    [Fact]
    public void Backdrop_rejects_an_empty_rectangle()
    {
        Assert.Throws<ArgumentException>(() => new Backdrop(new IntRect(0, 0, 0, 0), BackdropWindow.Magenta));
    }

    [Fact]
    public void ScreenProbe_captures_the_backdrop_color_with_alpha_255_and_exact_size()
    {
        using var backdrop = BackdropWindow.Around(Canvas);
        backdrop.BringToFrontAndAssertAt(Canvas.X + 4, Canvas.Y + 4);

        Assert.True(Eventually.True(() => IsAll(ScreenProbe.Capture(Canvas), BackdropWindow.Magenta), CaptureTimeout));
        var shot = ScreenProbe.Capture(backdrop.Rect);
        Assert.Equal((backdrop.Rect.W, backdrop.Rect.H), (shot.Width, shot.Height));
        Assert.True(IsAll(shot, BackdropWindow.Magenta));
    }

    [Fact]
    public void Backdrop_is_not_topmost_and_Dispose_destroys_the_window()
    {
        var backdrop = BackdropWindow.Around(Canvas);
        var handle = backdrop.Handle;
        Assert.True(Overlay.OverlayNative.IsWindow(handle));
        var ex = (uint)Overlay.OverlayNative.GetWindowLong(handle, Overlay.OverlayNative.GwlExStyle);
        Assert.Equal(0u, ex & Overlay.OverlayNative.WsExTopmost);

        backdrop.Dispose();
        Assert.False(Overlay.OverlayNative.IsWindow(handle));
        backdrop.Dispose(); // idempotent
    }
}
