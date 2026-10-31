using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using AgbStudio.Core.Project;

namespace AgbStudio.Editor.Windows.Settings;

/// <summary>
/// The data behind Project Settings (T-D08, T-S01..T-S05): a draft copy of the project's <see cref="ProjectFile"/>,
/// the option lists of the dropdowns, validation and the values derived from the project (scenes, fonts, string tables).
/// </summary>
public sealed partial class ProjectSettingsModel
{
    public ProjectFile Draft { get; }
    public GameProject? Project { get; }

    public ProjectSettingsModel(ProjectFile settings, GameProject? project)
    {
        Project = project;
        Draft = Clone(settings);
    }

    public static ProjectFile Clone(ProjectFile f) =>
        JsonSerializer.Deserialize<ProjectFile>(JsonSerializer.Serialize(f, GameProject.Json), GameProject.Json)!;

    /// <summary>Copies the sections the dialog edits into <paramref name="target"/> (the store's settings).</summary>
    public void ApplyTo(ProjectFile target)
    {
        var d = Clone(Draft);
        d.Save.Fields.RemoveAll(f => f.Name.Trim().Length == 0);
        foreach (var f in d.Save.Fields) { f.Name = f.Name.Trim(); f.Type = f.Type.Trim(); }
        foreach (var a in d.Input) a.Action = a.Action.Trim();
        target.General = d.General;
        target.Input = d.Input;
        target.AllowRemap = d.AllowRemap;
        target.Save = d.Save;
        target.Build = d.Build;
        target.Scripting = d.Scripting;
        target.Localization = d.Localization;
    }

    // ---- option lists ---------------------------------------------------------------------------------

    public static readonly IReadOnlyList<string> SaveTypeSegments = ["None", "SRAM 32 KB", "Flash 64 KB", "Flash 128 KB", "EEPROM 8 KB"];
    public static readonly IReadOnlyList<string> SaveTypeOptions = ["None", "SRAM · 32 KB", "Flash · 64 KB", "Flash · 128 KB", "EEPROM · 8 KB"];
    public static readonly IReadOnlyList<SaveType> SaveTypes = [SaveType.None, SaveType.Sram32K, SaveType.Flash64K, SaveType.Flash128K, SaveType.Eeprom8K];

    public static int SaveCapacity(SaveType t) => t switch
    {
        SaveType.Sram32K => 32 * 1024,
        SaveType.Flash64K => 64 * 1024,
        SaveType.Flash128K => 128 * 1024,
        SaveType.Eeprom8K => 8 * 1024,
        _ => 0,
    };

    public static readonly IReadOnlyList<(string Flag, string Label)> Optimisations =
        [("-O0", "-O0 · none"), ("-O1", "-O1 · balanced"), ("-O2", "-O2 · speed"), ("-O3", "-O3 · max speed"), ("-Os", "-Os · size")];

    public static string OptimiseLabel(string flag) => Optimisations.FirstOrDefault(o => o.Flag == flag).Label ?? flag;
    public static string OptimiseFlag(string label) => Optimisations.FirstOrDefault(o => o.Label == label).Flag ?? label;

    public static readonly IReadOnlyList<string> Splashes = ["None", "AGB Studio logo · 1 s", "AGB Studio logo · 2 s", "AGB Studio logo · 3 s", "AGB Studio logo · 4 s", "AGB Studio logo · 5 s"];

    public string SplashLabel => Draft.General.BootSplash ? $"AGB Studio logo · {Math.Clamp(Draft.General.SplashSeconds, 1, 5)} s" : "None";

    public void SetSplash(string label)
    {
        var i = Splashes.ToList().IndexOf(label);
        Draft.General.BootSplash = i > 0;
        if (i > 0) Draft.General.SplashSeconds = i;
    }

    public const string ScriptLanguage = "C# · GBA subset";
    public const string NoFont = "— none —";
    public static readonly IReadOnlyList<string> ThaiWrapOptions = ["Dictionary · 30k words", "Any character"];
    public static readonly IReadOnlyList<string> Severities = ["Error", "Warning", "Info", "None"];
    public static readonly IReadOnlyList<string> GamepadButtons = ["South", "East", "West", "North", "LB", "RB", "LT", "RT", "Menu", "View", "D-pad", "Stick", "Stick / D-pad"];

    /// <summary>
    /// The analyzer rules of Project Settings · Scripting: id and what each one catches, the titles of the analyzer itself
    /// (AgbStudio.Analyzers <see cref="AgbStudio.Analyzers.GaeRules"/>), so the dialog and the Problems list say the same,
    /// except where the Figma frame (T-S04) words a rule shorter: there Figma's text is shown.
    /// </summary>
    public static readonly IReadOnlyList<(string Id, string Catches)> AnalyzerRules =
        AgbStudio.Analyzers.GaeRules.All.Select(d => (d.Id, FigmaCatches.TryGetValue(d.Id, out var f) ? f : d.Title.ToString(CultureInfo.InvariantCulture))).ToList();

    /// <summary>The Catches texts Figma T-S04 gives without the analyzer title's reason in parentheses.</summary>
    static IReadOnlyDictionary<string, string> FigmaCatches => new Dictionary<string, string>
    {
        ["GAE005"] = "string concatenation in a loop",
        ["GAE006"] = "lambda that captures variables",
    };

    public string Severity(string rule) => Draft.Scripting.AnalyzerRules.TryGetValue(rule, out var s) ? s : rule is "GAE001" or "GAE004" ? "Error" : "Warning";

    // ---- project data ----------------------------------------------------------------------------------

    public IReadOnlyList<string> Scenes() => Project?.SceneFiles().Select(GameProject.AssetName).ToList() ?? [];

    public IReadOnlyList<string> Fonts() => Project?.FontFiles().Select(GameProject.AssetName).ToList() ?? [];

    public string LanguageName(string code) => Draft.Localization.Languages.FirstOrDefault(l => l.Code == code)?.Name ?? code;

    public string LanguageCode(string name) => Draft.Localization.Languages.FirstOrDefault(l => l.Name == name)?.Code ?? name;

    public IReadOnlyList<string> LanguageNames() => Draft.Localization.Languages.Select(l => l.Name).ToList();

    /// <summary>The one Fallback the Rules column shows: what the non-source languages fall back to.</summary>
    public string Fallback
    {
        get
        {
            var loc = Draft.Localization;
            var f = loc.Languages.Where(l => l.Code != loc.Source).Select(l => l.Fallback).FirstOrDefault(x => x.Length > 0);
            return f ?? loc.Source;
        }
        set
        {
            foreach (var l in Draft.Localization.Languages) l.Fallback = l.Code == value ? "" : value;
        }
    }

    /// <summary>Strings translated per language: keys of the source table that have text in the language's table.</summary>
    public (int Done, int Total) Progress(string code)
    {
        if (Project is null) return (0, 0);
        StringTable? Table(string c)
        {
            var path = Path.Combine(Project.TextDir, $"strings.{c}.json");
            try { return File.Exists(path) ? GameProject.Load<StringTable>(path) : null; }
            catch (Exception e) when (e is IOException or JsonException or InvalidDataException) { return null; }
        }
        var source = Table(Draft.Localization.Source);
        if (source is null) return (0, 0);
        var keys = source.Strings.Keys.ToList();
        if (code == Draft.Localization.Source) return (keys.Count(k => source.Strings[k].Length > 0), keys.Count);
        var t = Table(code);
        return (t is null ? 0 : keys.Count(k => t.Strings.TryGetValue(k, out var v) && v.Length > 0), keys.Count);
    }

    /// <summary>Bytes of one save slot: the fields of Save data.</summary>
    public int BytesPerSlot => Draft.Save.Fields.Where(f => f.Name.Trim().Length > 0).Sum(f => SafeSize(f.Type) ?? 0);

    public static int? SafeSize(string type)
    {
        var t = type.Trim();
        if (!TypeRx().IsMatch(t)) return null;
        try { return new SaveField { Type = t }.SizeBytes(); }
        catch (FormatException) { return null; }
        catch (OverflowException) { return null; }
    }

    // int, byte, short, bool, fixed, fixed2, bitset <N>, <Table>[<N>]
    [GeneratedRegex(@"^(int|byte|short|bool|fixed|fixed2|bitset \d{1,5}|[A-Za-z_][A-Za-z0-9_]*\[\d{1,4}\])$")]
    private static partial Regex TypeRx();

    [GeneratedRegex(@"^\d+\.\d+\.\d+$")]
    private static partial Regex VersionRx();

    [GeneratedRegex(@"^[A-Z0-9_]*$")]
    private static partial Regex HeaderRx();

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex IdentRx();

    /// <summary>The first problem of the draft in plain language, or null when it can be saved.</summary>
    public string? Validate()
    {
        var g = Draft.General;
        if (g.GameTitle.Trim().Length == 0) return "Game title is empty.";
        if (!VersionRx().IsMatch(g.Version.Trim())) return "Version must be major.minor.patch, like 1.0.3.";
        if (g.RomTitle.Length > 12 || !HeaderRx().IsMatch(g.RomTitle)) return "ROM title: up to 12 capital letters, digits or _.";
        if (g.GameCode.Length != 4 || !HeaderRx().IsMatch(g.GameCode)) return "Game code must be 4 capital letters or digits.";
        if (g.Maker.Length != 2 || !HeaderRx().IsMatch(g.Maker)) return "Maker must be 2 capital letters or digits.";
        var names = Draft.Input.Where(a => a.Button != "D-pad").Select(a => a.Action.Trim()).ToList();
        if (names.Any(n => !IdentRx().IsMatch(n))) return "Action names must be one word (letters, digits, _).";
        if (names.GroupBy(n => n).FirstOrDefault(same => same.Count() > 1) is { } twice)
        {
            var buttons = Draft.Input.Where(a => a.Button != "D-pad" && a.Action.Trim() == twice.Key).Select(a => a.Button).ToList();
            return $"{buttons[0]} and {buttons[1]} both use the action name {twice.Key}. Rename one of them.";
        }
        foreach (var f in Draft.Save.Fields.Where(f => f.Name.Trim().Length > 0))
        {
            if (!IdentRx().IsMatch(f.Name.Trim())) return $"Save field \"{f.Name.Trim()}\" must be one word (letters, digits, _).";
            if (SafeSize(f.Type) is null) return $"Save field \"{f.Name.Trim()}\": \"{f.Type.Trim()}\" is not a type. Use int, byte, short, bool, fixed, fixed2, bitset N or Table[N].";
        }
        if (Draft.Save.Slots is < 1 or > 16) return "Slots must be 1 to 16.";
        var cap = SaveCapacity(Draft.Save.Type);
        if (cap > 0 && BytesPerSlot * Draft.Save.Slots > cap)
        {
            var chip = SaveTypeOptions[Math.Max(0, SaveTypes.ToList().IndexOf(Draft.Save.Type))].Split(" · ")[0];
            return $"{Draft.Save.Slots} slots of {Kb(BytesPerSlot)} need {Kb(BytesPerSlot * Draft.Save.Slots)}, but {chip} holds {Kb(cap)}. Use fewer slots or fields, or a bigger save type.";
        }
        if (Draft.Build.WarnRomMegabytes is < 1 or > 32) return "Warn ROM must be 1 to 32 MB.";
        if (Draft.Build.Output.Trim().Length == 0) return "Build · Output folder is empty. Enter a folder for the ROM, like build/.";
        if (Draft.Scripting.Generated.Trim().Length == 0) return "Scripting · Generated folder is empty. Enter a folder, like build/gen.";
        if (Draft.Scripting.Namespace.Trim().Length > 0 && !Draft.Scripting.Namespace.Split('.').All(p => IdentRx().IsMatch(p))) return "Namespace must be a C# name, like SkyHopper.";
        return null;
    }

    public static string Kb(int bytes) => bytes >= 1024 && bytes % 1024 == 0 ? (bytes / 1024).ToString(CultureInfo.InvariantCulture) + " KB" : bytes.ToString(CultureInfo.InvariantCulture) + " B";
}
