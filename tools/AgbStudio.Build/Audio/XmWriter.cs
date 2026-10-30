using System.Text;

namespace AgbStudio.Build.Audio;

/// <summary>FastTracker II .xm writer (one sample per instrument, 8-bit samples) for generated music.</summary>
public sealed class XmWriter
{
    public sealed record Instrument(string Name, sbyte[] Data, int Volume = 64, bool Loop = true, int RelativeNote = 0);

    /// <summary>Note numbers: 1 = C-0 ... 96 = B-7, 97 = key off, 0 = none.</summary>
    public readonly record struct Cell(int Note, int Instrument = 0, int Volume = 0, int Effect = 0, int Param = 0);

    public string Title { get; set; } = "agb";
    public int Channels { get; set; } = 6;
    public int Tempo { get; set; } = 6;    // ticks per row
    public int Bpm { get; set; } = 128;
    public int Restart { get; set; }
    public List<Instrument> Instruments { get; } = new();
    public List<Cell[,]> Patterns { get; } = new();  // [row, channel]
    public List<int> Order { get; } = new();

    public Cell[,] NewPattern(int rows = 64) => new Cell[rows, Channels];

    public static int Note(string name)
    {
        string[] names = ["C-", "C#", "D-", "D#", "E-", "F-", "F#", "G-", "G#", "A-", "A#", "B-"];
        return Array.IndexOf(names, name[..2]) + 12 * (name[2] - '0') + 1;
    }

    public byte[] Encode()
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write(Encoding.ASCII.GetBytes("Extended Module: "));
        Text(w, Title, 20);
        w.Write((byte)0x1A);
        Text(w, "AGB Studio", 20);
        w.Write((ushort)0x0104);
        w.Write(276);  // header size from this field
        w.Write((ushort)Order.Count);
        w.Write((ushort)Restart);
        w.Write((ushort)Channels);
        w.Write((ushort)Patterns.Count);
        w.Write((ushort)Instruments.Count);
        w.Write((ushort)1);  // linear frequency table
        w.Write((ushort)Tempo);
        w.Write((ushort)Bpm);
        for (var i = 0; i < 256; i++) w.Write((byte)(i < Order.Count ? Order[i] : 0));
        foreach (var p in Patterns)
        {
            var rows = p.GetLength(0);
            var data = new MemoryStream();
            for (var r = 0; r < rows; r++)
                for (var c = 0; c < Channels; c++)
                {
                    var cell = c < p.GetLength(1) ? p[r, c] : default;
                    data.WriteByte((byte)cell.Note);
                    data.WriteByte((byte)cell.Instrument);
                    data.WriteByte((byte)cell.Volume);
                    data.WriteByte((byte)cell.Effect);
                    data.WriteByte((byte)cell.Param);
                }
            w.Write(9);              // pattern header length
            w.Write((byte)0);        // packing type
            w.Write((ushort)rows);
            w.Write((ushort)data.Length);
            w.Write(data.ToArray());
        }
        foreach (var ins in Instruments)
        {
            w.Write(263);            // instrument header size
            Text(w, ins.Name, 22);
            w.Write((byte)0);
            w.Write((ushort)1);      // samples
            w.Write(40);             // sample header size
            w.Write(new byte[96]);   // keymap: all notes use sample 0
            w.Write(new byte[48]);   // volume envelope points
            w.Write(new byte[48]);   // panning envelope points
            w.Write(new byte[10]);   // envelope point counts, sustain and loop points, envelope types
            w.Write((byte)0);        // vibrato type
            w.Write((byte)0);
            w.Write((byte)0);
            w.Write((byte)0);
            w.Write((ushort)0x400);  // fadeout
            w.Write(new byte[22]);   // reserved (pads the header to 263 bytes)
            var len = ins.Data.Length;
            w.Write(len);
            w.Write(0);                    // loop start
            w.Write(ins.Loop ? len : 0);   // loop length
            w.Write((byte)ins.Volume);
            w.Write((sbyte)0);             // finetune
            w.Write((byte)(ins.Loop ? 1 : 0));
            w.Write((byte)128);            // panning
            w.Write((sbyte)ins.RelativeNote);
            w.Write((byte)0);
            Text(w, ins.Name, 22);
            sbyte prev = 0;
            foreach (var s in ins.Data)
            {
                w.Write((sbyte)(s - prev));  // delta encoded
                prev = s;
            }
        }
        return ms.ToArray();
    }

    static void Text(BinaryWriter w, string s, int len)
    {
        var bytes = Encoding.ASCII.GetBytes(s.Length > len ? s[..len] : s);
        w.Write(bytes);
        for (var i = bytes.Length; i < len; i++) w.Write((byte)0);
    }

    /// <summary>An original 6-channel tune (Lead, Bass, Drums, Pad, Arp, FX) of the given length in patterns.</summary>
    public static byte[] Tune(string title, int bpm, int patterns, int seed, string key = "C")
    {
        var xm = new XmWriter { Title = title, Bpm = bpm, Channels = 6 };
        // A 32-sample cycle plays at C-4 when the relative note is +24 semitones from the 8363 Hz base.
        xm.Instruments.Add(new("Lead", ModWriter.Waveform("pulse25"), 40, true, 24));
        xm.Instruments.Add(new("Bass", ModWriter.Waveform("triangle"), 56, true, 24));
        xm.Instruments.Add(new("Drums", ModWriter.Noise(800, seed), 40, false));
        xm.Instruments.Add(new("Pad", ModWriter.Waveform("sine"), 24, true, 24));
        xm.Instruments.Add(new("Arp", ModWriter.Waveform("square"), 24, true, 24));
        xm.Instruments.Add(new("FX", ModWriter.Waveform("saw"), 20, true, 24));
        var root = key switch { "A" => 9, "G" => 7, "F" => 5, "D" => 2, _ => 0 };
        int[] scale = [0, 2, 4, 7, 9, 12, 14, 16];
        int[] chords = [0, 9, 5, 7];
        var rng = new Random(seed);
        for (var pi = 0; pi < patterns; pi++)
        {
            var p = xm.NewPattern();
            var step = 3;
            for (var row = 0; row < 64; row++)
            {
                var chord = chords[(row / 16 + pi) % 4];
                if (row % 4 == 0)
                {
                    step = Math.Clamp(step + rng.Next(-2, 3), 0, scale.Length - 1);
                    p[row, 0] = new(48 + root + scale[step] + 1, 1);
                }
                if (row % 8 == 0) p[row, 1] = new(24 + root + chord + 1, 2);
                if (row % 8 == 4) p[row, 2] = new(60, 3);
                if (row % 16 == 0) p[row, 3] = new(36 + root + chord + 1, 4);
                if (row % 2 == 0) p[row, 4] = new(48 + root + chord + (row / 2 % 3) * 4 + 1, 5);
            }
            xm.Patterns.Add(p);
        }
        for (var i = 0; i < patterns; i++) xm.Order.Add(i);
        xm.Restart = Math.Min(patterns - 1, patterns > 4 ? 4 : 0);
        return xm.Encode();
    }
}
