using System.IO;
using System.Text.Json.Nodes;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using Xunit.Abstractions;

namespace CrosshairManager.Ui.Tests;

/// <summary>u2: a data folder without settings shows DisclaimerDialog; DisclaimerOk persists disclaimerAccepted; the next run shows no dialog.</summary>
[Trait("Category", "Ui")]
public sealed class FirstRunTests(ITestOutputHelper output)
{
    [Fact]
    public void u2_FirstRun_ShowsDisclaimer_OkPersistsAcceptance_AndSecondRunShowsNoDialog()
    {
        string dataDir;
        using (var first = new AppFixture(createSettings: false, openSettings: false))
        {
            dataDir = first.DataDir;
            var dialog = first.TopLevel("DisclaimerDialog", AppFixture.StartupTimeout);
            output.WriteLine("DisclaimerDialog shown: " + dialog.Title);
            var ok = Retry.WhileNull(() => dialog.FindFirstDescendant(cf => cf.ByAutomationId("DisclaimerOk")), AppFixture.UiTimeout, throwOnTimeout: true).Result!;
            ok.AsButton().Invoke();

            Retry.WhileFalse(() => DisclaimerAccepted(first.SettingsPath), TimeSpan.FromSeconds(10), throwOnTimeout: true, ignoreException: true,
                timeoutMessage: "settings.json disclaimerAccepted did not become true");
            Retry.WhileTrue(() => first.FindTopLevel("DisclaimerDialog") is not null, AppFixture.UiTimeout, throwOnTimeout: true,
                timeoutMessage: "DisclaimerDialog did not close after DisclaimerOk");
            first.KeepDataDir = true;
        }

        using var second = new AppFixture(dataDir, createSettings: false, openSettings: true);
        Assert.True(DisclaimerAccepted(second.SettingsPath));
        var window = second.SettingsWindow();
        Retry.WhileNull(() => window.FindFirstDescendant(cf => cf.ByAutomationId("PresetList")), AppFixture.UiTimeout, throwOnTimeout: true);

        // Watch for 3 s after the window is ready: the dialog must never appear (the retry is expected to time out).
        var watch = Retry.WhileNull(() => second.FindTopLevel("DisclaimerDialog"), TimeSpan.FromSeconds(3));
        Assert.False(watch.Success, "DisclaimerDialog appeared on the second run");
        Assert.Null(watch.Result);
        output.WriteLine("no DisclaimerDialog for 3 s on the second run");
    }

    private static bool DisclaimerAccepted(string settingsPath) =>
        File.Exists(settingsPath)
        && JsonNode.Parse(File.ReadAllText(settingsPath))?["disclaimerAccepted"]?.GetValue<bool>() == true;
}
