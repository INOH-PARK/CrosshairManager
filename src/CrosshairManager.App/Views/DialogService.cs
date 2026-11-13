using System.Windows;
using CrosshairManager.App.Localization;
using CrosshairManager.App.ViewModels;
using Microsoft.Win32;

namespace CrosshairManager.App.Views;

/// <summary>Modal dialogs owned by the settings window, the *.json file pickers and the clipboard.</summary>
public sealed class DialogService(Window owner) : IDialogService
{
    public string? PromptRename(string currentName)
    {
        var dialog = new RenameDialog(currentName) { Owner = owner };
        return dialog.ShowDialog() == true ? dialog.NewName : null;
    }

    public bool ConfirmDelete(string name)
    {
        var dialog = new ConfirmDeleteDialog(name) { Owner = owner };
        return dialog.ShowDialog() == true;
    }

    public string? PickImportFile()
    {
        var dialog = new OpenFileDialog { Filter = Loc.Instance["Preset.FileFilter"], DefaultExt = ".json", CheckFileExists = true };
        return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
    }

    public string? PickExportFile(string suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            Filter = Loc.Instance["Preset.FileFilter"],
            DefaultExt = ".json",
            AddExtension = true,
            FileName = suggestedFileName,
        };
        return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
    }

    public void SetClipboardText(string text) => Clipboard.SetDataObject(text, copy: true);
}
