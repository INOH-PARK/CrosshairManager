using System.Runtime.InteropServices;

namespace CrosshairManager.Desktop.Tests.Overlay;

/// <summary>Test-only Win32 used to put a covering window into the topmost band above the overlay.</summary>
internal static class TopmostKeeperNative
{
    private static readonly nint HwndTopmost = -1;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int cx, int cy, uint flags);

    /// <summary>Moves <paramref name="hwnd"/> to the top of the topmost band without activating it.</summary>
    public static void MakeTopmost(nint hwnd)
    {
        if (!SetWindowPos(hwnd, HwndTopmost, 0, 0, 0, 0, SwpNoSize | SwpNoMove | SwpNoActivate | SwpShowWindow))
        {
            throw new InvalidOperationException($"SetWindowPos(HWND_TOPMOST) failed: {Marshal.GetLastWin32Error()}");
        }
    }
}
