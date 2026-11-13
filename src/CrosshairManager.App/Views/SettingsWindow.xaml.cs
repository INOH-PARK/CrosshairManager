using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using CrosshairManager.App.ViewModels;
using CrosshairManager.Core.Settings;
using WinForeground = CrosshairManager.Platform.Windowing.Foreground;

namespace CrosshairManager.App.Views;

/// <summary>
/// The settings window. Close hides to the tray (saving <c>settingsWindowPlacement</c> in physical pixels); the first
/// render shows <see cref="DisclaimerDialog"/> until it is accepted; <see cref="ShowAndActivate"/> restores the saved
/// placement inside a visible monitor and applies the Must have foreground rule.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly MonitorCatalog _monitors;
    private readonly SettingsViewModel _viewModel;
    private bool _exiting;
    private bool _disclaimerShown;

    public SettingsWindow(IAppShell shell)
    {
        ArgumentNullException.ThrowIfNull(shell);
        _monitors = new MonitorCatalog(Dispatcher);
        _viewModel = new SettingsViewModel(shell, new DialogService(this), _monitors, new DispatcherDebounce(SettingsViewModel.AutosaveDelay, Dispatcher));
        InitializeComponent();
        DataContext = _viewModel;
        RestorePlacement();
        ContentRendered += OnContentRendered;
        Closing += OnClosing;
    }

    public SettingsViewModel ViewModel => _viewModel;

    /// <summary>Shows the window (restoring its placement) and brings it to the foreground.</summary>
    public void ShowAndActivate()
    {
        if (!IsVisible)
        {
            RestorePlacement();
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == 0 || !WinForeground.Activate(hwnd))
        {
            Activate();
        }
    }

    /// <summary>Hides to the tray: flushes a pending autosave and stores the placement.</summary>
    public void HideToTray()
    {
        _viewModel.FlushPendingSave();
        SavePlacement();
        Hide();
    }

    /// <summary>Application exit: flush, save placement, release the view model; the window may now really close.</summary>
    public void PrepareForExit()
    {
        _exiting = true;
        _viewModel.FlushPendingSave();
        if (IsVisible)
        {
            SavePlacement();
        }

        _viewModel.Dispose();
        _monitors.Dispose();
    }

    private void OnContentRendered(object? sender, EventArgs e)
    {
        if (_viewModel.NeedsDisclaimer && !_disclaimerShown)
        {
            _disclaimerShown = true;
            var dialog = new DisclaimerDialog { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                _viewModel.AcceptDisclaimer();
            }
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_exiting)
        {
            return;
        }

        e.Cancel = true;
        HideToTray();
    }

    /// <summary>Saved placement is physical pixels; clamp it into the work area of the monitor holding its center.</summary>
    private void RestorePlacement()
    {
        var placement = _viewModel.Placement;
        if (placement is null)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return;
        }

        var monitors = _monitors.GetMonitors();
        var centerX = placement.Left + (placement.Width / 2);
        var centerY = placement.Top + (placement.Height / 2);
        var monitor = monitors.FirstOrDefault(m => Contains(m.WorkArea, centerX, centerY)) ?? monitors.First(m => m.IsPrimary);
        var area = monitor.WorkArea;
        var width = Math.Clamp(placement.Width, 1, area.W);
        var height = Math.Clamp(placement.Height, 1, area.H);
        var left = Math.Clamp(placement.Left, area.X, area.X + area.W - width);
        var top = Math.Clamp(placement.Top, area.Y, area.Y + area.H - height);
        var scaleX = monitor.DpiX / 96.0;
        var scaleY = monitor.DpiY / 96.0;

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = left / scaleX;
        Top = top / scaleY;
        Width = Math.Max(MinWidth, width / scaleX);
        Height = Math.Max(MinHeight, height / scaleY);
    }

    private static bool Contains(Core.Geometry.IntRect rect, int x, int y) =>
        x >= rect.X && x < rect.X + rect.W && y >= rect.Y && y < rect.Y + rect.H;

    private void SavePlacement()
    {
        var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, ActualWidth, ActualHeight) : RestoreBounds;
        if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var toDevice = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
        var topLeft = toDevice.Transform(new Point(bounds.Left, bounds.Top));
        var bottomRight = toDevice.Transform(new Point(bounds.Right, bounds.Bottom));
        _viewModel.SavePlacement(new WindowPlacement(
            (int)Math.Round(topLeft.X),
            (int)Math.Round(topLeft.Y),
            (int)Math.Round(bottomRight.X - topLeft.X),
            (int)Math.Round(bottomRight.Y - topLeft.Y)));
    }
}
