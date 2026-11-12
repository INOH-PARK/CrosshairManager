using System.Drawing;
using Windows.Win32;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.HiDpi;

namespace CrosshairManager.Platform.Dpi;

/// <summary>Per-Monitor-V2 DPI awareness helpers. All overlay coordinates are physical pixels.</summary>
public static class DpiAwareness
{
    private static readonly DPI_AWARENESS_CONTEXT PerMonitorAwareV2 = new((nint)(-4));

    /// <summary>
    /// Makes the process Per-Monitor-V2 aware unless it already is (for example via app.manifest, in which
    /// case SetProcessDpiAwarenessContext fails with ERROR_ACCESS_DENIED). Returns whether the calling
    /// thread ends up Per-Monitor-V2 aware.
    /// </summary>
    public static bool EnsurePerMonitorV2()
    {
        if (IsThreadPerMonitorV2())
        {
            return true;
        }

        PInvoke.SetProcessDpiAwarenessContext(PerMonitorAwareV2);
        return IsThreadPerMonitorV2();
    }

    public static bool IsThreadPerMonitorV2() =>
        PInvoke.AreDpiAwarenessContextsEqual(PInvoke.GetThreadDpiAwarenessContext(), PerMonitorAwareV2);

    /// <summary>Effective DPI (MDT_EFFECTIVE_DPI, X axis) of the monitor nearest to the given physical point.</summary>
    public static uint GetEffectiveDpi(int x, int y)
    {
        var monitor = PInvoke.MonitorFromPoint(new Point(x, y), MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        PInvoke.GetDpiForMonitor(monitor, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out var dpiX, out _).ThrowOnFailure();
        return dpiX;
    }
}
