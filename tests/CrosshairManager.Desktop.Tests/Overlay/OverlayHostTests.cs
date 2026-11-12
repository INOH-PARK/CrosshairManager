using System.Diagnostics;
using CrosshairManager.Core.Geometry;
using CrosshairManager.Core.Render;
using CrosshairManager.Platform.Overlay;
using static CrosshairManager.Desktop.Tests.Overlay.OverlayNative;

namespace CrosshairManager.Desktop.Tests.Overlay;

[Trait("Category", "Desktop")]
public class OverlayHostTests
{
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(5);
    private static readonly IntRect At100 = new(100, 100, 8, 8);

    private static PixelBuffer Solid(int w, int h, PixelBuffer.Pixel pixel)
    {
        var buffer = new PixelBuffer(w, h);
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                buffer.SetPixel(x, y, pixel);
            }
        }

        return buffer;
    }

    private static PixelBuffer OpaqueRed() => Solid(8, 8, new PixelBuffer.Pixel(0, 0, 255, 255));

    /// <summary>Subscribes to Updated before triggering the update, then waits for that signal (5 s cap).</summary>
    private static async Task UpdateAndWait(OverlayHost host, PixelBuffer? buffer, IntRect rect)
    {
        var updated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Handler(object? sender, EventArgs e) => updated.TrySetResult();
        host.Updated += Handler;
        try
        {
            host.Update(buffer, rect);
            await updated.Task.WaitAsync(EventTimeout);
        }
        finally
        {
            host.Updated -= Handler;
        }
    }

    [Fact]
    public async Task OverlayHost_8x8_red_at_100_100_is_visible_with_exact_rect_and_all_five_ex_styles()
    {
        using var host = new OverlayHost();
        host.Show();
        await UpdateAndWait(host, OpaqueRed(), At100);

        Assert.True(GetWindowRect(host.Handle, out var rect));
        Assert.Equal((100, 100, 108, 108), (rect.Left, rect.Top, rect.Right, rect.Bottom));
        Assert.True(host.IsVisible);
        Assert.True(IsWindowVisible(host.Handle));

        var ex = (uint)GetWindowLong(host.Handle, GwlExStyle);
        foreach (var (name, flag) in new[]
                 {
                     ("WS_EX_LAYERED", WsExLayered), ("WS_EX_TRANSPARENT", WsExTransparent),
                     ("WS_EX_TOPMOST", WsExTopmost), ("WS_EX_TOOLWINDOW", WsExToolWindow),
                     ("WS_EX_NOACTIVATE", WsExNoActivate),
                 })
        {
            Assert.True((ex & flag) == flag, $"{name} missing from GWL_EXSTYLE 0x{ex:X8}");
        }

        Assert.Equal(OverlayHost.WindowClassName, ClassNameOf(host.Handle));
        Assert.Equal(OverlayHost.WindowClassName, ClassNameOf(host.ControllerHandle));
    }

    [Fact]
    public async Task OverlayHost_Show_does_not_change_the_foreground_window()
    {
        var before = GetForegroundWindow();
        Assert.NotEqual(0, before);

        using var host = new OverlayHost();
        host.Show();
        await UpdateAndWait(host, OpaqueRed(), At100);
        DwmFlush();

        Assert.True(host.IsVisible);
        var after = GetForegroundWindow();
        Assert.Equal(before, after);
        Assert.NotEqual(host.Handle, after);
    }

    [Fact]
    public async Task OverlayHost_fully_transparent_buffer_hides_the_window()
    {
        using var host = new OverlayHost();
        host.Show();
        await UpdateAndWait(host, OpaqueRed(), At100);
        Assert.True(IsWindowVisible(host.Handle));

        var hidden = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        host.VisibilityChanged += (_, visible) => hidden.TrySetResult(visible);
        await UpdateAndWait(host, new PixelBuffer(8, 8), At100);

        Assert.False(await hidden.Task.WaitAsync(EventTimeout));
        Assert.False(host.IsVisible);
        Assert.False(IsWindowVisible(host.Handle));
    }

    [Fact]
    public async Task OverlayHost_null_buffer_or_empty_rect_hides_the_window()
    {
        using var host = new OverlayHost();
        host.Show();
        await UpdateAndWait(host, OpaqueRed(), At100);
        await UpdateAndWait(host, OpaqueRed(), new IntRect(100, 100, 0, 0));
        Assert.False(IsWindowVisible(host.Handle));

        await UpdateAndWait(host, OpaqueRed(), At100);
        Assert.True(IsWindowVisible(host.Handle));
        await UpdateAndWait(host, null, At100);
        Assert.False(IsWindowVisible(host.Handle));
    }

    [Fact]
    public async Task OverlayHost_Hide_hides_and_Show_restores_without_repainting()
    {
        using var host = new OverlayHost();
        await UpdateAndWait(host, OpaqueRed(), At100);
        Assert.False(IsWindowVisible(host.Handle)); // content alone does not show it

        host.Show();
        Assert.True(IsWindowVisible(host.Handle));
        host.Hide();
        Assert.False(IsWindowVisible(host.Handle));
        Assert.False(host.IsVisible);
        host.Show();
        Assert.True(IsWindowVisible(host.Handle));
        Assert.True(GetWindowRect(host.Handle, out var rect));
        Assert.Equal((100, 100, 108, 108), (rect.Left, rect.Top, rect.Right, rect.Bottom));
    }

    [Fact]
    public void OverlayHost_Update_with_mismatched_size_throws_and_leaves_window_hidden()
    {
        using var host = new OverlayHost();
        host.Show();
        Assert.Throws<ArgumentException>(() => host.Update(OpaqueRed(), new IntRect(100, 100, 9, 8)));
        Assert.False(IsWindowVisible(host.Handle));
    }

    [Fact]
    public void OverlayHost_Invoke_runs_on_the_Overlay_thread_and_propagates_exceptions()
    {
        using var host = new OverlayHost();
        string? name = null;
        host.Invoke(() => name = Thread.CurrentThread.Name);
        Assert.Equal("Overlay", name);

        var thrown = Assert.Throws<InvalidOperationException>(() => host.Invoke(() => throw new InvalidOperationException("boom")));
        Assert.Equal("boom", thrown.Message);

        // Still alive after a failing action.
        var ran = false;
        host.Invoke(() => ran = true);
        Assert.True(ran);
    }

    [Fact]
    public void OverlayHost_Dispose_destroys_both_windows_within_5_seconds()
    {
        var host = new OverlayHost();
        var overlay = host.Handle;
        var controller = host.ControllerHandle;
        Assert.True(IsWindow(overlay));
        Assert.True(IsWindow(controller));

        var watch = Stopwatch.StartNew();
        host.Dispose();
        watch.Stop();

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"Dispose took {watch.Elapsed}");
        Assert.False(IsWindow(overlay));
        Assert.False(IsWindow(controller));
        Assert.Throws<ObjectDisposedException>(() => host.Show());
        host.Dispose(); // idempotent
    }

    [Fact]
    public void OverlayHost_two_instances_coexist_and_class_survives_first_dispose()
    {
        using var first = new OverlayHost();
        var second = new OverlayHost();
        second.Dispose();
        using var third = new OverlayHost();
        Assert.True(IsWindow(first.Handle));
        Assert.True(IsWindow(third.Handle));
    }
}
