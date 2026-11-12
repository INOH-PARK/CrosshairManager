using System.Runtime.InteropServices;
using CrosshairManager.Core.Settings;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace CrosshairManager.TestSupport;

public static unsafe class Input
{
    /// <summary>Presses modifiers (Ctrl, Alt, Shift, Win), then the key, then releases in reverse order via SendInput.</summary>
    public static void SendChord(HotkeyGesture gesture)
    {
        ArgumentNullException.ThrowIfNull(gesture);
        var keys = new List<ushort>();
        if ((gesture.Modifiers & HotkeyGesture.ModControl) != 0) keys.Add(0x11);
        if ((gesture.Modifiers & HotkeyGesture.ModAlt) != 0) keys.Add(0x12);
        if ((gesture.Modifiers & HotkeyGesture.ModShift) != 0) keys.Add(0x10);
        if ((gesture.Modifiers & HotkeyGesture.ModWin) != 0) keys.Add(0x5B);
        keys.Add((ushort)gesture.VirtualKey);

        var inputs = new List<INPUT>();
        foreach (var k in keys)
        {
            inputs.Add(Make(k, up: false));
        }

        for (var i = keys.Count - 1; i >= 0; i--)
        {
            inputs.Add(Make(keys[i], up: true));
        }

        var sent = PInvoke.SendInput(inputs.ToArray(), Marshal.SizeOf<INPUT>());
        if (sent != inputs.Count)
        {
            throw new InvalidOperationException($"SendInput sent {sent} of {inputs.Count} events (Win32 error {Marshal.GetLastWin32Error()}).");
        }
    }

    public static nint GetForegroundHandle() => (nint)(PInvoke.GetForegroundWindow().Value);

    /// <summary>Brings a window of the calling process to the foreground (AttachThreadInput trick).</summary>
    public static bool ForceForeground(nint hwnd)
    {
        // A synthesized Alt tap lets the process through the foreground lock.
        PInvoke.SendInput([Make(0x12, up: false), Make(0x12, up: true)], Marshal.SizeOf<INPUT>());
        var target = new Windows.Win32.Foundation.HWND(hwnd);
        var fg = PInvoke.GetForegroundWindow();
        var fgThread = PInvoke.GetWindowThreadProcessId(fg, null);
        var self = PInvoke.GetCurrentThreadId();
        var attached = fgThread != 0 && fgThread != self && PInvoke.AttachThreadInput(self, fgThread, true);
        try
        {
            PInvoke.SetWindowPos(target, Windows.Win32.Foundation.HWND.HWND_TOP, 0, 0, 0, 0,
                Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOMOVE |
                Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOSIZE |
                Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW);
            return PInvoke.SetForegroundWindow(target);
        }
        finally
        {
            if (attached)
            {
                PInvoke.AttachThreadInput(self, fgThread, false);
            }
        }
    }

    public static void FlushDwm() => PInvoke.DwmFlush();

    private static bool IsExtended(ushort vk) => vk is 0x21 or 0x22 or 0x23 or 0x24 or 0x25 or 0x26 or 0x27 or 0x28 or 0x2D or 0x2E or 0x5B;

    private static INPUT Make(ushort vk, bool up)
    {
        var flags = (up ? KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP : 0) | (IsExtended(vk) ? KEYBD_EVENT_FLAGS.KEYEVENTF_EXTENDEDKEY : 0);
        return new INPUT
        {
            type = INPUT_TYPE.INPUT_KEYBOARD,
            Anonymous = { ki = new KEYBDINPUT { wVk = (VIRTUAL_KEY)vk, dwFlags = flags } },
        };
    }
}
