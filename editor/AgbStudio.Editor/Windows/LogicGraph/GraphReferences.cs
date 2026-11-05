using AgbStudio.Core.Project;
using AgbStudio.Scripting;
using AgbStudio.Scripting.Graph;

namespace AgbStudio.Editor.Windows.LogicGraph;

/// <summary>
/// What a property that names something in the project can be set to, and whether a saved value still exists there
/// (T-P17, P17.02, P17.03). One instance is a snapshot: each kind is read from disk once, when first asked.
/// </summary>
public sealed class GraphReferences(GameProject? project)
{
    /// <summary>On Touch with no tag runs for every actor; this option clears the tag.</summary>
    public const string AnyTag = "(any)";

    /// <summary>The only row when the project has nothing of that kind; choosing it changes nothing.</summary>
    public const string NoneFound = "(none in this project)";

    /// <summary>Last rows of the field and tag dropdowns: they ask for a name under the field (P17.04).</summary>
    public const string NewField = "+ New field", NewTag = "+ New tag";

    readonly Dictionary<PropKind, List<string>> _names = [];
    bool _brokenTextTable;

    public static bool IsReference(PropKind kind) =>
        kind is PropKind.Scene or PropKind.Sound or PropKind.Music or PropKind.Variable or PropKind.Tag or PropKind.TextKey;

    /// <summary>The value as the field shows it: an empty On Touch tag reads "(any)".</summary>
    public static string Display(PropDef prop, string value) => prop.Kind == PropKind.Tag && prop.Default.Length == 0 && value.Length == 0 ? AnyTag : value;

    /// <summary>The value a chosen option writes to the node, or null when it changes nothing.</summary>
    public static string? Value(string option) => option switch { NoneFound or NewField or NewTag => null, AnyTag => "", _ => option };

    public static bool IsNewRow(string option) => option is NewField or NewTag;

    /// <summary>Why a new field or tag cannot have this name, or null. Both become C# and C++ names in the build.</summary>
    public static string? NameProblem(string what, string name)
    {
        if (name.Length == 0) return $"Enter a name for the {what}.";
        if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^[A-Za-z_][A-Za-z0-9_]*$")) return $"A {what} name must be one word (letters, digits, _).";
        if (Microsoft.CodeAnalysis.CSharp.SyntaxFacts.GetKeywordKind(name) != Microsoft.CodeAnalysis.CSharp.SyntaxKind.None || Names.Cpp(name) != name) return $"\"{name}\" is a reserved word. Choose another name.";
        return null;
    }

    /// <summary>The Save data fields that hold one number: what the variable nodes and the Link builder can use.</summary>
    public static IReadOnlyList<string> NumberFields(GameProject? project) =>
        project?.Settings.Save.Fields.Where(f => IsNumber(f.Type) && f.Name.Trim().Length > 0).Select(f => f.Name.Trim()).ToList() ?? [];

    /// <summary>The line under a field whose value is missing.</summary>
    public static string MissingText(PropDef prop, string value) => $"{value} is not in this project. Choose another {KindName(prop.Kind)}; the graph keeps this name until you do.";

    static string KindName(PropKind kind) => kind switch
    {
        PropKind.Scene => "scene", PropKind.Sound => "sound", PropKind.Music => "track", PropKind.Variable => "field", PropKind.Tag => "tag", _ => "text key",
    };

    /// <summary>
    /// The dropdown's rows. A saved sound or music name may carry its extension ("jump.wav"): it stands in for its own
    /// row, so the row shows as selected and the file keeps the spelling until another row is chosen.
    /// </summary>
    public IReadOnlyList<string> Options(PropDef prop, string current)
    {
        var names = Of(prop.Kind).AsEnumerable();
        // the variable nodes read and write a number: Bool, Fixed2, Bitset and table fields do not fit their pins
        if (prop.Kind == PropKind.Variable) names = NumberFields(project);
        if (prop.Kind is PropKind.Sound or PropKind.Music)
            names = names.Select(n => SameAsset(n, current) ? current : n);
        var rows = names.ToList();
        if (prop.Kind == PropKind.Tag && prop.Default.Length == 0) rows.Insert(0, AnyTag);
        if (project != null && prop.Kind == PropKind.Variable) rows.Add(NewField);
        if (project != null && prop.Kind == PropKind.Tag) rows.Add(NewTag);
        return rows.Count > 0 ? rows : [NoneFound];
    }

    /// <summary>
    /// True when a saved name is not in the project, using the same matching as the build: case does not matter (except
    /// for fields, which are C# members) and sound and music names may carry an extension. An empty value is "not set",
    /// and a text key is never called missing while a Localization table cannot be read.
    /// </summary>
    public bool IsMissing(PropDef prop, string value)
    {
        if (project is null || !IsReference(prop.Kind) || value.Length == 0) return false;
        var names = Of(prop.Kind);
        if (prop.Kind == PropKind.TextKey && _brokenTextTable) return false;
        return prop.Kind switch
        {
            PropKind.Variable => !names.Contains(value),
            PropKind.Sound or PropKind.Music => !names.Any(n => SameAsset(n, value)),
            _ => !names.Contains(value, StringComparer.OrdinalIgnoreCase),
        };
    }

    static bool SameAsset(string name, string value) => string.Equals(name, Names.StripExtension(value), StringComparison.OrdinalIgnoreCase);

    static bool IsNumber(string type) => type.Trim() is "int" or "byte" or "short" or "fixed";

    List<string> Of(PropKind kind)
    {
        if (_names.TryGetValue(kind, out var known)) return known;
        var read = project is null ? [] : Read(project, kind).Distinct(kind == PropKind.Variable ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase).ToList();
        return _names[kind] = read;
    }

    IEnumerable<string> Read(GameProject p, PropKind kind) => kind switch
    {
        PropKind.Scene => p.SceneFiles().Select(GameProject.AssetName),
        PropKind.Sound => p.SoundFiles().Concat(p.SfxFiles()).Select(GameProject.AssetName).Order(StringComparer.OrdinalIgnoreCase),
        PropKind.Music => p.MusicFiles().Select(GameProject.AssetName).Order(StringComparer.OrdinalIgnoreCase),
        PropKind.Variable => p.Settings.Save.Fields.Where(f => f.Name.Trim().Length > 0).Select(f => f.Name.Trim()),
        PropKind.Tag => p.Settings.Tags,
        PropKind.TextKey => TextKeys(p),
        _ => [],
    };

    // Every key of the Localization tables, as the build lists them. A broken table leaves out only its own keys; the
    // Localization page and the build report it.
    SortedSet<string> TextKeys(GameProject p)
    {
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var f in p.StringTables())
        {
            try { keys.UnionWith(GameProject.Load<StringTable>(f).Strings.Keys); }
            catch (Exception e) when (e is System.Text.Json.JsonException or IOException or UnauthorizedAccessException) { _brokenTextTable = true; }
        }
        return keys;
    }
}
