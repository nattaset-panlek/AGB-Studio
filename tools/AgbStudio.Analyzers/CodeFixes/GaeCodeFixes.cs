using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace AgbStudio.Analyzers.CodeFixes;

/// <summary>"Change to fixed" for GAE002 (Script Editor diagnostic popup).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ChangeToFixedCodeFix)), Shared]
public sealed class ChangeToFixedCodeFix : CodeFixProvider
{
    public const string Title = "Change to fixed";
    public const string EquivalenceKey = "GAE.ChangeToFixed";

    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(GaeRules.FloatType.Id);

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null) return;
        foreach (var d in context.Diagnostics)
        {
            if (root.FindNode(d.Location.SourceSpan, getInnermostNodeForTie: true) is not TypeSyntax type) continue;
            context.RegisterCodeFix(
                CodeAction.Create(Title, ct => ChangeAsync(context.Document, type, ct), EquivalenceKey), d);
        }
    }

    static async Task<Document> ChangeAsync(Document document, TypeSyntax type, CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        var model = await document.GetSemanticModelAsync(ct).ConfigureAwait(false);
        if (root == null || model == null) return document;
        // `Fixed` when the project declares the alias (generated GBA project API), else the escaped `@fixed`.
        var useAlias = model.LookupNamespacesAndTypes(type.SpanStart, name: "Fixed").Any(s => s is IAliasSymbol);
        TypeSyntax replacement = useAlias
            ? SyntaxFactory.IdentifierName("Fixed")
            : SyntaxFactory.IdentifierName(SyntaxFactory.Identifier(default, SyntaxKind.IdentifierToken, "@fixed", "fixed", default));
        var mark = new SyntaxAnnotation();
        replacement = replacement.WithTriviaFrom(type).WithAdditionalAnnotations(mark);

        var newRoot = root.ReplaceNode(type, replacement);
        // Drop float / double suffixes of literal initializers of the changed declaration: 0.1f -> 0.1.
        var declaration = newRoot.GetAnnotatedNodes(mark).First().AncestorsAndSelf()
            .OfType<VariableDeclarationSyntax>().FirstOrDefault();
        if (declaration != null)
        {
            var literals = declaration.Variables.Select(v => v.Initializer?.Value).OfType<SyntaxNode>()
                .SelectMany(v => v.DescendantNodesAndSelf().OfType<LiteralExpressionSyntax>())
                .Where(l => l.IsKind(SyntaxKind.NumericLiteralExpression) && HasFloatSuffix(l.Token.Text))
                .ToList();
            newRoot = newRoot.ReplaceNodes(literals, (_, l) =>
            {
                var text = l.Token.Text.Substring(0, l.Token.Text.Length - 1);
                if (!text.Contains(".") && !text.Contains("e") && !text.Contains("E")) text += ".0";
                return l.WithToken(SyntaxFactory.ParseToken(text).WithTriviaFrom(l.Token));
            });
        }
        return document.WithSyntaxRoot(newRoot);
    }

    static bool HasFloatSuffix(string text) => text.Length > 1 && "fFdD".IndexOf(text[text.Length - 1]) >= 0 &&
        !(text.StartsWith("0x") || text.StartsWith("0X"));
}

/// <summary>"Ignore": wraps the line in <c>#pragma warning disable / restore</c> for the rule.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(IgnoreRuleCodeFix)), Shared]
public sealed class IgnoreRuleCodeFix : CodeFixProvider
{
    public const string Title = "Ignore";

    public override ImmutableArray<string> FixableDiagnosticIds => GaeRules.All.Select(d => d.Id).ToImmutableArray();

    public override FixAllProvider? GetFixAllProvider() => null;

    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        foreach (var d in context.Diagnostics)
            context.RegisterCodeFix(
                CodeAction.Create(Title, ct => IgnoreAsync(context.Document, d, ct), "GAE.Ignore." + d.Id), d);
        return Task.CompletedTask;
    }

    static async Task<Document> IgnoreAsync(Document document, Diagnostic d, CancellationToken ct)
    {
        var text = await document.GetTextAsync(ct).ConfigureAwait(false);
        var span = d.Location.SourceSpan;
        var first = text.Lines.GetLineFromPosition(span.Start);
        var last = text.Lines.GetLineFromPosition(span.End);
        var lineText = first.ToString();
        var indent = lineText.Substring(0, lineText.Length - lineText.TrimStart().Length);
        var newline = first.EndIncludingLineBreak > first.End ? text.ToString(TextSpan.FromBounds(first.End, first.EndIncludingLineBreak)) : "\n";
        var changes = new[]
        {
            new TextChange(new TextSpan(first.Start, 0), indent + "#pragma warning disable " + d.Id + newline),
            new TextChange(new TextSpan(last.End, 0), newline + indent + "#pragma warning restore " + d.Id),
        };
        return document.WithText(text.WithChanges(changes));
    }
}
