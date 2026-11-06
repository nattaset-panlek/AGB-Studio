// Ported from Maxmod Utility (mmutil) 1.10.1, samplefix.c (GBA path).
// Copyright (c) 2008, Mukunda Johnson. BSD 3-clause licence: see LICENSE-mmutil.txt in this folder.

namespace AgbStudio.Build.Audio.Maxmod;

/// <summary>
/// FixSample for the GBA: 16-bit data becomes 8-bit, data after a loop is dropped, ping-pong loops are unrolled and
/// loops shorter than 512 samples are repeated. The NDS path (padding, ADPCM, resampling) is not ported.
/// </summary>
internal static class SampleFix
{
    const uint GbaMinLoopSize = 512;

    public static void FixSample(Sample samp)
    {
        // Clamp loop_start and loop_end (CLAMP with 0 on an unsigned value only clamps the top)
        if (samp.LoopStart > samp.SampleLength) samp.LoopStart = samp.SampleLength;
        if (samp.LoopEnd > samp.SampleLength) samp.LoopEnd = samp.SampleLength;
        FixSampleGba(samp);
    }

    static void FixSampleGba(Sample samp)
    {
        // convert to 8-bit if neccesary
        Sample8Bit(samp);

        // delete data after loop_end if loop exists
        if (samp.LoopType != 0)
            samp.SampleLength = samp.LoopEnd;

        // unroll BIDI loop
        if (samp.LoopType == 2)
            UnrollBidiSample(samp);

        if (samp.LoopType != 0)
        {
            var looplen = samp.LoopEnd - samp.LoopStart;
            if (looplen < GbaMinLoopSize)
            {
                // mmutil divides by the loop length here: a loop of length 0 crashes it
                if (looplen == 0)
                    throw new MaxmodStopException("a looped sample has a loop of length 0; mmutil 1.10.1 crashes on it (division by zero)");
                UnrollSampleLoop(samp, GbaMinLoopSize / looplen + 1);
            }
        }
    }

    static void Sample8Bit(Sample samp)
    {
        if (!samp.Is16Bit) return;
        var src = samp.Data16;
        var newdata = new byte[samp.SampleLength];
        for (uint t = 0; t < samp.SampleLength; t++)
            newdata[t] = (byte)(Src16(src, t) / 256);
        samp.Data8 = newdata;
        samp.Data16 = null;
        samp.Format = (byte)(samp.Format & ~Mas.SampF16Bit);
    }

    // On the GBA the data is always 8-bit by the time a loop is unrolled (Sample_8bit runs first).
    static void UnrollSampleLoop(Sample samp, uint count)
    {
        var looplen = samp.LoopEnd - samp.LoopStart;
        var newlen = samp.SampleLength + looplen * count;
        var src = samp.Data8;
        var newdata = new byte[newlen];
        for (uint x = 0; x < samp.SampleLength; x++)
            newdata[x] = Src8(src, x);
        for (uint x = 0; x < looplen * count; x++)
            newdata[samp.SampleLength + x] = Src8(src, samp.LoopStart + x % looplen);
        samp.Data8 = newdata;
        samp.LoopEnd += looplen * count;
        samp.SampleLength += looplen * count;
    }

    static void UnrollBidiSample(Sample samp)
    {
        var looplen = samp.LoopEnd - samp.LoopStart;
        var newlen = samp.SampleLength + looplen;
        var src = samp.Data8;
        var newdata = new byte[newlen];
        for (uint x = 0; x < samp.SampleLength; x++)
            newdata[x] = Src8(src, x);
        for (uint x = 0; x < looplen; x++)
            newdata[x + samp.SampleLength] = Src8(src, samp.LoopEnd - 1 - x);
        samp.Data8 = newdata;
        samp.LoopType = 1;
        samp.SampleLength += looplen;
        samp.LoopEnd += looplen;
    }

    static byte Src8(byte[]? data, uint i) =>
        data != null && i < data.Length ? data[i] : throw new MaxmodStopException("mmutil 1.10.1 reads outside the sample data here");

    static ushort Src16(ushort[]? data, uint i) =>
        data != null && i < data.Length ? data[i] : throw new MaxmodStopException("mmutil 1.10.1 reads outside the sample data here");
}
