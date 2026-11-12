namespace CrosshairManager.Core.Render;

/// <summary>Premultiplied BGRA pixels, top-down, stride Width * 4.</summary>
public sealed class PixelBuffer : IEquatable<PixelBuffer>
{
    private readonly byte[] _bytes;

    public PixelBuffer(int width, int height)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        Width = width;
        Height = height;
        Stride = width * 4;
        _bytes = new byte[Stride * height];
    }

    public int Width { get; }

    public int Height { get; }

    public int Stride { get; }

    public ReadOnlySpan<byte> Bytes => _bytes;

    public void SetPixel(int x, int y, Pixel pixel)
    {
        int o = y * Stride + x * 4;
        _bytes[o] = pixel.B;
        _bytes[o + 1] = pixel.G;
        _bytes[o + 2] = pixel.R;
        _bytes[o + 3] = pixel.A;
    }

    public Pixel GetPixel(int x, int y)
    {
        int o = y * Stride + x * 4;
        return new Pixel(_bytes[o], _bytes[o + 1], _bytes[o + 2], _bytes[o + 3]);
    }

    public bool Equals(PixelBuffer? other) =>
        other is not null && Width == other.Width && Height == other.Height
        && _bytes.AsSpan().SequenceEqual(other._bytes);

    public override bool Equals(object? obj) => Equals(obj as PixelBuffer);

    public override int GetHashCode() => HashCode.Combine(Width, Height);

    public readonly record struct Pixel(byte B, byte G, byte R, byte A);
}
