using System.Security.Cryptography;
using CrosshairManager.Core.Model;
using CrosshairManager.Core.Presets;

namespace CrosshairManager.Core.Tests.Presets;

internal static class PresetTestData
{
    private static readonly double[] Rotations = [0, 45, 90, 135, 180, 225, 270, 315];

    public static CrosshairSpec RandomSpec(Random r)
    {
        LineSpec Line() => new(r.Next(2) == 0, r.Next(0, 513), r.Next(1, 65), r.Next(-32, 257));
        return new CrosshairSpec
        {
            Color = new Rgb((byte)r.Next(256), (byte)r.Next(256), (byte)r.Next(256)),
            Opacity = r.Next(0, 101),
            ScalePercent = r.Next(25, 401),
            RotationDeg = Rotations[r.Next(Rotations.Length)],
            OffsetX = r.Next(-4096, 4097),
            OffsetY = r.Next(-4096, 4097),
            Dot = new DotSpec(r.Next(2) == 0, r.Next(1, 65), r.Next(0, 101)),
            Top = Line(),
            Bottom = Line(),
            Left = Line(),
            Right = Line(),
            Outline = new OutlineSpec(r.Next(2) == 0, r.Next(1, 17), new Rgb((byte)r.Next(256), (byte)r.Next(256), (byte)r.Next(256)), r.Next(0, 101)),
        };
    }

    public static Preset RandomPreset(Random r, int index)
    {
        var bytes = new byte[16];
        r.NextBytes(bytes);
        var created = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(r.Next(0, 10_000_000)).AddTicks(r.Next(0, 10_000_000));
        return new Preset(new Guid(bytes).ToString("D"), $"Random {index}", created, created.AddMinutes(r.Next(0, 1000)), RandomSpec(r));
    }

    public static string FixturePath(params string[] parts)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            string candidate = Path.Combine([dir.FullName, "tests", "fixtures", .. parts]);
            if (File.Exists(candidate)) return candidate;
        }

        throw new FileNotFoundException($"Fixture {string.Join('/', parts)} not found above {AppContext.BaseDirectory}");
    }

    public static string Sha256(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    public static PresetLoadResult.Ok AssertOk(PresetLoadResult result) => Assert.IsType<PresetLoadResult.Ok>(result);
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}
