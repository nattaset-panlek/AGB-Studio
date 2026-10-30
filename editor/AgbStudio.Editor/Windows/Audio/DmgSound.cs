using System.Globalization;
using AgbStudio.Core.Project;

namespace AgbStudio.Editor.Windows.Audio;

/// <summary>One sound register write: its GBA name, I/O offset (from 0x04000000) and 16-bit value.</summary>
public readonly record struct SoundRegister(string Name, int Offset, ushort Value)
{
    public string Hex => $"0x{Value:X4}";
}

/// <summary>
/// The DMG sound channels of the GBA (SFX Designer, T-P25): turns an <see cref="SfxFile"/> into the exact
/// SOUNDxCNT register values the runtime writes (agb::audio::DmgSfx), and synthesises the same sound for the preview
/// with the hardware rules (512 Hz frame sequencer: length 256 Hz, sweep 128 Hz, envelope 64 Hz; square duty
/// patterns, 4-bit wave RAM, 15 / 7-bit noise LFSR).
/// </summary>
public static class DmgSound
{
    public const double FrameRate = 59.7275;
    public static readonly double[] Duties = [0.125, 0.25, 0.5, 0.75];
    static readonly string[] Names = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];

    /// <summary>"C5" -> MIDI 72, "A4" -> 69.</summary>
    public static int Midi(string note)
    {
        note = note.Trim().Replace("-", "");
        var sharp = note.Length > 1 && note[1] == '#';
        var name = sharp ? note[..2] : note[..1].ToUpperInvariant();
        var octave = int.Parse(note[(sharp ? 2 : 1)..], CultureInfo.InvariantCulture);
        return 12 * (octave + 1) + Array.IndexOf(Names, name);
    }

    public static string NoteName(int midi) => Names[((midi % 12) + 12) % 12] + (midi / 12 - 1).ToString(CultureInfo.InvariantCulture);

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

    public static double NoiseHz(int divider, int shift) => 524288 / (divider == 0 ? 0.5 : divider) / Math.Pow(2, shift + 1);

    /// <summary>Hardware length field and whether it is used (the sound stops itself); longer sounds are stopped by the runtime timer.</summary>
    public static (int Field, bool Use) LengthField(SfxFile s)
    {
        var units = s.Channel == DmgChannelKind.Wave ? 256 : 64;
        var ticks = (int)Math.Round(s.Length * 256);
        if (ticks < 1 || ticks > units) return (0, false);
        return (units - ticks, true);
    }

    /// <summary>How many frames the effect owns its channel (agb::audio::DmgSfx::frames).</summary>
    public static int Frames(SfxFile s) => Math.Max(1, (int)Math.Ceiling(s.Length * FrameRate));

    static int DutyCode(double duty) => Array.IndexOf(Duties, Duties.MinBy(d => Math.Abs(d - duty)));

    static int Envelope(SfxFile s) => (Math.Clamp(s.Volume, 0, 15) << 12) | (s.EnvelopeUp ? 1 << 11 : 0) | (Math.Clamp(s.EnvelopeStep, 0, 7) << 8);

    /// <summary>The register writes for this effect, in the order the runtime performs them (wave RAM before SOUND3CNT).</summary>
    public static IReadOnlyList<SoundRegister> Registers(SfxFile s)
    {
        var (len, useLen) = LengthField(s);
        var lenFlag = useLen ? 0x4000 : 0;
        var hz = Hz(s.StartNote);
        switch (s.Channel)
        {
            case DmgChannelKind.Square1:
                return
                [
                    new("SOUND1CNT_L", 0x60, (ushort)((Math.Clamp(s.SweepTime, 0, 7) << 4) | (s.SweepUp ? 0 : 8) | Math.Clamp(s.SweepShift, 0, 7))),
                    new("SOUND1CNT_H", 0x62, (ushort)(Envelope(s) | (DutyCode(s.Duty) << 6) | len)),
                    new("SOUND1CNT_X", 0x64, (ushort)(0x8000 | lenFlag | SquareRate(hz))),
                ];
            case DmgChannelKind.Square2:
                return
                [
                    new("SOUND2CNT_L", 0x68, (ushort)(Envelope(s) | (DutyCode(s.Duty) << 6) | len)),
                    new("SOUND2CNT_H", 0x6C, (ushort)(0x8000 | lenFlag | SquareRate(hz))),
                ];
            case DmgChannelKind.Wave:
                var list = new List<SoundRegister> { new("SOUND3CNT_L", 0x70, 0x0000) };
                var w = WaveSamples(s);
                for (var i = 0; i < 8; i++)
                {
                    // each byte holds two samples, the first in the high nibble
                    var lo = (w[i * 4] << 4) | w[i * 4 + 1];
                    var hi = (w[i * 4 + 2] << 4) | w[i * 4 + 3];
                    list.Add(new SoundRegister($"WAVE_RAM{i / 2}_{(i % 2 == 0 ? "L" : "H")}", 0x90 + i * 2, (ushort)(lo | (hi << 8))));
                }
                list.Add(new("SOUND3CNT_L", 0x70, 0x00C0));
                list.Add(new("SOUND3CNT_H", 0x72, (ushort)((WaveVolumeCode(s.Volume) << 13) | len)));
                list.Add(new("SOUND3CNT_X", 0x74, (ushort)(0x8000 | lenFlag | WaveRate(hz))));
                return list;
            default:
                var shift = NoiseShift(hz, s.NoiseDivider);
                return
                [
                    new("SOUND4CNT_L", 0x78, (ushort)(Envelope(s) | len)),
                    new("SOUND4CNT_H", 0x7C, (ushort)(0x8000 | lenFlag | (shift << 4) | (s.NoiseShort ? 8 : 0) | Math.Clamp(s.NoiseDivider, 0, 7))),
                ];
        }
    }

    /// <summary>The registers the Registers panel lists: the channel's control registers (wave RAM writes folded away).</summary>
    public static IReadOnlyList<SoundRegister> Shown(SfxFile s)
    {
        var all = Registers(s);
        var result = new List<SoundRegister>();
        foreach (var r in all)
        {
            if (r.Name.StartsWith("WAVE_RAM", StringComparison.Ordinal)) continue;
            var i = result.FindIndex(x => x.Name == r.Name);
            if (i >= 0) result[i] = r; else result.Add(r);
        }
        return result;
    }

    /// <summary>Wave channel volume: 0 = mute, 1 = 100 %, 2 = 50 %, 3 = 25 % (from the 0..15 volume).</summary>
    public static int WaveVolumeCode(int volume) => volume switch { <= 1 => 0, <= 5 => 3, <= 10 => 2, _ => 1 };

    /// <summary>32 samples of 4 bits; an all-zero wave gets a triangle so a new effect is audible.</summary>
    public static int[] WaveSamples(SfxFile s)
    {
        if (s.Wave.Length == 32 && s.Wave.Any(v => v != 0)) return [.. s.Wave.Select(v => Math.Clamp(v, 0, 15))];
        return [.. Enumerable.Range(0, 32).Select(i => i < 16 ? i : 31 - i)];
    }

    /// <summary>Pitch (Hz) and volume (0..15) at each step of the 512 Hz frame sequencer until the effect ends.</summary>
    public sealed record Trace(double[] Time, double[] Pitch, int[] Volume, double End);

    public static Trace Simulate(SfxFile s) => Run(s, 0, null);

    /// <summary>Renders the effect as mono samples (-1..1) at <paramref name="rate"/>.</summary>
    public static float[] Render(SfxFile s, int rate)
    {
        var n = (int)Math.Ceiling(Frames(s) / FrameRate * rate) + 1;
        var buf = new float[n];
        Run(s, rate, buf);
        return buf;
    }

    static Trace Run(SfxFile s, int rate, float[]? output)
    {
        var regs = Registers(s);
        ushort Reg(int offset) => regs.LastOrDefault(r => r.Offset == offset).Value;
        var ch = s.Channel;
        var (lenField, useLen) = LengthField(s);
        var lengthCounter = (ch == DmgChannelKind.Wave ? 256 : 64) - lenField;
        int x, sweepTime = 0, sweepShift = 0;
        var sweepDown = false;
        if (ch == DmgChannelKind.Square1)
        {
            var l = Reg(0x60);
            sweepShift = l & 7;
            sweepDown = (l & 8) != 0;
            sweepTime = (l >> 4) & 7;
        }
        x = ch switch
        {
            DmgChannelKind.Square1 => Reg(0x64) & 0x7FF,
            DmgChannelKind.Square2 => Reg(0x6C) & 0x7FF,
            DmgChannelKind.Wave => Reg(0x74) & 0x7FF,
            _ => 0,
        };
        var env = ch switch { DmgChannelKind.Square1 => Reg(0x62), DmgChannelKind.Square2 => Reg(0x68), DmgChannelKind.Noise => Reg(0x78), _ => (ushort)0 };
        var volume = ch == DmgChannelKind.Wave ? Math.Clamp(s.Volume, 0, 15) : env >> 12;
        var envUp = (env & 0x800) != 0;
        var envStep = (env >> 8) & 7;
        var duty = Duties[DutyCode(s.Duty)];
        var noise = Reg(0x7C);
        var noiseShort = (noise & 8) != 0;
        var noiseHz = NoiseHz(noise & 7, (noise >> 4) & 15);
        var waveScale = WaveVolumeCode(s.Volume) switch { 1 => 1.0, 2 => 0.5, 3 => 0.25, _ => 0.0 };
        var wave = WaveSamples(s);
        var active = true;
        // trigger: a sweep with a shift checks for overflow at once
        if (ch == DmgChannelKind.Square1 && sweepShift != 0 && !sweepDown && x + (x >> sweepShift) > 2047) active = false;
        var end = Frames(s) / FrameRate;
        int sweepTimer = sweepTime, envTimer = envStep;
        double Freq() => ch switch
        {
            DmgChannelKind.Noise => noiseHz,
            DmgChannelKind.Wave => 65536.0 / (2048 - x),
            _ => 131072.0 / (2048 - x),
        };

        var times = new List<double>();
        var pitch = new List<double>();
        var vols = new List<int>();
        var steps = (int)Math.Ceiling(end * 512);
        double phase = 0, noiseClock = 0;
        var lfsr = 0x7FFF;
        var sample = 0;
        var stoppedAt = end;
        for (var k = 0; k <= steps; k++)
        {
            var t = k / 512.0;
            if (t >= end) { if (active) stoppedAt = end; active = false; }
            times.Add(t);
            pitch.Add(active ? Freq() : double.NaN);
            vols.Add(active ? volume : 0);
            // audio for this step
            if (output != null)
            {
                var until = Math.Min(output.Length, (int)Math.Round((k + 1) / 512.0 * rate));
                for (; sample < until; sample++)
                {
                    if (!active) { output[sample] = 0; continue; }
                    var f = Freq();
                    float v;
                    switch (ch)
                    {
                        case DmgChannelKind.Noise:
                            noiseClock += f / rate;
                            while (noiseClock >= 1)
                            {
                                noiseClock -= 1;
                                var bit = (lfsr ^ (lfsr >> 1)) & 1;
                                lfsr = (lfsr >> 1) | (bit << 14);
                                if (noiseShort) lfsr = (lfsr & ~0x40) | (bit << 6);
                            }
                            v = (lfsr & 1) == 0 ? 1 : -1;
                            break;
                        case DmgChannelKind.Wave:
                            phase = (phase + f / rate) % 1;
                            v = (float)((wave[(int)(phase * 32) & 31] - 7.5) / 7.5 * waveScale);
                            break;
                        default:
                            phase = (phase + f / rate) % 1;
                            v = phase < duty ? 1 : -1;
                            break;
                    }
                    output[sample] = ch == DmgChannelKind.Wave ? v * 0.6f : v * volume / 15f * 0.6f;
                }
            }
            if (!active) continue;
            // frame sequencer step k (0..7 repeating)
            var seq = k & 7;
            if (useLen && seq % 2 == 0 && --lengthCounter <= 0) { active = false; stoppedAt = (k + 1) / 512.0; }
            if (ch == DmgChannelKind.Square1 && (seq == 2 || seq == 6) && sweepTime > 0 && --sweepTimer <= 0)
            {
                sweepTimer = sweepTime;
                if (sweepShift > 0)
                {
                    var nx = sweepDown ? x - (x >> sweepShift) : x + (x >> sweepShift);
                    if (nx > 2047) { active = false; stoppedAt = (k + 1) / 512.0; }
                    else if (nx >= 0)
                    {
                        x = nx;
                        if (!sweepDown && x + (x >> sweepShift) > 2047) { active = false; stoppedAt = (k + 1) / 512.0; }
                    }
                }
            }
            if (ch != DmgChannelKind.Wave && seq == 7 && envStep > 0 && --envTimer <= 0)
            {
                envTimer = envStep;
                if (envUp && volume < 15) volume++;
                else if (!envUp && volume > 0) volume--;
            }
        }
        return new Trace([.. times], [.. pitch], [.. vols], Math.Min(end, stoppedAt));
    }

    /// <summary>The 11 kHz mono 8-bit sample written by "Bake to .wav".</summary>
    public static sbyte[] Bake(SfxFile s) => [.. Render(s, 11025).Select(v => (sbyte)Math.Clamp(Math.Round(v * 127), -127, 127))];

    /// <summary>Bytes of ROM the effect costs: 0 for hardware playback, the 8-bit sample when baked.</summary>
    public static int RomBytes(SfxFile s) => s.Bake ? (int)Math.Ceiling(s.Length * 11025) : 0;
}

/// <summary>The presets of the SFX Designer (Figma: Jump, Coin, Hurt, Laser, Explosion, Power-up, Select, Step).</summary>
public static class SfxPresets
{
    public static readonly IReadOnlyList<string> Names = ["Jump", "Coin", "Hurt", "Laser", "Explosion", "Power-up", "Select", "Step"];

    /// <summary>Applies a preset's sound parameters to <paramref name="s"/> (name and output stay).</summary>
    public static void Apply(string preset, SfxFile s)
    {
        void Set(DmgChannelKind ch, string note, double len, int vol, int env, bool envUp = false, double duty = 0.5, int sweepTime = 0, int sweepShift = 0, bool up = true, int div = 3, bool shortNoise = false)
        {
            s.Channel = ch; s.StartNote = note; s.Length = len; s.Volume = vol; s.EnvelopeStep = env; s.EnvelopeUp = envUp; s.Duty = duty;
            s.SweepTime = sweepTime; s.SweepShift = sweepShift; s.SweepUp = up; s.NoiseDivider = div; s.NoiseShort = shortNoise;
        }
        switch (preset)
        {
            case "Jump": Set(DmgChannelKind.Square1, "C5", 0.21, 15, 2, sweepTime: 3, sweepShift: 6, up: true); break;
            case "Coin": Set(DmgChannelKind.Square2, "B5", 0.12, 12, 2, duty: 0.5); break;
            case "Hurt": Set(DmgChannelKind.Noise, "C3", 0.30, 15, 3, div: 3); break;
            case "Laser": Set(DmgChannelKind.Square1, "C6", 0.25, 14, 1, duty: 0.125, sweepTime: 1, sweepShift: 2, up: false); break;
            case "Explosion": Set(DmgChannelKind.Noise, "C2", 0.64, 15, 5, div: 7); break;
            case "Power-up": Set(DmgChannelKind.Square1, "C4", 0.50, 13, 0, duty: 0.25, sweepTime: 4, sweepShift: 5, up: true); break;
            case "Select": Set(DmgChannelKind.Square2, "C6", 0.05, 10, 0, duty: 0.25); break;
            case "Step": Set(DmgChannelKind.Noise, "C5", 0.04, 8, 1, div: 1, shortNoise: true); break;
            default: throw new ArgumentException("Unknown preset " + preset);
        }
    }

    /// <summary>The preset whose parameters equal the effect's, or null.</summary>
    public static string? Matching(SfxFile s) => Names.FirstOrDefault(n =>
    {
        var p = new SfxFile();
        Apply(n, p);
        return p.Channel == s.Channel && p.StartNote == s.StartNote && Math.Abs(p.Length - s.Length) < 1e-6 && p.Volume == s.Volume && p.EnvelopeStep == s.EnvelopeStep
            && p.EnvelopeUp == s.EnvelopeUp && (s.Channel is not (DmgChannelKind.Square1 or DmgChannelKind.Square2) || Math.Abs(p.Duty - s.Duty) < 1e-6)
            && (s.Channel != DmgChannelKind.Square1 || (p.SweepTime == s.SweepTime && p.SweepShift == s.SweepShift && p.SweepUp == s.SweepUp))
            && (s.Channel != DmgChannelKind.Noise || (p.NoiseDivider == s.NoiseDivider && p.NoiseShort == s.NoiseShort));
    });
}
