using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AgbStudio.Scripting;

/// <summary>One input file: a C# script, a dialogue (.dlg) or a logic graph (.graph).</summary>
public sealed record ScriptSource(string Path, string Text)
{
    public static ScriptSource FromFile(string path) => new(System.IO.Path.GetFullPath(path), File.ReadAllText(path));
}

/// <summary>
/// The Figma scripts write the fixed-point type as <c>fixed</c>, which is a reserved word in standard C#. AGB Studio
/// rewrites the keyword token to the alias <c>Fixed</c> (same length, so every line and column stays the same) before
/// Roslyn parses the file. External IDEs see the alias through the generated project API (<c>global using Fixed</c>).
/// </summary>
public static class FixedKeyword
{
    public const string Alias = "Fixed";

    public static string Rewrite(string text)
    {
        if (!text.Contains("fixed", StringComparison.Ordinal)) return text;
        StringBuilder? sb = null;
        var last = 0;
        foreach (var token in SyntaxFactory.ParseTokens(text))
        {
            if (!token.IsKind(SyntaxKind.FixedKeyword)) continue;
            sb ??= new StringBuilder(text.Length);
            sb.Append(text, last, token.SpanStart - last).Append(Alias);
            last = token.Span.End;
        }
        if (sb == null) return text;
        sb.Append(text, last, text.Length - last);
        return sb.ToString();
    }

    /// <summary>Turns the alias back into the Figma spelling in text produced by a code fix.</summary>
    public static string Restore(string text)
    {
        if (!text.Contains(Alias, StringComparison.Ordinal)) return text;
        var sb = new StringBuilder(text.Length);
        var last = 0;
        foreach (var token in SyntaxFactory.ParseTokens(text))
        {
            if (!token.IsKind(SyntaxKind.IdentifierToken) || token.Text != Alias) continue;
            sb.Append(text, last, token.SpanStart - last).Append("fixed");
            last = token.Span.End;
        }
        sb.Append(text, last, text.Length - last);
        return sb.ToString();
    }
}

/// <summary>Metadata references for binding scripts: the GBA API reference assembly and the core library.</summary>
public static class ApiReference
{
    static readonly Lazy<ImmutableArray<MetadataReference>> s_refs = new(Load);

    /// <summary>Path of AgbStudio.Api.dll (next to this assembly).</summary>
    public static string ApiAssemblyPath => Path.Combine(AppContext.BaseDirectory, "AgbStudio.Api.dll");

    /// <summary>XML documentation of the API (IntelliSense, generated docs).</summary>
    public static string ApiDocumentationPath => Path.ChangeExtension(ApiAssemblyPath, ".xml");

    /// <summary>Analyzer assemblies for external IDE projects.</summary>
    public static string AnalyzerAssemblyPath => Path.Combine(AppContext.BaseDirectory, "AgbStudio.Analyzers.dll");
    public static string CodeFixAssemblyPath => Path.Combine(AppContext.BaseDirectory, "AgbStudio.Analyzers.CodeFixes.dll");

    public static ImmutableArray<MetadataReference> All => s_refs.Value;

    static ImmutableArray<MetadataReference> Load()
    {
        var list = new List<MetadataReference>();
        var docs = File.Exists(ApiDocumentationPath) ? XmlDocumentationProvider.CreateFromFile(ApiDocumentationPath) : null;
        list.Add(MetadataReference.CreateFromFile(ApiAssemblyPath, documentation: docs));
        // Only what scripts may bind to: core types, tasks (async cutscenes) and the netstandard facade.
        var tpa = ((AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string) ?? "").Split(Path.PathSeparator);
        string[] wanted = ["System.Private.CoreLib.dll", "System.Runtime.dll", "netstandard.dll", "System.Threading.Tasks.dll"];
        foreach (var name in wanted)
        {
            var path = tpa.FirstOrDefault(p => string.Equals(Path.GetFileName(p), name, StringComparison.OrdinalIgnoreCase));
            if (path != null) list.Add(MetadataReference.CreateFromFile(path));
        }
        if (!list.Any(r => r.Display?.EndsWith("System.Private.CoreLib.dll", StringComparison.OrdinalIgnoreCase) == true))
            list.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        return list.ToImmutableArray();
    }
}

/// <summary>Reads the runtime files the compiler uses as single sources (opcodes, built-in natives).</summary>
internal static class RuntimeFiles
{
    public static string Read(string name, string? includeDir)
    {
        if (includeDir != null)
        {
            var path = Path.Combine(includeDir, "agb", name);
            if (File.Exists(path)) return File.ReadAllText(path);
            path = Path.Combine(includeDir, name);
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Built-in copy of {name} is missing from AgbStudio.Scripting.");
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }
}

/// <summary>Writes generated files only when their content changed (so Ninja rebuilds only what changed).</summary>
internal sealed class GeneratedFiles
{
    readonly string _dir;
    public List<string> All { get; } = new();
    public List<string> Changed { get; } = new();

    public GeneratedFiles(string dir) => _dir = dir;

    public string Write(string name, string text)
    {
        var path = Path.Combine(_dir, name);
        All.Add(path);
        text = text.Replace("\r\n", "\n");
        if (File.Exists(path) && File.ReadAllText(path) == text) return path;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text, new UTF8Encoding(false));
        Changed.Add(path);
        return path;
    }
}
