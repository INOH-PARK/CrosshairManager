using CommunityToolkit.Mvvm.ComponentModel;
using CrosshairManager.App.Localization;
using CrosshairManager.Core.Model;
using CrosshairManager.Core.Presets;

namespace CrosshairManager.App.ViewModels;

/// <summary>One PresetList row: thumbnail spec, display name (built-in names are resx keys) and status.</summary>
public sealed partial class PresetItemViewModel : ObservableObject
{
    public PresetItemViewModel(PresetEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Id = entry.Id;
        Name = entry.Id;
        Update(entry);
    }

    public string Id { get; }

    public bool IsBuiltIn => BuiltInPresets.IsBuiltInId(Id);

    /// <summary>AutomationId of the Simple-tab tile for this preset.</summary>
    public string TileAutomationId => "Tile." + Id;

    /// <summary>AutomationId of the PresetList row for this preset.</summary>
    public string RowAutomationId => "Preset." + Id;

    [ObservableProperty]
    public partial CrosshairSpec? Spec { get; set; }

    /// <summary>Raw name: a resx key for built-ins, the typed name for user presets, the file id for unusable files.</summary>
    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial string? StatusKey { get; set; }

    /// <summary>The shell's active preset (checked tile / list row).</summary>
    [ObservableProperty]
    public partial bool IsActive { get; set; }

    public bool IsUsable => Spec is not null;

    public string DisplayName => IsBuiltIn ? Loc.Instance[Name] : Name;

    public string Status => StatusKey is null ? string.Empty : Loc.Instance[StatusKey];

    public bool HasStatus => StatusKey is not null;

    public void Update(PresetEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var preset = entry.Preset;
        Spec = preset?.Crosshair;
        Name = preset?.Name ?? entry.Id;
        StatusKey = entry.Result switch
        {
            PresetLoadResult.Broken => "Preset.Status.Broken",
            PresetLoadResult.NewerVersion => "Preset.Status.NewerVersion",
            _ => IsBuiltIn ? "Preset.Status.BuiltIn" : null,
        };
        RefreshText();
    }

    public void RefreshText()
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(HasStatus));
        OnPropertyChanged(nameof(IsUsable));
    }
}
