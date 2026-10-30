using System.Text;

namespace AgbStudio.Editor.Windows.Play;

/// <summary>One symbol of the game's ELF (the build writes it next to the ROM): demangled name, address and size.</summary>
public sealed record ElfSymbol(string Name, string Mangled, uint Address, uint Size, bool IsFunction);

/// <summary>One allocated section of the ELF (.iwram, .bss, .ewram ...).</summary>
public sealed record ElfSection(string Name, uint Address, uint Size, bool Allocated, bool NoBits);

/// <summary>
/// Reads the symbol table of the Debug ELF that the build links before gbafix (infra: Play Mode overlays and the
/// Debugger memory map need the addresses of runtime state that the debug channel does not publish, such as the
/// scene camera). ELF32 little endian, as devkitARM writes it.
/// </summary>
public sealed class ElfSymbols
{
    public IReadOnlyList<ElfSymbol> Symbols { get; }
    public IReadOnlyList<ElfSection> Sections { get; }

    ElfSymbols(List<ElfSymbol> symbols, List<ElfSection> sections)
    {
        Symbols = symbols;
        Sections = sections;
    }

    public static ElfSymbols? TryLoad(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
        try { return Parse(File.ReadAllBytes(path)); }
        catch (Exception e) when (e is ArgumentException or IndexOutOfRangeException or InvalidDataException) { return null; }
    }

    public static ElfSymbols Parse(byte[] b)
    {
        if (b.Length < 52 || b[0] != 0x7F || b[1] != (byte)'E' || b[2] != (byte)'L' || b[3] != (byte)'F' || b[4] != 1 || b[5] != 1)
            throw new InvalidDataException("Not a 32-bit little-endian ELF file.");
        uint U32(int o) => BitConverter.ToUInt32(b, o);
        ushort U16(int o) => BitConverter.ToUInt16(b, o);
        var shoff = (int)U32(0x20);
        int shentsize = U16(0x2E), shnum = U16(0x30), shstrndx = U16(0x32);
        var headers = new List<(uint Name, uint Type, uint Flags, uint Addr, uint Offset, uint Size, uint Link)>();
        for (var i = 0; i < shnum; i++)
        {
            var o = shoff + i * shentsize;
            headers.Add((U32(o), U32(o + 4), U32(o + 8), U32(o + 12), U32(o + 16), U32(o + 20), U32(o + 24)));
        }
        string Str(int table, uint offset)
        {
            var start = (int)(headers[table].Offset + offset);
            var end = start;
            while (end < b.Length && b[end] != 0) end++;
            return Encoding.ASCII.GetString(b, start, end - start);
        }
        var sections = headers.Select(h => new ElfSection(Str(shstrndx, h.Name), h.Addr, h.Size, (h.Flags & 2) != 0, h.Type == 8)).ToList();
        var symbols = new List<ElfSymbol>();
        foreach (var h in headers.Where(h => h.Type == 2))
        {
            for (var o = (int)h.Offset; o + 16 <= h.Offset + h.Size; o += 16)
            {
                var info = b[o + 12];
                var type = info & 15;
                if (type is not (1 or 2)) continue;  // objects and functions
                var mangled = Str((int)h.Link, U32(o));
                if (mangled.Length == 0) continue;
                symbols.Add(new ElfSymbol(Demangle(mangled), mangled, U32(o + 4) & ~(type == 2 ? 1u : 0u), U32(o + 8), type == 2));
            }
        }
        return new ElfSymbols(symbols, sections);
    }

    /// <summary>The first symbol whose demangled name ends with <paramref name="qualifiedSuffix"/> (e.g. "agb::(anonymous namespace)::g_scene").</summary>
    public ElfSymbol? Find(string qualifiedSuffix) => Symbols.FirstOrDefault(s => s.Name == qualifiedSuffix || s.Name.EndsWith("::" + qualifiedSuffix, StringComparison.Ordinal));

    /// <summary>Linker symbols such as __iwram_end or __sp_usr (absolute addresses).</summary>
    public uint? Address(string name) => Symbols.FirstOrDefault(s => s.Mangled == name)?.Address;

    /// <summary>Nested names (<c>_ZN3agb12_GLOBAL__N_17g_sceneE</c>) to "agb::(anonymous namespace)::g_scene"; other names stay as they are.</summary>
    public static string Demangle(string m)
    {
        if (!m.StartsWith("_ZN", StringComparison.Ordinal) && !m.StartsWith("_ZL", StringComparison.Ordinal) && !m.StartsWith("_Z", StringComparison.Ordinal)) return m;
        var i = 2;
        var nested = m.Length > 2 && m[2] == 'N';
        if (nested) i = 3;
        else if (m.Length > 2 && m[2] == 'L') i = 3;
        var parts = new List<string>();
        while (i < m.Length)
        {
            if (m[i] is 'K' or 'V' or 'L') { i++; continue; }  // const, volatile, internal linkage
            if (!char.IsDigit(m[i])) break;
            var j = i;
            while (j < m.Length && char.IsDigit(m[j])) j++;
            var len = int.Parse(m.AsSpan(i, j - i), provider: System.Globalization.CultureInfo.InvariantCulture);
            if (j + len > m.Length) break;
            var name = m.Substring(j, len);
            parts.Add(name.StartsWith("_GLOBAL__N", StringComparison.Ordinal) ? "(anonymous namespace)" : name);
            i = j + len;
            if (!nested) break;
        }
        return parts.Count == 0 ? m : string.Join("::", parts);
    }
}
