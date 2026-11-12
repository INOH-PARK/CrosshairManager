namespace CrosshairManager.Core.Settings;

/// <summary>App-wide settings persisted in <c>settings.json</c>.</summary>
public sealed record AppSettings
{
    public static AppSettings Default { get; } = new();

    public string ActivePresetId { get; init; } = "builtin.default";

    public bool OverlayVisible { get; init; } = true;

    public MonitorSelection Monitor { get; init; } = MonitorSelection.Primary;

    public HotkeySettings Hotkeys { get; init; } = HotkeySettings.Default;

    /// <summary>"system", "ko" or "en".</summary>
    public string Language { get; init; } = "system";

    public bool StartWithWindows { get; init; }

    public bool DisclaimerAccepted { get; init; }

    public WindowPlacement? SettingsWindowPlacement { get; init; }
}

/// <summary>Global hotkeys; null means unbound.</summary>
public sealed record HotkeySettings
{
    public static HotkeySettings Default { get; } = new();

    public HotkeyGesture? ToggleOverlay { get; init; } = Parse("Ctrl+Alt+Shift+X");

    public HotkeyGesture? NextPreset { get; init; } = Parse("Ctrl+Alt+Shift+Right");

    public HotkeyGesture? PreviousPreset { get; init; } = Parse("Ctrl+Alt+Shift+Left");

    public HotkeyGesture? OpenSettings { get; init; }

    private static HotkeyGesture? Parse(string text) => HotkeyGesture.TryParse(text, out var gesture) ? gesture : null;
}

public sealed record WindowPlacement(int Left, int Top, int Width, int Height);
