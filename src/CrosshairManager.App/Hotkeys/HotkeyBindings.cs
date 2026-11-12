using CrosshairManager.Core.Settings;

namespace CrosshairManager.App.Hotkeys;

/// <summary>Registration result for one hotkey id. Unbound: no gesture, not registered, error 0.</summary>
public sealed record HotkeyState(HotkeyGesture? Gesture, bool Registered, int ErrorCode);

/// <summary>Maps the stable hotkey ids to <see cref="HotkeySettings"/> properties.</summary>
public static class HotkeyBindings
{
    public const string ToggleOverlay = "toggleOverlay";
    public const string NextPreset = "nextPreset";
    public const string PreviousPreset = "previousPreset";
    public const string OpenSettings = "openSettings";

    public static IReadOnlyList<string> Ids { get; } = [ToggleOverlay, NextPreset, PreviousPreset, OpenSettings];

    public static HotkeyGesture? Get(HotkeySettings hotkeys, string id)
    {
        ArgumentNullException.ThrowIfNull(hotkeys);
        return id switch
        {
            ToggleOverlay => hotkeys.ToggleOverlay,
            NextPreset => hotkeys.NextPreset,
            PreviousPreset => hotkeys.PreviousPreset,
            OpenSettings => hotkeys.OpenSettings,
            _ => throw new ArgumentException($"Unknown hotkey id '{id}'.", nameof(id)),
        };
    }

    public static HotkeySettings With(HotkeySettings hotkeys, string id, HotkeyGesture? gesture)
    {
        ArgumentNullException.ThrowIfNull(hotkeys);
        return id switch
        {
            ToggleOverlay => hotkeys with { ToggleOverlay = gesture },
            NextPreset => hotkeys with { NextPreset = gesture },
            PreviousPreset => hotkeys with { PreviousPreset = gesture },
            OpenSettings => hotkeys with { OpenSettings = gesture },
            _ => throw new ArgumentException($"Unknown hotkey id '{id}'.", nameof(id)),
        };
    }
}
