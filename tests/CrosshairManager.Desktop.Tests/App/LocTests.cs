using System.Collections;
using System.IO;
using System.ComponentModel;
using System.Globalization;
using System.Resources;
using CrosshairManager.App.Localization;
using CrosshairManager.Core.Presets;

namespace CrosshairManager.Desktop.Tests.App;

[Trait("Category", "Desktop")]
public class LocTests
{
    private static readonly string[] AppKeys =
    [
        "Tray.OpenSettings", "Tray.ToggleOverlay", "Tray.Presets", "Tray.Exit",
        "Error.DataDirReadOnly", "Error.InvalidArguments", "Error.StartupFailed", "Error.Unhandled", "Error.AutoStartFailed",
        "Settings.RecoveredFromCorrupt", "Settings.NewerVersion", "Settings.LoadFailed", "Settings.Warning",
        "Presets.LoadFailed", "Hotkey.RegistrationFailed",
    ];

    [Fact]
    public void EveryKeyUsedByTheShellAndEveryBuiltInNameExistsInEnglish()
    {
        var rm = new ResourceManager(Loc.ResourceBaseName, typeof(Loc).Assembly);
        var keys = AppKeys.Concat(BuiltInPresets.All.Select(p => p.Name));
        foreach (var key in keys)
        {
            Assert.False(string.IsNullOrWhiteSpace(rm.GetString(key, CultureInfo.InvariantCulture)), key);
        }
    }

    [Fact]
    public void Indexer_ReturnsResxValueOrKey_AndSetLanguageRaisesItemIndexer()
    {
        var rm = new ResourceManager(Loc.ResourceBaseName, typeof(Loc).Assembly);
        var raised = new List<string?>();
        PropertyChangedEventHandler handler = (_, e) => raised.Add(e.PropertyName);
        Loc.Instance.PropertyChanged += handler;
        try
        {
            Loc.Instance.SetLanguage("en");
            Assert.Equal(rm.GetString("Tray.Exit", CultureInfo.GetCultureInfo("en")), Loc.Instance["Tray.Exit"]);
            Assert.Equal("No.Such.Key", Loc.Instance["No.Such.Key"]);
        }
        finally
        {
            Loc.Instance.PropertyChanged -= handler;
        }

        Assert.Equal(["Item[]"], raised);
    }

    [Theory]
    [InlineData("ko", "en-US", "ko")]
    [InlineData("en", "ko-KR", "en")]
    [InlineData("system", "ko-KR", "ko")]
    [InlineData("system", "de-DE", "en")]
    [InlineData("system", "en-US", "en")]
    public void Resolve_SystemFollowsOsLanguage(string setting, string os, string expected) =>
        Assert.Equal(expected, Loc.Resolve(setting, CultureInfo.GetCultureInfo(os)).Name);
}
