using System.Collections;
using System.IO;
using CrosshairManager.App.Logging;
using CrosshairManager.App.Storage;
using CrosshairManager.Core.Settings;

namespace CrosshairManager.Desktop.Tests.App;

[Trait("Category", "Desktop")]
public class DataStoreTests
{
    private static readonly FixedTime SameSecond = new(new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.Zero));

    [Fact]
    public void CorruptSettings_IsMovedAsideAndReported()
    {
        using var dir = new AppTestDir();
        File.WriteAllText(dir.Combine("settings.json"), "{");
        var store = new DataStore(dir.Path, new FileLog(dir.Path, dir.Combine("fallback.log")), SameSecond);
        Assert.True(store.ProbeWritable());
        Assert.Equal(AppSettings.Default, store.LoadSettings());
        Assert.Single(Directory.GetFiles(dir.Path, "settings.json.corrupt-*"));
        Assert.Contains(store.Messages, m => m.Key == "InfoBar.SettingsRecovered");
    }

    [Fact]
    public void SecondCorruptLoadInSameSecond_KeepsRunningWithDefaults()
    {
        using var dir = new AppTestDir();
        var log = new FileLog(dir.Path, dir.Combine("fallback.log"));
        File.WriteAllText(dir.Combine("settings.json"), "{");
        new DataStore(dir.Path, log, SameSecond).LoadSettings();
        File.WriteAllText(dir.Combine("settings.json"), "{");

        var store = new DataStore(dir.Path, log, SameSecond);
        Assert.Equal(AppSettings.Default, store.LoadSettings());
        Assert.Contains(store.Messages, m => m.Key == "Settings.LoadFailed");
        Assert.False(store.ReadOnly);
    }

    [Fact]
    public void UnwritableDataDir_RunsInMemoryWithFallbackLogAndMessage()
    {
        using var dir = new AppTestDir();
        File.WriteAllText(dir.Combine("blocker"), "file");
        var dataDir = dir.Combine("blocker", "data");
        var fallback = dir.Combine("fallback.log");
        var log = new FileLog(dataDir, fallback);
        var store = new DataStore(dataDir, log);

        Assert.False(store.ProbeWritable());
        Assert.True(store.ReadOnly);
        Assert.Equal(AppSettings.Default, store.LoadSettings());
        Assert.NotEmpty(store.LoadPresets());
        Assert.False(store.TrySaveSettings(AppSettings.Default with { OverlayVisible = false }));
        log.Info("overlay shown 1,1,1,1");

        var text = File.ReadAllText(fallback);
        Assert.Contains("data dir read-only", text);
        Assert.Contains("overlay shown 1,1,1,1", text);
        Assert.Equal(["Error.DataDirReadOnly"], store.Messages.Select(m => m.Key));
    }

    [Fact]
    public void SaveFailureAfterStart_SwitchesToReadOnlyOnce()
    {
        using var dir = new AppTestDir();
        var fallback = dir.Combine("fallback.log");
        var store = new DataStore(dir.Path, new FileLog(dir.Path, fallback));
        Assert.True(store.ProbeWritable());
        Directory.CreateDirectory(dir.Combine("settings.json.tmp"));
        Assert.False(store.TrySaveSettings(AppSettings.Default));
        Assert.False(store.TrySaveSettings(AppSettings.Default));
        Assert.True(store.ReadOnly);
        Assert.Single(store.Messages, m => m.Key == "Error.DataDirReadOnly");
    }
}
