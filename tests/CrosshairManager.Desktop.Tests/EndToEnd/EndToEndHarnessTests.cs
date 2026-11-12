using CrosshairManager.Core.Geometry;
using CrosshairManager.Core.Model;
using CrosshairManager.Core.Presets;
using CrosshairManager.Core.Render;
using CrosshairManager.TestSupport;
using static CrosshairManager.Desktop.Tests.EndToEnd.EndToEndHarness;

namespace CrosshairManager.Desktop.Tests.EndToEnd;

/// <summary>Pure checks of the end-to-end harness (no windows, no capture) so its expectations cannot be vacuous.</summary>
[Trait("Category", "Desktop")]
public class EndToEndHarnessTests
{
    private static readonly Rgb Red = new(0xFF, 0x00, 0x00);

    [Theory]
    [InlineData("builtin.default", 1920, 1080)]
    [InlineData("builtin.dot", 1920, 1080)]
    [InlineData("builtin.classic1px", 1919, 1079)]
    [InlineData("builtin.x45", 1920, 1080)]
    public void Prepare_places_a_visible_frame_of_window_size_on_the_monitor(string id, int w, int h)
    {
        var monitor = new IntRect(1920, 0, w, h);
        var frame = Prepare(BuiltInPresets.All.Single(p => p.Id == id).Crosshair, monitor);

        Assert.NotNull(frame);
        Assert.Equal(frame.Window.W, frame.Pixels.Width);
        Assert.Equal(frame.Window.H, frame.Pixels.Height);
        Assert.Equal(1920 + (w / 2), frame.CenterX);
        Assert.Equal(h / 2, frame.CenterY);
        Assert.True(frame.Window.X >= monitor.X && frame.Window.X + frame.Window.W <= monitor.X + monitor.W);
    }

    [Fact]
    public void Prepare_returns_null_when_every_part_is_off()
    {
        var off = new LineSpec { Enabled = false };
        var spec = CrosshairSpec.Default with
        {
            Dot = CrosshairSpec.Default.Dot with { Enabled = false },
            Top = off,
            Bottom = off,
            Left = off,
            Right = off,
        };

        Assert.Null(Prepare(spec, new IntRect(0, 0, 1920, 1080)));
    }

    [Fact]
    public void Expected_composites_inside_the_window_and_keeps_the_margin_backdrop_colored()
    {
        var pixels = new PixelBuffer(2, 1);
        pixels.SetPixel(0, 0, new PixelBuffer.Pixel(0, 0, 255, 255)); // opaque red
        var frame = new OverlayFrame(pixels, new IntRect(100, 50, 2, 1), 100, 50);
        var capture = CaptureRect(frame);

        var expected = Expected(capture, BackdropWindow.Magenta, frame, new Rgb(0, 0, 0xFF));

        Assert.Equal(2 + (2 * BackdropWindow.Margin), expected.Width);
        Assert.Equal(new PixelBuffer.Pixel(0, 0, 255, 255), expected.GetPixel(BackdropWindow.Margin, BackdropWindow.Margin));
        Assert.Equal(new PixelBuffer.Pixel(255, 0, 0, 255), expected.GetPixel(BackdropWindow.Margin + 1, BackdropWindow.Margin));
        Assert.Equal(new PixelBuffer.Pixel(255, 0, 255, 255), expected.GetPixel(0, 0));
    }

    [Fact]
    public void Diff_counts_only_channels_beyond_the_tolerance()
    {
        var a = Solid(new IntRect(0, 0, 3, 2), Red);
        var b = Solid(new IntRect(0, 0, 3, 2), new Rgb(0xFD, 0x00, 0x01));

        Assert.Equal(6, Diff(a, b, 1).Mismatches);
        Assert.Equal(2, Diff(a, b, 1).MaxDiff);
        Assert.Equal(0, Diff(a, b, 2).Mismatches);
        Assert.Equal(0, Diff(a, a, 0).Mismatches);
        Assert.Equal(int.MaxValue, Diff(a, Solid(new IntRect(0, 0, 2, 2), Red), 255).Mismatches);
    }
}
