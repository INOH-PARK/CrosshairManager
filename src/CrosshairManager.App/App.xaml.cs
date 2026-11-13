using System.Windows;
using System.Windows.Threading;
using CrosshairManager.App.Localization;
using CrosshairManager.App.Logging;
using CrosshairManager.App.Views;

namespace CrosshairManager.App;

public partial class App : Application
{
    private const string Caption = "CrosshairManager";

    private FileLog? _log;
    private SingleInstance? _instance;
    private AppShell? _shell;
    private SettingsWindow? _settingsWindow;

    /// <summary>The running shell for the settings UI (todo 16); null before startup finished.</summary>
    public IAppShell? Shell => _shell;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            _log?.Error("unhandled exception (terminating: " + args.IsTerminating + ")", args.ExceptionObject as Exception);

        AppArguments args;
        try
        {
            args = AppArguments.Parse(e.Args, Environment.GetEnvironmentVariables());
        }
        catch (ArgumentException ex)
        {
            MessageBox.Show(Loc.Instance.Format("Error.InvalidArguments", ex.Message), Caption, MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(2);
            return;
        }

        _log = new FileLog(args.DataDir);
        _instance = SingleInstance.TryAcquire(args.DataDir, _log, out var pidWriteError);
        if (_instance is null)
        {
            // Another instance owns this data folder; it was signaled to open its settings window.
            Shutdown(0);
            return;
        }

        try
        {
            _shell = new AppShell(args, Dispatcher, _log, ExitApplication);
            _shell.SettingsRequested += (_, _) => ShowSettings();
            _shell.Start(pidWriteError);
            _instance.Listen(() => Dispatcher.BeginInvoke(() => _shell?.OpenSettings()));
        }
        catch (Exception ex)
        {
            _log.Error("startup failed", ex);
            MessageBox.Show(Loc.Instance.Format("Error.StartupFailed", ex.Message), Caption, MessageBoxButton.OK, MessageBoxImage.Error);
            ExitApplication(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Cleanup();
        base.OnExit(e);
    }

    private void ExitApplication() => ExitApplication(0);

    private void ExitApplication(int exitCode)
    {
        Cleanup();
        Shutdown(exitCode);
    }

    /// <summary>Creates the settings window on first request, then shows and activates it (tray, hotkey, second launch, first run).</summary>
    private void ShowSettings()
    {
        if (_shell is null)
        {
            return;
        }

        _settingsWindow ??= new SettingsWindow(_shell);
        _settingsWindow.ShowAndActivate();
    }

    private void Cleanup()
    {
        var window = _settingsWindow;
        _settingsWindow = null;
        window?.PrepareForExit();
        var shell = _shell;
        _shell = null;
        shell?.Dispose();
        var instance = _instance;
        _instance = null;
        instance?.Dispose();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _log?.Error("unhandled dispatcher exception", e.Exception);
        e.Handled = true;
        MessageBox.Show(Loc.Instance.Format("Error.Unhandled", e.Exception.Message), Caption, MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
