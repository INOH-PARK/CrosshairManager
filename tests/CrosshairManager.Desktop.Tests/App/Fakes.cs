using System.Windows;
using System.Windows.Threading;
using CrosshairManager.App;
using CrosshairManager.App.Hotkeys;
using CrosshairManager.App.Presets;
using CrosshairManager.App.ViewModels;
using CrosshairManager.Core.Geometry;
using CrosshairManager.Core.Model;
using CrosshairManager.Core.Presets;
using CrosshairManager.Core.Settings;
using CrosshairManager.Platform.Monitors;

namespace CrosshairManager.Desktop.Tests.App;

/// <summary>In-memory shell over a real PresetStore in a temp folder; records Apply/Save calls.</summary>
internal sealed class FakeAppShell : IAppShell
{
    public FakeAppShell(string dataDir)
    {
        PresetStore = new PresetStore(dataDir);
        Presets = PresetStore.LoadAll();
        Settings = AppSettings.Default with { DisclaimerAccepted = true };
    }

    public event EventHandler? SettingsRequested;

    public event EventHandler? StateChanged;

    public IReadOnlyList<PresetEntry> Presets { get; private set; }

    public PresetStore PresetStore { get; }

    public AppSettings Settings { get; set; }

    public Dictionary<string, HotkeyState> Hotkeys { get; } = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, HotkeyState> HotkeyStatus => Hotkeys;

    public List<PendingMessage> Pending { get; } = [];

    public IReadOnlyList<PendingMessage> PendingMessages => Pending;

    public bool MonitorFellBack { get; set; }

    public bool DataDirReadOnly => false;

    public List<CrosshairSpec> Applied { get; } = [];

    public int SaveCount { get; private set; }

    public HashSet<string> FailingGestures { get; } = new(StringComparer.Ordinal);

    public bool FailAutoStart { get; set; }

    public int OpenSettingsCount { get; private set; }

    public void OpenSettings()
    {
        OpenSettingsCount++;
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    public void Apply(CrosshairSpec spec) => Applied.Add(spec);

    public bool SaveSettings()
    {
        SaveCount++;
        return true;
    }

    public void ReloadPresets()
    {
        Presets = PresetStore.LoadAll();
        Raise();
    }

    public void SelectPreset(string presetId)
    {
        var preset = PresetCycle.Resolve(Presets, presetId);
        Settings = Settings with { ActivePresetId = preset.Id };
        Apply(preset.Crosshair);
        Raise();
    }

    public void SetOverlayVisible(bool visible)
    {
        Settings = Settings with { OverlayVisible = visible };
        Raise();
    }

    public void SetMonitor(MonitorSelection selection) => Settings = Settings with { Monitor = selection };

    public HotkeyState RebindHotkey(string id, HotkeyGesture? gesture)
    {
        HotkeyState state;
        if (gesture is not null && FailingGestures.Contains(gesture.ToString()))
        {
            state = new HotkeyState(gesture, false, 1409);
            Pending.Add(new PendingMessage("Hotkey.RegistrationFailed", gesture.ToString()));
        }
        else
        {
            state = new HotkeyState(gesture, gesture is not null, 0);
            Settings = Settings with { Hotkeys = HotkeyBindings.With(Settings.Hotkeys, id, gesture) };
        }

        Hotkeys[id] = state;
        Raise();
        return state;
    }

    public bool SetStartWithWindows(bool enabled)
    {
        if (FailAutoStart)
        {
            Pending.Add(new PendingMessage("Error.AutoStartFailed", "denied"));
            return false;
        }

        Settings = Settings with { StartWithWindows = enabled };
        return true;
    }

    public void Exit()
    {
    }

    public void Raise() => StateChanged?.Invoke(this, EventArgs.Empty);
}

internal sealed class FakeDialogs : IDialogService
{
    public string? RenameAnswer { get; set; }

    public bool DeleteAnswer { get; set; }

    public string? ImportPath { get; set; }

    public string? ExportPath { get; set; }

    public string? Clipboard { get; private set; }

    public string? PromptRename(string currentName) => RenameAnswer;

    public bool ConfirmDelete(string name) => DeleteAnswer;

    public string? PickImportFile() => ImportPath;

    public string? PickExportFile(string suggestedFileName) => ExportPath;

    public void SetClipboardText(string text) => Clipboard = text;
}

internal sealed class FakeMonitors : IMonitorCatalog
{
    public event EventHandler? Changed;

    public List<MonitorInfo> Monitors { get; } =
    [
        new(@"\\.\DISPLAY2", new IntRect(0, 0, 1920, 1080), new IntRect(0, 0, 1920, 1032), true, 96, 96),
        new(@"\\.\DISPLAY1", new IntRect(1920, 0, 1920, 1080), new IntRect(1920, 0, 1920, 1032), false, 96, 96),
    ];

    public IReadOnlyList<MonitorInfo> GetMonitors() => Monitors;

    public void Raise() => Changed?.Invoke(this, EventArgs.Empty);
}

/// <summary>Debounce under test control: nothing runs until <see cref="Fire"/>.</summary>
internal sealed class ManualDebounce : IDebounce
{
    private Action? _action;

    public bool IsPending => _action is not null;

    public int RestartCount { get; private set; }

    public void Restart(Action action)
    {
        RestartCount++;
        _action = action;
    }

    public void Cancel() => _action = null;

    public void Fire()
    {
        var action = _action;
        _action = null;
        action?.Invoke();
    }

    public void Dispose() => Cancel();
}

/// <summary>One STA thread with a running Dispatcher for headless WPF work (no windows). Exceptions propagate to the caller.</summary>
internal static class WpfThread
{
    private static readonly Lazy<Dispatcher> Instance = new(Start);

    public static void Run(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Instance.Value.Invoke(action);
    }

    public static T Run<T>(Func<T> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        return Instance.Value.Invoke(func);
    }

    /// <summary>Creates the WPF Application (once per process) with the app theme merged so StaticResource lookups resolve.</summary>
    public static void EnsureApplication()
    {
        if (Application.Current is not null)
        {
            return;
        }

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/CrosshairManager;component/Themes/Theme.xaml", UriKind.Absolute),
        });
    }

    private static Dispatcher Start()
    {
        var ready = new TaskCompletionSource<Dispatcher>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            ready.SetResult(Dispatcher.CurrentDispatcher);
            Dispatcher.Run();
        })
        {
            IsBackground = true,
            Name = "WpfTestThread",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return ready.Task.GetAwaiter().GetResult();
    }
}
