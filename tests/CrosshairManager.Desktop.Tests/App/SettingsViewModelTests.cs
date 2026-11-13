using System.IO;
using System.Text.Json.Nodes;
using System.Windows.Input;
using CrosshairManager.App;
using CrosshairManager.App.Controls;
using CrosshairManager.App.Hotkeys;
using CrosshairManager.App.Localization;
using CrosshairManager.App.ViewModels;
using CrosshairManager.Core.Model;
using CrosshairManager.Core.Presets;
using CrosshairManager.Core.Settings;

namespace CrosshairManager.Desktop.Tests.App;

/// <summary>SettingsViewModel over a fake shell and a real PresetStore in a temp folder. No windows.</summary>
[Trait("Category", "Desktop")]
public sealed class SettingsViewModelTests : IDisposable
{
    private readonly AppTestDir _dir = new();
    private readonly FakeAppShell _shell;
    private readonly FakeDialogs _dialogs = new();
    private readonly FakeMonitors _monitors = new();
    private readonly ManualDebounce _debounce = new();
    private readonly SettingsViewModel _vm;

    public SettingsViewModelTests()
    {
        try
        {
            _shell = new FakeAppShell(_dir.Path);
            _vm = new SettingsViewModel(_shell, _dialogs, _monitors, _debounce);
        }
        catch
        {
            // xunit does not call Dispose when the constructor throws: do not leak the temp folder.
            _dir.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        _vm.Dispose();
        _dir.Dispose();
    }

    private JsonNode PresetJson(string id) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(_shell.PresetStore.PresetsDir, id + ".json")))!;

    private Preset SelectNewUserPreset(string name = "mine")
    {
        var preset = _shell.PresetStore.Create(name, CrosshairSpec.Default);
        _shell.ReloadPresets();
        _shell.SelectPreset(preset.Id);
        return preset;
    }

    [Fact]
    public void EditingBuiltIn_AppliesLive_CreatesLocalizedCopy_AndSelectsIt()
    {
        Assert.Equal("builtin.default", _vm.EditingPresetId);
        _vm.DotSize = 5;

        Assert.Equal(5, _shell.Applied.Last().Dot.Size);
        var active = _shell.Settings.ActivePresetId;
        Assert.False(BuiltInPresets.IsBuiltInId(active));
        Assert.Equal(active, _vm.EditingPresetId);
        Assert.Equal(active, _vm.Presets.SelectedItem?.Id);
        var json = PresetJson(active);
        Assert.Equal(Loc.Instance.Format("Preset.CopyName", Loc.Instance["Preset.builtin.default"]), json["name"]!.GetValue<string>());
        Assert.Equal(5, json["crosshair"]!["dot"]!["size"]!.GetValue<int>());
        Assert.Equal(5, _vm.DotSize);
    }

    [Fact]
    public void EditingUserPreset_AppliesImmediately_ButSavesOnlyAfterDebounce()
    {
        var preset = SelectNewUserPreset();
        Assert.Equal(preset.Id, _vm.EditingPresetId);

        _vm.DotSize = 7;
        Assert.Equal(7, _shell.Applied.Last().Dot.Size);
        Assert.True(_debounce.IsPending);
        Assert.Equal(2, PresetJson(preset.Id)["crosshair"]!["dot"]!["size"]!.GetValue<int>());

        _debounce.Fire();
        Assert.False(_debounce.IsPending);
        Assert.Equal(7, PresetJson(preset.Id)["crosshair"]!["dot"]!["size"]!.GetValue<int>());
        Assert.Equal(preset.Id, _shell.Settings.ActivePresetId);
    }

    [Fact]
    public void FlushPendingSave_WritesTheEditNow()
    {
        var preset = SelectNewUserPreset();
        _vm.Opacity = 40;
        _vm.FlushPendingSave();
        Assert.Equal(40, PresetJson(preset.Id)["crosshair"]!["opacity"]!.GetValue<int>());
        Assert.False(_debounce.IsPending);
    }

    [Fact]
    public void OutOfRangeValue_IsClampedOnSave_AndTheFieldFollows()
    {
        var preset = SelectNewUserPreset();
        _vm.DotSize = 999;
        Assert.True(_vm.HasClampWarning);
        Assert.Equal(64, _shell.Applied.Last().Normalize().Spec.Dot.Size);
        _debounce.Fire();
        Assert.Equal(64, PresetJson(preset.Id)["crosshair"]!["dot"]!["size"]!.GetValue<int>());
        Assert.Equal(64, _vm.DotSize);
        Assert.False(_vm.HasClampWarning);
    }

    [Fact]
    public void InvalidHex_SetsValidationState_AndDoesNotApply()
    {
        var applied = _shell.Applied.Count;
        _vm.ColorHex = "#GG0000";

        Assert.True(_vm.HasErrors);
        Assert.Equal("invalid", _vm.ColorHexStatus);
        Assert.Equal(Loc.Instance["Color.Invalid"], _vm.ColorHexHelp);
        Assert.Equal(new[] { Loc.Instance["Color.Invalid"] }, _vm.GetErrors(nameof(SettingsViewModel.ColorHex)).Cast<string>().ToArray());
        Assert.Equal(applied, _shell.Applied.Count);
        Assert.False(_debounce.IsPending);
        Assert.Equal("builtin.default", _shell.Settings.ActivePresetId);

        _vm.ColorHex = "#FF8800";
        Assert.False(_vm.HasErrors);
        Assert.Equal("valid", _vm.ColorHexStatus);
        Assert.Equal(Rgb.Parse("#FF8800"), _shell.Applied.Last().Color);
        Assert.Equal((255, 0x88, 0), (_vm.ColorR, _vm.ColorG, _vm.ColorB));
    }

    [Fact]
    public void Channels_Swatches_AndHex_StayInSync()
    {
        _vm.ColorB = 128;
        Assert.Equal("#00FF80", _vm.ColorHex);
        Assert.Equal(Rgb.Parse("#00FF80"), _shell.Applied.Last().Color);

        _vm.SelectSwatchCommand.Execute(_vm.Swatches[6]);
        Assert.Equal("#FF8800", _vm.ColorHex);
        Assert.True(_vm.Swatches[6].IsSelected);
        Assert.False(_vm.Swatches[0].IsSelected);
        Assert.Equal(SettingsViewModel.SwatchHexes, _vm.Swatches.Select(s => s.Hex));
    }

    [Fact]
    public void LinkLines_PropagatesNumbersNotEnabled_AndTStyleTogglesTop()
    {
        _vm.LinkLines = true;
        _vm.Top.Length = 10;
        Assert.All(_vm.Lines, l => Assert.Equal(10, l.Length));
        _vm.Left.Enabled = false;
        Assert.True(_vm.Right.Enabled);
        Assert.False(_shell.Applied.Last().Left.Enabled);

        _vm.TStyle = true;
        Assert.False(_vm.Top.Enabled);
        Assert.False(_shell.Applied.Last().Top.Enabled);
        _vm.Top.Enabled = true;
        Assert.False(_vm.TStyle);
    }

    [Fact]
    public void StateChanged_WithAnotherActivePreset_ReloadsTheEditor()
    {
        _shell.SelectPreset("builtin.dot");
        Assert.Equal("builtin.dot", _vm.EditingPresetId);
        Assert.Equal(4, _vm.DotSize);
        Assert.All(_vm.Lines, l => Assert.False(l.Enabled));
        Assert.Equal("builtin.dot", _vm.Presets.SelectedItem?.Id);
        Assert.True(_vm.Presets.BuiltIns.Single(b => b.Id == "builtin.dot").IsActive);
    }

    [Fact]
    public void SelectingAListRow_FlushesThePendingEdit_ThenSwitches()
    {
        var mine = SelectNewUserPreset();
        _vm.Opacity = 55;
        Assert.True(_debounce.IsPending);
        _vm.Presets.Select("builtin.cross");
        Assert.Equal(55, PresetJson(mine.Id)["crosshair"]!["opacity"]!.GetValue<int>());
        Assert.Equal("builtin.cross", _shell.Settings.ActivePresetId);
        Assert.False(_vm.Presets.IsSelectedEditable);
        Assert.True(_vm.Presets.IsSelectedUsable);
    }

    [Fact]
    public void Rename_Delete_Duplicate_New_GoThroughStoreAndDialogs()
    {
        var mine = SelectNewUserPreset();
        _dialogs.RenameAnswer = "  Renamed  ";
        _vm.Presets.RenameCommand.Execute(null);
        Assert.Equal("Renamed", PresetJson(mine.Id)["name"]!.GetValue<string>());

        _vm.Presets.DuplicateCommand.Execute(null);
        var copyId = _shell.Settings.ActivePresetId;
        Assert.NotEqual(mine.Id, copyId);
        Assert.Equal(Loc.Instance.Format("Preset.CopyName", "Renamed"), PresetJson(copyId)["name"]!.GetValue<string>());

        _dialogs.DeleteAnswer = false;
        _vm.Presets.DeleteCommand.Execute(null);
        Assert.True(File.Exists(Path.Combine(_shell.PresetStore.PresetsDir, copyId + ".json")));
        _dialogs.DeleteAnswer = true;
        _vm.Presets.DeleteCommand.Execute(null);
        Assert.False(File.Exists(Path.Combine(_shell.PresetStore.PresetsDir, copyId + ".json")));
        Assert.Equal("builtin.default", _shell.Settings.ActivePresetId);

        _vm.Presets.NewCommand.Execute(null);
        var newId = _shell.Settings.ActivePresetId;
        Assert.Equal(Loc.Instance["Preset.NewName"], PresetJson(newId)["name"]!.GetValue<string>());
        Assert.Equal(BuiltInPresets.All.Count + 2, _vm.Presets.Items.Count);
    }

    [Fact]
    public void ShareCode_CopyUsesLocalizedBuiltInName_PasteRoundTrips_BadCodeShowsLocalizedError()
    {
        _vm.Presets.CopyShareCodeCommand.Execute(null);
        Assert.NotNull(_dialogs.Clipboard);
        Assert.StartsWith(ShareCode.Prefix, _dialogs.Clipboard, StringComparison.Ordinal);
        Assert.True(ShareCode.TryDecode(_dialogs.Clipboard!, out var name, out var spec, out _, out _));
        Assert.Equal(Loc.Instance["Preset.builtin.default"], name);
        Assert.Equal(CrosshairSpec.Default, spec);

        _vm.Presets.ShareCodeInput = "CHM1:!!!";
        _vm.Presets.PasteShareCodeCommand.Execute(null);
        Assert.Equal(Loc.Instance[ShareCode.ErrorBase64], _vm.Presets.ShareCodeError);
        Assert.True(_vm.Presets.HasShareCodeError);

        _vm.Presets.ShareCodeInput = ShareCode.Encode("shared", CrosshairSpec.Default with { Opacity = 33 });
        _vm.Presets.PasteShareCodeCommand.Execute(null);
        Assert.False(_vm.Presets.HasShareCodeError);
        Assert.Equal(string.Empty, _vm.Presets.ShareCodeInput);
        var active = _shell.Settings.ActivePresetId;
        Assert.Equal("shared", PresetJson(active)["name"]!.GetValue<string>());
        Assert.Equal(33, _vm.Opacity);
    }

    [Fact]
    public void ImportExport_UseTheDialogPaths()
    {
        _dialogs.ExportPath = _dir.Combine("out.json");
        _vm.Presets.ExportPresetFileCommand.Execute(null);
        Assert.True(File.Exists(_dialogs.ExportPath));

        _dialogs.ImportPath = _dialogs.ExportPath;
        _vm.Presets.ImportPresetFileCommand.Execute(null);
        Assert.False(BuiltInPresets.IsBuiltInId(_shell.Settings.ActivePresetId));
        Assert.Equal(BuiltInPresets.All.Count + 1, _shell.Presets.Count);

        File.WriteAllText(_dir.Combine("broken.json"), "{");
        _dialogs.ImportPath = _dir.Combine("broken.json");
        _vm.Presets.ImportPresetFileCommand.Execute(null);
        Assert.Equal("Error.PresetImportFailed", _vm.CurrentMessage?.Key);
    }

    [Fact]
    public void HotkeyRebindFailure_ShowsHotkeyError_AndSuccessClearsIt()
    {
        _shell.FailingGestures.Add("Ctrl+Alt+Shift+F9");
        var next = _vm.Hotkeys.Single(h => h.Id == HotkeyBindings.NextPreset);
        Assert.True(HotkeyGesture.TryParse("Ctrl+Alt+Shift+F9", out var failing));
        next.Gesture = failing;
        Assert.True(next.IsFailed);
        Assert.True(_vm.HasHotkeyError);
        Assert.Equal("Ctrl+Alt+Shift+Right", _shell.Settings.Hotkeys.NextPreset?.ToString());

        Assert.True(HotkeyGesture.TryParse("Ctrl+Alt+Shift+F11", out var ok));
        next.Gesture = ok;
        Assert.False(next.IsFailed);
        Assert.False(_vm.HasHotkeyError);
        Assert.Equal("Ctrl+Alt+Shift+F11", _shell.Settings.Hotkeys.NextPreset?.ToString());
    }

    [Fact]
    public void StartupHotkeyFailure_IsShownWithoutRebinding()
    {
        var shell = new FakeAppShell(_dir.Combine("second"));
        Assert.True(HotkeyGesture.TryParse("Ctrl+Alt+Shift+F9", out var gesture));
        shell.Hotkeys[HotkeyBindings.NextPreset] = new HotkeyState(gesture, false, 1409);
        using var vm = new SettingsViewModel(shell, _dialogs, _monitors, new ManualDebounce());
        Assert.True(vm.HasHotkeyError);
        Assert.True(vm.Hotkeys.Single(h => h.Id == HotkeyBindings.NextPreset).IsFailed);
    }

    [Fact]
    public void PendingMessages_FlowToTheInfoBar_AndDismissRestoresTheNotice()
    {
        Assert.Equal(Loc.Instance["InfoBar.Fullscreen"], _vm.InfoBarText);
        _shell.Pending.Add(new PendingMessage("InfoBar.SettingsRecovered", @"C:\x\settings.json.corrupt-1"));
        _shell.Raise();
        Assert.Equal(Loc.Instance["InfoBar.SettingsRecovered"], _vm.InfoBarText);
        Assert.True(_vm.HasMessage);
        _shell.Raise();
        Assert.Single(_vm.Messages);

        _vm.DismissCommand.Execute(null);
        Assert.False(_vm.HasMessage);
        Assert.Equal(Loc.Instance["InfoBar.Fullscreen"], _vm.InfoBarText);
    }

    [Fact]
    public void MonitorFallback_ShowsWarning_AndOptionsListPrimaryAndDevices()
    {
        Assert.Equal(3, _vm.MonitorOptions.Count);
        Assert.Equal(MonitorSelection.Primary, _vm.SelectedMonitor?.Selection);

        _vm.SelectedMonitor = _vm.MonitorOptions[2];
        Assert.Equal(new MonitorSelection(MonitorMode.Device, @"\\.\DISPLAY1"), _shell.Settings.Monitor);

        _shell.Settings = _shell.Settings with { Monitor = new MonitorSelection(MonitorMode.Device, @"\\.\GONE") };
        _shell.MonitorFellBack = true;
        _shell.Raise();
        Assert.True(_vm.MonitorFellBack);
        Assert.Contains(_vm.Messages, m => m.Key == SettingsViewModel.MonitorWarningKey);
        Assert.Equal(Loc.Instance.Format("Monitor.NotConnected", @"\\.\GONE"), _vm.SelectedMonitor?.Label);
    }

    [Fact]
    public void General_Language_StartWithWindows_OverlayVisible_RoundTrip()
    {
        var language = _vm.Language;
        try
        {
            _vm.Language = "en";
            Assert.Equal("en", _shell.Settings.Language);
            Assert.Equal("en", Loc.Instance.Culture.Name);
        }
        finally
        {
            _vm.Language = language;
        }

        _vm.OverlayVisible = false;
        Assert.False(_shell.Settings.OverlayVisible);

        _shell.FailAutoStart = true;
        _vm.StartWithWindows = true;
        Assert.False(_vm.StartWithWindows);
        Assert.False(_shell.Settings.StartWithWindows);
        Assert.Equal("Error.AutoStartFailed", _vm.CurrentMessage?.Key);

        _shell.FailAutoStart = false;
        _vm.StartWithWindows = true;
        Assert.True(_shell.Settings.StartWithWindows);
    }

    [Fact]
    public void Disclaimer_AndPlacement_AreSavedThroughTheShell()
    {
        _shell.Settings = _shell.Settings with { DisclaimerAccepted = false };
        Assert.True(_vm.NeedsDisclaimer);
        _vm.AcceptDisclaimer();
        Assert.True(_shell.Settings.DisclaimerAccepted);
        Assert.False(_vm.NeedsDisclaimer);

        var saves = _shell.SaveCount;
        _vm.SavePlacement(new WindowPlacement(40, 40, 900, 620));
        _vm.SavePlacement(new WindowPlacement(40, 40, 900, 620));
        Assert.Equal(saves + 1, _shell.SaveCount);
        Assert.Equal(new WindowPlacement(40, 40, 900, 620), _vm.Placement);
    }

    [Theory]
    [InlineData(Key.X, ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift, "Ctrl+Alt+Shift+X")]
    [InlineData(Key.F10, ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift, "Ctrl+Alt+Shift+F10")]
    [InlineData(Key.Right, ModifierKeys.Control | ModifierKeys.Alt, "Ctrl+Alt+Right")]
    [InlineData(Key.Prior, ModifierKeys.Windows, "Win+PageUp")]
    [InlineData(Key.NumPad3, ModifierKeys.Shift, "Shift+NumPad3")]
    [InlineData(Key.D7, ModifierKeys.Control, "Ctrl+7")]
    [InlineData(Key.LeftCtrl, ModifierKeys.Control, null)]
    [InlineData(Key.OemComma, ModifierKeys.Control, null)]
    public void HotkeyBox_ChordText_MapsKeysAndModifiers(Key key, ModifierKeys modifiers, string? expected)
    {
        var text = HotkeyBox.ChordText(key, modifiers);
        Assert.Equal(expected, text);
        if (expected is not null)
        {
            Assert.True(HotkeyGesture.TryParse(text, out var gesture));
            Assert.Equal(expected, gesture!.ToString());
        }
    }
}
