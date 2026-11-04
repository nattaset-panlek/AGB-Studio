using System.Buffers.Binary;
using System.Text;

namespace AgbStudio.Editor.Windows.Audio;

/// <summary>
/// Reading S3M (Scream Tracker 3) and IT (Impulse Tracker) for the Audio page. The file goes into the ROM unchanged
/// (mmutil reads both); here its notes, samples and common effects become the XM model, so the page can show and preview
/// it. The preview is close, not exact, and the tracker view does not write these formats.
/// </summary>
public sealed partial class TrackerModule
{
    string? _readOnlyFormat;

    /// <summary>False for S3M and IT: the file is read, never rewritten.</summary>
    public bool CanEdit => _readOnlyFormat == null;

    static bool IsS3m(byte[] d) => d.Length > 96 && Encoding.ASCII.GetString(d, 44, 4) == "SCRM";
    static bool IsIt(byte[] d) => d.Length > 192 && Encoding.ASCII.GetString(d, 0, 4) == "IMPM";

    /// <summary>S3M / IT command letter (A = 1) as the XM effect the player understands; 0xFF = not previewed.</summary>
    static (byte Effect, byte Param) MapEffect(int command, int p) => command switch
    {
        1 => (0x0F, (byte)Math.Clamp(p, 1, 31)),   // A speed
        2 => (0x0B, (byte)p),                      // B jump to order
        3 => (0x0D, (byte)p),                      // C pattern break
        4 => (0x0A, (byte)p),                      // D volume slide
        5 when p < 0xE0 => (0x02, (byte)p),        // E slide down
        6 when p < 0xE0 => (0x01, (byte)p),        // F slide up
        7 => (0x03, (byte)p),                      // G tone portamento
        8 => (0x04, (byte)p),                      // H vibrato
        10 => (0x00, (byte)p),                     // J arpeggio
        11 => (0x06, (byte)p),                     // K vibrato + volume slide
        12 => (0x05, (byte)p),                     // L portamento + volume slide
        15 => (0x09, (byte)p),                     // O sample offset
        18 => (0x07, (byte)p),                     // R tremolo
        20 when p >= 32 => (0x0F, (byte)p),        // T tempo
        22 => (0x10, (byte)Math.Min(p, 64)),       // V global volume
        _ => (0xFF, 0),
    };

    static TrackerCell Cell(int note, int instrument, int volume, int command, int param)
    {
        var (effect, p) = MapEffect(command, param);
        if (effect == 0xFF) (effect, p) = ((byte)0, (byte)0);
        var vol = volume is >= 0 and <= 64 ? 0x10 + volume : 0;
        return new TrackerCell((byte)note, (byte)instrument, (byte)vol, effect, p);
    }

    /// <summary>The XM relative note and fine tune that play a sample at <paramref name="rate"/> on its reference note.</summary>
    static (int Note, int Fine) Tuning(int rate, int octaveOffset)
    {
        var semis = 12 * Math.Log2(Math.Max(1, rate) / 8363.0) + octaveOffset * 12;
        var note = (int)Math.Round(semis);
        return (note, (int)Math.Clamp(Math.Round((semis - note) * 128), -128, 127));
    }

    void AddOrder(int value)
    {
        if (value < 254) Order.Add(value);
    }

    // ---- S3M ------------------------------------------------------------------------------------------

    void ParseS3m()
    {
        var d = _raw;
        int U16(int o) => o + 2 <= d.Length ? BinaryPrimitives.ReadUInt16LittleEndian(d.AsSpan(o)) : 0;
        _readOnlyFormat = "S3M";
        IsXm = true;
        LinearFrequency = true;
        Title = Encoding.ASCII.GetString(d, 0, 28).TrimEnd('\0', ' ');
        int orders = U16(32), samples = U16(34), patterns = U16(36);
        InitialSpeed = d[49] > 0 ? d[49] : 6;
        InitialBpm = d[50] >= 32 ? d[50] : 125;
        var channelOf = new int[32];
        for (var i = 0; i < 32; i++) channelOf[i] = d[64 + i] < 16 ? Channels++ : -1;
        if (Channels == 0) throw new InvalidDataException("It has no channels turned on. Save it again from your tracker.");
        for (var i = 0; i < orders; i++)
            if (96 + i < d.Length && d[96 + i] != 255) AddOrder(d[96 + i]);
        var pointers = 96 + orders;
        var unsigned = U16(42) != 1;  // the header's format word: 1 = signed samples, 2 = unsigned
        for (var i = 0; i < samples; i++)
        {
            var h = U16(pointers + i * 2) * 16;
            if (h == 0 || h + 80 > d.Length || d[h] != 1)
            {
                var empty = new TrackerInstrument { Name = "" };
                empty.Samples.Add(new TrackerSample());
                Instruments.Add(empty);
                continue;
            }
            var name = Encoding.ASCII.GetString(d, h + 48, 28).TrimEnd('\0', ' ');
            var at = (d[h + 13] << 16 | U16(h + 14)) * 16;
            int length = BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(h + 16)), loopStart = BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(h + 20)), loopEnd = BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(h + 24));
            var sixteen = (d[h + 31] & 4) != 0;
            var (note, fine) = Tuning(BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(h + 32)), 0);
            var ins = new TrackerInstrument { Name = name };
            var looped = (d[h + 31] & 1) != 0;
            ins.Samples.Add(new TrackerSample
            {
                Name = name, Data = Pcm(d, at, length, sixteen, unsigned), Volume = Math.Min((int)d[h + 28], 64), RelativeNote = note, FineTune = fine,
                LoopType = looped ? 1 : 0, LoopStart = looped ? loopStart : 0, LoopLength = looped ? Math.Max(0, loopEnd - loopStart) : 0,
            });
            Instruments.Add(ins);
        }
        for (var i = 0; i < patterns; i++)
        {
            var pattern = new TrackerPattern(64, Channels);
            Patterns.Add(pattern);
            var at = U16(pointers + samples * 2 + i * 2) * 16;
            if (at == 0 || at + 2 > d.Length) continue;
            var p = at + 2;
            var end = Math.Min(d.Length, p + U16(at));
            for (var row = 0; row < 64 && p < end;)
            {
                var what = d[p++];
                if (what == 0) { row++; continue; }
                int note = 0, instrument = 0, volume = -1, command = 0, param = 0;
                if ((what & 32) != 0 && p + 2 <= end) { note = d[p++]; instrument = d[p++]; }
                if ((what & 64) != 0 && p < end) volume = d[p++];
                if ((what & 128) != 0 && p + 2 <= end) { command = d[p++]; param = d[p++]; }
                var ch = channelOf[what & 31];
                if (ch < 0) continue;
                var xm = note switch
                {
                    0 or 255 => 0,
                    254 => TrackerCell.KeyOff,
                    _ => Math.Clamp((note >> 4) * 12 + (note & 15) + 1, 1, 96),
                };
                pattern.Cells[row, ch] = Cell(xm, instrument, volume, command, param);
            }
        }
    }

    static float[] Pcm(byte[] d, int at, int length, bool sixteen, bool unsigned)
    {
        length = Math.Max(0, Math.Min(length, (d.Length - at) / (sixteen ? 2 : 1)));
        var r = new float[length];
        for (var i = 0; i < length; i++)
        {
            if (sixteen)
            {
                var raw = BinaryPrimitives.ReadUInt16LittleEndian(d.AsSpan(at + i * 2));
                r[i] = (unsigned ? raw - 32768 : (short)raw) / 32768f;
            }
            else
            {
                r[i] = (unsigned ? d[at + i] - 128 : (sbyte)d[at + i]) / 128f;
            }
        }
        return r;
    }

    // ---- IT ---------------------------------------------------------------------------------------------

    void ParseIt()
    {
        var d = _raw;
        int U16(int o) => o + 2 <= d.Length ? BinaryPrimitives.ReadUInt16LittleEndian(d.AsSpan(o)) : 0;
        int I32(int o) => o + 4 <= d.Length ? BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(o)) : 0;
        _readOnlyFormat = "IT";
        IsXm = true;
        LinearFrequency = true;
        Title = Encoding.ASCII.GetString(d, 4, 26).TrimEnd('\0', ' ');
        int orders = U16(0x20), instruments = U16(0x22), samples = U16(0x24), patterns = U16(0x26), flags = U16(0x2C);
        InitialSpeed = d[0x32] > 0 ? d[0x32] : 6;
        InitialBpm = d[0x33] >= 32 ? d[0x33] : 125;
        for (var i = 0; i < orders && 0xC0 + i < d.Length; i++)
            if (d[0xC0 + i] == 255) break; else AddOrder(d[0xC0 + i]);
        var pointers = 0xC0 + orders;
        var smp = new List<TrackerSample>();
        for (var i = 0; i < samples; i++) smp.Add(ReadItSample(I32(pointers + instruments * 4 + i * 4)));
        if ((flags & 4) != 0)
            for (var i = 0; i < instruments; i++) Instruments.Add(ReadItInstrument(I32(pointers + i * 4), smp));
        else
            foreach (var s in smp)
            {
                var ins = new TrackerInstrument { Name = s.Name };
                ins.Samples.Add(s);
                Instruments.Add(ins);
            }

        // First pass: the channels the song really uses, as an IT file always lists 64.
        var patternAt = new int[patterns];
        for (var i = 0; i < patterns; i++) patternAt[i] = I32(pointers + (instruments + samples) * 4 + i * 4);
        var used = new SortedSet<int>();
        for (var i = 0; i < patterns; i++) ScanItPattern(patternAt[i], (_, ch, _, _, _, _, _) => used.Add(ch));
        if (used.Count == 0) used.Add(0);
        var channelOf = used.Select((ch, index) => (ch, index)).ToDictionary(x => x.ch, x => x.index);
        Channels = used.Count;
        for (var i = 0; i < patterns; i++)
        {
            var rows = patternAt[i] > 0 && patternAt[i] + 4 <= d.Length ? U16(patternAt[i] + 2) : 64;
            var pattern = new TrackerPattern(Math.Max(1, rows), Channels);
            Patterns.Add(pattern);
            ScanItPattern(patternAt[i], (row, ch, note, instrument, volume, command, param) =>
            {
                if (row >= pattern.Rows) return;
                var xm = note switch
                {
                    < 0 => 0,
                    >= 254 or 246 => TrackerCell.KeyOff,
                    > 119 => 0,
                    _ => Math.Clamp(note + 1, 1, 96),
                };
                pattern.Cells[row, channelOf[ch]] = Cell(xm, instrument, volume, command, param);
            });
        }
    }

    delegate void ItCell(int row, int channel, int note, int instrument, int volume, int command, int param);

    void ScanItPattern(int at, ItCell visit)
    {
        var d = _raw;
        if (at <= 0 || at + 8 > d.Length) return;
        var length = BinaryPrimitives.ReadUInt16LittleEndian(d.AsSpan(at));
        var rows = BinaryPrimitives.ReadUInt16LittleEndian(d.AsSpan(at + 2));
        int p = at + 8, end = Math.Min(d.Length, p + length);
        var mask = new byte[64];
        var note = new int[64];
        var instrument = new int[64];
        var volume = new int[64];
        var command = new int[64];
        var param = new int[64];
        for (var row = 0; row < rows && p < end;)
        {
            var what = d[p++];
            if (what == 0) { row++; continue; }
            var ch = (what - 1) & 63;
            if ((what & 128) != 0 && p < end) mask[ch] = d[p++];
            int n = -1, ins = 0, vol = -1, cmd = 0, par = 0;
            var m = mask[ch];
            if ((m & 1) != 0 && p < end) n = note[ch] = d[p++];
            if ((m & 2) != 0 && p < end) ins = instrument[ch] = d[p++];
            if ((m & 4) != 0 && p < end) vol = volume[ch] = d[p++];
            if ((m & 8) != 0 && p + 2 <= end) { cmd = command[ch] = d[p++]; par = param[ch] = d[p++]; }
            if ((m & 16) != 0) n = note[ch];
            if ((m & 32) != 0) ins = instrument[ch];
            if ((m & 64) != 0) vol = volume[ch];
            if ((m & 128) != 0) { cmd = command[ch]; par = param[ch]; }
            visit(row, ch, n, ins, vol, cmd, par);
        }
    }

    TrackerInstrument ReadItInstrument(int at, List<TrackerSample> samples)
    {
        var d = _raw;
        if (at <= 0 || at + 0x130 > d.Length) return new TrackerInstrument();
        var picked = new List<TrackerSample>();
        var map = new byte[96];
        for (var n = 0; n < 96; n++)
        {
            var s = d[at + 0x40 + n * 2 + 1];  // the table's (note, sample) pair; note transposes are not previewed
            if (s == 0 || s > samples.Count) continue;
            var index = picked.IndexOf(samples[s - 1]);
            if (index < 0) { index = picked.Count; picked.Add(samples[s - 1]); }
            map[n] = (byte)index;
        }
        var ins = new TrackerInstrument { Name = Encoding.ASCII.GetString(d, at + 0x20, 26).TrimEnd('\0', ' '), NoteMap = map };
        ins.Samples.AddRange(picked);
        return ins;
    }

    TrackerSample ReadItSample(int h)
    {
        var d = _raw;
        if (h <= 0 || h + 0x50 > d.Length) return new TrackerSample();
        int I32(int o) => BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(o));
        var flags = d[h + 0x12];
        if ((flags & 1) == 0) return new TrackerSample { Name = Encoding.ASCII.GetString(d, h + 0x14, 26).TrimEnd('\0', ' ') };
        var sixteen = (flags & 2) != 0;
        var length = I32(h + 0x30);
        var at = I32(h + 0x48);
        var signed = (d[h + 0x2E] & 1) != 0;
        float[] data;
        if ((flags & 8) != 0) data = DecompressIt214(d, at, length, sixteen, (d[h + 0x2E] & 4) != 0);
        else data = Pcm(d, at, length, sixteen, !signed);
        var looped = (flags & 16) != 0;
        var (note, fine) = Tuning(I32(h + 0x3C), -1);  // the rate belongs to C-5; XM's base rate to C-4
        int loopStart = I32(h + 0x34), loopEnd = I32(h + 0x38);
        return new TrackerSample
        {
            Name = Encoding.ASCII.GetString(d, h + 0x14, 26).TrimEnd('\0', ' '), Data = data, Volume = Math.Min((int)d[h + 0x13], 64), RelativeNote = note, FineTune = fine,
            LoopType = looped ? (flags & 64) != 0 ? 2 : 1 : 0, LoopStart = looped ? loopStart : 0, LoopLength = looped ? Math.Max(0, loopEnd - loopStart) : 0,
        };
    }

    /// <summary>IT 2.14 / 2.15 sample compression: blocks of bit-packed deltas whose bit width changes on the way.</summary>
    static float[] DecompressIt214(byte[] d, int at, int length, bool sixteen, bool it215)
    {
        var result = new float[Math.Max(0, length)];
        var written = 0;
        var blockSamples = sixteen ? 0x4000 : 0x8000;
        var fullWidth = sixteen ? 17 : 9;
        while (written < result.Length && at + 2 <= d.Length)
        {
            var size = BinaryPrimitives.ReadUInt16LittleEndian(d.AsSpan(at));
            at += 2;
            var end = Math.Min(d.Length, at + size);
            var bit = 0L;
            int Read(int count)
            {
                var v = 0;
                for (var i = 0; i < count; i++, bit++)
                {
                    var o = at + (int)(bit >> 3);
                    if (o >= end) return v;
                    if ((d[o] >> (int)(bit & 7) & 1) != 0) v |= 1 << i;
                }
                return v;
            }
            var count = Math.Min(blockSamples, result.Length - written);
            var width = fullWidth;
            int delta = 0, delta2 = 0;
            for (var i = 0; i < count && at + (int)(bit >> 3) < end;)
            {
                var v = Read(width);
                if (width < 7)
                {
                    if (v == 1 << (width - 1))
                    {
                        var next = Read(sixteen ? 4 : 3) + 1;
                        width = next < width ? next : next + 1;
                        continue;
                    }
                }
                else if (width < fullWidth)
                {
                    var border = (sixteen ? 0xFFFF : 0xFF) >> (fullWidth - width);
                    int lo = border - (sixteen ? 8 : 4), hi = border + (sixteen ? 8 : 4);
                    if (v > lo && v <= hi)
                    {
                        var next = v - lo;
                        width = next < width ? next : next + 1;
                        continue;
                    }
                }
                else
                {
                    var flag = 1 << (fullWidth - 1);
                    if ((v & flag) != 0)
                    {
                        width = (v & (flag - 1)) + 1;
                        continue;
                    }
                }
                // sign-extend to the sample's own width, then integrate
                var shift = 32 - width;
                var signedValue = width < 32 ? v << shift >> shift : v;
                delta += signedValue;
                delta2 += delta;
                var sample = it215 ? delta2 : delta;
                result[written + i] = sixteen ? (short)sample / 32768f : (sbyte)sample / 128f;
                i++;
            }
            written += count;
            at = end;
        }
        return result;
    }
}
