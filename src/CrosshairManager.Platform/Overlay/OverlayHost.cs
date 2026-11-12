using System.Collections.Concurrent;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using CrosshairManager.Core.Geometry;
using CrosshairManager.Core.Render;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace CrosshairManager.Platform.Overlay;

/// <summary>
/// Click-through, non-activating, topmost layered window owned by a dedicated background thread "Overlay"
/// with its own message loop. Pixels are pushed only when <see cref="Update"/> is called (no timer, no render
/// loop); the window is exactly as large as the canvas. Calls are marshalled to the overlay thread through a
/// message-only controller window (WM_APP+1 + work queue) and block until done (5 s cap).
/// <see cref="Updated"/> and <see cref="VisibilityChanged"/> are raised on the overlay thread; handlers must not
/// block on the caller's thread (use BeginInvoke/Post to get back to a UI thread).
/// </summary>
public sealed unsafe class OverlayHost : IDisposable
{
    public const string WindowClassName = "CrosshairManager.Overlay";

    private const uint WmInvoke = PInvoke.WM_APP + 1;
    private const uint BiRgb = 0;

    private const WINDOW_EX_STYLE OverlayExStyle =
        WINDOW_EX_STYLE.WS_EX_LAYERED | WINDOW_EX_STYLE.WS_EX_TRANSPARENT | WINDOW_EX_STYLE.WS_EX_TOPMOST |
        WINDOW_EX_STYLE.WS_EX_TOOLWINDOW | WINDOW_EX_STYLE.WS_EX_NOACTIVATE;

    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(5);
    private static readonly HWND HwndMessage = new(-3);
    private static readonly WNDPROC WndProcDelegate = WndProc;
    private static readonly ConcurrentDictionary<nint, OverlayHost> Controllers = new();
    private static readonly object ClassLock = new();
    private static int s_classRefs;

    private readonly ConcurrentQueue<(Action Action, TaskCompletionSource Done)> _work = new();
    private readonly ManualResetEventSlim _ready = new();
    private readonly Thread _thread;
    private Exception? _startupError;
    private HWND _overlay;
    private HWND _controller;
    private uint _threadId;
    private bool _wantVisible;
    private bool _hasContent;
    private volatile bool _isVisible;
    private int _disposed;

    public OverlayHost()
    {
        _thread = new Thread(ThreadMain) { IsBackground = true, Name = "Overlay" };
        _thread.Start();
        if (!_ready.Wait(CallTimeout))
        {
            throw new TimeoutException("Overlay thread did not start within 5 seconds.");
        }

        if (_startupError is not null)
        {
            _thread.Join(CallTimeout);
            throw new InvalidOperationException("Overlay window creation failed.", _startupError);
        }
    }

    /// <summary>Raised on the overlay thread after every <see cref="Update"/> has been applied.</summary>
    public event EventHandler? Updated;

    /// <summary>Raised on the overlay thread when the window's actual visibility changes.</summary>
    public event EventHandler<bool>? VisibilityChanged;

    /// <summary>Overlay HWND.</summary>
    public nint Handle => (nint)_overlay.Value;

    /// <summary>Message-only controller HWND that receives WM_APP+1 work notifications.</summary>
    public nint ControllerHandle => (nint)_controller.Value;

    /// <summary>True when the overlay window is shown: <see cref="Show"/> was requested and it has content.</summary>
    public bool IsVisible => _isVisible;

    /// <summary>Runs <paramref name="action"/> on the overlay thread and waits (5 s cap) for it to finish.</summary>
    public void Invoke(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (Environment.CurrentManagedThreadId == _thread.ManagedThreadId)
        {
            action();
            return;
        }

        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _work.Enqueue((action, done));
        if (!PInvoke.PostMessage(_controller, WmInvoke, 0, 0))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "PostMessage to the overlay controller failed.");
        }

        done.Task.WaitAsync(CallTimeout).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Pushes premultiplied BGRA pixels to the screen rectangle (physical pixels) with UpdateLayeredWindow.
    /// A null or fully transparent buffer, or an empty rectangle, hides the window.
    /// </summary>
    public void Update(PixelBuffer? buffer, IntRect screenRect)
    {
        if (buffer is not null && !screenRect.IsEmpty && (buffer.Width != screenRect.W || buffer.Height != screenRect.H))
        {
            throw new ArgumentException(
                $"Buffer is {buffer.Width}x{buffer.Height} but the rectangle is {screenRect.W}x{screenRect.H}.",
                nameof(screenRect));
        }

        var pixels = buffer is null || screenRect.IsEmpty || IsFullyTransparent(buffer) ? null : buffer.Bytes.ToArray();
        Invoke(() =>
        {
            if (pixels is null)
            {
                _hasContent = false;
            }
            else
            {
                Paint(pixels, screenRect);
                _hasContent = true;
            }

            ApplyVisibility();
            Updated?.Invoke(this, EventArgs.Empty);
        });
    }

    /// <summary>Requests visibility (SW_SHOWNOACTIVATE; never takes focus). Shown once it has content.</summary>
    public void Show() => Invoke(() =>
    {
        _wantVisible = true;
        ApplyVisibility();
    });

    public void Hide() => Invoke(() =>
    {
        _wantVisible = false;
        ApplyVisibility();
    });

    /// <summary>Posts WM_QUIT to the overlay thread and waits for it to exit (5 s cap, else <see cref="TimeoutException"/>).</summary>
    public void Dispose()
    {
        if (Environment.CurrentManagedThreadId == _thread.ManagedThreadId)
        {
            throw new InvalidOperationException("OverlayHost cannot be disposed from its own thread.");
        }

        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (!PInvoke.PostThreadMessage(_threadId, PInvoke.WM_QUIT, 0, 0))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "PostThreadMessage(WM_QUIT) to the overlay thread failed.");
        }

        if (!_thread.Join(CallTimeout))
        {
            throw new TimeoutException("Overlay thread did not exit within 5 seconds.");
        }

        _ready.Dispose();
    }

    private static bool IsFullyTransparent(PixelBuffer buffer)
    {
        var bytes = buffer.Bytes;
        for (var i = 3; i < bytes.Length; i += 4)
        {
            if (bytes[i] != 0)
            {
                return false;
            }
        }

        return true;
    }

    private void ApplyVisibility()
    {
        var visible = _wantVisible && _hasContent;
        if (visible == _isVisible)
        {
            return;
        }

        PInvoke.ShowWindow(_overlay, visible ? SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE : SHOW_WINDOW_CMD.SW_HIDE);
        _isVisible = visible;
        VisibilityChanged?.Invoke(this, visible);
    }

    private void Paint(byte[] pixels, IntRect rect)
    {
        var info = new BITMAPINFO
        {
            bmiHeader = new BITMAPINFOHEADER
            {
                biSize = (uint)sizeof(BITMAPINFOHEADER),
                biWidth = rect.W,
                biHeight = -rect.H, // top-down
                biPlanes = 1,
                biBitCount = 32,
                biCompression = BiRgb,
            },
        };

        var screen = PInvoke.GetDC(HWND.Null);
        if (screen.IsNull)
        {
            throw new Win32Exception("GetDC(NULL) failed.");
        }

        try
        {
            var memory = PInvoke.CreateCompatibleDC(screen);
            if (memory.IsNull)
            {
                throw new Win32Exception("CreateCompatibleDC failed.");
            }

            try
            {
                void* bits;
                var bitmap = PInvoke.CreateDIBSection(memory, &info, DIB_USAGE.DIB_RGB_COLORS, &bits, HANDLE.Null, 0);
                if (bitmap.IsNull)
                {
                    throw new Win32Exception("CreateDIBSection failed.");
                }

                try
                {
                    Marshal.Copy(pixels, 0, (nint)bits, pixels.Length);
                    var previous = PInvoke.SelectObject(memory, bitmap);
                    try
                    {
                        var destination = new Point(rect.X, rect.Y);
                        var size = new SIZE(rect.W, rect.H);
                        var source = new Point(0, 0);
                        var blend = new BLENDFUNCTION
                        {
                            BlendOp = (byte)PInvoke.AC_SRC_OVER,
                            BlendFlags = 0,
                            SourceConstantAlpha = 255,
                            AlphaFormat = (byte)PInvoke.AC_SRC_ALPHA,
                        };
                        if (!PInvoke.UpdateLayeredWindow(_overlay, screen, &destination, &size, memory, &source,
                                new COLORREF(0), &blend, UPDATE_LAYERED_WINDOW_FLAGS.ULW_ALPHA))
                        {
                            throw new Win32Exception(Marshal.GetLastWin32Error(), "UpdateLayeredWindow failed.");
                        }
                    }
                    finally
                    {
                        PInvoke.SelectObject(memory, previous);
                    }
                }
                finally
                {
                    PInvoke.DeleteObject(bitmap);
                }
            }
            finally
            {
                PInvoke.DeleteDC(memory);
            }
        }
        finally
        {
            PInvoke.ReleaseDC(HWND.Null, screen);
        }
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
            fixed (char* className = WindowClassName)
            fixed (char* title = "CrosshairManager Overlay")
            {
                _controller = PInvoke.CreateWindowEx(0, className, title, 0, 0, 0, 0, 0,
                    HwndMessage, HMENU.Null, hInstance, null);
                if (_controller.IsNull)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Creating the overlay controller window failed.");
                }

                _overlay = PInvoke.CreateWindowEx(OverlayExStyle, className, title, WINDOW_STYLE.WS_POPUP, 0, 0, 1, 1,
                    HWND.Null, HMENU.Null, hInstance, null);
                if (_overlay.IsNull)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Creating the overlay window failed.");
                }
            }

            Controllers[(nint)_controller.Value] = this;
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
        if (!_controller.IsNull)
        {
            Controllers.TryRemove((nint)_controller.Value, out _);
            PInvoke.DestroyWindow(_controller);
        }

        if (!_overlay.IsNull)
        {
            PInvoke.DestroyWindow(_overlay);
        }

        _isVisible = false;
        if (classHeld)
        {
            ReleaseClass(ModuleInstance());
        }

        while (_work.TryDequeue(out var item))
        {
            item.Done.TrySetException(new ObjectDisposedException(nameof(OverlayHost)));
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

    private static LRESULT WndProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        if (msg == WmInvoke && Controllers.TryGetValue((nint)hwnd.Value, out var host))
        {
            host.DrainWork();
            return (LRESULT)0;
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
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "RegisterClassEx(CrosshairManager.Overlay) failed.");
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
