using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using CrosshairManager.Core.Presets;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;
using Xunit.Abstractions;

namespace CrosshairManager.Ui.Tests;

/// <summary>Helpers for the e1..e12 end-to-end tests. Every wait is a bounded FlaUI retry, a process exit or a CLI run; no sleeps.</summary>
internal static class E2e
{
    /// <summary>Tray tooltip / notification-area button name (TrayIcon.ToolTip; a product name, not a translated string).</summary>
    public const string TrayName = "CrosshairManager";

    public static readonly TimeSpan FileTimeout = TimeSpan.FromSeconds(10);

    public static string SendHotkeyExe() => RequireFile(Path.Combine(AppFixture.RepositoryRoot, "tests", "tools", "SendHotkey", "bin", "Debug", AppFixture.Tfm, "SendHotkey.exe"));

    public static AutomationElement Element(AutomationElement root, string automationId) =>
        Retry.WhileNull(() => root.FindFirstDescendant(cf => cf.ByAutomationId(automationId)), AppFixture.UiTimeout, throwOnTimeout: true,
            timeoutMessage: automationId + " was not found").Result!;

    public static void WaitUntil(Func<bool> condition, string message, TimeSpan? timeout = null) =>
        Retry.WhileFalse(condition, timeout ?? FileTimeout, throwOnTimeout: true, ignoreException: true, timeoutMessage: message);

    public static string PresetsDir(AppFixture app) => Path.Combine(app.DataDir, "presets");

    /// <summary>User preset files (<c>presets/*.json</c>; .tmp/.bak siblings excluded), sorted.</summary>
    public static string[] PresetFiles(AppFixture app)
    {
        var dir = PresetsDir(app);
        return Directory.Exists(dir)
            ? Directory.EnumerateFiles(dir).Where(f => string.Equals(Path.GetExtension(f), ".json", StringComparison.OrdinalIgnoreCase)).Order(StringComparer.Ordinal).ToArray()
            : [];
    }

    /// <summary>Waits until exactly <paramref name="count"/> preset files exist.</summary>
    public static string[] WaitForPresetFiles(AppFixture app, int count)
    {
        WaitUntil(() => PresetFiles(app).Length == count, $"presets/ did not reach {count} file(s); now: {string.Join(", ", PresetFiles(app).Select(Path.GetFileName))}");
        return PresetFiles(app);
    }

    public static string Id(string presetPath) => Path.GetFileNameWithoutExtension(presetPath);

    /// <summary>Reads a JSON file the app may be replacing at the same moment (shared read; the caller retries).</summary>
    public static JsonNode ReadJson(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return JsonNode.Parse(reader.ReadToEnd()) ?? throw new InvalidDataException(path + " is empty JSON");
    }

    public static JsonNode Settings(AppFixture app) => ReadJson(app.SettingsPath);

    public static Preset LoadPreset(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return PresetFile.Load(reader.ReadToEnd()) is PresetLoadResult.Ok ok
            ? ok.Preset
            : throw new InvalidDataException(path + " is not a usable preset");
    }

    public static string ActivePresetId(AppFixture app) => (string)Settings(app)["activePresetId"]!;

    /// <summary>Runs <c>crosshair verify-overlay --data-dir &lt;fixture&gt; --out TestResults/ui/&lt;name&gt; ...</c> and asserts exit 0.</summary>
    public static void VerifyOverlay(AppFixture app, ITestOutputHelper output, string name, params string[] extra)
    {
        var args = new List<string> { "verify-overlay", "--data-dir", app.DataDir, "--out", Path.Combine(AppFixture.ResultsDir, name) };
        args.AddRange(extra);
        var (code, text) = AppFixture.RunCli([.. args]);
        output.WriteLine($"[{name}] crosshair {string.Join(' ', args)} -> exit {code}{Environment.NewLine}{text}");
        Assert.True(code == 0, $"[{name}] verify-overlay exit {code}{Environment.NewLine}{text}{Environment.NewLine}app.log: {app.AppLogTail()}");
    }

    /// <summary>Runs a helper executable (SendHotkey, a second CrosshairManager) and returns its exit code and output. Bounded.</summary>
    public static (int Code, string Output) Run(string exe, TimeSpan timeout, params string[] args)
    {
        var start = new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(exe),
        };
        foreach (var arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException(exe + " did not start.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(timeout))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"{Path.GetFileName(exe)} {string.Join(' ', args)} did not exit within {timeout}");
        }

        return (process.ExitCode, stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult());
    }

    /// <summary>
    /// Completes the open common file dialog owned by <paramref name="owner"/>: types <paramref name="path"/> into the file
    /// name box (Save: Edit 1001, Open: Edit 1148), presses Enter and waits for the dialog to close.
    /// </summary>
    public static void SubmitFileDialog(Window owner, string path)
    {
        var dialog = Retry.WhileNull(() => owner.ModalWindows.FirstOrDefault(), AppFixture.UiTimeout, throwOnTimeout: true,
            timeoutMessage: "the file dialog did not open").Result!;
        var edit = Retry.WhileNull(
            () => dialog.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit).And(cf.ByAutomationId("1001").Or(cf.ByAutomationId("1148")))),
            AppFixture.UiTimeout, throwOnTimeout: true, timeoutMessage: "the file name box of the dialog was not found").Result!.AsTextBox();
        edit.Focus();
        edit.Text = path;
        WaitUntil(() => edit.Text == path, "the file name box did not take the path", AppFixture.UiTimeout);
        Keyboard.Type(VirtualKeyShort.ENTER);
        WaitUntil(() => owner.ModalWindows.Length == 0, "the file dialog did not close after Enter", AppFixture.UiTimeout);
    }

    /// <summary>The CrosshairManager notification-area button: on the taskbar, else in the overflow flyout (opened via the chevron).</summary>
    public static AutomationElement TrayButton(AppFixture app)
    {
        var desktop = app.Automation.GetDesktop();
        var taskbar = Retry.WhileNull(() => desktop.FindFirstChild(cf => cf.ByClassName("Shell_TrayWnd")), AppFixture.UiTimeout, throwOnTimeout: true,
            timeoutMessage: "Shell_TrayWnd was not found").Result!;
        var direct = Retry.WhileNull(() => NamedButton(taskbar), AppFixture.UiTimeout);
        if (direct.Result is { } onTaskbar)
        {
            return onTaskbar;
        }

        // Windows 11: the chevron is AutomationId SystemTrayIcon; Windows 10: the Button inside TrayNotifyWnd.
        var chevron = taskbar.FindFirstDescendant(cf => cf.ByAutomationId("SystemTrayIcon"))
                      ?? taskbar.FindFirstDescendant(cf => cf.ByClassName("TrayNotifyWnd"))?.FindFirstChild(cf => cf.ByClassName("Button"))
                      ?? throw new InvalidOperationException("neither the tray button nor the show-hidden-icons button was found on Shell_TrayWnd");
        chevron.Click();
        var overflow = Retry.WhileNull(
            () => desktop.FindFirstChild(cf => cf.ByClassName("TopLevelWindowForOverflowXamlIsland").Or(cf.ByClassName("NotifyIconOverflowWindow"))),
            AppFixture.UiTimeout, throwOnTimeout: true, timeoutMessage: "the notification-area overflow window did not open").Result!;
        return Retry.WhileNull(() => NamedButton(overflow), AppFixture.UiTimeout, throwOnTimeout: true,
            timeoutMessage: TrayName + " was not in the overflow window").Result!;
    }

    /// <summary>Right-clicks the tray button and returns the context-menu item <paramref name="automationId"/> (a popup of the app process).</summary>
    public static AutomationElement OpenTrayMenu(AppFixture app, string automationId)
    {
        TrayButton(app).RightClick();
        return AppPopupItem(app, automationId);
    }

    /// <summary>An element in any top-level window (menu popups included) of the app process.</summary>
    public static AutomationElement AppPopupItem(AppFixture app, string automationId)
    {
        var desktop = app.Automation.GetDesktop();
        var pid = app.Process.Id;
        return Retry.WhileNull(
            () => desktop.FindAllChildren(cf => cf.ByProcessId(pid))
                .Select(w => w.FindFirstDescendant(cf => cf.ByAutomationId(automationId)))
                .FirstOrDefault(e => e is not null),
            AppFixture.UiTimeout, throwOnTimeout: true, ignoreException: true, timeoutMessage: automationId + " did not appear in a popup of the app").Result!;
    }

    private static AutomationElement? NamedButton(AutomationElement root) =>
        root.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName(TrayName)));

    private static string RequireFile(string path) =>
        File.Exists(path) ? path : throw new FileNotFoundException("Build the solution first (dotnet build CrosshairManager.sln -c Debug).", path);
}

/// <summary>Win32 clipboard text (CF_UNICODETEXT) without WPF/WinForms in the test process.</summary>
internal static class ClipboardText
{
    private const uint CfUnicodeText = 13;

    /// <summary>Empties the clipboard so a later read cannot see a stale value. Retries while another process holds it.</summary>
    public static void Clear() =>
        Retry.WhileFalse(() =>
        {
            if (!OpenClipboard(0))
            {
                return false;
            }

            try
            {
                return EmptyClipboard();
            }
            finally
            {
                CloseClipboard();
            }
        }, AppFixture.UiTimeout, throwOnTimeout: true, timeoutMessage: "the clipboard could not be emptied");

    /// <summary>The clipboard text, or null when there is none or the clipboard is busy.</summary>
    public static string? TryRead()
    {
        if (!OpenClipboard(0))
        {
            return null;
        }

        try
        {
            var handle = GetClipboardData(CfUnicodeText);
            if (handle == 0)
            {
                return null;
            }

            var pointer = GlobalLock(handle);
            if (pointer == 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            try
            {
                return Marshal.PtrToStringUni(pointer);
            }
            finally
            {
                GlobalUnlock(handle);
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenClipboard(nint newOwner);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint GetClipboardData(uint format);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint GlobalLock(nint memory);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalUnlock(nint memory);
}

/// <summary>
/// Holds a global hotkey for the test thread (RegisterHotKey with no window) so the app's registration of the same chord fails.
/// Dispose on the same thread.
/// </summary>
internal sealed class HotkeyReservation : IDisposable
{
    private const uint ModNoRepeat = 0x4000;
    private const int ReservationId = 0x5EED;

    public HotkeyReservation(Core.Settings.HotkeyGesture gesture)
    {
        ArgumentNullException.ThrowIfNull(gesture);
        if (!RegisterHotKey(0, ReservationId, gesture.Modifiers | ModNoRepeat, gesture.VirtualKey))
        {
            throw new InvalidOperationException($"the test could not reserve {gesture} (Win32 error {Marshal.GetLastWin32Error()}); is it already taken?");
        }
    }

    public void Dispose() => UnregisterHotKey(0, ReservationId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint window, int id);
}
