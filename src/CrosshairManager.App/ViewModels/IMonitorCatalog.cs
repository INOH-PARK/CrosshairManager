using System.Windows.Threading;
using CrosshairManager.Platform.Monitors;

namespace CrosshairManager.App.ViewModels;

/// <summary>Connected monitors for the MonitorSelect combo; <see cref="Changed"/> is raised on the UI thread.</summary>
public interface IMonitorCatalog
{
    event EventHandler? Changed;

    IReadOnlyList<MonitorInfo> GetMonitors();
}

/// <summary>Wraps <see cref="MonitorService"/> and marshals its SystemEvents-thread notifications to the dispatcher.</summary>
public sealed class MonitorCatalog : IMonitorCatalog, IDisposable
{
    private readonly MonitorService _service = new();
    private readonly Dispatcher _dispatcher;

    public MonitorCatalog(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _service.Changed += OnChanged;
    }

    public event EventHandler? Changed;

    public IReadOnlyList<MonitorInfo> GetMonitors() => _service.GetMonitors();

    public void Dispose()
    {
        _service.Changed -= OnChanged;
        _service.Dispose();
    }

    private void OnChanged(object? sender, EventArgs e) =>
        _dispatcher.BeginInvoke(() => Changed?.Invoke(this, EventArgs.Empty));
}
