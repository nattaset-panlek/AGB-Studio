using System.Globalization;
using System.Text;
using System.Xml.Linq;
using AgbStudio.Scripting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AgbStudio.Editor.Windows.Scripts;

/// <summary>A row of the Scripting API member table: MEMBER, RETURNS, DESCRIPTION.</summary>
public sealed record ApiRow(string Member, string Returns, string Description, string Name);

/// <summary>One page of the Scripting API tree (T-P18): the API types it documents, overview, example and see also.</summary>
public sealed record ApiPage(string Title, string Glyph, IReadOnlyList<string> Types, string Overview, string Example, IReadOnlyList<string> SeeAlso,
    string? Parent = null, string? Enum = null);

/// <summary>
/// The offline Scripting API reference (P11.56, G-DOCS.01): the tree of Figma T-P18, member tables generated with ApiDocs
/// from the GBA reference assembly and its XML documentation (plus the project API: Game fields, database tables), and a
/// hand-written overview and example per page. Nothing is fetched from the network.
/// </summary>
public static class ScriptingApi
{
    public static readonly IReadOnlyList<ApiPage> Pages =
    [
        new("Behaviour", "cube", ["Behaviour"], "A script class attached to an actor. The engine calls Start once, then Update every frame and OnTouch when actors overlap.",
            "public class Coin : Behaviour\n{\n    void OnTouch(Actor other)\n    {\n        if (other.tag == \"Player\")\n        {\n            Game.score += 10;\n            actor.Destroy();\n        }\n    }\n}", ["Actor", "Input"]),
        new("Actor", "user", ["Actor"], "An object of the scene: position, velocity, tag and its behaviours. Get<T>() finds a behaviour on the same actor.",
            "void Update()\n{\n    var body = actor.Get<PlatformerBody>();\n    if (actor.pos.y > 300)\n        actor.pos = new fixed2(40, 112);\n}", ["Behaviour", "Scene"]),
        new("Input", "pad", ["Input"], "Reads the GBA keypad once per frame. All members are static and free to call anywhere in Update().",
            "void Update()\n{\n    body.vx = Input.AxisX * speed;           // walk\n    if (Input.Pressed(Key.A) && body.OnGround)\n        body.vy = -jumpPower;               // jump\n    if (Input.Pressed(Key.Start))\n        Scene.Pause();\n}",
            ["Actor", "Scene"], Enum: "Key"),
        new("Key", "pad", ["Key"], "The ten buttons of the GBA, used with Input.Pressed, Input.Held and Input.Released.",
            "if (Input.Held(Key.B))\n    body.maxSpeed = 2.5;\nif (Input.Released(Key.B))\n    body.maxSpeed = 1.5;", ["Input"], Parent: "Input"),
        new("Sound", "sound", ["Sound"], "Plays the sound effects of the Audio page by name. Names are the file names without the extension.",
            "void OnTouch(Actor other)\n{\n    if (other.tag == \"Coin\")\n        Sound.Play(\"coin\", 0.8);\n}", ["Music"]),
        new("Music", "music", ["Music"], "Starts and stops the MOD / XM music modules of the project; one module plays at a time.",
            "void Start()\n{\n    Music.Play(\"bgm_forest\");\n}\n\nvoid OnDestroy()\n{\n    Music.Fade(0, 30);\n}", ["Sound", "Scene"]),
        new("Scene", "map", ["Scene"], "Loads scenes by name, pauses the current one and finds actors by their Hierarchy name.",
            "void Update()\n{\n    if (Input.Pressed(Key.Start))\n        Scene.Pause();\n    if (Game.coins >= 10)\n        Scene.Load(\"Forest_02\");\n}", ["Camera", "Save"]),
        new("Camera", "camera", ["Camera"], "The view of the scene: follows an actor inside the dead zone, or moves and shakes on request.",
            "void Start()\n{\n    Camera.Follow(actor);\n}\n\nvoid OnTouch(Actor other)\n{\n    Camera.Shake(2, 20);\n}", ["Scene", "Actor"]),
        new("Save", "save", ["Save", "Game"], "Writes and reads the game state (the Game fields of Project Settings · Save data) in the cartridge save slots.",
            "void OnTouch(Actor other)\n{\n    if (other.tag == \"Goal\")\n    {\n        Save.Checkpoint();\n        Save.Write(0);\n    }\n}", ["Scene"]),
        new("Text & Dialogue", "type", ["Text"], "Shows localized strings from the Localization tables in the dialogue box.",
            "void OnTouch(Actor other)\n{\n    if (other.tag == \"NPC\")\n        Text.Show(\"forest.help_me\");\n}", ["Items (Database)"]),
        new("Items (Database)", "db", ["Items", "Item", "ItemId"], "The Items table of the Database, compiled into ROM. Get returns a row by its id.",
            "void OnTouch(Actor other)\n{\n    var potion = Items.Get(ItemId.Potion);\n    Game.coins -= potion.price;\n}", ["Save"]),
        new("fixed (math)", "sliders", ["fixed", "fixed2"], "24.8 fixed-point numbers: write decimals as usual (fixed speed = 1.5;). The GBA has no FPU, so float and double are not available.",
            "fixed speed = 1.5;\n\nvoid Update()\n{\n    var dx = fixed.Clamp(Input.AxisX * speed, -2, 2);\n    actor.pos.x += dx;\n}", ["Input"]),
    ];

    public static ApiPage? Find(string title) => Pages.FirstOrDefault(p => p.Title == title) ?? Pages.FirstOrDefault(p => p.Types.Contains(title));

    /// <summary>The page that documents a member or type name ("Input.Pressed", "Key", "Debug").</summary>
    public static ApiPage PageFor(string name)
    {
        var type = name.Split('.')[0];
        return Find(type) ?? new ApiPage(type, "book", [type], Members().FirstOrDefault(m => m.Type == type && m.Name == type)?.Summary ?? "", "", []);
    }

    static IReadOnlyList<ApiMember>? s_members;

    /// <summary>Every documented member: the GBA assembly (ApiDocs) and the project API of <paramref name="projectApi"/>.</summary>
    public static IReadOnlyList<ApiMember> Members(string? projectApi = null)
    {
        s_members ??= ApiDocs.Members();
        return projectApi is null ? s_members : [.. s_members, .. ProjectMembers(projectApi)];
    }

    /// <summary>Members of the generated project API (Game, Items ...) with their XML summaries.</summary>
    public static IReadOnlyList<ApiMember> ProjectMembers(string projectApi)
    {
        var tree = CSharpSyntaxTree.ParseText(projectApi, new CSharpParseOptions(LanguageVersion.Latest, DocumentationMode.Parse));
        var c = CSharpCompilation.Create("project", [tree], ApiReference.All);
        var ns = c.Assembly.GlobalNamespace.GetNamespaceMembers().FirstOrDefault(n => n.Name == "GBA");
        if (ns is null) return [];
        var list = new List<ApiMember>();
        var sig = SymbolDisplayFormat.MinimallyQualifiedFormat.WithMemberOptions(SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeType)
            .WithParameterOptions(SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeName | SymbolDisplayParameterOptions.IncludeDefaultValue);
        foreach (var t in ns.GetTypeMembers())
        {
            list.Add(new ApiMember(t.Name, t.Name, t.TypeKind.ToString().ToLowerInvariant(), t.Name, Summary(t)));
            foreach (var m in t.GetMembers().Where(m => !m.IsImplicitlyDeclared && m.DeclaredAccessibility == Accessibility.Public && m is not IMethodSymbol { MethodKind: MethodKind.PropertyGet or MethodKind.PropertySet }))
                list.Add(new ApiMember(t.Name, m.Name, m.Kind.ToString().ToLowerInvariant(), m.ToDisplayString(sig), Summary(m)));
        }
        return list;
    }

    static string Summary(ISymbol s)
    {
        var xml = s.GetDocumentationCommentXml();
        if (string.IsNullOrWhiteSpace(xml)) return "";
        try { return string.Join(' ', (XElement.Parse(xml).Element("summary")?.Value ?? "").Split((char[])['\n', '\r', ' '], StringSplitOptions.RemoveEmptyEntries)); }
        catch (System.Xml.XmlException) { return ""; }
    }

    /// <summary>
    /// Member table of a page: "Pressed(Key key) | bool | true on the frame the button goes down". Pairs named X / Y with the
    /// same type are one row ("AxisX / AxisY").
    /// </summary>
    public static IReadOnlyList<ApiRow> Rows(ApiPage page, string? projectApi = null)
    {
        var members = Members(projectApi).Where(m => page.Types.Contains(m.Type) && m.Name != m.Type).ToList();
        var rows = new List<ApiRow>();
        foreach (var m in members)
        {
            var (member, returns) = Split(m);
            if (page.Types.Count > 1 && m.Type != page.Types[0]) member = m.Type + "." + member;
            rows.Add(new ApiRow(member, returns, Describe(m.Summary), m.Name));
        }
        // AxisX + AxisY -> one row
        for (var i = 0; i + 1 < rows.Count; i++)
        {
            var (a, b) = (rows[i], rows[i + 1]);
            if (a.Name.Length > 1 && a.Name[..^1] == b.Name[..^1] && a.Name[^1] == 'X' && b.Name[^1] == 'Y' && a.Returns == b.Returns && !a.Member.Contains('('))
            {
                rows[i] = new ApiRow($"{a.Member} / {b.Member}", a.Returns, Common(a.Description, b.Description), a.Name);
                rows.RemoveAt(i + 1);
            }
        }
        return rows;
    }

    static (string Member, string Returns) Split(ApiMember m)
    {
        var sig = m.Signature.Replace("@fixed", "fixed");
        if (m.Kind == "field" && sig.IndexOf(' ') < 0) return (m.Name, m.Type);
        var at = sig.IndexOf(" " + m.Name + "(", StringComparison.Ordinal);
        if (at < 0 && sig.EndsWith(" " + m.Name, StringComparison.Ordinal)) at = sig.Length - m.Name.Length - 1;
        if (at < 0) at = sig.IndexOf(" " + m.Name, StringComparison.Ordinal);
        if (at < 0) return (sig, m.Kind == "field" ? m.Type : "");
        var returns = sig[..at];
        var member = sig[(at + 1)..];
        // "Type.Member" from minimally qualified signatures: keep the member name only
        if (member.StartsWith(m.Type + ".", StringComparison.Ordinal)) member = member[(m.Type.Length + 1)..];
        if (returns.EndsWith("." + m.Type, StringComparison.Ordinal) || returns.Length == 0) returns = m.Type;
        return (member, returns);
    }

    /// <summary>Summary as a table description: lower-case first word ("true on the frame …"), no final period.</summary>
    public static string Describe(string summary)
    {
        var s = summary.Trim().TrimEnd('.');
        if (s.Length > 1 && char.IsUpper(s[0]) && char.IsLower(s[1]) && s.Split(' ')[0] is "True" or "False" or "Any" or "The" or "A" or "An" or "Number" or "Index" or "Left" or "Top") s = char.ToLowerInvariant(s[0]) + s[1..];
        return s;
    }

    static string Common(string a, string b)
    {
        var n = 0;
        while (n < a.Length && n < b.Length && a[n] == b[n]) n++;
        var c = a[..n].TrimEnd('(', ' ', '/');
        return c.Length >= 8 ? c : a;
    }

    /// <summary>Search across every member: "pressed" finds Input.Pressed. Returns (page, member) pairs.</summary>
    public static IReadOnlyList<(ApiPage Page, ApiMember Member)> Search(string query, string? projectApi = null)
    {
        query = query.Trim();
        if (query.Length == 0) return [];
        return Members(projectApi)
            .Select(m => (m, score: Shell.PaletteSearch.Score(m.Type + "." + m.Name, query) is var s1 && s1 >= 0 ? s1 : Shell.PaletteSearch.Score(m.Name, query)))
            .Where(x => x.score >= 0 || x.m.Summary.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.score).ThenBy(x => x.m.Type, StringComparer.Ordinal)
            .Select(x => (PageFor(x.m.Type), x.m)).Take(40).ToList();
    }

    /// <summary>Docs version shown in the status bar ("Docs v0.1"): the version of the API reference assembly.</summary>
    public static string Version()
    {
        try
        {
            var v = System.Reflection.AssemblyName.GetAssemblyName(AgbStudio.Scripting.ApiReference.ApiAssemblyPath).Version;
            return v is null ? "v0.1" : string.Create(CultureInfo.InvariantCulture, $"v{v.Major}.{v.Minor}");
        }
        catch (Exception e) when (e is IOException or BadImageFormatException) { return "v0.1"; }
    }

    /// <summary>Plain text of a page for the clipboard and tests.</summary>
    public static string Text(ApiPage page, string? projectApi = null)
    {
        var sb = new StringBuilder(page.Title).Append('\n').Append(page.Overview).Append('\n');
        foreach (var r in Rows(page, projectApi)) sb.Append(r.Member).Append(" | ").Append(r.Returns).Append(" | ").Append(r.Description).Append('\n');
        return sb.Append(page.Example).ToString();
    }
}
