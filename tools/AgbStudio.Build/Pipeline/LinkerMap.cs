using System.Globalization;
using System.Text.RegularExpressions;

namespace AgbStudio.Build.Pipeline;

/// <summary>The GNU ld map file written next to the ELF (<c>-Wl,-Map</c>): output sections with their run and load
/// addresses, the input sections each object file put into them, and the symbols the linker script defines
/// (<c>__rom_end__</c>, <c>__iheap_start</c>, <c>__eheap_start</c>). The Build Report takes its ROM and RAM numbers from
/// here, so they are the linker's numbers.</summary>
public sealed partial class LinkerMap
{
    public const long RomStart = 0x08000000, RomEnd = 0x0A000000;
    public const long IwramStart = 0x03000000, IwramEnd = 0x03008000;
    public const long EwramStart = 0x02000000, EwramEnd = 0x02040000;

    public sealed record OutputSection(string Name, long Address, long Size, long? LoadAddress)
    {
        /// <summary>Where the section's bytes are stored in the ROM image (its load address, or its address when it runs
        /// from ROM); null for sections that only take RAM (.bss, .sbss) or none (debug info).</summary>
        public long? RomAddress => LoadAddress is { } l && InRom(l) ? l : InRom(Address) ? Address : null;
    }

    /// <summary>One input section (or <c>*fill*</c> padding, Object = "") placed in an output section.</summary>
    public sealed record InputSection(OutputSection Output, string Name, long Address, long Size, string Object)
    {
        public long? RomAddress => Output.RomAddress is { } r ? r + (Address - Output.Address) : null;
        public bool IsFill => Name == "*fill*";
    }

    public List<OutputSection> Outputs { get; } = new();
    public List<InputSection> Inputs { get; } = new();
    public Dictionary<string, long> Symbols { get; } = new(StringComparer.Ordinal);

    public static bool InRom(long a) => a >= RomStart && a < RomEnd;

    /// <summary>Bytes of the ROM image (the end of the last section stored in ROM).</summary>
    public long RomUsed => Symbols.TryGetValue("__rom_end__", out var end) ? end - RomStart
        : Outputs.Where(o => o.RomAddress != null && o.Size > 0).Select(o => o.RomAddress!.Value + o.Size - RomStart).DefaultIfEmpty(0).Max();

    /// <summary>Static IWRAM use: code and data copied there at boot plus .bss (the heap starts after it).</summary>
    public long IwramUsed => Symbols.TryGetValue("__iheap_start", out var s) ? s - IwramStart : RamUsed(IwramStart, IwramEnd);

    /// <summary>Static EWRAM use (.ewram and EWRAM .bss; the EWRAM heap starts after it).</summary>
    public long EwramUsed => Symbols.TryGetValue("__eheap_start", out var s) ? s - EwramStart : RamUsed(EwramStart, EwramEnd);

    long RamUsed(long start, long end) =>
        Outputs.Where(o => o.Address >= start && o.Address < end && o.Size > 0).Select(o => o.Address + o.Size - start).DefaultIfEmpty(0).Max();

    [GeneratedRegex(@"^(\.\S*)\s+0x([0-9a-fA-F]+)\s+0x([0-9a-fA-F]+)(?:\s+load address 0x([0-9a-fA-F]+))?\s*$")]
    private static partial Regex OutputLine();
    [GeneratedRegex(@"^(\.\S*)\s*$")]
    private static partial Regex OutputNameOnly();
    [GeneratedRegex(@"^\s+0x([0-9a-fA-F]+)\s+0x([0-9a-fA-F]+)(?:\s+load address 0x([0-9a-fA-F]+))?\s*$")]
    private static partial Regex OutputNumbers();
    [GeneratedRegex(@"^ (\S+)\s+0x([0-9a-fA-F]+)\s+0x([0-9a-fA-F]+)(?:\s+(.*))?$")]
    private static partial Regex InputLine();
    [GeneratedRegex(@"^ (\S+)\s*$")]
    private static partial Regex InputNameOnly();
    [GeneratedRegex(@"^\s+0x([0-9a-fA-F]+)\s+0x([0-9a-fA-F]+)\s+(.*)$")]
    private static partial Regex InputNumbers();
    [GeneratedRegex(@"^\s+0x([0-9a-fA-F]+)\s+(?:PROVIDE \()?([A-Za-z_][\w.]*)\s*=")]
    private static partial Regex Assignment();

    public static LinkerMap Read(string path) => Parse(File.ReadAllLines(path));

    public static LinkerMap Parse(IReadOnlyList<string> lines)
    {
        var map = new LinkerMap();
        var start = 0;
        while (start < lines.Count && !lines[start].StartsWith("Linker script and memory map", StringComparison.Ordinal)) start++;
        OutputSection? current = null;
        string? pendingOutput = null, pendingInput = null;
        static long Hex(string s) => long.Parse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        for (var i = start + 1; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.Length == 0) continue;
            if (pendingOutput != null)
            {
                var n = OutputNumbers().Match(line);
                if (n.Success)
                {
                    current = new OutputSection(pendingOutput, Hex(n.Groups[1].Value), Hex(n.Groups[2].Value), n.Groups[3].Success ? Hex(n.Groups[3].Value) : null);
                    map.Outputs.Add(current);
                    pendingOutput = null;
                    continue;
                }
                current = new OutputSection(pendingOutput, 0, 0, null);  // an empty section: no numbers follow
                pendingOutput = null;
            }
            if (pendingInput != null)
            {
                var n = InputNumbers().Match(line);
                if (n.Success && current != null)
                {
                    map.Inputs.Add(new InputSection(current, pendingInput, Hex(n.Groups[1].Value), Hex(n.Groups[2].Value), n.Groups[3].Value.Trim()));
                    pendingInput = null;
                    continue;
                }
                pendingInput = null;
            }
            if (line[0] == '.')
            {
                var o = OutputLine().Match(line);
                if (o.Success)
                {
                    current = new OutputSection(o.Groups[1].Value, Hex(o.Groups[2].Value), Hex(o.Groups[3].Value), o.Groups[4].Success ? Hex(o.Groups[4].Value) : null);
                    map.Outputs.Add(current);
                }
                else if (OutputNameOnly().Match(line) is { Success: true } on) pendingOutput = on.Groups[1].Value;
                continue;
            }
            if (line[0] != ' ') continue;
            var a = Assignment().Match(line);
            if (a.Success)
            {
                map.Symbols[a.Groups[2].Value] = Hex(a.Groups[1].Value);
                continue;
            }
            if (current == null || line.Length < 2 || line[1] == ' ' || line[1] == '*' && !line.StartsWith(" *fill*", StringComparison.Ordinal)) continue;
            var m = InputLine().Match(line);
            if (m.Success)
            {
                var obj = m.Groups[4].Success ? m.Groups[4].Value.Trim() : "";
                if (m.Groups[1].Value == "*fill*") obj = "";
                map.Inputs.Add(new InputSection(current, m.Groups[1].Value, Hex(m.Groups[2].Value), Hex(m.Groups[3].Value), obj));
            }
            else if (InputNameOnly().Match(line) is { Success: true } inName) pendingInput = inName.Groups[1].Value;
        }
        return map;
    }

    /// <summary>The bytes each input section takes in the ROM image, including the padding up to the next one, so the
    /// spans add up to <see cref="RomUsed"/>.</summary>
    public List<(InputSection Section, long Bytes)> RomSpans()
    {
        var placed = Inputs.Where(s => s.RomAddress != null && s.Size > 0 && !s.IsFill).OrderBy(s => s.RomAddress).ToList();
        var end = RomStart + RomUsed;
        var spans = new List<(InputSection, long)>(placed.Count);
        for (var i = 0; i < placed.Count; i++)
        {
            var from = placed[i].RomAddress!.Value;
            var to = i + 1 < placed.Count ? placed[i + 1].RomAddress!.Value : end;
            spans.Add((placed[i], Math.Max(placed[i].Size, to - from)));
        }
        return spans;
    }
}
