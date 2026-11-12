using System.IO;
using CrosshairManager.Cli;

namespace CrosshairManager.Desktop.Tests.Cli;

/// <summary>SCREEN tests: create a backdrop window and capture the screen. Never run while a game is active.</summary>
[Trait("Category", "Desktop")]
public sealed class CliScreenTests : IDisposable
{
    private readonly string _dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "chm-clis-" + Guid.NewGuid().ToString("N"))).FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void VerifyOverlay_ExpectHidden_WithoutApp_Exit0_AndSavesPngs()
    {
        var o = new StringWriter();
        var e = new StringWriter();
        var code = CliApp.Run(["verify-overlay", "--data-dir", _dir, "--expect-hidden", "--out", Path.Combine(_dir, "out")], o, e, Environment.GetEnvironmentVariables());
        Assert.True(code == 0, o + e.ToString());
        Assert.True(File.Exists(Path.Combine(_dir, "out", "verify-expected.png")));
        Assert.True(File.Exists(Path.Combine(_dir, "out", "verify-actual.png")));
    }

    [Fact]
    public void VerifyOverlay_NotHidden_WithoutApp_Exit1()
    {
        var o = new StringWriter();
        var e = new StringWriter();
        var code = CliApp.Run(["verify-overlay", "--data-dir", _dir, "--timeout", "1", "--out", Path.Combine(_dir, "out")], o, e, Environment.GetEnvironmentVariables());
        Assert.True(code == 1, o + e.ToString());
    }

    [Fact]
    public void Capture_CenterRegion_WritesPng()
    {
        var code = CliApp.Run(["capture", Path.Combine(_dir, "c.png"), "--size", "32"], new StringWriter(), new StringWriter(), Environment.GetEnvironmentVariables());
        Assert.Equal(0, code);
        var png = CrosshairManager.Core.Render.Png.Decode(File.ReadAllBytes(Path.Combine(_dir, "c.png")));
        Assert.Equal(32, png.Width);
    }
}
