namespace AgbStudio.Build.Graphics;

/// <summary>A picture as Mode 4 stores it: one palette index per pixel (0 = see-through, the backdrop shows) and the
/// colours of indices 1, 2, ... <see cref="SourceColors"/> is how many colours the image had before any reduction.</summary>
public sealed record IndexedPicture(byte[] Indices, ushort[] Palette, int SourceColors)
{
    public bool Reduced => SourceColors > BitmapConverter.MaxColors;
}

/// <summary>
/// Pictures for the bitmap video modes (Scene › Picture, Bitmap.Show). Mode 3 and Mode 5 store 15-bit colours; they have
/// no see-through colour, so transparent pixels are stored black. Mode 4 stores palette indices: index 0 is the backdrop,
/// which leaves 255 colours, and an image with more is reduced to 255 by the median cut of <see cref="Quantizer"/> with
/// each pixel taking the nearest colour, no dithering. The build warns when that happens. The Scene Editor draws from the
/// same conversion.
/// </summary>
public static class BitmapConverter
{
    /// <summary>Palette indices 1..255: index 0 is the backdrop.</summary>
    public const int MaxColors = 255;

    public static bool IsBitmapMode(int mode) => mode is >= 3 and <= 5;

    /// <summary>The picture size of a bitmap mode: 240 x 160 in Modes 3 and 4, 160 x 128 in Mode 5.</summary>
    public static (int Width, int Height) PictureSize(int mode) => mode == 5 ? (160, 128) : (240, 160);

    /// <summary>The picture's 15-bit colours, row-major.</summary>
    public static ushort[] ToBgr(ColorImage img)
    {
        var px = new ushort[img.Width * img.Height];
        for (var i = 0; i < px.Length; i++) px[i] = img.Transparent[i] ? (ushort)0 : img.Colors[i];
        return px;
    }

    /// <summary>The picture as palette indices, row-major, with at most <see cref="MaxColors"/> colours.</summary>
    public static IndexedPicture ToIndexed(ColorImage img)
    {
        var histogram = img.Histogram();
        var palette = Quantizer.Reduce(histogram, MaxColors).OrderBy(c => c).ToArray();
        // Exact colours go straight to their index; the rest take the nearest one once per colour.
        var indexOf = new Dictionary<ushort, byte>(histogram.Count);
        for (var i = 0; i < palette.Length; i++) indexOf[palette[i]] = (byte)(i + 1);
        foreach (var color in histogram.Keys)
            if (!indexOf.ContainsKey(color)) indexOf[color] = (byte)(Nearest(palette, color) + 1);
        var indices = new byte[img.Width * img.Height];
        for (var i = 0; i < indices.Length; i++) indices[i] = img.Transparent[i] ? (byte)0 : indexOf[img.Colors[i]];
        return new IndexedPicture(indices, palette, histogram.Count);
    }

    static int Nearest(ushort[] palette, ushort color)
    {
        var best = 0;
        var bestDistance = int.MaxValue;
        for (var i = 0; i < palette.Length; i++)
        {
            int dr = (color & 31) - (palette[i] & 31), dg = ((color >> 5) & 31) - ((palette[i] >> 5) & 31), db = ((color >> 10) & 31) - ((palette[i] >> 10) & 31);
            var distance = dr * dr + dg * dg + db * db;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }
        return best;
    }
}
