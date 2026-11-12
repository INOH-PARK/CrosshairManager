using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;
using CrosshairManager.Core.Geometry;
using CrosshairManager.Core.Model;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace CrosshairManager.Platform.Capture;

/// <summary>
/// Solid-color, non-topmost popup on its own thread "Backdrop" with a message loop. Used only by the CLI verifier
/// and tests as a clean background for captures; the App must never reference it.
/// </summary>
public sealed unsafe class Backdrop : IDisposable
{
    public const string WindowClassName = "CrosshairManager.Backdrop";

    private const uint WmInvoke = PInvoke.WM_APP + 1;
    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(5);
    private static readonly WNDPROC WndProcDelegate = WndProc;
    private static readonly ConcurrentDictionary<nint, Backdrop> Windows = new();
    private static readonly object ClassLock = new();
    private static int s_classRefs;

    private readonly ConcurrentQueue<(Action Action, TaskCompletionSource Done)> _work = new();
    private readonly ManualResetEventSlim _ready = new();
    private readonly Thread _thread;
    private readonly IntRect _rect;
    private readonly Rgb _color;
    private Exception? _startupError;
    private HWND _hwnd;
    private HBRUSH _brush;
    private uint _threadId;
    private int _disposed;

    /// <summary>Creates and shows (without activating) the popup at <paramref name="rect"/> (physical pixels).</summary>
    public Backdrop(IntRect rect, Rgb color)
    {
        if (rect.IsEmpty)
        {
            throw new ArgumentException($"Backdrop rectangle {rect} is empty.", nameof(rect));
        }

        _rect = rect;
        _color = color;
        _thread = new Thread(ThreadMain) { IsBackground = true, Name = "Backdrop" };
        _thread.Start();
        if (!_ready.Wait(CallTimeout))
        {
            throw new TimeoutException("Backdrop thread did not start within 5 seconds.");
        }

        if (_startupError is not null)
        {
            _thread.Join(CallTimeout);
            throw new InvalidOperationException("Backdrop window creation failed.", _startupError);
        }
    }

    public nint Handle => (nint)_hwnd.Value;

    /// <summary>
    /// Must have foreground rule: SetForegroundWindow first; if the backdrop is not the foreground window, attach to
    /// the current foreground window's thread, SetWindowPos(HWND_TOP), SetForegroundWindow, detach. Runs on the
    /// backdrop thread. Returns whether the backdrop is the foreground window afterwards.
    /// </summary>
    public bool BringToFront()
    {
        var result = false;
        Invoke(() =>
        {
            PInvoke.SetForegroundWindow(_hwnd);
            if (PInvoke.GetForegroundWindow() == _hwnd)
            {
                result = true;
                return;
            }

            var foreground = PInvoke.GetForegroundWindow();
            var foregroundThread = foreground.IsNull ? 0 : PInvoke.GetWindowThreadProcessId(foreground, null);
            var self = PInvoke.GetCurrentThreadId();
            var attached = foregroundThread != 0 && foregroundThread != self &&
                           PInvoke.AttachThreadInput(self, foregroundThread, true);
            try
            {
                PInvoke.SetWindowPos(_hwnd, HWND.HWND_TOP, 0, 0, 0, 0,
                    SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW);
                PInvoke.SetForegroundWindow(_hwnd);
            }
            finally
            {
                if (attached)
                {
                    PInvoke.AttachThreadInput(self, foregroundThread, false);
                }
            }

            result = PInvoke.GetForegroundWindow() == _hwnd;
        });
        return result;
    }

    /// <summary>Posts WM_QUIT to the backdrop thread and waits for it to exit (5 s cap). Idempotent.</summary>
    public void Dispose()
    {
        if (Environment.CurrentManagedThreadId == _thread.ManagedThreadId)
        {
            throw new InvalidOperationException("Backdrop cannot be disposed from its own thread.");
        }

        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (!PInvoke.PostThreadMessage(_threadId, PInvoke.WM_QUIT, 0, 0))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "PostThreadMessage(WM_QUIT) to the backdrop thread failed.");
        }

        if (!_thread.Join(CallTimeout))
        {
            throw new TimeoutException("Backdrop thread did not exit within 5 seconds.");
        }

        _ready.Dispose();
    }

    private void Invoke(Action action)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _work.Enqueue((action, done));
        if (!PInvoke.PostMessage(_hwnd, WmInvoke, 0, 0))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "PostMessage to the backdrop window failed.");
        }

        done.Task.WaitAsync(CallTimeout).GetAwaiter().GetResult();
    }

    private void ThreadMain()
    {
        var classHeld = false;
        try
        {
            _threadId = PInvoke.GetCurrentThreadId();
            var hInstance = ModuleInstance();
            AcquireClass(hInstance);
            classHeld = true;
            _brush = PInvoke.CreateSolidBrush(new COLORREF((uint)(_color.R | (_color.G << 8) | (_color.B << 16))));
            if (_brush.IsNull)
            {
                throw new Win32Exception("CreateSolidBrush failed.");
            }

            fixed (char* className = WindowClassName)
            fixed (char* title = "CrosshairManager Backdrop")
            {
                _hwnd = PInvoke.CreateWindowEx(WINDOW_EX_STYLE.WS_EX_TOOLWINDOW, className, title, WINDOW_STYLE.WS_POPUP,
                    _rect.X, _rect.Y, _rect.W, _rect.H, HWND.Null, HMENU.Null, hInstance, null);
                if (_hwnd.IsNull)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Creating the backdrop window failed.");
                }
            }

            Windows[(nint)_hwnd.Value] = this;
            PInvoke.ShowWindow(_hwnd, SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE);
        }
        catch (Exception ex)
        {
            _startupError = ex;
            TearDown(classHeld);
            _ready.Set();
            return;
        }

        _ready.Set();
        while (PInvoke.GetMessage(out var msg, HWND.Null, 0, 0))
        {
            PInvoke.TranslateMessage(msg);
            PInvoke.DispatchMessage(msg);
        }

        TearDown(classHeld);
    }

    private void TearDown(bool classHeld)
    {
        if (!_hwnd.IsNull)
        {
            Windows.TryRemove((nint)_hwnd.Value, out _);
            PInvoke.DestroyWindow(_hwnd);
        }

        if (!_brush.IsNull)
        {
            PInvoke.DeleteObject(_brush);
        }

        if (classHeld)
        {
            ReleaseClass(ModuleInstance());
        }

        while (_work.TryDequeue(out var item))
        {
            item.Done.TrySetException(new ObjectDisposedException(nameof(Backdrop)));
        }
    }

    private void DrainWork()
    {
        while (_work.TryDequeue(out var item))
        {
            try
            {
                item.Action();
                item.Done.TrySetResult();
            }
            catch (Exception ex)
            {
                item.Done.TrySetException(ex);
            }
        }
    }

    private void Paint(HWND hwnd)
    {
        var paint = default(PAINTSTRUCT);
        var dc = PInvoke.BeginPaint(hwnd, &paint);
        if (!dc.IsNull)
        {
            var client = new RECT(0, 0, _rect.W, _rect.H);
            PInvoke.FillRect(dc, &client, _brush);
        }

        PInvoke.EndPaint(hwnd, &paint);
    }

    private static LRESULT WndProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        if (Windows.TryGetValue((nint)hwnd.Value, out var backdrop))
        {
            switch (msg)
            {
                case WmInvoke:
                    backdrop.DrainWork();
                    return (LRESULT)0;
                case PInvoke.WM_ERASEBKGND:
                    return (LRESULT)1; // WM_PAINT fills the whole client area
                case PInvoke.WM_PAINT:
                    backdrop.Paint(hwnd);
                    return (LRESULT)0;
            }
        }

        return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private static HINSTANCE ModuleInstance() => new(PInvoke.GetModuleHandle((PCWSTR)null).Value);

    private static void AcquireClass(HINSTANCE hInstance)
    {
        lock (ClassLock)
        {
            if (s_classRefs == 0)
            {
                fixed (char* className = WindowClassName)
                {
                    var wc = new WNDCLASSEXW
                    {
                        cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
                        lpfnWndProc = WndProcDelegate,
                        hInstance = hInstance,
                        lpszClassName = className,
                    };
                    if (PInvoke.RegisterClassEx(wc) == 0)
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "RegisterClassEx(CrosshairManager.Backdrop) failed.");
                    }
                }
            }

            s_classRefs++;
        }
    }

    private static void ReleaseClass(HINSTANCE hInstance)
    {
        lock (ClassLock)
        {
            if (--s_classRefs == 0)
            {
                fixed (char* className = WindowClassName)
                {
                    PInvoke.UnregisterClass(className, hInstance);
                }
            }
        }
    }
}
