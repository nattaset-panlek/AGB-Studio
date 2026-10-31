using AgbStudio.Core.Project;

namespace AgbStudio.Editor.Windows.Palette;

/// <summary>
/// Previews of the palette effects frame by frame, computed the way the runtime does it
/// (agb::render::palettes: startFade / startCycle / flash, update once per frame, blend in 16 steps).
/// </summary>
public static class PaletteEffects
{
    /// <summary>Fade amount (0 = none, 16 = full) after <paramref name="frame"/> frames of a fade lasting <paramref name="frames"/>.</summary>
    public static int FadeAmount(int frame, int frames) => frames <= 0 ? 16 : Math.Clamp(16 * Math.Clamp(frame, 0, frames) / frames, 0, 16);

    public static ushort Fade(ushort c, ushort target, int frame, int frames) => Colour15.Blend(c, target, FadeAmount(frame, frames));

    /// <summary>Rotation offset of a cycle of <paramref name="length"/> colours after <paramref name="frame"/> frames (ticks per step, direction).</summary>
    public static int CycleOffset(int length, int ticks, CycleDirection dir, int frame)
    {
        if (length <= 1) return 0;
        ticks = Math.Max(1, ticks);
        var steps = Math.Max(0, frame) / ticks;
        switch (dir)
        {
            case CycleDirection.Left: return steps % length;
            case CycleDirection.Right: return (length - steps % length) % length;
            default:
                // ping-pong as the runtime steps it: +1 until the end, then -1 back to 0
                var period = 2 * (length - 1);
                var k = steps % period;
                return k < length ? k : period - k;
        }
    }

    /// <summary>The colours of a bank with indices first..last rotated by the cycle after <paramref name="frame"/> frames.</summary>
    public static ushort[] Cycle(IReadOnlyList<ushort> bank, int first, int last, int ticks, CycleDirection dir, int frame)
    {
        var result = bank.ToArray();
        first = Math.Clamp(first, 0, Math.Max(0, bank.Count - 1));
        last = Math.Clamp(last, first, Math.Max(0, bank.Count - 1));
        var len = last - first + 1;
        var offset = CycleOffset(len, ticks, dir, frame);
        for (var i = first; i <= last; i++) result[i] = bank[first + (i - first + offset) % len];
        return result;
    }

    /// <summary>True while a flash of <paramref name="frames"/> frames shows the flash colour.</summary>
    public static bool FlashOn(int frame, int frames) => frame >= 0 && frame < frames;

    /// <summary>Text of the Palette Manager dropdowns: "to black · 30 frames", "BG 1 · 6-9 · water".</summary>
    public static string FadeText(PaletteEffectData fade) => $"to {ColourName(fade.Color)} · {fade.Frames} frames";

    public static string CycleText(PaletteEffectData c) => $"{(c.BankKind == BankKind.Obj ? "OBJ" : "BG")} {c.Bank} · {c.First}-{c.Last} · {c.Name}";

    public static string ColourName(int c15) => c15 switch
    {
        0 => "black",
        0x7FFF => "white",
        _ => Colour15.Hex24(Colour15.Argb((ushort)c15)),
    };
}
