// Ported from Maxmod Utility (mmutil) 1.10.1, wav.c.
// Copyright (c) 2008, Mukunda Johnson. BSD 3-clause licence: see LICENSE-mmutil.txt in this folder.

namespace AgbStudio.Build.Audio.Maxmod;

/// <summary>WAV loader: 8 or 16-bit PCM, channels averaged to mono, loop from a 'smpl' chunk.</summary>
internal static class WavLoader
{
    public const int Ok = 0x00;
    public const int Corrupt = 0x01;
    public const int UnknownComp = 0x11;
    public const int UnsupportedBd = 0x13;

    public static string ErrorText(int err) => err switch
    {
        Corrupt => "WAV file has no 'fmt ' chunk before its 'data' chunk, or no 'data' chunk",
        UnknownComp => "WAV file is not PCM",
        UnsupportedBd => "WAV file is neither 8-bit nor 16-bit",
        _ => $"WAV error {err}",
    };

    public static int Load(Sample samp, CReader f, bool fix)
    {
        uint bitDepth = 8;
        var hasformat = false;
        var hasdata = false;
        uint numChannels = 0;
        var smplChunkPos = 0;

        var fileSize = (uint)f.Size;

        f.Read32();  // "RIFF"
        f.Read32();  // filesize-8
        f.Read32();  // "WAVE"

        while (true)
        {
            // break on end of file
            if ((uint)f.Tell() >= fileSize) break;

            var chunkCode = f.Read32();
            var chunkSize = f.Read32();

            if (chunkCode == Mas.Sig("fmt "))
            {
                // check compression code (1 = PCM)
                if (f.Read16() != 1)
                    return UnknownComp;
                numChannels = f.Read16();
                samp.Frequency = f.Read32();
                f.Read32();  // average bytes per second
                f.Read16();  // block align
                bitDepth = f.Read16();
                if (bitDepth != 8 && bitDepth != 16)
                    return UnsupportedBd;
                if (bitDepth == 16)
                    samp.Format |= Mas.SampF16Bit;
                // skip the rest of the chunk; unsigned, so a chunk shorter than 16 seeks backwards
                if (chunkSize - 0x10 > 0)
                    f.Skip8(chunkSize - 0x10);
                hasformat = true;
            }
            else if (chunkCode == Mas.Sig("data"))
            {
                if (!hasformat)
                    return Corrupt;

                // clip chunk size against end of file
                var br = (int)(fileSize - (uint)f.Tell());
                chunkSize = chunkSize > (uint)br ? (uint)br : chunkSize;

                if (numChannels == 0)
                    throw new MaxmodStopException("the WAV file has 0 channels; mmutil 1.10.1 crashes on it (division by zero)");
                samp.SampleLength = chunkSize / (bitDepth / 8) / numChannels;
                samp.Data8 = null;
                samp.Data16 = null;
                if (bitDepth == 8)
                    samp.Data8 = new byte[samp.SampleLength];
                else
                    samp.Data16 = new ushort[samp.SampleLength];

                for (var t = 0; (uint)t < samp.SampleLength; t++)
                {
                    var dat = 0;
                    // for multi-channel samples, get average value
                    for (var c = 0; (uint)c < numChannels; c++)
                        dat += bitDepth == 8 ? f.Read8() - 128 : (short)f.Read16();
                    // int divided by unsigned int: the sum is divided as an unsigned number
                    dat = (int)((uint)dat / numChannels);

                    if (bitDepth == 8)
                        samp.Data8![t] = (byte)(dat + 128);
                    else
                        samp.Data16![t] = (ushort)(dat + 32768);
                }
                hasdata = true;
            }
            else if (chunkCode == Mas.Sig("smpl"))
            {
                smplChunkPos = f.Tell();
                f.Skip8(chunkSize);
            }
            else
            {
                f.Skip8(chunkSize);
            }
        }

        // sampler chunk is processed last because it depends on the sample length being known
        if (smplChunkPos != 0)
        {
            f.Seek(smplChunkPos, SeekOrigin.Begin);
            f.Skip8(4 + 4 + 4 + 4 + 4 + 4 + 4);  // manufacturer .. smpte offset
            var numSampleLoops = (int)f.Read32();
            f.Read32();  // sample data
            if (numSampleLoops != 0)
            {
                f.Read32();  // cue point ID
                var loopType = (int)f.Read32();
                if (loopType < 2)
                {
                    // 0 = forward -> 1, 1 = bidi -> 2
                    samp.LoopType = (byte)(loopType + 1);
                    samp.LoopStart = f.Read32();
                    samp.LoopEnd = f.Read32();

                    // clip loop end against sample length
                    if (samp.LoopEnd > samp.SampleLength)
                        samp.LoopEnd = samp.SampleLength;

                    // disable tiny loop, catch invalid loop
                    if (samp.LoopStart > samp.SampleLength || samp.LoopEnd - samp.LoopStart < 16)
                    {
                        samp.LoopType = 0;
                        samp.LoopStart = 0;
                        samp.LoopEnd = 0;
                    }
                }
            }
        }

        if (hasformat && hasdata)
        {
            if (fix) SampleFix.FixSample(samp);
            return Ok;
        }
        return Corrupt;
    }
}
