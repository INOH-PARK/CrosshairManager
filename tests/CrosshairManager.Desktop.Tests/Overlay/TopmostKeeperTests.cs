using CrosshairManager.Core.Geometry;
using CrosshairManager.Core.Model;
using CrosshairManager.Core.Render;
using CrosshairManager.Platform.Capture;
using CrosshairManager.Platform.Overlay;
using CrosshairManager.TestSupport;
using static CrosshairManager.Desktop.Tests.Overlay.TopmostKeeperNative;

namespace CrosshairManager.Desktop.Tests.Overlay;

/// <summary>Screen tests (primary monitor center). All captures sit on a BackdropWindow and follow the capture rule.</summary>
[Trait("Category", "Desktop")]
public class TopmostKeeperTests
{
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ReassertTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan CaptureTimeout = TimeSpan.FromSeconds(2);
    private static readonly IntRect Canvas = new(956, 536, 8, 8);
    private static readonly IntRect ProbeCanvas = new(100, 100, 8, 8);
    private static readonly Rgb Red = new(0xFF, 0x00, 0x00);
    private static readonly Rgb Blue = new(0x00, 0x00, 0xFF);

    private static PixelBuffer OpaqueRed(int w, int h)
    {
        var buffer = new PixelBuffer(w, h);
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                buffer.SetPixel(x, y, new PixelBuffer.Pixel(0, 0, 255, 255));
            }
        }

        return buffer;
    }

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

    /// <summary>Shows an 8x8 red overlay at <paramref name="rect"/>, waiting for Updated (subscribed first).</summary>
    private static async Task<OverlayHost> ShowRedOverlay(IntRect rect)
    {
        var host = new OverlayHost();
        var updated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        host.Updated += (_, _) => updated.TrySetResult();
        host.Show();
        host.Update(OpaqueRed(rect.W, rect.H), rect);
        await updated.Task.WaitAsync(EventTimeout);
        Assert.True(host.IsVisible);
        return host;
    }

    /// <summary>Completes after <paramref name="count"/> timer checks of <paramref name="keeper"/> (subscribed first).</summary>
    private static Task TimerTicks(TopmostKeeper keeper, int count)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var seen = 0;
        keeper.Checked += (_, source) =>
        {
            if (source == TopmostCheckSource.Timer && Interlocked.Increment(ref seen) >= count)
            {
                done.TrySetResult();
            }
        };
        return done.Task;
    }

    [Fact]
    public async Task TopmostKeeper_BringToFront_makes_the_backdrop_the_window_at_the_overlay_center()
    {
        using var backdrop = BackdropWindow.Around(Canvas);
        using var host = await ShowRedOverlay(Canvas);

        Assert.True(backdrop.BringToFront(), "backdrop did not become the foreground window");
        Assert.Equal(backdrop.Handle, ScreenProbe.WindowAt(Canvas.X + Canvas.W / 2, Canvas.Y + Canvas.H / 2));
        Assert.True(Eventually.True(() => IsAll(ScreenProbe.Capture(Canvas), Red), CaptureTimeout));
    }

    [Fact]
    public async Task TopmostKeeper_reasserts_within_3_seconds_when_a_topmost_window_covers_the_overlay()
    {
        using var backdrop = BackdropWindow.Around(Canvas);
        using var host = await ShowRedOverlay(Canvas);
        backdrop.BringToFrontAndAssertAt(Canvas.X + Canvas.W / 2, Canvas.Y + Canvas.H / 2);
        using var keeper = new TopmostKeeper(host);
        var reasserted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        keeper.Reasserted += (_, _) => reasserted.TrySetResult();

        using var cover = new BackdropWindow(Canvas, Blue);
        MakeTopmost(cover.Handle);

        await reasserted.Task.WaitAsync(ReassertTimeout);
        Assert.True(Eventually.True(() => IsAll(ScreenProbe.Capture(Canvas), Red), CaptureTimeout),
            "overlay is not visible above the topmost cover after Reasserted");
    }

    [Fact]
    public async Task TopmostKeeper_does_not_reassert_when_nothing_is_above_over_3_timer_ticks()
    {
        using var backdrop = BackdropWindow.Around(Canvas);
        using var host = await ShowRedOverlay(Canvas);
        backdrop.BringToFrontAndAssertAt(Canvas.X + Canvas.W / 2, Canvas.Y + Canvas.H / 2);
        using var keeper = new TopmostKeeper(host);
        var reasserts = 0;
        keeper.Reasserted += (_, _) => Interlocked.Increment(ref reasserts);
        var ticks = TimerTicks(keeper, 3);

        await ticks.WaitAsync(EventTimeout);

        Assert.Equal(0, Volatile.Read(ref reasserts));
    }

    [Fact]
    public async Task TopmostKeeper_after_Dispose_a_topmost_cover_stays_above_the_overlay()
    {
        using var backdrop = BackdropWindow.Around(Canvas);
        using var host = await ShowRedOverlay(Canvas);
        backdrop.BringToFrontAndAssertAt(Canvas.X + Canvas.W / 2, Canvas.Y + Canvas.H / 2);
        var keeper = new TopmostKeeper(host);
        var reasserts = 0;
        keeper.Reasserted += (_, _) => Interlocked.Increment(ref reasserts);
        keeper.Dispose();
        keeper.Dispose(); // idempotent

        // An independent keeper elsewhere paces the wait: two of its 1 s ticks cover a full interval of the disposed one.
        using var probeHost = await ShowRedOverlay(ProbeCanvas);
        using var probeKeeper = new TopmostKeeper(probeHost);
        var ticks = TimerTicks(probeKeeper, 2);

        using var cover = new BackdropWindow(Canvas, Blue);
        MakeTopmost(cover.Handle);
        await ticks.WaitAsync(EventTimeout);

        Assert.Equal(0, Volatile.Read(ref reasserts));
        Assert.True(Eventually.True(() => IsAll(ScreenProbe.Capture(Canvas), Blue), CaptureTimeout),
            "capture is not the cover color after the keeper was disposed");
        Assert.False(IsAll(ScreenProbe.Capture(Canvas), Red));
    }
}
