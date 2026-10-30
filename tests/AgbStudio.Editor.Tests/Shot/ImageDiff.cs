using SkiaSharp;

namespace AgbStudio.Editor.Tests.Shot;

/// <summary>Result of comparing one Figma export with the app capture (SHOT step 5).</summary>
public sealed record DiffResult(
    string Id, int FigmaWidth, int FigmaHeight, int AppWidth, int AppHeight,
    double DiffPercent, double BestShiftDiffPercent, int ShiftX, int ShiftY, double MeanChannelError, double ColourDiffPercent)
{
    /// <summary>Layout offset estimate in px: the best alignment shift, or the size difference if larger.</summary>
    public int LayoutOffset => Math.Max(Math.Max(Math.Abs(ShiftX), Math.Abs(ShiftY)), Math.Max(Math.Abs(FigmaWidth - AppWidth), Math.Abs(FigmaHeight - AppHeight)));
}

/// <summary>
/// Pixel comparison of two PNGs composited on the same background. A pixel differs when a channel differs by more
/// than <see cref="Threshold"/> (anti-aliasing noise stays below it). The best shift in [-3, 3] estimates layout offset.
/// </summary>
public static class ImageDiff
{
    public const int Threshold = 48;
    const int MaxShift = 3;

    sealed class Img(int w, int h, byte[] rgb)
    {
        public int W = w, H = h;
        public byte[] Rgb = rgb;
    }

    static Img Load(string path, SKColor bg)
    {
        using var src = SKBitmap.Decode(path) ?? throw new InvalidDataException("Cannot read " + path);
        using var bmp = src.Copy(SKColorType.Rgba8888);
        var px = bmp.GetPixelSpan();
        var rgb = new byte[bmp.Width * bmp.Height * 3];
        var premul = bmp.AlphaType == SKAlphaType.Premul;
        for (int i = 0, j = 0; i < px.Length; i += 4, j += 3)
        {
            var a = px[i + 3] / 255.0;
            for (var c = 0; c < 3; c++)
            {
                var b = c == 0 ? bg.Red : c == 1 ? bg.Green : bg.Blue;
                var s = premul ? px[i + c] : px[i + c] * a;
                rgb[j + c] = (byte)Math.Clamp(Math.Round(s + b * (1 - a)), 0, 255);
            }
        }
        return new Img(bmp.Width, bmp.Height, rgb);
    }

    static (int Diff, long Err, int Colour) Count(Img a, Img b, int dx, int dy, int w, int h, SKColor bg, bool[]? mask = null)
    {
        int diff = 0, colour = 0;
        long err = 0;
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var pa = Pixel(a, x, y, bg);
                var pb = Pixel(b, x - dx, y - dy, bg);
                var m = Math.Max(Math.Abs(pa.r - pb.r), Math.Max(Math.Abs(pa.g - pb.g), Math.Abs(pa.b - pb.b)));
                err += Math.Abs(pa.r - pb.r) + Math.Abs(pa.g - pb.g) + Math.Abs(pa.b - pb.b);
                if (m > Threshold) { diff++; if (mask != null) mask[y * w + x] = true; }
                if (m > 8) colour++;
            }
        return (diff, err, colour);
    }

    static (int r, int g, int b) Pixel(Img i, int x, int y, SKColor bg)
    {
        if (x < 0 || y < 0 || x >= i.W || y >= i.H) return (bg.Red, bg.Green, bg.Blue);
        var k = (y * i.W + x) * 3;
        return (i.Rgb[k], i.Rgb[k + 1], i.Rgb[k + 2]);
    }

    /// <summary>Compares and writes a side-by-side diff (Figma | app | differences in red) to <paramref name="diffPath"/>.</summary>
    public static DiffResult Compare(string id, string figmaPath, string appPath, string diffPath, SKColor background)
    {
        var f = Load(figmaPath, background);
        var a = Load(appPath, background);
        int w = Math.Max(f.W, a.W), h = Math.Max(f.H, a.H);
        var mask = new bool[w * h];
        var (diff0, err0, colour0) = Count(f, a, 0, 0, w, h, background, mask);
        int best = diff0, bx = 0, by = 0;
        // big frames: search shifts on a coarse grid of rows to keep it fast
        for (var dy = -MaxShift; dy <= MaxShift; dy++)
            for (var dx = -MaxShift; dx <= MaxShift; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                var (d, _, _) = Count(f, a, dx, dy, w, h, background);
                if (d < best) { best = d; bx = dx; by = dy; }
            }
        WriteDiff(f, a, mask, w, h, diffPath, background);
        var n = (double)(w * h);
        return new DiffResult(id, f.W, f.H, a.W, a.H, Math.Round(100 * diff0 / n, 2), Math.Round(100 * best / n, 2), bx, by,
            Math.Round(err0 / (n * 3), 2), Math.Round(100 * colour0 / n, 2));
    }

    /// <summary>Diff percent inside one rectangle (no shift), e.g. the chrome regions of a page whose content is Phase 11.</summary>
    public static double RegionDiffPercent(string figmaPath, string appPath, SKRectI r, SKColor background)
    {
        var f = Load(figmaPath, background);
        var a = Load(appPath, background);
        var diff = 0;
        for (var y = r.Top; y < r.Bottom; y++)
            for (var x = r.Left; x < r.Right; x++)
            {
                var pa = Pixel(f, x, y, background);
                var pb = Pixel(a, x, y, background);
                if (Math.Max(Math.Abs(pa.r - pb.r), Math.Max(Math.Abs(pa.g - pb.g), Math.Abs(pa.b - pb.b))) > Threshold) diff++;
            }
        return Math.Round(100.0 * diff / (r.Width * r.Height), 2);
    }

    static void WriteDiff(Img f, Img a, bool[] mask, int w, int h, string path, SKColor bg)
    {
        const int gap = 8;
        using var bmp = new SKBitmap(w * 3 + gap * 2, h, SKColorType.Rgba8888, SKAlphaType.Opaque);
        bmp.Erase(new SKColor(0x30, 0x30, 0x30));
        void Blit(Img img, int ox)
        {
            for (var y = 0; y < img.H; y++)
                for (var x = 0; x < img.W; x++)
                {
                    var k = (y * img.W + x) * 3;
                    bmp.SetPixel(ox + x, y, new SKColor(img.Rgb[k], img.Rgb[k + 1], img.Rgb[k + 2]));
                }
        }
        Blit(f, 0);
        Blit(a, w + gap);
        var ox = 2 * (w + gap);
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var p = Pixel(f, x, y, bg);
                var g = (byte)((p.r + p.g + p.b) / 9);
                bmp.SetPixel(ox + x, y, mask[y * w + x] ? new SKColor(255, 64, 64) : new SKColor(g, g, g));
            }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var data = bmp.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
    }
}
