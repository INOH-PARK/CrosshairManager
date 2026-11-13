using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CrosshairManager.App.Hotkeys;
using CrosshairManager.App.Localization;
using CrosshairManager.Core.Model;
using CrosshairManager.Core.Presets;
using CrosshairManager.Core.Settings;
using CrosshairManager.Platform.Monitors;

namespace CrosshairManager.App.ViewModels;

/// <summary>
/// The settings window state on top of <see cref="IAppShell"/>. Edits apply to the overlay immediately; a user preset is
/// saved 400 ms after the last edit; the first edit of a built-in preset creates and selects a user copy
/// (<c>Loc["Preset.CopyName"]</c>). Everything here runs on the UI thread.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject, INotifyDataErrorInfo, IDisposable
{
    public const string MonitorWarningKey = "Monitor.Warning";

    public static readonly TimeSpan AutosaveDelay = TimeSpan.FromMilliseconds(400);

    public static IReadOnlyList<string> SwatchHexes { get; } = ["#00FF00", "#FFFF00", "#00FFFF", "#FF00FF", "#FF0000", "#FFFFFF", "#FF8800", "#0080FF"];

    public static IReadOnlyList<int> Rotations { get; } = [0, 45, 90, 135, 180, 225, 270, 315];

    private readonly IAppShell _shell;
    private readonly IMonitorCatalog _monitors;
    private readonly IDebounce _autosave;
    private readonly Dictionary<string, string> _errors = new(StringComparer.Ordinal);
    private readonly HashSet<PendingMessage> _seenMessages = [];
    private bool _loading;
    private bool _syncing;
    private bool _refreshing;
    private bool _refreshPending;
    private bool _copyFailed;
    private bool _dirty;
    private bool _monitorWarningDismissed;
    private string? _editingId;
    private Rgb _color;
    private Rgb _outlineColor;
    private CrosshairSpec _spec;

    public SettingsViewModel(IAppShell shell, IDialogService dialogs, IMonitorCatalog monitors, IDebounce autosave)
    {
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(monitors);
        ArgumentNullException.ThrowIfNull(autosave);
        _shell = shell;
        _monitors = monitors;
        _autosave = autosave;
        _spec = CrosshairSpec.Default;
        _color = _spec.Color;
        _outlineColor = _spec.Outline.Color;
        _loading = true; // no change hooks until the line/hotkey view models exist
        ColorHex = _color.ToString();
        OutlineColorHex = _outlineColor.ToString();
        Language = "system";
        ClampWarning = string.Empty;
        PreviewSpec = _spec;

        Presets = new PresetListViewModel(shell, dialogs, FlushPendingSave, ReportError);
        Top = new LineViewModel("Top", OnLineChanged);
        Bottom = new LineViewModel("Bottom", OnLineChanged);
        Left = new LineViewModel("Left", OnLineChanged);
        Right = new LineViewModel("Right", OnLineChanged);
        Lines = [Top, Bottom, Left, Right];
        Swatches = SwatchHexes.Select((hex, i) => new SwatchViewModel(i, Rgb.Parse(hex))).ToList();
        Hotkeys =
        [
            new HotkeyViewModel(HotkeyBindings.ToggleOverlay, "Hotkey.Toggle", "HotkeyToggle", shell.RebindHotkey),
            new HotkeyViewModel(HotkeyBindings.NextPreset, "Hotkey.Next", "HotkeyNext", shell.RebindHotkey),
            new HotkeyViewModel(HotkeyBindings.PreviousPreset, "Hotkey.Prev", "HotkeyPrev", shell.RebindHotkey),
            new HotkeyViewModel(HotkeyBindings.OpenSettings, "Hotkey.OpenSettings", "HotkeyOpenSettings", shell.RebindHotkey),
        ];
        foreach (var hotkey in Hotkeys)
        {
            hotkey.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(HotkeyViewModel.IsFailed))
                {
                    HasHotkeyError = Hotkeys.Any(h => h.IsFailed);
                }
            };
        }

        _loading = false;
        _shell.StateChanged += OnStateChanged;
        _monitors.Changed += OnMonitorsChanged;
        Loc.Instance.PropertyChanged += OnLocChanged;
        LoadFromShell();
    }

    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    public PresetListViewModel Presets { get; }

    public LineViewModel Top { get; }

    public LineViewModel Bottom { get; }

    public LineViewModel Left { get; }

    public LineViewModel Right { get; }

    public IReadOnlyList<LineViewModel> Lines { get; }

    public IReadOnlyList<SwatchViewModel> Swatches { get; }

    public IReadOnlyList<HotkeyViewModel> Hotkeys { get; }

    public ObservableCollection<MonitorOption> MonitorOptions { get; } = [];

    public ObservableCollection<InfoMessageViewModel> Messages { get; } = [];

    public IReadOnlyList<int> RotationChoices => Rotations;

    // ----- color -----
    [ObservableProperty]
    public partial string ColorHex { get; set; }

    [ObservableProperty]
    public partial int ColorR { get; set; }

    [ObservableProperty]
    public partial int ColorG { get; set; }

    [ObservableProperty]
    public partial int ColorB { get; set; }

    // ----- dot -----
    [ObservableProperty]
    public partial bool DotEnabled { get; set; }

    [ObservableProperty]
    public partial int DotSize { get; set; }

    [ObservableProperty]
    public partial int DotOpacity { get; set; }

    // ----- lines -----
    [ObservableProperty]
    public partial bool LinkLines { get; set; }

    [ObservableProperty]
    public partial bool TStyle { get; set; }

    // ----- outline -----
    [ObservableProperty]
    public partial bool OutlineEnabled { get; set; }

    [ObservableProperty]
    public partial int OutlineThickness { get; set; }

    [ObservableProperty]
    public partial string OutlineColorHex { get; set; }

    [ObservableProperty]
    public partial int OutlineOpacity { get; set; }

    // ----- transform -----
    [ObservableProperty]
    public partial int Opacity { get; set; }

    [ObservableProperty]
    public partial int ScalePercent { get; set; }

    [ObservableProperty]
    public partial int Rotation { get; set; }

    [ObservableProperty]
    public partial int OffsetX { get; set; }

    [ObservableProperty]
    public partial int OffsetY { get; set; }

    // ----- display / general -----
    [ObservableProperty]
    public partial MonitorOption? SelectedMonitor { get; set; }

    [ObservableProperty]
    public partial bool MonitorFellBack { get; set; }

    [ObservableProperty]
    public partial string Language { get; set; }

    [ObservableProperty]
    public partial bool StartWithWindows { get; set; }

    [ObservableProperty]
    public partial bool OverlayVisible { get; set; }

    [ObservableProperty]
    public partial bool HasHotkeyError { get; set; }

    // ----- preview / info bar -----
    [ObservableProperty]
    public partial CrosshairSpec PreviewSpec { get; set; }

    [ObservableProperty]
    public partial string ClampWarning { get; set; }

    [ObservableProperty]
    public partial InfoMessageViewModel? CurrentMessage { get; set; }

    public bool HasClampWarning => ClampWarning.Length > 0;

    public bool HasMessage => CurrentMessage is not null;

    /// <summary>The InfoBar's main line: the newest message, else the always-present fullscreen notice.</summary>
    public string InfoBarText => CurrentMessage?.Text ?? Loc.Instance["InfoBar.Fullscreen"];

    public bool HasErrors => _errors.Count > 0;

    /// <summary>UIA ItemStatus for the ColorHex box: "valid" or "invalid".</summary>
    public string ColorHexStatus => _errors.ContainsKey(nameof(ColorHex)) ? "invalid" : "valid";

    public string ColorHexHelp => _errors.GetValueOrDefault(nameof(ColorHex)) ?? string.Empty;

    public string OutlineColorHexStatus => _errors.ContainsKey(nameof(OutlineColorHex)) ? "invalid" : "valid";

    public string OutlineColorHexHelp => _errors.GetValueOrDefault(nameof(OutlineColorHex)) ?? string.Empty;

    /// <summary>The spec the editor currently shows (what the overlay is showing).</summary>
    public CrosshairSpec CurrentSpec => _spec;

    public string? EditingPresetId => _editingId;

    public bool IsSavePending => _dirty && _autosave.IsPending;

    public bool NeedsDisclaimer => !_shell.Settings.DisclaimerAccepted;

    public WindowPlacement? Placement => _shell.Settings.SettingsWindowPlacement;

    public IEnumerable GetErrors(string? propertyName) =>
        propertyName is not null && _errors.TryGetValue(propertyName, out var error) ? new[] { error } : Array.Empty<string>();

    public void AcceptDisclaimer()
    {
        _shell.Settings = _shell.Settings with { DisclaimerAccepted = true };
        _shell.SaveSettings();
    }

    public void SavePlacement(WindowPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        if (placement.Equals(_shell.Settings.SettingsWindowPlacement))
        {
            return;
        }

        _shell.Settings = _shell.Settings with { SettingsWindowPlacement = placement };
        _shell.SaveSettings();
    }

    /// <summary>Saves a pending edit now (preset switch, window hide, exit).</summary>
    public void FlushPendingSave()
    {
        _autosave.Cancel();
        SaveNow();
    }

    public void Dispose()
    {
        _shell.StateChanged -= OnStateChanged;
        _monitors.Changed -= OnMonitorsChanged;
        Loc.Instance.PropertyChanged -= OnLocChanged;
        _autosave.Dispose();
    }

    [RelayCommand]
    private void SelectSwatch(SwatchViewModel swatch)
    {
        ArgumentNullException.ThrowIfNull(swatch);
        ColorHex = swatch.Hex;
    }

    [RelayCommand]
    private void SelectPreset(PresetItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        Presets.Select(item.Id);
    }

    [RelayCommand]
    private void Dismiss()
    {
        if (CurrentMessage is null)
        {
            return;
        }

        if (CurrentMessage.Key == MonitorWarningKey)
        {
            _monitorWarningDismissed = true;
        }

        Messages.Remove(CurrentMessage);
        CurrentMessage = Messages.LastOrDefault();
    }

    // ----- change hooks -----
    partial void OnColorHexChanged(string value)
    {
        if (_loading)
        {
            return;
        }

        if (!Rgb.TryParse(value, out var rgb))
        {
            SetError(nameof(ColorHex), Loc.Instance["Color.Invalid"]);
            return;
        }

        ClearError(nameof(ColorHex));
        if (_syncing)
        {
            return;
        }

        _color = rgb;
        _syncing = true;
        try
        {
            ColorR = rgb.R;
            ColorG = rgb.G;
            ColorB = rgb.B;
        }
        finally
        {
            _syncing = false;
        }

        OnFieldChanged();
    }

    partial void OnColorRChanged(int value) => OnChannelChanged();

    partial void OnColorGChanged(int value) => OnChannelChanged();

    partial void OnColorBChanged(int value) => OnChannelChanged();

    partial void OnOutlineColorHexChanged(string value)
    {
        if (_loading)
        {
            return;
        }

        if (!Rgb.TryParse(value, out var rgb))
        {
            SetError(nameof(OutlineColorHex), Loc.Instance["Color.Invalid"]);
            return;
        }

        ClearError(nameof(OutlineColorHex));
        _outlineColor = rgb;
        OnFieldChanged();
    }

    partial void OnDotEnabledChanged(bool value) => OnFieldChanged();

    partial void OnDotSizeChanged(int value) => OnFieldChanged();

    partial void OnDotOpacityChanged(int value) => OnFieldChanged();

    partial void OnOutlineEnabledChanged(bool value) => OnFieldChanged();

    partial void OnOutlineThicknessChanged(int value) => OnFieldChanged();

    partial void OnOutlineOpacityChanged(int value) => OnFieldChanged();

    partial void OnOpacityChanged(int value) => OnFieldChanged();

    partial void OnScalePercentChanged(int value) => OnFieldChanged();

    partial void OnRotationChanged(int value) => OnFieldChanged();

    partial void OnOffsetXChanged(int value) => OnFieldChanged();

    partial void OnOffsetYChanged(int value) => OnFieldChanged();

    partial void OnLinkLinesChanged(bool value)
    {
        if (_loading || !value)
        {
            return;
        }

        var source = Lines.FirstOrDefault(l => l.Enabled) ?? Top;
        _syncing = true;
        try
        {
            foreach (var line in Lines)
            {
                if (line != source)
                {
                    line.CopyValuesFrom(source);
                }
            }
        }
        finally
        {
            _syncing = false;
        }

        OnFieldChanged();
    }

    partial void OnTStyleChanged(bool value)
    {
        if (_loading || _syncing)
        {
            return;
        }

        Top.Enabled = !value;
    }

    partial void OnSelectedMonitorChanged(MonitorOption? value)
    {
        if (_loading || value is null)
        {
            return;
        }

        _shell.SetMonitor(value.Selection);
        MonitorFellBack = _shell.MonitorFellBack;
    }

    partial void OnMonitorFellBackChanged(bool value)
    {
        if (!value)
        {
            _monitorWarningDismissed = false;
        }
    }

    partial void OnLanguageChanged(string value)
    {
        if (_loading)
        {
            return;
        }

        Loc.Instance.SetLanguage(value);
        _shell.Settings = _shell.Settings with { Language = value };
        _shell.SaveSettings();
    }

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (_loading)
        {
            return;
        }

        if (!_shell.SetStartWithWindows(value))
        {
            _loading = true;
            try
            {
                StartWithWindows = !value;
            }
            finally
            {
                _loading = false;
            }

            RefreshMessages();
        }
    }

    partial void OnOverlayVisibleChanged(bool value)
    {
        if (!_loading)
        {
            _shell.SetOverlayVisible(value);
        }
    }

    partial void OnClampWarningChanged(string value) => OnPropertyChanged(nameof(HasClampWarning));

    partial void OnCurrentMessageChanged(InfoMessageViewModel? value)
    {
        OnPropertyChanged(nameof(InfoBarText));
        OnPropertyChanged(nameof(HasMessage));
    }

    // ----- edit pipeline -----
    private void OnChannelChanged()
    {
        if (_loading || _syncing)
        {
            return;
        }

        _color = new Rgb(Clamp8(ColorR), Clamp8(ColorG), Clamp8(ColorB));
        _syncing = true;
        try
        {
            ColorHex = _color.ToString();
        }
        finally
        {
            _syncing = false;
        }

        OnFieldChanged();
    }

    private void OnLineChanged(LineViewModel line)
    {
        if (_loading || _syncing)
        {
            return;
        }

        _syncing = true;
        try
        {
            if (LinkLines)
            {
                foreach (var other in Lines)
                {
                    if (other != line)
                    {
                        other.CopyValuesFrom(line);
                    }
                }
            }

            if (line == Top)
            {
                TStyle = !Top.Enabled;
            }
        }
        finally
        {
            _syncing = false;
        }

        OnFieldChanged();
    }

    private void OnFieldChanged()
    {
        if (_loading)
        {
            return;
        }

        _spec = BuildSpec();
        UpdatePreview();
        _shell.Apply(_spec);
        foreach (var swatch in Swatches)
        {
            swatch.IsSelected = swatch.Color == _color;
        }

        if (!EnsureEditableTarget())
        {
            return;
        }

        _dirty = true;
        _autosave.Restart(SaveNow);
    }

    /// <summary>A built-in preset gets a user copy (made from the current spec) on its first edit; false when nothing can be saved.</summary>
    private bool EnsureEditableTarget()
    {
        if (_editingId is null)
        {
            return false;
        }

        if (!BuiltInPresets.IsBuiltInId(_editingId))
        {
            return true;
        }

        if (_copyFailed)
        {
            return false;
        }

        var source = FindEntry(_editingId)?.Preset;
        var name = Loc.Instance.Format("Preset.CopyName", source is null ? _editingId : Loc.Instance[source.Name]);
        try
        {
            var copy = _shell.PresetStore.Create(name, _spec);
            _editingId = copy.Id; // before the shell events so the refresh does not reload the fields
            _shell.ReloadPresets();
            _shell.SelectPreset(copy.Id);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _copyFailed = true;
            ReportError("Error.PresetSaveFailed", ex.Message);
            return false;
        }
    }

    private void SaveNow()
    {
        if (!_dirty || _editingId is null || BuiltInPresets.IsBuiltInId(_editingId))
        {
            return;
        }

        _dirty = false;
        var preset = FindEntry(_editingId)?.Preset;
        if (preset is null)
        {
            return;
        }

        try
        {
            var saved = _shell.PresetStore.Save(preset with { Crosshair = _spec });
            _shell.ReloadPresets();
            if (saved.Crosshair != _spec)
            {
                // The store normalized (clamped) values: show what was written.
                _spec = saved.Crosshair;
                LoadFields(_spec);
                UpdatePreview();
                _shell.Apply(_spec);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            ReportError("Error.PresetSaveFailed", ex.Message);
        }
    }

    private CrosshairSpec BuildSpec() => new()
    {
        Color = _color,
        Opacity = Opacity,
        ScalePercent = ScalePercent,
        RotationDeg = Rotation,
        OffsetX = OffsetX,
        OffsetY = OffsetY,
        Dot = new DotSpec(DotEnabled, DotSize, DotOpacity),
        Top = Top.ToSpec(),
        Bottom = Bottom.ToSpec(),
        Left = Left.ToSpec(),
        Right = Right.ToSpec(),
        Outline = new OutlineSpec(OutlineEnabled, OutlineThickness, _outlineColor, OutlineOpacity),
    };

    private void UpdatePreview()
    {
        var (normalized, warnings) = _spec.Normalize();
        PreviewSpec = normalized;
        ClampWarning = warnings.Count == 0 ? string.Empty : Loc.Instance.Format("Editor.Clamped", string.Join("; ", warnings));
    }

    // ----- shell state -----
    private void OnStateChanged(object? sender, EventArgs e) => LoadFromShell();

    private void OnMonitorsChanged(object? sender, EventArgs e) => RefreshMonitors();

    private void LoadFromShell()
    {
        if (_refreshing)
        {
            _refreshPending = true;
            return;
        }

        _refreshing = true;
        try
        {
            do
            {
                _refreshPending = false;
                LoadFromShellCore();
            }
            while (_refreshPending);
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void LoadFromShellCore()
    {
        var settings = _shell.Settings;
        Presets.Refresh(_shell.Presets, settings.ActivePresetId);
        if (!string.Equals(settings.ActivePresetId, _editingId, StringComparison.Ordinal))
        {
            FlushPendingSave();
            LoadEditor(settings.ActivePresetId);
        }

        _loading = true;
        try
        {
            OverlayVisible = settings.OverlayVisible;
            StartWithWindows = settings.StartWithWindows;
            Language = settings.Language;
            foreach (var hotkey in Hotkeys)
            {
                hotkey.Load(HotkeyBindings.Get(settings.Hotkeys, hotkey.Id), _shell.HotkeyStatus.GetValueOrDefault(hotkey.Id));
            }
        }
        finally
        {
            _loading = false;
        }

        HasHotkeyError = Hotkeys.Any(h => h.IsFailed);
        RefreshMonitors();
        RefreshMessages();
    }

    private void LoadEditor(string id)
    {
        _editingId = id;
        _copyFailed = false;
        _dirty = false;
        _autosave.Cancel();
        _spec = FindEntry(id)?.Preset?.Crosshair ?? CrosshairSpec.Default;
        LoadFields(_spec);
        UpdatePreview();
    }

    private void LoadFields(CrosshairSpec spec)
    {
        _loading = true;
        try
        {
            _color = spec.Color;
            _outlineColor = spec.Outline.Color;
            ColorHex = _color.ToString();
            ColorR = _color.R;
            ColorG = _color.G;
            ColorB = _color.B;
            DotEnabled = spec.Dot.Enabled;
            DotSize = spec.Dot.Size;
            DotOpacity = spec.Dot.Opacity;
            Top.Load(spec.Top);
            Bottom.Load(spec.Bottom);
            Left.Load(spec.Left);
            Right.Load(spec.Right);
            TStyle = !spec.Top.Enabled;
            OutlineEnabled = spec.Outline.Enabled;
            OutlineThickness = spec.Outline.Thickness;
            OutlineColorHex = _outlineColor.ToString();
            OutlineOpacity = spec.Outline.Opacity;
            Opacity = spec.Opacity;
            ScalePercent = spec.ScalePercent;
            Rotation = (int)Math.Round(spec.RotationDeg);
            OffsetX = spec.OffsetX;
            OffsetY = spec.OffsetY;
            ClearError(nameof(ColorHex));
            ClearError(nameof(OutlineColorHex));
            foreach (var swatch in Swatches)
            {
                swatch.IsSelected = swatch.Color == _color;
            }
        }
        finally
        {
            _loading = false;
        }
    }

    private void RefreshMonitors()
    {
        var selection = _shell.Settings.Monitor;
        var options = new List<MonitorOption> { new(MonitorSelection.Primary, Loc.Instance["Monitor.Primary"]) };
        IReadOnlyList<MonitorInfo> monitors;
        try
        {
            monitors = _monitors.GetMonitors();
        }
        catch (Win32Exception ex)
        {
            _shell.LogError("monitor enumeration failed; listing only the primary monitor", ex);
            monitors = [];
        }

        foreach (var monitor in monitors)
        {
            options.Add(new MonitorOption(
                new MonitorSelection(MonitorMode.Device, monitor.DeviceName),
                Loc.Instance.Format("Monitor.Device", monitor.DeviceName, monitor.Bounds.W, monitor.Bounds.H)));
        }

        if (selection.Mode == MonitorMode.Device && selection.DeviceName is not null && !options.Any(o => o.Selection.Equals(selection)))
        {
            options.Add(new MonitorOption(selection, Loc.Instance.Format("Monitor.NotConnected", selection.DeviceName)));
        }

        _loading = true;
        try
        {
            MonitorOptions.Clear();
            foreach (var option in options)
            {
                MonitorOptions.Add(option);
            }

            SelectedMonitor = options.First(o => o.Selection.Equals(selection));
        }
        finally
        {
            _loading = false;
        }

        MonitorFellBack = _shell.MonitorFellBack;
    }

    private void RefreshMessages()
    {
        foreach (var message in _shell.PendingMessages)
        {
            if (_seenMessages.Add(message))
            {
                Messages.Add(new InfoMessageViewModel(message.Key, message.Detail));
            }
        }

        var warning = Messages.FirstOrDefault(m => m.Key == MonitorWarningKey);
        if (MonitorFellBack && warning is null && !_monitorWarningDismissed)
        {
            Messages.Add(new InfoMessageViewModel(MonitorWarningKey, null));
        }
        else if (!MonitorFellBack && warning is not null)
        {
            Messages.Remove(warning);
        }

        CurrentMessage = Messages.LastOrDefault();
    }

    private void ReportError(string key, string? detail)
    {
        Messages.Add(new InfoMessageViewModel(key, detail));
        CurrentMessage = Messages[^1];
    }

    private void OnLocChanged(object? sender, PropertyChangedEventArgs e)
    {
        Presets.RefreshText();
        foreach (var hotkey in Hotkeys)
        {
            hotkey.RefreshText();
        }

        foreach (var message in Messages)
        {
            message.RefreshText();
        }

        OnPropertyChanged(nameof(InfoBarText));
        if (_errors.ContainsKey(nameof(ColorHex)))
        {
            SetError(nameof(ColorHex), Loc.Instance["Color.Invalid"]);
        }

        if (_errors.ContainsKey(nameof(OutlineColorHex)))
        {
            SetError(nameof(OutlineColorHex), Loc.Instance["Color.Invalid"]);
        }

        UpdatePreview();
        RefreshMonitors();
    }

    private PresetEntry? FindEntry(string id) =>
        _shell.Presets.FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.Ordinal));

    private void SetError(string property, string message)
    {
        _errors[property] = message;
        RaiseErrorsChanged(property);
    }

    private void ClearError(string property)
    {
        if (_errors.Remove(property))
        {
            RaiseErrorsChanged(property);
        }
    }

    private void RaiseErrorsChanged(string property)
    {
        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(property));
        OnPropertyChanged(nameof(HasErrors));
        OnPropertyChanged(nameof(ColorHexStatus));
        OnPropertyChanged(nameof(ColorHexHelp));
        OnPropertyChanged(nameof(OutlineColorHexStatus));
        OnPropertyChanged(nameof(OutlineColorHexHelp));
    }

    private static byte Clamp8(int value) => (byte)Math.Clamp(value, 0, 255);
}
