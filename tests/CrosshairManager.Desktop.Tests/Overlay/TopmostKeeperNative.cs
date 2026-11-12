using System.Runtime.InteropServices;
using CrosshairManager.TestSupport;

namespace CrosshairManager.Desktop.Tests.Overlay;

/// <summary>Test-only Win32 used to put a covering window into the topmost band above the overlay.</summary>
internal static class TopmostKeeperNative
{
    private static readonly nint HwndTopmost = -1;
    private const int GwlExStyle = -20;
    private const long WsExTopmost = 0x00000008;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(nint hwnd, int index);

    /// <summary>
    /// Makes <paramref name="cover"/> the foreground window, then moves it to the top of the topmost band, like a
    /// foreground app that turns itself topmost. Windows can ignore HWND_TOPMOST for a window that is not foreground
    /// even though SetWindowPos returns TRUE, so WS_EX_TOPMOST is checked afterwards.
    /// </summary>
    public static void MakeTopmost(BackdropWindow cover)
    {
        if (!cover.BringToFront())
        {
            throw new InvalidOperationException("The cover did not become the foreground window.");
        }

        if (!SetWindowPos(cover.Handle, HwndTopmost, 0, 0, 0, 0, SwpNoSize | SwpNoMove | SwpNoActivate | SwpShowWindow))
        {
            throw new InvalidOperationException($"SetWindowPos(HWND_TOPMOST) failed: {Marshal.GetLastWin32Error()}");
        }

        if ((GetWindowLongPtr(cover.Handle, GwlExStyle) & WsExTopmost) == 0)
        {
            throw new InvalidOperationException("SetWindowPos(HWND_TOPMOST) returned TRUE but the cover is not topmost.");
        }
    }
}
