namespace AgbStudio.Editor.Windows.Audio;

/// <summary>
/// Renders a <see cref="TrackerModule"/> to 16-bit stereo PCM for the Audio page preview (P11.43). It follows the
/// ProTracker / FastTracker II playback rules that Maxmod implements: speed and BPM (Fxx), notes with instrument
/// volume and panning, the XM volume column, volume envelopes with fade-out, portamento (1xx 2xx 3xx 5xx), vibrato
/// (4xx 6xx), arpeggio, volume slides, sample offset, position jump / pattern break, pattern loop and delay, note
/// cut / delay, key off and global volume. Unsupported effects are ignored.
/// </summary>
public sealed class ModulePlayer
{
    sealed class Voice
    {
        public TrackerInstrument? Instrument;
        public TrackerSample? Sample;
        public double Pos;
        public bool Backwards, Active;
        public int Note, Volume, Pan = 128;
        public double Period, BasePeriod, TargetPeriod;
        public int PortaSpeed, VibratoSpeed, VibratoDepth, VibratoPos, VolSlide, PortaUp, PortaDown, Offset;
        public int FinePortaUp, FinePortaDown, FineVolUp, FineVolDown;
        public bool KeyOn = true;
        public int EnvTick, FadeVolume = 65536;
        public int Effect, Param, ArpTick;
        public int LoopRow, LoopCount;
        public double Peak;
    }

    readonly TrackerModule _m;
    readonly Voice[] _voices;
    int _speed, _bpm, _tick, _order, _row, _globalVolume = 64;
    int _nextOrder = -1, _nextRow = -1, _patternDelay;
    double _tickRemain;
    bool _rowPending = true;
    readonly HashSet<(int, int)> _visited = [];

    public int SampleRate { get; }
    /// <summary>Music volume (MusicMeta.Volume, 0..1).</summary>
    public double Volume { get; set; } = 1;
    /// <summary>Order to continue at when the song ends (MusicMeta.LoopStart); null = stop at the end.</summary>
    public int? LoopOrder { get; set; }
    public bool Ended { get; private set; }
    public int Order => _order;
    public int Row => _row;
    public int Speed => _speed;
    public int Bpm => _bpm;
    /// <summary>Per channel: muted channels are not mixed.</summary>
    public bool[] Muted { get; }
    /// <summary>Per channel peak level (0..1 of one full-volume sample) since the last reset.</summary>
    public double[] ChannelPeak => _voices.Select(v => v.Peak).ToArray();
    /// <summary>Raised on the render thread when a new row starts: (order, row, frame index in the current render call).</summary>
    public event Action<int, int, int>? RowStarted;

    public ModulePlayer(TrackerModule module, int sampleRate = 44100)
    {
        _m = module;
        SampleRate = sampleRate;
        _voices = Enumerable.Range(0, module.Channels).Select(i => new Voice { Pan = module.IsXm ? 128 : (i % 4 is 0 or 3 ? 64 : 192) }).ToArray();
        Muted = new bool[module.Channels];
        _speed = module.InitialSpeed;
        _bpm = module.InitialBpm;
    }

    public void Seek(int order, int row = 0)
    {
        _order = Math.Clamp(order, 0, Math.Max(0, _m.Order.Count - 1));
        _row = Math.Max(0, row);
        _tick = 0;
        _rowPending = true;
        _tickRemain = 0;
        _visited.Clear();
        Ended = _m.Order.Count == 0;
        foreach (var v in _voices) { v.Active = false; v.Peak = 0; }
    }

    public void ResetPeaks() { foreach (var v in _voices) v.Peak = 0; }

    double SamplesPerTick => SampleRate * 2.5 / _bpm;

    /// <summary>Renders interleaved stereo frames into <paramref name="buffer"/>; returns the frames written (less at the end of the song).</summary>
    public int Render(Span<short> buffer)
    {
        var frames = buffer.Length / 2;
        var f = 0;
        while (f < frames && !Ended)
        {
            if (_tickRemain <= 0)
            {
                if (_rowPending) { StartRow(f); if (Ended) break; }
                else TickEffects();
                _tickRemain += SamplesPerTick;
                AdvanceTick();
            }
            var n = Math.Min(frames - f, (int)Math.Ceiling(_tickRemain));
            Mix(buffer.Slice(f * 2, n * 2));
            _tickRemain -= n;
            f += n;
        }
        return f;
    }

    void AdvanceTick()
    {
        _ticksTotal++;
        _tick++;
        if (_tick >= _speed * (1 + _patternDelay))
        {
            _tick = 0;
            _patternDelay = 0;
            NextRow();
            _rowPending = true;
        }
    }

    void NextRow()
    {
        if (_nextOrder >= 0 || _nextRow >= 0)
        {
            if (_nextOrder >= 0) _order = _nextOrder;
            else _order++;
            _row = Math.Max(0, _nextRow);
            _nextOrder = _nextRow = -1;
        }
        else
        {
            _row++;
            var pat = Pattern;
            if (pat is null || _row >= pat.Rows) { _row = 0; _order++; }
        }
        if (_order >= _m.Order.Count)
        {
            if (LoopOrder is { } lo && lo < _m.Order.Count) { _order = lo; _row = 0; _visited.Clear(); }
            else Ended = true;
        }
    }

    TrackerPattern? Pattern => _order < _m.Order.Count && _m.Order[_order] < _m.Patterns.Count ? _m.Patterns[_m.Order[_order]] : null;

    void StartRow(int frame)
    {
        _rowPending = false;
        if (!_visited.Add((_order, _row)) && LoopOrder is null) { Ended = true; return; }
        RowStarted?.Invoke(_order, _row, frame);
        var pat = Pattern;
        if (pat is null) { Ended = true; return; }
        for (var c = 0; c < _m.Channels; c++) RowChannel(_voices[c], pat.Cells[Math.Min(_row, pat.Rows - 1), c]);
    }

    void RowChannel(Voice v, TrackerCell cell)
    {
        v.Effect = cell.Effect;
        v.Param = cell.Param;
        v.ArpTick = 0;
        var porta = cell.Effect is 3 or 5 || (_m.IsXm && cell.Volume >> 4 == 0xF);
        var delay = cell.Effect == 0xE && cell.Param >> 4 == 0xD && (cell.Param & 15) > 0;
        if (cell.Instrument > 0 && cell.Instrument <= _m.Instruments.Count)
        {
            v.Instrument = _m.Instruments[cell.Instrument - 1];
            var s = v.Instrument.SampleFor(cell.Note is > 0 and < TrackerCell.KeyOff ? cell.Note : Math.Max(1, v.Note));
            if (s != null) { v.Volume = s.Volume; if (_m.IsXm) v.Pan = s.Panning; }
            v.KeyOn = true;
            v.EnvTick = 0;
            v.FadeVolume = 65536;
        }
        if (cell.Note == TrackerCell.KeyOff) v.KeyOn = false;
        else if (cell.Note > 0 && !delay)
        {
            if (porta && v.Active) v.TargetPeriod = PeriodOf(v, cell.Note);
            else Trigger(v, cell.Note);
        }
        if (_m.IsXm) VolumeColumn(v, cell.Volume);
        RowEffect(v, cell);
    }

    void Trigger(Voice v, int note)
    {
        if (v.Instrument is null) return;
        var s = v.Instrument.SampleFor(note);
        if (s is null || s.Data.Length == 0) { v.Active = false; return; }
        v.Note = note;
        v.Sample = s;
        v.Period = v.BasePeriod = v.TargetPeriod = PeriodOf(v, note);
        v.Pos = 0;
        v.Backwards = false;
        v.Active = true;
        v.VibratoPos = 0;
        v.KeyOn = true;
        v.EnvTick = 0;
        v.FadeVolume = 65536;
    }

    double PeriodOf(Voice v, int note)
    {
        var s = v.Instrument?.SampleFor(note) ?? v.Sample;
        var rel = s?.RelativeNote ?? 0;
        var fine = s?.FineTune ?? 0;
        if (!_m.IsXm)
            return TrackerModule.NoteToPeriod(Math.Clamp(note, 25, 84)) * Math.Pow(2, -fine / 96.0);
        var n = note - 1 + rel;
        return _m.LinearFrequency ? 7680 - n * 64 - fine / 2.0 : 27392 * Math.Pow(2, -(n + fine / 128.0) / 12);
    }

    double Frequency(double period)
    {
        if (period <= 0) return 0;
        if (!_m.IsXm) return 3546895 / period;
        return _m.LinearFrequency ? 8363 * Math.Pow(2, (4608 - period) / 768) : 8363 * 1712 / period;
    }

    int PortaUnit => _m.IsXm ? 4 : 1;

    void VolumeColumn(Voice v, int vol)
    {
        if (vol is >= 0x10 and <= 0x50) v.Volume = vol - 0x10;
        else if (vol >> 4 == 0xC) v.Pan = (vol & 15) * 17;
        else if (vol >> 4 == 0x8) v.Volume = Math.Max(0, v.Volume - (vol & 15));
        else if (vol >> 4 == 0x9) v.Volume = Math.Min(64, v.Volume + (vol & 15));
        else if (vol >> 4 == 0xF && (vol & 15) != 0) v.PortaSpeed = (vol & 15) << 4;
    }

    void RowEffect(Voice v, TrackerCell cell)
    {
        var p = cell.Param;
        switch (cell.Effect)
        {
            case 0x1: if (p != 0) v.PortaUp = p; break;
            case 0x2: if (p != 0) v.PortaDown = p; break;
            case 0x3: if (p != 0) v.PortaSpeed = p; break;
            case 0x4:
                if ((p >> 4) != 0) v.VibratoSpeed = p >> 4;
                if ((p & 15) != 0) v.VibratoDepth = p & 15;
                break;
            case 0x5: case 0x6: case 0xA: if (p != 0) v.VolSlide = p; break;
            case 0x8: v.Pan = p; break;
            case 0x9:
                if (p != 0) v.Offset = p * 256;
                if (v.Active && v.Sample != null) { v.Pos = v.Offset; if (v.Pos >= v.Sample.Data.Length) v.Active = false; }
                break;
            case 0xB: _nextOrder = p; _nextRow = Math.Max(0, _nextRow); break;
            case 0xC: v.Volume = Math.Min(64, (int)p); break;
            case 0xD: _nextRow = (p >> 4) * 10 + (p & 15); if (_nextOrder < 0) _nextOrder = _order + 1; break;
            case 0xE: ExtendedRow(v, p); break;
            case 0xF:
                if (p == 0) break;
                if (p < 32) _speed = p; else _bpm = p;
                break;
            case 0x10 when _m.IsXm: _globalVolume = Math.Min(64, (int)p); break;  // Gxx
            case 0x14 when _m.IsXm: v.KeyOn = false; break;                       // Kxx
        }
    }

    void ExtendedRow(Voice v, int p)
    {
        var x = p & 15;
        switch (p >> 4)
        {
            case 0x1: if (x != 0) v.FinePortaUp = x; v.Period -= v.FinePortaUp * PortaUnit; v.BasePeriod = v.Period; break;
            case 0x2: if (x != 0) v.FinePortaDown = x; v.Period += v.FinePortaDown * PortaUnit; v.BasePeriod = v.Period; break;
            case 0x6:
                if (x == 0) v.LoopRow = _row;
                else if (v.LoopCount == 0) { v.LoopCount = x; _nextOrder = _order; _nextRow = v.LoopRow; _visited.Clear(); }
                else if (--v.LoopCount > 0) { _nextOrder = _order; _nextRow = v.LoopRow; _visited.Clear(); }
                break;
            case 0xA: if (x != 0) v.FineVolUp = x; v.Volume = Math.Min(64, v.Volume + v.FineVolUp); break;
            case 0xB: if (x != 0) v.FineVolDown = x; v.Volume = Math.Max(0, v.Volume - v.FineVolDown); break;
            case 0xC: if (x == 0) v.Volume = 0; break;
            case 0xE: _patternDelay = x; break;
        }
    }

    void TickEffects()
    {
        var pat = Pattern;
        for (var c = 0; c < _voices.Length; c++)
        {
            var v = _voices[c];
            var p = v.Param;
            var tick = _tick % _speed;
            if (pat != null && _m.IsXm)
            {
                var vol = pat.Cells[Math.Min(_row, pat.Rows - 1), c].Volume;
                if (vol >> 4 == 0x6) v.Volume = Math.Max(0, v.Volume - (vol & 15));
                else if (vol >> 4 == 0x7) v.Volume = Math.Min(64, v.Volume + (vol & 15));
                else if (vol >> 4 == 0xF) TonePorta(v);
            }
            switch (v.Effect)
            {
                case 0x0 when p != 0:
                    var k = ++v.ArpTick % 3;
                    var semis = k == 0 ? 0 : k == 1 ? p >> 4 : p & 15;
                    v.Period = !_m.IsXm || !_m.LinearFrequency ? v.BasePeriod * Math.Pow(2, -semis / 12.0) : v.BasePeriod - semis * 64;
                    break;
                case 0x1: v.Period = v.BasePeriod = Math.Max(1, v.BasePeriod - v.PortaUp * PortaUnit); break;
                case 0x2: v.Period = v.BasePeriod = v.BasePeriod + v.PortaDown * PortaUnit; break;
                case 0x3: TonePorta(v); break;
                case 0x4: Vibrato(v); break;
                case 0x5: TonePorta(v); VolSlide(v); break;
                case 0x6: Vibrato(v); VolSlide(v); break;
                case 0xA: VolSlide(v); break;
                case 0xE:
                    var x = p & 15;
                    if (p >> 4 == 0xC && tick == x) v.Volume = 0;
                    if (p >> 4 == 0xD && tick == x && pat != null)
                    {
                        var cell = pat.Cells[Math.Min(_row, pat.Rows - 1), c];
                        if (cell.Note is > 0 and < TrackerCell.KeyOff) Trigger(v, cell.Note);
                    }
                    if (p >> 4 == 0x9 && x > 0 && tick % x == 0 && v.Sample != null) { v.Pos = 0; v.Active = true; }
                    break;
                case 0x14 when _m.IsXm && tick == p: v.KeyOn = false; break;
            }
        }
    }

    void TonePorta(Voice v)
    {
        var step = v.PortaSpeed * PortaUnit;
        if (v.BasePeriod < v.TargetPeriod) v.BasePeriod = Math.Min(v.TargetPeriod, v.BasePeriod + step);
        else if (v.BasePeriod > v.TargetPeriod) v.BasePeriod = Math.Max(v.TargetPeriod, v.BasePeriod - step);
        v.Period = v.BasePeriod;
    }

    void Vibrato(Voice v)
    {
        var sine = Math.Sin(v.VibratoPos * Math.PI / 32) * 255;
        v.Period = v.BasePeriod + sine * v.VibratoDepth / 128 * PortaUnit;
        v.VibratoPos = (v.VibratoPos + v.VibratoSpeed) & 63;
    }

    static void VolSlide(Voice v)
    {
        var up = v.VolSlide >> 4;
        var down = v.VolSlide & 15;
        v.Volume = up > 0 ? Math.Min(64, v.Volume + up) : Math.Max(0, v.Volume - down);
    }

    double EnvelopeVolume(Voice v)
    {
        var ins = v.Instrument;
        if (ins is null || !ins.VolumeEnvelopeOn || ins.VolumeEnvelope.Length == 0) return v.KeyOn || !_m.IsXm ? 1 : 0;
        var env = ins.VolumeEnvelope;
        var t = v.EnvTick;
        double value = env[^1].Value;
        for (var i = 0; i < env.Length - 1; i++)
            if (t >= env[i].Tick && t <= env[i + 1].Tick)
            {
                var span = Math.Max(1, env[i + 1].Tick - env[i].Tick);
                value = env[i].Value + (env[i + 1].Value - env[i].Value) * (t - env[i].Tick) / (double)span;
                break;
            }
        if (t < env[0].Tick) value = env[0].Value;
        // advance once per tick (called once per tick from Mix via a counter)
        return Math.Clamp(value / 64.0, 0, 1);
    }

    void AdvanceEnvelope(Voice v)
    {
        var ins = v.Instrument;
        if (ins is null) return;
        if (!v.KeyOn) v.FadeVolume = Math.Max(0, v.FadeVolume - ins.FadeOut);
        if (!ins.VolumeEnvelopeOn || ins.VolumeEnvelope.Length == 0) return;
        var env = ins.VolumeEnvelope;
        if (v.KeyOn && ins.VolumeSustain >= 0 && ins.VolumeSustain < env.Length && v.EnvTick >= env[ins.VolumeSustain].Tick) return;
        v.EnvTick++;
        if (ins.VolumeLoopEnd >= 0 && ins.VolumeLoopEnd < env.Length && ins.VolumeLoopStart >= 0 && v.EnvTick >= env[ins.VolumeLoopEnd].Tick)
            v.EnvTick = env[ins.VolumeLoopStart].Tick;
    }

    long _ticksTotal, _tickCounter = -1;

    void Mix(Span<short> outBuf)
    {
        var frames = outBuf.Length / 2;
        Span<float> l = frames <= 4096 ? stackalloc float[frames] : new float[frames];
        Span<float> r = frames <= 4096 ? stackalloc float[frames] : new float[frames];
        if (_tickCounter != _ticksTotal) { _tickCounter = _ticksTotal; foreach (var v in _voices) AdvanceEnvelope(v); }
        for (var c = 0; c < _voices.Length; c++)
        {
            var v = _voices[c];
            if (!v.Active || v.Sample is null || Muted[c]) continue;
            var s = v.Sample;
            var data = s.Data;
            var step = Frequency(v.Period) / SampleRate;
            if (step <= 0) continue;
            var gain = v.Volume / 64.0 * EnvelopeVolume(v) * (_m.IsXm ? v.FadeVolume / 65536.0 : 1) * _globalVolume / 64.0;
            var pan = v.Pan / 255.0;
            var gl = (float)(gain * Math.Sqrt(1 - pan) * Math.Sqrt(2) * 0.5);
            var gr = (float)(gain * Math.Sqrt(pan) * Math.Sqrt(2) * 0.5);
            var loopEnd = s.LoopType != 0 && s.LoopLength > 0 ? s.LoopStart + s.LoopLength : data.Length;
            for (var i = 0; i < frames; i++)
            {
                var ip = (int)v.Pos;
                if (ip >= data.Length || ip < 0) { v.Active = false; break; }
                var frac = (float)(v.Pos - ip);
                var next = ip + 1 < loopEnd ? data[ip + 1] : s.LoopType == 1 ? data[s.LoopStart] : data[ip];
                var x = data[ip] + (next - data[ip]) * frac;
                var a = Math.Abs(x * gain);
                if (a > v.Peak) v.Peak = a;
                l[i] += x * gl;
                r[i] += x * gr;
                v.Pos += v.Backwards ? -step : step;
                if (s.LoopType == 0) continue;
                if (!v.Backwards && v.Pos >= loopEnd)
                {
                    if (s.LoopType == 1) v.Pos = s.LoopStart + (v.Pos - loopEnd) % Math.Max(1, s.LoopLength);
                    else { v.Backwards = true; v.Pos = loopEnd - 1 - (v.Pos - loopEnd); }
                }
                else if (v.Backwards && v.Pos < s.LoopStart) { v.Backwards = false; v.Pos = s.LoopStart + (s.LoopStart - v.Pos); }
            }
        }
        var master = (float)(Volume * 32767 * 0.7);
        for (var i = 0; i < frames; i++)
        {
            outBuf[i * 2] = (short)Math.Clamp(l[i] * master, -32768, 32767);
            outBuf[i * 2 + 1] = (short)Math.Clamp(r[i] * master, -32768, 32767);
        }
    }
}

/// <summary>What the Audio page shows for a module: length, overview peaks, channel levels and the time of every row.</summary>
public sealed record ModuleAnalysis(double Seconds, int Bpm, int Speed, int PatternCount, float[] Overview, double[] ChannelDb, IReadOnlyList<(double Time, int Order, int Row)> Rows)
{
    /// <summary>Plays the song once (without its loop) at a low rate and measures it.</summary>
    public static ModuleAnalysis Of(TrackerModule m, int bars = 160)
    {
        const int rate = 11025;
        var player = new ModulePlayer(m, rate);
        var rows = new List<(double, int, int)>();
        long framesDone = 0;
        player.RowStarted += (o, r, f) => rows.Add(((framesDone + f) / (double)rate, o, r));
        var peaks = new List<float>();
        var buf = new short[2 * 256];
        int? bpm = null, speed = null;
        while (!player.Ended && framesDone < rate * 60L * 20)
        {
            var n = player.Render(buf);
            bpm ??= player.Bpm;      // after the first row: its Fxx sets the song's tempo
            speed ??= player.Speed;
            float pk = 0;
            for (var i = 0; i < n * 2; i++) pk = Math.Max(pk, Math.Abs(buf[i] / 32768f));
            peaks.Add(pk);
            framesDone += n;
            if (n == 0) break;
        }
        var seconds = framesDone / (double)rate;
        var overview = new float[bars];
        if (peaks.Count > 0)
            for (var b = 0; b < bars; b++)
            {
                var from = b * peaks.Count / bars;
                var to = Math.Max(from + 1, (b + 1) * peaks.Count / bars);
                float sum = 0;
                for (var i = from; i < to && i < peaks.Count; i++) sum += peaks[i];
                overview[b] = sum / (to - from);
            }
        var max = overview.DefaultIfEmpty(0).Max();
        if (max > 0) for (var b = 0; b < bars; b++) overview[b] /= max;
        var db = player.ChannelPeak.Select(p => p > 0 ? 20 * Math.Log10(p) : double.NegativeInfinity).ToArray();
        var patterns = m.Order.Distinct().Count();
        return new ModuleAnalysis(seconds, bpm ?? m.InitialBpm, speed ?? m.InitialSpeed, patterns, overview, db, rows);
    }

    /// <summary>The time a row starts (first time it is played), or 0.</summary>
    public double TimeOf(int order, int row)
    {
        foreach (var r in Rows) if (r.Order == order && r.Row == row) return r.Time;
        foreach (var r in Rows) if (r.Order == order) return r.Time;
        return 0;
    }

    public (int Order, int Row) RowAt(double seconds)
    {
        (int, int) best = Rows.Count > 0 ? (Rows[0].Order, Rows[0].Row) : (0, 0);
        foreach (var r in Rows)
        {
            if (r.Time > seconds) break;
            best = (r.Order, r.Row);
        }
        return best;
    }

    public static string FormatTime(double seconds)
    {
        var s = (int)Math.Round(seconds);
        return $"{s / 60}:{s % 60:00}";
    }
}
