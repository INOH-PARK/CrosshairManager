using Windows.Win32;
using Windows.Win32.UI.HiDpi;

namespace CrosshairManager.TestSupport;

public static class DpiAwareness
{
    private static readonly DPI_AWARENESS_CONTEXT PerMonitorAwareV2 = new((nint)(-4));

    public static void EnsurePerMonitorV2() => PInvoke.SetProcessDpiAwarenessContext(PerMonitorAwareV2);

    public static bool IsThreadPerMonitorV2() =>
        PInvoke.AreDpiAwarenessContextsEqual(PInvoke.GetThreadDpiAwarenessContext(), PerMonitorAwareV2);
}
