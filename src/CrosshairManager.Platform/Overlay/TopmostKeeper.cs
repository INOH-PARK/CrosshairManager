using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.WindowsAndMessaging;

namespace CrosshairManager.Platform.Overlay;

/// <summary>What triggered a <see cref="TopmostKeeper"/> check.</summary>
public enum TopmostCheckSource
{
    Timer,
    Foreground,
}

/// <summary>
/// Keeps the overlay on top without re-asserting needlessly. On the overlay thread (installed through
/// <see cref="OverlayHost.Invoke"/>): SetWinEventHook(EVENT_SYSTEM_FOREGROUND, WINEVENT_OUTOFCONTEXT) plus a 1 s
/// thread timer. A check walks GW_HWNDPREV from the visible overlay and counts a window as "above" only if it is
/// visible, not DWM-cloaked and its rectangle intersects the overlay's; only then SetWindowPos(HWND_TOPMOST,
/// SWP_NOACTIVATE|SWP_NOMOVE|SWP_NOSIZE) runs and <see cref="Reasserted"/> is raised. Events fire on the overlay
/// thread. Dispose before the host.
/// </summary>
public sealed unsafe class TopmostKeeper : IDisposable
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    private readonly OverlayHost _host;
    private readonly WINEVENTPROC _hookProc;
    private readonly TIMERPROC _timerProc;
    private HWINEVENTHOOK _hook;
    private nuint _timer;
    private int _disposed;

    public TopmostKeeper(OverlayHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        _host = host;
        _hookProc = OnWinEvent;
        _timerProc = OnTimer;
        host.Invoke(() =>
        {
            _hook = PInvoke.SetWinEventHook(PInvoke.EVENT_SYSTEM_FOREGROUND, PInvoke.EVENT_SYSTEM_FOREGROUND,
                HMODULE.Null, _hookProc, 0, 0, PInvoke.WINEVENT_OUTOFCONTEXT);
            if (_hook.IsNull)
            {
                throw new InvalidOperationException("SetWinEventHook(EVENT_SYSTEM_FOREGROUND) failed.");
            }

            _timer = PInvoke.SetTimer(HWND.Null, 0, (uint)Interval.TotalMilliseconds, _timerProc);
            if (_timer == 0)
            {
                PInvoke.UnhookWinEvent(_hook);
                _hook = default;
                throw new InvalidOperationException("SetTimer for the topmost check failed.");
            }
        });
    }

    /// <summary>Raised on the overlay thread after the overlay was put back on top.</summary>
    public event EventHandler? Reasserted;

    /// <summary>Raised on the overlay thread after every check (timer tick or foreground change).</summary>
    public event EventHandler<TopmostCheckSource>? Checked;

    /// <summary>Removes the hook and the timer on the overlay thread. Idempotent.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _host.Invoke(() =>
        {
            PInvoke.KillTimer(HWND.Null, _timer);
            PInvoke.UnhookWinEvent(_hook);
        });
    }

    private void OnTimer(HWND hwnd, uint msg, nuint id, uint time) => Check(TopmostCheckSource.Timer);

    private void OnWinEvent(HWINEVENTHOOK hook, uint @event, HWND hwnd, int idObject, int idChild, uint thread, uint time) =>
        Check(TopmostCheckSource.Foreground);

    private void Check(TopmostCheckSource source)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        var overlay = new HWND(_host.Handle);
        if (PInvoke.IsWindowVisible(overlay) && SomethingAbove(overlay) &&
            PInvoke.SetWindowPos(overlay, HWND.HWND_TOPMOST, 0, 0, 0, 0,
                SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE))
        {
            Reasserted?.Invoke(this, EventArgs.Empty);
        }

        Checked?.Invoke(this, source);
    }

    private static bool SomethingAbove(HWND overlay)
    {
        if (!PInvoke.GetWindowRect(overlay, out var mine))
        {
            return false;
        }

        for (var above = PInvoke.GetWindow(overlay, GET_WINDOW_CMD.GW_HWNDPREV);
             !above.IsNull;
             above = PInvoke.GetWindow(above, GET_WINDOW_CMD.GW_HWNDPREV))
        {
            if (!PInvoke.IsWindowVisible(above) || IsCloaked(above) || !PInvoke.GetWindowRect(above, out var theirs))
            {
                continue;
            }

            if (PInvoke.IntersectRect(out _, mine, theirs))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCloaked(HWND hwnd)
    {
        uint cloaked = 0;
        var hr = PInvoke.DwmGetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_CLOAKED, &cloaked, sizeof(uint));
        return hr.Succeeded && cloaked != 0;
    }
}
