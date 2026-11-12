using CrosshairManager.Core.Model;
using CrosshairManager.Core.Render;
using CrosshairManager.TestSupport;

namespace CrosshairManager.Desktop.Tests.Capture;

/// <summary>Pure math: premultiplied BGRA over an opaque backdrop. Creates no windows and captures nothing.</summary>
[Trait("Category", "Desktop")]
public class CompositeTests
{
    private static readonly Rgb Magenta = new(0xFF, 0x00, 0xFF);

    private static PixelBuffer Single(PixelBuffer.Pixel pixel)
    {
        var buffer = new PixelBuffer(1, 1);
        buffer.SetPixel(0, 0, pixel);
        return buffer;
    }

    [Fact]
    public void Composite_transparent_pixel_yields_the_opaque_backdrop()
    {
        var result = Composite.Over(Single(new PixelBuffer.Pixel(0, 0, 0, 0)), Magenta);
        Assert.Equal(new PixelBuffer.Pixel(0xFF, 0x00, 0xFF, 0xFF), result.GetPixel(0, 0));
    }

    [Fact]
    public void Composite_opaque_pixel_replaces_the_backdrop()
    {
        var result = Composite.Over(Single(new PixelBuffer.Pixel(0, 0, 255, 255)), Magenta);
        Assert.Equal(new PixelBuffer.Pixel(0, 0, 255, 255), result.GetPixel(0, 0));
    }

    [Fact]
    public void Composite_half_alpha_adds_the_rounded_remaining_backdrop_share()
    {
        // premul red at alpha 128: c = p + round(bg * (255 - a) / 255) = p + round(255 * 127 / 255) = p + 127.
        var result = Composite.Over(Single(new PixelBuffer.Pixel(0, 0, 128, 128)), Magenta);
        Assert.Equal(new PixelBuffer.Pixel(127, 0, 255, 255), result.GetPixel(0, 0));
    }

    [Fact]
    public void Composite_rounds_half_up_on_the_backdrop_share()
    {
        // rest = 255 - 128 = 127: B bg 1 -> 0.498 -> 0; R bg 3 -> 1.494 -> 1.
        var result = Composite.Over(Single(new PixelBuffer.Pixel(0, 0, 0, 128)), new Rgb(3, 0, 1));
        Assert.Equal(new PixelBuffer.Pixel(0, 0, 1, 255), result.GetPixel(0, 0));
    }

    [Fact]
    public void Composite_preserves_size_and_does_not_mutate_the_input()
    {
        var input = new PixelBuffer(3, 2);
        input.SetPixel(2, 1, new PixelBuffer.Pixel(10, 20, 30, 40));
        var copy = new PixelBuffer(3, 2);
        copy.SetPixel(2, 1, new PixelBuffer.Pixel(10, 20, 30, 40));

        var result = Composite.Over(input, Magenta);

        Assert.Equal((3, 2), (result.Width, result.Height));
        Assert.Equal(copy, input);
        Assert.Equal(new PixelBuffer.Pixel(0xFF, 0x00, 0xFF, 0xFF), result.GetPixel(0, 0));
        // 10 + round(255*215/255)=225, 20 + 0, 30 + 215 = 245.
        Assert.Equal(new PixelBuffer.Pixel(225, 20, 245, 255), result.GetPixel(2, 1));
    }

    [Fact]
    public void Composite_null_buffer_throws()
    {
        Assert.Throws<ArgumentNullException>(() => Composite.Over(null!, Magenta));
    }
}
