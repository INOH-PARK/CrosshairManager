using System.IO;
using CrosshairManager.Core.Geometry;
using CrosshairManager.Core.Model;
using CrosshairManager.Core.Render;
using CrosshairManager.Platform.Capture;
using CrosshairManager.Platform.Monitors;
using CrosshairManager.Platform.Overlay;
using CrosshairManager.TestSupport;

namespace CrosshairManager.Desktop.Tests.EndToEnd;

/// <summary>A rendered overlay frame: pixels for <see cref="Window"/> and the screen pixel at the crosshair center P.</summary>
internal sealed record OverlayFrame(PixelBuffer Pixels, IntRect Window, int CenterX, int CenterY);

/// <summary>
/// End-to-end helpers: the fixed processing order (Normalize -> ApplyScale -> CrosshairLayout.Compute ->
/// CrosshairRasterizer.Render -> OverlayPlacement.Compute), expected-screen composition on the backdrop color,
/// capture comparison under the capture rule and the expected.png/actual.png pairs in TestResults/desktop/&lt;case&gt;/.
/// </summary>
internal static class EndToEndHarness
{
    public static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan CaptureTimeout = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Runs the processing order for <paramref name="spec"/> on <paramref name="monitor"/> (physical pixels).
    /// Returns null when nothing is visible (empty canvas, e.g. all parts off, or placed fully off the monitor).
    /// </summary>
    public static OverlayFrame? Prepare(CrosshairSpec spec, IntRect monitor)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var (normalized, _) = spec.Normalize();
        var scaled = normalized.ApplyScale();
        var geometry = CrosshairLayout.Compute(scaled, monitor.W % 2 != 0, monitor.H % 2 != 0);
        if (geometry.CanvasW <= 0 || geometry.CanvasH <= 0)
        {
            return null;
        }

        var (window, region) = OverlayPlacement.Compute(geometry, monitor, scaled.OffsetX, scaled.OffsetY);
        if (window.IsEmpty)
        {
            return null;
        }

        var pixels = CrosshairRasterizer.Render(geometry, region);
        return new OverlayFrame(
            pixels,
            window,
            monitor.X + (monitor.W / 2) + scaled.OffsetX,
            monitor.Y + (monitor.H / 2) + scaled.OffsetY);
    }

    /// <summary>Like <see cref="Prepare"/> but fails the test when the frame is hidden.</summary>
    public static OverlayFrame PrepareVisible(CrosshairSpec spec, IntRect monitor) =>
        Prepare(spec, monitor) ?? throw new InvalidOperationException(
            $"The crosshair renders nothing on monitor {monitor}; the end-to-end case needs visible pixels.");

    /// <summary>The backdrop rectangle used for a frame: overlay window plus <see cref="BackdropWindow.Margin"/> per side.</summary>
    public static IntRect CaptureRect(OverlayFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var m = BackdropWindow.Margin;
        return new IntRect(frame.Window.X - m, frame.Window.Y - m, frame.Window.W + (2 * m), frame.Window.H + (2 * m));
    }

    /// <summary>Opaque buffer of <paramref name="rect"/>'s size filled with <paramref name="color"/>.</summary>
    public static PixelBuffer Solid(IntRect rect, Rgb color)
    {
        var buffer = new PixelBuffer(rect.W, rect.H);
        Fill(buffer, rect, rect, color);
        return buffer;
    }

    /// <summary>Fills the part of screen rectangle <paramref name="area"/> inside <paramref name="capture"/> with an opaque color.</summary>
    public static void Fill(PixelBuffer target, IntRect capture, IntRect area, Rgb color)
    {
        ArgumentNullException.ThrowIfNull(target);
        var pixel = new PixelBuffer.Pixel(color.B, color.G, color.R, 255);
        for (var y = Math.Max(area.Y, capture.Y); y < Math.Min(area.Y + area.H, capture.Y + capture.H); y++)
        {
            for (var x = Math.Max(area.X, capture.X); x < Math.Min(area.X + area.W, capture.X + capture.W); x++)
            {
                target.SetPixel(x - capture.X, y - capture.Y, pixel);
            }
        }
    }

    /// <summary>
    /// Expected screen pixels for <paramref name="capture"/>: <paramref name="outer"/> everywhere, the overlay window
    /// filled with <paramref name="under"/> and the frame composited (source-over, <see cref="Composite.Over"/>) onto it.
    /// </summary>
    public static PixelBuffer Expected(IntRect capture, Rgb outer, OverlayFrame frame, Rgb under)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var expected = Solid(capture, outer);
        var composited = Composite.Over(frame.Pixels, under);
        for (var y = 0; y < composited.Height; y++)
        {
            for (var x = 0; x < composited.Width; x++)
            {
                var sx = frame.Window.X + x - capture.X;
                var sy = frame.Window.Y + y - capture.Y;
                if (sx >= 0 && sy >= 0 && sx < capture.W && sy < capture.H)
                {
                    expected.SetPixel(sx, sy, composited.GetPixel(x, y));
                }
            }
        }

        return expected;
    }

    /// <summary>Per-channel comparison (B, G, R); alpha is ignored because captures are always opaque.</summary>
    public static (int Mismatches, int MaxDiff, string First) Diff(PixelBuffer expected, PixelBuffer actual, int tolerance)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);
        if (expected.Width != actual.Width || expected.Height != actual.Height)
        {
            return (int.MaxValue, int.MaxValue,
                $"size {actual.Width}x{actual.Height} != expected {expected.Width}x{expected.Height}");
        }

        var mismatches = 0;
        var maxDiff = 0;
        var first = string.Empty;
        for (var y = 0; y < expected.Height; y++)
        {
            for (var x = 0; x < expected.Width; x++)
            {
                var e = expected.GetPixel(x, y);
                var a = actual.GetPixel(x, y);
                var d = Math.Max(Math.Abs(e.B - a.B), Math.Max(Math.Abs(e.G - a.G), Math.Abs(e.R - a.R)));
                maxDiff = Math.Max(maxDiff, d);
                if (d > tolerance)
                {
                    if (mismatches == 0)
                    {
                        first = $"({x},{y}) expected {e} actual {a}";
                    }

                    mismatches++;
                }
            }
        }

        return (mismatches, maxDiff, first);
    }

    /// <summary>
    /// Capture rule: Eventually (DwmFlush before every attempt, 2 s bound) captures <paramref name="capture"/> until it
    /// matches <paramref name="expected"/> within <paramref name="tolerance"/>; always saves the PNG pair, then fails
    /// with the mismatch summary when it never matched.
    /// </summary>
    public static void AssertScreenMatches(string caseName, IntRect capture, PixelBuffer expected, int tolerance)
    {
        PixelBuffer? last = null;
        var matched = Eventually.True(() =>
        {
            last = ScreenProbe.Capture(capture);
            return Diff(expected, last, tolerance).Mismatches == 0;
        }, CaptureTimeout);

        var actual = last ?? throw new InvalidOperationException("Eventually made no capture attempt.");
        var dir = SavePair(caseName, expected, actual);
        if (!matched)
        {
            var (mismatches, maxDiff, first) = Diff(expected, actual, tolerance);
            Assert.Fail($"[{caseName}] capture {capture} differs from expected: {mismatches} pixel(s) over tolerance " +
                        $"{tolerance}, max channel diff {maxDiff}, first {first}. PNGs: {dir}");
        }
    }

    /// <summary>Writes expected.png and actual.png to TestResults/desktop/&lt;case&gt;/ under the repository root.</summary>
    public static string SavePair(string caseName, PixelBuffer expected, PixelBuffer actual)
    {
        ArgumentNullException.ThrowIfNull(caseName);
        var dir = Path.Combine(RepositoryRoot(), "TestResults", "desktop", caseName);
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, "expected.png"), Png.Encode(expected));
        File.WriteAllBytes(Path.Combine(dir, "actual.png"), Png.Encode(actual));
        return dir;
    }

    /// <summary>Nearest ancestor of the test binaries that contains CrosshairManager.sln.</summary>
    public static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "CrosshairManager.sln")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException($"CrosshairManager.sln not found above {AppContext.BaseDirectory}.");
    }

    /// <summary>Creates a host, Show() then Update(frame), waiting for Updated (subscribed first); asserts it is visible.</summary>
    public static async Task<OverlayHost> ShowAsync(OverlayFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var host = new OverlayHost();
        try
        {
            var updated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            host.Updated += (_, _) => updated.TrySetResult();
            host.Show();
            host.Update(frame.Pixels, frame.Window);
            await updated.Task.WaitAsync(EventTimeout);
            Assert.True(host.IsVisible, "overlay is not visible after Show() and a non-empty Update");
            return host;
        }
        catch
        {
            host.Dispose();
            throw;
        }
    }

    public static MonitorInfo PrimaryMonitor()
    {
        using var service = new MonitorService();
        return service.GetMonitors().First(m => m.IsPrimary);
    }

    /// <summary>First non-primary monitor in enumeration order; fails with a message on a single-monitor system.</summary>
    public static MonitorInfo FirstNonPrimaryMonitor()
    {
        using var service = new MonitorService();
        var monitors = service.GetMonitors();
        var other = monitors.FirstOrDefault(m => !m.IsPrimary);
        if (other is null)
        {
            Assert.Fail($"A second monitor is required but only {monitors.Count} monitor(s) are connected.");
        }

        return other;
    }
}
