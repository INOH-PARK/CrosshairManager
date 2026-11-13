using System.Windows.Threading;

namespace CrosshairManager.App.ViewModels;

/// <summary>Runs an action once a quiet period has passed since the last <see cref="Restart"/> (the 400 ms autosave).</summary>
public interface IDebounce : IDisposable
{
    bool IsPending { get; }

    void Restart(Action action);

    void Cancel();
}

/// <summary>DispatcherTimer-backed debounce; the action runs on the dispatcher thread.</summary>
public sealed class DispatcherDebounce : IDebounce
{
    private readonly DispatcherTimer _timer;
    private Action? _action;

    public DispatcherDebounce(TimeSpan delay, Dispatcher? dispatcher = null)
    {
        _timer = new DispatcherTimer(DispatcherPriority.Normal, dispatcher ?? Dispatcher.CurrentDispatcher) { Interval = delay };
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            var action = _action;
            _action = null;
            action?.Invoke();
        };
    }

    public bool IsPending => _timer.IsEnabled;

    public void Restart(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _action = action;
        _timer.Stop();
        _timer.Start();
    }

    public void Cancel()
    {
        _timer.Stop();
        _action = null;
    }

    public void Dispose() => Cancel();
}
