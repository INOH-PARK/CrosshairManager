using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using CrosshairManager.Core.Settings;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using FlaUI.UIA3;

namespace CrosshairManager.Ui.Tests;

/// <summary>
/// Launches CrosshairManager.exe with a unique temp data folder (<c>--data-dir</c>, pre-made settings.json unless
/// <paramref name="createSettings"/> is false) and attaches UIA3. Dispose kills the process, asserts HasExited and
/// deletes the folder (unless <see cref="KeepDataDir"/>). All waits are bounded FlaUI retries; no sleeps.
/// </summary>
public sealed class AppFixture : IDisposable
{
    public const string Tfm = "net10.0-windows10.0.19041.0";
    public static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(20);
    public static readonly TimeSpan UiTimeout = TimeSpan.FromSeconds(5);

    /// <summary>UIA_E_ELEMENTNOTAVAILABLE.</summary>
    private const uint ElementNotAvailable = 0x80040201;

    public AppFixture(string? dataDir = null, bool createSettings = true, bool openSettings = true, Func<AppSettings, AppSettings>? configure = null)
    {
        DataDir = dataDir ?? Path.Combine(TempRoot(), "chm-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(DataDir);
        if (createSettings)
        {
            var settings = AppSettings.Default with
            {
                DisclaimerAccepted = true,
                Language = "en",
                SettingsWindowPlacement = new WindowPlacement(40, 40, 900, 620),
            };
            new SettingsStore(DataDir).Save(configure is null ? settings : configure(settings));
        }

        Automation = new UIA3Automation();
        var start = new ProcessStartInfo(AppExe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(AppExe) };
        start.ArgumentList.Add("--data-dir");
        start.ArgumentList.Add(DataDir);
        if (openSettings)
        {
            start.ArgumentList.Add("--settings");
        }

        Process = System.Diagnostics.Process.Start(start) ?? throw new InvalidOperationException("CrosshairManager.exe did not start.");
        App = FlaUI.Core.Application.Attach(Process);
    }

    public string DataDir { get; }

    public Process Process { get; }

    public UIA3Automation Automation { get; }

    public FlaUI.Core.Application App { get; }

    /// <summary>Set by a test that hands the folder to a second fixture.</summary>
    public bool KeepDataDir { get; set; }

    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    public static string AppExe { get; } = Require(Path.Combine(RepositoryRoot, "src", "CrosshairManager.App", "bin", "Debug", Tfm, "CrosshairManager.exe"));

    public static string CliExe { get; } = Require(Path.Combine(RepositoryRoot, "tools", "CrosshairManager.Cli", "bin", "Debug", Tfm, "crosshair.exe"));

    public static string EvidenceDir { get; } = Directory.CreateDirectory(Path.Combine(RepositoryRoot, ".omo", "evidence")).FullName;

    public static string ResultsDir { get; } = Directory.CreateDirectory(Path.Combine(RepositoryRoot, "TestResults", "ui")).FullName;

    public string SettingsPath => Path.Combine(DataDir, "settings.json");

    /// <summary>The settings window (AutomationId SettingsWindow) once it exists.</summary>
    public Window SettingsWindow() => TopLevel("SettingsWindow", StartupTimeout);

    /// <summary>Waits for <see cref="FindTopLevel"/>; on timeout the message lists the app's windows and its log tail.</summary>
    public Window TopLevel(string automationId, TimeSpan timeout)
    {
        var result = Retry.WhileNull(() => FindTopLevel(automationId), timeout, throwOnTimeout: false);
        if (result.Result is { } window)
        {
            return window;
        }

        throw new TimeoutException(
            $"window '{automationId}' did not appear within {timeout}. Windows: {DescribeWindows()}. app.log: {AppLogTail()}");
    }

    /// <summary>
    /// A window of the app by AutomationId. UIA lists an owned window (a dialog with Owner set, e.g. DisclaimerDialog)
    /// under its owner instead of under the desktop, so the unowned top-level windows and their child windows are searched.
    /// Throws when the process exited (an error MessageBox or a crash must fail fast, not time out).
    /// </summary>
    public Window? FindTopLevel(string automationId)
    {
        if (Process.HasExited)
        {
            throw new InvalidOperationException($"CrosshairManager exited with code {Process.ExitCode}");
        }

        foreach (var top in App.GetAllTopLevelWindows(Automation))
        {
            if (top.Properties.AutomationId.ValueOrDefault == automationId)
            {
                return top;
            }

            try
            {
                var owned = top.FindAllChildren(cf => cf.ByControlType(ControlType.Window))
                    .FirstOrDefault(w => w.Properties.AutomationId.ValueOrDefault == automationId);
                if (owned is not null)
                {
                    return owned.AsWindow();
                }
            }
            catch (COMException ex) when ((uint)ex.HResult == ElementNotAvailable)
            {
                // The window closed between enumeration and the query: it is not there, which is the answer.
            }
        }

        return null;
    }

    /// <summary>Top-level windows of the process and their owned child windows: "ControlType(AutomationId|Name)".</summary>
    public string DescribeWindows()
    {
        var parts = new List<string>();
        foreach (var top in App.GetAllTopLevelWindows(Automation))
        {
            parts.Add(Describe(top));
            foreach (var child in top.FindAllChildren(cf => cf.ByControlType(ControlType.Window)))
            {
                parts.Add(Describe(top) + " > " + Describe(child));
            }
        }

        return parts.Count == 0 ? "(none)" : string.Join("; ", parts);
    }

    /// <summary>Last lines of logs/app.log (shared read; the app keeps it open).</summary>
    public string AppLogTail(int lines = 12)
    {
        var path = Path.Combine(DataDir, "logs", "app.log");
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            var all = reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" | ", all.Skip(Math.Max(0, all.Length - lines)).Select(l => l.Trim()));
        }
        catch (IOException ex)
        {
            return "(unreadable: " + ex.Message + ")";
        }
    }

    public static string Describe(AutomationElement element)
    {
        var id = element.Properties.AutomationId.ValueOrDefault;
        var name = element.Properties.Name.ValueOrDefault;
        return $"{element.Properties.ControlType.ValueOrDefault}({id}|{name})";
    }

    /// <summary>Selects a tab and waits until a control of that tab is present.</summary>
    public static AutomationElement SelectTab(Window window, string tabId, string probeId)
    {
        ArgumentNullException.ThrowIfNull(window);
        var tab = Retry.WhileNull(() => window.FindFirstDescendant(cf => cf.ByAutomationId(tabId)), UiTimeout, throwOnTimeout: true).Result!.AsTabItem();
        tab.Select();
        Retry.WhileFalse(() => tab.IsSelected, UiTimeout, throwOnTimeout: true, timeoutMessage: tabId + " was not selected");
        return Retry.WhileNull(() => window.FindFirstDescendant(cf => cf.ByAutomationId(probeId)), UiTimeout, throwOnTimeout: true,
            timeoutMessage: probeId + " did not appear after selecting " + tabId).Result!;
    }

    /// <summary>Runs the crosshair CLI (e.g. verify-overlay) and returns exit code + combined output. Bounded by 90 s.</summary>
    public static (int Code, string Output) RunCli(params string[] args)
    {
        var start = new ProcessStartInfo(CliExe)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(CliExe),
        };
        foreach (var arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        using var cli = System.Diagnostics.Process.Start(start) ?? throw new InvalidOperationException("crosshair.exe did not start.");
        var stdout = cli.StandardOutput.ReadToEndAsync();
        var stderr = cli.StandardError.ReadToEndAsync();
        if (!cli.WaitForExit(TimeSpan.FromSeconds(90)))
        {
            cli.Kill(entireProcessTree: true);
            throw new TimeoutException("crosshair " + string.Join(' ', args) + " did not finish within 90 s");
        }

        return (cli.ExitCode, stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult());
    }

    public void Dispose()
    {
        try
        {
            if (!Process.HasExited)
            {
                Process.Kill(entireProcessTree: true);
                Process.WaitForExit(TimeSpan.FromSeconds(10));
            }

            Assert.True(Process.HasExited, "CrosshairManager is still running after Kill");
        }
        finally
        {
            Automation.Dispose();
            Process.Dispose();
            if (!KeepDataDir)
            {
                // The exited process may still be releasing the log file handle; bounded retry instead of a sleep.
                Retry.WhileException(() => Directory.Delete(DataDir, recursive: true), TimeSpan.FromSeconds(10), throwOnTimeout: true);
                Assert.False(Directory.Exists(DataDir), DataDir + " still exists");
            }
        }
    }

    public static string TempRoot()
    {
        var tmp = Environment.GetEnvironmentVariable("TMPDIR");
        return !string.IsNullOrEmpty(tmp) && Directory.Exists(tmp) ? tmp : Path.GetTempPath();
    }

    private static string FindRepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "CrosshairManager.sln")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("CrosshairManager.sln not found above " + AppContext.BaseDirectory);
    }

    private static string Require(string path) =>
        File.Exists(path) ? path : throw new FileNotFoundException("Build the solution first (dotnet build CrosshairManager.sln -c Debug).", path);
}
