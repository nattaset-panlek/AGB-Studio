using System.Globalization;
using System.Text.RegularExpressions;

namespace AgbStudio.Build;

/// <summary>The input sections and global symbols of a GNU ld map file (the <c>.elf.map</c> written next to every ROM):
/// where each object's code and data were placed. Used to check debug-channel exports (P2.13) and the IWRAM / EWRAM
/// split (G-HW.04).</summary>
public sealed class LinkerSections
{
    public sealed record Section(string Name, uint Address, uint Size, string Object)
    {
        /// <summary>The object file name (or <c>lib.a(member.o)</c>) without its folder.</summary>
        public string ObjectName => Path.GetFileName(Object.Replace('\\', '/'));
        public MemoryRegion Region => RegionOf(Address);
    }

    public enum MemoryRegion { Other, Ewram, Iwram, Rom }

    public IReadOnlyList<Section> Sections { get; }
    public IReadOnlyDictionary<string, uint> Symbols { get; }

    LinkerSections(List<Section> sections, Dictionary<string, uint> symbols)
    {
        Sections = sections;
        Symbols = symbols;
    }

    static readonly Regex OneLine = new(@"^ (\S+)\s+0x([0-9a-fA-F]+)\s+0x([0-9a-fA-F]+)\s+(.+)$", RegexOptions.Compiled);
    static readonly Regex NameOnly = new(@"^ (\.\S+|COMMON)$", RegexOptions.Compiled);
    static readonly Regex AddressSize = new(@"^\s+0x([0-9a-fA-F]+)\s+0x([0-9a-fA-F]+)\s+(.+)$", RegexOptions.Compiled);
    static readonly Regex SymbolLine = new(@"^\s+0x([0-9a-fA-F]+)\s+([A-Za-z_][\w.$]*)$", RegexOptions.Compiled);

    public static LinkerSections Read(string path) => Parse(File.ReadLines(path));

    public static LinkerSections Parse(IEnumerable<string> lines)
    {
        var sections = new List<Section>();
        var symbols = new Dictionary<string, uint>(StringComparer.Ordinal);
        var inMap = false;
        string? pending = null;
        foreach (var line in lines)
        {
            // Discarded input sections come first and carry address 0; the layout starts after this heading.
            if (!inMap)
            {
                inMap = line.StartsWith("Linker script and memory map", StringComparison.Ordinal);
                continue;
            }
            if (pending != null)
            {
                var m2 = AddressSize.Match(line);
                if (m2.Success) Add(pending, m2.Groups[1].Value, m2.Groups[2].Value, m2.Groups[3].Value);
                pending = null;
                if (m2.Success) continue;
            }
            var m = OneLine.Match(line);
            if (m.Success && (m.Groups[1].Value.StartsWith('.') || m.Groups[1].Value == "COMMON"))
            {
                Add(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value);
                continue;
            }
            var n = NameOnly.Match(line);
            if (n.Success)
            {
                pending = n.Groups[1].Value;
                continue;
            }
            var s = SymbolLine.Match(line);
            if (s.Success) symbols.TryAdd(s.Groups[2].Value, Hex(s.Groups[1].Value));
        }
        return new LinkerSections(sections, symbols);

        void Add(string name, string address, string size, string obj)
        {
            var sz = Hex(size);
            if (sz != 0) sections.Add(new Section(name, Hex(address), sz, obj.Trim()));
        }
    }

    /// <summary>The input section with exactly this name (for example <c>.sbss.agb_debug_channel</c>).</summary>
    public Section? Find(string name) => Sections.FirstOrDefault(s => s.Name == name);

    public static MemoryRegion RegionOf(uint address) => address switch
    {
        >= 0x02000000 and < 0x02040000 => MemoryRegion.Ewram,
        >= 0x03000000 and < 0x03008000 => MemoryRegion.Iwram,
        >= 0x08000000 and < 0x0A000000 => MemoryRegion.Rom,
        _ => MemoryRegion.Other,
    };

    static uint Hex(string s) => uint.Parse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
}
