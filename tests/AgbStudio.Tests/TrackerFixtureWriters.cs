using System.Text;

namespace AgbStudio.Tests;

/// <summary>
/// Test-only writers for small tracker modules (S3M, IT with compressed samples, XM with envelopes and 16-bit samples,
/// MOD variants) that cover what the maxmod soundbank port reads. They write valid files; the tests compare what
/// mmutil and the port make of them, not what the files sound like.
/// </summary>
static class TrackerFixtures
{
    static void Text(BinaryWriter w, string s, int len)
    {
        var b = Encoding.ASCII.GetBytes(s);
        for (var i = 0; i < len; i++) w.Write(i < b.Length ? b[i] : (byte)0);
    }

    static void PadTo(BinaryWriter w, int align)
    {
        while (w.BaseStream.Position % align != 0) w.Write((byte)0);
    }

    // ---------------------------------------------------------------- sample data

    /// <summary>A deterministic waveform: a few sines and noise, as signed values of <paramref name="bits"/> bits.</summary>
    public static int[] Wave(int length, int seed, int bits = 8, double amp = 0.9)
    {
        var rng = new Random(seed);
        var max = (1 << (bits - 1)) - 1;
        var r = new int[length];
        for (var i = 0; i < length; i++)
        {
            var v = 0.6 * Math.Sin(i * 0.11 * (seed % 5 + 1)) + 0.3 * Math.Sin(i * 0.031) + 0.1 * (rng.NextDouble() * 2 - 1);
            r[i] = (int)Math.Clamp(Math.Round(v * amp * max), -max - 1, max);
        }
        return r;
    }

    public static byte[] Signed8(int[] v) => v.Select(x => (byte)(sbyte)x).ToArray();
    public static byte[] Unsigned8(int[] v) => v.Select(x => (byte)(x + 128)).ToArray();
    public static byte[] Signed16(int[] v) => v.SelectMany(x => BitConverter.GetBytes((short)x)).ToArray();
    public static byte[] Unsigned16(int[] v) => v.SelectMany(x => BitConverter.GetBytes((ushort)(x + 32768))).ToArray();

    // ---------------------------------------------------------------- WAV

    /// <summary>A PCM WAV with any channel count and bit depth, optional 'smpl' loop and extra chunks.</summary>
    public static byte[] Wav(int[][] channels, int bits, int rate, (int Type, int Start, int End)? loop = null,
        bool extraChunks = false, int fmtSize = 16)
    {
        var n = channels[0].Length;
        var data = new MemoryStream();
        for (var i = 0; i < n; i++)
            foreach (var ch in channels)
            {
                if (bits == 8) data.WriteByte((byte)(ch[i] + 128));
                else data.Write(BitConverter.GetBytes((short)ch[i]));
            }
        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);
        w.Write("RIFF"u8);
        w.Write(0);
        w.Write("WAVE"u8);
        if (extraChunks)
        {
            w.Write("LIST"u8);
            w.Write(10);
            w.Write("INFOabcdef"u8);
        }
        w.Write("fmt "u8);
        w.Write(fmtSize);
        w.Write((short)1);
        w.Write((short)channels.Length);
        w.Write(rate);
        w.Write(rate * channels.Length * bits / 8);
        w.Write((short)(channels.Length * bits / 8));
        w.Write((short)bits);
        for (var i = 16; i < fmtSize; i++) w.Write((byte)0);
        if (loop is { } l)
        {
            w.Write("smpl"u8);
            w.Write(36 + 24);
            for (var i = 0; i < 7; i++) w.Write(i == 3 ? 60 : 0);
            w.Write(1);  // loops
            w.Write(0);  // sampler data
            w.Write(0);  // cue point id
            w.Write(l.Type);
            w.Write(l.Start);
            w.Write(l.End);
            w.Write(0);
            w.Write(0);
        }
        w.Write("data"u8);
        w.Write((int)data.Length);
        w.Write(data.ToArray());
        if (extraChunks)
        {
            w.Write("note"u8);
            w.Write(3);
            w.Write("xyz"u8);
        }
        w.Flush();
        var bytes = ms.ToArray();
        BitConverter.GetBytes(bytes.Length - 8).CopyTo(bytes, 4);
        return bytes;
    }

    // ---------------------------------------------------------------- MOD

    public sealed record ModSample(string Name, int[] Data, int Finetune = 0, int Volume = 64, int LoopStart = 0, int LoopLength = 0);

    /// <summary>A MOD with the given signature ("M.K.", "6CHN", "12CH", ...); cells are raw (period, sample, effect, param).</summary>
    public static byte[] Mod(string sig, int channels, IReadOnlyList<ModSample> samples, IReadOnlyList<(int Period, int Sample, int Fx, int Param)[,]> patterns,
        IReadOnlyList<int> orders, int restart = 0, int orderCountOverride = -1)
    {
        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);
        Text(w, "fixture mod", 20);
        for (var i = 0; i < 31; i++)
        {
            var s = i < samples.Count ? samples[i] : null;
            Text(w, s?.Name ?? "", 22);
            var words = s == null ? 0 : s.Data.Length / 2;
            w.Write((byte)(words >> 8)); w.Write((byte)words);
            w.Write((byte)((s?.Finetune ?? 0) & 15));
            w.Write((byte)(s?.Volume ?? 0));
            w.Write((byte)((s?.LoopStart ?? 0) / 2 >> 8)); w.Write((byte)((s?.LoopStart ?? 0) / 2));
            w.Write((byte)((s?.LoopLength ?? 2) / 2 >> 8)); w.Write((byte)((s?.LoopLength ?? 2) / 2));
        }
        w.Write((byte)(orderCountOverride >= 0 ? orderCountOverride : orders.Count));
        w.Write((byte)restart);
        for (var i = 0; i < 128; i++) w.Write((byte)(i < orders.Count ? orders[i] : 0));
        w.Write(Encoding.ASCII.GetBytes(sig));
        foreach (var p in patterns)
            for (var r = 0; r < 64; r++)
                for (var c = 0; c < channels; c++)
                {
                    var (period, smp, fx, param) = p[r, c];
                    w.Write((byte)((smp & 0xF0) | (period >> 8 & 0xF)));
                    w.Write((byte)period);
                    w.Write((byte)(((smp & 0xF) << 4) | (fx & 0xF)));
                    w.Write((byte)param);
                }
        foreach (var s in samples)
            foreach (var v in s.Data) w.Write((sbyte)v);
        w.Flush();
        return ms.ToArray();
    }

    // ---------------------------------------------------------------- S3M

    public sealed record S3MSample(string Name, string FileName, int[] Data, bool Bit16 = false, bool Loop = false,
        int LoopStart = 0, int LoopEnd = 0, int Volume = 64, int C2Spd = 8363, int Type = 1);

    public readonly record struct S3MCell(int Note = -1, int Inst = 0, int Vol = -1, int Fx = 0, int Param = 0);

    /// <summary>An S3M; <paramref name="ffi"/> 1 stores signed samples, 2 unsigned. A null pattern gets parapointer 0.</summary>
    public static byte[] S3M(IReadOnlyList<S3MSample> samples, IReadOnlyList<S3MCell[,]?> patterns, IReadOnlyList<int> orders,
        int ffi = 2, bool stereo = true, byte[]? channels = null, byte[]? defaultPan = null, int globalVolume = 64, int speed = 6, int tempo = 125)
    {
        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);
        Text(w, "fixture s3m", 28);
        w.Write((byte)0x1A);
        w.Write((byte)16);
        w.Write((short)0);
        w.Write((short)orders.Count);
        w.Write((short)samples.Count);
        w.Write((short)patterns.Count);
        w.Write((short)0);       // flags
        w.Write((short)0x1320);  // cwt
        w.Write((short)ffi);
        w.Write("SCRM"u8);
        w.Write((byte)globalVolume);
        w.Write((byte)speed);
        w.Write((byte)tempo);
        w.Write((byte)(stereo ? 0xB0 : 0x30));
        w.Write((byte)0);
        w.Write((byte)(defaultPan != null ? 252 : 0));
        for (var i = 0; i < 10; i++) w.Write((byte)0);
        for (var i = 0; i < 32; i++)
            w.Write(channels != null ? channels[i] : (byte)(i < 8 ? (i % 2 == 0 ? i / 2 : 8 + i / 2) : 255));
        foreach (var o in orders) w.Write((byte)o);
        var paraAt = ms.Position;
        for (var i = 0; i < samples.Count + patterns.Count; i++) w.Write((short)0);
        if (defaultPan != null) w.Write(defaultPan);
        var paras = new List<int>();
        var dataFix = new List<(long At, int Index)>();
        for (var i = 0; i < samples.Count; i++)
        {
            PadTo(w, 16);
            paras.Add((int)(ms.Position / 16));
            var s = samples[i];
            w.Write((byte)s.Type);
            Text(w, s.FileName, 12);
            dataFix.Add((ms.Position, i));
            w.Write((byte)0); w.Write((short)0);  // memseg, filled in below
            w.Write(s.Data.Length);
            w.Write(s.LoopStart);
            w.Write(s.LoopEnd);
            w.Write((byte)s.Volume);
            w.Write((byte)0);
            w.Write((byte)0);
            w.Write((byte)((s.Loop ? 1 : 0) | (s.Bit16 ? 4 : 0)));
            w.Write(s.C2Spd);
            for (var k = 0; k < 12; k++) w.Write((byte)0);
            Text(w, s.Name, 28);
            w.Write("SCRS"u8);
        }
        foreach (var p in patterns)
        {
            PadTo(w, 16);
            if (p == null) { paras.Add(0); continue; }
            paras.Add((int)(ms.Position / 16));
            var body = new MemoryStream();
            for (var r = 0; r < 64; r++)
            {
                for (var c = 0; c < p.GetLength(1); c++)
                {
                    var cell = p[r, c];
                    if (cell == default) continue;  // an unset cell
                    var what = c;
                    if (cell.Note >= 0) what |= 32;
                    if (cell.Vol >= 0) what |= 64;
                    if (cell.Fx != 0) what |= 128;
                    if (what == c) continue;
                    body.WriteByte((byte)what);
                    if (cell.Note >= 0) { body.WriteByte((byte)cell.Note); body.WriteByte((byte)cell.Inst); }
                    if (cell.Vol >= 0) body.WriteByte((byte)cell.Vol);
                    if (cell.Fx != 0) { body.WriteByte((byte)cell.Fx); body.WriteByte((byte)cell.Param); }
                }
                body.WriteByte(0);
            }
            w.Write((short)(body.Length + 2));
            w.Write(body.ToArray());
        }
        var dataAt = new long[samples.Count];
        for (var i = 0; i < samples.Count; i++)
        {
            PadTo(w, 16);
            dataAt[i] = ms.Position;
            var s = samples[i];
            var bytes = s.Bit16 ? (ffi == 1 ? Signed16(s.Data) : Unsigned16(s.Data)) : (ffi == 1 ? Signed8(s.Data) : Unsigned8(s.Data));
            w.Write(bytes);
        }
        w.Flush();
        var b = ms.ToArray();
        for (var i = 0; i < paras.Count; i++) BitConverter.GetBytes((short)paras[i]).CopyTo(b, paraAt + 2 * i);
        foreach (var (at, i) in dataFix)
        {
            var seg = dataAt[i] / 16;
            b[at] = (byte)(seg >> 16);
            BitConverter.GetBytes((ushort)seg).CopyTo(b, at + 1);
        }
        return b;
    }

    // ---------------------------------------------------------------- XM

    public sealed record XmSample(string Name, int[] Data, bool Bit16 = false, int LoopType = 0, int LoopStart = 0, int LoopLength = 0,
        int Volume = 64, int Finetune = 0, int Panning = 128, int RelNote = 0);

    public sealed record XmEnvelope(int[] Points, int Sustain = -1, int LoopStart = -1, int LoopEnd = -1, bool On = true);

    public sealed record XmInstrument(string Name, IReadOnlyList<XmSample> Samples, byte[]? KeyMap = null, XmEnvelope? Volume = null,
        XmEnvelope? Pan = null, int VibType = 0, int VibSweep = 0, int VibDepth = 0, int VibRate = 0, int Fadeout = 0);

    public readonly record struct XmCell(int Note = 0, int Inst = 0, int Vol = 0, int Fx = 0, int Param = 0);

    /// <summary>An XM; patterns are [row, channel], cells with Pack false-ish data are written unpacked every 3rd row.</summary>
    public static byte[] Xm(int channels, IReadOnlyList<XmInstrument> instruments, IReadOnlyList<XmCell[,]> patterns, IReadOnlyList<int> orders,
        int restart = 0, bool linear = true, int speed = 6, int bpm = 125)
    {
        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);
        w.Write(Encoding.ASCII.GetBytes("Extended Module: "));
        Text(w, "fixture xm", 20);
        w.Write((byte)0x1A);
        Text(w, "fixture", 20);
        w.Write((short)0x0104);
        w.Write(276);
        w.Write((short)orders.Count);
        w.Write((short)restart);
        w.Write((short)channels);
        w.Write((short)patterns.Count);
        w.Write((short)instruments.Count);
        w.Write((short)(linear ? 1 : 0));
        w.Write((short)speed);
        w.Write((short)bpm);
        for (var i = 0; i < 256; i++) w.Write((byte)(i < orders.Count ? orders[i] : 0));
        foreach (var p in patterns)
        {
            var rows = p.GetLength(0);
            var body = new MemoryStream();
            var any = false;
            for (var r = 0; r < rows; r++)
                for (var c = 0; c < channels; c++)
                {
                    var cell = c < p.GetLength(1) ? p[r, c] : default;
                    if (cell != default) any = true;
                    if (r % 3 == 2 && cell.Note < 128)
                    {
                        body.WriteByte((byte)cell.Note); body.WriteByte((byte)cell.Inst); body.WriteByte((byte)cell.Vol);
                        body.WriteByte((byte)cell.Fx); body.WriteByte((byte)cell.Param);
                        continue;
                    }
                    var b = 0x80 | (cell.Note != 0 ? 1 : 0) | (cell.Inst != 0 ? 2 : 0) | (cell.Vol != 0 ? 4 : 0) | (cell.Fx != 0 ? 8 : 0) | (cell.Param != 0 ? 16 : 0);
                    body.WriteByte((byte)b);
                    if (cell.Note != 0) body.WriteByte((byte)cell.Note);
                    if (cell.Inst != 0) body.WriteByte((byte)cell.Inst);
                    if (cell.Vol != 0) body.WriteByte((byte)cell.Vol);
                    if (cell.Fx != 0) body.WriteByte((byte)cell.Fx);
                    if (cell.Param != 0) body.WriteByte((byte)cell.Param);
                }
            w.Write(9);
            w.Write((byte)0);
            w.Write((short)rows);
            w.Write((short)(any ? body.Length : 0));
            if (any) w.Write(body.ToArray());
        }
        foreach (var ins in instruments)
        {
            var n = ins.Samples.Count;
            w.Write(n > 0 ? 263 : 29);
            Text(w, ins.Name, 22);
            w.Write((byte)0);
            w.Write((short)n);
            if (n == 0) continue;
            w.Write(40);
            for (var k = 0; k < 96; k++) w.Write(ins.KeyMap?[k] ?? (byte)0);
            void Env(XmEnvelope? e)
            {
                for (var k = 0; k < 12; k++)
                {
                    w.Write((short)(e != null && k * 2 < e.Points.Length ? e.Points[k * 2] : 0));
                    w.Write((short)(e != null && k * 2 < e.Points.Length ? e.Points[k * 2 + 1] : 0));
                }
            }
            Env(ins.Volume);
            Env(ins.Pan);
            w.Write((byte)((ins.Volume?.Points.Length ?? 0) / 2));
            w.Write((byte)((ins.Pan?.Points.Length ?? 0) / 2));
            foreach (var e in new[] { ins.Volume, ins.Pan })
            {
                w.Write((byte)Math.Max(0, e?.Sustain ?? 0));
                w.Write((byte)Math.Max(0, e?.LoopStart ?? 0));
                w.Write((byte)Math.Max(0, e?.LoopEnd ?? 0));
            }
            // the fields above are vol sustain, vol loop start, vol loop end, pan sustain, ... as XM orders them
            foreach (var e in new[] { ins.Volume, ins.Pan })
                w.Write((byte)(e == null ? 0 : (e.On ? 1 : 0) | (e.Sustain >= 0 ? 2 : 0) | (e.LoopStart >= 0 ? 4 : 0)));
            w.Write((byte)ins.VibType);
            w.Write((byte)ins.VibSweep);
            w.Write((byte)ins.VibDepth);
            w.Write((byte)ins.VibRate);
            w.Write((short)ins.Fadeout);
            for (var k = 0; k < 22; k++) w.Write((byte)0);
            foreach (var s in ins.Samples)
            {
                var size = s.Bit16 ? 2 : 1;
                w.Write(s.Data.Length * size);
                w.Write(s.LoopStart * size);
                w.Write(s.LoopLength * size);
                w.Write((byte)s.Volume);
                w.Write((sbyte)s.Finetune);
                w.Write((byte)(s.LoopType | (s.Bit16 ? 16 : 0)));
                w.Write((byte)s.Panning);
                w.Write((sbyte)s.RelNote);
                w.Write((byte)0);
                Text(w, s.Name, 22);
            }
            foreach (var s in ins.Samples)
            {
                var prev = 0;
                foreach (var v in s.Data)
                {
                    if (s.Bit16) w.Write((short)(v - prev));
                    else w.Write((sbyte)(v - prev));
                    prev = v;
                }
            }
        }
        w.Flush();
        return ms.ToArray();
    }

    // ---------------------------------------------------------------- IT

    public sealed record ItEnvelope(int Flags, (int Y, int X)[] Nodes, int LoopStart = 0, int LoopEnd = 0, int SusStart = 0, int SusEnd = 0);

    public sealed record ItInstrument(string Name, ushort[] NoteMap, int Nna = 0, int Dct = 0, int Dca = 0, int Fadeout = 256, int GlobalVolume = 128,
        int DefaultPan = 32 | 128, int RandomVolume = 0, ItEnvelope? Volume = null, ItEnvelope? Pan = null, ItEnvelope? Pitch = null);

    public sealed record ItSample(string Name, string FileName, int[] Data, bool Bit16 = false, bool Signed = true, bool Loop = false,
        bool PingPong = false, int LoopStart = 0, int LoopEnd = 0, int C5 = 8363, int GlobalVolume = 64, int Volume = 64, int DefaultPan = 32,
        bool Compressed = false, int VibSpeed = 0, int VibDepth = 0, int VibRate = 0, int VibType = 0);

    /// <summary>One IT cell; Note 255 = note off, 254 = note cut; -1 = none.</summary>
    public readonly record struct ItCell(int Note = -1, int Inst = 0, int Vol = -1, int Fx = 0, int Param = 0);

    /// <summary>An IT; <paramref name="cmwt"/> 0x215 makes compressed samples IT 2.15 (double delta). A null pattern gets parapointer 0.</summary>
    public static byte[] It(IReadOnlyList<ItInstrument>? instruments, IReadOnlyList<ItSample> samples, IReadOnlyList<(int Rows, ItCell[,] Cells)?> patterns,
        IReadOnlyList<int> orders, int cmwt = 0x214, int flags = 1 | 8, byte[]? chanPan = null, byte[]? chanVol = null, int globalVolume = 128, int speed = 6, int tempo = 125)
    {
        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);
        var useInst = instruments != null;
        w.Write("IMPM"u8);
        Text(w, "fixture it", 26);
        w.Write((short)0x1004);
        w.Write((short)orders.Count);
        w.Write((short)(instruments?.Count ?? 0));
        w.Write((short)samples.Count);
        w.Write((short)patterns.Count);
        w.Write((short)0x0214);
        w.Write((short)cmwt);
        w.Write((short)(flags | (useInst ? 4 : 0)));
        w.Write((short)0);
        w.Write((byte)globalVolume);
        w.Write((byte)48);
        w.Write((byte)speed);
        w.Write((byte)tempo);
        w.Write((byte)128);
        w.Write((byte)0);
        w.Write((short)0);
        w.Write(0);
        w.Write(0);
        for (var i = 0; i < 64; i++) w.Write(chanPan != null ? chanPan[i] : (byte)(i % 2 == 0 ? 16 : 48));
        for (var i = 0; i < 64; i++) w.Write(chanVol != null ? chanVol[i] : (byte)64);
        foreach (var o in orders) w.Write((byte)o);
        var paraAt = ms.Position;
        var nInst = instruments?.Count ?? 0;
        for (var i = 0; i < nInst + samples.Count + patterns.Count; i++) w.Write(0);
        var paras = new List<int>();

        foreach (var ins in instruments ?? [])
        {
            paras.Add((int)ms.Position);
            w.Write("IMPI"u8);
            Text(w, "inst.iti", 12);
            w.Write((byte)0);
            w.Write((byte)ins.Nna);
            w.Write((byte)ins.Dct);
            w.Write((byte)ins.Dca);
            w.Write((short)ins.Fadeout);
            w.Write((byte)0);
            w.Write((byte)60);
            w.Write((byte)ins.GlobalVolume);
            w.Write((byte)ins.DefaultPan);
            w.Write((byte)ins.RandomVolume);
            w.Write((byte)0);
            w.Write((short)0x0214);
            w.Write((byte)samples.Count);
            w.Write((byte)0);
            Text(w, ins.Name, 26);
            w.Write((byte)0); w.Write((byte)0); w.Write((byte)0); w.Write((byte)0); w.Write((short)0);
            foreach (var m in ins.NoteMap) w.Write(m);
            foreach (var e in new[] { ins.Volume, ins.Pan, ins.Pitch })
            {
                w.Write((byte)(e?.Flags ?? 0));
                w.Write((byte)(e?.Nodes.Length ?? 0));
                w.Write((byte)(e?.LoopStart ?? 0));
                w.Write((byte)(e?.LoopEnd ?? 0));
                w.Write((byte)(e?.SusStart ?? 0));
                w.Write((byte)(e?.SusEnd ?? 0));
                for (var k = 0; k < 25; k++)
                {
                    var (y, x) = e != null && k < e.Nodes.Length ? e.Nodes[k] : (0, 0);
                    w.Write((sbyte)y);
                    w.Write((short)x);
                }
                w.Write((byte)0);
            }
            w.Write(0);  // to 554 bytes
        }
        var sampleHeaderAt = new List<long>();
        foreach (var s in samples)
        {
            paras.Add((int)ms.Position);
            w.Write("IMPS"u8);
            Text(w, s.FileName, 12);
            w.Write((byte)0);
            w.Write((byte)s.GlobalVolume);
            w.Write((byte)((s.Data.Length > 0 ? 1 : 0) | (s.Bit16 ? 2 : 0) | (s.Compressed ? 8 : 0) | (s.Loop ? 16 : 0) | (s.PingPong ? 64 : 0)));
            w.Write((byte)s.Volume);
            Text(w, s.Name, 26);
            w.Write((byte)(s.Signed ? 1 : 0));
            w.Write((byte)s.DefaultPan);
            w.Write(s.Data.Length);
            w.Write(s.LoopStart);
            w.Write(s.LoopEnd);
            w.Write(s.C5);
            w.Write(0);
            w.Write(0);
            sampleHeaderAt.Add(ms.Position);
            w.Write(0);  // sample pointer, filled in below
            w.Write((byte)s.VibSpeed);
            w.Write((byte)s.VibDepth);
            w.Write((byte)s.VibRate);
            w.Write((byte)s.VibType);
        }
        foreach (var p in patterns)
        {
            if (p == null) { paras.Add(0); continue; }
            paras.Add((int)ms.Position);
            var (rows, cells) = p.Value;
            var body = new MemoryStream();
            var chans = cells.GetLength(1);
            var lastMask = new int[64];
            var last = new ItCell[64];
            for (var c = 0; c < 64; c++) { lastMask[c] = -1; last[c] = new ItCell(-2, -2, -2, -2, -2); }
            for (var r = 0; r < rows; r++)
            {
                for (var c = 0; c < chans; c++)
                {
                    var cell = r < cells.GetLength(0) ? cells[r, c] : default;
                    if (cell.Note < 0 && cell.Inst == 0 && cell.Vol < 0 && cell.Fx == 0 && cell.Param == 0) continue;
                    if (cell == default) continue;
                    var mask = 0;
                    // reuse the last value through bits 16..128 when it is the same
                    if (cell.Note >= 0) mask |= cell.Note == last[c].Note ? 16 : 1;
                    if (cell.Inst != 0) mask |= cell.Inst == last[c].Inst ? 32 : 2;
                    if (cell.Vol >= 0) mask |= cell.Vol == last[c].Vol ? 64 : 4;
                    if (cell.Fx != 0 || cell.Param != 0) mask |= cell.Fx == last[c].Fx && cell.Param == last[c].Param ? 128 : 8;
                    var chanvar = (c + 1) | (mask != lastMask[c] ? 128 : 0);
                    body.WriteByte((byte)chanvar);
                    if (mask != lastMask[c]) body.WriteByte((byte)mask);
                    lastMask[c] = mask;
                    if ((mask & 1) != 0) { body.WriteByte((byte)cell.Note); last[c] = last[c] with { Note = cell.Note }; }
                    if ((mask & 2) != 0) { body.WriteByte((byte)cell.Inst); last[c] = last[c] with { Inst = cell.Inst }; }
                    if ((mask & 4) != 0) { body.WriteByte((byte)cell.Vol); last[c] = last[c] with { Vol = cell.Vol }; }
                    if ((mask & 8) != 0) { body.WriteByte((byte)cell.Fx); body.WriteByte((byte)cell.Param); last[c] = last[c] with { Fx = cell.Fx, Param = cell.Param }; }
                }
                body.WriteByte(0);
            }
            w.Write((short)body.Length);
            w.Write((short)rows);
            w.Write(0);
            w.Write(body.ToArray());
        }
        var dataAt = new List<long>();
        foreach (var s in samples)
        {
            dataAt.Add(ms.Position);
            if (s.Compressed) w.Write(ItCompress(s.Data, s.Bit16, cmwt == 0x215));
            else if (s.Bit16) w.Write(s.Signed ? Signed16(s.Data) : Unsigned16(s.Data));
            else w.Write(s.Signed ? Signed8(s.Data) : Unsigned8(s.Data));
        }
        w.Flush();
        var b = ms.ToArray();
        for (var i = 0; i < paras.Count; i++) BitConverter.GetBytes(paras[i]).CopyTo(b, paraAt + 4 * i);
        for (var i = 0; i < samples.Count; i++) BitConverter.GetBytes((int)dataAt[i]).CopyTo(b, sampleHeaderAt[i]);
        return b;
    }

    /// <summary>
    /// IT 2.14 / 2.15 sample compression. Each value uses the narrowest bit width that holds it, so the stream changes
    /// width often and uses all three ways the format has to change it (1-6 bits, 7..n bits, n+1 bits).
    /// </summary>
    public static byte[] ItCompress(int[] samples, bool bit16, bool it215)
    {
        var nbits = bit16 ? 16 : 8;
        var dsize = bit16 ? 4 : 3;
        var blockSamples = bit16 ? 0x4000 : 0x8000;
        var outp = new MemoryStream();
        int Wrap(int v) => bit16 ? (short)v : (sbyte)v;
        bool Fits(int v, int width)
        {
            if (width == nbits + 1) return true;
            if (width < 7) return v > -(1 << (width - 1)) && v <= (1 << (width - 1)) - 1;
            var esc = bit16 ? 8 : 4;
            return v >= -(1 << (width - 1)) + esc && v <= (1 << (width - 1)) - 1 - esc;
        }
        for (var start = 0; start < samples.Length; start += blockSamples)
        {
            var bits = new List<bool>();
            void Put(int value, int count)
            {
                for (var i = 0; i < count; i++) bits.Add(((value >> i) & 1) != 0);
            }
            var width = nbits + 1;
            int prevS = 0, prevD = 0;
            for (var i = start; i < Math.Min(samples.Length, start + blockSamples); i++)
            {
                var d = Wrap(samples[i] - prevS);
                var v = it215 ? Wrap(d - prevD) : d;
                prevS = samples[i];
                prevD = d;
                var need = 1;
                while (!Fits(v, need)) need++;
                if (need != width)
                {
                    if (width < 7)
                    {
                        Put(1 << (width - 1), width);
                        Put((need < width ? need : need - 1) - 1, dsize);
                    }
                    else if (width <= nbits)
                    {
                        var border = (1 << (width - 1)) - 1 - nbits / 2;
                        Put(border + (need < width ? need : need - 1), width);
                    }
                    else
                    {
                        Put((1 << nbits) | (need - 1), width);
                    }
                    width = need;
                }
                Put(width == nbits + 1 ? v & ((1 << nbits) - 1) : v, width);
            }
            var bytes = new byte[(bits.Count + 7) / 8];
            for (var i = 0; i < bits.Count; i++)
                if (bits[i]) bytes[i >> 3] |= (byte)(1 << (i & 7));
            outp.Write(BitConverter.GetBytes((ushort)bytes.Length));
            outp.Write(bytes);
        }
        return outp.ToArray();
    }
}
