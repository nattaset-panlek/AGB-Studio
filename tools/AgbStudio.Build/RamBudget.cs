namespace AgbStudio.Build;

/// <summary>IWRAM and EWRAM use of a linked ROM, read from its linker map and split into the rows of blueprint 10
/// (G-HW.04). Hard limits, reported as build warnings: IWRAM code and data plus an 8 KB stack fit the 32 KB below the
/// BIOS / IRQ area, EWRAM fits 256 KB. Each row also carries the blueprint's default budget; a row over its default is
/// shown in report.json (<c>ramSplit</c>, <c>overBudget</c>) but is no warning, because the defaults are a plan that rows
/// trade against the free reserve and the game cannot change the runtime's own rows.</summary>
public sealed class RamBudget
{
    public sealed record Row(string Region, string Name, long Bytes, long Budget, string Holds)
    {
        public bool OverBudget => Bytes > Budget;
    }

    public const long IwramUsable = 0x7F00;  // 0x03000000..0x03007EFF; above it the IRQ stack and BIOS variables
    public const long StackReserve = 8 * 1024;
    public const long EwramSize = 256 * 1024;

    public List<Row> Rows { get; } = new();
    public long IwramStatic { get; private set; }
    public long EwramStatic { get; private set; }
    /// <summary>IWRAM left for the stack after code and data (blueprint: 8 KB).</summary>
    public long StackSpace => IwramUsable - IwramStatic;
    public bool WithinLimits => StackSpace >= StackReserve && EwramStatic <= EwramSize;

    public IEnumerable<string> Warnings()
    {
        if (StackSpace < StackReserve)
            yield return $"IWRAM: code and data use {IwramStatic / 1024.0:0.0} KB, leaving {StackSpace / 1024.0:0.0} KB for the stack; blueprint 10 keeps 8 KB.";
        if (EwramStatic > EwramSize) yield return $"EWRAM: {EwramStatic / 1024.0:0.0} KB is more than the 256 KB the GBA has.";
    }

    /// <summary>One line per row: used, default budget and what the row holds (test output, evidence).</summary>
    public IEnumerable<string> Describe() =>
        Rows.Select(r => $"{r.Region} {r.Name}: {r.Bytes / 1024.0:0.0} KB of {r.Budget / 1024} KB{(r.OverBudget ? " (over the default)" : "")} - {r.Holds}")
            .Append($"IWRAM stack space {StackSpace / 1024.0:0.0} KB (blueprint 8 KB); EWRAM {EwramStatic / 1024.0:0.0} KB of 256 KB");

    // Object files of the runtime by row (runtime/src/<folder>/<name>.cpp compiles to <...>_<folder>_<name>.o).
    static readonly string[] PoolObjects = ["core_task.o", "render_sprite.o", "framework_actor.o", "framework_bus.o"];
    static readonly string[] VmObjects = ["script_vm.o", "script_natives.o", "script_probe.o", "core_input.o"];
    static readonly string[] MapObjects = ["render_map.o", "render_map_draw.iwram.o", "render_affine.o", "render_bg.o"];
    static readonly string[] TextObjects = ["render_text.o", "render_textdb.o", "render_glyph.iwram.o", "framework_ui.o"];

    static bool From(LinkerMap.Section s, string[] objects) => objects.Any(o => s.ObjectName.EndsWith("_" + o, StringComparison.OrdinalIgnoreCase));

    public static RamBudget FromMap(LinkerMap map)
    {
        var b = new RamBudget();
        long code = 0, pools = 0, vm = 0, otherI = 0, arena = 0, maps = 0, save = 0, text = 0, scratch = 0;
        foreach (var s in map.Sections)
        {
            if (s.Region == LinkerMap.MemoryRegion.Iwram)
            {
                b.IwramStatic += s.Size;
                if (s.Name.StartsWith(".iwram", StringComparison.Ordinal)) code += s.Size;
                else if (From(s, PoolObjects)) pools += s.Size;
                else if (From(s, VmObjects)) vm += s.Size;
                else otherI += s.Size;
            }
            else if (s.Region == LinkerMap.MemoryRegion.Ewram)
            {
                b.EwramStatic += s.Size;
                if (s.ObjectName.EndsWith("_core_memory.o", StringComparison.OrdinalIgnoreCase)) arena += s.Size;
                else if (s.ObjectName.EndsWith("_core_save.o", StringComparison.OrdinalIgnoreCase)) save += s.Size;
                else if (From(s, MapObjects)) maps += s.Size;
                else if (From(s, TextObjects)) text += s.Size;
                else scratch += s.Size;
            }
        }
        b.Rows.Add(new Row("IWRAM", "Hot ARM code", code, 8 * 1024, "code in .iwram sections: *.iwram.cpp, Maxmod mixer, interrupt dispatcher"));
        b.Rows.Add(new Row("IWRAM", "Pools", pools, 6 * 1024, "task, sprite, actor and command-bus state in IWRAM"));
        b.Rows.Add(new Row("IWRAM", "VM + input", vm, 6 * 1024, "Event Script VM, natives, C# probes, input"));
        b.Rows.Add(new Row("IWRAM", "Other data", otherI, 4 * 1024, "not a blueprint row, measured against its free 4 KB: commit shadows, Maxmod mixing buffer, game data"));
        b.Rows.Add(new Row("EWRAM", "Screen arena", arena, 112 * 1024, "per-Screen arena; map shadows and text windows are allocated in it"));
        b.Rows.Add(new Row("EWRAM", "Map buffer", maps, 40 * 1024, "map, affine and BG manager state outside the arena"));
        b.Rows.Add(new Row("EWRAM", "Save blocks", save, 32 * 1024, "save buffers"));
        b.Rows.Add(new Row("EWRAM", "Text", text, 20 * 1024, "text, string table and menu state outside the arena"));
        b.Rows.Add(new Row("EWRAM", "Scratch", scratch, 26 * 1024, "everything else in EWRAM: actor table, sprites, debug channel, audio buffers, game data"));
        return b;
    }
}
