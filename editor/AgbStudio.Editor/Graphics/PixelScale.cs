using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace AgbStudio.Editor.Graphics;

/// <summary>
/// Game pixels at a fractional scale, as Figma draws a pixel-art picture placed at that size: every pixel is a square,
/// and a screen pixel that a square edge crosses is the area-weighted mix of the colours it covers (no blur, no
/// nearest-neighbour pixels of uneven width, no dark seam where two colours meet). At a whole-number scale the result is
/// the same as nearest neighbour.
/// </summary>
public static class PixelScale
{
    /// <summary>
    /// <paramref name="argb"/> (0xAARRGGBB, <paramref name="width"/> x <paramref name="height"/>) drawn at
    /// <paramref name="scale"/> into a bitmap of ceil(width * scale) x ceil(height * scale) pixels, to draw 1:1.
    /// </summary>
    public static WriteableBitmap Area(uint[] argb, int width, int height, double scale)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        var dw = Math.Max(1, (int)Math.Ceiling(width * scale - 1e-6));
        var dh = Math.Max(1, (int)Math.Ceiling(height * scale - 1e-6));
        var xs = Spans(width, dw, scale);
        var ys = Spans(height, dh, scale);
        var outPx = new int[dw * dh];
        var norm = scale * scale;
        for (var dy = 0; dy < dh; dy++)
            for (var dx = 0; dx < dw; dx++)
            {
                double a = 0, r = 0, g = 0, b = 0;
                foreach (var (sy, wy) in ys[dy])
                    foreach (var (sx, wx) in xs[dx])
                    {
                        var i = sy * width + sx;
                        var c = i < argb.Length ? argb[i] : 0u;
                        var ca = (c >> 24) / 255.0;
                        if (ca <= 0) continue;
                        var w = wx * wy * norm * ca;
                        a += w;
                        r += w * (c >> 16 & 0xFF);
                        g += w * (c >> 8 & 0xFF);
                        b += w * (c & 0xFF);
                    }
                // premultiplied BGRA
                uint A = (uint)Math.Clamp(Math.Round(a * 255), 0, 255), R = (uint)Math.Clamp(Math.Round(r), 0, 255),
                    G = (uint)Math.Clamp(Math.Round(g), 0, 255), B = (uint)Math.Clamp(Math.Round(b), 0, 255);
                outPx[dy * dw + dx] = unchecked((int)(A << 24 | R << 16 | G << 8 | B));
            }
        var bmp = new WriteableBitmap(new PixelSize(dw, dh), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var fb = bmp.Lock();
        for (var y = 0; y < dh; y++) Marshal.Copy(outPx, y * dw, fb.Address + y * fb.RowBytes, dw);
        return bmp;
    }

    /// <summary>True when <paramref name="scale"/> is a whole number (nearest neighbour draws it exactly).</summary>
    public static bool IsWhole(double scale) => Math.Abs(scale - Math.Round(scale)) < 1e-6;

    /// <summary>Per screen pixel along one axis: the source pixels it covers and the covered length (source units).</summary>
    static List<(int Src, double W)>[] Spans(int n, int dn, double scale)
    {
        var spans = new List<(int, double)>[dn];
        for (var d = 0; d < dn; d++)
        {
            spans[d] = [];
            double s0 = d / scale, s1 = (d + 1) / scale;
            for (var s = (int)Math.Floor(s0); s < Math.Min(n, (int)Math.Ceiling(s1 - 1e-9)); s++)
            {
                var w = Math.Min(s1, s + 1) - Math.Max(s0, s);
                if (w > 1e-9) spans[d].Add((s, w));
            }
        }
        return spans;
    }
}
