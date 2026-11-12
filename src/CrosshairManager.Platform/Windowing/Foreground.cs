using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace CrosshairManager.Platform.Windowing;

/// <summary>Foreground hand-off between instances and the Must have foreground rule for app windows.</summary>
public static unsafe class Foreground
{
    /// <summary>ASFW_ANY: lets any process set the foreground window.</summary>
    public const int AnyProcess = -1;

    /// <summary>AllowSetForegroundWindow for <paramref name="processId"/> (or <see cref="AnyProcess"/>).</summary>
    public static bool AllowFor(int processId) =>
        PInvoke.AllowSetForegroundWindow(processId < 0 ? uint.MaxValue : (uint)processId);

    /// <summary>
    /// SetForegroundWindow first; if <paramref name="hwnd"/> is not the foreground window afterwards, attach to the current
    /// foreground window's thread, SetWindowPos(HWND_TOP), SetForegroundWindow, detach. Call on the window's thread.
    /// Returns whether the window is the foreground window afterwards.
    /// </summary>
    public static bool Activate(nint hwnd)
    {
        var target = new HWND(hwnd);
        PInvoke.SetForegroundWindow(target);
        if (PInvoke.GetForegroundWindow() == target)
        {
            return true;
        }

        var foreground = PInvoke.GetForegroundWindow();
        var foregroundThread = foreground.IsNull ? 0 : PInvoke.GetWindowThreadProcessId(foreground, null);
        var self = PInvoke.GetCurrentThreadId();
        var attached = foregroundThread != 0 && foregroundThread != self && PInvoke.AttachThreadInput(self, foregroundThread, true);
        try
        {
            PInvoke.SetWindowPos(target, HWND.HWND_TOP, 0, 0, 0, 0,
                SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW);
            PInvoke.SetForegroundWindow(target);
        }
        finally
        {
            if (attached)
            {
                PInvoke.AttachThreadInput(self, foregroundThread, false);
            }
        }

        return PInvoke.GetForegroundWindow() == target;
    }
}
