namespace AgbStudio.Scripting;

/// <summary>
/// Everything the script compiler needs from the project besides the script files. The build fills it from the
/// project model; ids must match the data the rest of the build writes (soundbank, scenes, string tables).
/// Names are matched exactly first, then ignoring case and a file extension ("jump.wav" finds "jump").
/// </summary>
public sealed class ScriptCompileContext
{
    /// <summary>Project name (comments and the IDE project name).</summary>
    public string ProjectName { get; set; } = "Game";

    /// <summary>Project folder. Script paths in diagnostics and <c>#line</c> directives are relative to it.</summary>
    public string ProjectRoot { get; set; } = "";

    /// <summary>
    /// Project Settings · Scripting · Namespace (P6.07): used by new script templates, and as the C++ namespace of
    /// classes declared outside any C# namespace. Empty = global namespace.
    /// </summary>
    public string Namespace { get; set; } = "";

    /// <summary>Debug build: line probes (AGB_PROBE), shadow call stack (AGB_ENTER) and Line opcodes.</summary>
    public bool DebugBuild { get; set; } = true;

    /// <summary>Analyzer severities and "Treat warnings as errors" (Project Settings · Scripting).</summary>
    public AnalyzerSettings Analyzer { get; set; } = new();

    /// <summary>Sound effect name to soundbank sample id (Sound.Play).</summary>
    public Dictionary<string, int> Sounds { get; set; } = new();

    /// <summary>Music name to soundbank module id (Music.Play).</summary>
    public Dictionary<string, int> Music { get; set; } = new();

    /// <summary>Scene name to scene index in GameDef.scenes (Scene.Load).</summary>
    public Dictionary<string, int> Scenes { get; set; } = new();

    /// <summary>Tag name to tag id (Actor.tag, 0 = no tag). Use <see cref="Indexed"/> with first = 1 for a tag list.</summary>
    public Dictionary<string, int> Tags { get; set; } = new();

    /// <summary>
    /// String key to string id (Localization). Dialogue files add their keys after the largest id; the result
    /// lists the final table in <see cref="ScriptCompileResult.Strings"/>.
    /// </summary>
    public Dictionary<string, int> Strings { get; set; } = new();

    /// <summary>Dialogue speaker name to speaker id.</summary>
    public Dictionary<string, int> Speakers { get; set; } = new();

    /// <summary>Portrait name to portrait id.</summary>
    public Dictionary<string, int> Portraits { get; set; } = new();

    /// <summary>Movement paths for Move(actor, path); the id is the index in this list.</summary>
    public List<ScriptPathDef> Paths { get; set; } = new();

    /// <summary>Project Settings · Save data fields: the <c>Game</c> class (Game.score).</summary>
    public List<SaveFieldDef> SaveFields { get; set; } = new();

    /// <summary>Database tables (data/*.db): typed accessors such as Items.Get(ItemId.Potion).</summary>
    public List<DatabaseTableDef> Tables { get; set; } = new();

    /// <summary>Enums for typed script variables (Story.Forest = ForestState.Rescued).</summary>
    public List<ScriptEnumDef> Enums { get; set; } = new();

    /// <summary>Typed script variables and flags of the Event Script VM state store.</summary>
    public List<ScriptVarDef> Vars { get; set; } = new();

    /// <summary>
    /// Stable behaviour type indexes (kTypeId = 64 + index) by class name, kept in the project so ids survive
    /// renames of other classes. New classes get the next free index; the result returns the updated map.
    /// </summary>
    public Dictionary<string, int> TypeIndices { get; set; } = new();

    /// <summary>Game state field holding the money that Shop.Buy spends (int field).</summary>
    public string ShopCurrencyField { get; set; } = "coins";

    /// <summary>
    /// Folder with agb/script_ops.def and agb/natives.hpp to read instead of the copies built into this assembly
    /// (runtime/include). Null = built-in copies.
    /// </summary>
    public string? RuntimeIncludeDir { get; set; }

    /// <summary>Makes a name table from a list: the first name gets <paramref name="first"/>.</summary>
    public static Dictionary<string, int> Indexed(IEnumerable<string> names, int first = 0)
    {
        var d = new Dictionary<string, int>();
        var i = first;
        foreach (var n in names) d.TryAdd(n, i++);
        return d;
    }
}

/// <summary>Severity of one analyzer rule (Project Settings · Scripting).</summary>
public enum RuleSeverity { None, Info, Warning, Error }

public sealed class AnalyzerSettings
{
    /// <summary>Rule id (GAE001..GAE006) to severity; missing rules keep their default.</summary>
    public Dictionary<string, RuleSeverity> Rules { get; set; } = new();

    /// <summary>"Treat warnings as errors".</summary>
    public bool TreatWarningsAsErrors { get; set; }

    /// <summary>Reads the project file form ("Error", "Warning", "Info", "None").</summary>
    public static AnalyzerSettings From(IReadOnlyDictionary<string, string> rules, bool treatWarningsAsErrors)
    {
        var s = new AnalyzerSettings { TreatWarningsAsErrors = treatWarningsAsErrors };
        foreach (var (id, sev) in rules)
            if (Enum.TryParse<RuleSeverity>(sev, true, out var v)) s.Rules[id] = v;
        return s;
    }
}

/// <summary>A movement path (Move). Points are pixels, relative to the actor's start unless Relative is false.</summary>
public sealed class ScriptPathDef
{
    public string Name { get; set; } = "";
    public List<int[]> Points { get; set; } = new();
    public bool Relative { get; set; } = true;
    public double Speed { get; set; } = 1;
}

/// <summary>
/// A save field: Type is <c>int</c>, <c>byte</c>, <c>short</c>, <c>fixed</c>, <c>bool</c>, <c>fixed2</c>,
/// <c>bitset N</c> or <c>Table[N]</c> (N item ids of a database table).
/// </summary>
public sealed class SaveFieldDef
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "int";
    /// <summary>Starting value as C# literal text ("3", "1.5", "true"); null = zero.</summary>
    public string? Default { get; set; }
}

/// <summary>A database table. Column types: int, fixed, bool, string, or another table's name (its id).</summary>
public sealed class DatabaseTableDef
{
    public string Name { get; set; } = "";
    /// <summary>Id enum name; null = singular of the table name + "Id" (Items -> ItemId).</summary>
    public string? IdEnum { get; set; }
    /// <summary>Row struct name; null = singular of the table name (Items -> Item).</summary>
    public string? RowType { get; set; }
    public List<DatabaseColumnDef> Columns { get; set; } = new();
    public List<DatabaseRowDef> Rows { get; set; } = new();
}

public sealed class DatabaseColumnDef
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "int";
}

public sealed class DatabaseRowDef
{
    /// <summary>Row id (enum member name, for example "Potion").</summary>
    public string Id { get; set; } = "";
    /// <summary>Column name to value (number, bool, string or row id of the referenced table).</summary>
    public Dictionary<string, object?> Values { get; set; } = new();
}

public sealed class ScriptEnumDef
{
    public string Name { get; set; } = "";
    public List<string> Values { get; set; } = new();
}

/// <summary>
/// A typed variable of the script state store, shown as <c>Group.Name</c> in C# (Story.Forest, Flags.MetElder).
/// Type <c>bool</c> uses the flag store, <c>int</c> or an enum name the variable store. Id spaces: 0x0000
/// persistent (saved), 0x4000 per-map, 0x8000 session.
/// </summary>
public sealed class ScriptVarDef
{
    public string Group { get; set; } = "Story";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "int";
    public int Id { get; set; }
}
