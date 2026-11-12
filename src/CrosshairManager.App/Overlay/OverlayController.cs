using System.Windows.Threading;
using CrosshairManager.App.Logging;
using CrosshairManager.Core.Geometry;
using CrosshairManager.Core.Model;
using CrosshairManager.Core.Render;
using CrosshairManager.Core.Settings;
using CrosshairManager.Platform.Monitors;
using CrosshairManager.Platform.Overlay;

namespace CrosshairManager.App.Overlay;

/// <summary>
/// Drives the overlay from the UI thread. <see cref="Apply"/> coalesces at Dispatcher Background priority and runs the fixed
/// pipeline Normalize -> ApplyScale -> CrosshairLayout (parity from the target monitor) -> CrosshairRasterizer -> OverlayPlacement.
/// Re-runs on MonitorService.Changed. Logs <c>overlay shown x,y,w,h</c> after every visible update and <c>overlay hidden</c>
/// when it becomes hidden. OverlayHost calls block until the overlay thread finished, so state is read right after them.
/// </summary>
public sealed class OverlayController : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly MonitorService _monitors;
    private readonly FileLog _log;
    private readonly OverlayHost _host;
    private readonly TopmostKeeper _keeper;
    private CrosshairSpec _spec = CrosshairSpec.Default;
    private MonitorSelection _selection = MonitorSelection.Primary;
    private IntRect _window;
    private bool _visible;
    private bool _pending;
    private bool? _loggedVisible;

    public OverlayController(Dispatcher dispatcher, MonitorService monitors, FileLog log)
    {
        _dispatcher = dispatcher;
        _monitors = monitors;
        _log = log;
        _host = new OverlayHost();
        _keeper = new TopmostKeeper(_host);
        _monitors.Changed += OnMonitorsChanged;
    }

    /// <summary>Raised on the UI thread when <see cref="MonitorFellBack"/> or <see cref="Monitor"/> changed.</summary>
    public event EventHandler? MonitorStateChanged;

    public bool Visible => _visible;

    /// <summary>The configured device is not connected; the primary monitor is used (settings are not rewritten).</summary>
    public bool MonitorFellBack { get; private set; }

    public MonitorInfo? Monitor { get; private set; }

    /// <summary>First paint: sets everything and renders synchronously so the first sentinel line reflects the real state.</summary>
    public void Initialize(CrosshairSpec spec, MonitorSelection selection, bool visible)
    {
        _spec = spec;
        _selection = selection;
        _visible = visible;
        if (visible)
        {
            _host.Show();
        }
        else
        {
            _host.Hide();
        }

        Render();
    }

    public void Apply(CrosshairSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        _spec = spec;
        Schedule();
    }

    public void SetMonitor(MonitorSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        _selection = selection;
        Schedule();
    }

    public void SetVisible(bool visible)
    {
        _visible = visible;
        if (visible)
        {
            _host.Show();
        }
        else
        {
            _host.Hide();
        }

        LogState();
    }

    public void Dispose()
    {
        _monitors.Changed -= OnMonitorsChanged;
        _keeper.Dispose();
        _host.Dispose();
    }

    private void OnMonitorsChanged(object? sender, EventArgs e) =>
        _dispatcher.BeginInvoke(DispatcherPriority.Background, Schedule);

    private void Schedule()
    {
        if (_pending)
        {
            return;
        }

        _pending = true;
        _dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            _pending = false;
            Render();
        });
    }

    private void Render()
    {
        var monitor = _monitors.Resolve(_selection, out var fellBack);
        if (fellBack != MonitorFellBack || monitor != Monitor)
        {
            if (fellBack && !MonitorFellBack)
            {
                _log.Info("monitor " + _selection.DeviceName + " not connected; using primary " + monitor.DeviceName);
            }

            MonitorFellBack = fellBack;
            Monitor = monitor;
            MonitorStateChanged?.Invoke(this, EventArgs.Empty);
        }

        var bounds = monitor.Bounds;
        var spec = _spec.Normalize().Spec;
        var geometry = CrosshairLayout.Compute(spec.ApplyScale(), bounds.W % 2 != 0, bounds.H % 2 != 0);
        if (geometry.CanvasW <= 0 || geometry.CanvasH <= 0)
        {
            // Every part is off: CrosshairRasterizer.Render rejects a 0x0 canvas, so this is simply hidden.
            _window = default;
            _host.Update(null, default);
        }
        else
        {
            var (window, region) = OverlayPlacement.Compute(geometry, bounds, spec.OffsetX, spec.OffsetY);
            _window = window;
            _host.Update(window.IsEmpty ? null : CrosshairRasterizer.Render(geometry, region), window);
        }

        LogState();
    }

    private void LogState()
    {
        var visible = _host.IsVisible;
        if (visible)
        {
            _log.Info($"overlay shown {_window.X},{_window.Y},{_window.W},{_window.H}");
        }
        else if (_loggedVisible != false)
        {
            _log.Info("overlay hidden");
        }

        _loggedVisible = visible;
    }
}
