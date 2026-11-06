// Ported from Maxmod Utility (mmutil) 1.10.1, mas.c.
// Copyright (c) 2008, Mukunda Johnson. BSD 3-clause licence: see LICENSE-mmutil.txt in this folder.

namespace AgbStudio.Build.Audio.Maxmod;

/// <summary>Writes a module (MAS) and sample data in maxmod's format, GBA target.</summary>
internal static class MasWriter
{
    static int CalcEnvelopeSize(InstrumentEnvelope env) => env.NodeCount * 4 + 8;

    static int CalcInstrumentSize(Instrument instr)
    {
        var size = 12;
        if ((instr.EnvFlags & 1) != 0) size += CalcEnvelopeSize(instr.EnvelopeVolume);
        if ((instr.EnvFlags & 2) != 0) size += CalcEnvelopeSize(instr.EnvelopePan);
        if ((instr.EnvFlags & 4) != 0) size += CalcEnvelopeSize(instr.EnvelopePitch);
        return size;
    }

    static void WriteInstrumentEnvelope(CWriter w, InstrumentEnvelope env)
    {
        w.Write8((byte)(env.NodeCount * 4 + 8));
        w.Write8(env.LoopStart);
        w.Write8(env.LoopEnd);
        w.Write8(env.SusStart);
        w.Write8(env.SusEnd);
        w.Write8(env.NodeCount);
        w.Write8(env.EnvFilter);
        w.Write8(Mas.ByteSmasher);
        if (env.NodeCount <= 1) return;
        if (env.NodeCount > 25)
            throw new MaxmodStopException($"an envelope has {env.NodeCount} nodes; mmutil 1.10.1 keeps 25 and reads past them");
        for (var x = 0; x < env.NodeCount; x++)
        {
            int delta, range;
            int bas = env.NodeY[x];
            if (x != env.NodeCount - 1)
            {
                range = env.NodeX[x + 1] - env.NodeX[x];
                if (range > 511) range = 511;
                if (range < 1) range = 1;
                delta = ((env.NodeY[x + 1] - bas) * 512 + range / 2) / range;
                if (delta > 32767) delta = 32767;
                if (delta < -32768) delta = -32768;
                while (bas + ((delta * range) >> 9) > 64) delta--;
                while (bas + ((delta * range) >> 9) < 0) delta++;
            }
            else
            {
                range = 0;
                delta = 0;
            }
            w.Write16((ushort)delta);
            w.Write16((ushort)(bas | (range << 7)));
        }
    }

    static void WriteInstrument(CWriter w, Instrument inst, int masOffset)
    {
        w.Align32();
        inst.Parapointer = (uint)(w.Tell() - masOffset);
        w.Write8(inst.GlobalVolume);
        w.Write8((byte)inst.Fadeout);
        w.Write8(inst.RandomVolume);
        w.Write8(inst.Dct);
        w.Write8(inst.Nna);
        w.Write8(inst.EnvFlags);
        w.Write8(inst.SetPan);
        w.Write8(inst.Dca);

        var fullNotemap = false;
        var firstNotemapSamp = inst.Notemap[0] >> 8;
        for (var y = 0; y < 120; y++)
        {
            if ((inst.Notemap[y] & 0xFF) != y || inst.Notemap[y] >> 8 != firstNotemapSamp)
            {
                fullNotemap = true;
                break;
            }
        }

        if (fullNotemap)
            w.Write16((ushort)CalcInstrumentSize(inst));  // offset of the notemap
        else
            w.Write16((ushort)(0x8000 | firstNotemapSamp));  // single notemap entry

        w.Write16(0);  // reserved space

        if ((inst.EnvFlags & 1) != 0) WriteInstrumentEnvelope(w, inst.EnvelopeVolume);
        if ((inst.EnvFlags & 2) != 0) WriteInstrumentEnvelope(w, inst.EnvelopePan);
        if ((inst.EnvFlags & 4) != 0) WriteInstrumentEnvelope(w, inst.EnvelopePitch);

        if (fullNotemap)
            for (var y = 0; y < 120; y++)
                w.Write16(inst.Notemap[y]);
    }

    /// <summary>Write_SampleData, GBA branch: a 12-byte header, the 8-bit data and 4 bytes of loop padding.</summary>
    public static void WriteSampleData(CWriter w, Sample samp)
    {
        var sampleLength = samp.SampleLength;
        var sampleLooplen = samp.LoopEnd - samp.LoopStart;

        w.Write32(sampleLength);
        w.Write32(samp.LoopType != 0 ? sampleLooplen : 0xFFFFFFFF);
        w.Write8(Mas.SampFormatU8);
        w.Write8(Mas.ByteSmasher);
        w.Write16((ushort)((samp.Frequency * 1024 + 15768 / 2) / 15768));  // unsigned 32-bit arithmetic

        if (samp.Is16Bit)
        {
            // only a sample mmutil never fixed (length 0) is still 16-bit here
            for (uint x = 0; x < sampleLength; x++)
                w.Write16(Data16(samp, x));
            if (samp.LoopType != 0 && sampleLength >= samp.LoopStart + 2)
            {
                w.Write16(Data16(samp, samp.LoopStart));
                w.Write16(Data16(samp, samp.LoopStart + 1));
            }
            else
            {
                w.Write16(0);
                w.Write16(0);
            }
        }
        else
        {
            for (uint x = 0; x < sampleLength; x++)
                w.Write8(Data8(samp, x));
            if (samp.LoopType != 0 && sampleLength >= samp.LoopStart + 4)
            {
                w.Write8(Data8(samp, samp.LoopStart));
                w.Write8(Data8(samp, samp.LoopStart + 1));
                w.Write8(Data8(samp, samp.LoopStart + 2));
                w.Write8(Data8(samp, samp.LoopStart + 3));
            }
            else
            {
                for (var x = 0; x < 4; x++)
                    w.Write8(128);
            }
        }
    }

    public static byte Data8(Sample s, uint i) =>
        s.Data8 != null && i < s.Data8.Length ? s.Data8[i] : throw new MaxmodStopException("mmutil 1.10.1 reads outside the sample data here");

    public static ushort Data16(Sample s, uint i) =>
        s.Data16 != null && i < s.Data16.Length ? s.Data16[i] : throw new MaxmodStopException("mmutil 1.10.1 reads outside the sample data here");

    static void WriteSample(CWriter w, Sample samp, int masOffset)
    {
        w.Align32();
        samp.Parapointer = (uint)(w.Tell() - masOffset);
        w.Write8(samp.DefaultVolume);
        w.Write8(samp.DefaultPanning);
        w.Write16((ushort)(samp.Frequency / 4));
        w.Write8(samp.VibType);
        w.Write8(samp.VibDepth);
        w.Write8(samp.VibSpeed);
        w.Write8(samp.GlobalVolume);
        w.Write16(samp.VibRate);
        w.Write16(samp.MslIndex);
        if (samp.MslIndex == 0xFFFF)
            WriteSampleData(w, samp);
    }

    static void WritePattern(CWriter w, Pattern patt, bool xmVol, int masOffset)
    {
        var lastMask = new ushort[Mas.MaxChannels];
        var lastNote = new ushort[Mas.MaxChannels];
        var lastInst = new ushort[Mas.MaxChannels];
        var lastVol = new ushort[Mas.MaxChannels];
        var lastFx = new ushort[Mas.MaxChannels];
        var lastParam = new ushort[Mas.MaxChannels];

        patt.Parapointer = (uint)(w.Tell() - masOffset);
        w.Write8((byte)(patt.NRows - 1));

        patt.CMarks[0] = true;
        var emptyvol = (byte)(xmVol ? 0 : 255);

        // using IT pattern compression
        for (var row = 0; row < patt.NRows; row++)
        {
            if (patt.CMarks[row])
            {
                for (var col = 0; col < Mas.MaxChannels; col++)
                {
                    // row is marked, clear previous data
                    lastMask[col] = 256;
                    lastNote[col] = 256;
                    lastInst[col] = 256;
                    lastVol[col] = 256;
                    lastFx[col] = 256;
                    lastParam[col] = 256;
                }
            }
            for (var col = 0; col < Mas.MaxChannels; col++)
            {
                var pe = patt.Data[row * Mas.MaxChannels + col];
                if (pe.Note == 250 && pe.Inst == 0 && pe.Vol == emptyvol && pe.Fx == 0 && pe.Param == 0)
                    continue;

                byte maskvar = 0;
                var chanvar = (byte)(col + 1);
                if (pe.Note != 250)
                    maskvar |= 1 | 16;
                if (pe.Inst != 0)
                    maskvar |= 2 | 32;
                if (pe.Note > 250)  // noteoff/cut disabled start+reset
                    maskvar &= unchecked((byte)~(16 | 32));
                if (pe.Vol != emptyvol)
                    maskvar |= 4 | 64;
                if (pe.Fx != 0 || pe.Param != 0)
                    maskvar |= 8 | 128;

                if ((maskvar & 1) != 0)
                {
                    if (pe.Note == lastNote[col])
                    {
                        maskvar &= unchecked((byte)~1);
                    }
                    else
                    {
                        lastNote[col] = pe.Note;
                        if (lastNote[col] == 254 || lastNote[col] == 255)  // noteoff / notecut do not reuse parameters
                            lastNote[col] = 256;
                    }
                }

                if ((maskvar & 2) != 0)
                {
                    if (pe.Inst == lastInst[col]) maskvar &= unchecked((byte)~2);
                    else lastInst[col] = pe.Inst;
                }

                if ((maskvar & 4) != 0)
                {
                    if (pe.Vol == lastVol[col]) maskvar &= unchecked((byte)~4);
                    else lastVol[col] = pe.Vol;
                }

                if ((maskvar & 8) != 0)
                {
                    if (pe.Fx == lastFx[col] && pe.Param == lastParam[col])
                    {
                        maskvar &= unchecked((byte)~8);
                    }
                    else
                    {
                        lastFx[col] = pe.Fx;
                        lastParam[col] = pe.Param;
                    }
                }

                if (maskvar != lastMask[col])
                {
                    chanvar |= 128;
                    lastMask[col] = maskvar;
                }

                w.Write8(chanvar);
                if ((chanvar & 128) != 0) w.Write8(maskvar);
                if ((maskvar & 1) != 0) w.Write8(pe.Note);
                if ((maskvar & 2) != 0) w.Write8(pe.Inst);
                if ((maskvar & 4) != 0) w.Write8(pe.Vol);
                if ((maskvar & 8) != 0)
                {
                    w.Write8(pe.Fx);
                    w.Write8(pe.Param);
                }
            }
            w.Write8(0);
        }
    }

    static byte OrderAt(MasModule mod, int order) =>
        order < mod.Orders.Length ? mod.Orders[order] : throw new MaxmodStopException("mmutil 1.10.1 reads past the order table here");

    static void MarkPatternRow(MasModule mod, int order, int row)
    {
        if (row >= 256)
            return;
        if (OrderAt(mod, order) == 255)
            order = 0;
        while (OrderAt(mod, order) >= 254)
        {
            if (OrderAt(mod, order) == 255)
                return;
            if (OrderAt(mod, order) == 254)
                order++;
        }
        var p = mod.Orders[order];
        // mmutil does not check the pattern number here; a mark outside the patterns changes nothing it writes
        if (mod.Patterns != null && p < mod.Patterns.Length)
            mod.Patterns[p].CMarks[row] = true;
    }

    static void MarkPatterns(MasModule mod)
    {
        for (var o = 0; o < mod.OrderCount; o++)
        {
            int p = OrderAt(mod, o);
            if (p == 255) break;
            if (p == 254) continue;
            if (p >= mod.PattCount) continue;
            var patt = mod.Patterns![p];
            for (var row = 0; row < patt.NRows; row++)
            {
                for (var col = 0; col < Mas.MaxChannels; col++)
                {
                    var pe = patt.Data[row * Mas.MaxChannels + col];
                    if (pe.Fx == 3)  // pattern break
                    {
                        if (pe.Param != 0)  // if param != 0 then mark next pattern
                            MarkPatternRow(mod, o + 1, pe.Param);
                    }
                    else if (pe.Fx == 19)
                    {
                        if (pe.Param == 0xB0)
                            MarkPatternRow(mod, o, row);
                    }
                }
            }
        }
    }

    /// <summary>Write_MAS with msl_dep: the module goes into the soundbank, its samples are referenced by index.</summary>
    public static int WriteMas(CWriter w, MasModule mod, bool mslDep, MaxmodLog log)
    {
        w.Write32(Mas.ByteSmasher);
        w.Write8(Mas.TypeSong);
        w.Write8(Mas.Version);
        w.Write8(Mas.ByteSmasher);
        w.Write8(Mas.ByteSmasher);

        var masOffset = w.Tell();

        w.Write8((byte)mod.OrderCount);
        w.Write8(mod.InstCount);
        w.Write8(mod.SampCount);
        w.Write8(mod.PattCount);
        w.Write8((byte)((mod.LinkGxx != 0 ? 1 : 0) | (mod.OldEffects != 0 ? 2 : 0) | (mod.FreqMode != 0 ? 4 : 0) |
                        (mod.XmMode != 0 ? 8 : 0) | (mslDep ? 16 : 0) | (mod.OldMode != 0 ? 32 : 0)));
        w.Write8(mod.GlobalVolume);
        w.Write8(mod.InitialSpeed);
        w.Write8(mod.InitialTempo);
        w.Write8(mod.RestartPos);

        w.Write8(Mas.ByteSmasher);
        w.Write8(Mas.ByteSmasher);
        w.Write8(Mas.ByteSmasher);
        for (var x = 0; x < Mas.MaxChannels; x++)
            w.Write8(mod.ChannelVolume[x]);
        for (var x = 0; x < Mas.MaxChannels; x++)
            w.Write8(mod.ChannelPanning[x]);
        int o;
        for (o = 0; o < mod.OrderCount; o++)
        {
            var order = OrderAt(mod, o);
            if (order < 254)
                w.Write8(order < mod.PattCount ? order : (byte)254);
            else
                w.Write8(order);
        }
        for (; o < 200; o++)
            w.Write8(255);

        // reserve space for offsets
        var fposPointer = w.Tell();
        for (var x = 0; x < mod.InstCount * 4 + mod.SampCount * 4 + mod.PattCount * 4; x++)
            w.Write8(Mas.ByteSmasher);

        for (var x = 0; x < mod.InstCount; x++)
            WriteInstrument(w, mod.Instruments![x], masOffset);

        for (var x = 0; x < mod.SampCount; x++)
            WriteSample(w, mod.Samples![x], masOffset);

        MarkPatterns(mod);
        for (var x = 0; x < mod.PattCount; x++)
            WritePattern(w, mod.Patterns![x], mod.XmMode != 0, masOffset);
        w.Align32();

        var fileSize = w.Tell() - masOffset;
        w.SeekSet(masOffset - 8);
        w.Write32((uint)fileSize);
        w.SeekSet(fposPointer);
        for (var x = 0; x < mod.InstCount; x++)
            w.Write32(mod.Instruments![x].Parapointer);
        for (var x = 0; x < mod.SampCount; x++)
        {
            var s = mod.Samples![x];
            // mmutil 1.10.1 prints this line for every sample, verbose or not
            log.Print($"sample {Mas.CString(s.Name)} is at {(int)s.Parapointer}/{w.Tell()} of {(int)s.SampleLength}\n");
            w.Write32(s.Parapointer);
        }
        for (var x = 0; x < mod.PattCount; x++)
            w.Write32(mod.Patterns![x].Parapointer);
        return fileSize;
    }
}
