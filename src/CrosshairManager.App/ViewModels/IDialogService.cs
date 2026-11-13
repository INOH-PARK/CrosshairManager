namespace CrosshairManager.App.ViewModels;

/// <summary>Modal prompts and OS services the view models need; the window implements it, tests fake it.</summary>
public interface IDialogService
{
    /// <summary>RenameText/RenameOk dialog; null when cancelled.</summary>
    string? PromptRename(string currentName);

    /// <summary>ConfirmDeleteOk/ConfirmDeleteCancel dialog.</summary>
    bool ConfirmDelete(string name);

    /// <summary>OpenFileDialog filtered to *.json; null when cancelled.</summary>
    string? PickImportFile();

    /// <summary>SaveFileDialog filtered to *.json; null when cancelled.</summary>
    string? PickExportFile(string suggestedFileName);

    void SetClipboardText(string text);
}
