using System.Buffers.Binary;
using System.Text;

namespace AgbStudio.Editor.Windows.Audio;

/// <summary>One cell of a tracker pattern. Note uses the XM numbering (1 = C-0 ... 96 = B-7, 97 = key off, 0 = none).</summary>
public readonly record struct TrackerCell(byte Note, byte Instrument, byte Volume, byte Effect, byte Param)
{
    public const byte KeyOff = 97;
    public bool IsEmpty => Note == 0 && Instrument == 0 && Volume == 0 && Effect == 0 && Param == 0;
}

/// <summary>A sample of an instrument as signed values in -1..1.</summary>
public sealed class TrackerSample
{
    public string Name { get; init; } = "";
    public float[] Data { get; init; } = [];
    public int LoopStart { get; init; }
    public int LoopLength { get; init; }
    /// <summary>0 = none, 1 = forward, 2 = ping-pong.</summary>
    public int LoopType { get; init; }
    public int Volume { get; init; } = 64;
    /// <summary>XM: -128..127 (1/128 semitone); MOD: -8..7 eighths of a semitone.</summary>
    public int FineTune { get; init; }
    public int RelativeNote { get; init; }
    public int Panning { get; init; } = 128;
}

/// <summary>An instrument: its samples, note map and volume envelope (XM); MOD instruments have one sample.</summary>
public sealed class TrackerInstrument
{
    public string Name { get; init; } = "";
    public List<TrackerSample> Samples { get; } = [];
    public byte[] NoteMap { get; init; } = new byte[96];
    public (int Tick, int Value)[] VolumeEnvelope { get; init; } = [];
    public bool VolumeEnvelopeOn { get; init; }
    public int VolumeSustain { get; init; } = -1;
    public int VolumeLoopStart { get; init; } = -1;
    public int VolumeLoopEnd { get; init; } = -1;
    public int FadeOut { get; init; }

    public TrackerSample? SampleFor(int note)
    {
        if (Samples.Count == 0) return null;
        var i = note is >= 1 and <= 96 ? NoteMap[note - 1] : 0;
        return i < Samples.Count ? Samples[i] : Samples[0];
    }
}

public sealed class TrackerPattern(int rows, int channels)
{
    public int Rows { get; } = rows;
    public TrackerCell[,] Cells { get; } = new TrackerCell[rows, channels];
}

/// <summary>
/// A MOD (ProTracker, 4 / 6 / 8 channels) or XM (FastTracker II) module as the Audio page reads it: patterns, order,
/// instruments, initial speed and BPM. Pattern edits from the tracker view are written back with <see cref="Encode"/>,
/// which keeps every other byte of the original file.
/// </summary>
public sealed class TrackerModule
{
    byte[] _raw = [];
    int _modPatternBase;                 // MOD: offset of pattern 0
    int _xmPatternsStart, _xmPatternsEnd; // XM: byte range of the pattern block

    public bool IsXm { get; private set; }
    public string Format => IsXm ? "XM" : "MOD";
    public string Title { get; private set; } = "";
    public int Channels { get; private set; }
    public int InitialSpeed { get; private set; } = 6;
    public int InitialBpm { get; private set; } = 125;
    public bool LinearFrequency { get; private set; }
    public int Restart { get; private set; }
    public List<int> Order { get; } = [];
    public List<TrackerPattern> Patterns { get; } = [];
    public List<TrackerInstrument> Instruments { get; } = [];

    public static TrackerModule Load(string path) => Parse(File.ReadAllBytes(path));

    public static TrackerModule Parse(byte[] data)
    {
        var m = new TrackerModule { _raw = data };
        if (data.Length > 60 && Encoding.ASCII.GetString(data, 0, 17) == "Extended Module: ") m.ParseXm();
        else if (data.Length >= 1084) m.ParseMod();
        else throw new InvalidDataException("Not a MOD or XM module.");
        return m;
    }

    /// <summary>Note name as the tracker shows it: "C-4", "C#5", "===" for key off. MOD notes use ProTracker octaves.</summary>
    public string NoteName(int note)
    {
        if (note == TrackerCell.KeyOff) return "===";
        if (note is < 1 or > 96) return "...";
        string[] names = ["C-", "C#", "D-", "D#", "E-", "F-", "F#", "G-", "G#", "A-", "A#", "B-"];
        var n = note - 1;
        var octave = n / 12 - (IsXm ? 0 : 2);
        return names[n % 12] + octave.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>The name shown for a channel: its most used instrument, else the instrument with the channel's number.</summary>
    public string ChannelName(int channel)
    {
        var counts = new Dictionary<int, int>();
        foreach (var p in Patterns)
            for (var r = 0; r < p.Rows; r++)
            {
                var c = p.Cells[r, channel];
                if (c.Instrument > 0) counts[c.Instrument] = counts.GetValueOrDefault(c.Instrument) + 1;
            }
        var ins = counts.Count > 0 ? counts.MaxBy(kv => kv.Value).Key : channel + 1;
        var name = ins <= Instruments.Count ? Instruments[ins - 1].Name.Trim() : "";
        return name.Length > 0 ? name : $"Ch {channel + 1}";
    }

    public bool ChannelHasNotes(int channel) =>
        Patterns.Any(p => Enumerable.Range(0, p.Rows).Any(r => p.Cells[r, channel].Note is > 0 and < TrackerCell.KeyOff));

    // ---- MOD ------------------------------------------------------------------------------------------

    static readonly int[] AmigaPeriods =
    [
        1712, 1616, 1525, 1440, 1357, 1281, 1209, 1141, 1077, 1017, 961, 907,
        856, 808, 762, 720, 678, 640, 604, 570, 538, 508, 480, 453,
        428, 404, 381, 360, 339, 320, 302, 285, 269, 254, 240, 226,
        214, 202, 190, 180, 170, 160, 151, 143, 135, 127, 120, 113,
        107, 101, 95, 90, 85, 80, 76, 71, 67, 64, 60, 57,
    ];

    /// <summary>MOD period to note (XM numbering: ProTracker C-1 = period 856 = note 37, C-3 of the XM scale).</summary>
    public static int PeriodToNote(int period)
    {
        if (period <= 0) return 0;
        var best = 0;
        for (var i = 1; i < AmigaPeriods.Length; i++)
            if (Math.Abs(AmigaPeriods[i] - period) < Math.Abs(AmigaPeriods[best] - period)) best = i;
        return best + 25;  // table starts at ProTracker C-0 (= XM C-2, note 25)
    }

    public static int NoteToPeriod(int note) => note is >= 25 and < 25 + 60 ? AmigaPeriods[note - 25] : 0;

    void ParseMod()
    {
        var d = _raw;
        Title = Encoding.ASCII.GetString(d, 0, 20).TrimEnd('\0', ' ');
        var sig = Encoding.ASCII.GetString(d, 1080, 4);
        Channels = sig switch
        {
            "M.K." or "M!K!" or "FLT4" or "4CHN" => 4,
            "6CHN" => 6,
            "8CHN" or "FLT8" or "OCTA" => 8,
            _ when sig.EndsWith("CH", StringComparison.Ordinal) && int.TryParse(sig[..2], out var n) => n,
            _ when sig.EndsWith("CHN", StringComparison.Ordinal) && int.TryParse(sig[..1], out var n1) => n1,
            _ => throw new InvalidDataException($"Unknown MOD signature '{sig}'."),
        };
        var songLength = d[950];
        Restart = d[951] < songLength ? d[951] : 0;
        var maxPattern = 0;
        for (var i = 0; i < 128; i++) maxPattern = Math.Max(maxPattern, d[952 + i]);
        for (var i = 0; i < songLength; i++) Order.Add(d[952 + i]);
        _modPatternBase = 1084;
        var patternBytes = 64 * Channels * 4;
        for (var p = 0; p <= maxPattern; p++)
        {
            var pat = new TrackerPattern(64, Channels);
            for (var r = 0; r < 64; r++)
                for (var c = 0; c < Channels; c++)
                {
                    var o = _modPatternBase + p * patternBytes + (r * Channels + c) * 4;
                    if (o + 4 > d.Length) continue;
                    var sample = (d[o] & 0xF0) | (d[o + 2] >> 4);
                    var period = ((d[o] & 0x0F) << 8) | d[o + 1];
                    pat.Cells[r, c] = new TrackerCell((byte)PeriodToNote(period), (byte)sample, 0, (byte)(d[o + 2] & 0x0F), d[o + 3]);
                }
            Patterns.Add(pat);
        }
        var so = _modPatternBase + (maxPattern + 1) * patternBytes;
        for (var s = 0; s < 31; s++)
        {
            var h = 20 + s * 30;
            var len = BinaryPrimitives.ReadUInt16BigEndian(d.AsSpan(h + 22)) * 2;
            var fine = d[h + 24] & 0x0F;
            if (fine > 7) fine -= 16;
            var loopStart = BinaryPrimitives.ReadUInt16BigEndian(d.AsSpan(h + 26)) * 2;
            var loopLen = BinaryPrimitives.ReadUInt16BigEndian(d.AsSpan(h + 28)) * 2;
            var data = new float[Math.Max(0, Math.Min(len, d.Length - so))];
            for (var i = 0; i < data.Length; i++) data[i] = (sbyte)d[so + i] / 128f;
            so += len;
            var ins = new TrackerInstrument { Name = Encoding.ASCII.GetString(d, h, 22).TrimEnd('\0', ' ') };
            ins.Samples.Add(new TrackerSample
            {
                Name = ins.Name, Data = data, Volume = Math.Min(64, (int)d[h + 25]), FineTune = fine,
                LoopStart = Math.Min(loopStart, data.Length), LoopLength = loopLen > 2 ? Math.Min(loopLen, data.Length - Math.Min(loopStart, data.Length)) : 0,
                LoopType = loopLen > 2 ? 1 : 0,
            });
            Instruments.Add(ins);
        }
        // trim unused trailing instruments (names only)
        while (Instruments.Count > 0 && Instruments[^1].Samples[0].Data.Length == 0 && Instruments[^1].Name.Length == 0) Instruments.RemoveAt(Instruments.Count - 1);
        LinearFrequency = false;
    }

    // ---- XM -------------------------------------------------------------------------------------------

    void ParseXm()
    {
        var d = _raw;
        IsXm = true;
        Title = Encoding.ASCII.GetString(d, 17, 20).TrimEnd('\0', ' ');
        var headerSize = BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(60));
        int U16(int o) => BinaryPrimitives.ReadUInt16LittleEndian(d.AsSpan(o));
        var songLength = U16(64);
        Restart = U16(66);
        Channels = U16(68);
        var patterns = U16(70);
        var instruments = U16(72);
        LinearFrequency = (U16(74) & 1) != 0;
        InitialSpeed = Math.Max(1, (int)U16(76));
        InitialBpm = Math.Max(32, (int)U16(78));
        for (var i = 0; i < Math.Min(songLength, 256); i++) Order.Add(d[80 + i]);
        var o = 60 + headerSize;
        _xmPatternsStart = o;
        for (var p = 0; p < patterns; p++)
        {
            var len = BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(o));
            var rows = U16(o + 5);
            var packed = U16(o + 7);
            var pat = new TrackerPattern(rows, Channels);
            var q = o + len;
            var end = q + packed;
            if (packed > 0)
                for (var r = 0; r < rows; r++)
                    for (var c = 0; c < Channels && q < end; c++)
                    {
                        byte note = 0, ins = 0, vol = 0, eff = 0, par = 0;
                        var b = d[q++];
                        if ((b & 0x80) != 0)
                        {
                            if ((b & 1) != 0) note = d[q++];
                            if ((b & 2) != 0) ins = d[q++];
                            if ((b & 4) != 0) vol = d[q++];
                            if ((b & 8) != 0) eff = d[q++];
                            if ((b & 16) != 0) par = d[q++];
                        }
                        else
                        {
                            note = b;
                            ins = d[q++];
                            vol = d[q++];
                            eff = d[q++];
                            par = d[q++];
                        }
                        pat.Cells[r, c] = new TrackerCell(note, ins, vol, eff, par);
                    }
            Patterns.Add(pat);
            o = end;
        }
        _xmPatternsEnd = o;
        for (var i = 0; i < instruments && o + 29 <= d.Length; i++)
        {
            var size = BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(o));
            var name = Encoding.ASCII.GetString(d, o + 4, 22).TrimEnd('\0', ' ');
            var count = U16(o + 27);
            if (count == 0)
            {
                Instruments.Add(new TrackerInstrument { Name = name });
                o += size;
                continue;
            }
            var sampleHeader = BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(o + 29));
            var map = d.AsSpan(o + 33, 96).ToArray();
            var env = new List<(int, int)>();
            var points = d[o + 225];
            for (var k = 0; k < Math.Min((int)points, 12); k++) env.Add((U16(o + 129 + k * 4), U16(o + 131 + k * 4)));
            var volType = d[o + 233];
            var ins = new TrackerInstrument
            {
                Name = name, NoteMap = map, VolumeEnvelope = [.. env], VolumeEnvelopeOn = (volType & 1) != 0 && env.Count > 0,
                VolumeSustain = (volType & 2) != 0 ? d[o + 227] : -1,
                VolumeLoopStart = (volType & 4) != 0 ? d[o + 228] : -1, VolumeLoopEnd = (volType & 4) != 0 ? d[o + 229] : -1,
                FadeOut = U16(o + 239),
            };
            o += size;
            var headers = new List<(int Len, int LoopStart, int LoopLen, int Vol, int Fine, int Type, int Pan, int Rel, string Name)>();
            for (var s = 0; s < count; s++)
            {
                var h = o + s * sampleHeader;
                headers.Add((BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(h)), BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(h + 4)),
                    BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(h + 8)), d[h + 12], (sbyte)d[h + 13], d[h + 14], d[h + 15], (sbyte)d[h + 16],
                    Encoding.ASCII.GetString(d, h + 18, 22).TrimEnd('\0', ' ')));
            }
            o += count * sampleHeader;
            foreach (var h in headers)
            {
                var sixteen = (h.Type & 16) != 0;
                var n = sixteen ? h.Len / 2 : h.Len;
                var data = new float[Math.Max(0, Math.Min(n, (d.Length - o) / (sixteen ? 2 : 1)))];
                if (sixteen)
                {
                    short acc = 0;
                    for (var k = 0; k < data.Length; k++) { acc += BinaryPrimitives.ReadInt16LittleEndian(d.AsSpan(o + k * 2)); data[k] = acc / 32768f; }
                }
                else
                {
                    sbyte acc = 0;
                    for (var k = 0; k < data.Length; k++) { acc += (sbyte)d[o + k]; data[k] = acc / 128f; }
                }
                o += h.Len;
                var div = sixteen ? 2 : 1;
                var loopStart = Math.Min(h.LoopStart / div, data.Length);
                ins.Samples.Add(new TrackerSample
                {
                    Name = h.Name, Data = data, Volume = Math.Min(64, h.Vol), FineTune = h.Fine, Panning = h.Pan, RelativeNote = h.Rel,
                    LoopStart = loopStart, LoopLength = Math.Min(h.LoopLen / div, data.Length - loopStart), LoopType = h.LoopLen > 0 ? h.Type & 3 : 0,
                });
            }
            Instruments.Add(ins);
        }
    }

    /// <summary>The module file with the current pattern data (tracker edits); everything else is kept byte for byte.</summary>
    public byte[] Encode()
    {
        if (!IsXm)
        {
            var d = (byte[])_raw.Clone();
            for (var p = 0; p < Patterns.Count; p++)
                for (var r = 0; r < 64; r++)
                    for (var c = 0; c < Channels; c++)
                    {
                        var o = _modPatternBase + p * 64 * Channels * 4 + (r * Channels + c) * 4;
                        if (o + 4 > d.Length) continue;
                        var cell = Patterns[p].Cells[r, c];
                        var oldPeriod = ((d[o] & 0x0F) << 8) | d[o + 1];
                        var period = cell.Note == 0 ? 0 : PeriodToNote(oldPeriod) == cell.Note ? oldPeriod : NoteToPeriod(cell.Note);
                        d[o] = (byte)((cell.Instrument & 0xF0) | ((period >> 8) & 0x0F));
                        d[o + 1] = (byte)period;
                        d[o + 2] = (byte)(((cell.Instrument & 0x0F) << 4) | (cell.Effect & 0x0F));
                        d[o + 3] = cell.Param;
                    }
            return d;
        }
        using var ms = new MemoryStream();
        ms.Write(_raw, 0, _xmPatternsStart);
        Span<byte> head = stackalloc byte[9];
        foreach (var p in Patterns)
        {
            var data = new MemoryStream();
            for (var r = 0; r < p.Rows; r++)
                for (var c = 0; c < Channels; c++)
                {
                    var cell = p.Cells[r, c];
                    byte mask = 0x80;
                    if (cell.Note != 0) mask |= 1;
                    if (cell.Instrument != 0) mask |= 2;
                    if (cell.Volume != 0) mask |= 4;
                    if (cell.Effect != 0) mask |= 8;
                    if (cell.Param != 0) mask |= 16;
                    data.WriteByte(mask);
                    if (cell.Note != 0) data.WriteByte(cell.Note);
                    if (cell.Instrument != 0) data.WriteByte(cell.Instrument);
                    if (cell.Volume != 0) data.WriteByte(cell.Volume);
                    if (cell.Effect != 0) data.WriteByte(cell.Effect);
                    if (cell.Param != 0) data.WriteByte(cell.Param);
                }
            BinaryPrimitives.WriteInt32LittleEndian(head, 9);
            head[4] = 0;
            BinaryPrimitives.WriteUInt16LittleEndian(head[5..], (ushort)p.Rows);
            BinaryPrimitives.WriteUInt16LittleEndian(head[7..], (ushort)data.Length);
            ms.Write(head);
            data.Position = 0;
            data.CopyTo(ms);
        }
        ms.Write(_raw, _xmPatternsEnd, _raw.Length - _xmPatternsEnd);
        return ms.ToArray();
    }
}
