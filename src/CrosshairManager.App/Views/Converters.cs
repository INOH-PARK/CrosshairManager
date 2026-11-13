using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using CrosshairManager.Core.Model;

namespace CrosshairManager.App.Views;

/// <summary>Core <see cref="Rgb"/> to a frozen <see cref="SolidColorBrush"/>.</summary>
public sealed class RgbToBrushConverter : IValueConverter
{
    private readonly Dictionary<Rgb, SolidColorBrush> _cache = [];

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Rgb rgb)
        {
            return null;
        }

        if (!_cache.TryGetValue(rgb, out var brush))
        {
            brush = new SolidColorBrush(Color.FromRgb(rgb.R, rgb.G, rgb.B));
            brush.Freeze();
            _cache[rgb] = brush;
        }

        return brush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
