using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using CrosshairManager.Core.Render;

namespace CrosshairManager.Core.Tests.Render;

public class PngCodecTests
{
    private static readonly byte[] Signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    [Fact]
    public void Round_trip_matches_for_random_buffers_from_1x1_to_33x17()
    {
        var random = new Random(20260612);
        for (int i = 0; i < 50; i++)
        {
            var original = RandomPremultipliedBuffer(random, random.Next(1, 34), random.Next(1, 18));
            var decoded = Png.Decode(Png.Encode(original));
            Assert.Equal(original, decoded);
        }
    }

    [Fact]
    public void Signature_and_IHDR_and_IEND_CRC_match_known_constants()
    {
        byte[] bytes = Png.Encode(new PixelBuffer(1, 1));

        Assert.Equal(Signature, bytes[..8]);
        Assert.Equal(0x1F15C489u, BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(29)));
        Assert.Equal(0xAE426082u, BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(bytes.Length - 4)));
    }

    [Fact]
    public void Flipping_one_byte_is_detected_as_crc_mismatch()
    {
        byte[] bytes = Png.Encode(RandomPremultipliedBuffer(new Random(7), 2, 2));
        bytes[19] ^= 0x01; // IHDR data starts at offset 16; byte 19 is the width's low byte

        var ex = Assert.Throws<InvalidDataException>(() => Png.Decode(bytes));
        Assert.Contains("CRC", ex.Message);
    }

    [Fact]
    public void Decode_accepts_filter_types_0_to_4()
    {
        const int width = 5, height = 5;
        var random = new Random(4);
        var rgba = new byte[width * height * 4];
        random.NextBytes(rgba);
        for (int i = 3; i < rgba.Length; i += 4) rgba[i] = 255; // opaque, so decoded values equal the input

        byte[] png = BuildPng(width, height, rgba, y => y % 5);
        var decoded = Png.Decode(png);

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int i = (y * width + x) * 4;
                Assert.Equal(new PixelBuffer.Pixel(rgba[i + 2], rgba[i + 1], rgba[i], 255), decoded.GetPixel(x, y));
            }
    }

    private static PixelBuffer RandomPremultipliedBuffer(Random random, int width, int height)
    {
        var buffer = new PixelBuffer(width, height);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                byte a = (byte)random.Next(256);
                byte Channel() => a == 0 ? (byte)0 : (byte)random.Next(a + 1);
                buffer.SetPixel(x, y, new PixelBuffer.Pixel(Channel(), Channel(), Channel(), a));
            }
        return buffer;
    }

    private static byte[] BuildPng(int width, int height, byte[] rgba, Func<int, int> filterFor)
    {
        int stride = width * 4;
        var raw = new MemoryStream();
        for (int y = 0; y < height; y++)
        {
            int f = filterFor(y);
            raw.WriteByte((byte)f);
            for (int x = 0; x < stride; x++)
            {
                int cur = rgba[y * stride + x];
                int left = x >= 4 ? rgba[y * stride + x - 4] : 0;
                int up = y > 0 ? rgba[(y - 1) * stride + x] : 0;
                int upLeft = x >= 4 && y > 0 ? rgba[(y - 1) * stride + x - 4] : 0;
                int predicted = f switch
                {
                    0 => 0,
                    1 => left,
                    2 => up,
                    3 => (left + up) / 2,
                    4 => Paeth(left, up, upLeft),
                    _ => throw new ArgumentOutOfRangeException(nameof(filterFor)),
                };
                raw.WriteByte((byte)(cur - predicted));
            }
        }

        byte[] compressed;
        using (var ms = new MemoryStream())
        {
            using (var z = new ZLibStream(ms, CompressionLevel.Optimal, leaveOpen: true))
                z.Write(raw.ToArray());
            compressed = ms.ToArray();
        }

        var ihdr = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(0), (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(4), (uint)height);
        ihdr[8] = 8;
        ihdr[9] = 6;

        var output = new MemoryStream();
        output.Write(Signature);
        WriteChunk(output, "IHDR", ihdr);
        WriteChunk(output, "IDAT", compressed);
        WriteChunk(output, "IEND", Array.Empty<byte>());
        return output.ToArray();
    }

    private static void WriteChunk(Stream output, string type, byte[] data)
    {
        var typeAndData = new byte[4 + data.Length];
        Encoding.ASCII.GetBytes(type).CopyTo(typeAndData, 0);
        data.CopyTo(typeAndData, 4);
        var word = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(word, (uint)data.Length);
        output.Write(word);
        output.Write(typeAndData);
        BinaryPrimitives.WriteUInt32BigEndian(word, Crc32(typeAndData));
        output.Write(word);
    }

    private static int Paeth(int a, int b, int c)
    {
        int p = a + b - c;
        int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    private static uint Crc32(ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFFu;
        foreach (byte b in data)
        {
            crc ^= b;
            for (int k = 0; k < 8; k++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }
        return ~crc;
    }
}
