using CrosshairManager.App.Hotkeys;
using CrosshairManager.Core.Model;
using CrosshairManager.Core.Presets;
using CrosshairManager.Core.Settings;

namespace CrosshairManager.App;

/// <summary>
/// What the settings UI (todo 16) uses. All members are called on the UI thread and all events are raised there.
/// A subscriber to <see cref="SettingsRequested"/> shows and activates the settings window
/// (CrosshairManager.Platform.Windowing.Foreground.Activate implements the Must have foreground rule).
/// </summary>
public interface IAppShell
{
    /// <summary>Raised by <see cref="OpenSettings"/> (tray, hotkey, second launch, <c>--settings</c>, first run).</summary>
    event EventHandler? SettingsRequested;

    /// <summary>Presets, active preset, visibility, monitor fallback or hotkey status changed.</summary>
    event EventHandler? StateChanged;

    IReadOnlyList<PresetEntry> Presets { get; }

    PresetStore PresetStore { get; }

    /// <summary>Current settings (in memory). Assign, then <see cref="SaveSettings"/> to persist.</summary>
    AppSettings Settings { get; set; }

    IReadOnlyDictionary<string, HotkeyState> HotkeyStatus { get; }

    IReadOnlyList<PendingMessage> PendingMessages { get; }

    bool MonitorFellBack { get; }

    bool DataDirReadOnly { get; }

    void OpenSettings();

    /// <summary>Shows <paramref name="spec"/> on the overlay (coalesced; no saving).</summary>
    void Apply(CrosshairSpec spec);

    /// <summary>Persists <see cref="Settings"/>; false when the data folder is read-only (the app keeps running in memory).</summary>
    bool SaveSettings();

    /// <summary>Re-reads the presets folder after the UI created, renamed, imported or deleted presets.</summary>
    void ReloadPresets();

    /// <summary>Makes <paramref name="presetId"/> active (saves activePresetId) and applies it.</summary>
    void SelectPreset(string presetId);

    /// <summary>Shows or hides the overlay and saves overlayVisible.</summary>
    void SetOverlayVisible(bool visible);

    /// <summary>Changes the target monitor and saves it.</summary>
    void SetMonitor(MonitorSelection selection);

    /// <summary>Registers <paramref name="gesture"/> (null unbinds) for one of <see cref="HotkeyBindings.Ids"/>; saved only on success.</summary>
    HotkeyState RebindHotkey(string id, HotkeyGesture? gesture);

    /// <summary>Writes or removes the HKCU Run value and saves startWithWindows. False when the registry write failed.</summary>
    bool SetStartWithWindows(bool enabled);

    void Exit();
}
