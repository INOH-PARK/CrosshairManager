using System.Drawing;
using System.Globalization;
using System.IO;
using CrosshairManager.Core.Geometry;
using CrosshairManager.Core.Model;
using CrosshairManager.Core.Presets;
using CrosshairManager.Core.Render;
using CrosshairManager.Core.Settings;
using CrosshairManager.Platform.Monitors;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;
using Microsoft.Win32;
using Xunit.Abstractions;
using MonitorInfo = CrosshairManager.Platform.Monitors.MonitorInfo;

namespace CrosshairManager.Ui.Tests;

/// <summary>
/// e1..e12: edit -> preset file -> on-screen overlay (crosshair verify-overlay exit code), tray, dialogs, hotkeys, single instance,
/// settings recovery, auto-start. Each test owns an <see cref="AppFixture"/> (own data folder, killed and deleted on Dispose).
/// </summary>
[Trait("Category", "Ui")]
public sealed class EndToEndTests(ITestOutputHelper output)
{
    private const int PreviewZoom = 8;
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "CrosshairManager";

    [Fact]
    public void e1_DotSizeOnBuiltInDefault_WritesAUserCopy_AndTheOverlayMatches()
    {
        using var app = new AppFixture();
        var window = app.SettingsWindow();
        SelectListPreset(window, "builtin.default");
        AppFixture.SelectTab(window, "TabAdvanced", "DotSize").AsTextBox().Text = "5";

        var file = E2e.WaitForPresetFiles(app, 1)[0];
        E2e.WaitUntil(() => E2e.ReadJson(file)["crosshair"]?["dot"]?["size"]?.GetValue<int>() == 5, "the new preset file does not have dot.size == 5");
        E2e.WaitUntil(() => E2e.ActivePresetId(app) == E2e.Id(file), "settings.json activePresetId is not the new copy");
        output.WriteLine("preset file " + file);
        E2e.VerifyOverlay(app, output, "e1");
    }

    [Fact]
    public void e2_ColorHex_PreviewShowsEachOpaqueSourcePixelAsAn8x8DeviceBlock_AndTheOverlayMatches()
    {
        using var app = new AppFixture();
        var window = app.SettingsWindow();
        AppFixture.SelectTab(window, "TabAdvanced", "ColorHex").AsTextBox().Text = "#FF8800";

        var file = E2e.WaitForPresetFiles(app, 1)[0];
        E2e.WaitUntil(() => E2e.LoadPreset(file).Crosshair.Color == Rgb.Parse("#FF8800"), "the new preset file does not have color #FF8800");
        var spec = E2e.LoadPreset(file).Crosshair;
        var source = CrosshairRasterizer.Render(CrosshairLayout.Compute(spec.Normalize().Spec.ApplyScale(), oddWidth: false, oddHeight: false));

        var preview = E2e.Element(window, "Preview");
        var problems = new List<string>();
        var matched = Retry.WhileFalse(() =>
        {
            problems = PreviewMismatches(preview, source);
            return problems.Count == 0;
        }, AppFixture.UiTimeout, ignoreException: true);
        using (var shot = Capture.Element(preview))
        {
            var png = Path.Combine(Directory.CreateDirectory(Path.Combine(AppFixture.ResultsDir, "e2")).FullName, "preview.png");
            shot.ToFile(png);
            output.WriteLine("preview capture " + png);
        }

        Assert.True(matched.Success && problems.Count == 0, string.Join(Environment.NewLine, problems.Take(20)));
        E2e.VerifyOverlay(app, output, "e2");
    }

    [Fact]
    public void e3_Rename_Duplicate_Delete_UpdateThePresetFiles_AndCancelKeepsTheFile()
    {
        using var app = new AppFixture();
        var window = app.SettingsWindow();
        E2e.Element(window, "NewPreset").AsButton().Invoke();
        var original = E2e.WaitForPresetFiles(app, 1)[0];
        E2e.WaitUntil(() => E2e.ActivePresetId(app) == E2e.Id(original), "the new preset did not become active");

        const string newName = "e3 renamed";
        E2e.Element(window, "RenamePreset").AsButton().Invoke();
        var rename = app.TopLevel("RenameDialog", AppFixture.UiTimeout);
        E2e.Element(rename, "RenameText").AsTextBox().Text = newName;
        E2e.Element(rename, "RenameOk").AsButton().Invoke();
        E2e.WaitUntil(() => (string?)E2e.ReadJson(original)["name"] == newName, "the preset file name field was not updated");
        WaitClosed(app, "RenameDialog");

        E2e.Element(window, "DuplicatePreset").AsButton().Invoke();
        var both = E2e.WaitForPresetFiles(app, 2);
        var copy = both.Single(f => f != original);
        E2e.WaitUntil(() => E2e.ActivePresetId(app) == E2e.Id(copy), "the duplicate did not become active");

        E2e.Element(window, "DeletePreset").AsButton().Invoke();
        E2e.Element(app.TopLevel("ConfirmDeleteDialog", AppFixture.UiTimeout), "ConfirmDeleteCancel").AsButton().Invoke();
        WaitClosed(app, "ConfirmDeleteDialog");
        Assert.Equal(both, E2e.PresetFiles(app));

        E2e.Element(window, "DeletePreset").AsButton().Invoke();
        E2e.Element(app.TopLevel("ConfirmDeleteDialog", AppFixture.UiTimeout), "ConfirmDeleteOk").AsButton().Invoke();
        E2e.WaitUntil(() => !File.Exists(copy), "the deleted preset file still exists");
        WaitClosed(app, "ConfirmDeleteDialog");
        Assert.Equal([original], E2e.PresetFiles(app));
    }

    [Fact]
    public void e4_ExportThroughSaveFileDialog_Validates_AndImportAddsAListEntry()
    {
        using var app = new AppFixture();
        var window = app.SettingsWindow();
        SelectListPreset(window, "builtin.cross");
        var exportPath = Path.Combine(Directory.CreateDirectory(Path.Combine(app.DataDir, "exports")).FullName, "e4-export.json");

        E2e.Element(window, "ExportPresetFile").AsButton().Invoke();
        E2e.SubmitFileDialog(window, exportPath);
        E2e.WaitUntil(() => File.Exists(exportPath), exportPath + " was not written");
        var (code, text) = AppFixture.RunCli("validate", exportPath);
        output.WriteLine($"crosshair validate {exportPath} -> exit {code}{Environment.NewLine}{text}");
        Assert.True(code == 0, "validate exit " + code + Environment.NewLine + text);

        var list = E2e.Element(window, "PresetList").AsListBox();
        var before = list.Items.Length;
        E2e.Element(window, "ImportPresetFile").AsButton().Invoke();
        E2e.SubmitFileDialog(window, exportPath);
        var imported = E2e.WaitForPresetFiles(app, 1)[0];
        E2e.WaitUntil(() => list.Items.Length == before + 1, $"PresetList did not grow from {before}");
        Assert.Equal(BuiltInPresets.All.Single(p => p.Id == "builtin.cross").Crosshair, E2e.LoadPreset(imported).Crosshair);
        Assert.Equal(Resx.English("Preset.builtin.cross"), E2e.LoadPreset(imported).Name);
    }

    [Fact]
    public void e5_ShareCode_CopyPasteRoundTrips_AndABadCodeShowsTheLocalizedError()
    {
        using var app = new AppFixture();
        var window = app.SettingsWindow();
        var cross = BuiltInPresets.All.Single(p => p.Id == "builtin.cross");
        SelectListPreset(window, cross.Id);
        E2e.WaitUntil(() => E2e.ActivePresetId(app) == cross.Id, "builtin.cross did not become active");

        ClipboardText.Clear();
        E2e.Element(window, "CopyShareCode").AsButton().Invoke();
        string? code = null;
        E2e.WaitUntil(() => (code = ClipboardText.TryRead()) is not null && code.StartsWith("CHM1:", StringComparison.Ordinal),
            "the clipboard does not start with CHM1:", AppFixture.UiTimeout);
        output.WriteLine("share code " + code);

        E2e.Element(window, "NewPreset").AsButton().Invoke();
        var fresh = E2e.WaitForPresetFiles(app, 1)[0];
        E2e.WaitUntil(() => E2e.ActivePresetId(app) == E2e.Id(fresh), "the new preset did not become active");

        var input = E2e.Element(window, "ShareCodeInput").AsTextBox();
        input.Text = code!;
        E2e.Element(window, "PasteShareCode").AsButton().Invoke();
        var pasted = E2e.WaitForPresetFiles(app, 2).Single(f => f != fresh);
        var preset = E2e.LoadPreset(pasted);
        Assert.Equal(cross.Crosshair, preset.Crosshair);
        Assert.Equal(Resx.English("Preset.builtin.cross"), preset.Name);
        E2e.WaitUntil(() => E2e.ActivePresetId(app) == E2e.Id(pasted), "the pasted preset did not become active");

        // Failure path: a code whose payload is not base64url.
        const string bad = "CHM1:!!!";
        Assert.False(ShareCode.TryDecode(bad, out _, out _, out _, out var errorKey));
        Assert.Equal("ShareCode.Error.Base64", errorKey);
        input.Text = bad;
        E2e.Element(window, "PasteShareCode").AsButton().Invoke();
        var expected = Resx.English("ShareCode.Error.Base64");
        var error = E2e.Element(window, "ShareCodeError");
        E2e.WaitUntil(() => error.Name == expected, $"ShareCodeError is '{error.Name}', expected '{expected}'", AppFixture.UiTimeout);
        output.WriteLine("ShareCodeError: " + error.Name);
        Assert.Equal(2, E2e.PresetFiles(app).Length);
    }

    [Fact]
    public void e6_HotkeyToggleSetThroughTheSystemKeyPath_HidesTheOverlayWhenPressed()
    {
        const string chord = "Ctrl+Alt+Shift+F10";
        using var app = new AppFixture();
        var window = app.SettingsWindow();
        var box = AppFixture.SelectTab(window, "TabAdvanced", "HotkeyToggle").AsTextBox();
        box.Focus();
        // Alt held: WPF reports Key.System and the box must read e.SystemKey (F10).
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.ALT, VirtualKeyShort.SHIFT, VirtualKeyShort.F10);
        E2e.WaitUntil(() => box.Text == chord, $"HotkeyToggle shows '{box.Text}', expected {chord}", AppFixture.UiTimeout);
        E2e.WaitUntil(() => (string?)E2e.Settings(app)["hotkeys"]?["toggleOverlay"] == chord, "settings.json hotkeys.toggleOverlay was not updated");

        var (code, text) = E2e.Run(E2e.SendHotkeyExe(), TimeSpan.FromSeconds(30), chord);
        output.WriteLine($"SendHotkey {chord} -> exit {code}: {text}");
        Assert.True(code == 0, "SendHotkey exit " + code + ": " + text);
        E2e.WaitUntil(() => E2e.Settings(app)["overlayVisible"]?.GetValue<bool>() == false, "the toggle hotkey did not hide the overlay");
        E2e.VerifyOverlay(app, output, "e6", "--expect-hidden");
    }

    [Fact]
    public void e6_HotkeyTakenByAnotherProcess_ShowsHotkeyErrorForHotkeyNext()
    {
        Assert.True(HotkeyGesture.TryParse("Ctrl+Alt+Shift+F9", out var taken));
        using var reservation = new HotkeyReservation(taken);
        using var app = new AppFixture(configure: s => s with { Hotkeys = s.Hotkeys with { NextPreset = taken } });
        var window = app.SettingsWindow();
        AppFixture.SelectTab(window, "TabAdvanced", "HotkeyNext");

        var error = E2e.Element(window, "HotkeyError");
        Assert.Equal(Resx.English("Hotkey.Error"), error.Name);
        Assert.Equal(taken.ToString(), E2e.Element(window, "HotkeyNext").AsTextBox().Text);
        var message = E2e.Element(window, "InfoBarMessage");
        E2e.WaitUntil(() => message.Name == Resx.English("Hotkey.RegistrationFailed"), $"InfoBarMessage is '{message.Name}'", AppFixture.UiTimeout);
        output.WriteLine($"HotkeyError '{error.Name}', InfoBar '{message.Name}'");
    }

    [Fact]
    public void e7_SecondLaunch_ExitsZero_AndBringsTheSettingsWindowToTheForeground()
    {
        using var app = new AppFixture(openSettings: false);
        E2e.WaitUntil(() => File.Exists(Path.Combine(app.DataDir, "instance.pid")), "the first instance did not write instance.pid", AppFixture.StartupTimeout);
        Assert.Null(app.FindTopLevel("SettingsWindow"));

        var (code, text) = E2e.Run(AppFixture.AppExe, AppFixture.StartupTimeout, "--data-dir", app.DataDir);
        output.WriteLine($"second instance -> exit {code} {text}");
        Assert.Equal(0, code);

        var window = app.SettingsWindow();
        var handle = window.Properties.NativeWindowHandle.ValueOrDefault;
        E2e.WaitUntil(() => TestSupport.Input.GetForegroundHandle() == handle,
            $"foreground is 0x{TestSupport.Input.GetForegroundHandle():X}, settings window is 0x{handle:X}", AppFixture.UiTimeout);
    }

    [Fact]
    public void e8_CorruptSettings_AreMovedAside_AndTheInfoBarSaysSo()
    {
        var dataDir = Path.Combine(AppFixture.TempRoot(), "chm-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataDir);
        File.WriteAllText(Path.Combine(dataDir, "settings.json"), "{ this is not json");
        using var app = new AppFixture(dataDir, createSettings: false);

        // Defaults were loaded, so this is a first run: the disclaimer comes first.
        var dialog = app.TopLevel("DisclaimerDialog", AppFixture.StartupTimeout);
        E2e.Element(dialog, "DisclaimerOk").AsButton().Invoke();
        var window = app.SettingsWindow();
        var expected = Resx.SystemLanguage("InfoBar.SettingsRecovered");
        var bar = E2e.Element(window, "InfoBar");
        E2e.WaitUntil(() => bar.Name == expected, $"InfoBar is '{bar.Name}', expected '{expected}'", AppFixture.UiTimeout);
        Assert.Single(Directory.GetFiles(dataDir, "settings.json.corrupt-*"));
        output.WriteLine("InfoBar: " + bar.Name);
    }

    [Fact]
    public void e9_TStyle_TurnsTopOff_AndTheOverlayMatches()
    {
        using var app = new AppFixture();
        var window = app.SettingsWindow();
        AppFixture.SelectTab(window, "TabAdvanced", "TStyle").AsCheckBox().IsChecked = true;

        var file = E2e.WaitForPresetFiles(app, 1)[0];
        E2e.WaitUntil(() => E2e.ReadJson(file)["crosshair"]?["top"]?["enabled"]?.GetValue<bool>() == false, "the preset file does not have top.enabled == false");
        E2e.WaitUntil(() => E2e.ActivePresetId(app) == E2e.Id(file), "the T copy did not become active");
        Assert.False(E2e.LoadPreset(file).Crosshair.Top.Enabled);
        E2e.VerifyOverlay(app, output, "e9");
    }

    [Fact]
    public void e10_MonitorSelect_FirstNonPrimaryMonitor_MovesTheOverlayThere()
    {
        MonitorInfo? target;
        using (var service = new MonitorService())
        {
            target = service.GetMonitors().FirstOrDefault(m => !m.IsPrimary);
        }

        if (target is null)
        {
            Assert.Fail("e10 needs a second monitor; only the primary monitor is connected");
            return;
        }

        using var app = new AppFixture();
        var window = app.SettingsWindow();
        var combo = AppFixture.SelectTab(window, "TabAdvanced", "MonitorSelect").AsComboBox();
        var label = string.Format(CultureInfo.GetCultureInfo("en"), Resx.English("Monitor.Device"), target.DeviceName, target.Bounds.W, target.Bounds.H);
        combo.Expand();
        var item = Retry.WhileNull(() => combo.Items.FirstOrDefault(i => i.Name == label), AppFixture.UiTimeout, throwOnTimeout: true,
            timeoutMessage: $"MonitorSelect has no item '{label}'; items: {string.Join(" | ", combo.Items.Select(i => i.Name))}").Result!;
        item.Select();
        combo.Collapse();

        E2e.WaitUntil(() => (string?)E2e.Settings(app)["monitor"]?["deviceName"] == target.DeviceName
                            && (string?)E2e.Settings(app)["monitor"]?["mode"] == "device", "settings.json monitor was not set to " + target.DeviceName);
        E2e.VerifyOverlay(app, output, "e10", "--monitor", target.DeviceName);
    }

    [Fact]
    public void e11_TrayMenu_TogglesTheOverlay_SwitchesPresets_AndExits()
    {
        using var app = new AppFixture(openSettings: false);
        E2e.WaitUntil(() => app.AppLogTail(40).Contains("started", StringComparison.Ordinal), "the app did not log 'started'", AppFixture.StartupTimeout);

        E2e.OpenTrayMenu(app, "TrayToggleOverlay").AsMenuItem().Invoke();
        E2e.WaitUntil(() => E2e.Settings(app)["overlayVisible"]?.GetValue<bool>() == false, "TrayToggleOverlay did not hide the overlay");
        E2e.VerifyOverlay(app, output, "e11-hidden", "--expect-hidden");

        E2e.OpenTrayMenu(app, "TrayToggleOverlay").AsMenuItem().Invoke();
        E2e.WaitUntil(() => E2e.Settings(app)["overlayVisible"]?.GetValue<bool>() == true, "TrayToggleOverlay did not show the overlay again");

        E2e.OpenTrayMenu(app, "TrayPresets").AsMenuItem().Expand();
        E2e.AppPopupItem(app, "TrayPreset.builtin.dot").AsMenuItem().Invoke();
        E2e.WaitUntil(() => E2e.ActivePresetId(app) == "builtin.dot", "TrayPresets > builtin.dot did not switch the preset");
        E2e.VerifyOverlay(app, output, "e11-dot", "--preset", "builtin.dot");

        E2e.OpenTrayMenu(app, "TrayExit").AsMenuItem().Invoke();
        Assert.True(app.Process.WaitForExit(TimeSpan.FromSeconds(5)), "CrosshairManager did not exit within 5 s of TrayExit");
        output.WriteLine("exit code " + app.Process.ExitCode);
    }

    [Fact]
    public void e12_StartWithWindows_WritesAndRemovesTheRunValue()
    {
        Assert.True(RunValue() is null, $"HKCU\\{RunKeyPath}\\{RunValueName} already exists ('{RunValue()}'); remove it before running e12");
        try
        {
            using var app = new AppFixture();
            var window = app.SettingsWindow();
            var box = AppFixture.SelectTab(window, "TabAdvanced", "StartWithWindows").AsCheckBox();
            var expected = "\"" + AppFixture.AppExe + "\"";

            box.IsChecked = true;
            E2e.WaitUntil(() => string.Equals(RunValue(), expected, StringComparison.OrdinalIgnoreCase), $"Run value is '{RunValue()}', expected {expected}");
            E2e.WaitUntil(() => E2e.Settings(app)["startWithWindows"]?.GetValue<bool>() == true, "settings.json startWithWindows is not true");
            output.WriteLine("Run value: " + RunValue());

            box.IsChecked = false;
            E2e.WaitUntil(() => RunValue() is null, $"Run value '{RunValue()}' was not removed");
            E2e.WaitUntil(() => E2e.Settings(app)["startWithWindows"]?.GetValue<bool>() == false, "settings.json startWithWindows is not false");
        }
        finally
        {
            // Never leave the machine starting the test build at logon, whatever failed above.
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            key?.DeleteValue(RunValueName, throwOnMissingValue: false);
        }
    }

    private static string? RunValue()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(RunValueName) as string;
    }

    private static void SelectListPreset(Window window, string presetId)
    {
        var row = E2e.Element(window, "Preset." + presetId).AsListBoxItem();
        row.Select();
        E2e.WaitUntil(() => row.IsSelected, "Preset." + presetId + " was not selected", AppFixture.UiTimeout);
    }

    private static void WaitClosed(AppFixture app, string automationId) =>
        E2e.WaitUntil(() => app.FindTopLevel(automationId) is null, automationId + " did not close", AppFixture.UiTimeout);

    /// <summary>Every fully opaque source pixel must be a <see cref="PreviewZoom"/>-square block of its color in the captured Preview image.</summary>
    private static List<string> PreviewMismatches(AutomationElement preview, PixelBuffer source)
    {
        var problems = new List<string>();
        using var shot = Capture.Element(preview);
        var bitmap = shot.Bitmap;
        if (bitmap.Width != source.Width * PreviewZoom || bitmap.Height != source.Height * PreviewZoom)
        {
            problems.Add($"Preview is {bitmap.Width}x{bitmap.Height} device px, expected {source.Width * PreviewZoom}x{source.Height * PreviewZoom}");
            return problems;
        }

        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                var p = source.GetPixel(x, y);
                if (p.A != 255)
                {
                    continue;
                }

                for (var dy = 0; dy < PreviewZoom; dy++)
                {
                    for (var dx = 0; dx < PreviewZoom; dx++)
                    {
                        Color c = bitmap.GetPixel((x * PreviewZoom) + dx, (y * PreviewZoom) + dy);
                        if (c.R != p.R || c.G != p.G || c.B != p.B)
                        {
                            problems.Add($"source ({x},{y}) #{p.R:X2}{p.G:X2}{p.B:X2} but device ({(x * PreviewZoom) + dx},{(y * PreviewZoom) + dy}) is #{c.R:X2}{c.G:X2}{c.B:X2}");
                            break;
                        }
                    }
                }
            }
        }

        return problems;
    }
}
