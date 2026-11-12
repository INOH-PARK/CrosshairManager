using System.Collections;
using System.Diagnostics;
using CrosshairManager.Core.Geometry;
using CrosshairManager.Core.Model;
using CrosshairManager.Core.Render;
using CrosshairManager.Core.Settings;
using CrosshairManager.Platform.Capture;
using CrosshairManager.Platform.Monitors;

namespace CrosshairManager.Cli;

/// <summary>
/// Verifies what the running overlay draws: a #FF00FF backdrop (canvas + 8 px per side, like BackdropWindow.Around) is placed
/// under the overlay, brought to the foreground, and the screen is compared with the Core render composited over it.
/// SCREEN: creates a window and captures pixels.
/// </summary>
internal static class VerifyOverlayCommand
{
    private const int Margin = 8;
    private static readonly Rgb Magenta = new(0xFF, 0x00, 0xFF);

    public static int Run(Options o, TextWriter output, IDictionary env)
    {
        o.ExpectPositionals(0);
        var timeout = TimeSpan.FromSeconds(o.Int("--timeout", 5, 1, 300));
        var expectHidden = o.Flag("--expect-hidden");
        var dataDir = DataDirectory.Resolve(Shared.DataDirArgs(o), env);
        var outDir = Path.GetFullPath(o.Value("--out") ?? "verify-out");

        // Only read settings that exist; a missing file must not be created by a verifier.
        var settings = File.Exists(Path.Combine(dataDir, "settings.json")) ? new SettingsStore(dataDir).Load().Settings : AppSettings.Default;
        var preset = Shared.FromStore(o.Value("--preset") ?? settings.ActivePresetId, dataDir);

        using var service = new MonitorService();
        var monitor = o.Value("--monitor") is { } device
            ? Shared.FindMonitor(service, device)
            : service.Resolve(settings.Monitor, out var fellBack);
        var b = monitor.Bounds;
        var spec = preset.Crosshair.Normalize().Spec;
        var g = Shared.Layout(spec, b.W % 2 != 0, b.H % 2 != 0);
        var (window, region) = g.CanvasW > 0 ? OverlayPlacement.Compute(g, b, spec.OffsetX, spec.OffsetY) : (default, default);
        var px = b.X + (b.W / 2) + spec.OffsetX;
        var py = b.Y + (b.H / 2) + spec.OffsetY;

        var hidden = expectHidden || window.IsEmpty;
        var capture = window;
        if (capture.IsEmpty)
        {
            var x0 = Math.Max(px - 4, b.X);
            var y0 = Math.Max(py - 4, b.Y);
            var x1 = Math.Min(px + 4, b.X + b.W);
            var y1 = Math.Min(py + 4, b.Y + b.H);
            capture = new IntRect(x0, y0, x1 - x0, y1 - y0);
            if (capture.IsEmpty)
            {
                throw new UsageException($"crosshair center ({px},{py}) is outside monitor {monitor.DeviceName}; nothing on screen to verify", showUsage: false);
            }
        }

        var expected = hidden
            ? PixelOps.Solid(capture.W, capture.H, Magenta)
            : PixelOps.Over(CrosshairRasterizer.Render(g, region), Magenta);
        var tolerance = spec.RotationDeg % 90 == 0 ? 0 : 2;
        output.WriteLine($"verify-overlay: preset {preset.Id}, monitor {monitor.DeviceName}, window {capture.X},{capture.Y} {capture.W}x{capture.H}, " +
                         $"{(hidden ? "expecting hidden" : "expecting visible")}, tolerance {tolerance}");

        var backdropRect = new IntRect(capture.X - Margin, capture.Y - Margin, capture.W + (2 * Margin), capture.H + (2 * Margin));
        using var backdrop = new Backdrop(backdropRect, Magenta);
        var foreground = backdrop.BringToFront();
        var probeX = px >= backdropRect.X && px < backdropRect.X + backdropRect.W ? px : capture.X + (capture.W / 2);
        var probeY = py >= backdropRect.Y && py < backdropRect.Y + backdropRect.H ? py : capture.Y + (capture.H / 2);
        var at = ScreenProbe.WindowAt(probeX, probeY);
        if (at != backdrop.Handle)
        {
            output.WriteLine($"FAIL: window at ({probeX},{probeY}) is 0x{at:X}, not the backdrop 0x{backdrop.Handle:X} (backdrop foreground: {foreground})");
            return 1;
        }

        // Every capture attempt starts with DwmFlush (inside ScreenProbe.Capture), so retries are paced by the compositor.
        var watch = Stopwatch.StartNew();
        PixelBuffer actual;
        PixelOps.Difference diff;
        do
        {
            actual = ScreenProbe.Capture(capture);
            diff = PixelOps.Diff(expected, actual, tolerance);
        }
        while (diff.Count != 0 && watch.Elapsed < timeout);

        Shared.WritePng(Path.Combine(outDir, "verify-expected.png"), expected);
        Shared.WritePng(Path.Combine(outDir, "verify-actual.png"), actual);
        if (diff.Count == 0)
        {
            output.WriteLine($"OK: screen matches the expected {(hidden ? "backdrop-only" : "composited overlay")} pixels (PNGs in {outDir})");
            return 0;
        }

        output.WriteLine("MISMATCH: " + PixelOps.Summarize(diff) + $" (PNGs in {outDir})");
        return 1;
    }
}
