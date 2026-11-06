// Ported from Maxmod Utility (mmutil) 1.10.1, xm.c.
// Copyright (c) 2008, Mukunda Johnson. BSD 3-clause licence: see LICENSE-mmutil.txt in this folder.

namespace AgbStudio.Build.Audio.Maxmod;

/// <summary>FastTracker II XM loader; XM effects are converted to maxmod's IT-style effects.</summary>
internal static class XmLoader
{
    const int Cho = 64;

    static uint GetXmFrequency(sbyte relnote, sbyte finetune)
    {
        double rn = relnote;
        double ft = finetune;
        double middleC = 8363.0f;
        var freq = middleC * Math.Pow(2.0, 1.0 / 12.0 * rn + 1.0 / (12.0 * 128.0) * ft);
        return (uint)(int)freq;
    }

    static int LoadXmInstrument(Instrument inst, MasModule mas, ref byte nextSample, CReader f)
    {
        int ns = nextSample;

        var instHeadstart = f.Tell();
        var instSize = (int)f.Read32();

        for (var x = 0; x < 22; x++)
            inst.Name[x] = f.Read8();
        f.Read8();  // instrument type, supposed to always be 0
        int nsamples = f.Read16();
        if (nsamples > 0)
        {
            var sampHeadsize = (int)f.Read32();
            // read sample map
            for (var x = 0; x < 96; x++)
                inst.Notemap[x + 12] = (ushort)(((f.Read8() + ns + 1) * 256) | (x + 12));
            for (var x = 0; x < 12; x++)
                inst.Notemap[x] = (ushort)((inst.Notemap[12] & 0xFF00) | x);
            for (var x = 96; x < 120; x++)
                inst.Notemap[x] = (ushort)((inst.Notemap[12] & 0xFF00) | x);
            for (var x = 0; x < 12; x++)
            {
                inst.EnvelopeVolume.NodeX[x] = f.Read16();
                inst.EnvelopeVolume.NodeY[x] = (byte)f.Read16();
            }
            for (var x = 0; x < 12; x++)
            {
                inst.EnvelopePan.NodeX[x] = f.Read16();
                inst.EnvelopePan.NodeY[x] = (byte)f.Read16();
            }
            inst.GlobalVolume = 128;
            inst.EnvelopeVolume.NodeCount = f.Read8();
            inst.EnvelopePan.NodeCount = f.Read8();
            inst.EnvelopeVolume.SusStart = inst.EnvelopeVolume.SusEnd = f.Read8();
            inst.EnvelopeVolume.LoopStart = f.Read8();
            inst.EnvelopeVolume.LoopEnd = f.Read8();
            inst.EnvelopePan.SusStart = inst.EnvelopePan.SusEnd = f.Read8();
            inst.EnvelopePan.LoopStart = f.Read8();
            inst.EnvelopePan.LoopEnd = f.Read8();
            var volbits = f.Read8();
            var panbits = f.Read8();
            inst.EnvFlags = 0;
            if ((volbits & 1) != 0)
                inst.EnvFlags |= 1 | 8;
            if ((panbits & 1) != 0)
                inst.EnvFlags |= 2;

            if ((volbits & 2) == 0)
                inst.EnvelopeVolume.SusStart = inst.EnvelopeVolume.SusEnd = 255;
            if ((panbits & 2) == 0)
                inst.EnvelopePan.SusStart = inst.EnvelopePan.SusEnd = 255;

            if ((volbits & 4) == 0)
                inst.EnvelopeVolume.LoopStart = inst.EnvelopeVolume.LoopEnd = 255;
            if ((panbits & 4) == 0)
                inst.EnvelopePan.LoopStart = inst.EnvelopePan.LoopEnd = 255;

            var vibtype = f.Read8();
            var vibsweep = (byte)(32768 / (f.Read8() + 1));
            var vibdepth = f.Read8();
            var vibrate = f.Read8();
            inst.Fadeout = (ushort)(f.Read16() / 32);
            f.Seek(instHeadstart + instSize, SeekOrigin.Begin);

            // read sample headers
            for (var x = 0; x < nsamples; x++)
            {
                if (ns + x >= 256)
                    return Mas.ErrTooManySamples;
                var sampHeadstart = f.Tell();
                var samp = mas.Samples![ns + x] = new Sample();
                samp.MslIndex = 0xFFFF;
                samp.SampleLength = f.Read32();
                samp.LoopStart = f.Read32();
                samp.LoopEnd = f.Read32() + samp.LoopStart;
                samp.DefaultVolume = f.Read8();
                samp.GlobalVolume = 64;

                samp.VibType = vibtype;
                samp.VibDepth = vibdepth;
                samp.VibSpeed = vibrate;
                samp.VibRate = vibsweep;

                var finetune = (sbyte)f.Read8();
                var loopbits = f.Read8();
                samp.DefaultPanning = (byte)((f.Read8() >> 1) | 128);
                var relnote = (sbyte)f.Read8();
                f.Read8();  // reserved

                for (var y = 0; y < 22; y++)
                {
                    samp.Name[y] = f.Read8();
                    if (y < 12)
                        samp.Filename[y] = samp.Name[y];
                }

                samp.Frequency = GetXmFrequency(relnote, finetune);

                samp.Format = (loopbits & 16) != 0 ? Mas.SampFormatU16 : Mas.SampFormatU8;
                if (samp.Is16Bit)
                {
                    samp.SampleLength /= 2;
                    samp.LoopStart /= 2;
                    samp.LoopEnd /= 2;
                }
                samp.LoopType = (byte)(loopbits & 3);
                f.Seek(sampHeadstart + sampHeadsize, SeekOrigin.Begin);
            }

            // read sample data (delta coded)
            for (var x = 0; x < nsamples; x++)
            {
                var samp = mas.Samples![ns + x];
                if (samp.SampleLength == 0)
                    continue;  // never fixed: a 16-bit empty sample stays 16-bit

                var sampleOld = 0;
                if (samp.Is16Bit)
                {
                    samp.Data16 = new ushort[samp.SampleLength];
                    for (uint t = 0; t < samp.SampleLength; t++)
                    {
                        sampleOld = (short)((short)f.Read16() + sampleOld);
                        samp.Data16[t] = (ushort)(sampleOld + 32768);
                    }
                }
                else
                {
                    samp.Data8 = new byte[samp.SampleLength];
                    for (uint t = 0; t < samp.SampleLength; t++)
                    {
                        sampleOld = (sbyte)((sbyte)f.Read8() + sampleOld);
                        samp.Data8[t] = (byte)(sampleOld + 128);
                    }
                }
                SampleFix.FixSample(samp);
            }
            nextSample = (byte)(ns + nsamples);
        }
        else
        {
            f.Seek(instHeadstart + instSize, SeekOrigin.Begin);
        }
        return Mas.ErrNone;
    }

    /// <summary>CONV_XM_EFFECT: an XM (and MOD) effect as the IT-style effect maxmod plays.</summary>
    public static void ConvXmEffect(ref byte fx, ref byte param)
    {
        int wfx = fx;
        int wpm = param;

        switch (wfx)
        {
            case 0:  // 0xy arpeggio
                if (wpm != 0)
                    wfx = 'J' - Cho;
                else
                    wfx = wpm = 0;
                break;
            case 1:  // 1xx porta up
                wfx = 'F' - Cho;
                if (wpm >= 0xE0)
                    wpm = 0xDF;
                break;
            case 2:  // 2xx porta down
                wfx = 'E' - Cho;
                if (wpm >= 0xE0)
                    wpm = 0xDF;
                break;
            case 3: wfx = 'G' - Cho; break;  // porta to note
            case 4: wfx = 'H' - Cho; break;  // vibrato
            case 5: wfx = 'L' - Cho; break;  // volslide+glissando
            case 6: wfx = 'K' - Cho; break;  // volslide+vibrato
            case 7: wfx = 'R' - Cho; break;  // tremolo
            case 8: wfx = 'X' - Cho; break;  // set panning
            case 9: wfx = 'O' - Cho; break;  // set offset
            case 0xA: wfx = 'D' - Cho; break;  // volume slide
            case 0xB: wfx = 'B' - Cho; break;  // position jump
            case 0xC: wfx = 27; break;  // set volume (compatibility effect)
            case 0xD:  // pattern break
                wfx = 'C' - Cho;
                wpm = (wpm & 0xF) + (wpm >> 4) * 10;
                break;
            case 0xE:  // Exy extended
                switch (wpm >> 4)
                {
                    case 1: wfx = 'F' - Cho; wpm = 0xF0 | (wpm & 0xF); break;  // fine porta up
                    case 2: wfx = 'E' - Cho; wpm = 0xF0 | (wpm & 0xF); break;  // fine porta down
                    case 3:  // glissando control
                    case 5:  // set finetune
                        wfx = 0;
                        wpm = 0;
                        break;
                    case 4: wfx = 'S' - Cho; wpm = 0x30 | (wpm & 0xF); break;  // vibrato control
                    case 6: wfx = 'S' - Cho; wpm = 0xB0 | (wpm & 0xF); break;  // pattern loop
                    case 7: wfx = 'S' - Cho; wpm = 0x40 | (wpm & 0xF); break;  // tremolo control
                    case 8: wfx = 'X' - Cho; wpm = (wpm & 0xF) * 16; break;  // set panning
                    case 9: wfx = 'S' - Cho; wpm = 0x20 | (wpm & 0xF); break;  // old retrig
                    case 10: wfx = 'S' - Cho; wpm = 0x00 | (wpm & 0xF); break;  // fine volslide up
                    case 11: wfx = 'S' - Cho; wpm = 0x10 | (wpm & 0xF); break;  // fine volslide down
                    case 12: wfx = 'S' - Cho; wpm = 0xC0 | (wpm & 0xF); break;  // note cut
                    case 13: wfx = 'S' - Cho; wpm = 0xD0 | (wpm & 0xF); break;  // note delay
                    case 14: wfx = 'S' - Cho; wpm = 0xE0 | (wpm & 0xF); break;  // pattern delay
                    case 15: wfx = 'S' - Cho; break;  // event
                    case 0:  // set filter
                        wfx = 0;
                        wpm = 0;
                        break;
                }
                break;
            case 0xF:  // Fxx set speed
                wfx = wpm >= 32 ? 'T' - Cho : 'A' - Cho;
                break;
            case 16: wfx = 'V' - Cho; break;  // Gxx set global volume
            case 17: wfx = 'W' - Cho; break;  // Hxx global volume slide
            case 18: case 19: case 22: case 23: case 24: case 26: case 28: case 30: case 31: case 32: case 34: case 35:
                wfx = 0;  // unused
                wpm = 0;
                break;
            case 20: wfx = 28; break;  // Kxx key off
            case 21: wfx = 29; break;  // Lxx set envelope position
            case 25: wfx = 'P' - Cho; break;  // Pxx panning slide
            case 27: wfx = 'Q' - Cho; break;  // Rxx retrigger note
            case 29: wfx = 30; break;  // Txx tremor
            case 33:  // Xxx extra fine slide
                if (wpm >> 4 == 1)
                {
                    wfx = 'F' - Cho;
                    wpm = 0xE0 | (wpm & 0xF);
                }
                else if (wpm >> 4 == 2)
                {
                    wfx = 'E' - Cho;
                    wpm = 0xE0 | (wpm & 0xF);
                }
                else
                {
                    wfx = 0;
                    wpm = 0;
                }
                break;
        }
        fx = (byte)wfx;
        param = (byte)wpm;
    }

    static int LoadXmPattern(Pattern patt, uint nchannels, CReader f)
    {
        var headstart = f.Tell();
        var headsize = f.Read32();

        if (f.Read8() != 0)
            return Mas.ErrUnknownPattern;  // mmutil then leaves the pattern uninitialised

        patt.NRows = f.Read16();
        var clength = f.Read16();
        if (patt.NRows > 256)
            throw new MaxmodStopException($"an XM pattern has {patt.NRows} rows; mmutil 1.10.1 keeps 256 and writes past them");

        for (var row = 0; row < patt.NRows * Mas.MaxChannels; row++)
        {
            patt.Data[row].Note = 250;
            patt.Data[row].Vol = 0;
        }

        f.Seek(headstart + headsize, SeekOrigin.Begin);

        if (clength == 0)
            return Mas.ErrNone;  // pattern is empty

        for (var row = 0; row < patt.NRows; row++)
        {
            for (uint col = 0; col < nchannels; col++)
            {
                var e = (uint)row * Mas.MaxChannels + col;
                if (e >= patt.Data.Length)
                    throw new MaxmodStopException($"an XM file has {nchannels} channels; mmutil 1.10.1 keeps 32 and writes past them");
                ref var d = ref patt.Data[e];
                var b = f.Read8();
                byte fx, param;
                if ((b & 128) != 0)  // packed
                {
                    if ((b & 1) != 0)  // note follows
                    {
                        d.Note = f.Read8();
                        if (d.Note == 97)
                            d.Note = 255;
                        else
                            d.Note += 12 - 1;
                    }
                    if ((b & 2) != 0)  // instrument follows
                        d.Inst = f.Read8();
                    if ((b & 4) != 0)  // volume column byte follows
                        d.Vol = f.Read8();
                    fx = (b & 8) != 0 ? f.Read8() : (byte)0;
                    param = (b & 16) != 0 ? f.Read8() : (byte)0;

                    if (fx != 0 || param != 0)
                    {
                        ConvXmEffect(ref fx, ref param);
                        d.Fx = fx;
                        d.Param = param;
                    }
                }
                else  // unpacked
                {
                    d.Note = b;
                    if (d.Note == 97)
                        d.Note = 255;
                    else
                        d.Note += 12 - 1;
                    d.Inst = f.Read8();
                    d.Vol = f.Read8();
                    fx = f.Read8();
                    param = f.Read8();
                    ConvXmEffect(ref fx, ref param);
                    d.Fx = fx;
                    d.Param = param;
                }
            }
        }
        return Mas.ErrNone;
    }

    public static int Load(MasModule mod, CReader f, MaxmodLog log)
    {
        mod.OldEffects = 1;
        mod.XmMode = 1;
        mod.GlobalVolume = 64;
        mod.OldMode = 0;

        if (f.Read32() != Mas.Sig("Exte") || f.Read32() != Mas.Sig("nded") || f.Read32() != Mas.Sig(" Mod") ||
            f.Read32() != Mas.Sig("ule:") || f.Read8() != ' ')
            return Mas.ErrInvalidModule;
        for (var x = 0; x < 20; x++)
            mod.Title[x] = f.Read8();
        if (f.Read8() != 0x1A)
            return Mas.ErrInvalidModule;
        f.Skip8(20);  // tracker name
        f.Read16();  // version
        var xmHeadsize = f.Read32();
        mod.OrderCount = (byte)f.Read16();
        mod.RestartPos = (byte)f.Read16();
        uint xmNchannels = f.Read16();
        mod.PattCount = (byte)f.Read16();
        mod.InstCount = (byte)f.Read16();
        mod.FreqMode = (byte)((f.Read16() & 1) != 0 ? 1 : 0);
        mod.InitialSpeed = (byte)f.Read16();
        mod.InitialTempo = (byte)f.Read16();

        for (var x = 0; x < 32; x++)
        {
            mod.ChannelVolume[x] = 64;
            mod.ChannelPanning[x] = 128;
        }
        int o;
        for (o = 0; o < 200; o++)  // read order table
        {
            if (o < mod.OrderCount)
            {
                mod.Orders[o] = f.Read8();
            }
            else
            {
                f.Read8();
                mod.Orders[o] = 255;
            }
        }
        for (; o < 256; o++)  // skip 200->255
            f.Read8();
        f.Seek((int)(60 + xmHeadsize), SeekOrigin.Begin);

        mod.Patterns = ModLoader.NewArray<Pattern>(mod.PattCount);
        for (var x = 0; x < mod.PattCount; x++)
            if (LoadXmPattern(mod.Patterns[x], xmNchannels, f) != Mas.ErrNone)
                log.Error($"pattern {x} is packed in a way mmutil 1.10.1 does not read; it then writes uninitialised memory");

        mod.Instruments = ModLoader.NewArray<Instrument>(mod.InstCount);
        mod.Samples = new Sample[256];
        byte nextSample = 0;

        for (var x = 0; x < mod.InstCount; x++)
            if (LoadXmInstrument(mod.Instruments[x], mod, ref nextSample, f) == Mas.ErrTooManySamples)
                log.Error("more than 256 samples; mmutil 1.10.1 stops reading that instrument and goes on from the wrong place");

        mod.SampCount = nextSample;
        for (var x = 0; x < mod.SampCount; x++)
            mod.Samples[x] ??= new Sample();
        return Mas.ErrNone;
    }
}
