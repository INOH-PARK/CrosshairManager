using System.IO;
using System.Windows;
using System.Windows.Threading;
using CrosshairManager.App.Hotkeys;
using CrosshairManager.App.Localization;
using CrosshairManager.App.Logging;
using CrosshairManager.App.Overlay;
using CrosshairManager.App.Presets;
using CrosshairManager.App.Startup;
using CrosshairManager.App.Storage;
using CrosshairManager.App.Tray;
using CrosshairManager.Core.Model;
using CrosshairManager.Core.Presets;
using CrosshairManager.Core.Settings;
using CrosshairManager.Platform.Hotkeys;
using CrosshairManager.Platform.Monitors;

namespace CrosshairManager.App;

/// <summary>Composition root: stores, overlay, monitors, hotkeys, tray and auto-start, wired on the UI thread.</summary>
public sealed class AppShell : IAppShell, IDisposable
{
    private readonly AppArguments _args;
    private readonly Dispatcher _dispatcher;
    private readonly FileLog _log;
    private readonly Action _exit;
    private readonly DataStore _store;
    private readonly List<PendingMessage> _messages = [];
    private readonly Dictionary<string, HotkeyState> _hotkeyStatus = new(StringComparer.Ordinal);
    private MonitorService? _monitors;
    private OverlayController? _overlay;
    private GlobalHotkeys? _hotkeys;
    private TrayIcon? _tray;
    private IReadOnlyList<PresetEntry> _presets = [];
    private AppSettings _settings = AppSettings.Default;
    private bool _disposed;

    public AppShell(AppArguments args, Dispatcher dispatcher, FileLog log, Action exit)
    {
        _args = args;
        _dispatcher = dispatcher;
        _log = log;
        _exit = exit;
        _store = new DataStore(args.DataDir, log);
    }

    public event EventHandler? SettingsRequested;

    public event EventHandler? StateChanged;

    public IReadOnlyList<PresetEntry> Presets => _presets;

    public PresetStore PresetStore => _store.Presets;

    public AppSettings Settings
    {
        get => _settings;
        set => _settings = value ?? throw new ArgumentNullException(nameof(value));
    }

    public IReadOnlyDictionary<string, HotkeyState> HotkeyStatus => _hotkeyStatus;

    public IReadOnlyList<PendingMessage> PendingMessages => _store.Messages.Concat(_messages).ToList();

    public bool MonitorFellBack => _overlay?.MonitorFellBack ?? false;

    public bool DataDirReadOnly => _store.ReadOnly;

    private OverlayController Overlay => _overlay ?? throw new InvalidOperationException("AppShell.Start has not run.");

    public void Start(Exception? pidWriteError)
    {
        _log.Info("starting; data dir " + _store.DataDir);
        if (_store.ProbeWritable() && pidWriteError is not null)
        {
            _log.Error("could not write " + SingleInstance.PidFileName, pidWriteError);
        }

        _settings = _store.LoadSettings();
        _presets = _store.LoadPresets();
        Loc.Instance.SetLanguage(_settings.Language);

        _monitors = new MonitorService();
        _overlay = new OverlayController(_dispatcher, _monitors, _log);
        _overlay.MonitorStateChanged += (_, _) => RaiseStateChanged();
        var visible = _args.InitialOverlayVisible(_settings);
        _overlay.Initialize(PresetCycle.Resolve(_presets, _settings.ActivePresetId).Crosshair, _settings.Monitor, visible);

        _hotkeys = new GlobalHotkeys();
        _hotkeys.HandlerFailed += ex => _log.Error("hotkey handler failed", ex);
        _hotkeys.Pressed += id => _dispatcher.BeginInvoke(() => OnHotkey(id));
        foreach (var id in HotkeyBindings.Ids)
        {
            Register(id, HotkeyBindings.Get(_settings.Hotkeys, id));
        }

        _tray = new TrayIcon();
        _tray.OpenSettingsClicked += (_, _) => OpenSettings();
        _tray.ToggleOverlayClicked += (_, _) => SetOverlayVisible(!Overlay.Visible);
        _tray.PresetClicked += (_, id) => SelectPreset(id);
        _tray.ExitClicked += (_, _) => Exit();
        UpdateTray();

        if (_settings.StartWithWindows)
        {
            ApplyAutoStart(true);
        }

        _log.Info("started");
        if (_args.OpenSettingsAtStartup(_settings))
        {
            OpenSettings();
        }
    }

    public void OpenSettings()
    {
        _log.Info("settings requested");
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    public void Apply(CrosshairSpec spec) => Overlay.Apply(spec);

    public bool SaveSettings() => _store.TrySaveSettings(_settings);

    public void ReloadPresets()
    {
        _presets = _store.LoadPresets();
        UpdateTray();
        RaiseStateChanged();
    }

    public void SelectPreset(string presetId)
    {
        ArgumentNullException.ThrowIfNull(presetId);
        var preset = PresetCycle.Resolve(_presets, presetId);
        _settings = _settings with { ActivePresetId = preset.Id };
        SaveSettings();
        _log.Info("active preset " + preset.Id);
        Overlay.Apply(preset.Crosshair);
        UpdateTray();
        RaiseStateChanged();
    }

    public void SetOverlayVisible(bool visible)
    {
        _settings = _settings with { OverlayVisible = visible };
        SaveSettings();
        Overlay.SetVisible(visible);
        UpdateTray();
        RaiseStateChanged();
    }

    public void SetMonitor(MonitorSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        _settings = _settings with { Monitor = selection };
        SaveSettings();
        Overlay.SetMonitor(selection);
    }

    public HotkeyState RebindHotkey(string id, HotkeyGesture? gesture)
    {
        var previous = HotkeyBindings.Get(_settings.Hotkeys, id);
        var state = Register(id, gesture);
        if (gesture is null || state.Registered)
        {
            _settings = _settings with { Hotkeys = HotkeyBindings.With(_settings.Hotkeys, id, gesture) };
            SaveSettings();
        }
        else if (previous is not null)
        {
            // Keep the old binding working; the failed state stays visible so the UI can show HotkeyError.
            var restored = Register(id, previous);
            _hotkeyStatus[id] = restored.Registered ? state : restored;
        }

        RaiseStateChanged();
        return state;
    }

    public bool SetStartWithWindows(bool enabled)
    {
        if (!ApplyAutoStart(enabled))
        {
            return false;
        }

        _settings = _settings with { StartWithWindows = enabled };
        SaveSettings();
        return true;
    }

    public void LogError(string message, Exception exception) => _log.Error(message, exception);

    public void Exit()
    {
        _log.Info("exit requested");
        _exit();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _tray?.Dispose();
        _hotkeys?.Dispose();
        _overlay?.Dispose();
        _monitors?.Dispose();
        _log.Info("stopped");
    }

    private void OnHotkey(string id)
    {
        _log.Info("hotkey " + id);
        switch (id)
        {
            case HotkeyBindings.ToggleOverlay:
                SetOverlayVisible(!Overlay.Visible);
                break;
            case HotkeyBindings.NextPreset:
            case HotkeyBindings.PreviousPreset:
                var next = PresetCycle.Step(_presets, _settings.ActivePresetId, id == HotkeyBindings.NextPreset ? 1 : -1);
                if (next is not null)
                {
                    SelectPreset(next);
                }

                break;
            case HotkeyBindings.OpenSettings:
                OpenSettings();
                break;
        }
    }

    private HotkeyState Register(string id, HotkeyGesture? gesture)
    {
        var hotkeys = _hotkeys ?? throw new InvalidOperationException("Hotkeys are not running.");
        HotkeyState state;
        if (gesture is null)
        {
            hotkeys.Unregister(id);
            state = new HotkeyState(null, false, 0);
        }
        else if (hotkeys.TryRegister(id, gesture, out var error))
        {
            state = new HotkeyState(gesture, true, 0);
        }
        else
        {
            _log.Info($"hotkey {id} {gesture} could not be registered (Win32 error {error})");
            _messages.Add(new PendingMessage("Hotkey.RegistrationFailed", $"{id}: {gesture} ({error})"));
            state = new HotkeyState(gesture, false, error);
        }

        _hotkeyStatus[id] = state;
        return state;
    }

    private bool ApplyAutoStart(bool enabled)
    {
        var exe = Environment.ProcessPath ?? throw new InvalidOperationException("The process path is unknown.");
        try
        {
            if (new AutoStart(new RegistryRunKey(), exe).Apply(enabled))
            {
                _log.Info("start with Windows " + (enabled ? "set to " + exe : "removed"));
            }

            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            _log.Error("start with Windows could not be updated", ex);
            _messages.Add(new PendingMessage("Error.AutoStartFailed", ex.Message));
            return false;
        }
    }

    private void UpdateTray() => _tray?.Update(_presets, _settings.ActivePresetId, _overlay?.Visible ?? false);

    private void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
}
