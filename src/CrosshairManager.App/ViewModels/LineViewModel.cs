using CommunityToolkit.Mvvm.ComponentModel;
using CrosshairManager.Core.Model;

namespace CrosshairManager.App.ViewModels;

/// <summary>One of the four crosshair lines. Every change is reported to the owner unless <see cref="Load"/> is running.</summary>
public sealed partial class LineViewModel : ObservableObject
{
    private readonly Action<LineViewModel> _changed;
    private bool _loading;

    public LineViewModel(string id, Action<LineViewModel> changed)
    {
        Id = id;
        _changed = changed;
        Load(new LineSpec());
    }

    /// <summary>"Top", "Bottom", "Left" or "Right" (AutomationId prefix).</summary>
    public string Id { get; }

    [ObservableProperty]
    public partial bool Enabled { get; set; }

    [ObservableProperty]
    public partial int Length { get; set; }

    [ObservableProperty]
    public partial int Thickness { get; set; }

    [ObservableProperty]
    public partial int Gap { get; set; }

    public LineSpec ToSpec() => new(Enabled, Length, Thickness, Gap);

    public void Load(LineSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        _loading = true;
        try
        {
            Enabled = spec.Enabled;
            Length = spec.Length;
            Thickness = spec.Thickness;
            Gap = spec.Gap;
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>Copies the numeric values (not Enabled) from another line; used by LinkLines.</summary>
    public void CopyValuesFrom(LineViewModel other)
    {
        ArgumentNullException.ThrowIfNull(other);
        Length = other.Length;
        Thickness = other.Thickness;
        Gap = other.Gap;
    }

    partial void OnEnabledChanged(bool value) => Notify();

    partial void OnLengthChanged(int value) => Notify();

    partial void OnThicknessChanged(int value) => Notify();

    partial void OnGapChanged(int value) => Notify();

    private void Notify()
    {
        if (!_loading)
        {
            _changed(this);
        }
    }
}
