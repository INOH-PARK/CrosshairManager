using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using CrosshairManager.Core.Geometry;
using CrosshairManager.Core.Render;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace CrosshairManager.Platform.Capture;

/// <summary>
/// Screen reads for the CLI verifier and tests only. The App must never reference this type (Must NOT have).
/// </summary>
public static unsafe class ScreenProbe
{
    private const uint BiRgb = 0;

    /// <summary>
    /// DwmFlush, then copies the screen rectangle (physical pixels) with GetDC(NULL) + BitBlt SRCCOPY|CAPTUREBLT
    /// into a 32bpp top-down DIB. Alpha is forced to 255.
    /// </summary>
    public static PixelBuffer Capture(IntRect rect)
    {
        if (rect.IsEmpty)
        {
            throw new ArgumentException($"Capture rectangle {rect} is empty.", nameof(rect));
        }

        PInvoke.DwmFlush();
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
                    var previous = PInvoke.SelectObject(memory, bitmap);
                    try
                    {
                        if (!PInvoke.BitBlt(memory, 0, 0, rect.W, rect.H, screen, rect.X, rect.Y,
                                ROP_CODE.SRCCOPY | ROP_CODE.CAPTUREBLT))
                        {
                            throw new Win32Exception(Marshal.GetLastWin32Error(), "BitBlt from the screen failed.");
                        }
                    }
                    finally
                    {
                        PInvoke.SelectObject(memory, previous);
                    }

                    var buffer = new PixelBuffer(rect.W, rect.H);
                    var source = (byte*)bits;
                    for (var y = 0; y < rect.H; y++)
                    {
                        for (var x = 0; x < rect.W; x++)
                        {
                            var o = (y * rect.W + x) * 4;
                            buffer.SetPixel(x, y, new PixelBuffer.Pixel(source[o], source[o + 1], source[o + 2], 255));
                        }
                    }

                    return buffer;
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

    /// <summary>
    /// Top-level window that receives hit tests at the screen point (WindowFromPoint, then GA_ROOT). Click-through
    /// layered windows such as the overlay are skipped by the system. 0 when there is none.
    /// </summary>
    public static nint WindowAt(int x, int y)
    {
        var hit = PInvoke.WindowFromPoint(new Point(x, y));
        if (hit.IsNull)
        {
            return 0;
        }

        var root = PInvoke.GetAncestor(hit, GET_ANCESTOR_FLAGS.GA_ROOT);
        return (nint)(root.IsNull ? hit : root).Value;
    }
}
