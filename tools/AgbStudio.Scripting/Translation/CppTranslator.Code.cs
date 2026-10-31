using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using static AgbStudio.Scripting.Translation.CppExpr;

namespace AgbStudio.Scripting.Translation;

internal sealed partial class CppTranslator
{
    // =====================================================================================================
    // Statements
    // =====================================================================================================

    void Stmt(IOperation op)
    {
        var (file, line) = Pos(op);
        // One probe per source line: a statement on the same line as its if / loop is not probed again.
        var probe = Debug && !op.IsImplicit && op is not IBlockOperation && line != _probedLine ? $"AGB_PROBE({FileIdOf(file)}, {line}); " : "";
        if (probe.Length > 0) _probedLine = line;
        switch (op)
        {
            case IBlockOperation b:
                _w.Line("{", file, line);
                _w.Indent++;
                foreach (var s in b.Operations) Stmt(s);
                _w.Indent--;
                _w.Line("}");
                break;
            case IExpressionStatementOperation es:
                _w.Line(probe + ExprStatement(es.Operation) + ";", file, line);
                break;
            case IVariableDeclarationGroupOperation g:
                foreach (var d in g.Declarations)
                    foreach (var v in d.Declarators) LocalDecl(v, d, probe, file, line);
                break;
            case IConditionalOperation c:
                IfChain(c, probe, file, line);
                break;
            case IWhileLoopOperation w when w.ConditionIsTop:
                _w.Line($"{probe}while ({Cond(w.Condition!)}) {{", file, line);
                Body(w.Body);
                _w.Line("}");
                break;
            case IWhileLoopOperation w:
                _w.Line($"{probe}do {{", file, line);
                Body(w.Body);
                _w.Line($"}} while ({Cond(w.Condition!)});");
                break;
            case IForLoopOperation f:
                {
                    var init = string.Join(", ", f.Before.Select(ForPart));
                    if (f.Before.Length == 1 && f.Before[0] is IVariableDeclarationGroupOperation) init = ForPart(f.Before[0]);
                    var cond = f.Condition != null ? Cond(f.Condition) : "";
                    var incr = string.Join(", ", f.AtLoopBottom.Select(ForPart));
                    _w.Line($"{probe}for ({init}; {cond}; {incr}) {{", file, line);
                    Body(f.Body);
                    _w.Line("}");
                    break;
                }
            case IForEachLoopOperation fe:
                {
                    var coll = fe.Collection;
                    while (coll is IConversionOperation cv) coll = cv.Operand;
                    if (coll.Type is not IArrayTypeSymbol)
                    {
                        Error(op, "foreach works over arrays only.");
                        break;
                    }
                    var v = fe.Locals.FirstOrDefault();
                    var vt = v != null ? TypeOf(v.Type, op.Syntax) : "auto";
                    _w.Line($"{probe}for ({vt} {Names.Cpp(v?.Name ?? "item")} : {Expr(coll).Text}) {{", file, line);
                    Body(fe.Body);
                    _w.Line("}");
                    break;
                }
            case ISwitchOperation sw:
                Switch(sw, probe, file, line);
                break;
            case IReturnOperation r:
                if (r.Kind != OperationKind.Return) { Error(op, "yield is not supported."); break; }
                _w.Line(probe + (r.ReturnedValue != null ? "return " + Expr(r.ReturnedValue).Text + ";" : "return;"), file, line);
                break;
            case IBranchOperation br:
                _w.Line(br.BranchKind switch
                {
                    BranchKind.Break => "break;",
                    BranchKind.Continue => "continue;",
                    _ => $"goto {Names.Cpp(br.Target.Name)};",
                }, file, line);
                break;
            case ILabeledOperation lab:
                _w.Line($"{Names.Cpp(lab.Label.Name)}:;", file, line);
                if (lab.Operation != null) Stmt(lab.Operation);
                break;
            case IEmptyOperation:
                break;
            case IUsingDeclarationOperation or IUsingOperation:
                Error(op, "using blocks are only allowed in [Cutscene] methods (using var _ = LockAll();).");
                break;
            case ITryOperation or IThrowOperation:
                Error(op, "Exceptions are not available on GBA (GAE004): remove try / throw.");
                break;
            case ILocalFunctionOperation:
                Error(op, "Local functions are not supported; make it a method of the class.");
                break;
            default:
                Error(op, $"This statement is not supported in GBA scripts ({op.Kind}).");
                break;
        }
    }

    void Body(IOperation? body)
    {
        _w.Indent++;
        if (body is IBlockOperation b) foreach (var s in b.Operations) Stmt(s);
        else if (body != null) Stmt(body);
        _w.Indent--;
    }

    void IfChain(IConditionalOperation c, string probe, string file, int line)
    {
        _w.Line($"{probe}if ({Cond(c.Condition)}) {{", file, line);
        Body(c.WhenTrue);
        var e = c.WhenFalse;
        while (e is IConditionalOperation elseIf && elseIf.Syntax is Microsoft.CodeAnalysis.CSharp.Syntax.IfStatementSyntax)
        {
            var (f2, l2) = Pos(elseIf);
            _w.Line($"}} else if ({Cond(elseIf.Condition)}) {{", f2, l2);
            Body(elseIf.WhenTrue);
            e = elseIf.WhenFalse;
        }
        if (e != null)
        {
            _w.Line("} else {");
            Body(e);
        }
        _w.Line("}");
    }

    void Switch(ISwitchOperation sw, string probe, string file, int line)
    {
        _w.Line($"{probe}switch ({Expr(sw.Value).Text}) {{", file, line);
        foreach (var section in sw.Cases)
        {
            foreach (var clause in section.Clauses)
            {
                switch (clause)
                {
                    case ISingleValueCaseClauseOperation sv:
                        _w.Line($"case {Constant(sv.Value.ConstantValue.Value, sw.Value.Type).Text}:", Pos(clause).File, Pos(clause).Line);
                        break;
                    case IDefaultCaseClauseOperation:
                        _w.Line("default:", Pos(clause).File, Pos(clause).Line);
                        break;
                    default:
                        Error(clause, "Only constant case labels are supported in switch.");
                        break;
                }
            }
            _w.Line("{");
            _w.Indent++;
            foreach (var s in section.Body) Stmt(s);
            _w.Indent--;
            _w.Line("}");
        }
        _w.Line("}");
    }

    string ForPart(IOperation op) => op switch
    {
        IExpressionStatementOperation es => ExprStatement(es.Operation),
        IVariableDeclarationGroupOperation g => DeclText(g),
        _ => ExprStatement(op),
    };

    string DeclText(IVariableDeclarationGroupOperation g)
    {
        var parts = new List<string>();
        string? type = null;
        foreach (var d in g.Declarations)
            foreach (var v in d.Declarators)
            {
                type ??= TypeOf(v.Symbol.Type, v.Syntax);
                var init = v.Initializer?.Value ?? d.Initializer?.Value;
                parts.Add(Names.Cpp(v.Symbol.Name) + " = " + (init != null ? Expr(init).Text : DefaultValue(v.Symbol.Type)));
            }
        return type + " " + string.Join(", ", parts);
    }

    void LocalDecl(IVariableDeclaratorOperation v, IVariableDeclarationOperation d, string probe, string file, int line)
    {
        var sym = v.Symbol;
        if (sym.IsRef) { Error(v, "ref locals are not supported."); return; }
        var init = v.Initializer?.Value ?? d.Initializer?.Value;
        if (sym.Type is IArrayTypeSymbol arr)
        {
            ArrayDecl(arr, Names.Cpp(sym.Name), init, probe, v.Syntax, file, line);
            return;
        }
        var t = TypeOf(sym.Type, v.Syntax);
        var value = init != null ? Expr(init).Text : DefaultValue(sym.Type);
        var unused = _used.Contains(sym) ? "" : "[[maybe_unused]] ";
        _w.Line($"{probe}{unused}{(sym.IsConst ? "constexpr " : "")}{t} {Names.Cpp(sym.Name)} = {value};", file, line);
    }

    string Cond(IOperation op) => Expr(op).Text;

    /// <summary>An expression used as a statement (setters and fixed ++ have statement-only forms).</summary>
    string ExprStatement(IOperation op)
    {
        switch (op)
        {
            case ISimpleAssignmentOperation a when a.Target is IPropertyReferenceOperation pr && SetterTemplate(pr) is { } setTemplate:
                return Expand(setTemplate, pr.Instance, ArgsOf(pr.Property.GetMethod ?? pr.Property.SetMethod!, pr.Arguments), null, Expr(a.Value), pr, useSet: true).Text;
            case ISimpleAssignmentOperation a when a.Target is IPropertyReferenceOperation pr && _m.IsUser(pr.Property.ContainingType) && !IsAutoProperty(pr.Property):
                return Receiver(pr.Instance, pr.Property.IsStatic, pr.Property.ContainingType) + "set_" + Names.Cpp(pr.Property.Name) + "(" + Expr(a.Value).Text + ")";
            case ICompoundAssignmentOperation ca when ca.Target is IPropertyReferenceOperation pr && (SetterTemplate(pr) != null || (_m.IsUser(pr.Property.ContainingType) && !IsAutoProperty(pr.Property))):
                {
                    var current = Expr(ca.Target);
                    var value = BinaryText(ca.OperatorKind, ca.OperatorMethod, current, Expr(ca.Value), ca);
                    if (SetterTemplate(pr) is { } st)
                        return Expand(st, pr.Instance, ArgsOf(pr.Property.GetMethod!, pr.Arguments), null, value, pr, useSet: true).Text;
                    return Receiver(pr.Instance, pr.Property.IsStatic, pr.Property.ContainingType) + "set_" + Names.Cpp(pr.Property.Name) + "(" + value.Text + ")";
                }
            case IIncrementOrDecrementOperation inc when inc.OperatorMethod != null || _m.IsFixed(inc.Target.Type):
                return Expr(inc.Target).At(Unary) + (inc.Kind == OperationKind.Increment ? " += " : " -= ") + (_m.IsFixed(inc.Target.Type) ? "agb::fixed(1)" : "1");
        }
        return Expr(op).Text;
    }

    CppMap? SetterTemplate(IPropertyReferenceOperation pr)
    {
        var map = ScriptModel.Cpp(pr.Property);
        return map?.Set != null ? map : null;
    }

    // =====================================================================================================
    // Expressions
    // =====================================================================================================

    CppExpr Expr(IOperation op)
    {
        switch (op)
        {
            case ILiteralOperation lit:
                return Constant(lit.ConstantValue.Value, lit.Type);
            case IConversionOperation conv:
                return Conversion(conv);
            case IParenthesizedOperation p:
                return Expr(p.Operand);
            case IBinaryOperation b:
                if (b.Type?.SpecialType == SpecialType.System_String && b.OperatorKind == BinaryOperatorKind.Add)
                {
                    if (b.ConstantValue.HasValue) return Constant(b.ConstantValue.Value, b.Type);
                    Error(op, "Strings cannot be joined at runtime on GBA. Use Debug.Log($\"...\") to show values, or string keys from Localization for text.");
                    return new CppExpr("\"\"", Primary);
                }
                if (b.OperatorMethod?.ContainingType.SpecialType == SpecialType.System_String || (b.LeftOperand.Type?.SpecialType == SpecialType.System_String && b.OperatorKind is BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals))
                    return StringCompare(b);
                return BinaryText(b.OperatorKind, b.OperatorMethod, Expr(b.LeftOperand), Expr(b.RightOperand), b);
            case IUnaryOperation u:
                {
                    if (u.ConstantValue.HasValue && u.OperatorMethod == null) return Constant(u.ConstantValue.Value, u.Type);
                    var map = u.OperatorMethod != null ? ScriptModel.Cpp(u.OperatorMethod) : null;
                    var e = Expr(u.Operand);
                    if (map != null) return Expand(map, null, [e], null, null, op);
                    var sym = u.OperatorKind switch
                    {
                        UnaryOperatorKind.Minus => "-",
                        UnaryOperatorKind.Plus => "+",
                        UnaryOperatorKind.Not => "!",
                        UnaryOperatorKind.BitwiseNegation => "~",
                        _ => null,
                    };
                    if (sym == null) { Error(op, $"The operator {u.OperatorKind} is not supported."); return e; }
                    return new CppExpr(sym + e.At(Unary), Unary);
                }
            case IConditionalOperation c:
                return new CppExpr($"{Expr(c.Condition).At(Or)} ? {Expr(c.WhenTrue!).At(CppExpr.Cond)} : {Expr(c.WhenFalse!).At(CppExpr.Cond)}", CppExpr.Cond);
            case ISimpleAssignmentOperation a:
                {
                    if (a.Target is IPropertyReferenceOperation pr && (SetterTemplate(pr) != null || (_m.IsUser(pr.Property.ContainingType) && !IsAutoProperty(pr.Property))))
                    {
                        if (a.Parent is IExpressionStatementOperation) return new CppExpr(ExprStatement(a), Assign);
                        Error(op, "This property can only be assigned in a statement of its own.");
                    }
                    var target = Expr(a.Target);
                    var value = AssetValue(a.Target, a.Value) ?? Expr(a.Value);
                    return new CppExpr($"{target.At(Unary)} = {value.At(Assign)}", Assign);
                }
            case ICompoundAssignmentOperation ca:
                {
                    if (ca.Target is IPropertyReferenceOperation pr2 && (SetterTemplate(pr2) != null || (_m.IsUser(pr2.Property.ContainingType) && !IsAutoProperty(pr2.Property))))
                    {
                        if (ca.Parent is IExpressionStatementOperation) return new CppExpr(ExprStatement(ca), Assign);
                        Error(op, "This property can only be changed in a statement of its own.");
                    }
                    if (ca.Target.Type?.SpecialType == SpecialType.System_String) { Error(op, "Strings cannot be joined at runtime on GBA."); return new CppExpr("0", Primary); }
                    var sym = OperatorSymbol(ca.OperatorKind, enumOp: false);
                    var target = Expr(ca.Target);
                    var value = Expr(ca.Value);
                    if (ca.OperatorMethod != null && ScriptModel.Cpp(ca.OperatorMethod) is { } m && m.Template.Contains("{1} * {0}"))
                        return new CppExpr($"{target.At(Unary)} = {BinaryText(ca.OperatorKind, ca.OperatorMethod, target, value, ca).At(Assign)}", Assign);
                    return new CppExpr($"{target.At(Unary)} {sym}= {value.At(Assign)}", Assign);
                }
            case IIncrementOrDecrementOperation inc:
                {
                    if (inc.OperatorMethod != null || _m.IsFixed(inc.Target.Type))
                    {
                        if (inc.Parent is IExpressionStatementOperation || inc.Parent is IForLoopOperation) return new CppExpr(ExprStatement(inc), Assign);
                        Error(op, "fixed ++ / -- can only be used as a statement of its own.");
                    }
                    var t = Expr(inc.Target).At(Unary);
                    var s = inc.Kind == OperationKind.Increment ? "++" : "--";
                    return inc.IsPostfix ? new CppExpr(t + s, Primary) : new CppExpr(s + t, Unary);
                }
            case IInvocationOperation inv:
                return Invocation(inv);
            case IPropertyReferenceOperation pr:
                return Property(pr);
            case IFieldReferenceOperation fr:
                return Field(fr);
            case ILocalReferenceOperation l:
                return new CppExpr(Names.Cpp(l.Local.Name), Primary);
            case IParameterReferenceOperation p:
                return new CppExpr(Names.Cpp(p.Parameter.Name), Primary, _refParams.Contains(p.Parameter));
            case IInstanceReferenceOperation:
                return new CppExpr("this", Primary);
            case IObjectCreationOperation oc:
                return ObjectCreation(oc);
            case IArrayElementReferenceOperation ae:
                if (ae.Indices.Length != 1) { Error(op, "Only one-dimensional arrays are supported."); return new CppExpr("0", Primary); }
                return new CppExpr($"{Expr(ae.ArrayReference).At(Primary)}[{Expr(ae.Indices[0]).Text}]", Primary);
            case IDefaultValueOperation dv:
                return new CppExpr(dv.Type != null ? DefaultValue(dv.Type) : "{}", Primary);
            case INameOfOperation or ISizeOfOperation when op.ConstantValue.HasValue:
                return Constant(op.ConstantValue.Value, op.Type);
            case IIsPatternOperation ip when ip.Pattern is IConstantPatternOperation { Value.ConstantValue: { HasValue: true, Value: null } }:
                return new CppExpr($"{Expr(ip.Value).At(Eq)} == nullptr", Eq);
            case IIsPatternOperation ip when ip.Pattern is INegatedPatternOperation { Pattern: IConstantPatternOperation { Value.ConstantValue: { HasValue: true, Value: null } } }:
                return new CppExpr($"{Expr(ip.Value).At(Eq)} != nullptr", Eq);
            case IAwaitOperation:
                Error(op, "await is only allowed in [Cutscene] async Task methods.");
                return new CppExpr("0", Primary);
            case IInterpolatedStringOperation:
                Error(op, "Interpolated strings can only be passed to Debug.Log on GBA.");
                return new CppExpr("\"\"", Primary);
            case IAnonymousFunctionOperation or IDelegateCreationOperation:
                Error(op, "Lambdas are not supported in translated scripts (GAE006). Use a method instead.");
                return new CppExpr("nullptr", Primary);
            case IIsTypeOperation or ITypeOfOperation:
                Error(op, "Type checks need RTTI, which GBA builds do not have. Use actor.Is<T>() or Get<T>() != null.");
                return new CppExpr("false", Primary);
            case IConditionalAccessOperation or ICoalesceOperation:
                Error(op, "?. and ?? are not supported; check for null with an if statement.");
                return new CppExpr("0", Primary);
            case IThrowOperation:
                Error(op, "Exceptions are not available on GBA (GAE004).");
                return new CppExpr("0", Primary);
            case IArrayCreationOperation or ICollectionExpressionOperation:
                Error(op, "Arrays can only be created where a field or local variable is declared (the GBA has no heap).");
                return new CppExpr("nullptr", Primary);
            default:
                Error(op, $"This expression is not supported in GBA scripts ({op.Kind}).");
                return new CppExpr("0", Primary);
        }
    }

    // ---- constants ----------------------------------------------------------------------------------

    CppExpr Constant(object? v, ITypeSymbol? type)
    {
        if (type is INamedTypeSymbol { TypeKind: TypeKind.Enum } e && v != null)
        {
            var member = e.GetMembers().OfType<IFieldSymbol>().FirstOrDefault(f => f.HasConstantValue && Equals(f.ConstantValue, Convert.ChangeType(v, f.ConstantValue!.GetType(), CultureInfo.InvariantCulture)));
            if (_m.IsUser(e)) _out.SourceDeps.Add(e.OriginalDefinition);
            if (member != null) return new CppExpr(_m.CppTypeName(e) + "::" + Names.Cpp(member.Name), Primary);
            return new CppExpr($"static_cast<{_m.CppTypeName(e)}>({Convert.ToInt64(v, CultureInfo.InvariantCulture)})", Primary);
        }
        switch (v)
        {
            case null: return new CppExpr("nullptr", Primary);
            case bool b: return new CppExpr(b ? "true" : "false", Primary);
            case string s: return new CppExpr(Names.CppString(s), Primary);
            case int i when i == int.MinValue: return new CppExpr("(-2147483647 - 1)", Primary);
            case int i: return new CppExpr(i.ToString(CultureInfo.InvariantCulture), i < 0 ? Unary : Primary);
            case uint u: return new CppExpr(u.ToString(CultureInfo.InvariantCulture) + "u", Primary);
            case long l: return new CppExpr(l.ToString(CultureInfo.InvariantCulture) + "LL", l < 0 ? Unary : Primary);
            case ulong ul: return new CppExpr(ul.ToString(CultureInfo.InvariantCulture) + "ULL", Primary);
            case float f: return new CppExpr(Names.Decimal(f) + "f", f < 0 ? Unary : Primary);
            case double d: return new CppExpr(Names.Decimal(d), d < 0 ? Unary : Primary);
            case char c: return new CppExpr(((int)c).ToString(CultureInfo.InvariantCulture), Primary);
            default:
                var n = Convert.ToInt64(v, CultureInfo.InvariantCulture);
                return new CppExpr(n.ToString(CultureInfo.InvariantCulture), n < 0 ? Unary : Primary);
        }
    }

    // ---- conversions --------------------------------------------------------------------------------

    CppExpr Conversion(IConversionOperation conv)
    {
        var operand = conv.Operand;
        var to = conv.Type;
        var from = operand.Type;
        if (conv.OperatorMethod != null)
        {
            if (_m.IsFixed(to) && from != null && from.SpecialType is SpecialType.System_Double or SpecialType.System_Single or SpecialType.System_Decimal)
            {
                if (!operand.ConstantValue.HasValue)
                {
                    _diag.Error("AGB0201", operand.Syntax.GetLocation(), "Only decimal constants convert to fixed (for example 1.5). The GBA has no FPU: keep the value in a fixed variable instead of a float or double (GAE002).");
                    return new CppExpr("agb::fixed()", Primary);
                }
                var d = Convert.ToDouble(operand.ConstantValue.Value, CultureInfo.InvariantCulture);
                if (Math.Abs(d) >= 8388608)
                    _diag.Error("AGB0202", operand.Syntax.GetLocation(), $"{d.ToString(CultureInfo.InvariantCulture)} is outside the fixed range (-8388608 to 8388607.99).");
                return new CppExpr($"agb::fixed::lit({Names.Decimal(d)})", Primary);
            }
            if (_m.IsFixed(to) && from != null && from.SpecialType is >= SpecialType.System_SByte and <= SpecialType.System_Int32)
                return new CppExpr($"agb::fixed({Expr(operand).Text})", Primary);
            var map = ScriptModel.Cpp(conv.OperatorMethod);
            if (map != null) return Expand(map, null, [Expr(operand)], null, null, conv);
            Error(conv, $"The conversion to '{to?.ToDisplayString()}' is not supported.");
            return Expr(operand);
        }
        if (to == null || from == null || conv.Conversion.IsIdentity) return Expr(operand);
        if (conv.ConstantValue.HasValue && (to.SpecialType != SpecialType.None || to.TypeKind == TypeKind.Enum)) return Constant(conv.ConstantValue.Value, to);
        if (operand.ConstantValue.HasValue && operand.ConstantValue.Value == null && to.IsReferenceType) return new CppExpr("nullptr", Primary);
        if (conv.Conversion.IsNumeric || to.TypeKind == TypeKind.Enum || from.TypeKind == TypeKind.Enum)
        {
            if (conv.Conversion.IsImplicit && to.SpecialType == SpecialType.System_Int32 &&
                from.SpecialType is SpecialType.System_Byte or SpecialType.System_SByte or SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Char)
                return Expr(operand);
            if (conv.Conversion.IsImplicit && to.SpecialType is SpecialType.System_Int64 && from.SpecialType == SpecialType.System_Int32)
                return new CppExpr($"static_cast<agb::s64>({Expr(operand).Text})", Primary);
            return new CppExpr($"static_cast<{TypeOf(to, conv.Syntax)}>({Expr(operand).Text})", Primary);
        }
        if (conv.Conversion.IsReference)
        {
            var e = Expr(operand);
            if (conv.Conversion.IsImplicit) return e.IsRef ? new CppExpr("&" + e.At(Unary), Unary) : e;
            return new CppExpr($"static_cast<{TypeOf(to, conv.Syntax)}>({(e.IsRef ? "&" + e.At(Unary) : e.Text)})", Primary);
        }
        if (to.SpecialType == SpecialType.System_Object || to.TypeKind == TypeKind.Interface || from.SpecialType == SpecialType.System_Object)
        {
            Error(conv, "Boxing (object, interfaces) is not available on GBA.");
            return Expr(operand);
        }
        return Expr(operand);
    }

    // ---- operators ----------------------------------------------------------------------------------

    static string OperatorSymbol(BinaryOperatorKind k, bool enumOp) => k switch
    {
        BinaryOperatorKind.Add => "+",
        BinaryOperatorKind.Subtract => "-",
        BinaryOperatorKind.Multiply => "*",
        BinaryOperatorKind.Divide => "/",
        BinaryOperatorKind.Remainder => "%",
        BinaryOperatorKind.LeftShift => "<<",
        BinaryOperatorKind.RightShift => ">>",
        BinaryOperatorKind.And => "&",
        BinaryOperatorKind.Or => "|",
        BinaryOperatorKind.ExclusiveOr => "^",
        BinaryOperatorKind.ConditionalAnd => "&&",
        BinaryOperatorKind.ConditionalOr => "||",
        BinaryOperatorKind.Equals => "==",
        BinaryOperatorKind.NotEquals => "!=",
        BinaryOperatorKind.LessThan => "<",
        BinaryOperatorKind.LessThanOrEqual => "<=",
        BinaryOperatorKind.GreaterThan => ">",
        BinaryOperatorKind.GreaterThanOrEqual => ">=",
        _ => "?",
    };

    static int OperatorPrec(BinaryOperatorKind k) => k switch
    {
        BinaryOperatorKind.Multiply or BinaryOperatorKind.Divide or BinaryOperatorKind.Remainder => Mul,
        BinaryOperatorKind.Add or BinaryOperatorKind.Subtract => CppExpr.Add,
        BinaryOperatorKind.LeftShift or BinaryOperatorKind.RightShift or BinaryOperatorKind.UnsignedRightShift => Shift,
        BinaryOperatorKind.LessThan or BinaryOperatorKind.LessThanOrEqual or BinaryOperatorKind.GreaterThan or BinaryOperatorKind.GreaterThanOrEqual => Rel,
        BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals => Eq,
        BinaryOperatorKind.And => BitAnd,
        BinaryOperatorKind.ExclusiveOr => Xor,
        BinaryOperatorKind.Or => BitOr,
        BinaryOperatorKind.ConditionalAnd => CppExpr.And,
        BinaryOperatorKind.ConditionalOr => Or,
        _ => Primary,
    };

    CppExpr BinaryText(BinaryOperatorKind kind, IMethodSymbol? method, CppExpr left, CppExpr right, IOperation at)
    {
        if (method != null && ScriptModel.Cpp(method) is { } map) return Expand(map, null, [left, right], null, null, at);
        if (kind == BinaryOperatorKind.UnsignedRightShift)
            return new CppExpr($"static_cast<agb::s32>(static_cast<agb::u32>({left.Text}) >> {right.At(Shift + 1)})", Primary);
        // Pointer comparisons with a reference parameter (OnTouch other) compare addresses.
        if (kind is BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals)
        {
            if (left.IsRef) left = new CppExpr("&" + left.At(Unary), Unary);
            if (right.IsRef) right = new CppExpr("&" + right.At(Unary), Unary);
        }
        var type = at switch { IBinaryOperation b => b.LeftOperand.Type, ICompoundAssignmentOperation c => c.Target.Type, _ => null };
        if (type is INamedTypeSymbol { TypeKind: TypeKind.Enum } e && kind is BinaryOperatorKind.And or BinaryOperatorKind.Or or BinaryOperatorKind.ExclusiveOr)
        {
            var et = _m.CppTypeName(e);
            var bt = _m.EnumBase(e);
            return new CppExpr($"static_cast<{et}>(static_cast<{bt}>({left.Text}) {OperatorSymbol(kind, true)} static_cast<{bt}>({right.Text}))", Primary);
        }
        var p = OperatorPrec(kind);
        // g++ -Wparentheses (an error with -Werror): binary operands of bitwise and shift operators, and && inside
        // ||, keep the parentheses the C# source had or its precedence implies.
        var need = kind is BinaryOperatorKind.And or BinaryOperatorKind.Or or BinaryOperatorKind.ExclusiveOr or
            BinaryOperatorKind.LeftShift or BinaryOperatorKind.RightShift ? Unary
            : kind == BinaryOperatorKind.ConditionalOr ? CppExpr.And + 1 : p;
        return new CppExpr($"{left.At(Math.Max(p, need))} {OperatorSymbol(kind, false)} {right.At(Math.Max(p + 1, need))}", p);
    }

    /// <summary>tag == "Coin" and other asset-name comparisons resolve the string at build time.</summary>
    CppExpr StringCompare(IBinaryOperation b)
    {
        var l = b.LeftOperand;
        var r = b.RightOperand;
        var kind = AssetKindOf(l) ?? AssetKindOf(r);
        if (kind == null)
        {
            Error(b, "Strings cannot be compared at runtime on GBA. Compare tags with actor.tag == \"Coin\", or use enums and ids.");
            return new CppExpr("false", Primary);
        }
        var left = AssetValue(r, l) ?? Expr(l);
        var right = AssetValue(l, r) ?? Expr(r);
        var sym = b.OperatorKind == BinaryOperatorKind.NotEquals ? "!=" : "==";
        return new CppExpr($"{left.At(Eq)} {sym} {right.At(Eq + 1)}", Eq);
    }

    /// <summary>Asset kind of a property / field reference or a method result marked [Asset] (Actor.tag, Scene.TagAt).</summary>
    static AssetKind? AssetKindOf(IOperation op)
    {
        while (op is IConversionOperation c) op = c.Operand;
        return op switch
        {
            IPropertyReferenceOperation p => ScriptModel.Asset(p.Property),
            IFieldReferenceOperation f => ScriptModel.Asset(f.Field),
            IInvocationOperation i => ScriptModel.Asset(i.TargetMethod),
            _ => null,
        };
    }

    /// <summary>When <paramref name="target"/> is an asset-name member, a constant string value becomes the asset id.</summary>
    CppExpr? AssetValue(IOperation target, IOperation value)
    {
        var kind = AssetKindOf(target);
        if (kind == null) return null;
        while (value is IConversionOperation c) value = c.Operand;
        if (value.ConstantValue is { HasValue: true, Value: string s })
        {
            var r = _m.Resolve(kind.Value, s, value.Syntax.GetLocation(), _diag);
            return new CppExpr(r?.Cpp ?? "0", Primary);
        }
        if (value.Type?.SpecialType == SpecialType.System_String && AssetKindOf(value) == null)
            Error(value, "This name must be a constant string so it can be turned into an id at build time.");
        return null;
    }

    // ---- members --------------------------------------------------------------------------------------

    /// <summary>Receiver text followed by -> or . (empty for implicit this or static members).</summary>
    string Receiver(IOperation? instance, bool isStatic, INamedTypeSymbol containing)
    {
        if (isStatic || instance == null)
        {
            if (_m.IsUser(containing)) _out.SourceDeps.Add(containing.OriginalDefinition);
            return _m.CppTypeName(containing) + "::";
        }
        if (instance is IInstanceReferenceOperation { IsImplicit: true }) return "";
        if (instance.Syntax is Microsoft.CodeAnalysis.CSharp.Syntax.BaseExpressionSyntax) return _m.CppTypeName(containing) + "::";
        var r = Expr(instance);
        if (instance.Type is INamedTypeSymbol n && _m.IsUser(n)) _out.SourceDeps.Add(n.OriginalDefinition);
        return r.At(Primary) + (r.IsRef || instance.Type?.IsValueType == true ? "." : "->");
    }

    CppExpr Field(IFieldReferenceOperation fr)
    {
        var f = fr.Field;
        var ct = f.ContainingType;
        if (ct.TypeKind == TypeKind.Enum)
        {
            if (_m.IsUser(ct)) _out.SourceDeps.Add(ct.OriginalDefinition);
            return new CppExpr(_m.CppTypeName(ct) + "::" + Names.Cpp(f.Name), Primary);
        }
        var map = ScriptModel.Cpp(f);
        if (map != null) return Expand(map, fr.Instance, [], null, null, fr);
        if (_m.IsUser(ct)) return new CppExpr(Receiver(fr.Instance, f.IsStatic, ct) + Names.Cpp(f.Name), Primary);
        if (f.IsConst && f.HasConstantValue) return Constant(f.ConstantValue, f.Type);
        if (ScriptModel.Cpp(ct) != null && !f.IsStatic) return new CppExpr(Receiver(fr.Instance, false, ct) + f.Name, Primary);
        Error(fr, $"'{ct.Name}.{f.Name}' is not available on GBA.");
        return new CppExpr("0", Primary);
    }

    CppExpr Property(IPropertyReferenceOperation pr)
    {
        var p = pr.Property;
        var ct = p.ContainingType;
        if (_m.IsUser(ct))
        {
            if (IsAutoProperty(p)) return new CppExpr(Receiver(pr.Instance, p.IsStatic, ct) + Names.Cpp(p.Name), Primary);
            if (p.GetMethod == null) { Error(pr, $"'{p.Name}' has no getter."); return new CppExpr("0", Primary); }
            return new CppExpr(Receiver(pr.Instance, p.IsStatic, ct) + "get_" + Names.Cpp(p.Name) + "()", Primary);
        }
        if (p.Name == "Length" && pr.Instance?.Type is IArrayTypeSymbol)
            return new CppExpr($"agb_countof({Expr(pr.Instance).Text})", Primary);
        var map = ScriptModel.Cpp(p);
        if (map != null) return Expand(map, pr.Instance, ArgsOf(p.GetMethod ?? p.SetMethod!, pr.Arguments), null, null, pr);
        Error(pr, $"'{ct.Name}.{p.Name}' is not available on GBA.");
        return new CppExpr("0", Primary);
    }

    CppExpr ObjectCreation(IObjectCreationOperation oc)
    {
        var type = oc.Type as INamedTypeSymbol;
        var ctor = oc.Constructor;
        if (type == null || ctor == null) { Error(oc, "This object cannot be created."); return new CppExpr("nullptr", Primary); }
        if (oc.Initializer != null && oc.Initializer.Initializers.Length > 0)
            Error(oc, "Object initializers { ... } are not supported; set the fields after creating the object.");
        var map = ScriptModel.Cpp(ctor);
        var args = ArgsOf(ctor, oc.Arguments);
        if (map != null) return Expand(map, null, args, null, null, oc);
        var name = _m.CppTypeName(type);
        if (_m.IsUser(type)) _out.SourceDeps.Add(type.OriginalDefinition);
        if (type.IsValueType)
            return new CppExpr(ctor.IsImplicitlyDeclared || args.Count == 0 ? name + "{}" : $"{name}({string.Join(", ", args.Select(a => a.Text))})", Primary);
        if (_m.DerivesFrom(type, _m.Behaviour))
        {
            _diag.Error("AGB0120", oc.Syntax.GetLocation(), $"Behaviours are created by the scene, not with new. Add {type.Name} to an actor in the Inspector, or use Get<{type.Name}>().");
            return new CppExpr("nullptr", Primary);
        }
        if (!_m.IsUser(type)) { Error(oc, $"'{type.Name}' cannot be created on GBA."); return new CppExpr("nullptr", Primary); }
        // Classes live in the scene's memory arena (no garbage collector): created once, freed when the scene ends.
        return new CppExpr($"agb::screenArena().make<{name}>({string.Join(", ", args.Select(a => a.Text))})", Primary);
    }

    List<CppExpr> ArgsOf(IMethodSymbol method, IEnumerable<IArgumentOperation> arguments) => Args(method, arguments);

    /// <summary>Arguments in parameter order; asset names are resolved to ids.</summary>
    List<CppExpr> Args(IMethodSymbol method, IEnumerable<IArgumentOperation> arguments)
    {
        var list = arguments.ToList();
        var result = new CppExpr[method.Parameters.Length];
        foreach (var a in list)
        {
            if (a.Parameter == null) continue;
            var i = a.Parameter.Ordinal;
            if (i >= result.Length) continue;
            var kind = ScriptModel.Asset(a.Parameter);
            if (kind != null)
            {
                var v = a.Value;
                while (v is IConversionOperation c) v = c.Operand;
                if (v.ConstantValue.HasValue && v.ConstantValue.Value == null) result[i] = new CppExpr(kind == AssetKind.ActorName ? "nullptr" : "-1", Unary);
                else if (v.ConstantValue is { HasValue: true, Value: string s })
                {
                    if (kind == AssetKind.ActorName) result[i] = new CppExpr(Names.CppString(s), Primary);
                    else result[i] = new CppExpr(_m.Resolve(kind.Value, s, v.Syntax.GetLocation(), _diag)?.Cpp ?? "0", Primary);
                }
                else
                {
                    _diag.Error("AGB0011", v.Syntax.GetLocation(), $"The {a.Parameter.Name} argument must be a constant string (for example \"jump\") so it can be turned into an id at build time.");
                    result[i] = new CppExpr("0", Primary);
                }
                continue;
            }
            var e = Expr(a.Value);
            // Reference parameters (OnTouch other) passed on as Actor pointers.
            if (e.IsRef && a.Parameter.Type.IsReferenceType) e = new CppExpr("&" + e.At(Unary), Unary);
            result[i] = e;
        }
        for (var i = 0; i < result.Length; i++)
            if (result[i].Text == null) result[i] = new CppExpr(DefaultValue(method.Parameters[i].Type), Primary);
        return result.ToList();
    }

    CppExpr Invocation(IInvocationOperation inv)
    {
        var m = inv.TargetMethod;
        if (ScriptModel.IsCutscene(m) && m.IsAsync) return StartCutscene(inv);
        if (m.Name == "Log" && m.ContainingType.ToDisplayString() == "GBA.Debug" && inv.Arguments.Length == 1 &&
            inv.Arguments[0].Value is IInterpolatedStringOperation interp)
            return LogInterpolated(interp);
        if (_m.IsUser(m.ContainingType))
        {
            if (m.MethodKind == MethodKind.LocalFunction) { Error(inv, "Local functions are not supported."); return new CppExpr("0", Primary); }
            var args = Args(m, inv.Arguments);
            var recv = Receiver(inv.Instance, m.IsStatic, m.ContainingType);
            return new CppExpr($"{recv}{MethodName(m)}({string.Join(", ", args.Select(a => a.Text))})", Primary);
        }
        var map = ScriptModel.Cpp(m);
        if (map != null)
        {
            var typeArg = m.TypeArguments.FirstOrDefault();
            return Expand(map, inv.Instance, Args(m, inv.Arguments), typeArg, null, inv);
        }
        if (ScriptModel.ScriptOp(m) != null)
            Error(inv, $"{m.Name} can only be used inside a [Cutscene] async Task method (it waits across frames).");
        else
            Error(inv, $"'{m.ContainingType.Name}.{m.Name}' is not available on GBA. Scripts can use the GBA API (Input, Sound, Scene, ...) and their own classes.");
        return new CppExpr("0", Primary);
    }

    CppExpr StartCutscene(IInvocationOperation inv)
    {
        var m = inv.TargetMethod;
        var id = _scriptId(m);
        _usesScripts = true;
        var args = new List<string>();
        if (!m.IsStatic && _m.DerivesFrom(m.ContainingType, _m.Behaviour))
        {
            var recv = inv.Instance is null or IInstanceReferenceOperation { IsImplicit: true } ? "" : Receiver(inv.Instance, false, m.ContainingType);
            args.Add($"{recv}actor->index");
        }
        foreach (var a in Args(m, inv.Arguments).Zip(m.Parameters))
            args.Add(_m.IsFixed(a.Second.Type) ? a.First.At(Primary) + ".raw" : a.First.Text);
        var name = $"agb_cutscene::{Names.Identifier(m.ContainingType.Name)}_{Names.Identifier(m.Name)}";
        if (id < 0) Error(inv, $"Cutscene {m.Name} was not compiled.");
        return new CppExpr($"agb_start_script({name}{string.Concat(args.Select(a => ", " + a))})", Primary);
    }

    CppExpr LogInterpolated(IInterpolatedStringOperation interp)
    {
        var fmt = new StringBuilder();
        var args = new List<string>();
        foreach (var part in interp.Parts)
        {
            switch (part)
            {
                case IInterpolatedStringTextOperation t:
                    fmt.Append(((string?)t.Text.ConstantValue.Value ?? "").Replace("%", "%%"));
                    break;
                case IInterpolationOperation ip:
                    {
                        var e = ip.Expression;
                        var t = e.Type;
                        var x = Expr(e);
                        if (_m.IsFixed(t)) { fmt.Append("%s"); args.Add($"agb_fixed_str({x.Text})"); }
                        else if (t?.SpecialType == SpecialType.System_Boolean) { fmt.Append("%s"); args.Add($"{x.At(Or)} ? \"true\" : \"false\""); }
                        else if (t?.SpecialType == SpecialType.System_String) { fmt.Append("%s"); args.Add(x.Text); }
                        else if (t?.TypeKind == TypeKind.Enum || t?.SpecialType is >= SpecialType.System_Char and <= SpecialType.System_UInt64)
                        { fmt.Append("%ld"); args.Add($"static_cast<long>({x.Text})"); }
                        else { Error(ip, "Debug.Log can show int, bool, fixed, enum and string values."); }
                        break;
                    }
            }
        }
        if (args.Count == 0) return new CppExpr($"agb::api::Debug::Log({Names.CppString(fmt.ToString().Replace("%%", "%"))})", Primary);
        return new CppExpr($"agb::api::Debug::Log({Names.CppString(fmt.ToString())}, {string.Join(", ", args)})", Primary);
    }

    // ---- templates ------------------------------------------------------------------------------------

    CppExpr Expand(CppMap map, IOperation? instance, IReadOnlyList<CppExpr> args, ITypeSymbol? typeArg, CppExpr? value, IOperation at, bool useSet = false)
    {
        var template = useSet ? map.Set! : map.Template;
        string thisDot = "", thisPtr = "this";
        if (instance != null && instance is not IInstanceReferenceOperation { IsImplicit: true })
        {
            var r = Expr(instance);
            var isValue = instance.Type?.IsValueType == true;
            thisDot = r.At(Primary) + (r.IsRef || isValue ? "." : "->");
            thisPtr = r.IsRef ? "&" + r.At(Unary) : r.At(Primary);
            if (instance.Type is INamedTypeSymbol n && _m.IsUser(n)) _out.SourceDeps.Add(n.OriginalDefinition);
        }
        var sb = new StringBuilder();
        for (var i = 0; i < template.Length; i++)
        {
            var c = template[i];
            if (c == '{')
            {
                var close = template.IndexOf('}', i + 1);
                if (close > i)
                {
                    var name = template[(i + 1)..close];
                    var next = close + 1 < template.Length ? template[close + 1] : '\0';
                    var member = next is '.' or '[' || (next == '-' && close + 2 < template.Length && template[close + 2] == '>');
                    string? text = null;
                    if (name == "this") text = thisPtr;
                    else if (name == "this.") text = thisDot;
                    else if (name == "value") text = value?.At(Assign);
                    else if (name == "T" && typeArg is INamedTypeSymbol ta)
                    {
                        if (_m.IsUser(ta)) _out.SourceDeps.Add(ta.OriginalDefinition);
                        text = _m.CppTypeName(ta);
                    }
                    else if (int.TryParse(name, out var ai) && ai < args.Count)
                    {
                        var a = args[ai];
                        text = member ? a.At(Primary) : map.Prec < Primary ? a.At(map.Prec + 1) : a.Text;
                    }
                    if (text != null)
                    {
                        sb.Append(text);
                        i = close;
                        continue;
                    }
                }
            }
            sb.Append(c);
        }
        return new CppExpr(sb.ToString(), useSet ? Assign : map.Prec);
    }
}
