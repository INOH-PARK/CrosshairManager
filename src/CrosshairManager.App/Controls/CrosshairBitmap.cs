using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CrosshairManager.Core.Geometry;
using CrosshairManager.Core.Model;
using CrosshairManager.Core.Render;

namespace CrosshairManager.App.Controls;

/// <summary>Runs the fixed pipeline (Normalize -> ApplyScale -> Layout -> Rasterizer) into a frozen premultiplied-BGRA bitmap.</summary>
public static class CrosshairBitmap
{
    /// <summary>Even monitor parity (P on a pixel boundary). Null when every part is off (empty canvas).</summary>
    public static BitmapSource? Render(CrosshairSpec spec, double dpi = 96)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var normalized = spec.Normalize().Spec;
        var geometry = CrosshairLayout.Compute(normalized.ApplyScale(), oddWidth: false, oddHeight: false);
        if (geometry.CanvasW <= 0 || geometry.CanvasH <= 0)
        {
            return null;
        }

        var pixels = CrosshairRasterizer.Render(geometry);
        var bitmap = new WriteableBitmap(pixels.Width, pixels.Height, dpi, dpi, PixelFormats.Pbgra32, null);
        bitmap.WritePixels(new Int32Rect(0, 0, pixels.Width, pixels.Height), pixels.Bytes.ToArray(), pixels.Stride, 0);
        bitmap.Freeze();
        return bitmap;
    }
}
