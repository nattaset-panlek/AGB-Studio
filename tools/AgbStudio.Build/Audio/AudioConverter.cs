using System.Globalization;
using NLayer;
using NVorbis;

namespace AgbStudio.Build.Audio;

/// <summary>A song converted for the GBA: an XM module, and what the conversion could not keep.</summary>
public sealed record ConvertedSong(byte[] Xm, int DroppedNotes = 0, bool Truncated = false);

/// <summary>
/// Turns audio the GBA cannot play into XM modules when a file is imported. MP3 and OGG are decoded and become one long
/// 8-bit mono sample, 16.7 kHz, so they play as music with its volume and loop settings. MIDI holds notes and no sound,
/// so its notes become a module played with simple waveforms chosen from each General MIDI program.
/// </summary>
public static class AudioConverter
{
    const int StreamRate = 16726;     // C-4 + one octave at the XM base rate of 8363 Hz
    const int StreamRelativeNote = 12;
    const double RowSeconds = 0.12;   // speed 6 at 125 BPM
    const int RowsPerPattern = 256;
    const int MaxOrders = 256;
    const int MidiRowsPerQuarter = 8; // with speed 3, the module's BPM equals the song's BPM
    const int MidiMaxChannels = 8;    // the default mixing channels of a project

    public static bool IsStream(string extension) => extension.ToLowerInvariant() is ".mp3" or ".ogg";
    public static bool IsMidi(string extension) => extension.ToLowerInvariant() is ".mid" or ".midi";

    /// <summary>Length of an MP3 or OGG file in seconds; the file's data is checked, not decoded.</summary>
    public static double StreamSeconds(string path)
    {
        var seconds = Guarded(path, () =>
        {
            if (Path.GetExtension(path).Equals(".ogg", StringComparison.OrdinalIgnoreCase))
            {
                using var ogg = new VorbisReader(path);
                return ogg.TotalTime.TotalSeconds;
            }
            using var mp3 = new MpegFile(path);
            return mp3.Duration.TotalSeconds;
        });
        if (seconds <= 0) throw new InvalidDataException("It has no sound in it. Export it again from your audio editor.");
        return seconds;
    }

    /// <summary>The bytes an MP3 or OGG file takes in the ROM.</summary>
    public static long StreamRomBytes(double seconds) => (long)Math.Ceiling(seconds * StreamRate);

    public static ConvertedSong StreamToXm(string path)
    {
        var (mono, rate) = Guarded(path, () => Path.GetExtension(path).Equals(".ogg", StringComparison.OrdinalIgnoreCase) ? DecodeOgg(path) : DecodeMp3(path));
        if (mono.Length == 0 || rate <= 0) throw new InvalidDataException("It has no sound in it. Export it again from your audio editor.");
        var sample = To8Bit(Resample(mono, rate, StreamRate));
        var xm = new XmWriter { Title = Title(path), Channels = 2, Tempo = 6, Bpm = 125 };
        xm.Instruments.Add(new XmWriter.Instrument(Title(path), sample, 64, false, StreamRelativeNote));
        var rows = Math.Max(1, (int)Math.Ceiling(sample.Length / (double)StreamRate / RowSeconds));
        var truncated = rows > RowsPerPattern * MaxOrders;
        rows = Math.Min(rows, RowsPerPattern * MaxOrders);
        // The first pattern starts the note; the others only let it ring. Full patterns share one pattern.
        int first = Math.Min(rows, RowsPerPattern), left = rows - first;
        var start = xm.NewPattern(first);
        start[0, 0] = new XmWriter.Cell(XmWriter.Note("C-4"), 1);
        xm.Patterns.Add(start);
        xm.Order.Add(0);
        if (left >= RowsPerPattern)
        {
            xm.Patterns.Add(xm.NewPattern(RowsPerPattern));
            for (var i = 0; i < left / RowsPerPattern; i++) xm.Order.Add(1);
        }
        if (left % RowsPerPattern > 0)
        {
            xm.Patterns.Add(xm.NewPattern(left % RowsPerPattern));
            xm.Order.Add(xm.Patterns.Count - 1);
        }
        return new ConvertedSong(xm.Encode(), 0, truncated);
    }

    static T Guarded<T>(string path, Func<T> read)
    {
        try { return read(); }
        catch (Exception e) when (e is not (OutOfMemoryException or IOException or UnauthorizedAccessException or InvalidDataException))
        {
            throw new InvalidDataException($"It is not a valid {Path.GetExtension(path).TrimStart('.').ToUpperInvariant()} file ({e.Message}) Export it again from your audio editor.");
        }
    }

    static (float[] Mono, int Rate) DecodeMp3(string path)
    {
        using var mp3 = new MpegFile(path);
        return (Mix(n => mp3.ReadSamples(n, 0, n.Length), mp3.Channels), mp3.SampleRate);
    }

    static (float[] Mono, int Rate) DecodeOgg(string path)
    {
        using var ogg = new VorbisReader(path);
        return (Mix(n => ogg.ReadSamples(n, 0, n.Length), ogg.Channels), ogg.SampleRate);
    }

    static float[] Mix(Func<float[], int> read, int channels)
    {
        channels = Math.Max(1, channels);
        var mono = new List<float>();
        var buffer = new float[4096 * channels];
        int got;
        while ((got = read(buffer)) > 0)
            for (var i = 0; i + channels <= got; i += channels)
            {
                var sum = 0f;
                for (var c = 0; c < channels; c++) sum += buffer[i + c];
                mono.Add(sum / channels);
            }
        return [.. mono];
    }

    /// <summary>Resamples, averaging the source over each output sample so a lower rate does not alias as badly.</summary>
    public static float[] Resample(float[] src, int from, int to)
    {
        if (from == to) return src;
        var result = new float[(int)Math.Ceiling(src.Length * (double)to / from)];
        var step = (double)from / to;
        for (var i = 0; i < result.Length; i++)
        {
            var a = i * step;
            if (step <= 1)
            {
                var k = (int)a;
                var f = (float)(a - k);
                result[i] = src[Math.Min(k, src.Length - 1)] * (1 - f) + src[Math.Min(k + 1, src.Length - 1)] * f;
                continue;
            }
            int lo = (int)a, hi = Math.Clamp((int)(a + step), lo + 1, src.Length);
            var sum = 0f;
            for (var k = lo; k < hi; k++) sum += src[Math.Min(k, src.Length - 1)];
            result[i] = sum / (hi - lo);
        }
        return result;
    }

    /// <summary>Signed 8 bits, scaled so the loudest moment nearly fills the range.</summary>
    static sbyte[] To8Bit(float[] s)
    {
        var peak = 0f;
        foreach (var v in s) peak = Math.Max(peak, Math.Abs(v));
        var gain = peak > 0.001f ? 120f / peak : 120f;
        var d = new sbyte[s.Length];
        for (var i = 0; i < s.Length; i++) d[i] = (sbyte)Math.Clamp((int)Math.Round(s[i] * gain), -127, 127);
        return d;
    }

    static string Title(string path)
    {
        var t = Path.GetFileNameWithoutExtension(path);
        return string.Concat(t.Where(c => c is >= ' ' and <= '~'));
    }

    // ---- MIDI ------------------------------------------------------------------------------------------

    sealed record MidiNote(int StartTick, int EndTick, int Key, int Velocity, int Program, bool Drum);

    public static ConvertedSong MidiToXm(byte[] data, string title)
    {
        var (notes, tempos, ppq) = ReadMidi(data);
        if (notes.Count == 0) throw new InvalidDataException("It has no notes in it. Export it again from your music program.");
        int Row(int tick) => (int)(tick * (long)MidiRowsPerQuarter / ppq);

        var xm = new XmWriter { Title = string.Concat(title.Where(c => c is >= ' ' and <= '~')), Channels = MidiMaxChannels, Tempo = 3 };
        xm.Bpm = Bpm(tempos.Where(t => t.Tick == 0).Select(t => t.MicrosPerQuarter).DefaultIfEmpty(500000).Last());
        string[] waves = ["triangle", "sine", "square", "pulse25", "saw"];
        foreach (var w in waves) xm.Instruments.Add(new XmWriter.Instrument(w, ModWriter.Waveform(w), 48));
        xm.Instruments.Add(new XmWriter.Instrument("drum", ModWriter.Noise(2048, 7), 48, false));

        var lastRow = notes.Max(n => Math.Max(Row(n.EndTick), Row(n.StartTick) + 1));
        var patterns = Math.Min(MaxOrders, lastRow / 64 + 1);
        var truncated = lastRow / 64 + 1 > MaxOrders;
        var grid = new XmWriter.Cell[patterns * 64, MidiMaxChannels];
        var freeAt = new int[MidiMaxChannels];
        var dropped = 0;
        var used = 0;
        foreach (var n in notes.OrderBy(n => n.StartTick).ThenBy(n => n.Drum ? 1 : 0).ThenByDescending(n => n.Key))
        {
            var on = Row(n.StartTick);
            if (on >= grid.GetLength(0)) continue;
            var off = Math.Max(on + 1, Row(n.EndTick));
            var ch = Array.FindIndex(freeAt, f => f <= on);
            if (ch < 0) { dropped++; continue; }
            freeAt[ch] = off;
            used = Math.Max(used, ch + 1);
            var volume = 0x10 + n.Velocity * 64 / 127;
            grid[on, ch] = n.Drum
                ? new XmWriter.Cell(DrumNote(n.Key), waves.Length + 1, volume)
                : new XmWriter.Cell(Math.Clamp(n.Key - 11, 1, 96), WaveOf(n.Program) + 1, volume);
            if (off < grid.GetLength(0) && !n.Drum && grid[off, ch].Note == 0) grid[off, ch] = grid[off, ch] with { Note = 97 };
        }
        foreach (var t in tempos.Where(t => t.Tick > 0))
        {
            var row = Row(t.Tick);
            if (row < grid.GetLength(0)) grid[row, 0] = grid[row, 0] with { Effect = 0x0F, Param = Bpm(t.MicrosPerQuarter) };
        }
        // XM channel counts are even in FastTracker II.
        xm.Channels = Math.Max(2, (used + 1) / 2 * 2);
        for (var p = 0; p < patterns; p++)
        {
            var pattern = xm.NewPattern();
            for (var r = 0; r < 64; r++)
                for (var c = 0; c < xm.Channels; c++) pattern[r, c] = grid[p * 64 + r, c];
            xm.Patterns.Add(pattern);
            xm.Order.Add(p);
        }
        return new ConvertedSong(xm.Encode(), dropped, truncated);
    }

    static int Bpm(int microsPerQuarter) => Math.Clamp((int)Math.Round(60000000.0 / Math.Max(1, microsPerQuarter)), 32, 255);

    // General MIDI families to the waves: piano and bass soft, organ and leads square, guitars thin, strings and brass bright.
    static int WaveOf(int program) => (program / 8) switch
    {
        0 or 4 => 0,                 // piano, bass
        1 or 8 or 9 or >= 11 => 1,   // chromatic percussion, reed, pipe, pads and effects
        2 or 10 => 2,                // organ, synth lead
        3 => 3,                      // guitar
        _ => 4,                      // strings, ensemble, brass
    };

    // The drum is one noise burst: low kit pieces play it slowly, cymbals quickly.
    static int DrumNote(int key) => key switch { <= 36 => XmWriter.Note("C-3"), <= 40 => XmWriter.Note("C-5"), <= 48 => XmWriter.Note("G-4"), _ => XmWriter.Note("C-6") };

    static (List<MidiNote> Notes, List<(int Tick, int MicrosPerQuarter)> Tempos, int Ppq) ReadMidi(byte[] d)
    {
        if (d.Length < 14 || d[0] != 'M' || d[1] != 'T' || d[2] != 'h' || d[3] != 'd')
            throw new InvalidDataException("It is not a MIDI song inside (no MThd header). Export it again from your music program.");
        var division = d[12] << 8 | d[13];
        if ((division & 0x8000) != 0 || division == 0)
            throw new InvalidDataException("It counts time in SMPTE frames. Export it again with time in beats (PPQ).");
        var notes = new List<MidiNote>();
        var tempos = new List<(int, int)>();
        var at = 8 + (d[4] << 24 | d[5] << 16 | d[6] << 8 | d[7]);
        while (at + 8 <= d.Length)
        {
            var size = d[at + 4] << 24 | d[at + 5] << 16 | d[at + 6] << 8 | d[at + 7];
            var isTrack = d[at] == 'M' && d[at + 1] == 'T' && d[at + 2] == 'r' && d[at + 3] == 'k';
            var end = Math.Min(d.Length, at + 8 + Math.Max(0, size));
            if (isTrack) ReadTrack(d, at + 8, end, notes, tempos);
            if (size < 0) break;
            at = end;
        }
        return (notes, tempos.OrderBy(t => t.Item1).ToList(), division);
    }

    static void ReadTrack(byte[] d, int p, int end, List<MidiNote> notes, List<(int, int)> tempos)
    {
        var tick = 0;
        byte status = 0;
        var programs = new int[16];
        var open = new Dictionary<(int Channel, int Key), Queue<(int Tick, int Velocity)>>();
        int Var()
        {
            var v = 0;
            while (p < end)
            {
                var b = d[p++];
                v = v << 7 | b & 0x7F;
                if ((b & 0x80) == 0) break;
            }
            return v;
        }
        void Close(int ch, int key)
        {
            if (!open.TryGetValue((ch, key), out var q) || q.Count == 0) return;
            var (start, velocity) = q.Dequeue();
            notes.Add(new MidiNote(start, tick, key, velocity, programs[ch], ch == 9));
        }
        while (p < end)
        {
            tick += Var();
            if (p >= end) break;
            var b = d[p];
            if (b >= 0x80) { status = b; p++; }
            if (status == 0xFF)
            {
                if (p >= end) break;
                var type = d[p++];
                var len = Var();
                if (type == 0x51 && len == 3 && p + 3 <= end) tempos.Add((tick, d[p] << 16 | d[p + 1] << 8 | d[p + 2]));
                p += len;
                continue;
            }
            if (status is 0xF0 or 0xF7) { p += Var(); continue; }
            var channel = status & 15;
            switch (status & 0xF0)
            {
                case 0x90 when p + 2 <= end:
                    var key = d[p];
                    var velocity = d[p + 1];
                    p += 2;
                    if (velocity == 0) { Close(channel, key); break; }
                    if (!open.TryGetValue((channel, key), out var queue)) open[(channel, key)] = queue = new();
                    queue.Enqueue((tick, velocity));
                    break;
                case 0x80 when p + 2 <= end:
                    Close(channel, d[p]);
                    p += 2;
                    break;
                case 0xC0 when p < end: programs[channel] = d[p++]; break;
                case 0xD0: p++; break;
                case 0xA0 or 0xB0 or 0xE0: p += 2; break;
                default: p = end; break;
            }
        }
        foreach (var ((ch, key), q) in open)
            while (q.Count > 0) Close(ch, key);
    }

    /// <summary>"3 min 12 s" for the import dialog.</summary>
    public static string FormatLength(double seconds)
    {
        var total = (int)Math.Round(seconds);
        return total >= 60 ? string.Format(CultureInfo.InvariantCulture, "{0} min {1} s", total / 60, total % 60) : string.Format(CultureInfo.InvariantCulture, "{0} s", total);
    }
}
