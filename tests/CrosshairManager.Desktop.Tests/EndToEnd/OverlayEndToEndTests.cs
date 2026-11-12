using CrosshairManager.Core.Model;
using CrosshairManager.Core.Presets;
using CrosshairManager.Core.Settings;
using CrosshairManager.Platform.Capture;
using CrosshairManager.Platform.Hotkeys;
using CrosshairManager.Platform.Overlay;
using CrosshairManager.TestSupport;
using static CrosshairManager.Desktop.Tests.EndToEnd.EndToEndHarness;
using static CrosshairManager.Desktop.Tests.Overlay.TopmostKeeperNative;

namespace CrosshairManager.Desktop.Tests.EndToEnd;

/// <summary>
/// Core + Platform end to end on the real desktop: processing order -> OverlayHost.Update -> capture on a #FF00FF
/// BackdropWindow (canvas + 8 px). Every case saves expected.png/actual.png under TestResults/desktop/&lt;case&gt;/.
/// </summary>
[Trait("Category", "Desktop")]
public class OverlayEndToEndTests
{
    private static readonly TimeSpan ReassertTimeout = TimeSpan.FromSeconds(3);
    private static readonly Rgb Blue = new(0x00, 0x00, 0xFF);

    private static CrosshairSpec BuiltIn(string id) =>
        BuiltInPresets.All.Single(p => p.Id == id).Crosshair with { ScalePercent = 100 };

    /// <summary>Shows <paramref name="spec"/> on <paramref name="monitor"/> over a backdrop and asserts the capture.</summary>
    private static async Task AssertRendersOn(string caseName, CrosshairSpec spec, Core.Geometry.IntRect monitor, int tolerance)
    {
        var frame = PrepareVisible(spec, monitor);
        var capture = CaptureRect(frame);
        using var backdrop = BackdropWindow.Around(frame.Window);
        using var host = await ShowAsync(frame);
        backdrop.BringToFrontAndAssertAt(frame.CenterX, frame.CenterY);

        AssertScreenMatches(caseName, capture, Expected(capture, BackdropWindow.Magenta, frame, BackdropWindow.Magenta), tolerance);
    }

    [Theory]
    [InlineData("builtin.default", "a-default")]
    [InlineData("builtin.dot", "a-dot")]
    [InlineData("builtin.classic1px", "a-classic1px")]
    public async Task EndToEnd_a_builtin_at_100_percent_matches_the_composite_exactly(string presetId, string caseName)
    {
        await AssertRendersOn(caseName, BuiltIn(presetId), PrimaryMonitor().Bounds, tolerance: 0);
    }

    [Fact]
    public async Task EndToEnd_b_opacity_50_matches_within_1_per_channel()
    {
        var spec = CrosshairSpec.Default with { Opacity = 50 };
        await AssertRendersOn("b-opacity50", spec, PrimaryMonitor().Bounds, tolerance: 1);
    }

    [Fact]
    public async Task EndToEnd_c_builtin_x45_matches_within_2_per_channel()
    {
        await AssertRendersOn("c-x45", BuiltIn("builtin.x45"), PrimaryMonitor().Bounds, tolerance: 2);
    }

    [Fact]
    public async Task EndToEnd_d_window_at_the_crosshair_center_is_the_backdrop()
    {
        var frame = PrepareVisible(BuiltIn("builtin.default"), PrimaryMonitor().Bounds);
        var capture = CaptureRect(frame);
        using var backdrop = BackdropWindow.Around(frame.Window);
        using var host = await ShowAsync(frame);
        Assert.True(backdrop.BringToFront(), "backdrop did not become the foreground window");

        // The overlay is really drawn over P, yet hit testing at P passes through it to the backdrop.
        AssertScreenMatches("d-click-through", capture,
            Expected(capture, BackdropWindow.Magenta, frame, BackdropWindow.Magenta), tolerance: 0);
        Assert.Equal(backdrop.Handle, ScreenProbe.WindowAt(frame.CenterX, frame.CenterY));
        Assert.NotEqual(host.Handle, ScreenProbe.WindowAt(frame.CenterX, frame.CenterY));
    }

    [Fact]
    public async Task EndToEnd_e_topmost_cover_is_recovered_and_the_crosshair_is_drawn_above_it()
    {
        var frame = PrepareVisible(BuiltIn("builtin.default"), PrimaryMonitor().Bounds);
        var capture = CaptureRect(frame);
        using var backdrop = BackdropWindow.Around(frame.Window);
        using var host = await ShowAsync(frame);
        backdrop.BringToFrontAndAssertAt(frame.CenterX, frame.CenterY);
        using var keeper = new TopmostKeeper(host);
        var reasserted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        keeper.Reasserted += (_, _) => reasserted.TrySetResult();

        using var cover = new BackdropWindow(frame.Window, Blue);
        MakeTopmost(cover);
        await reasserted.Task.WaitAsync(ReassertTimeout);

        // Margin stays magenta; inside the window the crosshair composites over the blue cover, not under it.
        AssertScreenMatches("e-zorder", capture, Expected(capture, BackdropWindow.Magenta, frame, Blue), tolerance: 0);
    }

    [Fact]
    public async Task EndToEnd_f_hotkey_Ctrl_Alt_Shift_F12_hides_and_only_the_backdrop_color_remains()
    {
        Assert.True(HotkeyGesture.TryParse("Ctrl+Alt+Shift+F12", out var gesture));
        var frame = PrepareVisible(BuiltIn("builtin.default"), PrimaryMonitor().Bounds);
        var capture = CaptureRect(frame);
        using var backdrop = BackdropWindow.Around(frame.Window);
        using var host = await ShowAsync(frame);
        backdrop.BringToFrontAndAssertAt(frame.CenterX, frame.CenterY);

        // Precondition: the crosshair is on screen, so a background-only capture below cannot pass vacuously.
        AssertScreenMatches("f-hotkey-before", capture,
            Expected(capture, BackdropWindow.Magenta, frame, BackdropWindow.Magenta), tolerance: 0);

        using var hotkeys = new GlobalHotkeys();
        Assert.True(hotkeys.TryRegister("toggle", gesture, out var error),
            $"RegisterHotKey(Ctrl+Alt+Shift+F12) failed with Win32 error {error}");
        var hidden = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        host.VisibilityChanged += (_, visible) =>
        {
            if (!visible)
            {
                hidden.TrySetResult();
            }
        };
        hotkeys.Pressed += id =>
        {
            if (id != "toggle")
            {
                return;
            }

            try
            {
                host.Hide();
            }
            catch (Exception ex)
            {
                hidden.TrySetException(ex);
            }
        };

        Input.SendChord(gesture);
        await hidden.Task.WaitAsync(EventTimeout);

        Assert.False(host.IsVisible);
        AssertScreenMatches("f-hotkey-hide", capture, Solid(capture, BackdropWindow.Magenta), tolerance: 0);
    }

    [Fact]
    public async Task EndToEnd_g_first_non_primary_monitor_matches_on_that_monitor()
    {
        var monitor = FirstNonPrimaryMonitor().Bounds;
        var frame = PrepareVisible(BuiltIn("builtin.default"), monitor);
        Assert.True(frame.Window.X >= monitor.X && frame.Window.X + frame.Window.W <= monitor.X + monitor.W &&
                    frame.Window.Y >= monitor.Y && frame.Window.Y + frame.Window.H <= monitor.Y + monitor.H,
            $"overlay window {frame.Window} is not on monitor {monitor}");

        await AssertRendersOn("g-second-monitor", BuiltIn("builtin.default"), monitor, tolerance: 0);
    }
}
