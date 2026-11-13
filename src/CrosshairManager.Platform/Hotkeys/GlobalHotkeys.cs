using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using CrosshairManager.Core.Settings;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace CrosshairManager.Platform.Hotkeys;

/// <summary>
/// RegisterHotKey wrapper that owns a dedicated background thread with a message-only window.
/// <see cref="Pressed"/> is raised on that thread.
/// </summary>
public sealed unsafe class GlobalHotkeys : IDisposable
{
    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(5);

    private readonly ConcurrentQueue<Action> _work = new();
    private readonly Dictionary<string, int> _idToAtom = new();
    private readonly Dictionary<int, string> _atomToId = new();
    private readonly ManualResetEventSlim _ready = new();
    private readonly Thread _thread;
    private readonly string _className = "CrosshairManager.Hotkeys." + Guid.NewGuid().ToString("N");
    private readonly WNDPROC _wndProc;
    private HWND _hwnd;
    private Exception? _startupError;
    private int _nextAtom = 1;
    private int _disposed;

    public GlobalHotkeys()
    {
        _wndProc = WndProc;
        _thread = new Thread(ThreadMain) { IsBackground = true, Name = "GlobalHotkeys" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        if (!_ready.Wait(CallTimeout))
        {
            throw new TimeoutException("Hotkey thread did not start.");
        }

        if (_startupError is not null)
        {
            throw new InvalidOperationException("Hotkey window creation failed.", _startupError);
        }
    }

    /// <summary>Raised with the registered id when its hotkey is pressed (on the hotkey thread).</summary>
    public event Action<string>? Pressed;

    /// <summary>
    /// Raised on the hotkey thread with an exception a <see cref="Pressed"/> handler threw. The exception is caught there so it
    /// cannot unwind through the window procedure; without a subscriber it is written to Trace.
    /// </summary>
    public event Action<Exception>? HandlerFailed;

    /// <summary>The message-only window that owns the registrations (tests post WM_HOTKEY to it).</summary>
    internal nint WindowHandle => (nint)_hwnd.Value;

    public bool TryRegister(string id, HotkeyGesture gesture, out int win32Error)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(gesture);
        var error = 0;
        var ok = Invoke(() =>
        {
            if (_idToAtom.TryGetValue(id, out var old))
            {
                PInvoke.UnregisterHotKey(_hwnd, old);
                _idToAtom.Remove(id);
                _atomToId.Remove(old);
            }

            var atom = _nextAtom++;
            var mods = (HOT_KEY_MODIFIERS)gesture.Modifiers | HOT_KEY_MODIFIERS.MOD_NOREPEAT;
            if (!PInvoke.RegisterHotKey(_hwnd, atom, mods, gesture.VirtualKey))
            {
                error = Marshal.GetLastWin32Error();
                return false;
            }

            _idToAtom[id] = atom;
            _atomToId[atom] = id;
            return true;
        });
        win32Error = error;
        return ok;
    }

    public void Unregister(string id)
    {
        Invoke(() =>
        {
            if (_idToAtom.Remove(id, out var atom))
            {
                PInvoke.UnregisterHotKey(_hwnd, atom);
                _atomToId.Remove(atom);
            }

            return true;
        });
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        // Hotkeys belong to _hwnd: release them on its own thread before the window goes away.
        _work.Enqueue(() =>
        {
            foreach (var atom in _atomToId.Keys)
            {
                PInvoke.UnregisterHotKey(_hwnd, atom);
            }

            _atomToId.Clear();
            _idToAtom.Clear();
            PInvoke.DestroyWindow(_hwnd);
        });
        if (!PInvoke.PostMessage(_hwnd, PInvoke.WM_APP, 0, 0))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "PostMessage to the hotkey window failed; hotkeys were not released.");
        }

        if (!_thread.Join(CallTimeout))
        {
            throw new TimeoutException("Hotkey thread did not exit within 5 seconds.");
        }

        _ready.Dispose();
    }

    private bool Invoke(Func<bool> func)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        var result = false;
        Exception? error = null;
        // A TaskCompletionSource needs no disposal, so a work item that runs after a timeout cannot throw inside the window procedure.
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _work.Enqueue(() =>
        {
            // Rethrown on the calling thread below instead of unwinding through the window procedure.
            try
            {
                result = func();
            }
            catch (Exception ex)
            {
                error = ex;
            }
            finally
            {
                done.TrySetResult();
            }
        });
        if (!PInvoke.PostMessage(_hwnd, PInvoke.WM_APP, 0, 0))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "PostMessage to the hotkey window failed.");
        }

        if (!done.Task.Wait(CallTimeout))
        {
            throw new TimeoutException("Hotkey thread did not respond.");
        }

        if (error is not null)
        {
            ExceptionDispatchInfo.Throw(error);
        }

        return result;
    }

    private void ThreadMain()
    {
        var registered = false;
        try
        {
            var hInstance = ModuleInstance();
            fixed (char* name = _className)
            {
                var wc = new WNDCLASSEXW
                {
                    cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
                    lpfnWndProc = _wndProc,
                    hInstance = hInstance,
                    lpszClassName = name,
                };
                if (PInvoke.RegisterClassEx(wc) == 0)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                registered = true;
                fixed (char* title = "CrosshairManager.Hotkeys")
                {
                    _hwnd = PInvoke.CreateWindowEx(0, name, title, 0, 0, 0, 0, 0,
                        HwndMessage, HMENU.Null, hInstance, null);
                }

                if (_hwnd.IsNull)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
        }
        catch (Exception ex)
        {
            _startupError = ex;
            _ready.Set();
            if (registered)
            {
                UnregisterClassName();
            }

            return;
        }

        _ready.Set();
        while (PInvoke.GetMessage(out var msg, HWND.Null, 0, 0))
        {
            PInvoke.TranslateMessage(msg);
            PInvoke.DispatchMessage(msg);
        }

        UnregisterClassName();
    }

    private static readonly HWND HwndMessage = new(-3);

    private static HINSTANCE ModuleInstance() => new(PInvoke.GetModuleHandle((PCWSTR)null).Value);

    private void UnregisterClassName()
    {
        fixed (char* name = _className)
        {
            PInvoke.UnregisterClass(name, ModuleInstance());
        }
    }

    private bool ReportHandlerFailure(Exception ex)
    {
        var handler = HandlerFailed;
        handler?.Invoke(ex);
        return handler is not null;
    }

    private LRESULT WndProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        if (msg == PInvoke.WM_APP)
        {
            while (_work.TryDequeue(out var action))
            {
                action();
            }

            return (LRESULT)0;
        }

        if (msg == PInvoke.WM_HOTKEY)
        {
            if (_atomToId.TryGetValue((int)wParam.Value, out var id))
            {
                CallbackGuard.Run(() => Pressed?.Invoke(id), ReportHandlerFailure, "hotkey Pressed");
            }

            return (LRESULT)0;
        }

        if (msg == PInvoke.WM_DESTROY)
        {
            PInvoke.PostQuitMessage(0);
            return (LRESULT)0;
        }

        return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
    }
}
