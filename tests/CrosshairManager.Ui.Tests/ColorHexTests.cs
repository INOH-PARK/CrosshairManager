using System.IO;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using Xunit.Abstractions;

namespace CrosshairManager.Ui.Tests;

/// <summary>u3: an invalid hex puts the ColorHex box into the validation-error state and the overlay keeps the previous color.</summary>
[Trait("Category", "Ui")]
public sealed class ColorHexTests(ITestOutputHelper output) : IDisposable
{
    private readonly AppFixture _app = new();

    public void Dispose() => _app.Dispose();

    [Fact]
    public void u3_InvalidHex_MarksTheTextBoxInvalid_AndVerifyOverlayStillMatchesThePreviousColor()
    {
        var window = _app.SettingsWindow();
        var hex = AppFixture.SelectTab(window, "TabAdvanced", "ColorHex").AsTextBox();
        Assert.Equal("valid", hex.Properties.ItemStatus.ValueOrDefault);
        Assert.Equal("#00FF00", hex.Text);

        hex.Text = "#GG0000";
        Retry.WhileFalse(() => hex.Properties.ItemStatus.ValueOrDefault == "invalid", AppFixture.UiTimeout, throwOnTimeout: true,
            timeoutMessage: "ColorHex ItemStatus did not become 'invalid'");
        Assert.Equal(Resx.English("Color.Invalid"), hex.Properties.HelpText.ValueOrDefault);
        Assert.Equal("#GG0000", hex.Text);

        // The invalid value was never applied: the overlay still shows the active preset (builtin.default, #00FF00).
        var outDir = Path.Combine(AppFixture.ResultsDir, "u3");
        var (code, cliOutput) = AppFixture.RunCli("verify-overlay", "--data-dir", _app.DataDir, "--out", outDir);
        output.WriteLine(cliOutput);
        Assert.True(code == 0, "verify-overlay exit " + code + Environment.NewLine + cliOutput);

        var presetsDir = Path.Combine(_app.DataDir, "presets");
        Assert.False(Directory.Exists(presetsDir) && Directory.EnumerateFiles(presetsDir).Any(), "an invalid color must not create or save a preset");
    }
}
