using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CrosshairManager.Core.Model;

namespace CrosshairManager.App.Controls;

/// <summary>
/// Pixel-grid preview of a <see cref="CrosshairSpec"/> on a checkerboard: the Core PixelBuffer at integer <see cref="Zoom"/>,
/// NearestNeighbor, with the Image sized in DIPs = pixels * Zoom / DpiScale so one source pixel is Zoom x Zoom device pixels.
/// <see cref="Fit"/> (thumbnails) shrinks large crosshairs into the control instead of clipping them.
/// </summary>
public partial class PreviewControl : UserControl
{
    public static readonly DependencyProperty SpecProperty = DependencyProperty.Register(
        nameof(Spec), typeof(CrosshairSpec), typeof(PreviewControl), new PropertyMetadata(null, (d, _) => ((PreviewControl)d).Render()));

    public static readonly DependencyProperty ZoomProperty = DependencyProperty.Register(
        nameof(Zoom), typeof(int), typeof(PreviewControl), new PropertyMetadata(8, (d, _) => ((PreviewControl)d).Render()));

    public static readonly DependencyProperty FitProperty = DependencyProperty.Register(
        nameof(Fit), typeof(bool), typeof(PreviewControl), new PropertyMetadata(false, (d, _) => ((PreviewControl)d).Render()));

    public static readonly DependencyProperty PreviewAutomationIdProperty = DependencyProperty.Register(
        nameof(PreviewAutomationId), typeof(string), typeof(PreviewControl), new PropertyMetadata(null));

    public PreviewControl()
    {
        InitializeComponent();
        Loaded += (_, _) => Render();
    }

    public CrosshairSpec? Spec
    {
        get => (CrosshairSpec?)GetValue(SpecProperty);
        set => SetValue(SpecProperty, value);
    }

    public int Zoom
    {
        get => (int)GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public bool Fit
    {
        get => (bool)GetValue(FitProperty);
        set => SetValue(FitProperty, value);
    }

    /// <summary>AutomationId of the inner Image (the element whose bounds are the zoomed pixels).</summary>
    public string? PreviewAutomationId
    {
        get => (string?)GetValue(PreviewAutomationIdProperty);
        set => SetValue(PreviewAutomationIdProperty, value);
    }

    /// <summary>The rendered source pixels (null when the crosshair is empty).</summary>
    public BitmapSource? Bitmap { get; private set; }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        Render();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new CustomControlPeer(this, AutomationControlType.Pane, nameof(PreviewControl));

    private void Render()
    {
        var zoom = Math.Max(1, Zoom);
        var spec = Spec;
        Bitmap = spec is null ? null : CrosshairBitmap.Render(spec);
        if (Fit)
        {
            // Natural size = pixels * zoom (bitmap DPI 96 / zoom); shrink only when larger than the control.
            PixelImage.Source = spec is null ? null : CrosshairBitmap.Render(spec, 96.0 / zoom);
            PixelImage.Stretch = Stretch.Uniform;
            PixelImage.StretchDirection = StretchDirection.DownOnly;
            PixelImage.Width = double.NaN;
            PixelImage.Height = double.NaN;
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var width = Bitmap?.PixelWidth ?? 1;
        var height = Bitmap?.PixelHeight ?? 1;
        PixelImage.Source = Bitmap;
        PixelImage.Stretch = Stretch.Fill;
        PixelImage.StretchDirection = StretchDirection.Both;
        PixelImage.Width = width * zoom / dpi.DpiScaleX;
        PixelImage.Height = height * zoom / dpi.DpiScaleY;
    }
}
