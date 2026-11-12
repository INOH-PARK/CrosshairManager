using CrosshairManager.Core.Settings;
using CrosshairManager.Platform.Hotkeys;
using System.Windows;
using System.Windows.Interop;
using CrosshairManager.TestSupport;

namespace CrosshairManager.Desktop.Tests.Hotkeys;

[Trait("Category", "Desktop")]
public class GlobalHotkeysTests
{
    private const int ErrorHotkeyAlreadyRegistered = 1409;

    private static HotkeyGesture Chord()
    {
        Assert.True(HotkeyGesture.TryParse("Ctrl+Alt+Shift+F11", out var g));
        return g;
    }

    [Fact]
    public async Task Registered_chord_raises_Pressed_when_sent()
    {
        using var focus = new ForegroundWindow();
        using var hotkeys = new GlobalHotkeys();
        var pressed = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        hotkeys.Pressed += id => pressed.TrySetResult(id);

        Assert.True(hotkeys.TryRegister("test", Chord(), out var error), $"Win32 error {error}");
        Input.SendChord(Chord());

        var id = await pressed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("test", id);
    }

    [Fact]
    public void Second_instance_registering_same_chord_fails_with_1409()
    {
        using var first = new GlobalHotkeys();
        using var second = new GlobalHotkeys();

        Assert.True(first.TryRegister("a", Chord(), out _));
        Assert.False(second.TryRegister("b", Chord(), out var error));
        Assert.Equal(ErrorHotkeyAlreadyRegistered, error);
    }

    [Fact]
    public void Unregister_releases_chord_for_other_instance()
    {
        using var first = new GlobalHotkeys();
        using var second = new GlobalHotkeys();

        Assert.True(first.TryRegister("a", Chord(), out _));
        first.Unregister("a");
        Assert.True(second.TryRegister("b", Chord(), out var error), $"Win32 error {error}");
    }

    [Fact]
    public void Dispose_releases_chord()
    {
        var first = new GlobalHotkeys();
        Assert.True(first.TryRegister("a", Chord(), out _));
        first.Dispose();
        using var second = new GlobalHotkeys();
        Assert.True(second.TryRegister("b", Chord(), out var error), $"Win32 error {error}");
    }

    /// <summary>
    /// Tiny foreground window so injected input is not swallowed by whatever (possibly elevated) app has focus.
    /// RegisterHotKey itself does not depend on focus.
    /// </summary>
    private sealed class ForegroundWindow : IDisposable
    {
        private readonly Thread _thread;
        private readonly ManualResetEventSlim _shown = new();
        private System.Windows.Threading.Dispatcher? _dispatcher;
        private nint _handle;

        public ForegroundWindow()
        {
            _thread = new Thread(() =>
            {
                var window = new Window
                {
                    Width = 8, Height = 8, Left = 0, Top = 0,
                    WindowStyle = WindowStyle.None, ShowInTaskbar = false, ResizeMode = ResizeMode.NoResize,
                };
                window.SourceInitialized += (_, _) => _handle = new WindowInteropHelper(window).Handle;
                _dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
                window.Show();
                window.Activate();
                Input.ForceForeground(_handle);
                _shown.Set();
                System.Windows.Threading.Dispatcher.Run();
                window.Close();
            }) { IsBackground = true };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
            Assert.True(_shown.Wait(TimeSpan.FromSeconds(5)), "focus window did not show");
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
            while (Input.GetForegroundHandle() != _handle && DateTime.UtcNow < deadline)
            {
                Input.FlushDwm();
            }

            Assert.Equal(_handle, Input.GetForegroundHandle());
        }

        public void Dispose()
        {
            _dispatcher?.InvokeShutdown();
            _thread.Join(TimeSpan.FromSeconds(5));
            _shown.Dispose();
        }
    }
}
