using System.Text;

namespace AgbStudio.Build.Audio;

/// <summary>Writes 8-bit mono PCM WAV files (the format sound effects are resampled to: 11 kHz mono 8-bit).</summary>
public static class WavWriter
{
    public static byte[] Encode(ReadOnlySpan<sbyte> samples, int rate)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write("RIFF"u8);
        w.Write(36 + samples.Length);
        w.Write("WAVEfmt "u8);
        w.Write(16);
        w.Write((short)1);  // PCM
        w.Write((short)1);  // mono
        w.Write(rate);
        w.Write(rate);      // byte rate
        w.Write((short)1);  // block align
        w.Write((short)8);  // bits
        w.Write("data"u8);
        w.Write(samples.Length);
        foreach (var s in samples) w.Write((byte)(s + 128));  // 8-bit WAV is unsigned
        return ms.ToArray();
    }

    /// <summary>A short tone: square or sine at <paramref name="hz"/>, linear decay.</summary>
    public static sbyte[] Tone(double hz, double seconds, int rate, bool square = false, double volume = 0.8)
    {
        var n = (int)(seconds * rate);
        var data = new sbyte[n];
        for (var i = 0; i < n; i++)
        {
            var t = i / (double)rate;
            var env = 1.0 - i / (double)n;
            var v = square ? Math.Sign(Math.Sin(2 * Math.PI * hz * t)) : Math.Sin(2 * Math.PI * hz * t);
            data[i] = (sbyte)Math.Clamp(Math.Round(v * env * volume * 127), -127, 127);
        }
        return data;
    }
}

/// <summary>ProTracker 4-channel MOD ("M.K.") writer for generated chiptune music.</summary>
public sealed class ModWriter
{
    public sealed record Sample(string Name, sbyte[] Data, int Volume = 64, int LoopStart = 0, int LoopLength = 0);

    /// <summary>One cell of a pattern: note name like "C-2" (null = empty), sample number (1-based), effect.</summary>
    public readonly record struct Cell(string? Note, int Sample = 0, int Effect = 0, int Param = 0);

    public string Title { get; set; } = "agb";
    public List<Sample> Samples { get; } = new();
    public List<Cell[,]> Patterns { get; } = new();  // [row 0..63, channel 0..3]
    public List<int> Order { get; } = new();
    public int Restart { get; set; }

    static readonly string[] Names = ["C-", "C#", "D-", "D#", "E-", "F-", "F#", "G-", "G#", "A-", "A#", "B-"];

    // Amiga periods for octaves 1..3 (ProTracker C-1 .. B-3).
    static readonly int[] Periods =
    [
        856, 808, 762, 720, 678, 640, 604, 570, 538, 508, 480, 453,
        428, 404, 381, 360, 339, 320, 302, 285, 269, 254, 240, 226,
        214, 202, 190, 180, 170, 160, 151, 143, 135, 127, 120, 113,
    ];

    public static int Period(string note)
    {
        var name = note[..2];
        var octave = note[2] - '0';
        var idx = Array.IndexOf(Names, name);
        if (idx < 0 || octave < 1 || octave > 3) throw new ArgumentException($"Note '{note}' is outside C-1..B-3.");
        return Periods[(octave - 1) * 12 + idx];
    }

    public Cell[,] NewPattern() => new Cell[64, 4];

    public byte[] Encode()
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        WriteText(w, Title, 20);
        for (var i = 0; i < 31; i++)
        {
            var s = i < Samples.Count ? Samples[i] : null;
            WriteText(w, s?.Name ?? "", 22);
            var words = (s?.Data.Length ?? 0) / 2;
            WriteBe16(w, words);
            w.Write((byte)0);  // finetune
            w.Write((byte)(s?.Volume ?? 0));
            WriteBe16(w, (s?.LoopStart ?? 0) / 2);
            WriteBe16(w, s == null || s.LoopLength == 0 ? 1 : s.LoopLength / 2);
        }
        w.Write((byte)Order.Count);
        w.Write((byte)Restart);
        for (var i = 0; i < 128; i++) w.Write((byte)(i < Order.Count ? Order[i] : 0));
        w.Write("M.K."u8);
        foreach (var p in Patterns)
            for (var row = 0; row < 64; row++)
                for (var ch = 0; ch < 4; ch++)
                {
                    var c = p[row, ch];
                    var period = c.Note == null ? 0 : Period(c.Note);
                    w.Write((byte)((c.Sample & 0xF0) | ((period >> 8) & 0x0F)));
                    w.Write((byte)(period & 0xFF));
                    w.Write((byte)(((c.Sample & 0x0F) << 4) | (c.Effect & 0x0F)));
                    w.Write((byte)c.Param);
                }
        foreach (var s in Samples)
        {
            var data = s.Data.Length % 2 == 0 ? s.Data : [.. s.Data, 0];
            foreach (var b in data) w.Write((byte)b);
        }
        return ms.ToArray();
    }

    static void WriteText(BinaryWriter w, string s, int len)
    {
        var bytes = Encoding.ASCII.GetBytes(s.Length > len ? s[..len] : s);
        w.Write(bytes);
        for (var i = bytes.Length; i < len; i++) w.Write((byte)0);
    }

    static void WriteBe16(BinaryWriter w, int v)
    {
        w.Write((byte)(v >> 8));
        w.Write((byte)v);
    }

    /// <summary>One cycle of a waveform (looped instrument sample).</summary>
    public static sbyte[] Waveform(string kind, int length = 32)
    {
        var d = new sbyte[length];
        for (var i = 0; i < length; i++)
        {
            var t = i / (double)length;
            d[i] = kind switch
            {
                "square" => (sbyte)(t < 0.5 ? 100 : -100),
                "pulse25" => (sbyte)(t < 0.25 ? 100 : -100),
                "saw" => (sbyte)(-100 + 200 * t),
                "triangle" => (sbyte)(t < 0.5 ? -100 + 400 * t : 300 - 400 * t),
                _ => (sbyte)Math.Round(Math.Sin(2 * Math.PI * t) * 100),
            };
        }
        return d;
    }

    public static sbyte[] Noise(int length, int seed)
    {
        var r = new Random(seed);
        var d = new sbyte[length];
        for (var i = 0; i < length; i++) d[i] = (sbyte)(r.Next(-100, 101) * (1.0 - i / (double)length));
        return d;
    }
}
