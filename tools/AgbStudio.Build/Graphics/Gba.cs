namespace AgbStudio.Build.Graphics;

/// <summary>GBA colour and OAM helpers.</summary>
public static class Gba
{
    /// <summary>ARGB to BGR555 using the nearest of the 32 levels per channel.</summary>
    public static ushort ToBgr555(uint argb)
    {
        int r = (int)((argb >> 16) & 0xFF), g = (int)((argb >> 8) & 0xFF), b = (int)(argb & 0xFF);
        return (ushort)(Level(r) | (Level(g) << 5) | (Level(b) << 10));
    }

    static int Level(int v) => (v * 31 + 127) / 255;

    public static uint ToArgb(ushort bgr)
    {
        int r = bgr & 31, g = (bgr >> 5) & 31, b = (bgr >> 10) & 31;
        return 0xFF000000u | ((uint)(r * 255 / 31) << 16) | ((uint)(g * 255 / 31) << 8) | (uint)(b * 255 / 31);
    }

    public static string Hex(ushort bgr) => $"0x{bgr:X4}";

    public static bool IsTransparent(uint argb) => (argb >> 24) < 128;

    /// <summary>OAM shape / size code for a frame size, or null when it is not a hardware size.</summary>
    public static (int Shape, int Size)? OamSize(int w, int h) => (w, h) switch
    {
        (8, 8) => (0, 0), (16, 16) => (0, 1), (32, 32) => (0, 2), (64, 64) => (0, 3),
        (16, 8) => (1, 0), (32, 8) => (1, 1), (32, 16) => (1, 2), (64, 32) => (1, 3),
        (8, 16) => (2, 0), (8, 32) => (2, 1), (16, 32) => (2, 2), (32, 64) => (2, 3),
        _ => null,
    };

    public static readonly (int W, int H)[] OamSizes =
    [
        (8, 8), (16, 16), (32, 32), (64, 64), (16, 8), (32, 8), (32, 16), (64, 32), (8, 16), (8, 32), (16, 32), (32, 64),
    ];

    /// <summary>The smallest OAM size that holds a w x h frame (frames are padded to it).</summary>
    public static (int W, int H) FitOam(int w, int h)
    {
        var best = (W: 64, H: 64);
        foreach (var s in OamSizes)
            if (s.W >= w && s.H >= h && s.W * s.H < best.W * best.H) best = s;
        if (w > 64 || h > 64) throw new ArgumentException($"A {w}x{h} frame is larger than the biggest GBA sprite (64x64).");
        return best;
    }

    /// <summary>Squared distance between two BGR555 colours (per-channel weights for perceived difference).</summary>
    public static int Distance(ushort a, ushort b)
    {
        int dr = (a & 31) - (b & 31), dg = ((a >> 5) & 31) - ((b >> 5) & 31), db = ((a >> 10) & 31) - ((b >> 10) & 31);
        return 3 * dr * dr + 4 * dg * dg + 2 * db * db;
    }
}
