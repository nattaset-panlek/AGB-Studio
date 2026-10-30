using System.Net;
using System.Text;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AgbStudio.Scripting;

/// <summary>A documented member of the GBA scripting API.</summary>
public sealed record ApiMember(string Type, string Name, string Kind, string Signature, string Summary);

/// <summary>
/// G-DOCS.01 / P6.09: the Scripting API reference generated offline from the API assembly and its XML comments, one
/// page per type with every public member.
/// </summary>
public static class ApiDocs
{
    static readonly SymbolDisplayFormat s_sig = SymbolDisplayFormat.MinimallyQualifiedFormat
        .WithMemberOptions(SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeType | SymbolDisplayMemberOptions.IncludeRef)
        .WithParameterOptions(SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeName | SymbolDisplayParameterOptions.IncludeDefaultValue);

    /// <summary>Public types of namespace GBA that users see (interop attributes are hidden).</summary>
    public static IReadOnlyList<INamedTypeSymbol> Types()
    {
        var c = CSharpCompilation.Create("docs", references: ApiReference.All);
        var api = c.References.Select(c.GetAssemblyOrModuleSymbol).OfType<IAssemblySymbol>().First(a => a.Name == "AgbStudio.Api");
        var ns = api.GlobalNamespace.GetNamespaceMembers().First(n => n.Name == "GBA");
        return ns.GetTypeMembers().Where(t => t.DeclaredAccessibility == Accessibility.Public && !Hidden(t)).OrderBy(t => t.Name, StringComparer.Ordinal).ToList();
    }

    static bool Hidden(ISymbol s) => s.GetAttributes().Any(a => a.AttributeClass?.Name == "EditorBrowsableAttribute");

    /// <summary>Every public member with its summary (empty summary = missing documentation).</summary>
    public static IReadOnlyList<ApiMember> Members()
    {
        var list = new List<ApiMember>();
        foreach (var t in Types())
        {
            list.Add(new ApiMember(t.Name, t.Name, t.TypeKind.ToString().ToLowerInvariant(), t.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat), Summary(t)));
            foreach (var m in t.GetMembers().Where(m => !m.IsImplicitlyDeclared && m.DeclaredAccessibility is Accessibility.Public or Accessibility.Protected))
            {
                if (m is IMethodSymbol { MethodKind: MethodKind.PropertyGet or MethodKind.PropertySet }) continue;
                if (m is IMethodSymbol { MethodKind: MethodKind.Constructor } ctor && ctor.DeclaredAccessibility != Accessibility.Public) continue;
                list.Add(new ApiMember(t.Name, m.Name, m.Kind.ToString().ToLowerInvariant(), m.ToDisplayString(s_sig), Summary(m)));
            }
        }
        return list;
    }

    static string Summary(ISymbol s)
    {
        var xml = s.GetDocumentationCommentXml();
        if (string.IsNullOrWhiteSpace(xml)) return "";
        try
        {
            var summary = XElement.Parse(xml).Element("summary");
            if (summary == null) return "";
            var sb = new StringBuilder();
            foreach (var node in summary.Nodes())
                sb.Append(node switch
                {
                    XText text => text.Value,
                    XElement { Name.LocalName: "see" } see => (see.Attribute("cref")?.Value ?? "").Split('.', ':').Last(),
                    XElement { Name.LocalName: "paramref" } pr => pr.Attribute("name")?.Value ?? "",
                    XElement e => e.Value,
                    _ => "",
                });
            return string.Join(' ', sb.ToString().Split((char[])['\n', '\r', ' '], StringSplitOptions.RemoveEmptyEntries));
        }
        catch (System.Xml.XmlException)
        {
            return "";
        }
    }

    /// <summary>P6.01: what scripts may use (D-02), what is forbidden and why. Matches what the translator accepts.</summary>
    public const string SubsetHtml = """
        <h1>The C# subset for GBA scripts</h1>
        <p>Scripts are C# checked by Roslyn and the GAE rules, then translated to C++20 without exceptions, RTTI or a heap.
        Cutscenes compile to Event Script bytecode instead.</p>
        <h2>Types</h2>
        <p><code>int</code>, <code>bool</code>, <code>byte</code>, <code>short</code>, <code>ushort</code>, <code>uint</code> (and <code>long</code>, which is slow),
        <code>fixed</code> (24.8 fixed point: write decimals as usual, <code>fixed speed = 1.5;</code>), <code>fixed2</code>, enums, structs and classes.
        In an external IDE write <code>Fixed</code>, because <code>fixed</code> is a reserved word in standard C#.
        Strings are constant data: names and keys such as <code>Sound.Play("jump")</code>, tags (<code>other.tag == "Coin"</code>) and <code>Debug.Log</code> text.
        Arrays have a fixed size where they are declared: <code>int[] hits = new int[8];</code></p>
        <h2>Classes</h2>
        <p>Behaviour classes get the events <code>Start</code>, <code>Update</code>, <code>LateUpdate</code>, <code>OnTouch(Actor other)</code> and <code>OnDestroy</code>;
        their public <code>int</code>, <code>fixed</code>, <code>bool</code> and enum fields show in the Inspector (<code>[Range(min, max)]</code> adds a slider).
        Scene scripts (<code>SceneScript</code>) get <code>OnLoad</code>, <code>OnEnter</code> and <code>OnExit</code>. Static helper classes, plain classes and structs are allowed,
        with fields, properties, constructors, methods, <code>virtual</code>, <code>override</code>, <code>abstract</code> and static members.
        <code>new</code> of a class takes memory from the scene's arena, which is freed when the scene ends: create objects in <code>Start</code>.</p>
        <h2>Statements</h2>
        <p><code>if</code>, <code>while</code>, <code>do</code>, <code>for</code>, <code>foreach</code> over arrays, <code>switch</code> with constant cases, <code>return</code>,
        <code>break</code>, <code>continue</code>, <code>goto</code>; <code>?:</code>, all integer and fixed arithmetic, comparisons and logic operators.</p>
        <h2>Generics and attributes</h2>
        <p>Generics only on API calls: <code>Get&lt;T&gt;()</code>, <code>Is&lt;T&gt;()</code>. Attributes: <code>[Range]</code>, <code>[Cutscene]</code>,
        <code>[ScriptNative(waits: true)]</code>.</p>
        <h2>Cutscenes</h2>
        <p><code>[Cutscene] async Task</code> (or <code>Task&lt;int&gt;</code>) methods can <code>await Say</code>, <code>Choice</code>, <code>Move</code>, <code>Wait</code>,
        <code>FadeIn</code> and <code>FadeOut</code>, use <code>using var _ = LockAll();</code>, read and write <code>Story</code> / <code>Flags</code> variables and
        <code>Game</code> fields, call other cutscenes and <code>[ScriptNative]</code> methods. Their variables are <code>int</code>, <code>bool</code>,
        <code>fixed</code> or enums, at most 16 per cutscene. Calling a cutscene from normal code starts it.</p>
        <h2>Not available, and why</h2>
        <p><code>float</code> / <code>double</code>: the GBA has no FPU (GAE002). <code>try</code> / <code>throw</code>: no exceptions in the runtime (GAE004).
        Allocating classes or arrays every frame: there is no garbage collector (GAE001, GAE003). Joining strings at runtime: strings live in ROM (GAE005).
        Lambdas and delegates: they need hidden objects (GAE006). Collections, LINQ and other .NET libraries: no heap and no .NET runtime on the GBA.
        <code>is</code> / <code>as</code> / <code>typeof</code>: no RTTI (use <code>Is&lt;T&gt;()</code>). <code>?.</code> and <code>??</code>, nested types, generic classes,
        interfaces, indexers, operator overloads in scripts and <code>async void</code> are not supported yet.</p>
        """;

    /// <summary>Writes index.html, subset.html and one page per type to <paramref name="dir"/>; returns the files written.</summary>
    public static List<string> Write(string dir)
    {
        Directory.CreateDirectory(dir);
        var files = new List<string>();
        var members = Members();
        const string style = "<style>body{font-family:Inter,Segoe UI,sans-serif;max-width:860px;margin:24px auto;padding:0 16px;color:#1d2230}" +
            "code{font-family:JetBrains Mono,Consolas,monospace;font-size:13px}h1{font-size:22px}h2{font-size:15px;margin-top:20px}p{margin:4px 0 0}</style>";
        var index = new StringBuilder($"<!doctype html><html><head><meta charset=\"utf-8\"><title>Scripting API</title>{style}</head><body><h1>Scripting API · namespace GBA</h1><ul>");
        foreach (var g in members.GroupBy(m => m.Type))
        {
            var type = g.First();
            index.Append($"<li><a href=\"{g.Key}.html\"><code>{WebUtility.HtmlEncode(type.Signature)}</code></a> {WebUtility.HtmlEncode(type.Summary)}</li>");
            var page = new StringBuilder($"<!doctype html><html><head><meta charset=\"utf-8\"><title>{g.Key}</title>{style}</head><body>");
            page.Append($"<p><a href=\"index.html\">Scripting API</a></p><h1>{WebUtility.HtmlEncode(type.Signature)}</h1><p>{WebUtility.HtmlEncode(type.Summary)}</p>");
            foreach (var m in g.Skip(1))
                page.Append($"<h2 id=\"{WebUtility.HtmlEncode(m.Name)}\"><code>{WebUtility.HtmlEncode(m.Signature)}</code></h2><p>{WebUtility.HtmlEncode(m.Summary)}</p>");
            page.Append("</body></html>\n");
            var path = Path.Combine(dir, g.Key + ".html");
            File.WriteAllText(path, page.ToString());
            files.Add(path);
        }
        index.Append("</ul><p><a href=\"subset.html\">The C# subset for GBA scripts</a></p></body></html>\n");
        var sp = Path.Combine(dir, "subset.html");
        File.WriteAllText(sp, $"<!doctype html><html><head><meta charset=\"utf-8\"><title>C# subset</title>{style}</head><body>" +
            "<p><a href=\"index.html\">Scripting API</a></p>" + SubsetHtml + "</body></html>\n");
        files.Add(sp);
        var ip = Path.Combine(dir, "index.html");
        File.WriteAllText(ip, index.ToString());
        files.Add(ip);
        return files;
    }
}
