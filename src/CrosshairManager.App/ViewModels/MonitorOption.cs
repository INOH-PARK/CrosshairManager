using CrosshairManager.Core.Settings;

namespace CrosshairManager.App.ViewModels;

/// <summary>A MonitorSelect entry. Equality is by <see cref="Selection"/> so the combo keeps its selection across refreshes.</summary>
public sealed class MonitorOption(MonitorSelection selection, string label)
{
    public MonitorSelection Selection { get; } = selection;

    public string Label { get; } = label;

    public override bool Equals(object? obj) => obj is MonitorOption other && Selection.Equals(other.Selection);

    public override int GetHashCode() => Selection.GetHashCode();

    public override string ToString() => Label;
}
