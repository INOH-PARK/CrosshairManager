using CommunityToolkit.Mvvm.ComponentModel;
using CrosshairManager.App.Localization;

namespace CrosshairManager.App.ViewModels;

/// <summary>An InfoBar row: resx key (localized on demand) plus optional untranslated detail.</summary>
public sealed partial class InfoMessageViewModel(string key, string? detail) : ObservableObject
{
    public string Key { get; } = key;

    public string? Detail { get; } = detail;

    public string Text => Loc.Instance[Key];

    public bool HasDetail => !string.IsNullOrEmpty(Detail);

    public void RefreshText() => OnPropertyChanged(nameof(Text));
}
