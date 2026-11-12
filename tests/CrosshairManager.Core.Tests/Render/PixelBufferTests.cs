using CrosshairManager.Core.Render;

namespace CrosshairManager.Core.Tests.Render;

public class PixelBufferTests
{
    [Fact]
    public void Pixels_are_stored_top_down_as_bgra_with_stride_width_times_four()
    {
        var buffer = new PixelBuffer(3, 2);
        buffer.SetPixel(1, 1, new PixelBuffer.Pixel(10, 20, 30, 40));

        Assert.Equal(12, buffer.Stride);
        Assert.Equal(new PixelBuffer.Pixel(10, 20, 30, 40), buffer.GetPixel(1, 1));
        int offset = 1 * buffer.Stride + 1 * 4;
        Assert.Equal(new byte[] { 10, 20, 30, 40 }, buffer.Bytes.Slice(offset, 4).ToArray());
    }

    [Fact]
    public void Equals_compares_dimensions_and_bytes()
    {
        var a = new PixelBuffer(2, 1);
        var b = new PixelBuffer(2, 1);
        Assert.Equal(a, b);

        b.SetPixel(0, 0, new PixelBuffer.Pixel(1, 0, 0, 0));
        Assert.NotEqual(a, b);
    }
}
