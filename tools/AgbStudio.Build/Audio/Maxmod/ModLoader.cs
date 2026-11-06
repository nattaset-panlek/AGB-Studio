// Ported from Maxmod Utility (mmutil) 1.10.1, mod.c.
// Copyright (c) 2008, Mukunda Johnson. BSD 3-clause licence: see LICENSE-mmutil.txt in this folder.

namespace AgbStudio.Build.Audio.Maxmod;

/// <summary>ProTracker MOD loader (M.K., 1CHN..9CHN, 10CH..32CH).</summary>
internal static class ModLoader
{
    static void CreateModInstrument(Instrument inst, byte sample)
    {
        inst.GlobalVolume = 128;
        for (var x = 0; x < 120; x++)
            inst.Notemap[x] = (ushort)(x | ((sample + 1) << 8));
    }

    static void LoadModSampleData(Sample samp, CReader f)
    {
        if (samp.SampleLength > 0)
        {
            samp.Data8 = new byte[samp.SampleLength];
            for (uint t = 0; t < samp.SampleLength; t++)
                samp.Data8[t] = (byte)(f.Read8() + 128);  // unsign data
        }
        SampleFix.FixSample(samp);
    }

    static void LoadModPattern(Pattern patt, byte nchannels, ref byte instCount, CReader f)
    {
        patt.NRows = 64;  // MODs have fixed 64 rows per pattern
        for (var row = 0; row < 64 * Mas.MaxChannels; row++)
            patt.Data[row].Note = 250;

        for (var row = 0; row < 64; row++)
        {
            for (var col = 0; col < nchannels; col++)
            {
                var data1 = f.Read8();
                var data2 = f.Read8();
                var data3 = f.Read8();
                var data4 = f.Read8();

                var period = (ushort)((data1 & 0xF) * 256 + data2);
                var inst = (byte)((data1 & 0xF0) + (data3 >> 4));
                var effect = (byte)(data3 & 0xF);
                var param = data4;

                // fix parameter for certain MOD effects
                if (effect is 5 or 6 && (param & 0xF0) != 0)  // 5xy glis+slide, 6xy vib+slide: clear Y if X
                    param &= 0xF0;

                ref var p = ref patt.Data[row * Mas.MaxChannels + col];
                p.Inst = inst;
                XmLoader.ConvXmEffect(ref effect, ref param);
                p.Fx = effect;
                p.Param = param;

                // 0 = no note, otherwise the note from the amiga period (C99 round: halves away from zero)
                if (period != 0)
                    p.Note = (byte)((int)Math.Round(12.0 * Math.Log(856.0 / period) / Math.Log(2), MidpointRounding.AwayFromZero) + 37 + 11);
                if (instCount < inst + 1)
                {
                    instCount = (byte)(inst + 1);
                    if (instCount > 31)
                        instCount = 31;
                }
            }
        }
    }

    static void LoadModSample(Sample samp, CReader f)
    {
        samp.MslIndex = 0xFFFF;
        for (var x = 0; x < 22; x++)
            samp.Name[x] = f.Read8();
        for (var x = 0; x < 12; x++)  // copy to filename
            samp.Filename[x] = samp.Name[x];
        samp.SampleLength = (uint)(f.Read8() * 256 + f.Read8()) * 2;
        int finetune = f.Read8();
        if (finetune >= 8) finetune -= 16;

        samp.DefaultVolume = f.Read8();
        samp.LoopStart = (uint)(f.Read8() * 256 + f.Read8()) * 2;
        samp.LoopEnd = samp.LoopStart + (uint)(f.Read8() * 256 + f.Read8()) * 2;

        samp.Frequency = (uint)(int)(8363.0 * Math.Pow(2.0, finetune * (1.0 / 192.0)));

        samp.GlobalVolume = 64;
        if (samp.LoopEnd - samp.LoopStart <= 2)  // loop length <= 2: no loop
        {
            samp.LoopType = 0;
            samp.LoopStart = samp.LoopEnd = 0;
        }
        else
        {
            samp.LoopType = 1;
        }
    }

    public static int Load(MasModule mod, CReader f, MaxmodLog log)
    {
        var fileStart = f.Tell();
        f.Seek(0x438, SeekOrigin.Begin);
        var sig = f.Read32();
        uint modChannels;
        switch (sig)
        {
            case 0x4E484331: modChannels = 1; break;  // 1CHN
            case 0x4E484332: modChannels = 2; break;
            case 0x4E484333: modChannels = 3; break;
            case 0x2E4B2E4D:                          // M.K.
            case 0x4E484334: modChannels = 4; break;
            case 0x4E484335: modChannels = 5; break;
            case 0x4E484336: modChannels = 6; break;
            case 0x4E484337: modChannels = 7; break;
            case 0x4E484338: modChannels = 8; break;
            case 0x4E484339: modChannels = 9; break;
            default:
                if (sig >> 16 == 0x4843)  // **CH where ** = 10-32 channels
                {
                    modChannels = (uint)Atoi2((byte)(sig & 0xFF), (byte)((sig >> 8) & 0xFF));
                    if (modChannels > Mas.MaxChannels)
                        return Mas.ErrManyChannels;
                }
                else
                {
                    return Mas.ErrInvalidModule;
                }
                break;
        }

        f.Seek(fileStart, SeekOrigin.Begin);
        for (var x = 0; x < 20; x++)
            mod.Title[x] = f.Read8();

        for (var x = 0; x < Mas.MaxChannels; x++)
        {
            if ((x & 3) != 1 && (x & 3) != 2)
                mod.ChannelPanning[x] = Simple.ClampU8(128 - Mas.PanningSep / 2);
            else
                mod.ChannelPanning[x] = Simple.ClampU8(128 + Mas.PanningSep / 2);
            mod.ChannelVolume[x] = 64;
        }

        mod.FreqMode = 0;
        mod.GlobalVolume = 64;
        mod.InitialSpeed = 6;
        mod.InitialTempo = 125;
        mod.InstCount = 0;  // filled in by LoadModPattern
        mod.InstMode = 0;
        mod.Instruments = NewArray<Instrument>(31);
        mod.LinkGxx = 0;
        mod.OldEffects = 1;
        mod.RestartPos = 0;
        mod.SampCount = 0;
        mod.Samples = NewArray<Sample>(31);
        mod.Stereo = 1;
        mod.XmMode = 1;
        mod.OldMode = 1;

        for (var x = 0; x < 31; x++)
        {
            CreateModInstrument(mod.Instruments[x], (byte)x);
            LoadModSample(mod.Samples[x], f);
        }

        // read sequence
        mod.OrderCount = f.Read8();
        mod.RestartPos = f.Read8();
        if (mod.RestartPos >= 127)
            mod.RestartPos = 0;
        var npatterns = 0;
        for (var x = 0; x < 128; x++)
        {
            mod.Orders[x] = f.Read8();
            if (mod.Orders[x] >= npatterns)
                npatterns = mod.Orders[x] + 1;
        }

        f.Read32();  // M.K. etc
        mod.PattCount = (byte)npatterns;
        mod.Patterns = NewArray<Pattern>(mod.PattCount);

        var instCount = mod.InstCount;
        for (var x = 0; x < mod.PattCount; x++)
            LoadModPattern(mod.Patterns[x], (byte)modChannels, ref instCount, f);
        mod.InstCount = instCount;

        mod.SampCount = mod.InstCount;
        for (var x = 0; x < 31; x++)
            LoadModSampleData(mod.Samples[x], f);
        return Mas.ErrNone;
    }

    /// <summary>atoi of a two-character string.</summary>
    static int Atoi2(byte a, byte b)
    {
        byte[] s = [a, b];
        var i = 0;
        while (i < 2 && s[i] is (byte)' ' or (byte)'\t' or (byte)'\n' or (byte)'\v' or (byte)'\f' or (byte)'\r') i++;
        var neg = false;
        if (i < 2 && s[i] is (byte)'+' or (byte)'-')
        {
            neg = s[i] == '-';
            i++;
        }
        var v = 0;
        while (i < 2 && s[i] >= '0' && s[i] <= '9') v = v * 10 + (s[i++] - '0');
        return neg ? -v : v;
    }

    internal static T[] NewArray<T>(int n) where T : new()
    {
        var a = new T[n];
        for (var i = 0; i < n; i++) a[i] = new T();
        return a;
    }
}
