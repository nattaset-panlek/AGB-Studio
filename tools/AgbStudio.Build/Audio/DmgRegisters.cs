using System.Globalization;
using AgbStudio.Core.Project;

namespace AgbStudio.Build.Audio;

/// <summary>
/// SFX Designer effects (<see cref="SfxFile"/>, T-P25) as the register image the runtime writes
/// (agb::audio::DmgSfx): SOUNDxCNT values, the 4-bit wave RAM and how long the effect owns its channel. The SFX
/// Designer shows the same values in its Registers panel.
/// </summary>
public static class DmgRegisters
{
    public const double FrameRate = 59.7275;
    public static readonly double[] Duties = [0.125, 0.25, 0.5, 0.75];
    static readonly string[] Names = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];

    /// <summary>The runtime's DmgSfx fields for one effect.</summary>
    public sealed record Image(DmgChannelKind Channel, ushort Sweep, ushort Envelope, ushort Frequency, uint[]? Wave, int Frames);

    /// <summary>"C5" -> MIDI 72, "A4" -> 69.</summary>
    public static int Midi(string note)
    {
        note = note.Trim().Replace("-", "");
        var sharp = note.Length > 1 && note[1] == '#';
        var name = sharp ? note[..2] : note[..1].ToUpperInvariant();
        var octave = int.Parse(note[(sharp ? 2 : 1)..], CultureInfo.InvariantCulture);
        return 12 * (octave + 1) + Array.IndexOf(Names, name);
    }

    public static double Hz(string note) => 440 * Math.Pow(2, (Midi(note) - 69) / 12.0);

    /// <summary>11-bit frequency value of a square channel: f = 131072 / (2048 - x).</summary>
    public static int SquareRate(double hz) => Math.Clamp((int)Math.Round(2048 - 131072 / hz), 0, 2047);
    public static int WaveRate(double hz) => Math.Clamp((int)Math.Round(2048 - 65536 / hz), 0, 2047);

    /// <summary>Noise shift clock for a pitch, with the file's divider (r = 0 counts as 0.5).</summary>
    public static int NoiseShift(double hz, int divider)
    {
        var r = divider == 0 ? 0.5 : divider;
        var s = (int)Math.Round(Math.Log2(524288 / r / (hz * 8)) - 1);
        return Math.Clamp(s, 0, 13);
    }

    /// <summary>Hardware length field and whether it is used (the sound stops itself); longer sounds are stopped by the runtime timer.</summary>
    public static (int Field, bool Use) LengthField(SfxFile s)
    {
        var units = s.Channel == DmgChannelKind.Wave ? 256 : 64;
        var ticks = (int)Math.Round(s.Length * 256);
        if (ticks < 1 || ticks > units) return (0, false);
        return (units - ticks, true);
    }

    /// <summary>How many frames the effect owns its channel.</summary>
    public static int Frames(SfxFile s) => Math.Max(1, (int)Math.Ceiling(s.Length * FrameRate));

    static int DutyCode(double duty) => Array.IndexOf(Duties, Duties.MinBy(d => Math.Abs(d - duty)));

    static int Envelope(SfxFile s) => (Math.Clamp(s.Volume, 0, 15) << 12) | (s.EnvelopeUp ? 1 << 11 : 0) | (Math.Clamp(s.EnvelopeStep, 0, 7) << 8);

    /// <summary>Wave channel volume: 0 = mute, 1 = 100 %, 2 = 50 %, 3 = 25 % (from the 0..15 volume).</summary>
    public static int WaveVolumeCode(int volume) => volume switch { <= 1 => 0, <= 5 => 3, <= 10 => 2, _ => 1 };

    /// <summary>32 samples of 4 bits; an all-zero wave gets a triangle so a new effect is audible.</summary>
    public static int[] WaveSamples(SfxFile s)
    {
        if (s.Wave.Length == 32 && s.Wave.Any(v => v != 0)) return [.. s.Wave.Select(v => Math.Clamp(v, 0, 15))];
        return [.. Enumerable.Range(0, 32).Select(i => i < 16 ? i : 31 - i)];
    }

    /// <summary>Wave RAM as four words: each byte holds two samples, the first in the high nibble.</summary>
    public static uint[] WaveWords(SfxFile s)
    {
        var w = WaveSamples(s);
        var words = new uint[4];
        for (var b = 0; b < 16; b++) words[b / 4] |= (uint)((w[b * 2] << 4) | w[b * 2 + 1]) << (b % 4 * 8);
        return words;
    }

    public static Image Encode(SfxFile s)
    {
        var (len, useLen) = LengthField(s);
        var lenFlag = useLen ? 0x4000 : 0;
        var hz = Hz(s.StartNote);
        var frames = Frames(s);
        return s.Channel switch
        {
            DmgChannelKind.Square1 => new Image(s.Channel,
                (ushort)((Math.Clamp(s.SweepTime, 0, 7) << 4) | (s.SweepUp ? 0 : 8) | Math.Clamp(s.SweepShift, 0, 7)),
                (ushort)(Envelope(s) | (DutyCode(s.Duty) << 6) | len), (ushort)(lenFlag | SquareRate(hz)), null, frames),
            DmgChannelKind.Square2 => new Image(s.Channel, 0, (ushort)(Envelope(s) | (DutyCode(s.Duty) << 6) | len), (ushort)(lenFlag | SquareRate(hz)), null, frames),
            DmgChannelKind.Wave => new Image(s.Channel, 0, (ushort)((WaveVolumeCode(s.Volume) << 13) | len), (ushort)(lenFlag | WaveRate(hz)), WaveWords(s), frames),
            _ => new Image(s.Channel, 0, (ushort)(Envelope(s) | len),
                (ushort)(lenFlag | (NoiseShift(hz, s.NoiseDivider) << 4) | (s.NoiseShort ? 8 : 0) | Math.Clamp(s.NoiseDivider, 0, 7)), null, frames),
        };
    }
}
