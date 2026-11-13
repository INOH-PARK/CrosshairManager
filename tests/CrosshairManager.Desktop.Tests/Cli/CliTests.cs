using System.IO;
using System.Buffers.Binary;
using System.Text.Json.Nodes;
using CrosshairManager.Cli;
using CrosshairManager.Core.Presets;
using CrosshairManager.Core.Render;

namespace CrosshairManager.Desktop.Tests.Cli;

/// <summary>Non-screen CLI commands: these never create windows, send input or capture the screen.</summary>
[Trait("Category", "Desktop")]
public sealed class CliTests : IDisposable
{
    private readonly string _dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "chm-cli-" + Guid.NewGuid().ToString("N"))).FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    internal static (int Code, string Out, string Err) Run(params string[] args)
    {
        var o = new StringWriter();
        var e = new StringWriter();
        var code = CliApp.Run(args, o, e, Environment.GetEnvironmentVariables());
        return (code, o.ToString(), e.ToString());
    }

    private string P(string name) => Path.Combine(_dir, name);

    private static string FixturePath()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
        {
            var candidate = Path.Combine(d.FullName, "tests", "fixtures", "presets", "v1.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("tests/fixtures/presets/v1.json not found above " + AppContext.BaseDirectory);
    }

    [Fact]
    public void Render_BuiltinDefault_WritesDecodableZoomedPngOnBackground()
    {
        var (code, _, err) = Run("render", "builtin.default", P("out.png"), "--zoom", "8", "--bg", "#202020");
        Assert.True(code == 0, err);
        var png = Png.Decode(File.ReadAllBytes(P("out.png")));
        Assert.True(png.Width > 0 && png.Width % 8 == 0 && png.Height % 8 == 0);
        Assert.Equal(new PixelBuffer.Pixel(0x20, 0x20, 0x20, 255), png.GetPixel(0, 0));
    }

    [Fact]
    public void Render_WithoutBackground_KeepsTransparency_AndOddFlagMakesCanvasOdd()
    {
        Assert.Equal(0, Run("render", "builtin.default", P("even.png"), "--zoom", "1").Code);
        Assert.Equal(0, Run("render", "builtin.default", P("odd.png"), "--zoom", "1", "--odd").Code);
        var even = Png.Decode(File.ReadAllBytes(P("even.png")));
        var odd = Png.Decode(File.ReadAllBytes(P("odd.png")));
        Assert.Equal(0, even.GetPixel(0, 0).A);
        Assert.Equal(0, even.Width % 2);
        Assert.Equal(1, odd.Width % 2);
    }

    [Fact]
    public void Render_JsonPath_UsesThatFile()
    {
        File.WriteAllText(P("p.json"), PresetFile.Serialize(BuiltInPresets.All.Single(p => p.Id == "builtin.dot")));
        Assert.Equal(0, Run("render", P("p.json"), P("dot.png"), "--zoom", "1").Code);
        var png = Png.Decode(File.ReadAllBytes(P("dot.png")));
        Assert.True(png.Width < 16);
    }

    [Fact]
    public void Render_MissingPreset_Exit2_AndWritesNothing()
    {
        var (code, _, err) = Run("render", P("nope.json"), P("x.png"), "--data-dir", _dir);
        Assert.Equal(2, code);
        Assert.NotEmpty(err);
        Assert.False(File.Exists(P("x.png")));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("33")]
    [InlineData("abc")]
    [InlineData("")]
    public void Render_BadZoom_Exit2(string zoom)
    {
        Assert.Equal(2, Run("render", "builtin.default", P("x.png"), "--zoom", zoom).Code);
    }

    [Fact]
    public void Render_BadBackground_Exit2()
    {
        Assert.Equal(2, Run("render", "builtin.default", P("x.png"), "--bg", "nothex").Code);
    }

    [Fact]
    public void Validate_Fixture_PrintsOk()
    {
        var (code, output, _) = Run("validate", FixturePath());
        Assert.Equal(0, code);
        Assert.StartsWith("OK", output);
    }

    [Fact]
    public void Validate_OutOfRangeValue_IsOkWithWarnings()
    {
        var node = JsonNode.Parse(File.ReadAllText(FixturePath()))!;
        node["crosshair"]!["opacity"] = 500;
        File.WriteAllText(P("w.json"), node.ToJsonString());
        var (code, output, _) = Run("validate", P("w.json"));
        Assert.Equal(0, code);
        Assert.StartsWith("OK", output);
        Assert.Contains("clamped", output);
    }

    [Theory]
    [InlineData("{not json")]
    [InlineData("[]")]
    [InlineData("{\"schemaVersion\":1}")]
    public void Validate_Broken_Exit2(string content)
    {
        File.WriteAllText(P("b.json"), content);
        var (code, output, _) = Run("validate", P("b.json"));
        Assert.Equal(2, code);
        Assert.StartsWith("BROKEN:", output);
    }

    [Fact]
    public void Validate_MissingFile_IsBroken()
    {
        var (code, output, _) = Run("validate", P("missing.json"));
        Assert.Equal(2, code);
        Assert.StartsWith("BROKEN:", output);
    }

    [Fact]
    public void Validate_NewerVersion_Exit3()
    {
        File.WriteAllText(P("n.json"), "{\"schemaVersion\":99,\"crosshair\":{}}");
        var (code, output, _) = Run("validate", P("n.json"));
        Assert.Equal(3, code);
        Assert.StartsWith("NEWER: 99", output);
    }

    private void TwoPngs(out string a, out string b)
    {
        a = P("a.png");
        b = P("b.png");
        Assert.Equal(0, Run("render", "builtin.default", a, "--zoom", "1", "--bg", "#202020").Code);
        var img = Png.Decode(File.ReadAllBytes(a));
        img.SetPixel(3, 2, new PixelBuffer.Pixel(0x20 + 10, 0x20, 0x20, 255));
        File.WriteAllBytes(b, Png.Encode(img));
    }

    [Fact]
    public void Compare_SameFile_Exit0()
    {
        TwoPngs(out var a, out _);
        Assert.Equal(0, Run("compare", a, a).Code);
    }

    [Fact]
    public void Compare_OnePixelChanged_Exit1_ReportsCountAndFirst()
    {
        TwoPngs(out var a, out var b);
        var (code, output, _) = Run("compare", a, b);
        Assert.Equal(1, code);
        Assert.Contains("1 ", output);
        Assert.Contains("(3,2)", output);
    }

    [Fact]
    public void Compare_ToleranceCoversSmallDifference()
    {
        TwoPngs(out var a, out var b);
        Assert.Equal(1, Run("compare", a, b, "--tolerance", "9").Code);
        Assert.Equal(0, Run("compare", a, b, "--tolerance", "10").Code);
    }

    [Fact]
    public void Compare_SizeMismatch_Exit1()
    {
        TwoPngs(out var a, out _);
        Assert.Equal(0, Run("render", "builtin.default", P("big.png"), "--zoom", "2").Code);
        Assert.Equal(1, Run("compare", a, P("big.png")).Code);
    }

    [Fact]
    public void Compare_NotAPng_Exit2()
    {
        TwoPngs(out var a, out _);
        File.WriteAllText(P("junk.png"), "junk");
        Assert.Equal(2, Run("compare", a, P("junk.png")).Code);
        Assert.Equal(2, Run("compare", a, P("missing.png")).Code);
        Assert.Equal(2, Run("compare", a).Code);
        Assert.Equal(2, Run("compare", a, a, "--tolerance", "-1").Code);
    }

    [Fact]
    public void Ico_HasThreeValidImages()
    {
        Assert.Equal(0, Run("ico", P("x.ico")).Code);
        var b = File.ReadAllBytes(P("x.ico"));
        Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(0)));
        Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(2)));
        Assert.Equal(3, BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(4)));
        var sizes = new List<int>();
        for (var i = 0; i < 3; i++)
        {
            var e = 6 + i * 16;
            var size = b[e];
            sizes.Add(size);
            Assert.Equal(size, b[e + 1]);
            Assert.Equal(32, BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(e + 6)));
            var length = (int)BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(e + 8));
            var offset = (int)BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(e + 12));
            Assert.Equal(40 + size * size * 4 + size * ((size + 31) / 32 * 4), length);
            Assert.True(offset + length <= b.Length);
            Assert.Equal(40u, BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(offset)));
            Assert.Equal(size, BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(offset + 4)));
            Assert.Equal(size * 2, BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(offset + 8)));
        }

        Assert.Equal(new[] { 16, 32, 48 }, sizes);
    }

    [Theory]
    [InlineData]
    [InlineData("bogus")]
    [InlineData("render")]
    [InlineData("render", "builtin.default")]
    [InlineData("render", "builtin.default", "x.png", "--nope")]
    [InlineData("render", "builtin.default", "x.png", "extra")]
    [InlineData("render", "builtin.default", "x.png", "--zoom")]
    [InlineData("validate")]
    [InlineData("ico")]
    [InlineData("capture")]
    [InlineData("capture", "x.png", "--size", "0")]
    public void UsageErrors_PrintUsage_Exit2(params string[] args)
    {
        var (code, _, err) = Run(args);
        Assert.Equal(2, code);
        Assert.Contains("usage", err, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("--timeout", "0")]
    [InlineData("--timeout", "abc")]
    [InlineData("--preset", "no-such-preset")]
    [InlineData("--monitor", "\\\\.\\NOSUCHDISPLAY")]
    [InlineData("--bogus")]
    public void VerifyOverlay_BadInput_Exit2_BeforeAnyWindow(params string[] extra)
    {
        var args = new[] { "verify-overlay", "--data-dir", _dir }.Concat(extra).ToArray();
        var (code, _, err) = Run(args);
        Assert.Equal(2, code);
        Assert.NotEmpty(err);
        Assert.False(File.Exists(P("settings.json")), "verify-overlay must not create settings.json");
    }

    [Fact]
    public void VerifyOverlay_CorruptSettings_AreReadAsDefaults_AndLeftUntouched()
    {
        byte[] corrupt = [0x7B, 0x22, 0xFF, 0x00, 0x7B];
        File.WriteAllBytes(P("settings.json"), corrupt);

        // The unknown preset stops the command with exit 2 after settings were loaded and before any window exists.
        var (code, _, err) = Run("verify-overlay", "--data-dir", _dir, "--preset", "no-such-preset");

        Assert.Equal(2, code);
        Assert.Contains("no-such-preset", err, StringComparison.Ordinal);
        Assert.Equal(corrupt, File.ReadAllBytes(P("settings.json")));
        Assert.Equal([P("settings.json")], Directory.GetFiles(_dir));
    }
}
