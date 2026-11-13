using CommunityToolkit.Mvvm.ComponentModel;
using CrosshairManager.App.Hotkeys;
using CrosshairManager.App.Localization;
using CrosshairManager.Core.Settings;

namespace CrosshairManager.App.ViewModels;

/// <summary>One HotkeyBox: the bound gesture plus whether its last registration failed (HotkeyError).</summary>
public sealed partial class HotkeyViewModel : ObservableObject
{
    private readonly Func<string, HotkeyGesture?, HotkeyState> _rebind;
    private bool _loading;

    public HotkeyViewModel(string id, string labelKey, string automationId, Func<string, HotkeyGesture?, HotkeyState> rebind)
    {
        Id = id;
        LabelKey = labelKey;
        AutomationId = automationId;
        _rebind = rebind;
    }

    /// <summary>One of <see cref="HotkeyBindings.Ids"/>.</summary>
    public string Id { get; }

    public string LabelKey { get; }

    public string AutomationId { get; }

    /// <summary>Localized label (refreshed by the owner on language change).</summary>
    public string Label => Loc.Instance[LabelKey];

    [ObservableProperty]
    public partial HotkeyGesture? Gesture { get; set; }

    [ObservableProperty]
    public partial bool IsFailed { get; set; }

    public void RefreshText() => OnPropertyChanged(nameof(Label));

    /// <summary>Shows the shell state without rebinding.</summary>
    public void Load(HotkeyGesture? saved, HotkeyState? state)
    {
        _loading = true;
        try
        {
            IsFailed = state is { Registered: false, Gesture: not null };
            Gesture = IsFailed ? state!.Gesture : saved;
        }
        finally
        {
            _loading = false;
        }
    }

    partial void OnGestureChanged(HotkeyGesture? value)
    {
        if (_loading)
        {
            return;
        }

        var state = _rebind(Id, value);
        IsFailed = state is { Registered: false, Gesture: not null };
    }
}
