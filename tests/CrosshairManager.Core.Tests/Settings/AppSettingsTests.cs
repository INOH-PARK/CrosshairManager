using CrosshairManager.Core.Settings;

namespace CrosshairManager.Core.Tests.Settings;

public class AppSettingsTests
{
    [Fact]
    public void Defaults_match_the_plan()
    {
        var settings = AppSettings.Default;

        Assert.Equal("builtin.default", settings.ActivePresetId);
        Assert.True(settings.OverlayVisible);
        Assert.Equal(MonitorSelection.Primary, settings.Monitor);
        Assert.Equal("Ctrl+Alt+Shift+X", settings.Hotkeys.ToggleOverlay?.ToString());
        Assert.Equal("Ctrl+Alt+Shift+Right", settings.Hotkeys.NextPreset?.ToString());
        Assert.Equal("Ctrl+Alt+Shift+Left", settings.Hotkeys.PreviousPreset?.ToString());
        Assert.Null(settings.Hotkeys.OpenSettings);
        Assert.Equal("system", settings.Language);
        Assert.False(settings.StartWithWindows);
        Assert.False(settings.DisclaimerAccepted);
        Assert.Null(settings.SettingsWindowPlacement);
    }
}
