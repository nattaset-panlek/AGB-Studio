using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using AgbStudio.Analyzers;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;
using AgbStudio.Scripting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;

namespace AgbStudio.Editor.Windows.Scripts;

/// <summary>Colour class of a code span (Figma code colours: keyword, type, number, method, comment, plain).</summary>
public enum CodeKind { Plain, Keyword, Type, Number, String, Comment, Method }

/// <summary>A coloured span of the text (absolute offsets).</summary>
public readonly record struct CodeSpan(int Start, int Length, CodeKind Kind);

/// <summary>An analyzer or compiler message at a text range (offsets) and its 1-based line / column.</summary>
public sealed record CodeIssue(string Id, ScriptSeverity Severity, string Message, string Description, int Start, int End, int Line, int Column)
{
    public bool IsGae => Id.StartsWith("GAE", StringComparison.Ordinal);
    /// <summary>Popup title as in Figma: "GAE002 · float is not supported on GBA".</summary>
    public string Title => $"{Id} · {Message}";
}

/// <summary>One entry of the Outline panel: the class, its fields and methods.</summary>
public sealed record OutlineItem(string Name, string Meta, OutlineKind Kind, int Line, int Start, bool HasIssue);

public enum OutlineKind { Class, Field, Method }

/// <summary>A public Inspector field of a behaviour (side panel "Exposed fields"). Value* is the initializer literal in the source.</summary>
public sealed record ExposedField(string Name, string Type, string Value, double? Min, double? Max, int ValueStart, int ValueLength);

/// <summary>An autocomplete entry from Roslyn.</summary>
public sealed record CompletionItem(string Name, string Kind, string Detail);

/// <summary>Everything the Script Editor shows for one file after an analysis pass.</summary>
public sealed class AnalysisResult
{
    public string Text { get; init; } = "";
    public IReadOnlyList<CodeSpan> Spans { get; init; } = [];
    public IReadOnlyList<CodeIssue> Issues { get; init; } = [];
    public IReadOnlyList<OutlineItem> Outline { get; init; } = [];
    public IReadOnlyList<ExposedField> Fields { get; init; } = [];
    /// <summary>First class in the file and its base type ("Behaviour", "SceneScript", "static" ...).</summary>
    public string? ClassName { get; init; }
    public string? ClassKind { get; init; }
    public static readonly AnalysisResult Empty = new();
}

/// <summary>
/// Roslyn services of the built-in Script Editor (P11.52): colours, GAE analyzer and compiler messages, outline, exposed
/// fields, autocomplete and formatting. Scripts bind against the GBA API reference assembly and the generated project API
/// (Game fields, database tables, string keys) like the build does; `fixed` is rewritten to `Fixed` before parsing (D-22).
/// </summary>
public sealed class ScriptAnalysis
{
    static readonly CSharpParseOptions Parse = new(LanguageVersion.Latest, preprocessorSymbols: ["AGB_STUDIO"]);

    readonly ScriptCompileContext _ctx;
    readonly string _projectApi;

    /// <summary>Other scripts of the project (path to text) so classes such as Coin resolve in PlayerController.</summary>
    public Dictionary<string, string> OtherScripts { get; } = new(StringComparer.OrdinalIgnoreCase);

    public ScriptAnalysis(ScriptCompileContext ctx)
    {
        _ctx = ctx;
        _projectApi = ProjectApi.CSharp(ctx, ctx.Vars);
    }

    public ScriptCompileContext Context => _ctx;

    /// <summary>The compile context of a project, as the build fills it (scenes, tags, sounds, Game fields, tables, strings).</summary>
    public static ScriptCompileContext ContextFor(GameProject? project)
    {
        if (project is null) return new ScriptCompileContext();
        try
        {
            var scenes = project.SceneFiles().Select(GameProject.AssetName).ToList();
            var input = new ScriptInput
            {
                OutputDir = Path.Combine(project.BuildDir, "gen", "scripts"),
                Debug = true,
                Scenes = ScriptCompileContext.Indexed(scenes),
                Tags = ScriptCompileContext.Indexed(project.Settings.Tags, 1),
                GameState = project.Settings.Save.Fields,
                Sounds = ScriptCompileContext.Indexed(project.SoundFiles().Select(Path.GetFileNameWithoutExtension).OfType<string>()),
                Music = ScriptCompileContext.Indexed(project.MusicFiles().Select(Path.GetFileNameWithoutExtension).OfType<string>()),
            };
            return ScriptBackend.Context(project, input);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException)
        {
            return new ScriptCompileContext { ProjectName = project.Settings.Name, ProjectRoot = project.Root, Namespace = project.Settings.Scripting.Namespace };
        }
    }

    // ---- workspace -------------------------------------------------------------------------------------------

    (Document Doc, AdhocWorkspace Ws) Build(string path, string text)
    {
        var ws = new AdhocWorkspace();
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Disable,
            generalDiagnosticOption: _ctx.Analyzer.TreatWarningsAsErrors ? ReportDiagnostic.Error : ReportDiagnostic.Default,
            specificDiagnosticOptions: SeverityOptions(_ctx.Analyzer));
        var project = ws.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(), "Scripts", "Scripts", LanguageNames.CSharp,
            compilationOptions: options, parseOptions: Parse, metadataReferences: ApiReference.All));
        project = project.AddDocument(ProjectApi.CSharpFileName, SourceText.From(_projectApi, Encoding.UTF8)).Project;
        foreach (var (p, t) in OtherScripts)
            if (!string.Equals(Path.GetFullPath(p), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
                project = project.AddDocument(Path.GetFileName(p), SourceText.From(FixedKeyword.Rewrite(t), Encoding.UTF8), filePath: p).Project;
        var doc = project.AddDocument(Path.GetFileName(path), SourceText.From(FixedKeyword.Rewrite(text), Encoding.UTF8), filePath: path);
        ws.TryApplyChanges(doc.Project.Solution);
        return (ws.CurrentSolution.GetDocument(doc.Id)!, ws);
    }

    static ImmutableDictionary<string, ReportDiagnostic> SeverityOptions(AnalyzerSettings s)
    {
        var b = ImmutableDictionary.CreateBuilder<string, ReportDiagnostic>();
        // unused-field warnings are noise while typing (the generated IDE project hides them too)
        b["CS0414"] = ReportDiagnostic.Suppress;
        b["CS0169"] = ReportDiagnostic.Suppress;
        foreach (var (id, sev) in s.Rules)
            b[id] = sev switch
            {
                RuleSeverity.None => ReportDiagnostic.Suppress,
                RuleSeverity.Info => ReportDiagnostic.Info,
                RuleSeverity.Warning => s.TreatWarningsAsErrors ? ReportDiagnostic.Error : ReportDiagnostic.Warn,
                _ => ReportDiagnostic.Error,
            };
        return b.ToImmutable();
    }

    // ---- analysis ----------------------------------------------------------------------------------------------

    /// <summary>Colours, messages, outline and exposed fields of <paramref name="text"/>.</summary>
    public AnalysisResult Analyze(string path, string text, CancellationToken cancel = default) => OffUi(() => AnalyzeCore(path, text, cancel));

    /// <summary>
    /// Runs Roslyn work that blocks on its own tasks away from the UI synchronization context: an await inside Roslyn
    /// that resumes on the UI thread would wait for the blocked UI thread forever.
    /// </summary>
    static T OffUi<T>(Func<T> work) => SynchronizationContext.Current is null ? work() : Task.Run(work).GetAwaiter().GetResult();

    AnalysisResult AnalyzeCore(string path, string text, CancellationToken cancel)
    {
        var (doc, _) = Build(path, text);
        var tree = doc.GetSyntaxTreeAsync(cancel).GetAwaiter().GetResult()!;
        var model = doc.GetSemanticModelAsync(cancel).GetAwaiter().GetResult()!;
        var spans = Classify(doc, text.Length, cancel, text);
        var issues = Diagnostics(doc, tree, text, cancel);
        var root = tree.GetCompilationUnitRoot(cancel);
        var cls = root.DescendantNodes().OfType<TypeDeclarationSyntax>().FirstOrDefault();
        var outline = Outline(root, issues, text);
        var fields = cls is null ? [] : Exposed(cls, model, text);
        string? kind = null;
        if (cls != null)
        {
            var sym = model.GetDeclaredSymbol(cls, cancel);
            kind = sym is { IsStatic: true } ? "Static helper" : sym?.BaseType is { SpecialType: SpecialType.None } bt ? bt.Name : cls.Keyword.ValueText;
        }
        return new AnalysisResult { Text = text, Spans = spans, Issues = issues, Outline = outline, Fields = fields, ClassName = cls?.Identifier.ValueText, ClassKind = kind };
    }

    static List<CodeSpan> Classify(Document doc, int length, CancellationToken cancel, string? original = null)
    {
        var list = new List<CodeSpan>();
        var classified = Classifier.GetClassifiedSpansAsync(doc, new TextSpan(0, length), cancel).GetAwaiter().GetResult();
        var last = -1;
        foreach (var c in classified.OrderBy(c => c.TextSpan.Start))
        {
            var kind = KindOf(c.ClassificationType);
            if (kind is null) continue;
            // Roslyn reports several classifications for one span (identifier + static symbol); keep the first coloured one
            if (c.TextSpan.Start <= last) continue;
            list.Add(new CodeSpan(c.TextSpan.Start, c.TextSpan.Length, kind.Value));
            last = c.TextSpan.Start;
        }
        return original is null ? list : FixedAsKeyword(original, list);
    }

    /// <summary>`fixed` is written as a keyword in scripts (D-22): colour it as one even though it binds as the Fixed alias.</summary>
    static List<CodeSpan> FixedAsKeyword(string original, List<CodeSpan> spans)
    {
        if (!original.Contains("fixed", StringComparison.Ordinal)) return spans;
        var keywords = SyntaxFactory.ParseTokens(original).Where(t => t.IsKind(SyntaxKind.FixedKeyword)).Select(t => t.SpanStart).ToHashSet();
        if (keywords.Count == 0) return spans;
        var list = spans.Where(s => !keywords.Contains(s.Start)).ToList();
        list.AddRange(keywords.Select(k => new CodeSpan(k, 5, CodeKind.Keyword)));
        return list.OrderBy(s => s.Start).ToList();
    }

    static readonly Dictionary<string, IReadOnlyList<CodeSpan>> s_colours = [];

    /// <summary>
    /// Colours of a code sample that is not a whole file (Scripting API example, Generated C# panel): the snippet is placed
    /// in a behaviour with the usual fields (body, speed, jumpPower) so API names bind.
    /// </summary>
    public static IReadOnlyList<CodeSpan> Colors(string snippet, string? projectApi = null) => OffUi(() => ColorsCore(snippet, projectApi));

    static IReadOnlyList<CodeSpan> ColorsCore(string snippet, string? projectApi)
    {
        lock (s_colours) if (projectApi is null && s_colours.TryGetValue(snippet, out var cached)) return cached;
        var whole = snippet.TrimStart().StartsWith("public class", StringComparison.Ordinal) || snippet.TrimStart().StartsWith("using ", StringComparison.Ordinal) || snippet.TrimStart().StartsWith("//", StringComparison.Ordinal);
        var prefix = whole ? (snippet.Contains("using GBA;", StringComparison.Ordinal) ? "" : "using GBA;\n") : "using GBA;\npublic class Example : Behaviour\n{\nPlatformerBody body; fixed speed; fixed jumpPower;\n";
        var text = prefix + snippet + (whole ? "" : "\n}\n");
        var ws = new AdhocWorkspace();
        var project = ws.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(), "Sample", "Sample", LanguageNames.CSharp,
            compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary), parseOptions: Parse, metadataReferences: ApiReference.All));
        project = project.AddDocument(ProjectApi.CSharpFileName, SourceText.From(projectApi ?? ProjectApi.CSharp(new ScriptCompileContext(), []), Encoding.UTF8)).Project;
        var doc = project.AddDocument("Sample.cs", SourceText.From(FixedKeyword.Rewrite(text), Encoding.UTF8));
        var spans = Classify(doc, text.Length, default, text)
            .Where(s => s.Start >= prefix.Length && s.Start + s.Length <= prefix.Length + snippet.Length)
            .Select(s => s with { Start = s.Start - prefix.Length }).ToList();
        if (projectApi is null) lock (s_colours) s_colours[snippet] = spans;
        return spans;
    }

    static CodeKind? KindOf(string type) => type switch
    {
        ClassificationTypeNames.Keyword or ClassificationTypeNames.ControlKeyword or ClassificationTypeNames.PreprocessorKeyword => CodeKind.Keyword,
        ClassificationTypeNames.ClassName or ClassificationTypeNames.StructName or ClassificationTypeNames.EnumName or ClassificationTypeNames.InterfaceName
            or ClassificationTypeNames.NamespaceName or ClassificationTypeNames.TypeParameterName or ClassificationTypeNames.RecordClassName
            or ClassificationTypeNames.DelegateName or ClassificationTypeNames.RecordStructName => CodeKind.Type,
        ClassificationTypeNames.NumericLiteral => CodeKind.Number,
        ClassificationTypeNames.StringLiteral or ClassificationTypeNames.VerbatimStringLiteral => CodeKind.String,
        ClassificationTypeNames.Comment or ClassificationTypeNames.XmlDocCommentText or ClassificationTypeNames.XmlDocCommentDelimiter => CodeKind.Comment,
        ClassificationTypeNames.MethodName or ClassificationTypeNames.ExtensionMethodName => CodeKind.Method,
        _ => null,
    };

    List<CodeIssue> Diagnostics(Document doc, SyntaxTree tree, string text, CancellationToken cancel)
    {
        var compilation = doc.Project.GetCompilationAsync(cancel).GetAwaiter().GetResult()!;
        var withAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new GbaSubsetAnalyzer()));
        var all = withAnalyzers.GetAllDiagnosticsAsync(cancel).GetAwaiter().GetResult();
        var list = new List<CodeIssue>();
        foreach (var d in all.Where(d => d.Location.SourceTree == tree && d.Severity != DiagnosticSeverity.Hidden && !d.IsSuppressed && d.Id != "CS1998")
                     .OrderBy(d => d.Location.SourceSpan.Start))
        {
            var span = d.Location.SourceSpan;
            var pos = d.Location.GetLineSpan().StartLinePosition;
            var sev = d.Severity switch { DiagnosticSeverity.Error => ScriptSeverity.Error, DiagnosticSeverity.Warning => ScriptSeverity.Warning, _ => ScriptSeverity.Info };
            var message = FixedKeyword.Restore(d.GetMessage(CultureInfo.InvariantCulture));
            var description = d.Descriptor.Description.ToString(CultureInfo.InvariantCulture);
            if (description.Length == 0) description = d.Descriptor.Title.ToString(CultureInfo.InvariantCulture);
            var start = Math.Min(span.Start, text.Length);
            list.Add(new CodeIssue(d.Id, sev, message, description, start, Math.Min(Math.Max(span.End, start + 1), text.Length), pos.Line + 1, pos.Character + 1));
        }
        return list;
    }

    static List<OutlineItem> Outline(CompilationUnitSyntax root, IReadOnlyList<CodeIssue> issues, string text)
    {
        var list = new List<OutlineItem>();
        int LineOf(int pos) => text.AsSpan(0, Math.Min(pos, text.Length)).Count('\n') + 1;
        bool Issue(SyntaxNode n) => issues.Any(i => i.Severity != ScriptSeverity.Info && i.Start >= n.SpanStart && i.Start < n.Span.End);
        foreach (var t in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            list.Add(new OutlineItem(t.Identifier.ValueText, t.Keyword.ValueText, OutlineKind.Class, LineOf(t.Identifier.SpanStart), t.Identifier.SpanStart, false));
            foreach (var m in t.Members)
            {
                switch (m)
                {
                    case FieldDeclarationSyntax f:
                        // PlatformerBody body; is a component reference, not a value field
                        var type = FixedKeyword.Restore(f.Declaration.Type.ToString());
                        if (!IsValueType(type)) continue;
                        foreach (var v in f.Declaration.Variables)
                            list.Add(new OutlineItem(v.Identifier.ValueText, type, OutlineKind.Field, LineOf(v.Identifier.SpanStart), v.Identifier.SpanStart, Issue(f)));
                        break;
                    case PropertyDeclarationSyntax p:
                        list.Add(new OutlineItem(p.Identifier.ValueText, FixedKeyword.Restore(p.Type.ToString()), OutlineKind.Field, LineOf(p.Identifier.SpanStart), p.Identifier.SpanStart, Issue(p)));
                        break;
                    case MethodDeclarationSyntax md:
                        var ps = string.Join(", ", md.ParameterList.Parameters.Select(x => FixedKeyword.Restore(x.Type?.ToString() ?? "")));
                        list.Add(new OutlineItem($"{md.Identifier.ValueText}({ps})", "", OutlineKind.Method, LineOf(md.Identifier.SpanStart), md.Identifier.SpanStart, Issue(md)));
                        break;
                }
            }
        }
        return list;
    }

    static bool IsValueType(string type) => type is "int" or "fixed" or "Fixed" or "bool" or "float" or "double" or "byte" or "short" or "fixed2" or "string" || char.IsUpper(type[0]) && type.EndsWith("State", StringComparison.Ordinal);

    static List<ExposedField> Exposed(TypeDeclarationSyntax cls, SemanticModel model, string text)
    {
        var list = new List<ExposedField>();
        foreach (var f in cls.Members.OfType<FieldDeclarationSyntax>())
        {
            if (!f.Modifiers.Any(SyntaxKind.PublicKeyword) || f.Modifiers.Any(SyntaxKind.StaticKeyword) || f.Modifiers.Any(SyntaxKind.ConstKeyword)) continue;
            var type = FixedKeyword.Restore(f.Declaration.Type.ToString());
            var t = model.GetTypeInfo(f.Declaration.Type).Type;
            var isEnum = t?.TypeKind == TypeKind.Enum;
            if (type is not ("int" or "fixed" or "bool") && !isEnum) continue;
            double? min = null, max = null;
            foreach (var a in f.AttributeLists.SelectMany(l => l.Attributes).Where(a => a.Name.ToString() is "Range" or "RangeAttribute"))
            {
                var args = a.ArgumentList?.Arguments.Select(x => x.Expression.ToString()).ToList() ?? [];
                if (args.Count == 2 && double.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lo) &&
                    double.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var hi)) (min, max) = (lo, hi);
            }
            foreach (var v in f.Declaration.Variables)
            {
                var init = v.Initializer?.Value;
                var value = init?.ToString() ?? (type == "bool" ? "false" : "0");
                list.Add(new ExposedField(v.Identifier.ValueText, type, value, min, max, init?.SpanStart ?? -1, init?.Span.Length ?? 0));
            }
        }
        return list;
    }

    // ---- autocomplete ------------------------------------------------------------------------------------------

    /// <summary>Symbols Roslyn recommends at <paramref name="position"/> (members after a dot, locals, types and keywords of the GBA API).</summary>
    public IReadOnlyList<CompletionItem> Complete(string path, string text, int position, CancellationToken cancel = default) => OffUi(() => CompleteCore(path, text, position, cancel));

    IReadOnlyList<CompletionItem> CompleteCore(string path, string text, int position, CancellationToken cancel)
    {
        var (doc, _) = Build(path, text);
#pragma warning disable CS0618 // Recommender is the in-process completion source the IDE tests use too
        var symbols = Microsoft.CodeAnalysis.Recommendations.Recommender.GetRecommendedSymbolsAtPositionAsync(doc, position, cancellationToken: cancel).GetAwaiter().GetResult();
#pragma warning restore CS0618
        var hidden = new HashSet<string>(StringComparer.Ordinal) { "CppAttribute", "AssetAttribute", "ScriptOpAttribute", "AssetKind" };
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var list = new List<CompletionItem>();
        foreach (var s in symbols)
        {
            if (hidden.Contains(s.Name) || s.Name.StartsWith('<') || s.Name.StartsWith("op_", StringComparison.Ordinal) || s.IsImplicitlyDeclared) continue;
            if (s is IMethodSymbol { MethodKind: not MethodKind.Ordinary }) continue;
            if (s.ContainingAssembly is { Name: not ("AgbStudio.Api" or "Scripts") } && s is not ILocalSymbol and not IParameterSymbol && s.ContainingNamespace?.ToDisplayString() is "System" or "System.Threading.Tasks") continue;
            if (!seen.Add(s.Name)) continue;
            list.Add(new CompletionItem(s.Name, s.Kind.ToString().ToLowerInvariant(), Detail(s)));
        }
        var dotted = position > 0 && text[position - 1] == '.';
        return list.OrderBy(c => dotted ? 0 : c.Kind is "local" or "parameter" or "field" ? 0 : 1).ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    static string Detail(ISymbol s) => s switch
    {
        IMethodSymbol m => FixedKeyword.Restore(m.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)).Replace("@fixed", "fixed"),
        IPropertySymbol p => FixedKeyword.Restore(p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)).Replace("@fixed", "fixed"),
        IFieldSymbol f => f.ContainingType?.TypeKind == TypeKind.Enum ? f.ContainingType.Name : FixedKeyword.Restore(f.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)).Replace("@fixed", "fixed"),
        ILocalSymbol l => l.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
        INamedTypeSymbol t => t.TypeKind.ToString().ToLowerInvariant(),
        _ => s.Kind.ToString().ToLowerInvariant(),
    };

    // ---- formatting --------------------------------------------------------------------------------------------

    /// <summary>Format on save (Preferences · Editor): Roslyn formatting with spaces and the editor's tab size.</summary>
    public static string Format(string text, int tabSize) => OffUi(() => FormatCore(text, tabSize));

    static string FormatCore(string text, int tabSize)
    {
        var ws = new AdhocWorkspace();
        var rewritten = FixedKeyword.Rewrite(text);
        var tree = CSharpSyntaxTree.ParseText(rewritten, Parse);
        var options = ws.Options
            .WithChangedOption(FormattingOptions.UseTabs, LanguageNames.CSharp, false)
            .WithChangedOption(FormattingOptions.TabSize, LanguageNames.CSharp, tabSize)
            .WithChangedOption(FormattingOptions.IndentationSize, LanguageNames.CSharp, tabSize)
            .WithChangedOption(FormattingOptions.NewLine, LanguageNames.CSharp, "\n");
        var formatted = Formatter.Format(tree.GetRoot(), ws, options).ToFullString();
        // Keep the Figma spelling of the fixed-point type (the rewrite changed only `fixed` keywords).
        return Restore(rewritten, formatted, text);
    }

    /// <summary>Puts `fixed` back where the original had the keyword (formatting does not reorder tokens).</summary>
    static string Restore(string rewritten, string formatted, string original)
    {
        if (ReferenceEquals(rewritten, original) || rewritten == original) return formatted;
        var origTokens = SyntaxFactory.ParseTokens(original).Where(t => !t.IsKind(SyntaxKind.EndOfFileToken)).ToList();
        var newTokens = SyntaxFactory.ParseTokens(formatted).Where(t => !t.IsKind(SyntaxKind.EndOfFileToken)).ToList();
        if (origTokens.Count != newTokens.Count) return FixedKeyword.Restore(formatted);
        var sb = new StringBuilder(formatted.Length);
        var last = 0;
        for (var i = 0; i < newTokens.Count; i++)
        {
            if (!origTokens[i].IsKind(SyntaxKind.FixedKeyword) || newTokens[i].Text != FixedKeyword.Alias) continue;
            sb.Append(formatted, last, newTokens[i].SpanStart - last).Append("fixed");
            last = newTokens[i].Span.End;
        }
        sb.Append(formatted, last, formatted.Length - last);
        return sb.ToString();
    }

    /// <summary>Name of the member that contains <paramref name="position"/> ("Update()", "speed"), for the breadcrumb.</summary>
    public static string? MemberAt(string text, int position)
    {
        var root = CSharpSyntaxTree.ParseText(FixedKeyword.Rewrite(text), Parse).GetCompilationUnitRoot();
        var token = root.FindToken(Math.Clamp(position, 0, Math.Max(0, text.Length - 1)));
        for (var n = token.Parent; n != null; n = n.Parent)
            switch (n)
            {
                case MethodDeclarationSyntax m: return m.Identifier.ValueText + "()";
                case PropertyDeclarationSyntax p: return p.Identifier.ValueText;
                case FieldDeclarationSyntax f: return f.Declaration.Variables.FirstOrDefault()?.Identifier.ValueText;
                case TypeDeclarationSyntax t: return t.Identifier.ValueText;
            }
        return null;
    }

    /// <summary>Return type and parameters of an API member for the quick reference ("Input.Pressed" -> "Input.Pressed(Key k)", "bool").</summary>
    public (string Signature, string Returns)? Describe(string member)
    {
        var dot = member.IndexOf('.');
        if (dot < 0) return null;
        var compilation = CSharpCompilation.Create("ref", [CSharpSyntaxTree.ParseText(_projectApi, Parse)], ApiReference.All);
        var type = compilation.GetTypeByMetadataName("GBA." + member[..dot]) ?? compilation.GetSymbolsWithName(member[..dot], SymbolFilter.Type).OfType<INamedTypeSymbol>().FirstOrDefault();
        var sym = type?.GetMembers(member[(dot + 1)..]).FirstOrDefault(m => m is IMethodSymbol or IPropertySymbol or IFieldSymbol);
        if (sym is null) return null;
        static string T(ITypeSymbol t) => t.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat).Replace("@fixed", "fixed");
        return sym switch
        {
            IMethodSymbol m => ($"{member}({string.Join(", ", m.Parameters.Select(p => $"{T(p.Type)} {Short(p.Name, T(p.Type))}"))})", T(m.ReturnType)),
            IPropertySymbol p => (member, T(p.Type)),
            IFieldSymbol f => (member, T(f.Type)),
            _ => null,
        };
    }

    // A parameter named like its type is shortened, as the Figma quick reference writes Input.Pressed(Key k)
    static string Short(string name, string type) => string.Equals(name, type, StringComparison.OrdinalIgnoreCase) ? name[..1] : name;
}
