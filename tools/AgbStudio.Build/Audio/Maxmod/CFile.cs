// Ported from Maxmod Utility (mmutil) 1.10.1, files.c.
// Copyright (c) 2008, Mukunda Johnson. BSD 3-clause licence: see LICENSE-mmutil.txt in this folder.

namespace AgbStudio.Build.Audio.Maxmod;

/// <summary>
/// An input file read the way mmutil reads it through fopen / fread / fseek: little-endian, a seek past the end is
/// allowed and a read there fails without moving. mmutil then returns an uninitialised byte; this returns 0 and
/// remembers that it happened.
/// </summary>
internal sealed class CReader(byte[] data)
{
    long _pos;

    public bool ReadPastEnd { get; private set; }
    public int Size => data.Length;

    /// <summary>ftell; mmutil keeps it in an int.</summary>
    public int Tell() => (int)_pos;

    /// <summary>fseek with a 32-bit long (Windows); a target before the start fails and leaves the position.</summary>
    public void Seek(long offset, SeekOrigin origin)
    {
        var target = origin == SeekOrigin.Begin ? offset : origin == SeekOrigin.Current ? _pos + offset : data.Length + offset;
        if (target >= 0) _pos = target;
    }

    public byte Read8()
    {
        if (_pos >= 0 && _pos < data.Length) return data[_pos++];
        ReadPastEnd = true;
        return 0;
    }

    public ushort Read16()
    {
        ushort a = Read8();
        a |= (ushort)(Read8() << 8);
        return a;
    }

    public uint Read32()
    {
        uint a = Read16();
        a |= (uint)Read16() << 16;
        return a;
    }

    /// <summary>skip8: fseek(count, SEEK_CUR) with count passed as a 32-bit long.</summary>
    public void Skip8(uint count) => Seek((int)count, SeekOrigin.Current);
}

/// <summary>An output file (or one of mmutil's temporary files) written with write8 / write16 / write32 and fseek.</summary>
internal sealed class CWriter
{
    readonly MemoryStream _ms = new();

    public int Tell() => (int)_ms.Position;
    public int Length => (int)_ms.Length;
    public void SeekSet(int offset) => _ms.Position = offset;
    public void SeekEnd() => _ms.Position = _ms.Length;

    public void Write8(byte v) => _ms.WriteByte(v);

    public void Write16(ushort v)
    {
        Write8((byte)(v & 0xFF));
        Write8((byte)(v >> 8));
    }

    public void Write32(uint v)
    {
        Write16((ushort)(v & 0xFFFF));
        Write16((ushort)(v >> 16));
    }

    public void Align32()
    {
        if ((Tell() & 3) != 0) Write8(Mas.ByteSmasher);
        if ((Tell() & 3) != 0) Write8(Mas.ByteSmasher);
        if ((Tell() & 3) != 0) Write8(Mas.ByteSmasher);
    }

    /// <summary>The bytes written so far (GetBuffer, not a copy; valid up to <see cref="Length"/>).</summary>
    public byte[] Buffer => _ms.GetBuffer();

    public byte[] ToArray() => _ms.ToArray();
}

/// <summary>Reads one of mmutil's temporary files back (read32f, read8f, skip8f on a second FILE*).</summary>
internal sealed class CTempReader(CWriter file)
{
    long _pos;

    public long Tell() => _pos;

    public byte Read8()
    {
        if (_pos >= 0 && _pos < file.Length) return file.Buffer[_pos++];
        return 0;
    }

    public ushort Read16()
    {
        ushort a = Read8();
        a |= (ushort)(Read8() << 8);
        return a;
    }

    public uint Read32()
    {
        uint a = Read16();
        a |= (uint)Read16() << 16;
        return a;
    }

    public void Skip8(uint count)
    {
        var target = _pos + (int)count;
        if (target >= 0) _pos = target;
    }
}
