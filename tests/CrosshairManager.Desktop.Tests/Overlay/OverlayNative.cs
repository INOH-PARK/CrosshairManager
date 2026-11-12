using System.Runtime.InteropServices;

namespace CrosshairManager.Desktop.Tests.Overlay;

/// <summary>Read-only Win32 probes the overlay tests use to observe the real window, independent of OverlayHost.</summary>
internal static class OverlayNative
{
    public const int GwlExStyle = -20;
    public const uint WsExTopmost = 0x00000008;
    public const uint WsExTransparent = 0x00000020;
    public const uint WsExToolWindow = 0x00000080;
    public const uint WsExLayered = 0x00080000;
    public const uint WsExNoActivate = 0x08000000;

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(nint hwnd, out Rect rect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindow(nint hwnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindowVisible(nint hwnd);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    public static extern int GetWindowLong(nint hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "GetClassNameW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetClassName(nint hwnd, char[] buffer, int maxCount);

    [DllImport("user32.dll")]
    public static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern nint GetParent(nint hwnd);

    [DllImport("dwmapi.dll")]
    public static extern int DwmFlush();

    public static string ClassNameOf(nint hwnd)
    {
        var buffer = new char[256];
        var length = GetClassName(hwnd, buffer, buffer.Length);
        return new string(buffer, 0, length);
    }
}
