using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace AgbStudio.Analyzers;

/// <summary>Reports GAE001-GAE006 on user scripts (generated files are skipped).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GbaSubsetAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => GaeRules.All;

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var behaviour = start.Compilation.GetTypeByMetadataName("GBA.Behaviour");
            start.RegisterOperationAction(c => ObjectCreation(c, behaviour),
                OperationKind.ObjectCreation, OperationKind.AnonymousObjectCreation, OperationKind.TypeParameterObjectCreation);
            start.RegisterOperationAction(c => ArrayCreation(c, behaviour), OperationKind.ArrayCreation, OperationKind.CollectionExpression);
            start.RegisterOperationAction(StringConcat, OperationKind.Binary, OperationKind.CompoundAssignment, OperationKind.InterpolatedString);
            start.RegisterOperationAction(Lambda, OperationKind.AnonymousFunction, OperationKind.LocalFunction);
        });
        context.RegisterSyntaxNodeAction(FloatSyntax, SyntaxKind.PredefinedType, SyntaxKind.IdentifierName);
        context.RegisterSyntaxNodeAction(Exceptions, SyntaxKind.TryStatement, SyntaxKind.ThrowStatement, SyntaxKind.ThrowExpression);
    }

    static void Report(OperationAnalysisContext c, DiagnosticDescriptor d, Location location, params string[] args) =>
        c.ReportDiagnostic(Diagnostic.Create(d, location, Props(args), args));

    static void Report(SyntaxNodeAnalysisContext c, DiagnosticDescriptor d, Location location, params string[] args) =>
        c.ReportDiagnostic(Diagnostic.Create(d, location, Props(args), args));

    static ImmutableDictionary<string, string?> Props(string[] args) =>
        ImmutableDictionary<string, string?>.Empty.Add(GaeRules.ArgsProperty, string.Join("|", args));

    // ---- GAE001 / GAE003: allocations in per-frame methods ---------------------------------------------

    /// <summary>Update, LateUpdate or OnTouch of a Behaviour that contains the operation (lambdas count too).</summary>
    static string? PerFrameMethod(IOperation op, ISymbol? containing, INamedTypeSymbol? behaviour)
    {
        if (behaviour == null) return null;
        var m = containing as IMethodSymbol;
        while (m != null && (m.MethodKind == MethodKind.AnonymousFunction || m.MethodKind == MethodKind.LocalFunction))
            m = m.ContainingSymbol as IMethodSymbol;
        if (m == null || m.MethodKind != MethodKind.Ordinary || m.IsStatic) return null;
        if (m.Name is not ("Update" or "LateUpdate" or "OnTouch")) return null;
        for (var t = m.ContainingType; t != null; t = t.BaseType)
            if (SymbolEqualityComparer.Default.Equals(t, behaviour)) return m.Name;
        return null;
    }

    static void ObjectCreation(OperationAnalysisContext c, INamedTypeSymbol? behaviour)
    {
        var type = c.Operation.Type;
        if (type == null || !type.IsReferenceType || type.SpecialType == SpecialType.System_String) return;
        var where = PerFrameMethod(c.Operation, c.ContainingSymbol, behaviour);
        if (where == null) return;
        var name = type.IsAnonymousType ? "{ }" : type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        Report(c, GaeRules.ClassInUpdate, c.Operation.Syntax.GetLocation(), name, where);
    }

    static void ArrayCreation(OperationAnalysisContext c, INamedTypeSymbol? behaviour)
    {
        var where = PerFrameMethod(c.Operation, c.ContainingSymbol, behaviour);
        if (where == null) return;
        // A collection expression converted to an array reports once (not also as its inner array creation).
        if (c.Operation.Kind == OperationKind.ArrayCreation && c.Operation.Parent?.Kind == OperationKind.CollectionExpression) return;
        var type = c.Operation.Type;
        var name = type is IArrayTypeSymbol a ? a.ElementType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat) + "[]" : "[...]";
        Report(c, GaeRules.NewInUpdate, c.Operation.Syntax.GetLocation(), name, where);
    }

    // ---- GAE002: float / double ---------------------------------------------------------------------------

    static void FloatSyntax(SyntaxNodeAnalysisContext c)
    {
        if (c.Node is PredefinedTypeSyntax p)
        {
            if (p.Keyword.IsKind(SyntaxKind.FloatKeyword) || p.Keyword.IsKind(SyntaxKind.DoubleKeyword))
                Report(c, GaeRules.FloatType, p.GetLocation(), p.Keyword.Text);
            return;
        }
        var id = (IdentifierNameSyntax)c.Node;
        var text = id.Identifier.ValueText;
        if (text == "var")
        {
            if (!id.IsVar) return;
            var t = c.SemanticModel.GetTypeInfo(id, c.CancellationToken).Type;
            if (IsFloat(t)) Report(c, GaeRules.FloatType, id.GetLocation(), Keyword(t!));
            return;
        }
        if (text != "Single" && text != "Double") return;
        // Only type names (System.Single / System.Double), not members that happen to have these names.
        if (id.Parent is MemberAccessExpressionSyntax ma && ma.Name == id && ma.Parent is MemberAccessExpressionSyntax) return;
        var symbol = c.SemanticModel.GetSymbolInfo(id, c.CancellationToken).Symbol as ITypeSymbol;
        if (IsFloat(symbol))
        {
            var node = id.Parent is QualifiedNameSyntax q && q.Right == id ? (SyntaxNode)q : id;
            Report(c, GaeRules.FloatType, node.GetLocation(), Keyword(symbol!));
        }
    }

    static bool IsFloat(ITypeSymbol? t) => t != null && (t.SpecialType == SpecialType.System_Single || t.SpecialType == SpecialType.System_Double);
    static string Keyword(ITypeSymbol t) => t.SpecialType == SpecialType.System_Single ? "float" : "double";

    // ---- GAE004: exceptions -------------------------------------------------------------------------------

    static void Exceptions(SyntaxNodeAnalysisContext c)
    {
        switch (c.Node)
        {
            case TryStatementSyntax t:
                Report(c, GaeRules.Exceptions, t.TryKeyword.GetLocation(), "try");
                break;
            case ThrowStatementSyntax s:
                Report(c, GaeRules.Exceptions, s.ThrowKeyword.GetLocation(), "throw");
                break;
            case ThrowExpressionSyntax e:
                Report(c, GaeRules.Exceptions, e.ThrowKeyword.GetLocation(), "throw");
                break;
        }
    }

    // ---- GAE005: string concatenation in loops ------------------------------------------------------------

    static void StringConcat(OperationAnalysisContext c)
    {
        // Plain ifs rather than a pattern switch with `when` clauses: the Release IL of that switch was
        // rejected by the runtime ("Common Language Runtime detected an invalid program").
        var op = c.Operation;
        bool concat;
        if (op is IBinaryOperation b)
            // Report the outermost part of a chain a + b + c once.
            concat = IsStringAdd(b) && !(op.Parent is IBinaryOperation pb && IsStringAdd(pb));
        else if (op is ICompoundAssignmentOperation ca)
            concat = ca.OperatorKind == BinaryOperatorKind.Add && IsString(ca.Target.Type);
        else
            concat = op is IInterpolatedStringOperation;
        if (concat && InLoop(op)) Report(c, GaeRules.StringConcatInLoop, op.Syntax.GetLocation());
    }

    static bool IsString(ITypeSymbol? t) => t != null && t.SpecialType == SpecialType.System_String;
    static bool IsStringAdd(IBinaryOperation b) => b.OperatorKind == BinaryOperatorKind.Add && IsString(b.Type);

    static bool InLoop(IOperation op)
    {
        for (var p = op.Parent; p != null; p = p.Parent)
        {
            if (p is ILoopOperation) return true;
            if (p is IAnonymousFunctionOperation || p is ILocalFunctionOperation) return false;
        }
        return false;
    }

    // ---- GAE006: capturing lambdas ------------------------------------------------------------------------

    static void Lambda(OperationAnalysisContext c)
    {
        SyntaxNode? body;
        Location location;
        switch (c.Operation.Syntax)
        {
            case AnonymousFunctionExpressionSyntax f:
                if (f.Modifiers.Any(SyntaxKind.StaticKeyword)) return;
                body = f.Body;
                location = f.GetLocation();
                break;
            case LocalFunctionStatementSyntax lf:
                if (lf.Modifiers.Any(SyntaxKind.StaticKeyword)) return;
                body = (SyntaxNode?)lf.Body ?? lf.ExpressionBody?.Expression;
                location = lf.Identifier.GetLocation();
                break;
            default:
                return;
        }
        if (body == null) return;
        var model = c.Operation.SemanticModel;
        if (model == null) return;
        var flow = body is ExpressionSyntax e ? model.AnalyzeDataFlow(e) : model.AnalyzeDataFlow((StatementSyntax)body);
        if (flow == null || !flow.Succeeded) return;
        var lambdaSymbol = model.GetSymbolInfo(c.Operation.Syntax).Symbol ?? model.GetDeclaredSymbol(c.Operation.Syntax);
        // Variables read or written inside the body that are declared outside it (including `this`).
        var captured = flow.ReadInside.Concat(flow.WrittenInside)
            .Where(s => !flow.VariablesDeclared.Contains(s))
            .Where(s => !(s is IParameterSymbol ps && SymbolEqualityComparer.Default.Equals(ps.ContainingSymbol, lambdaSymbol)))
            .Where(s => s is ILocalSymbol || s is IParameterSymbol)
            .Select(s => s is IParameterSymbol { IsThis: true } ? "this" : s.Name)
            .Distinct()
            .ToList();
        if (captured.Count == 0 && UsesInstanceMembers(body, model)) captured.Add("this");
        if (captured.Count > 0) Report(c, GaeRules.CapturingLambda, location, string.Join(", ", captured));
    }

    static bool UsesInstanceMembers(SyntaxNode body, SemanticModel model)
    {
        foreach (var n in body.DescendantNodesAndSelf())
        {
            if (n is ThisExpressionSyntax || n is BaseExpressionSyntax) return true;
            if (n is IdentifierNameSyntax id && !(id.Parent is MemberAccessExpressionSyntax ma && ma.Name == id))
            {
                var s = model.GetSymbolInfo(id).Symbol;
                if (s != null && !s.IsStatic && (s is IFieldSymbol || s is IPropertySymbol || s is IMethodSymbol { MethodKind: MethodKind.Ordinary }) &&
                    s.ContainingType != null && s.ContainingSymbol is INamedTypeSymbol) return true;
            }
        }
        return false;
    }
}
