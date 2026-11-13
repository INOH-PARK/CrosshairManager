using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CrosshairManager.Core.Model;

namespace CrosshairManager.App.ViewModels;

/// <summary>One of the eight fixed swatches (AutomationId Swatch0..Swatch7).</summary>
public sealed partial class SwatchViewModel(int index, Rgb color) : ObservableObject
{
    public int Index { get; } = index;

    public Rgb Color { get; } = color;

    public string Hex => Color.ToString();

    public string AutomationId => "Swatch" + Index.ToString(CultureInfo.InvariantCulture);

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}
