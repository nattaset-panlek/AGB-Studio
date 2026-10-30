using System.Collections.Immutable;
using System.Text;
using AgbStudio.Analyzers;
using AgbStudio.Analyzers.CodeFixes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace AgbStudio.Scripting;

/// <summary>
/// The Script Editor diagnostic popup: lists and applies the code fixes ("Change to fixed", "Ignore") of a GAE
/// diagnostic in process, with the same providers external IDEs load.
/// </summary>
public static class ScriptCodeFixes
{
    static readonly ImmutableArray<CodeFixProvider> s_providers = [new ChangeToFixedCodeFix(), new IgnoreRuleCodeFix()];

    /// <summary>Titles of the fixes offered for the diagnostic at (line, column), 1-based.</summary>
    public static async Task<IReadOnlyList<string>> FixesAsync(string source, string diagnosticId, int line, int column,
        ScriptCompileContext? ctx = null, CancellationToken cancel = default)
    {
        var (doc, diag) = await FindAsync(source, diagnosticId, line, column, ctx, cancel).ConfigureAwait(false);
        if (diag == null) return [];
        var actions = await ActionsAsync(doc, diag, cancel).ConfigureAwait(false);
        return actions.Select(a => a.Title).ToList();
    }

    /// <summary>The source with the fix applied, or null when the diagnostic or the fix was not found.</summary>
    public static async Task<string?> ApplyAsync(string source, string diagnosticId, int line, int column, string fixTitle,
        ScriptCompileContext? ctx = null, CancellationToken cancel = default)
    {
        var (doc, diag) = await FindAsync(source, diagnosticId, line, column, ctx, cancel).ConfigureAwait(false);
        if (diag == null) return null;
        var action = (await ActionsAsync(doc, diag, cancel).ConfigureAwait(false)).FirstOrDefault(a => a.Title == fixTitle);
        if (action == null) return null;
        var ops = await action.GetOperationsAsync(cancel).ConfigureAwait(false);
        var apply = ops.OfType<ApplyChangesOperation>().FirstOrDefault();
        if (apply == null) return null;
        var newDoc = apply.ChangedSolution.GetDocument(doc.Id)!;
        var changes = await newDoc.GetTextChangesAsync(doc, cancel).ConfigureAwait(false);
        // The document was parsed with `fixed` written as `Fixed` (same length): apply the changes to the original
        // text and keep the Figma spelling in what the fix inserted.
        var restored = changes.Select(c => new TextChange(c.Span, FixedKeyword.Restore(c.NewText ?? "")));
        return SourceText.From(source).WithChanges(restored).ToString();
    }

    static async Task<List<CodeAction>> ActionsAsync(Document doc, Diagnostic diag, CancellationToken cancel)
    {
        var actions = new List<CodeAction>();
        foreach (var p in s_providers.Where(p => p.FixableDiagnosticIds.Contains(diag.Id)))
        {
            var context = new CodeFixContext(doc, diag, (a, _) => actions.Add(a), cancel);
            await p.RegisterCodeFixesAsync(context).ConfigureAwait(false);
        }
        return actions;
    }

    static async Task<(Document Doc, Diagnostic? Diag)> FindAsync(string source, string id, int line, int column,
        ScriptCompileContext? ctx, CancellationToken cancel)
    {
        ctx ??= new ScriptCompileContext();
        var ws = new AdhocWorkspace();
        var parse = new CSharpParseOptions(LanguageVersion.Latest);
        var project = ws.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(), "Scripts", "Scripts", LanguageNames.CSharp,
            compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary), parseOptions: parse, metadataReferences: ApiReference.All));
        project = project.AddDocument(ProjectApi.CSharpFileName, SourceText.From(ProjectApi.CSharp(ctx, ctx.Vars), Encoding.UTF8)).Project;
        var doc = project.AddDocument("Script.cs", SourceText.From(FixedKeyword.Rewrite(source), Encoding.UTF8));
        var compilation = (await doc.Project.GetCompilationAsync(cancel).ConfigureAwait(false))!;
        var diags = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new GbaSubsetAnalyzer()))
            .GetAnalyzerDiagnosticsAsync(cancel).ConfigureAwait(false);
        var tree = await doc.GetSyntaxTreeAsync(cancel).ConfigureAwait(false);
        var match = diags.FirstOrDefault(d => d.Id == id && d.Location.SourceTree == tree &&
            d.Location.GetLineSpan().StartLinePosition.Line + 1 == line && d.Location.GetLineSpan().StartLinePosition.Character + 1 == column);
        return (doc, match);
    }
}
