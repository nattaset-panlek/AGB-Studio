using System.Globalization;
using AgbStudio.Scripting.Translation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace AgbStudio.Scripting.Bytecode;

/// <summary>One compiled Event Script (a [Cutscene] method or a dialogue conversation).</summary>
internal sealed class CompiledScript
{
    public required IMethodSymbol Method { get; init; }
    public required string Name { get; init; }      // "Forest.RescueIntro"
    public required string Symbol { get; init; }    // agb_cutscene::Forest_RescueIntro
    public int Id { get; set; }
    public bool HasSelf { get; set; }
    public byte[] Code { get; set; } = [];
    public int Locals { get; set; }
    public int Args { get; set; }
    public string File { get; set; } = "";
    public int Line { get; set; }
    public List<(int Pc, int Line)> Lines { get; set; } = new();
}

/// <summary>Scripts, natives and the names table of one build.</summary>
internal sealed class ScriptTables
{
    public List<CompiledScript> Scripts { get; } = new();
    public List<NativeInfo> Natives { get; } = new();
    public List<IMethodSymbol> UserNatives { get; } = new();
    public List<string> Names { get; } = new();
    public int BuiltinCount { get; set; }

    public int NativeId(string name) => Natives.FindIndex(n => n.Name == name);

    public int NameIndex(string s)
    {
        var i = Names.IndexOf(s);
        if (i >= 0) return i;
        Names.Add(s);
        return Names.Count - 1;
    }

    public CompiledScript? Find(IMethodSymbol m) => Scripts.FirstOrDefault(s => SymbolEqualityComparer.Default.Equals(s.Method, m.OriginalDefinition));
}

/// <summary>Compiles <c>[Cutscene] async Task</c> methods to Event Script VM bytecode (opcodes from script_ops.def).</summary>
internal sealed class BytecodeCompiler
{
    public const int MaxLocals = 16;   // agb::script::kMaxLocals
    public const int MaxStack = 24;    // agb::script::kStackSize
    public const int MaxNativeArgs = 8;

    readonly ScriptModel _m;
    readonly DiagnosticSink _diag;
    readonly OpcodeTable _ops;
    readonly ScriptTables _tables;

    ScriptAssembler _a = null!;
    readonly Dictionary<ISymbol, int> _slots = new(SymbolEqualityComparer.Default);
    readonly Dictionary<ILabelSymbol, int> _labels = new(SymbolEqualityComparer.Default);
    readonly Stack<(int Break, int Continue)> _loops = new();
    int _locks;
    int _nextSlot;
    bool _hasSelf;
    CompiledScript _script = null!;

    public BytecodeCompiler(ScriptModel m, DiagnosticSink diag, OpcodeTable ops, ScriptTables tables)
    {
        _m = m;
        _diag = diag;
        _ops = ops;
        _tables = tables;
    }

    void Error(IOperation op, string message) => _diag.Error("AGB0300", op.Syntax.GetLocation(), message);
    void Error(SyntaxNode node, string message) => _diag.Error("AGB0300", node.GetLocation(), message);

    public void Compile(CompiledScript script)
    {
        _script = script;
        _a = new ScriptAssembler(_ops);
        _slots.Clear();
        _labels.Clear();
        _loops.Clear();
        _locks = 0;
        _nextSlot = 0;
        var m = script.Method;
        var syntax = m.DeclaringSyntaxReferences[0].GetSyntax();
        var span = syntax.GetLocation().GetMappedLineSpan();
        script.File = _m.DisplayPath(span.Path);
        script.Line = span.StartLinePosition.Line + 1;
        _hasSelf = script.HasSelf;
        if (_hasSelf) _nextSlot = 1;  // slot 0: the actor slot of the behaviour (actor->index)
        foreach (var p in m.Parameters)
        {
            if (!IsScalar(p.Type)) Error(syntax, $"Cutscene parameter '{p.Name}' must be int, bool, fixed or an enum.");
            Slot(p);
        }
        script.Args = _nextSlot;
        var op = _m.Compilation.GetSemanticModel(syntax.SyntaxTree).GetOperation(syntax);
        var body = (op as IMethodBodyBaseOperation)?.BlockBody ?? (op as IMethodBodyBaseOperation)?.ExpressionBody;
        if (body == null) { Error(syntax, "This cutscene has no body, so there is nothing to run. Add a body { ... }, or remove [Cutscene]."); return; }
        foreach (var s in body.Operations) Stmt(s);
        for (; _locks > 0; _locks--) Unlock();
        try
        {
            script.Code = _a.Finish();
        }
        catch (ScriptAssembler.TooLongException)
        {
            Error(syntax, $"Cutscene {m.Name} is too long to compile: a jump in it spans more than 32 KB of code. Split it into smaller cutscenes.");
            script.Code = [];
        }
        script.Locals = Math.Max(_nextSlot, script.Args);
        script.Lines = _a.Lines;
        if (script.Locals > MaxLocals)
            Error(syntax, $"Cutscene {m.Name} uses {script.Locals} variables; the Event Script VM allows {MaxLocals}. Split it into smaller cutscenes.");
        if (_a.MaxDepth > MaxStack)
            Error(syntax, $"Cutscene {m.Name} needs {_a.MaxDepth} stack entries; the VM has {MaxStack}. Simplify long expressions.");
    }

    bool IsScalar(ITypeSymbol? t) => t != null && (t.SpecialType is SpecialType.System_Int32 or SpecialType.System_Boolean or SpecialType.System_Int16
        or SpecialType.System_Byte or SpecialType.System_UInt16 or SpecialType.System_SByte || t.TypeKind == TypeKind.Enum || _m.IsFixed(t));

    int Line(IOperation op) => op.Syntax.GetLocation().GetMappedLineSpan().StartLinePosition.Line + 1;

    int Slot(ISymbol s)
    {
        if (_slots.TryGetValue(s, out var i)) return i;
        _slots[s] = i = _nextSlot++;
        return i;
    }

    int Label(ILabelSymbol l)
    {
        if (!_labels.TryGetValue(l, out var id)) _labels[l] = id = _a.NewLabel();
        return id;
    }

    void Unlock()
    {
        _a.Native(_tables.NativeId("UnlockAll"), 0);
        _a.Op("Pop", -1);
    }

    // ---- statements -------------------------------------------------------------------------------------

    void Stmt(IOperation op)
    {
        if (_m.Ctx.DebugBuild && !op.IsImplicit && op is not IBlockOperation and not ILabeledOperation) _a.Line(Line(op));
        switch (op)
        {
            case IBlockOperation b:
                {
                    var locks = _locks;
                    foreach (var s in b.Operations) Stmt(s);
                    for (; _locks > locks; _locks--) Unlock();
                    break;
                }
            case IExpressionStatementOperation es:
                Effect(es.Operation);
                break;
            case IVariableDeclarationGroupOperation g:
                foreach (var d in g.Declarations)
                    foreach (var v in d.Declarators)
                    {
                        if (!IsScalar(v.Symbol.Type)) { Error(v, $"Cutscene variables must be int, bool, fixed or an enum ('{v.Symbol.Name}')."); continue; }
                        var init = v.Initializer?.Value ?? d.Initializer?.Value;
                        if (init != null) Value(init);
                        else _a.Push(0);
                        _a.StoreLocal(Slot(v.Symbol));
                    }
                break;
            case IUsingDeclarationOperation ud:
                foreach (var d in ud.DeclarationGroup.Declarations)
                    foreach (var v in d.Declarators)
                    {
                        var init = v.Initializer?.Value ?? d.Initializer?.Value;
                        if (init == null || !IsLock(init)) { Error(v, "Only using var _ = LockAll(); is supported in cutscenes."); continue; }
                        Effect(init);
                        _locks++;
                    }
                break;
            case IUsingOperation u:
                {
                    var res = u.Resources is IVariableDeclarationGroupOperation rg ? rg.Declarations.SelectMany(d => d.Declarators).Select(v => v.Initializer?.Value).FirstOrDefault() : u.Resources;
                    if (res == null || !IsLock(res)) { Error(u, "Only using (LockAll()) { ... } is supported in cutscenes."); break; }
                    Effect(res);
                    _locks++;
                    Stmt(u.Body);
                    _locks--;
                    Unlock();
                    break;
                }
            case IConditionalOperation c:
                {
                    var other = _a.NewLabel();
                    var end = _a.NewLabel();
                    Value(c.Condition);
                    _a.Jump("JumpIfFalse", other);
                    Stmt(c.WhenTrue);
                    if (c.WhenFalse != null)
                    {
                        _a.Jump("Jump", end);
                        _a.Mark(other);
                        Stmt(c.WhenFalse);
                        _a.Mark(end);
                    }
                    else
                    {
                        _a.Mark(other);
                        _a.Mark(end);
                    }
                    break;
                }
            case IWhileLoopOperation w:
                {
                    var start = _a.NewLabel();
                    var cont = _a.NewLabel();
                    var end = _a.NewLabel();
                    _loops.Push((end, cont));
                    _a.Mark(start);
                    if (w.ConditionIsTop)
                    {
                        _a.Mark(cont);
                        if (w.Condition != null) { Value(w.Condition); _a.Jump("JumpIfFalse", end); }
                        Stmt(w.Body);
                        _a.Jump("Jump", start);
                    }
                    else
                    {
                        Stmt(w.Body);
                        _a.Mark(cont);
                        Value(w.Condition!);
                        _a.Jump("JumpIfTrue", start);
                    }
                    _a.Mark(end);
                    _loops.Pop();
                    break;
                }
            case IForLoopOperation f:
                {
                    foreach (var s in f.Before) Stmt(s);
                    var start = _a.NewLabel();
                    var cont = _a.NewLabel();
                    var end = _a.NewLabel();
                    _loops.Push((end, cont));
                    _a.Mark(start);
                    if (f.Condition != null) { Value(f.Condition); _a.Jump("JumpIfFalse", end); }
                    Stmt(f.Body);
                    _a.Mark(cont);
                    foreach (var s in f.AtLoopBottom) Stmt(s);
                    _a.Jump("Jump", start);
                    _a.Mark(end);
                    _loops.Pop();
                    break;
                }
            case ISwitchOperation sw:
                Switch(sw);
                break;
            case IReturnOperation r:
                if (r.ReturnedValue != null) Value(r.ReturnedValue);
                else _a.Push(0);
                for (var i = 0; i < _locks; i++)
                {
                    _a.Native(_tables.NativeId("UnlockAll"), 0);
                    _a.Op("Pop", -1);
                }
                _a.Op("Ret", -1);
                break;
            case IBranchOperation br:
                if (br.BranchKind == BranchKind.Break && _loops.Count > 0) _a.Jump("Jump", _loops.Peek().Break);
                else if (br.BranchKind == BranchKind.Continue && _loops.Count > 0) _a.Jump("Jump", _loops.Peek().Continue);
                else if (br.BranchKind == BranchKind.GoTo) _a.Jump("Jump", Label(br.Target));
                else Error(op, "break / continue is outside a loop, so there is nothing to leave. Move it into a loop, or use return to end the cutscene.");
                break;
            case ILabeledOperation lab:
                _a.Mark(Label(lab.Label));
                if (lab.Operation != null) Stmt(lab.Operation);
                break;
            case IEmptyOperation:
                break;
            case IForEachLoopOperation:
                Error(op, "foreach is not available in cutscenes; use a for loop with a counter.");
                break;
            default:
                Error(op, "This kind of statement can't run in a cutscene. Cutscenes run if, while, for, switch, return, assignments and calls; do the rest in a [ScriptNative] method and call that.");
                break;
        }
    }

    void Switch(ISwitchOperation sw)
    {
        var tmp = _nextSlot++;
        Value(sw.Value);
        _a.StoreLocal(tmp);
        var end = _a.NewLabel();
        var bodies = sw.Cases.Select(_ => _a.NewLabel()).ToList();
        var defaultIndex = -1;
        for (var i = 0; i < sw.Cases.Length; i++)
            foreach (var clause in sw.Cases[i].Clauses)
            {
                if (clause is IDefaultCaseClauseOperation) { defaultIndex = i; continue; }
                if (clause is not ISingleValueCaseClauseOperation sv || !sv.Value.ConstantValue.HasValue) { Error(clause, "Only constant case labels are supported."); continue; }
                _a.LoadLocal(tmp);
                _a.Push(IntConstant(sv.Value.ConstantValue.Value, sv.Value.Type));
                _a.Op("Eq", -1);
                _a.Jump("JumpIfTrue", bodies[i]);
            }
        _a.Jump("Jump", defaultIndex >= 0 ? bodies[defaultIndex] : end);
        _loops.Push((end, _loops.Count > 0 ? _loops.Peek().Continue : end));
        for (var i = 0; i < sw.Cases.Length; i++)
        {
            _a.Mark(bodies[i]);
            foreach (var s in sw.Cases[i].Body) Stmt(s);
        }
        _loops.Pop();
        _a.Mark(end);
    }

    bool IsLock(IOperation op)
    {
        while (op is IConversionOperation c) op = c.Operand;
        return op is IInvocationOperation inv && ScriptModel.ScriptOp(inv.TargetMethod) == "lock";
    }

    // ---- expressions ------------------------------------------------------------------------------------

    void Value(IOperation op)
    {
        if (!Emit(op))
        {
            Error(op, "This expression gives no value a cutscene can use (for example a call that returns nothing). Use it as a statement of its own.");
            _a.Push(0);
        }
    }

    void Effect(IOperation op)
    {
        if (Emit(op)) _a.Op("Pop", -1);
    }

    int IntConstant(object? v, ITypeSymbol? t)
    {
        if (v == null) return t?.IsReferenceType == true ? -3 : -1;
        if (v is bool b) return b ? 1 : 0;
        if (v is double or float) return (int)Math.Round(Convert.ToDouble(v, CultureInfo.InvariantCulture) * 256);
        return Convert.ToInt32(v, CultureInfo.InvariantCulture);
    }

    /// <summary>Emits an expression; returns true when it left one value on the stack.</summary>
    bool Emit(IOperation op)
    {
        switch (op)
        {
            case ILiteralOperation lit:
                if (lit.ConstantValue.Value is string) { Error(op, "Strings are only allowed as names here (string keys, sounds, scenes)."); _a.Push(0); return true; }
                _a.Push(IntConstant(lit.ConstantValue.Value, lit.Type));
                return true;
            case IConversionOperation conv:
                return Conversion(conv);
            case IBinaryOperation b:
                return Binary(b);
            case IUnaryOperation u:
                if (u.ConstantValue.HasValue && u.OperatorMethod == null) { _a.Push(IntConstant(u.ConstantValue.Value, u.Type)); return true; }
                Value(u.Operand);
                switch (u.OperatorKind)
                {
                    case UnaryOperatorKind.Minus: _a.Op("Neg"); break;
                    case UnaryOperatorKind.Not: _a.Op("Not"); break;
                    case UnaryOperatorKind.Plus: break;
                    default: Error(op, $"The operator {ScriptModel.OperatorText(u.OperatorKind)} is not available in cutscenes. Do this step in a [ScriptNative] method and call that."); break;
                }
                return true;
            case IConditionalOperation c:
                {
                    var other = _a.NewLabel();
                    var end = _a.NewLabel();
                    Value(c.Condition);
                    _a.Jump("JumpIfFalse", other);
                    var depth = _a.Depth;
                    Value(c.WhenTrue);
                    _a.Jump("Jump", end);
                    _a.Mark(other);
                    _a.SetDepth(depth);
                    Value(c.WhenFalse!);
                    _a.Mark(end);
                    return true;
                }
            case ISimpleAssignmentOperation a:
                return Assign(a.Target, () => Value(a.Value), op.Parent is not IExpressionStatementOperation);
            case ICompoundAssignmentOperation ca:
                return Assign(ca.Target, () =>
                {
                    Load(ca.Target);
                    Value(ca.Value);
                    Arith(ca.OperatorKind, ca.OperatorMethod, ca);
                }, op.Parent is not IExpressionStatementOperation);
            case IIncrementOrDecrementOperation inc:
                {
                    var want = op.Parent is not IExpressionStatementOperation and not IForLoopOperation;
                    if (want && inc.IsPostfix) { Error(op, "Use x++ as a statement of its own in cutscenes."); }
                    return Assign(inc.Target, () =>
                    {
                        Load(inc.Target);
                        _a.Push(_m.IsFixed(inc.Target.Type) ? 256 : 1);
                        _a.Op(inc.Kind == OperationKind.Increment ? "Add" : "Sub", -1);
                    }, want);
                }
            case ILocalReferenceOperation or IParameterReferenceOperation or IPropertyReferenceOperation or IFieldReferenceOperation:
                Load(op);
                return true;
            case IInvocationOperation inv:
                return Invocation(inv);
            case IAwaitOperation aw:
                return Emit(aw.Operation);
            case IDefaultValueOperation:
                _a.Push(0);
                return true;
            default:
                Error(op, "This kind of expression can't run in a cutscene. Compute it in a [ScriptNative] method and call that.");
                _a.Push(0);
                return true;
        }
    }

    bool Conversion(IConversionOperation conv)
    {
        var from = conv.Operand.Type;
        if (conv.OperatorMethod != null && _m.IsFixed(conv.Type))
        {
            if (from?.SpecialType is SpecialType.System_Double or SpecialType.System_Single)
            {
                if (!conv.Operand.ConstantValue.HasValue) { Error(conv, "Only decimal constants convert to fixed in cutscenes (for example 1.5). Keep the value in a fixed variable instead."); _a.Push(0); return true; }
                _a.Push((int)Math.Round(Convert.ToDouble(conv.Operand.ConstantValue.Value, CultureInfo.InvariantCulture) * 256));
                return true;
            }
            if (conv.Operand.ConstantValue.HasValue)
            {
                _a.Push(Convert.ToInt32(conv.Operand.ConstantValue.Value, CultureInfo.InvariantCulture) * 256);
                return true;
            }
            Value(conv.Operand);
            _a.Push(256);
            _a.Op("Mul", -1);
            return true;
        }
        if (conv.OperatorMethod != null && _m.IsFixed(from) && conv.Type?.SpecialType == SpecialType.System_Int32)
        {
            Value(conv.Operand);
            _a.Push(256);
            _a.Op("Div", -1);
            return true;
        }
        if (conv.ConstantValue.HasValue && conv.ConstantValue.Value is not string)
        {
            _a.Push(IntConstant(conv.ConstantValue.Value, conv.Type));
            return true;
        }
        if (conv.Operand.ConstantValue is { HasValue: true, Value: null })
        {
            _a.Push(-3);
            return true;
        }
        return Emit(conv.Operand);
    }

    bool HasCall(IOperation op) => op.DescendantsAndSelf().Any(o => o is IInvocationOperation);

    bool Binary(IBinaryOperation b)
    {
        if (b.ConstantValue.HasValue && b.ConstantValue.Value is not string)
        {
            _a.Push(IntConstant(b.ConstantValue.Value, b.Type));
            return true;
        }
        if (b.OperatorKind is BinaryOperatorKind.ConditionalAnd or BinaryOperatorKind.ConditionalOr)
        {
            Value(b.LeftOperand);
            if (!HasCall(b.RightOperand))
            {
                Value(b.RightOperand);
                _a.Op(b.OperatorKind == BinaryOperatorKind.ConditionalAnd ? "And" : "Or", -1);
                return true;
            }
            // Short circuit when the right side calls a native or another script.
            var end = _a.NewLabel();
            _a.Op("Dup", 1);
            _a.Jump(b.OperatorKind == BinaryOperatorKind.ConditionalAnd ? "JumpIfFalse" : "JumpIfTrue", end);
            _a.Op("Pop", -1);
            Value(b.RightOperand);
            _a.Op("Not");
            _a.Op("Not");
            _a.Mark(end);
            return true;
        }
        if (b.LeftOperand.Type?.SpecialType == SpecialType.System_String) { Error(b, "Strings cannot be compared in cutscenes. Compare enums, ids or flags instead."); _a.Push(0); return true; }
        Value(b.LeftOperand);
        Value(b.RightOperand);
        Arith(b.OperatorKind, b.OperatorMethod, b);
        return true;
    }

    void Arith(BinaryOperatorKind kind, IMethodSymbol? method, IOperation at)
    {
        var fixedMath = method != null && method.Parameters.Length == 2 && _m.IsFixed(method.Parameters[0].Type) && _m.IsFixed(method.Parameters[1].Type);
        string? name = kind switch
        {
            BinaryOperatorKind.Add => "Add",
            BinaryOperatorKind.Subtract => "Sub",
            BinaryOperatorKind.Multiply => fixedMath ? "FMul" : "Mul",
            BinaryOperatorKind.Divide => fixedMath ? "FDiv" : "Div",
            BinaryOperatorKind.Remainder => "Mod",
            BinaryOperatorKind.Equals => "Eq",
            BinaryOperatorKind.NotEquals => "Ne",
            BinaryOperatorKind.LessThan => "Lt",
            BinaryOperatorKind.LessThanOrEqual => "Le",
            BinaryOperatorKind.GreaterThan => "Gt",
            BinaryOperatorKind.GreaterThanOrEqual => "Ge",
            BinaryOperatorKind.And when at.Type?.SpecialType == SpecialType.System_Boolean || (at as ICompoundAssignmentOperation)?.Target.Type?.SpecialType == SpecialType.System_Boolean => "And",
            BinaryOperatorKind.Or when at.Type?.SpecialType == SpecialType.System_Boolean || (at as ICompoundAssignmentOperation)?.Target.Type?.SpecialType == SpecialType.System_Boolean => "Or",
            BinaryOperatorKind.ExclusiveOr when at.Type?.SpecialType == SpecialType.System_Boolean => "Ne",
            _ => null,
        };
        if (name == null) { Error(at, $"The operator {ScriptModel.OperatorText(kind)} is not available in cutscenes. Do this step in a [ScriptNative] method and call that."); return; }
        _a.Op(name, -1);
    }

    // ---- variables ------------------------------------------------------------------------------------

    /// <summary>Where a value lives: a local slot, a VM store or a game state field.</summary>
    (string Kind, int Id)? Place(IOperation target)
    {
        switch (target)
        {
            case ILocalReferenceOperation l: return ("local", Slot(l.Local));
            case IParameterReferenceOperation p: return ("local", Slot(p.Parameter));
            case IPropertyReferenceOperation pr: return StorePlace(pr.Property, target);
            case IFieldReferenceOperation fr: return StorePlace(fr.Field, target);
        }
        return null;
    }

    (string Kind, int Id)? StorePlace(ISymbol s, IOperation at)
    {
        var map = ScriptModel.ScriptOp(s);
        if (map == null) return null;
        var parts = map.Split(':');
        if (parts.Length == 2 && parts[0] is "var" or "flag" or "game" && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            return (parts[0], id);
        if (map == "actor:player") return ("player", 0);
        if (map == "actor:self") return ("self", 0);
        return null;
    }

    void Load(IOperation op)
    {
        var place = Place(op);
        if (place == null)
        {
            if (op.ConstantValue.HasValue && op.ConstantValue.Value is not string) { _a.Push(IntConstant(op.ConstantValue.Value, op.Type)); return; }
            if (op is IFieldReferenceOperation { Field.ContainingType.TypeKind: TypeKind.Enum } ef && ef.Field.HasConstantValue)
            {
                _a.Push(IntConstant(ef.Field.ConstantValue, ef.Type));
                return;
            }
            Error(op, $"Cutscenes can't read '{ScriptModel.Excerpt(op.Syntax)}'. They can read their own variables, parameters, Story / Flags variables, Game fields and constants; read anything else in a [ScriptNative] method.");
            _a.Push(0);
            return;
        }
        switch (place.Value.Kind)
        {
            case "local": _a.LoadLocal(place.Value.Id); break;
            case "var": _a.LoadVar(place.Value.Id); break;
            case "flag": _a.GetFlag(place.Value.Id); break;
            case "game":
                _a.Push(place.Value.Id);
                _a.Native(_tables.NativeId("GameGet"), 1);
                break;
            case "player": _a.Push(-1); break;
            case "self":
                if (!_hasSelf) { Error(op, "actor is only available in cutscenes declared in a Behaviour."); _a.Push(-3); }
                else _a.LoadLocal(0);
                break;
        }
    }

    bool Assign(IOperation target, Action value, bool want)
    {
        var place = Place(target);
        if (place == null || place.Value.Kind is "player" or "self")
        {
            Error(target, $"Cutscenes can't assign '{ScriptModel.Excerpt(target.Syntax)}'. They can assign their own variables, Story / Flags variables and Game fields; change anything else in a [ScriptNative] method.");
            value();
            return true;
        }
        if (place.Value.Kind == "game")
        {
            _a.Push(place.Value.Id);
            value();
            _a.Native(_tables.NativeId("GameSet"), 2);
            if (want) return true;
            _a.Op("Pop", -1);
            return false;
        }
        value();
        if (want) _a.Op("Dup", 1);
        switch (place.Value.Kind)
        {
            case "local": _a.StoreLocal(place.Value.Id); break;
            case "var": _a.StoreVar(place.Value.Id); break;
            case "flag": _a.SetFlag(place.Value.Id); break;
        }
        return want;
    }

    // ---- calls --------------------------------------------------------------------------------------------

    bool Invocation(IInvocationOperation inv)
    {
        var m = inv.TargetMethod;
        if (ScriptModel.IsCutscene(m) && m.IsAsync)
        {
            var callee = _tables.Find(m);
            if (callee == null) { Error(inv, $"Cutscene {m.Name} could not be compiled (see its own error), so it can't be called here. Fix that error first."); _a.Push(0); return true; }
            var argc = 0;
            if (callee.HasSelf)
            {
                if (!_hasSelf || inv.Instance is not IInstanceReferenceOperation) { Error(inv, $"{m.Name} belongs to a behaviour; call it from a cutscene of the same behaviour."); }
                _a.LoadLocal(0);
                argc++;
            }
            foreach (var a in Ordered(m, inv.Arguments)) { Value(a.Value); argc++; }
            _a.Call(callee.Id, argc);
            return true;
        }
        if (ScriptModel.NativeWaits(m) != null)
        {
            var id = _tables.NativeId("User." + m.ContainingType.Name + "." + m.Name);
            var args = Ordered(m, inv.Arguments).ToList();
            foreach (var a in args) Value(a.Value);
            _a.Native(id, args.Count);
            return true;
        }
        var map = ScriptModel.ScriptOp(m);
        if (m.Name == "Log" && m.ContainingType.ToDisplayString() == "GBA.Debug")
        {
            var arg = inv.Arguments[0].Value;
            while (arg is IConversionOperation c) arg = c.Operand;
            if (arg.ConstantValue.Value is not string text) { Error(inv, "Debug.Log in a cutscene takes a constant string."); text = ""; }
            _a.Push(_tables.NameIndex(text));
            _a.Native(_tables.NativeId("Log"), 1);
            return true;
        }
        if (map == null)
        {
            Error(inv, $"'{m.ContainingType.Name}.{m.Name}' cannot be called from a cutscene. Cutscenes can call the cutscene API (Say, Choice, Move, Wait, ...), other cutscenes and [ScriptNative] methods.");
            _a.Push(0);
            return true;
        }
        if (map == "lock")
        {
            _a.Native(_tables.NativeId("LockAll"), 0);
            return true;
        }
        if (map == "waituntil")
        {
            WaitUntil(inv);
            return false;
        }
        var parts = map.Split(':');
        if (parts[0] == "op")
        {
            var count = 0;
            foreach (var a in Ordered(m, inv.Arguments)) { Argument(a); count++; }
            _a.Op(parts[1], -count);
            return false;
        }
        if (parts[0] == "native")
        {
            var id = _tables.NativeId(parts[1]);
            if (id < 0) { Error(inv, $"{m.ContainingType.Name}.{m.Name} is not available in this version of the engine, so a cutscene can't call it. Update AGB Studio, or report this as a bug."); _a.Push(0); return true; }
            var argc = 0;
            for (var i = 2; i < parts.Length; i++) { _a.Push(int.Parse(parts[i], CultureInfo.InvariantCulture)); argc++; }
            foreach (var a in Ordered(m, inv.Arguments)) argc += Argument(a);
            if (argc > MaxNativeArgs) Error(inv, $"{m.Name} takes at most {MaxNativeArgs} values in a cutscene.");
            _a.Native(id, argc);
            return true;
        }
        Error(inv, $"'{m.ContainingType.Name}.{m.Name}' cannot be called from a cutscene. Cutscenes can call the cutscene API (Say, Choice, Move, Wait, ...), other cutscenes and [ScriptNative] methods.");
        _a.Push(0);
        return true;
    }

    /// <summary>
    /// <c>await WaitUntil(() =&gt; cond)</c>: the lambda body is compiled in place (it is never a delegate at run time),
    /// followed by WaitUntil, which jumps back to the condition and yields until the next frame while it is false.
    /// </summary>
    void WaitUntil(IInvocationOperation inv)
    {
        IOperation? arg = inv.Arguments.Length == 1 ? inv.Arguments[0].Value : null;
        while (arg is IConversionOperation c) arg = c.Operand;
        if (arg is IDelegateCreationOperation dc) arg = dc.Target;
        if (arg is not IAnonymousFunctionOperation fn)
        {
            Error(inv, "WaitUntil takes a lambda written in place: await WaitUntil(() => Story.metOwl);");
            return;
        }
        var body = fn.Body.Operations;
        var ret = body.Length == 1 ? body[0] as IReturnOperation : null;
        if (ret?.ReturnedValue == null)
        {
            Error(fn, "The WaitUntil condition must be one expression: () => Game.coins >= 10.");
            return;
        }
        var check = _a.NewLabel();
        _a.Mark(check);
        Value(ret.ReturnedValue);
        _a.Jump("WaitUntil", check);
    }

    static IEnumerable<IArgumentOperation> Ordered(IMethodSymbol m, IEnumerable<IArgumentOperation> args) =>
        args.Where(a => a.Parameter != null).OrderBy(a => a.Parameter!.Ordinal);

    /// <summary>Pushes one argument (asset names become ids; params arrays push each element). Returns values pushed.</summary>
    int Argument(IArgumentOperation a)
    {
        var p = a.Parameter!;
        var kind = ScriptModel.Asset(p);
        var v = a.Value;
        if (p.IsParams && v is IArrayCreationOperation ac)
        {
            var elements = ac.Initializer?.ElementValues ?? [];
            foreach (var e in elements) AssetOrValue(e, kind);
            return elements.Length;
        }
        AssetOrValue(v, kind);
        return 1;
    }

    void AssetOrValue(IOperation v, AssetKind? kind)
    {
        while (v is IConversionOperation c && c.OperatorMethod == null) v = c.Operand;
        if (kind == null)
        {
            Value(v);
            return;
        }
        if (v is IConditionalOperation cond)
        {
            var other = _a.NewLabel();
            var end = _a.NewLabel();
            Value(cond.Condition);
            _a.Jump("JumpIfFalse", other);
            var depth = _a.Depth;
            AssetOrValue(cond.WhenTrue, kind);
            _a.Jump("Jump", end);
            _a.Mark(other);
            _a.SetDepth(depth);
            AssetOrValue(cond.WhenFalse!, kind);
            _a.Mark(end);
            return;
        }
        if (!v.ConstantValue.HasValue) { _diag.Error("AGB0011", v.Syntax.GetLocation(), "This name must be a constant string so it can be turned into an id at build time."); _a.Push(0); return; }
        if (v.ConstantValue.Value is not string s) { _a.Push(-1); return; }
        if (kind == AssetKind.ActorName) { _a.Push(_tables.NameIndex(s)); return; }
        var r = _m.Resolve(kind.Value, s, v.Syntax.GetLocation(), _diag);
        _a.Push(r?.Id ?? 0);
    }
}
