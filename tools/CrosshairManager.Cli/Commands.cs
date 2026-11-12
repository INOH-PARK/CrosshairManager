using System.Buffers.Binary;
using System.Collections;
using CrosshairManager.Core.Geometry;
using CrosshairManager.Core.IO;
using CrosshairManager.Core.Model;
using CrosshairManager.Core.Presets;
using CrosshairManager.Core.Render;
using CrosshairManager.Core.Settings;
using CrosshairManager.Platform.Capture;
using CrosshairManager.Platform.Monitors;

namespace CrosshairManager.Cli;

internal static class Shared
{
    /// <summary>The fixed pipeline: Normalize, ApplyScale, Layout. Parity comes from the target monitor size.</summary>
    public static CrosshairGeometry Layout(CrosshairSpec spec, bool oddWidth, bool oddHeight) =>
        CrosshairLayout.Compute(spec.Normalize().Spec.ApplyScale(), oddWidth, oddHeight);

    public static void WritePng(string path, PixelBuffer image)
    {
        var full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        AtomicFile.WriteAllBytes(full, Png.Encode(image));
    }

    public static PixelBuffer ReadPng(string path)
    {
        try
        {
            return Png.Decode(File.ReadAllBytes(path));
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            throw new UsageException($"cannot read PNG '{path}': {ex.Message}", showUsage: false);
        }
    }

    public static string[] DataDirArgs(Options o) => o.Value("--data-dir") is { } d ? ["--data-dir", d] : [];

    public static Preset FromStore(string id, string dataDir)
    {
        var entry = new PresetStore(dataDir).LoadAll().FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase))
            ?? throw new UsageException($"preset '{id}' not found in '{dataDir}'", showUsage: false);
        return entry.Preset ?? throw new UsageException($"preset '{id}' is unusable: {entry.Result}", showUsage: false);
    }

    public static MonitorInfo FindMonitor(MonitorService service, string device)
    {
        var all = service.GetMonitors();
        return all.FirstOrDefault(m => string.Equals(m.DeviceName, device, StringComparison.OrdinalIgnoreCase))
            ?? throw new UsageException($"monitor '{device}' not found (connected: {string.Join(", ", all.Select(m => m.DeviceName))})", showUsage: false);
    }
}

internal static class RenderCommand
{
    public static int Run(Options o, TextWriter output, IDictionary env)
    {
        o.ExpectPositionals(2);
        var zoom = o.Int("--zoom", 8, 1, 32);
        var bg = o.Color("--bg");
        var preset = Resolve(o.Positional[0], o, env);
        var odd = o.Flag("--odd");
        var g = Shared.Layout(preset.Crosshair, odd, odd);
        if (g.CanvasW <= 0 || g.CanvasH <= 0)
        {
            throw new UsageException($"preset '{preset.Id}' has no visible parts; nothing to render", showUsage: false);
        }

        var image = CrosshairRasterizer.Render(g);
        if (bg is { } color)
        {
            image = PixelOps.Over(image, color);
        }

        image = PixelOps.Zoom(image, zoom);
        Shared.WritePng(o.Positional[1], image);
        output.WriteLine($"wrote {o.Positional[1]} ({image.Width}x{image.Height}, zoom {zoom})");
        return 0;
    }

    private static Preset Resolve(string idOrPath, Options o, IDictionary env)
    {
        if (File.Exists(idOrPath))
        {
            return PresetFile.Load(File.ReadAllText(idOrPath)) switch
            {
                PresetLoadResult.Ok ok => ok.Preset,
                PresetLoadResult.NewerVersion n => throw new UsageException($"'{idOrPath}' uses newer schema version {n.Version}", showUsage: false),
                PresetLoadResult.Broken b => throw new UsageException($"'{idOrPath}' is broken: {b.Reason}", showUsage: false),
                _ => throw new UsageException($"'{idOrPath}' is unusable", showUsage: false),
            };
        }

        if (BuiltInPresets.All.FirstOrDefault(p => p.Id == idOrPath) is { } builtIn)
        {
            return builtIn;
        }

        if (idOrPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || idOrPath.IndexOfAny(['\\', '/']) >= 0)
        {
            throw new UsageException($"preset file '{idOrPath}' does not exist", showUsage: false);
        }

        return Shared.FromStore(idOrPath, DataDirectory.Resolve(Shared.DataDirArgs(o), env));
    }
}

internal static class ValidateCommand
{
    public static int Run(Options o, TextWriter output)
    {
        o.ExpectPositionals(1);
        string text;
        try
        {
            text = File.ReadAllText(o.Positional[0]);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            output.WriteLine($"BROKEN: cannot read file: {ex.Message}");
            return 2;
        }

        switch (PresetFile.Load(text))
        {
            case PresetLoadResult.Ok ok:
                output.WriteLine("OK");
                foreach (var warning in ok.Warnings)
                {
                    output.WriteLine("warning: " + warning);
                }

                return 0;
            case PresetLoadResult.NewerVersion n:
                output.WriteLine($"NEWER: {n.Version}");
                return 3;
            case PresetLoadResult.Broken b:
                output.WriteLine($"BROKEN: {b.Reason}");
                return 2;
            default:
                output.WriteLine("BROKEN: unknown result");
                return 2;
        }
    }
}

internal static class CompareCommand
{
    public static int Run(Options o, TextWriter output)
    {
        o.ExpectPositionals(2);
        var tolerance = o.Int("--tolerance", 0, 0, 255);
        var expected = Shared.ReadPng(o.Positional[0]);
        var actual = Shared.ReadPng(o.Positional[1]);
        if (expected.Width != actual.Width || expected.Height != actual.Height)
        {
            output.WriteLine($"DIFFERENT: size {expected.Width}x{expected.Height} vs {actual.Width}x{actual.Height}");
            return 1;
        }

        var d = PixelOps.Diff(expected, actual, tolerance);
        if (d.Count == 0)
        {
            output.WriteLine($"SAME: {expected.Width * expected.Height} pixels compared (tolerance {tolerance})");
            return 0;
        }

        output.WriteLine("DIFFERENT: " + PixelOps.Summarize(d));
        return 1;
    }
}

internal static class CaptureCommand
{
    public static int Run(Options o, TextWriter output)
    {
        o.ExpectPositionals(1);
        var size = o.Int("--size", 64, 1, 4096);
        using var service = new MonitorService();
        var monitor = o.Value("--monitor") is { } device
            ? Shared.FindMonitor(service, device)
            : service.Resolve(MonitorSelection.Primary, out _);
        var b = monitor.Bounds;
        if (size > b.W || size > b.H)
        {
            throw new UsageException($"--size {size} does not fit monitor {monitor.DeviceName} ({b.W}x{b.H})", showUsage: false);
        }

        var region = new IntRect(b.X + (b.W / 2) - (size / 2), b.Y + (b.H / 2) - (size / 2), size, size);
        Shared.WritePng(o.Positional[0], ScreenProbe.Capture(region));
        output.WriteLine($"captured {monitor.DeviceName} center region {region.X},{region.Y} {size}x{size} -> {o.Positional[0]}");
        return 0;
    }
}

internal static class IcoCommand
{
    private static readonly int[] Sizes = [16, 32, 48];

    public static int Run(Options o, TextWriter output)
    {
        o.ExpectPositionals(1);
        var spec = BuiltInPresets.All.Single(p => p.Id == "builtin.default").Crosshair;
        var images = Sizes.Select(s => (Size: s, Pixels: Fit(spec, s))).ToArray();

        using var ms = new MemoryStream();
        var word = new byte[4];
        void U16(int v) { BinaryPrimitives.WriteUInt16LittleEndian(word, (ushort)v); ms.Write(word, 0, 2); }
        void U32(uint v) { BinaryPrimitives.WriteUInt32LittleEndian(word, v); ms.Write(word, 0, 4); }
        void I32(int v) { BinaryPrimitives.WriteInt32LittleEndian(word, v); ms.Write(word, 0, 4); }

        U16(0);
        U16(1);
        U16(images.Length);
        var offset = 6 + (16 * images.Length);
        foreach (var (size, _) in images)
        {
            var length = ImageBytes(size);
            ms.WriteByte((byte)size);
            ms.WriteByte((byte)size);
            ms.WriteByte(0);
            ms.WriteByte(0);
            U16(1);
            U16(32);
            U32((uint)length);
            U32((uint)offset);
            offset += length;
        }

        foreach (var (size, pixels) in images)
        {
            U32(40);
            I32(size);
            I32(size * 2); // XOR bitmap + AND mask
            U16(1);
            U16(32);
            U32(0);
            U32((uint)(size * size * 4));
            I32(0);
            I32(0);
            U32(0);
            U32(0);
            for (var y = size - 1; y >= 0; y--) // bottom-up, straight (non-premultiplied) BGRA
            {
                for (var x = 0; x < size; x++)
                {
                    var p = pixels.GetPixel(x, y);
                    ms.WriteByte(Straight(p.B, p.A));
                    ms.WriteByte(Straight(p.G, p.A));
                    ms.WriteByte(Straight(p.R, p.A));
                    ms.WriteByte(p.A);
                }
            }

            ms.Write(new byte[size * ((size + 31) / 32 * 4)]); // AND mask: alpha channel decides transparency
        }

        var full = Path.GetFullPath(o.Positional[0]);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        AtomicFile.WriteAllBytes(full, ms.ToArray());
        output.WriteLine($"wrote {o.Positional[0]} ({images.Length} images: {string.Join(", ", Sizes)})");
        return 0;
    }

    private static int ImageBytes(int size) => 40 + (size * size * 4) + (size * ((size + 31) / 32 * 4));

    private static byte Straight(byte channel, byte alpha) => alpha == 0 ? (byte)0 : (byte)Math.Min(255, ((2 * channel * 255) + alpha) / (2 * alpha));

    /// <summary>Largest scale (400% down to 25%) whose canvas fits the icon, centered on a transparent size x size canvas.</summary>
    private static PixelBuffer Fit(CrosshairSpec spec, int size)
    {
        CrosshairGeometry? chosen = null;
        for (var percent = 400; percent >= 25 && chosen is null; percent--)
        {
            var g = Shared.Layout(spec with { ScalePercent = percent }, false, false);
            if (g.CanvasW > 0 && g.CanvasW <= size && g.CanvasH <= size)
            {
                chosen = g;
            }
        }

        chosen ??= Shared.Layout(spec with { ScalePercent = 25 }, false, false);
        var canvas = new PixelBuffer(size, size);
        if (chosen.CanvasW <= 0)
        {
            return canvas;
        }

        var art = CrosshairRasterizer.Render(chosen);
        var ox = (size - art.Width) / 2;
        var oy = (size - art.Height) / 2;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                int sx = x - ox, sy = y - oy;
                if (sx >= 0 && sy >= 0 && sx < art.Width && sy < art.Height)
                {
                    canvas.SetPixel(x, y, art.GetPixel(sx, sy));
                }
            }
        }

        return canvas;
    }
}
