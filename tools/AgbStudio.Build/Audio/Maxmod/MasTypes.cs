// Ported from Maxmod Utility (mmutil) 1.10.1, mas.h, defs.h and errors.h.
// Copyright (c) 2008, Mukunda Johnson. BSD 3-clause licence: see LICENSE-mmutil.txt in this folder.

namespace AgbStudio.Build.Audio.Maxmod;

/// <summary>Constants from defs.h, mas.h, errors.h and version.h (GBA target only).</summary>
internal static class Mas
{
    public const byte ByteSmasher = 0xBA;
    public const int MaxChannels = 32;
    public const int PanningSep = 128;  // mmutil's default; AGB Studio never passes -p

    public const byte SampF16Bit = 0x001;
    public const byte SampFSigned = 0x002;
    public const byte SampFComp = 0x004;
    public const byte SampFormatU8 = 0;
    public const byte SampFormatU16 = SampF16Bit;

    public const byte TypeSong = 0;
    public const byte TypeSampleGba = 1;
    public const byte Version = 0x18;

    public const int ErrNone = 0x00;
    public const int ErrInvalidModule = 0x01;
    public const int ErrManyChannels = 0x05;
    public const int ErrUnknownSample = 0x06;
    public const int ErrUnknownPattern = 0x07;
    public const int ErrTooManySamples = 0x09;

    public static string ErrorText(int err) => err switch
    {
        ErrInvalidModule => "invalid module",
        ErrManyChannels => "too many channels",
        ErrUnknownSample => "unsupported or broken sample",
        ErrUnknownPattern => "unsupported or broken pattern",
        ErrTooManySamples => "too many samples",
        _ => $"error {err}",
    };

    /// <summary>A multi-character constant like 'SCRM' as read32 sees the bytes "SCRM" in the file.</summary>
    public static uint Sig(string fourBytes) =>
        fourBytes[0] | (uint)fourBytes[1] << 8 | (uint)fourBytes[2] << 16 | (uint)fourBytes[3] << 24;

    /// <summary>A char array read as a C string: the bytes up to the first 0.</summary>
    public static string CString(byte[] s)
    {
        var n = Array.IndexOf(s, (byte)0);
        if (n < 0) n = s.Length;
        return System.Text.Encoding.Latin1.GetString(s, 0, n);
    }
}

internal sealed class InstrumentEnvelope
{
    public byte LoopStart, LoopEnd, SusStart, SusEnd, NodeCount;
    public readonly ushort[] NodeX = new ushort[25];
    public readonly byte[] NodeY = new byte[25];
    public byte EnvFilter, EnvValid, EnvEnabled;  // C bool (unsigned char)
}

internal sealed class Instrument
{
    public uint Parapointer;
    public byte GlobalVolume, SetPan;
    public ushort Fadeout;
    public byte RandomVolume, Nna, Dct, Dca, EnvFlags;
    public readonly ushort[] Notemap = new ushort[120];
    public readonly byte[] Name = new byte[32];
    public readonly InstrumentEnvelope EnvelopeVolume = new(), EnvelopePan = new(), EnvelopePitch = new();
}

internal sealed class Sample
{
    public uint Parapointer;
    public byte GlobalVolume, DefaultVolume, DefaultPanning;
    public uint SampleLength, LoopStart, LoopEnd;
    public byte LoopType;
    public uint Frequency;
    /// <summary>The data when <see cref="Format"/> has no SAMPF_16BIT; null when mmutil leaves the pointer NULL.</summary>
    public byte[]? Data8;
    /// <summary>The data when <see cref="Format"/> has SAMPF_16BIT.</summary>
    public ushort[]? Data16;
    public byte VibType, VibDepth, VibSpeed, VibRate;
    public ushort MslIndex;
    public byte Format;
    public uint DataPointer;
    public byte ItCompression;
    public readonly byte[] Name = new byte[32];
    /// <summary>char filename[12]; in mmutil the struct padding after it is zero, so it reads as at most 12 chars.</summary>
    public readonly byte[] Filename = new byte[12];

    public bool Is16Bit => (Format & Mas.SampF16Bit) != 0;
}

internal struct PatternEntry
{
    public byte Note, Inst, Vol, Fx, Param;
}

internal sealed class Pattern
{
    public uint Parapointer;
    public ushort NRows;
    public int CLength;
    public readonly PatternEntry[] Data = new PatternEntry[Mas.MaxChannels * 256];
    public readonly bool[] CMarks = new bool[256];
}

internal sealed class MasModule
{
    public readonly byte[] Title = new byte[32];
    public ushort OrderCount;
    public byte InstCount, SampCount, PattCount, RestartPos;
    // C bool fields are unsigned char: the IT loader stores flag bits like 16 in them.
    public byte Stereo, InstMode, FreqMode, OldEffects, LinkGxx, XmMode, OldMode;
    public byte GlobalVolume, InitialSpeed, InitialTempo;
    public readonly byte[] ChannelVolume = new byte[Mas.MaxChannels];
    public readonly byte[] ChannelPanning = new byte[Mas.MaxChannels];
    public readonly byte[] Orders = new byte[256];
    public Instrument[]? Instruments;
    public Sample[]? Samples;
    public Pattern[]? Patterns;
}

/// <summary>
/// Thrown where mmutil 1.10.1 itself crashes (division by zero) or reads or writes outside its buffers; the port stops
/// there instead of guessing what the C program would have done.
/// </summary>
internal sealed class MaxmodStopException(string message) : Exception(message);

/// <summary>What mmutil prints, plus the problems it passes over silently.</summary>
internal sealed class MaxmodLog
{
    public readonly System.Text.StringBuilder Text = new();
    public readonly List<string> Errors = new();
    public readonly List<string> Warnings = new();
    public string File = "";

    public void Print(string s) => Text.Append(s);
    public void Error(string s) => Errors.Add($"{File}: {s}");
    public void Warning(string s) => Warnings.Add($"{File}: {s}");
}
