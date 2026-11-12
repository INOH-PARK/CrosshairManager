using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CrosshairManager.Core.Render;

namespace CrosshairManager.Desktop.Tests;

[Trait("Category", "Desktop")]
public class PngDecoderInteropTests
{
    [Fact]
    public void PngBitmapDecoder_decodes_encoder_output_3x2_to_the_same_pixels()
    {
        var buffer = new PixelBuffer(3, 2);
        buffer.SetPixel(0, 0, new PixelBuffer.Pixel(0, 0, 255, 255));
        buffer.SetPixel(1, 0, new PixelBuffer.Pixel(0, 255, 0, 255));
        buffer.SetPixel(2, 0, new PixelBuffer.Pixel(255, 0, 0, 255));
        buffer.SetPixel(0, 1, new PixelBuffer.Pixel(0, 0, 0, 0));
        buffer.SetPixel(1, 1, new PixelBuffer.Pixel(255, 255, 255, 255));
        buffer.SetPixel(2, 1, new PixelBuffer.Pixel(64, 64, 64, 128)); // premultiplied; PNG stores 128,128,128,128

        byte[] png = Png.Encode(buffer);
        using var stream = new MemoryStream(png);
        var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = new FormatConvertedBitmap(decoder.Frames[0], PixelFormats.Bgra32, null, 0);

        Assert.Equal(3, frame.PixelWidth);
        Assert.Equal(2, frame.PixelHeight);
        var actual = new byte[3 * 2 * 4];
        frame.CopyPixels(actual, 3 * 4, 0);
        byte[] expected =
        {
            0, 0, 255, 255,   0, 255, 0, 255,   255, 0, 0, 255,
            0, 0, 0, 0,       255, 255, 255, 255,   128, 128, 128, 128,
        };
        Assert.Equal(expected, actual);
    }
}
