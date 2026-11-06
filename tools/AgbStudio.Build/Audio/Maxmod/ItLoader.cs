// Ported from Maxmod Utility (mmutil) 1.10.1, it.c.
// Copyright (c) 2008, Mukunda Johnson. BSD 3-clause licence: see LICENSE-mmutil.txt in this folder.
// mmutil's IT sample decompression is based on ChibiTracker's code, which is based on xmp's, which is based on OpenCP's.

namespace AgbStudio.Build.Audio.Maxmod;

/// <summary>Impulse Tracker IT loader, including IT 2.14 / 2.15 compressed samples.</summary>
internal static class ItLoader
{
    static void LoadItEnvelope(InstrumentEnvelope env, bool unsign, CReader f)
    {
        var envLoop = false;
        var envSus = false;
        var envEnabled = false;

        var a = f.Read8();
        if ((a & 1) != 0)
            envEnabled = true;
        if ((a & 2) == 0)
        {
            env.LoopStart = 255;
            env.LoopEnd = 255;
        }
        else
        {
            envLoop = true;
        }
        if ((a & 4) == 0)
        {
            env.SusStart = 255;
            env.SusEnd = 255;
        }
        else
        {
            envSus = true;
        }

        if ((a & 128) != 0)
        {
            unsign = false;
            env.EnvFilter = 1;
        }

        var nodeCount = f.Read8();
        if (nodeCount != 0)
            env.EnvValid = 1;

        env.NodeCount = nodeCount;
        if (envLoop)
        {
            env.LoopStart = f.Read8();
            env.LoopEnd = f.Read8();
        }
        else
        {
            f.Skip8(2);
        }
        if (envSus)
        {
            env.SusStart = f.Read8();
            env.SusEnd = f.Read8();
        }
        else
        {
            f.Skip8(2);
        }
        for (var x = 0; x < 25; x++)
        {
            env.NodeY[x] = f.Read8();
            if (unsign)
                env.NodeY[x] += 32;
            env.NodeX[x] = f.Read16();
        }
        f.Read8();  // unused byte
        env.EnvEnabled = (byte)(envEnabled ? 1 : 0);
    }

    static void LoadItInstrument(Instrument inst, CReader f)
    {
        f.Skip8(17);
        inst.Nna = f.Read8();
        inst.Dct = f.Read8();
        inst.Dca = f.Read8();
        var a = f.Read16();
        if (a > 255) a = 255;
        inst.Fadeout = (byte)a;
        f.Skip8(2);
        inst.GlobalVolume = f.Read8();
        a = f.Read8();
        a = (ushort)((a & 128) | ((a & 127) * 2 > 127 ? 127 : (a & 127) * 2));
        inst.SetPan = (byte)(a ^ 128);
        inst.RandomVolume = f.Read8();
        f.Skip8(5);
        for (var x = 0; x < 26; x++)
            inst.Name[x] = f.Read8();
        f.Skip8(6);

        for (var x = 0; x < 120; x++)
            inst.Notemap[x] = f.Read16();

        inst.EnvFlags = 0;

        LoadItEnvelope(inst.EnvelopeVolume, false, f);
        inst.EnvFlags |= (byte)(inst.EnvelopeVolume.EnvValid != 0 ? 1 : 0);
        inst.EnvFlags |= (byte)(inst.EnvelopeVolume.EnvEnabled != 0 ? 8 : 0);

        LoadItEnvelope(inst.EnvelopePan, true, f);
        inst.EnvFlags |= (byte)(inst.EnvelopePan.EnvEnabled != 0 ? 2 : 0);

        LoadItEnvelope(inst.EnvelopePitch, true, f);
        inst.EnvFlags |= (byte)(inst.EnvelopePitch.EnvEnabled != 0 ? 4 : 0);

        f.Skip8(7);
    }

    static void CreateItInstrument(Instrument inst, int sample)
    {
        inst.GlobalVolume = 128;
        for (var x = 0; x < 120; x++)
            inst.Notemap[x] = (ushort)(x + sample * 256);
    }

    static int LoadItSample(Sample samp, CReader f)
    {
        samp.MslIndex = 0xFFFF;

        if (f.Read32() != Mas.Sig("IMPS"))
            return Mas.ErrUnknownSample;
        for (var x = 0; x < 12; x++)  // dos filename
            samp.Filename[x] = f.Read8();
        if (f.Read8() != 0)
            return Mas.ErrUnknownSample;
        samp.GlobalVolume = f.Read8();
        var a = f.Read8();
        samp.ItCompression = (byte)((a & 8) != 0 ? 1 : 0);
        var bit16 = (a & 2) != 0;
        var hasloop = (a & 16) != 0;
        var pingpong = (a & 64) != 0;
        samp.DefaultVolume = f.Read8();
        for (var x = 0; x < 26; x++)
            samp.Name[x] = f.Read8();
        a = f.Read8();
        int dfp = f.Read8();
        samp.DefaultPanning = (byte)(((dfp & 127) == 64 ? 127 : dfp << 1) | (dfp & 128));
        var sampUnsigned = (a & 1) == 0;
        var sampLength = f.Read32();
        var loopStart = f.Read32();
        var loopEnd = f.Read32();
        var c5spd = f.Read32();

        samp.Frequency = c5spd;
        samp.SampleLength = sampLength;
        samp.LoopStart = loopStart;
        samp.LoopEnd = loopEnd;

        f.Skip8(8);  // susloop start/end
        var dataAddress = f.Read32();
        samp.VibSpeed = f.Read8();
        samp.VibDepth = f.Read8();
        samp.VibRate = f.Read8();
        samp.VibType = f.Read8();
        samp.DataPointer = dataAddress;
        if (hasloop)
        {
            samp.LoopType = (byte)(pingpong ? 2 : 1);
            samp.LoopStart = loopStart;
            samp.LoopEnd = loopEnd;
        }
        else
        {
            samp.LoopType = 0;
        }
        samp.Format = (byte)((bit16 ? Mas.SampF16Bit : 0) | (sampUnsigned ? 0 : Mas.SampFSigned));
        if (samp.SampleLength == 0) samp.LoopType = 0;
        return Mas.ErrNone;
    }

    static void LoadItSampleData(Sample samp, ushort cmwt, CReader f, MaxmodLog log)
    {
        if (samp.SampleLength == 0) return;
        if (samp.Is16Bit)
            samp.Data16 = new ushort[samp.SampleLength];
        else
            samp.Data8 = new byte[samp.SampleLength];

        if (samp.ItCompression == 0)
        {
            var signed = (samp.Format & Mas.SampFSigned) != 0;
            for (uint x = 0; x < samp.SampleLength; x++)
            {
                if (samp.Is16Bit)
                    samp.Data16![x] = signed ? (ushort)((short)f.Read16() + 32768) : f.Read16();
                else
                    samp.Data8![x] = signed ? (byte)((sbyte)f.Read8() + 128) : f.Read8();
            }
        }
        else if (LoadItSampleCmp(samp, (int)samp.SampleLength, cmwt, samp.Is16Bit, f) != Mas.ErrNone)
        {
            if (samp.Is16Bit)
                throw new MaxmodStopException("a compressed 16-bit sample has an illegal bit width; mmutil 1.10.1 then keeps uninitialised sample data");
            log.Warning("a compressed sample has an illegal bit width; the rest of it is silence (as in mmutil 1.10.1)");
        }
        SampleFix.FixSample(samp);
    }

    static void EmptyItPattern(Pattern patt)
    {
        patt.NRows = 64;
        for (var x = 0; x < patt.NRows * Mas.MaxChannels; x++)
        {
            patt.Data[x].Note = 250;  // special clears for vol&note
            patt.Data[x].Vol = 255;
        }
    }

    static int LoadItPattern(Pattern patt, CReader f)
    {
        // mmutil leaves these uninitialised; a valid file sets a channel's mask before using it
        var oldMaskvar = new byte[Mas.MaxChannels];
        var oldNote = new byte[Mas.MaxChannels];
        var oldInst = new byte[Mas.MaxChannels];
        var oldVol = new byte[Mas.MaxChannels];
        var oldFx = new byte[Mas.MaxChannels];
        var oldParam = new byte[Mas.MaxChannels];

        int clength = f.Read16();
        patt.NRows = f.Read16();
        f.Skip8(4);
        if (patt.NRows > 256)
            throw new MaxmodStopException($"an IT pattern has {patt.NRows} rows; mmutil 1.10.1 keeps 256 and writes past them");

        patt.CLength = clength;

        for (var x = 0; x < patt.NRows * Mas.MaxChannels; x++)
        {
            patt.Data[x].Note = 250;  // special clears for vol&note
            patt.Data[x].Vol = 255;
        }

        for (var x = 0; x < patt.NRows; x++)
        {
            while (true)
            {
                var chanvar = f.Read8();
                if (chanvar == 0)  // end of row
                    break;

                var chan = (byte)((chanvar - 1) & 63);
                if (chan >= Mas.MaxChannels)
                    return Mas.ErrManyChannels;

                if ((chanvar & 128) != 0)
                    oldMaskvar[chan] = f.Read8();

                var maskvar = oldMaskvar[chan];
                ref var d = ref patt.Data[x * Mas.MaxChannels + chan];

                if ((maskvar & 1) != 0)
                {
                    oldNote[chan] = f.Read8();
                    d.Note = oldNote[chan];
                }
                if ((maskvar & 2) != 0)
                {
                    oldInst[chan] = f.Read8();
                    d.Inst = oldInst[chan];
                }
                if ((maskvar & 4) != 0)
                {
                    oldVol[chan] = f.Read8();
                    d.Vol = oldVol[chan];
                }
                if ((maskvar & 8) != 0)
                {
                    oldFx[chan] = f.Read8();
                    d.Fx = oldFx[chan];
                    oldParam[chan] = f.Read8();
                    d.Param = oldParam[chan];
                }
                if ((maskvar & 16) != 0)
                    d.Note = oldNote[chan];
                if ((maskvar & 32) != 0)
                    d.Inst = oldInst[chan];
                if ((maskvar & 64) != 0)
                    d.Vol = oldVol[chan];
                if ((maskvar & 128) != 0)
                {
                    d.Fx = oldFx[chan];
                    d.Param = oldParam[chan];
                }
            }
        }
        return Mas.ErrNone;
    }

    public static int Load(MasModule itm, CReader f, MaxmodLog log)
    {
        if (f.Read32() != Mas.Sig("IMPM"))
            return Mas.ErrInvalidModule;
        for (var x = 0; x < 28; x++)
            itm.Title[x] = f.Read8();
        itm.OrderCount = f.Read16();
        itm.InstCount = (byte)f.Read16();
        itm.SampCount = (byte)f.Read16();
        itm.PattCount = (byte)f.Read16();
        f.Read16();  // cwt
        var cmwt = f.Read16();  // upward compatible
        var w = f.Read16();  // flags
        itm.Stereo = (byte)(w & 1);
        itm.InstMode = (byte)(w & 4);
        var instrMode = itm.InstMode != 0;
        itm.FreqMode = (byte)(w & 8);
        itm.OldEffects = (byte)(w & 16);
        itm.LinkGxx = (byte)(w & 32);
        f.Skip8(2);  // special
        itm.GlobalVolume = f.Read8();
        f.Skip8(1);  // mix volume
        itm.InitialSpeed = f.Read8();
        itm.InitialTempo = f.Read8();

        f.Skip8(12);  // SEP, PWD, MSGLENGTH, MESSAGE OFFSET, [RESERVED]
        for (var x = 0; x < 64; x++)
        {
            var b = f.Read8();
            if (x < Mas.MaxChannels)
                itm.ChannelPanning[x] = (byte)(b * 4 > 255 ? 255 : b * 4);  // map 0->64 to 0->255
        }
        for (var x = 0; x < 64; x++)
        {
            var b = f.Read8();
            if (x < Mas.MaxChannels)
                itm.ChannelVolume[x] = b;
        }
        if (itm.OrderCount > 256)
            throw new MaxmodStopException($"the order list has {itm.OrderCount} entries; mmutil 1.10.1 keeps 256 and writes past them");
        for (var x = 0; x < itm.OrderCount; x++)
            itm.Orders[x] = f.Read8();

        var parapInst = new uint[itm.InstCount];
        var parapSamp = new uint[itm.SampCount];
        var parapPatt = new uint[itm.PattCount];
        for (var x = 0; x < itm.InstCount; x++)
            parapInst[x] = f.Read32();
        for (var x = 0; x < itm.SampCount; x++)
            parapSamp[x] = f.Read32();
        for (var x = 0; x < itm.PattCount; x++)
            parapPatt[x] = f.Read32();

        itm.Samples = ModLoader.NewArray<Sample>(itm.SampCount);
        itm.Patterns = ModLoader.NewArray<Pattern>(itm.PattCount);

        if (instrMode)
        {
            itm.Instruments = ModLoader.NewArray<Instrument>(itm.InstCount);
            for (var x = 0; x < itm.InstCount; x++)
            {
                f.Seek((int)parapInst[x], SeekOrigin.Begin);
                LoadItInstrument(itm.Instruments[x], f);
            }
        }

        for (var x = 0; x < itm.SampCount; x++)
        {
            f.Seek((int)parapSamp[x], SeekOrigin.Begin);
            if (LoadItSample(itm.Samples[x], f) != Mas.ErrNone)
                log.Error($"sample {x + 1} has no IMPS header (mmutil 1.10.1 does not stop and adds an empty sample)");
        }

        if (!instrMode)
        {
            itm.InstCount = itm.SampCount;
            itm.Instruments = ModLoader.NewArray<Instrument>(itm.InstCount);
            for (var x = 0; x < itm.SampCount; x++)
                CreateItInstrument(itm.Instruments[x], x + 1);
        }

        for (var x = 0; x < itm.PattCount; x++)
        {
            f.Seek((int)parapPatt[x], SeekOrigin.Begin);
            if (parapPatt[x] != 0)
            {
                if (LoadItPattern(itm.Patterns[x], f) == Mas.ErrManyChannels)
                    log.Warning($"pattern {x} uses a channel above 32; mmutil 1.10.1 drops the rest of that pattern");
            }
            else
            {
                EmptyItPattern(itm.Patterns[x]);
            }
        }

        for (var x = 0; x < itm.SampCount; x++)
        {
            f.Seek((int)itm.Samples[x].DataPointer, SeekOrigin.Begin);
            LoadItSampleData(itm.Samples[x], cmwt, f, log);
        }
        return Mas.ErrNone;
    }

    static byte[] LoadItCompressedSampleBlock(CReader f)
    {
        uint size = f.Read16();
        var buffer = new byte[size + 4];  // the last 4 bytes stay 0
        for (uint x = 0; x < size; x++)
            buffer[x] = f.Read8();
        return buffer;
    }

    /// <summary>Load_IT_Sample_CMP: decompresses IT 2.14 (or 2.15 when cmwt is 0x215) sample data into the sample.</summary>
    static int LoadItSampleCmp(Sample samp, int sampLen, ushort cmwt, bool bit16, CReader f)
    {
        var nbits = bit16 ? 16u : 8u;
        var dsize = bit16 ? 4u : 3u;
        // mmutil sets the first samp_len BYTES to 128, also for 16-bit data
        if (bit16)
        {
            for (var i = 0; i < sampLen; i++)
                samp.Data16![i / 2] |= (ushort)(128 << (i % 2 * 8));
        }
        else
        {
            for (var i = 0; i < sampLen; i++)
                samp.Data8![i] = 128;
        }

        var it215 = cmwt == 0x215;
        var dest = 0;

        while (sampLen != 0)
        {
            // read a new block of compressed data and reset variables
            var cBuffer = LoadItCompressedSampleBlock(f);
            uint bitReadpos = 0;
            ushort blockLength = bit16 ? (ushort)(sampLen < 0x4000 ? sampLen : 0x4000) : (ushort)(sampLen < 0x8000 ? sampLen : 0x8000);
            ushort blockPosition = 0;
            var bitWidth = (byte)(nbits + 1);
            short d1 = 0, d2 = 0;
            sbyte d18 = 0, d28 = 0;

            while (blockPosition < blockLength)
            {
                var auxValue = Simple.ReadBits(cBuffer, bitReadpos, bitWidth);
                bitReadpos += bitWidth;

                if (bitWidth < 7)
                {
                    // method 1 (1-6 bits): check for "100..."; shifts by -1 behave as on x86 (count & 31)
                    var escape = bit16 ? (int)auxValue == 1 << (bitWidth - 1) : auxValue == 1u << (bitWidth - 1);
                    if (escape)
                    {
                        auxValue = Simple.ReadBits(cBuffer, bitReadpos, dsize) + 1;  // read new width
                        bitReadpos += dsize;
                        bitWidth = (byte)(auxValue < bitWidth ? auxValue : auxValue + 1);
                        continue;
                    }
                }
                else if (bitWidth < nbits + 1)
                {
                    // method 2 (7-8 bits, 7-16 for 16-bit)
                    if (bit16)
                    {
                        var border = (ushort)((0xFFFF >> (int)(nbits + 1 - bitWidth)) - nbits / 2);
                        if ((int)auxValue > border && (uint)(int)auxValue <= border + nbits)
                        {
                            auxValue -= border;  // convert width to 1-8
                            bitWidth = (byte)(auxValue < bitWidth ? auxValue : auxValue + 1);
                            continue;
                        }
                    }
                    else
                    {
                        var border = (ushort)((0xFF >> (int)(nbits + 1 - bitWidth)) - nbits / 2);
                        if (auxValue > border && auxValue <= border + nbits)
                        {
                            auxValue -= border;
                            bitWidth = (byte)(auxValue < bitWidth ? auxValue : auxValue + 1);
                            continue;
                        }
                    }
                }
                else if (bitWidth == nbits + 1)
                {
                    // method 3 (9 bits, 17 for 16-bit)
                    if ((auxValue & (1u << (int)nbits)) != 0)
                    {
                        bitWidth = (byte)((auxValue + 1) & 0xFF);  // new width
                        continue;
                    }
                }
                else
                {
                    return Mas.ErrUnknownSample;  // illegal width, abort
                }

                // now expand value to signed
                short v16 = 0;
                sbyte v8 = 0;
                if (bitWidth < nbits)
                {
                    var tmpShift = (int)(nbits - bitWidth);
                    if (bit16)
                        v16 = (short)((short)(auxValue << tmpShift) >> tmpShift);
                    else
                        v8 = (sbyte)((sbyte)(auxValue << tmpShift) >> tmpShift);
                }
                else
                {
                    if (bit16)
                        v16 = (short)auxValue;
                    else
                        v8 = (sbyte)auxValue;
                }

                if (bit16)
                {
                    // integrate upon the sample values
                    d1 += v16;
                    d2 += d1;
                    samp.Data16![dest++] = (ushort)(it215 ? d2 + 32768 : d1 + 32768);
                }
                else
                {
                    d18 += v8;
                    d28 += d18;
                    samp.Data8![dest++] = (byte)(it215 ? d28 + 128 : d18 + 128);
                }
                blockPosition++;
            }

            sampLen -= blockLength;
        }
        return Mas.ErrNone;
    }
}
