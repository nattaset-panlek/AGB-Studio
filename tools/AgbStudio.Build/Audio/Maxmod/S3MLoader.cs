// Ported from Maxmod Utility (mmutil) 1.10.1, s3m.c.
// Copyright (c) 2008, Mukunda Johnson. BSD 3-clause licence: see LICENSE-mmutil.txt in this folder.

namespace AgbStudio.Build.Audio.Maxmod;

/// <summary>Scream Tracker 3 S3M loader.</summary>
internal static class S3MLoader
{
    static byte S3MNote(int a) => (byte)((a & 15) + (a >> 4) * 12 + 12);

    static int LoadS3MSampleData(Sample samp, byte ffi, CReader f)
    {
        if (samp.SampleLength == 0)
            return Mas.ErrNone;
        if (ffi != 1 && ffi != 2)
            throw new MaxmodStopException($"the sample format field is {ffi}; mmutil 1.10.1 then keeps uninitialised sample data");
        if (samp.Is16Bit)
            samp.Data16 = new ushort[samp.SampleLength];
        else
            samp.Data8 = new byte[samp.SampleLength];
        for (uint x = 0; x < samp.SampleLength; x++)
        {
            int a;
            if (samp.Is16Bit)
            {
                a = f.Read16();
                if (ffi == 1) a += 32768;  // signed samples [very old]
                samp.Data16![x] = (ushort)a;
            }
            else
            {
                a = f.Read8();
                if (ffi == 1) a += 128;
                samp.Data8![x] = (byte)a;
            }
        }
        SampleFix.FixSample(samp);
        return Mas.ErrNone;
    }

    static int LoadS3MSample(Sample samp, CReader f)
    {
        samp.MslIndex = 0xFFFF;
        if (f.Read8() != 1)  // type, 1 = sample
            return Mas.ErrNone;
        for (var x = 0; x < 12; x++)
            samp.Filename[x] = f.Read8();
        samp.DataPointer = (uint)(f.Read8() * 65536 + f.Read16()) * 16;
        samp.SampleLength = f.Read32();
        samp.LoopStart = f.Read32();
        samp.LoopEnd = f.Read32();
        samp.DefaultVolume = f.Read8();
        samp.GlobalVolume = 64;
        f.Read8();  // reserved
        if (f.Read8() != 0)  // packing, 0 = unpacked
            return Mas.ErrUnknownSample;
        var flags = f.Read8();
        samp.LoopType = (byte)((flags & 1) != 0 ? 1 : 0);
        if ((flags & 2) != 0)
            return Mas.ErrUnknownSample;
        samp.Format = (flags & 4) != 0 ? Mas.SampFormatU16 : Mas.SampFormatU8;
        samp.Frequency = f.Read32();
        f.Read32();  // reserved
        f.Skip8(8);  // internal variables
        for (var x = 0; x < 28; x++)
            samp.Name[x] = f.Read8();
        if (f.Read32() != Mas.Sig("SCRS"))
            return Mas.ErrUnknownSample;
        return Mas.ErrNone;
    }

    static void LoadS3MPattern(Pattern patt, CReader f)
    {
        int clength = f.Read16();
        patt.CLength = clength;
        patt.NRows = 64;

        for (var row = 0; row < 64 * Mas.MaxChannels; row++)
        {
            patt.Data[row].Note = 250;
            patt.Data[row].Vol = 255;
        }

        for (var row = 0; row < 64; row++)
        {
            byte what;
            while ((what = f.Read8()) != 0)  // 0 = end of row
            {
                var col = what & 31;
                ref var d = ref patt.Data[row * Mas.MaxChannels + col];

                if ((what & 32) != 0)  // note, instrument
                {
                    d.Note = f.Read8();
                    if (d.Note == 255)
                        d.Note = 250;
                    else if (d.Note == 254)
                        d.Note = 254;
                    else
                        d.Note = S3MNote(d.Note);
                    d.Inst = f.Read8();
                }

                if ((what & 64) != 0)  // volume
                    d.Vol = f.Read8();

                if ((what & 128) != 0)  // command, info
                {
                    d.Fx = f.Read8();
                    d.Param = f.Read8();
                    if (d.Fx == 3)  // convert pattern break to hexadecimal
                        d.Param = (byte)((d.Param & 0xF) + d.Param / 16 * 10);
                    if (d.Fx == 'X' - 64)
                        d.Param *= 2;  // multiply volume scale by 2
                    if (d.Fx == 'V' - 64)
                        d.Param *= 2;  // multiply volume scale by 2
                }
                if (d.Fx == 255)
                {
                    d.Fx = 0;
                    d.Param = 0;
                }
            }
        }
    }

    public static int Load(MasModule mod, CReader f, MaxmodLog log)
    {
        for (var x = 0; x < 28; x++)
            mod.Title[x] = f.Read8();  // song name

        f.Read8();  // 0x1A, not checked
        if (f.Read8() != 16)
            return Mas.ErrInvalidModule;

        f.Skip8(2);  // reserved space
        mod.OrderCount = (byte)f.Read16();
        mod.InstCount = (byte)f.Read16();
        mod.SampCount = mod.InstCount;
        mod.PattCount = (byte)f.Read16();

        for (var x = 0; x < 32; x++)
            mod.ChannelVolume[x] = 64;

        mod.FreqMode = 0;  // amiga frequencies
        mod.OldEffects = 1;
        mod.LinkGxx = 0;
        mod.RestartPos = 0;
        mod.OldMode = 1;

        f.Read16();  // flags
        f.Read16();  // cwt
        var ffi = f.Read16();
        if (f.Read32() != Mas.Sig("SCRM"))
            return Mas.ErrInvalidModule;
        mod.GlobalVolume = (byte)(f.Read8() * 2);
        mod.InitialSpeed = f.Read8();
        mod.InitialTempo = f.Read8();
        var stereo = f.Read8() >> 7;  // master volume
        f.Read8();  // ultra click removal
        var dp = f.Read8();  // default pan positions (when 252)
        f.Skip8(8 + 2);  // reserved space + special pointer
        for (var x = 0; x < 32; x++)
        {
            var chn = f.Read8();
            if (stereo != 0)
                mod.ChannelPanning[x] = (chn & 127) < 8 ? Simple.ClampU8(128 - Mas.PanningSep / 2) : Simple.ClampU8(128 + Mas.PanningSep / 2);
            else
                mod.ChannelPanning[x] = 128;
        }
        for (var x = 0; x < mod.OrderCount; x++)
            mod.Orders[x] = f.Read8();

        var parapInst = new ushort[mod.InstCount];
        var parapPatt = new ushort[mod.PattCount];
        for (var x = 0; x < mod.InstCount; x++)
            parapInst[x] = f.Read16();
        for (var x = 0; x < mod.PattCount; x++)
            parapPatt[x] = f.Read16();

        if (dp == 252)
        {
            for (var x = 0; x < 32; x++)
            {
                var a = f.Read8();
                if ((a & 32) != 0)
                    mod.ChannelPanning[x] = (byte)((a & 15) * 16 > 255 ? 255 : (a & 15) * 16);
            }
        }
        else
        {
            for (var x = 0; x < 32; x++)
            {
                if (stereo != 0)
                    mod.ChannelPanning[x] = (x & 1) != 0 ? Simple.ClampU8(128 - Mas.PanningSep / 2) : Simple.ClampU8(128 + Mas.PanningSep / 2);
                else
                    mod.ChannelPanning[x] = 128;
            }
        }

        mod.Instruments = ModLoader.NewArray<Instrument>(mod.InstCount);
        mod.Samples = ModLoader.NewArray<Sample>(mod.SampCount);
        mod.Patterns = ModLoader.NewArray<Pattern>(mod.PattCount);

        for (var x = 0; x < mod.InstCount; x++)
        {
            // create instrument for sample
            var inst = mod.Instruments[x];
            inst.GlobalVolume = 128;
            for (var y = 0; y < 120; y++)
                inst.Notemap[y] = (ushort)(y | ((x + 1) << 8));

            f.Seek(parapInst[x] * 16, SeekOrigin.Begin);
            if (LoadS3MSample(mod.Samples[x], f) != Mas.ErrNone)
            {
                log.Print("Error loading sample!\n");
                // mmutil returns here and leaves the other samples and all patterns uninitialised
                throw new MaxmodStopException($"sample {x + 1} is packed, stereo or has no SCRS mark; mmutil 1.10.1 then writes uninitialised memory");
            }
        }

        for (var x = 0; x < mod.PattCount; x++)
        {
            f.Seek(parapPatt[x] * 16, SeekOrigin.Begin);
            LoadS3MPattern(mod.Patterns[x], f);
        }

        for (var x = 0; x < mod.SampCount; x++)
        {
            f.Seek(mod.Samples[x].DataPointer, SeekOrigin.Begin);
            LoadS3MSampleData(mod.Samples[x], (byte)ffi, f);
        }
        return Mas.ErrNone;
    }
}
