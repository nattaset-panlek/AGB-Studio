using System.Globalization;
using AgbStudio.Build.Graphics;
using Avalonia.Media;

namespace AgbStudio.Editor.Windows.Palette;

/// <summary>
/// GBA colours: 15-bit BGR555 (5 bits per channel, R / G / B 0-31) and the 24-bit source colours of the art.
/// Palette and pixel colours are data, so they are built from numbers here, never from theme literals.
/// </summary>
public static class Colour15
{
    /// <summary>ARGB of a transparent pixel / the locked index 0.</summary>
    public const uint Transparent = 0;

    public static int R(ushort c) => c & 31;
    public static int G(ushort c) => (c >> 5) & 31;
    public static int B(ushort c) => (c >> 10) & 31;

    public static ushort Make(int r, int g, int b) =>
        (ushort)(Math.Clamp(r, 0, 31) | (Math.Clamp(g, 0, 31) << 5) | (Math.Clamp(b, 0, 31) << 10));

    /// <summary>The nearest of the 32 levels per channel (what the ROM stores).</summary>
    public static ushort Of(uint argb) => Gba.ToBgr555(argb);

    /// <summary>The 24-bit colour a 15-bit value shows on screen.</summary>
    public static uint Argb(ushort c) => Gba.ToArgb(c);

    public static bool IsTransparent(uint argb) => (argb >> 24) < 128;

    public static string Hex24(uint argb) => "#" + (argb & 0xFFFFFF).ToString("X6", CultureInfo.InvariantCulture);
    public static string Hex15(ushort c) => "0x" + c.ToString("X4", CultureInfo.InvariantCulture);

    public static bool TryParse24(string? s, out uint argb)
    {
        argb = 0;
        s = s?.Trim();
        if (string.IsNullOrEmpty(s)) return false;
        if (s[0] == '#') s = s[1..];
        if (s.Length != 6 || !uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v)) return false;
        argb = 0xFF000000u | v;
        return true;
    }

    public static bool TryParse15(string? s, out ushort c)
    {
        c = 0;
        s = s?.Trim();
        if (string.IsNullOrEmpty(s)) return false;
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s[2..];
        if (s.Length is 0 or > 4 || !ushort.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v) || v > 0x7FFF) return false;
        c = v;
        return true;
    }

    public static Color ToColor(uint argb) => Color.FromUInt32(argb);

    /// <summary>Linear blend toward <paramref name="b"/> by amount/16, exactly as agb::render::palettes::blend.</summary>
    public static ushort Blend(ushort a, ushort b, int amount16)
    {
        if (amount16 >= 16) return b;
        if (amount16 <= 0) return a;
        int r = R(a) + (R(b) - R(a)) * amount16 / 16, g = G(a) + (G(b) - G(a)) * amount16 / 16, bl = B(a) + (B(b) - B(a)) * amount16 / 16;
        return (ushort)(r | (g << 5) | (bl << 10));
    }

    /// <summary>Hue in degrees, -1 for greys (same order as Quantizer.SortByHue).</summary>
    public static double Hue(ushort c)
    {
        double r = R(c) / 31.0, g = G(c) / 31.0, b = B(c) / 31.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        if (max - min < 1e-9) return -1;
        var h = max == r ? (g - b) / (max - min) : max == g ? 2 + (b - r) / (max - min) : 4 + (r - g) / (max - min);
        return (h * 60 + 360) % 360;
    }
}
