using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CrosshairManager.App.Localization;
using CrosshairManager.Core.Model;
using CrosshairManager.Core.Presets;

namespace CrosshairManager.App.ViewModels;

/// <summary>
/// The preset pane: list (PresetList), New/Duplicate/Rename/Delete, Import/Export (*.json), CopyShareCode and
/// ShareCodeInput/PasteShareCode/ShareCodeError. Selecting a usable row makes it the shell's active preset.
/// </summary>
public sealed partial class PresetListViewModel : ObservableObject
{
    private readonly IAppShell _shell;
    private readonly IDialogService _dialogs;
    private readonly Action _beforeChange;
    private readonly Action<string, string?> _report;
    private bool _syncing;

    public PresetListViewModel(IAppShell shell, IDialogService dialogs, Action beforeChange, Action<string, string?> report)
    {
        _shell = shell;
        _dialogs = dialogs;
        _beforeChange = beforeChange;
        _report = report;
        ShareCodeInput = string.Empty;
        ShareCodeError = string.Empty;
    }

    public ObservableCollection<PresetItemViewModel> Items { get; } = [];

    /// <summary>The built-in rows of <see cref="Items"/> (Simple tab tiles).</summary>
    public ObservableCollection<PresetItemViewModel> BuiltIns { get; } = [];

    [ObservableProperty]
    public partial PresetItemViewModel? SelectedItem { get; set; }

    [ObservableProperty]
    public partial string ShareCodeInput { get; set; }

    [ObservableProperty]
    public partial string ShareCodeError { get; set; }

    public bool HasShareCodeError => ShareCodeError.Length > 0;

    public bool IsSelectedUsable => SelectedItem is { IsUsable: true };

    public bool IsSelectedEditable => SelectedItem is { IsUsable: true, IsBuiltIn: false };

    public bool IsSelectedDeletable => SelectedItem is { IsBuiltIn: false };

    /// <summary>Rebuilds (or updates in place) from the shell and selects the active preset without re-selecting it in the shell.</summary>
    public void Refresh(IReadOnlyList<PresetEntry> entries, string activeId)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _syncing = true;
        try
        {
            if (entries.Count == Items.Count && entries.Select(e => e.Id).SequenceEqual(Items.Select(i => i.Id), StringComparer.Ordinal))
            {
                for (var i = 0; i < entries.Count; i++)
                {
                    Items[i].Update(entries[i]);
                }
            }
            else
            {
                Items.Clear();
                BuiltIns.Clear();
                foreach (var entry in entries)
                {
                    var item = new PresetItemViewModel(entry);
                    Items.Add(item);
                    if (item.IsBuiltIn)
                    {
                        BuiltIns.Add(item);
                    }
                }
            }

            foreach (var item in Items)
            {
                item.IsActive = string.Equals(item.Id, activeId, StringComparison.Ordinal);
            }

            SelectedItem = Items.FirstOrDefault(i => i.IsActive);
        }
        finally
        {
            _syncing = false;
        }

        NotifySelectionDependents();
    }

    public void RefreshText()
    {
        foreach (var item in Items)
        {
            item.RefreshText();
        }
    }

    /// <summary>Selects <paramref name="presetId"/> (Simple tab tiles, tests).</summary>
    public void Select(string presetId)
    {
        ArgumentNullException.ThrowIfNull(presetId);
        var item = Items.FirstOrDefault(i => string.Equals(i.Id, presetId, StringComparison.Ordinal));
        if (item is not null)
        {
            SelectedItem = item;
        }
    }

    partial void OnSelectedItemChanged(PresetItemViewModel? value)
    {
        NotifySelectionDependents();
        if (_syncing || value is null || !value.IsUsable
            || string.Equals(value.Id, _shell.Settings.ActivePresetId, StringComparison.Ordinal))
        {
            return;
        }

        _beforeChange();
        _shell.SelectPreset(value.Id);
    }

    partial void OnShareCodeErrorChanged(string value) => OnPropertyChanged(nameof(HasShareCodeError));

    [RelayCommand]
    private void New()
    {
        _beforeChange();
        Run(() =>
        {
            var preset = _shell.PresetStore.Create(Loc.Instance["Preset.NewName"], CrosshairSpec.Default);
            _shell.ReloadPresets();
            _shell.SelectPreset(preset.Id);
        });
    }

    [RelayCommand(CanExecute = nameof(IsSelectedUsable))]
    private void Duplicate()
    {
        var source = SelectedItem!;
        _beforeChange();
        Run(() =>
        {
            var copy = _shell.PresetStore.Duplicate(source.Id, Loc.Instance.Format("Preset.CopyName", source.DisplayName));
            _shell.ReloadPresets();
            _shell.SelectPreset(copy.Id);
        });
    }

    [RelayCommand(CanExecute = nameof(IsSelectedEditable))]
    private void Rename()
    {
        var target = SelectedItem!;
        var name = _dialogs.PromptRename(target.DisplayName);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        _beforeChange();
        Run(() =>
        {
            _shell.PresetStore.Rename(target.Id, name.Trim());
            _shell.ReloadPresets();
        });
    }

    [RelayCommand(CanExecute = nameof(IsSelectedDeletable))]
    private void Delete()
    {
        var target = SelectedItem!;
        if (!_dialogs.ConfirmDelete(target.DisplayName))
        {
            return;
        }

        _beforeChange();
        Run(() =>
        {
            var wasActive = string.Equals(target.Id, _shell.Settings.ActivePresetId, StringComparison.Ordinal);
            _shell.PresetStore.Delete(target.Id);
            _shell.ReloadPresets();
            if (wasActive)
            {
                _shell.SelectPreset(BuiltInPresets.All[0].Id);
            }
        });
    }

    [RelayCommand]
    private void ImportPresetFile()
    {
        var path = _dialogs.PickImportFile();
        if (path is null)
        {
            return;
        }

        _beforeChange();
        Run(() =>
        {
            var preset = _shell.PresetStore.ImportFile(path);
            _shell.ReloadPresets();
            _shell.SelectPreset(preset.Id);
        }, "Error.PresetImportFailed");
    }

    [RelayCommand(CanExecute = nameof(IsSelectedUsable))]
    private void ExportPresetFile()
    {
        var source = SelectedItem!;
        var path = _dialogs.PickExportFile(SafeFileName(source.DisplayName) + ".json");
        if (path is null)
        {
            return;
        }

        _beforeChange();
        Run(() => _shell.PresetStore.ExportFile(source.Id, path), "Error.PresetExportFailed");
    }

    [RelayCommand(CanExecute = nameof(IsSelectedUsable))]
    private void CopyShareCode()
    {
        var id = SelectedItem!.Id;
        _beforeChange();
        var preset = _shell.Presets.FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.Ordinal))?.Preset;
        if (preset is null)
        {
            return;
        }

        // Built-in names are resx keys: share the localized name.
        var name = preset.IsBuiltIn ? Loc.Instance[preset.Name] : preset.Name;
        _dialogs.SetClipboardText(ShareCode.Encode(name, preset.Crosshair));
    }

    [RelayCommand]
    private void PasteShareCode()
    {
        var text = ShareCodeInput.Trim();
        if (text.Length == 0)
        {
            return;
        }

        if (!ShareCode.TryDecode(text, out var name, out var spec, out _, out var errorKey))
        {
            ShareCodeError = Loc.Instance[errorKey];
            return;
        }

        ShareCodeError = string.Empty;
        _beforeChange();
        Run(() =>
        {
            var preset = _shell.PresetStore.Create(name.Length > 0 ? name : Loc.Instance["Preset.ImportedName"], spec);
            _shell.ReloadPresets();
            _shell.SelectPreset(preset.Id);
            ShareCodeInput = string.Empty;
        });
    }

    private void NotifySelectionDependents()
    {
        OnPropertyChanged(nameof(IsSelectedUsable));
        OnPropertyChanged(nameof(IsSelectedEditable));
        OnPropertyChanged(nameof(IsSelectedDeletable));
        DuplicateCommand.NotifyCanExecuteChanged();
        RenameCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
        ExportPresetFileCommand.NotifyCanExecuteChanged();
        CopyShareCodeCommand.NotifyCanExecuteChanged();
    }

    private void Run(Action action, string errorKey = "Error.PresetOperationFailed")
    {
        try
        {
            action();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException
                                       or InvalidOperationException or ArgumentException or KeyNotFoundException)
        {
            _report(errorKey, ex.Message);
        }
    }

    private static string SafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return cleaned.Length == 0 ? "preset" : cleaned;
    }
}
