using System.Buffers.Binary;
using System.IO.Compression;

namespace AgbStudio.Emu;

/// <summary>Minimal PNG encoder and decoder for 8-bit RGBA / RGB / palette images (no interlace).</summary>
public static class Png
{
    static readonly uint[] CrcTable = BuildCrcTable();

    public static void WriteArgb(string path, uint[] argb, int width, int height)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllBytes(path, EncodeArgb(argb, width, height));
    }

    public static byte[] EncodeArgb(uint[] argb, int width, int height)
    {
        var raw = new byte[height * (width * 4 + 1)];
        var o = 0;
        for (var y = 0; y < height; y++)
        {
            raw[o++] = 0;
            for (var x = 0; x < width; x++)
            {
                var p = argb[y * width + x];
                raw[o++] = (byte)(p >> 16);
                raw[o++] = (byte)(p >> 8);
                raw[o++] = (byte)p;
                raw[o++] = (byte)(p >> 24);
            }
        }
        using var ms = new MemoryStream();
        ms.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr, width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4), height);
        ihdr[8] = 8;
        ihdr[9] = 6;
        WriteChunk(ms, "IHDR", ihdr);
        using (var z = new MemoryStream())
        {
            using (var zs = new ZLibStream(z, CompressionLevel.Optimal, true)) zs.Write(raw);
            WriteChunk(ms, "IDAT", z.ToArray());
        }
        WriteChunk(ms, "IEND", []);
        return ms.ToArray();
    }

    /// <summary>Decoded image as ARGB pixels plus the palette when the file is indexed.</summary>
    public sealed record Image(int Width, int Height, uint[] Pixels, uint[]? Palette, byte[]? Indices);

    public static Image Read(string path) => Decode(File.ReadAllBytes(path));

    public static Image Decode(byte[] data)
    {
        if (data.Length < 8 || data[0] != 0x89 || data[1] != 'P') throw new InvalidDataException("Not a PNG file.");
        int width = 0, height = 0, depth = 0, colorType = 0;
        uint[]? palette = null;
        byte[]? trns = null;
        using var idat = new MemoryStream();
        var pos = 8;
        while (pos + 8 <= data.Length)
        {
            var len = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(pos));
            var type = System.Text.Encoding.ASCII.GetString(data, pos + 4, 4);
            var body = data.AsSpan(pos + 8, len);
            switch (type)
            {
                case "IHDR":
                    width = BinaryPrimitives.ReadInt32BigEndian(body);
                    height = BinaryPrimitives.ReadInt32BigEndian(body[4..]);
                    depth = body[8];
                    colorType = body[9];
                    if (body[12] != 0) throw new InvalidDataException("Interlaced PNG files are not supported.");
                    break;
                case "PLTE":
                    palette = new uint[len / 3];
                    for (var i = 0; i < palette.Length; i++)
                        palette[i] = 0xFF000000u | ((uint)body[i * 3] << 16) | ((uint)body[i * 3 + 1] << 8) | body[i * 3 + 2];
                    break;
                case "tRNS":
                    trns = body.ToArray();
                    break;
                case "IDAT":
                    idat.Write(body);
                    break;
            }
            pos += 12 + len;
        }
        if (palette != null && trns != null)
            for (var i = 0; i < Math.Min(trns.Length, palette.Length); i++)
                palette[i] = (palette[i] & 0x00FFFFFFu) | ((uint)trns[i] << 24);

        int channels = colorType switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, 6 => 4, _ => throw new InvalidDataException("Unknown PNG colour type.") };
        if (depth != 8 && !(colorType == 3 && depth is 1 or 2 or 4)) throw new InvalidDataException($"PNG bit depth {depth} is not supported.");
        var bitsPerPixel = channels * depth;
        var stride = (width * bitsPerPixel + 7) / 8;
        var bpp = Math.Max(1, bitsPerPixel / 8);
        idat.Position = 0;
        using var zs = new ZLibStream(idat, CompressionMode.Decompress);
        var raw = new byte[height * (stride + 1)];
        var read = 0;
        while (read < raw.Length)
        {
            var n = zs.Read(raw, read, raw.Length - read);
            if (n == 0) break;
            read += n;
        }
        var cur = new byte[stride];
        var prev = new byte[stride];
        var pixels = new uint[width * height];
        var indices = colorType == 3 ? new byte[width * height] : null;
        for (var y = 0; y < height; y++)
        {
            var filter = raw[y * (stride + 1)];
            Array.Copy(raw, y * (stride + 1) + 1, cur, 0, stride);
            Unfilter(filter, cur, prev, bpp);
            for (var x = 0; x < width; x++)
            {
                uint p;
                switch (colorType)
                {
                    case 3:
                        int bitPos = x * depth, idx = (cur[bitPos / 8] >> (8 - depth - bitPos % 8)) & ((1 << depth) - 1);
                        indices![y * width + x] = (byte)idx;
                        p = palette != null && idx < palette.Length ? palette[idx] : 0xFF000000u;
                        break;
                    case 0: p = 0xFF000000u | (uint)(cur[x] * 0x010101); break;
                    case 4: p = ((uint)cur[x * 2 + 1] << 24) | (uint)(cur[x * 2] * 0x010101); break;
                    case 2: p = 0xFF000000u | ((uint)cur[x * 3] << 16) | ((uint)cur[x * 3 + 1] << 8) | cur[x * 3 + 2]; break;
                    default: p = ((uint)cur[x * 4 + 3] << 24) | ((uint)cur[x * 4] << 16) | ((uint)cur[x * 4 + 1] << 8) | cur[x * 4 + 2]; break;
                }
                pixels[y * width + x] = p;
            }
            (prev, cur) = (cur, prev);
        }
        return new Image(width, height, pixels, palette, indices);
    }

    static void Unfilter(byte filter, byte[] cur, byte[] prev, int bpp)
    {
        for (var i = 0; i < cur.Length; i++)
        {
            int a = i >= bpp ? cur[i - bpp] : 0, b = prev[i], c = i >= bpp ? prev[i - bpp] : 0;
            cur[i] = filter switch
            {
                0 => cur[i],
                1 => (byte)(cur[i] + a),
                2 => (byte)(cur[i] + b),
                3 => (byte)(cur[i] + (a + b) / 2),
                4 => (byte)(cur[i] + Paeth(a, b, c)),
                _ => throw new InvalidDataException("Bad PNG filter."),
            };
        }
    }

    static int Paeth(int a, int b, int c)
    {
        int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    static void WriteChunk(Stream s, string type, byte[] body)
    {
        Span<byte> len = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(len, body.Length);
        s.Write(len);
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        s.Write(typeBytes);
        s.Write(body);
        var crc = Crc(typeBytes, body);
        BinaryPrimitives.WriteUInt32BigEndian(len, crc);
        s.Write(len);
    }

    static uint Crc(byte[] a, byte[] b)
    {
        var c = 0xFFFFFFFFu;
        foreach (var x in a) c = CrcTable[(c ^ x) & 0xFF] ^ (c >> 8);
        foreach (var x in b) c = CrcTable[(c ^ x) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFFu;
    }

    static uint[] BuildCrcTable()
    {
        var t = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            t[n] = c;
        }
        return t;
    }
}
