namespace CrosshairManager.Ui.Tests;

/// <summary>Fixed AutomationIds from docs/contracts.md. Conditional ones (dialogs, errors, warnings, Tray*) are listed separately.</summary>
internal static class FixedIds
{
    public static readonly string[] AlwaysVisible =
    [
        "PresetList", "NewPreset", "DuplicatePreset", "RenamePreset", "DeletePreset", "ImportPresetFile", "ExportPresetFile",
        "CopyShareCode", "ShareCodeInput", "PasteShareCode", "TabSimple", "TabAdvanced",
        "Swatch0", "Swatch1", "Swatch2", "Swatch3", "Swatch4", "Swatch5", "Swatch6", "Swatch7", "SimpleScale", "SimpleOpacity", "SimpleOutline",
        "ColorHex", "ColorR", "ColorG", "ColorB", "DotEnabled", "DotSize", "DotOpacity", "LinkLines", "TStyle",
        "Top.Enabled", "Top.Length", "Top.Thickness", "Top.Gap",
        "Bottom.Enabled", "Bottom.Length", "Bottom.Thickness", "Bottom.Gap",
        "Left.Enabled", "Left.Length", "Left.Thickness", "Left.Gap",
        "Right.Enabled", "Right.Length", "Right.Thickness", "Right.Gap",
        "OutlineEnabled", "OutlineThickness", "OutlineColorHex", "OutlineOpacity", "Opacity", "Scale", "Rotation", "OffsetX", "OffsetY",
        "MonitorSelect", "HotkeyToggle", "HotkeyNext", "HotkeyPrev", "HotkeyOpenSettings", "Language", "StartWithWindows", "OverlayVisible",
        "Preview", "InfoBar",
    ];

    public static readonly string[] Conditional =
    [
        "DisclaimerDialog", "DisclaimerOk", "RenameText", "RenameOk", "ConfirmDeleteOk", "ConfirmDeleteCancel",
        "ShareCodeError", "MonitorWarning", "HotkeyError",
    ];
}
