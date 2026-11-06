// Ported from Maxmod Utility (mmutil) 1.10.1, simple.c.
// Copyright (c) 2008, Mukunda Johnson. BSD 3-clause licence: see LICENSE-mmutil.txt in this folder.

namespace AgbStudio.Build.Audio.Maxmod;

internal static class Simple
{
    public const int InputTypeMod = 0;
    public const int InputTypeS3M = 1;
    public const int InputTypeXm = 2;
    public const int InputTypeIt = 3;
    public const int InputTypeWav = 4;
    public const int InputTypeTxt = 5;
    public const int InputTypeUnk = 6;
    public const int InputTypeH = 7;
    public const int InputTypeMsl = 8;

    /// <summary>
    /// get_ext: up to the last four characters before a '.', lower-cased, as a multi-character constant. A name
    /// without a dot uses its last four characters, so "song.mod" matches but "xmod" does not.
    /// </summary>
    public static int GetExt(string filename)
    {
        var strl = filename.Length;
        uint a = 0;
        if (strl < 4)
            return InputTypeUnk;
        for (var x = 0; x < 4; x++)
        {
            var c = filename[strl - x - 1];
            if (c == '.') break;
            // tolower of a char above 127 (negative in C) never gives an extension mmutil knows
            if (c > 127) return InputTypeUnk;
            a |= (uint)char.ToLowerInvariant(c) << (x * 8);
        }
        return a switch
        {
            0x6D6F64 => InputTypeMod,   // 'mod'
            0x73336D => InputTypeS3M,   // 's3m'
            0x747874 => InputTypeTxt,   // 'txt'
            0x776176 => InputTypeWav,   // 'wav'
            0x6D736C => InputTypeMsl,   // 'msl'
            0x786D => InputTypeXm,      // 'xm'
            0x6974 => InputTypeIt,      // 'it'
            0x68 => InputTypeH,         // 'h'
            _ => InputTypeUnk,
        };
    }

    /// <summary>readbits: <paramref name="size"/> bits from bit <paramref name="pos"/>, least significant first.</summary>
    public static uint ReadBits(byte[] buffer, uint pos, uint size)
    {
        uint result = 0;
        for (uint i = 0; i < size; i++)
        {
            var bytePos = (pos + i) >> 3;
            var bitPos = (int)((pos + i) & 7);
            if (bytePos >= buffer.Length)
                throw new MaxmodStopException("compressed IT sample data runs past its block; mmutil 1.10.1 reads outside its buffer here");
            result |= (uint)((buffer[bytePos] >> bitPos) & 1) << (int)i;
        }
        return result;
    }

    public static byte ClampU8(int value) => (byte)(value < 0 ? 0 : value > 255 ? 255 : value);
}
