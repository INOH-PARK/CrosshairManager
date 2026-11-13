using CrosshairManager.Core.Settings;

namespace CrosshairManager.Core.Tests.Settings;

public class SettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "chm-settings-" + Guid.NewGuid().ToString("N"));

    private string FilePath => Path.Combine(_dir, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void Missing_file_loads_defaults_and_creates_the_file()
    {
        var result = new SettingsStore(_dir).Load();

        Assert.IsType<SettingsLoadStatus.CreatedDefault>(result.Status);
        Assert.Equal(AppSettings.Default, result.Settings);
        Assert.Empty(result.Warnings);
        Assert.True(File.Exists(FilePath));
    }

    [Fact]
    public void Corrupt_file_is_moved_aside_with_its_original_bytes_and_defaults_are_used()
    {
        Directory.CreateDirectory(_dir);
        byte[] original = [0x7B, 0x22, 0xFF, 0x00, 0x7B];
        File.WriteAllBytes(FilePath, original);

        var result = new SettingsStore(_dir).Load();

        var recovered = Assert.IsType<SettingsLoadStatus.RecoveredFromCorrupt>(result.Status);
        Assert.Matches(@"settings\.json\.corrupt-\d{14}$", recovered.BackupPath);
        Assert.Equal(original, File.ReadAllBytes(recovered.BackupPath));
        Assert.Equal(AppSettings.Default, result.Settings);
        Assert.False(File.Exists(FilePath));
    }

    [Fact]
    public void Read_of_a_corrupt_file_returns_defaults_and_leaves_the_folder_untouched()
    {
        Directory.CreateDirectory(_dir);
        byte[] original = [0x7B, 0x22, 0xFF, 0x00, 0x7B];
        File.WriteAllBytes(FilePath, original);

        var settings = new SettingsStore(_dir).Read();

        Assert.Equal(AppSettings.Default, settings);
        Assert.Equal(original, File.ReadAllBytes(FilePath));
        Assert.Equal([FilePath], Directory.GetFiles(_dir));
    }

    [Fact]
    public void Read_of_a_missing_file_returns_defaults_and_creates_nothing()
    {
        Assert.Equal(AppSettings.Default, new SettingsStore(_dir).Read());
        Assert.False(Directory.Exists(_dir));
    }

    [Fact]
    public void Read_returns_the_stored_settings()
    {
        var stored = AppSettings.Default with { ActivePresetId = "mine", OverlayVisible = false };
        new SettingsStore(_dir).Save(stored);

        Assert.Equal(stored, new SettingsStore(_dir).Read());
    }

    [Fact]
    public void Newer_schema_version_uses_defaults_and_leaves_the_file_byte_for_byte()
    {
        Directory.CreateDirectory(_dir);
        byte[] original = System.Text.Encoding.UTF8.GetBytes(
            """{"schemaVersion": 2, "activePresetId": "mine", "future": {"x": 1}}""");
        File.WriteAllBytes(FilePath, original);

        var result = new SettingsStore(_dir).Load();

        Assert.Equal(new SettingsLoadStatus.NewerVersion(2), result.Status);
        Assert.Equal(AppSettings.Default, result.Settings);
        Assert.Equal(original, File.ReadAllBytes(FilePath));
    }

    [Fact]
    public void Saved_settings_load_back_unchanged()
    {
        var store = new SettingsStore(_dir);
        var settings = AppSettings.Default with
        {
            ActivePresetId = "9b1d2f4e-0000-4000-8000-000000000001",
            OverlayVisible = false,
            Monitor = new MonitorSelection(MonitorMode.Device, @"\\.\DISPLAY2"),
            Hotkeys = AppSettings.Default.Hotkeys with { ToggleOverlay = null, OpenSettings = Gesture("Ctrl+Alt+Shift+O") },
            Language = "ko",
            StartWithWindows = true,
            DisclaimerAccepted = true,
            SettingsWindowPlacement = new WindowPlacement(10, 20, 900, 620),
        };

        store.Save(settings);
        var result = store.Load();

        Assert.IsType<SettingsLoadStatus.Ok>(result.Status);
        Assert.Empty(result.Warnings);
        Assert.Equal(settings, result.Settings);
    }

    [Fact]
    public void Invalid_hotkey_text_loads_as_unbound_with_a_warning()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, """
            {"schemaVersion": 1, "hotkeys": {"toggleOverlay": "Bogus", "nextPreset": "Ctrl+Alt+Shift+Right", "previousPreset": "Ctrl+Alt+Shift+Left", "openSettings": null}}
            """);

        var result = new SettingsStore(_dir).Load();

        Assert.IsType<SettingsLoadStatus.Ok>(result.Status);
        Assert.Null(result.Settings.Hotkeys.ToggleOverlay);
        Assert.Equal(AppSettings.Default.Hotkeys.NextPreset, result.Settings.Hotkeys.NextPreset);
        Assert.Single(result.Warnings);
        Assert.Contains("hotkeys.toggleOverlay", result.Warnings[0]);
    }

    [Fact]
    public void Save_passes_IOException_through_when_the_data_folder_is_a_file()
    {
        Directory.CreateDirectory(_dir);
        string blocker = Path.Combine(_dir, "blocker");
        File.WriteAllText(blocker, "not a folder");

        Assert.ThrowsAny<IOException>(() => new SettingsStore(blocker).Save(AppSettings.Default));
    }

    private static HotkeyGesture Gesture(string text)
    {
        Assert.True(HotkeyGesture.TryParse(text, out var gesture));
        return gesture!;
    }
}
