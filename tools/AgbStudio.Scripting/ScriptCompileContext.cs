using AgbStudio.Core.Project;

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

    /// <summary>
    /// Project Settings · Save data fields: the <c>Game</c> class (C# Game.score is C++ agb_state.score, declared in
    /// agb_game.hpp by the build).
    /// </summary>
    public List<SaveField> SaveFields { get; set; } = new();

    /// <summary>Database tables (data/*.db): C# Items.Get(ItemId.Potion) is C++ agb_db::Items::Get (agb_db.hpp).</summary>
    public List<TableFile> Tables { get; set; } = new();

    /// <summary>Scene events (Scene Settings OnLoad / OnEnter / OnExit statements) and link "Only if" conditions.</summary>
    public List<SceneEventsDef> SceneEvents { get; set; } = new();

    /// <summary>Enums for typed script variables (Story.Forest = ForestState.Rescued).</summary>
    public List<ScriptEnumDef> Enums { get; set; } = new();

    /// <summary>Typed script variables and flags of the Event Script VM state store.</summary>
    public List<ScriptVarDef> Vars { get; set; } = new();

    /// <summary>
    /// Saved ids of the flags that dialogues declare ("Set flag" and conditions), from earlier builds
    /// (Project Settings · Save data, <c>save.flagIds</c>). A flag keeps its id; a new one never takes a recorded id,
    /// so adding a flag does not change what existing save data means. The result's <c>FlagIds</c> is the updated map.
    /// </summary>
    public Dictionary<string, int> FlagIds { get; set; } = new();

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

    /// <summary>
    /// Also write the project headers the build normally generates (agb_game.hpp with GameState, agb_state and the
    /// scene / tag / text ids, soundbank.hpp ids, agb_db.hpp and the table sources). For tests and tools that
    /// compile scripts without the game build.
    /// </summary>
    public bool EmitProjectHeaders { get; set; }

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

/// <summary>Statements typed in Scene Settings (for example <c>FadeIn(0.5s)</c>) and link conditions of one scene.</summary>
public sealed class SceneEventsDef
{
    public string Scene { get; set; } = "";
    /// <summary>Scene file (diagnostics point at it).</summary>
    public string File { get; set; } = "";
    public string OnLoad { get; set; } = "";
    public string OnEnter { get; set; } = "";
    public string OnExit { get; set; } = "";
    /// <summary>Link index in the scene to its "Only if" C# condition.</summary>
    public Dictionary<int, string> LinkConditions { get; set; } = new();
}
