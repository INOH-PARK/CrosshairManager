using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace CrosshairManager.Core.Render;

/// <summary>Minimal PNG codec: 8-bit RGBA encode, 8-bit RGB/RGBA decode with filters 0-4.</summary>
public static class Png
{
    private static readonly byte[] Signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    private static readonly uint[] CrcTable = BuildCrcTable();

    public static byte[] Encode(PixelBuffer image)
    {
        int width = image.Width, height = image.Height;
        var raw = new byte[height * (1 + width * 4)];
        int o = 0;
        for (int y = 0; y < height; y++)
        {
            raw[o++] = 0; // filter type: None
            for (int x = 0; x < width; x++)
            {
                var p = image.GetPixel(x, y);
                raw[o++] = Unpremultiply(p.R, p.A);
                raw[o++] = Unpremultiply(p.G, p.A);
                raw[o++] = Unpremultiply(p.B, p.A);
                raw[o++] = p.A;
            }
        }

        byte[] compressed;
        using (var ms = new MemoryStream())
        {
            using (var z = new ZLibStream(ms, CompressionLevel.Optimal, leaveOpen: true))
                z.Write(raw);
            compressed = ms.ToArray();
        }

        var ihdr = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(0), (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(4), (uint)height);
        ihdr[8] = 8; // bit depth
        ihdr[9] = 6; // color type RGBA

        var output = new MemoryStream();
        output.Write(Signature);
        WriteChunk(output, "IHDR", ihdr);
        WriteChunk(output, "IDAT", compressed);
        WriteChunk(output, "IEND", Array.Empty<byte>());
        return output.ToArray();
    }

    public static PixelBuffer Decode(byte[] png)
    {
        if (png.Length < 8 || !png.AsSpan(0, 8).SequenceEqual(Signature))
            throw new InvalidDataException("Not a PNG file.");

        int width = 0, height = 0, colorType = 0;
        var idat = new MemoryStream();
        int pos = 8;
        while (true)
        {
            if (pos + 12 > png.Length) throw new InvalidDataException("Truncated PNG chunk.");
            uint length = BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(pos));
            string type = Encoding.ASCII.GetString(png, pos + 4, 4);
            long dataStart = pos + 8;
            long end = dataStart + length + 4;
            if (end > png.Length) throw new InvalidDataException("Truncated PNG chunk.");

            int dataLength = (int)length;
            uint expectedCrc = BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan((int)dataStart + dataLength));
            if (Crc32(png.AsSpan(pos + 4, 4 + dataLength)) != expectedCrc)
                throw new InvalidDataException($"PNG CRC mismatch in {type} chunk.");

            var data = png.AsSpan((int)dataStart, dataLength);
            if (type == "IHDR")
            {
                width = (int)BinaryPrimitives.ReadUInt32BigEndian(data);
                height = (int)BinaryPrimitives.ReadUInt32BigEndian(data[4..]);
                if (data[8] != 8 || (data[9] != 6 && data[9] != 2) || data[12] != 0)
                    throw new InvalidDataException("Unsupported PNG format; only 8-bit non-interlaced RGB/RGBA is supported.");
                colorType = data[9];
            }
            else if (type == "IDAT")
            {
                idat.Write(data);
            }
            else if (type == "IEND")
            {
                break;
            }

            pos = (int)end;
        }

        if (width <= 0 || height <= 0) throw new InvalidDataException("PNG is missing a valid IHDR chunk.");

        int bpp = colorType == 6 ? 4 : 3;
        int stride = width * bpp;
        var raw = new MemoryStream();
        idat.Position = 0;
        using (var z = new ZLibStream(idat, CompressionMode.Decompress))
            z.CopyTo(raw);
        byte[] rawBytes = raw.ToArray();
        if (rawBytes.Length != height * (stride + 1)) throw new InvalidDataException("PNG image data has the wrong size.");

        var recon = new byte[height * stride];
        for (int y = 0; y < height; y++)
        {
            int filter = rawBytes[y * (stride + 1)];
            int src = y * (stride + 1) + 1;
            int dst = y * stride;
            for (int x = 0; x < stride; x++)
            {
                int left = x >= bpp ? recon[dst + x - bpp] : 0;
                int up = y > 0 ? recon[dst - stride + x] : 0;
                int upLeft = x >= bpp && y > 0 ? recon[dst - stride + x - bpp] : 0;
                int predicted = filter switch
                {
                    0 => 0,
                    1 => left,
                    2 => up,
                    3 => (left + up) / 2,
                    4 => Paeth(left, up, upLeft),
                    _ => throw new InvalidDataException("Unknown PNG filter type."),
                };
                recon[dst + x] = (byte)(rawBytes[src + x] + predicted);
            }
        }

        var buffer = new PixelBuffer(width, height);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int i = y * stride + x * bpp;
                byte r = recon[i], g = recon[i + 1], b = recon[i + 2];
                byte a = bpp == 4 ? recon[i + 3] : (byte)255;
                buffer.SetPixel(x, y, new PixelBuffer.Pixel(Premultiply(b, a), Premultiply(g, a), Premultiply(r, a), a));
            }
        return buffer;
    }

    private static byte Unpremultiply(byte channel, byte alpha)
    {
        if (alpha == 0) return 0;
        return (byte)((2 * channel * 255 + alpha) / (2 * alpha));
    }

    private static byte Premultiply(byte channel, byte alpha) => (byte)((channel * alpha + 127) / 255);

    private static int Paeth(int a, int b, int c)
    {
        int p = a + b - c;
        int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
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

    private static uint Crc32(ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFFu;
        foreach (byte b in data)
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return ~crc;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }
}
