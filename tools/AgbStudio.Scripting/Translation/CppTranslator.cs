using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace AgbStudio.Scripting.Translation;

/// <summary>An Inspector field of a behaviour (public int / fixed / bool / enum instance field).</summary>
internal sealed record ExposedField(string Name, string CppName, string CsType, int Kind, string CppType, string? InitCpp,
    double? Min, double? Max, string? Default, string? EnumCpp, IReadOnlyList<string>? EnumValues);

/// <summary>Generated C++ for one C# type.</summary>
internal sealed class TypeOutput
{
    public required INamedTypeSymbol Symbol { get; init; }
    public required TypeRole Role { get; init; }
    public required string Name { get; init; }
    public required string CppName { get; init; }
    public string Header { get; set; } = "";
    public string? Source { get; set; }
    public int TypeIndex { get; set; } = -1;
    public bool Abstract { get; set; }
    public bool Emitted { get; set; } = true;
    public List<ExposedField> Fields { get; } = new();
    public HashSet<string> SceneEvents { get; } = new();   // OnLoad / OnEnter / OnExit present
    public string File { get; set; } = "";
    public int Line { get; set; }
    public HashSet<INamedTypeSymbol> SourceDeps { get; } = new(SymbolEqualityComparer.Default);
}

/// <summary>A typed C++ expression: text, precedence (16 primary .. 3 assignment) and whether it is a C++ reference.</summary>
internal readonly record struct CppExpr(string Text, int Prec, bool IsRef = false)
{
    public const int Primary = 16, Unary = 15, Mul = 14, Add = 13, Shift = 12, Rel = 11, Eq = 10, BitAnd = 9, Xor = 8, BitOr = 7,
        And = 6, Or = 5, Cond = 4, Assign = 3;

    public string At(int prec) => Prec >= prec ? Text : "(" + Text + ")";
}

/// <summary>Translates user C# types (bound by Roslyn) into C++20: no exceptions, no RTTI, no heap.</summary>
internal sealed partial class CppTranslator
{
    readonly ScriptModel _m;
    readonly DiagnosticSink _diag;
    readonly Func<IMethodSymbol, int> _scriptId;  // [Cutscene] method -> script id (-1 unknown)

    // per type
    INamedTypeSymbol _type = null!;
    TypeOutput _out = null!;
    CppWriter _w = null!;
    readonly HashSet<ISymbol> _refParams = new(SymbolEqualityComparer.Default);

    public CppTranslator(ScriptModel model, DiagnosticSink diag, Func<IMethodSymbol, int> scriptId)
    {
        _m = model;
        _diag = diag;
        _scriptId = scriptId;
    }

    bool Debug => _m.Ctx.DebugBuild;

    static readonly Dictionary<string, string> s_events = new()
    {
        ["Start"] = "start", ["Update"] = "update", ["LateUpdate"] = "lateUpdate", ["OnTouch"] = "onTouch", ["OnDestroy"] = "onDestroy",
    };

    static readonly HashSet<string> s_sceneEvents = new() { "OnLoad", "OnEnter", "OnExit" };

    /// <summary>True when the method is a Behaviour event (Start, Update, ...) with the runtime's signature.</summary>
    bool IsBehaviourEvent(IMethodSymbol m) =>
        !m.IsStatic && s_events.ContainsKey(m.Name) && m.ReturnsVoid && m.TypeParameters.Length == 0 &&
        _m.DerivesFrom(m.ContainingType, _m.Behaviour) &&
        (m.Name == "OnTouch" ? m.Parameters.Length == 1 && ScriptModel.Same(m.Parameters[0].Type, _m.Actor) : m.Parameters.Length == 0);

    public string MethodName(IMethodSymbol m) =>
        IsBehaviourEvent(m) ? s_events[m.Name] : m.MethodKind == MethodKind.PropertyGet ? "get_" + Names.Cpp(m.AssociatedSymbol!.Name)
        : m.MethodKind == MethodKind.PropertySet ? "set_" + Names.Cpp(m.AssociatedSymbol!.Name) : Names.Cpp(m.Name);

    public static bool IsAutoProperty(IPropertySymbol p)
    {
        foreach (var r in p.DeclaringSyntaxReferences)
        {
            if (r.GetSyntax() is not PropertyDeclarationSyntax pd) return false;
            if (pd.ExpressionBody != null || pd.AccessorList == null) return false;
            return pd.AccessorList.Accessors.All(a => a.Body == null && a.ExpressionBody == null);
        }
        return false;
    }

    (string File, int Line) Pos(IOperation op) => Pos(op.Syntax);

    (string File, int Line) Pos(SyntaxNode node)
    {
        var s = node.GetLocation().GetMappedLineSpan();
        return (_m.DisplayPath(s.Path), s.StartLinePosition.Line + 1);
    }

    int FileIdOf(string file)
    {
        if (!_m.FileIds.TryGetValue(file, out var id)) _m.FileIds[file] = id = _m.FileIds.Count;
        return id;
    }

    void Error(IOperation op, string message) => _diag.Error("AGB0100", op.Syntax.GetLocation(), message);
    void Error(SyntaxNode node, string message) => _diag.Error("AGB0100", node.GetLocation(), message);

    string TypeOf(ITypeSymbol t, SyntaxNode at)
    {
        var s = _m.CppType(t);
        if (s != null)
        {
            if (t is INamedTypeSymbol n && _m.IsUser(n)) _out.SourceDeps.Add(n.OriginalDefinition);
            return s;
        }
        Error(at, $"The type '{t.ToDisplayString()}' cannot be used in GBA scripts. Use int, bool, fixed, fixed2, an enum, a struct or a class of the GBA API or of your scripts.");
        return "int";
    }

    // =====================================================================================================
    // Types
    // =====================================================================================================

    public TypeOutput Translate(INamedTypeSymbol type, int typeIndex)
    {
        _type = type;
        var role = _m.Role(type);
        _out = new TypeOutput { Symbol = type, Role = role, Name = type.Name, CppName = _m.CppTypeName(type), TypeIndex = typeIndex, Abstract = type.IsAbstract };
        var decl = type.DeclaringSyntaxReferences.First().GetSyntax();
        (_out.File, _out.Line) = Pos(decl);
        if (type.ContainingType != null)
        {
            Error(decl, $"Nested type '{type.Name}' is not supported. Move it out of '{type.ContainingType.Name}'.");
            return _out;
        }
        if (type.TypeParameters.Length > 0)
        {
            Error(decl, $"Generic type '{type.Name}' is not supported; generics are only allowed on API calls such as Get<T>().");
            return _out;
        }
        if (role == TypeRole.Enum) TranslateEnum(type);
        else TranslateClass(type, role);
        return _out;
    }

    void TranslateEnum(INamedTypeSymbol e)
    {
        var w = new CppWriter(e.Name + ".hpp");
        Header(w, null);
        var ns = _m.CppNamespace(e);
        OpenNs(w, ns);
        w.Line($"enum class {Names.Cpp(e.Name)} : {_m.EnumBase(e)} {{", _out.File, _out.Line);
        w.Indent++;
        foreach (var f in e.GetMembers().OfType<IFieldSymbol>().Where(f => f.HasConstantValue))
            w.Line($"{Names.Cpp(f.Name)} = {Convert.ToInt64(f.ConstantValue, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)},");
        w.Indent--;
        w.Line("};");
        w.Unmap();
        CloseNs(w, ns);
        _out.Header = w.ToString();
        _out.Source = null;
    }

    static void Header(CppWriter w, string? from)
    {
        w.Raw("// Generated by AGB Studio" + (from != null ? " from " + from : "") + ". Do not edit.");
        w.Raw("#pragma once");
        w.Raw("#include \"agb_prelude.hpp\"");
    }

    static void OpenNs(CppWriter w, string ns)
    {
        if (ns.Length > 0) w.Raw($"namespace {ns} {{");
    }

    static void CloseNs(CppWriter w, string ns)
    {
        if (ns.Length > 0) w.Raw($"}}  // namespace {ns}");
    }

    sealed record MemberPlan(ISymbol Symbol, SyntaxNode Syntax);

    void TranslateClass(INamedTypeSymbol type, TypeRole role)
    {
        var name = Names.Cpp(type.Name);
        var ns = _m.CppNamespace(type);
        var hdr = new CppWriter(type.Name + ".hpp");
        var src = new CppWriter(type.Name + ".cpp");
        _w = hdr;

        var members = type.GetMembers().Where(s => !s.IsImplicitlyDeclared).ToList();
        var fields = members.OfType<IFieldSymbol>().Where(f => f.AssociatedSymbol == null).ToList();
        var props = members.OfType<IPropertySymbol>().ToList();
        var methods = members.OfType<IMethodSymbol>()
            .Where(m => m.MethodKind is MethodKind.Ordinary or MethodKind.Constructor or MethodKind.StaticConstructor or MethodKind.UserDefinedOperator or MethodKind.Conversion)
            .Where(m => !(ScriptModel.IsCutscene(m) && m.IsAsync))
            .ToList();
        foreach (var m in members.OfType<IMethodSymbol>().Where(m => ScriptModel.IsCutscene(m) && !m.IsAsync))
            Error(m.DeclaringSyntaxReferences[0].GetSyntax(), $"[Cutscene] method '{m.Name}' must be declared 'async Task'.");
        foreach (var m in methods.Where(m => m.IsAsync))
            Error(m.DeclaringSyntaxReferences[0].GetSyntax(), $"async method '{m.Name}' needs [Cutscene]: only cutscenes can wait, and they compile to Event Script bytecode.");
        methods = methods.Where(m => !m.IsAsync).ToList();
        foreach (var t in members.OfType<INamedTypeSymbol>())
            Error(t.DeclaringSyntaxReferences[0].GetSyntax(), $"Nested type '{t.Name}' is not supported. Move it out of '{type.Name}'.");

        if (role == TypeRole.ScriptHolder && fields.Count == 0 && props.Count == 0 && methods.All(m => m.MethodKind == MethodKind.Constructor && m.Parameters.Length == 0))
            _out.Emitted = false;

        // ---- header ---------------------------------------------------------------------------------
        Header(hdr, _out.File);
        var baseType = type.BaseType;
        string? baseCpp = null;
        if (role == TypeRole.Behaviour) baseCpp = _m.IsUser(baseType) ? _m.CppTypeName(baseType!) : "agb::Behaviour";
        else if (baseType != null && _m.IsUser(baseType)) baseCpp = _m.CppTypeName(baseType);
        else if (baseType != null && baseType.SpecialType != SpecialType.System_Object && baseType.SpecialType != SpecialType.System_ValueType &&
                 !ScriptModel.Same(baseType, _m.SceneScript) && !ScriptModel.Same(baseType, _m.ScriptBase))
            Error(type.DeclaringSyntaxReferences[0].GetSyntax(), $"'{type.Name}' derives from '{baseType.Name}', which scripts cannot extend.");
        if (baseType != null && _m.IsUser(baseType)) hdr.Raw($"#include \"{baseType.Name}.hpp\"");

        // Value types used in declarations need their full definition; classes only a forward declaration.
        var declTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        void Use(ITypeSymbol? t)
        {
            if (t is IArrayTypeSymbol a) t = a.ElementType;
            if (t is INamedTypeSymbol n && _m.IsUser(n) && !ScriptModel.Same(n.OriginalDefinition, type)) declTypes.Add(n.OriginalDefinition);
        }
        foreach (var f in fields) Use(f.Type);
        foreach (var p in props) Use(p.Type);
        foreach (var m in methods) { Use(m.ReturnType); foreach (var p in m.Parameters) Use(p.Type); }
        foreach (var t in declTypes.Where(t => t.IsValueType).OrderBy(t => t.Name, StringComparer.Ordinal)) hdr.Raw($"#include \"{t.Name}.hpp\"");
        foreach (var t in declTypes.Where(t => !t.IsValueType).OrderBy(t => t.Name, StringComparer.Ordinal))
        {
            var tns = _m.CppNamespace(t);
            hdr.Raw(tns.Length > 0 ? $"namespace {tns} {{ class {Names.Cpp(t.Name)}; }}" : $"class {Names.Cpp(t.Name)};");
        }
        hdr.Raw("");
        OpenNs(hdr, ns);

        var isStruct = role == TypeRole.Struct;
        var derived = _m.UserTypes.Any(t => ScriptModel.Same(t.BaseType?.OriginalDefinition, type));
        var head = (isStruct || role == TypeRole.StaticClass ? "struct " : "class ") + name +
            (!derived && !type.IsAbstract && !type.IsStatic && !isStruct ? " final" : "") + (baseCpp != null ? " : public " + baseCpp : "") + " {";
        hdr.Line(head, _out.File, _out.Line);
        if (!isStruct && role != TypeRole.StaticClass) hdr.Raw("public:");
        hdr.Indent++;
        hdr.Unmap();

        if (role == TypeRole.Behaviour && !type.IsAbstract)
        {
            hdr.Line($"static constexpr agb::u16 kTypeId = {64 + _out.TypeIndex};");
            hdr.Line("agb::u16 typeId() const override { return kTypeId; }");
            hdr.Line($"const char* typeName() const override {{ return {Names.CppString(type.Name)}; }}");
        }

        // Fields (and auto-properties, which are plain fields in C++).
        foreach (var f in fields) FieldDecl(f, f.Type, f.Name, f.IsStatic, f.IsConst, f.DeclaringSyntaxReferences[0].GetSyntax(), f.HasConstantValue ? f.ConstantValue : null);
        foreach (var p in props.Where(IsAutoProperty))
            FieldDecl(p, p.Type, p.Name, p.IsStatic, false, p.DeclaringSyntaxReferences[0].GetSyntax(), null);

        // Inspector fields of behaviours.
        if (role == TypeRole.Behaviour) CollectExposed(fields);

        // Declarations of methods and property accessors.
        var bodies = new List<(IMethodSymbol M, SyntaxNode Syntax)>();
        foreach (var p in props.Where(p => !IsAutoProperty(p)))
        {
            if (p.IsIndexer) { Error(p.DeclaringSyntaxReferences[0].GetSyntax(), "Indexers are not supported in scripts."); continue; }
            foreach (var acc in new[] { p.GetMethod, p.SetMethod }.OfType<IMethodSymbol>())
            {
                var syn = acc.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() ?? p.DeclaringSyntaxReferences[0].GetSyntax();
                hdr.Line(Signature(acc, declaration: true) + ";", Pos(syn).File, Pos(syn).Line);
                bodies.Add((acc, syn));
            }
        }
        foreach (var m in methods)
        {
            var syn = m.DeclaringSyntaxReferences[0].GetSyntax();
            if (m.TypeParameters.Length > 0) { Error(syn, $"Generic method '{m.Name}' is not supported; generics are only allowed on API calls such as Get<T>()."); continue; }
            if (m.MethodKind is MethodKind.UserDefinedOperator or MethodKind.Conversion)
            {
                Error(syn, "Operators declared in scripts are not supported yet. Use a method instead (for example Add(a, b)).");
                continue;
            }
            if (m.MethodKind == MethodKind.StaticConstructor) { Error(syn, "Static constructors are not supported; initialize static fields where they are declared."); continue; }
            if (role == TypeRole.SceneScript && s_sceneEvents.Contains(m.Name) && !m.IsStatic && m.Parameters.Length == 0) _out.SceneEvents.Add(m.Name);
            var (f, l) = Pos(syn);
            if (m.IsAbstract) hdr.Line(Signature(m, declaration: true) + " = 0;", f, l);
            else
            {
                hdr.Line(Signature(m, declaration: true) + ";", f, l);
                bodies.Add((m, syn));
            }
        }
        if (isStruct && methods.Any(m => m.MethodKind == MethodKind.Constructor))
            hdr.Line($"{name}() = default;");
        hdr.Unmap();

        if (_out.Fields.Count > 0)
        {
            var baseCount = _m.IsUser(baseType) ? $"{_m.CppTypeName(baseType!)}::fieldCount() + " : "";
            hdr.Line($"int fieldCount() const override {{ return {baseCount}{_out.Fields.Count}; }}");
            hdr.Line("const char* fieldName(int i) const override;");
            hdr.Line("void* fieldPtr(int i) override;");
            hdr.Line("agb::u8 fieldKind(int i) const override;");
        }
        hdr.Indent--;
        hdr.Line("};");
        CloseNs(hdr, ns);
        if (role == TypeRole.Behaviour && !type.IsAbstract)
        {
            hdr.Raw("");
            hdr.Raw($"agb::Behaviour* agb_make_{type.Name}(agb::Arena& arena);");
        }

        // ---- source ---------------------------------------------------------------------------------
        _w = src;
        foreach (var (m, syn) in bodies) MethodBody(m, syn);
        if (_out.Fields.Count > 0) InspectorBodies(name);
        src.Unmap();
        var bodyText = src.ToString();

        var s = new CppWriter(type.Name + ".cpp");
        s.Raw($"// Generated by AGB Studio from {_out.File}. Do not edit.");
        s.Raw($"#include \"{type.Name}.hpp\"");
        foreach (var t in _out.SourceDeps.Where(t => !ScriptModel.Same(t, type)).OrderBy(t => t.Name, StringComparer.Ordinal))
            s.Raw($"#include \"{t.Name}.hpp\"");
        if (role == TypeRole.Behaviour && !type.IsAbstract || _usesScripts) s.Raw("#include \"agb_scripts.hpp\"");
        s.Raw("");
        _out.Source = s + (ns.Length > 0 ? $"namespace {ns} {{\n" : "") + bodyText + (ns.Length > 0 ? $"}}  // namespace {ns}\n" : "") + Factories(type);
        _out.Header = hdr.ToString();
        _usesScripts = false;
    }

    bool _usesScripts;

    string Factories(INamedTypeSymbol type)
    {
        if (_out.Role != TypeRole.Behaviour || type.IsAbstract) return "";
        var cpp = _out.CppName;
        var sb = new StringBuilder("\n");
        sb.Append($"agb::Behaviour* agb_make_{type.Name}(agb::Arena& arena) {{ return arena.make<{cpp}>(); }}\n");
        sb.Append($"agb::Behaviour* agb_make_{type.Name}_with(agb::Arena& arena, const agb_init_{type.Name}& init) {{\n");
        sb.Append($"    auto* b = arena.make<{cpp}>();\n");
        var settable = _out.Fields.Where(f => f.InitCpp != null).ToList();
        if (settable.Count > 0)
        {
            sb.Append("    if (!b) return nullptr;\n");
            foreach (var f in settable) sb.Append($"    b->{f.CppName} = init.{f.CppName};\n");
        }
        else sb.Append("    AGB_UNUSED(init);\n");
        sb.Append("    return b;\n}\n");
        return sb.ToString();
    }

    void CollectExposed(List<IFieldSymbol> fields)
    {
        foreach (var f in fields)
        {
            if (f.IsStatic || f.IsConst || f.DeclaredAccessibility != Accessibility.Public) continue;
            int kind;
            string cs;
            string? enumCpp = null;
            List<string>? enumValues = null;
            if (f.Type.SpecialType == SpecialType.System_Int32) { kind = 0; cs = "int"; }
            else if (_m.IsFixed(f.Type)) { kind = 1; cs = "fixed"; }
            else if (f.Type.SpecialType == SpecialType.System_Boolean) { kind = 2; cs = "bool"; }
            else if (f.Type is INamedTypeSymbol { TypeKind: TypeKind.Enum } e && _m.IsUser(e) && e.EnumUnderlyingType?.SpecialType == SpecialType.System_Int32)
            {
                kind = 0;
                cs = e.Name;
                enumCpp = _m.CppTypeName(e);
                enumValues = e.GetMembers().OfType<IFieldSymbol>().Where(x => x.HasConstantValue).Select(x => x.Name).ToList();
            }
            else continue;
            var syntax = f.DeclaringSyntaxReferences[0].GetSyntax() as VariableDeclaratorSyntax;
            string? init = kind switch { 1 => "agb::fixed()", 2 => "false", _ => enumCpp != null ? enumCpp + "{}" : "0" };
            string? def = null;
            if (syntax?.Initializer != null)
            {
                var sm = _m.Compilation.GetSemanticModel(syntax.SyntaxTree);
                var op = sm.GetOperation(syntax.Initializer) as IFieldInitializerOperation;
                if (op != null && op.Value.Descendants().Any(d => d is IInstanceReferenceOperation)) init = null;
                else if (op != null)
                {
                    var save = _w;
                    init = Expr(op.Value).Text;
                    _w = save;
                }
                def = syntax.Initializer.Value.ToString();
            }
            var range = ScriptModel.Range(f);
            _out.Fields.Add(new ExposedField(f.Name, Names.Cpp(f.Name), cs, kind, _m.CppType(f.Type) ?? "agb::s32", init, range?.Min, range?.Max, def, enumCpp, enumValues));
        }
    }

    void InspectorBodies(string cls)
    {
        var bt = _type.BaseType;
        var hasBase = _m.IsUser(bt);
        var baseName = hasBase ? _m.CppTypeName(bt!) : "";
        var n = _out.Fields.Count;
        var w = _w;
        w.Blank();
        var idx = hasBase ? $"i - {baseName}::fieldCount()" : "i";
        w.Line($"const char* {cls}::fieldName(int i) const {{");
        w.Indent++;
        if (hasBase) w.Line($"if (i < {baseName}::fieldCount()) return {baseName}::fieldName(i);");
        w.Line($"static constexpr const char* kNames[{n}] = {{{string.Join(", ", _out.Fields.Select(f => Names.CppString(f.Name)))}}};");
        w.Line($"const int k = {idx};");
        w.Line($"return k >= 0 && k < {n} ? kNames[k] : \"\";");
        w.Indent--;
        w.Line("}");
        w.Blank();
        w.Line($"void* {cls}::fieldPtr(int i) {{");
        w.Indent++;
        if (hasBase) w.Line($"if (i < {baseName}::fieldCount()) return {baseName}::fieldPtr(i);");
        w.Line($"switch ({idx}) {{");
        for (var i = 0; i < n; i++) w.Line($"    case {i}: return &{_out.Fields[i].CppName};");
        w.Line("    default: return nullptr;");
        w.Line("}");
        w.Indent--;
        w.Line("}");
        w.Blank();
        w.Line($"agb::u8 {cls}::fieldKind(int i) const {{");
        w.Indent++;
        if (hasBase) w.Line($"if (i < {baseName}::fieldCount()) return {baseName}::fieldKind(i);");
        w.Line($"static constexpr agb::u8 kKinds[{n}] = {{{string.Join(", ", _out.Fields.Select(f => f.Kind))}}};");
        w.Line($"const int k = {idx};");
        w.Line($"return k >= 0 && k < {n} ? kKinds[k] : 0;");
        w.Indent--;
        w.Line("}");
    }

    // ---- fields -----------------------------------------------------------------------------------------

    void FieldDecl(ISymbol symbol, ITypeSymbol type, string csName, bool isStatic, bool isConst, SyntaxNode syntax, object? constValue)
    {
        var (file, line) = Pos(syntax);
        var name = Names.Cpp(csName);
        EqualsValueClauseSyntax? initSyntax = syntax switch
        {
            VariableDeclaratorSyntax v => v.Initializer,
            PropertyDeclarationSyntax p => p.Initializer,
            _ => null,
        };
        IOperation? init = null;
        if (initSyntax != null)
        {
            var sm = _m.Compilation.GetSemanticModel(initSyntax.SyntaxTree);
            init = sm.GetOperation(initSyntax) switch
            {
                IFieldInitializerOperation fi => fi.Value,
                IPropertyInitializerOperation pi => pi.Value,
                var o => o,
            };
        }
        if (type is IArrayTypeSymbol arr)
        {
            ArrayDecl(arr, name, init, isStatic ? "static inline " : "", syntax, file, line);
            return;
        }
        var t = TypeOf(type, syntax);
        if (isConst)
        {
            var value = Constant(constValue, type).Text;
            _w.Line($"static constexpr {t} {name} = {value};", file, line);
            return;
        }
        var initText = init != null ? Expr(init).Text : DefaultValue(type);
        _w.Line($"{(isStatic ? "static inline " : "")}{t} {name} = {initText};", file, line);
    }

    void ArrayDecl(IArrayTypeSymbol arr, string name, IOperation? init, string prefix, SyntaxNode at, string file, int line)
    {
        if (arr.Rank != 1 || arr.ElementType is IArrayTypeSymbol)
        {
            Error(at, "Only one-dimensional arrays are supported.");
            return;
        }
        var et = TypeOf(arr.ElementType, at);
        while (init is IConversionOperation c) init = c.Operand;
        string size;
        var values = "{}";
        switch (init)
        {
            case IArrayCreationOperation ac:
                if (ac.Initializer != null)
                {
                    size = ac.Initializer.ElementValues.Length.ToString(CultureInfo.InvariantCulture);
                    values = "{" + string.Join(", ", ac.Initializer.ElementValues.Select(v => Expr(v).Text)) + "}";
                }
                else if (ac.DimensionSizes.Length == 1 && ac.DimensionSizes[0].ConstantValue.Value is int n) size = n.ToString(CultureInfo.InvariantCulture);
                else { Error(at, "Array sizes must be constants (the GBA has no heap): new int[8]."); return; }
                break;
            case IArrayInitializerOperation ai:
                size = ai.ElementValues.Length.ToString(CultureInfo.InvariantCulture);
                values = "{" + string.Join(", ", ai.ElementValues.Select(v => Expr(v).Text)) + "}";
                break;
            case ICollectionExpressionOperation ce:
                size = ce.Elements.Length.ToString(CultureInfo.InvariantCulture);
                values = "{" + string.Join(", ", ce.Elements.Select(v => Expr(v).Text)) + "}";
                break;
            default:
                Error(at, "Arrays need a fixed size where they are declared: int[] counts = new int[8]; (the GBA has no heap).");
                return;
        }
        if (size == "0") size = "1";
        _w.Line($"{prefix}{et} {name}[{size}] = {values};", file, line);
    }

    string DefaultValue(ITypeSymbol t)
    {
        if (t.IsReferenceType) return "nullptr";
        return t.SpecialType switch
        {
            SpecialType.System_Boolean => "false",
            SpecialType.System_Single or SpecialType.System_Double => "0",
            _ when t.SpecialType >= SpecialType.System_Char && t.SpecialType <= SpecialType.System_UInt64 => "0",
            _ => (_m.CppType(t) ?? "int") + "{}",
        };
    }

    // ---- methods ----------------------------------------------------------------------------------------

    string Signature(IMethodSymbol m, bool declaration)
    {
        var cls = Names.Cpp(m.ContainingType.Name);
        var ps = string.Join(", ", m.Parameters.Select(p => Param(p, declaration)));
        if (m.MethodKind == MethodKind.Constructor) return declaration ? $"{cls}({ps})" : $"{cls}::{cls}({ps})";
        var ret = m.MethodKind == MethodKind.PropertySet ? "void" : m.ReturnsVoid ? "void" : TypeOf(m.ReturnType, m.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() ?? _type.DeclaringSyntaxReferences[0].GetSyntax());
        var nameText = MethodName(m);
        if (!declaration) return $"{ret} {cls}::{nameText}({ps})";
        var prefix = m.IsStatic ? "static " : m.IsAbstract || m.IsVirtual ? "virtual " : "";
        var suffix = IsBehaviourEvent(m) || m.IsOverride ? " override" : "";
        return $"{prefix}{ret} {nameText}({ps}){suffix}";
    }

    string Param(IParameterSymbol p, bool declaration)
    {
        var at = p.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() ?? _type.DeclaringSyntaxReferences[0].GetSyntax();
        if (p.IsParams) Error(at, "params arrays are not supported in translated code.");
        var m = (IMethodSymbol)p.ContainingSymbol;
        string t;
        if (IsBehaviourEvent(m) && m.Name == "OnTouch") t = "agb::Actor&";
        else
        {
            t = TypeOf(p.Type, at);
            if (p.RefKind is RefKind.Ref or RefKind.Out) t += "&";
            else if (p.RefKind == RefKind.In) t = "const " + t + "&";
        }
        var s = $"{t} {Names.Cpp(p.Name)}";
        if (declaration && p.HasExplicitDefaultValue)
            s += " = " + (p.ExplicitDefaultValue == null && !p.Type.IsReferenceType ? DefaultValue(p.Type) : Constant(p.ExplicitDefaultValue, p.Type).Text);
        return s;
    }

    void MethodBody(IMethodSymbol m, SyntaxNode syntax)
    {
        var sm = _m.Compilation.GetSemanticModel(syntax.SyntaxTree);
        var op = sm.GetOperation(syntax);
        _refParams.Clear();
        if (IsBehaviourEvent(m) && m.Name == "OnTouch") _refParams.Add(m.Parameters[0]);
        var (file, line) = Pos(syntax);
        _w.Blank();
        var sig = Signature(m, declaration: false);
        IBlockOperation? block = null;
        string? init = null;
        switch (op)
        {
            case IMethodBodyBaseOperation mb:
                block = mb.BlockBody ?? mb.ExpressionBody;
                if (op is IConstructorBodyOperation cb && cb.Initializer != null)
                {
                    var ctorCall = cb.Initializer is IExpressionStatementOperation es ? es.Operation : cb.Initializer;
                    if (ctorCall is IInvocationOperation inv && inv.TargetMethod.MethodKind == MethodKind.Constructor &&
                        !ScriptModel.Same(inv.TargetMethod.ContainingType, m.ContainingType) && _m.IsUser(inv.TargetMethod.ContainingType))
                        init = $" : {_m.CppTypeName(inv.TargetMethod.ContainingType)}({string.Join(", ", Args(inv.TargetMethod, inv.Arguments).Select(a => a.Text))})";
                }
                break;
            case IBlockOperation b:
                block = b;
                break;
        }
        if (block == null && syntax is AccessorDeclarationSyntax { ExpressionBody: { } eb })
            block = sm.GetOperation(eb) as IBlockOperation;
        if (block == null && syntax is ArrowExpressionClauseSyntax arrow)
            block = sm.GetOperation(arrow) as IBlockOperation;
        if (block == null && syntax is PropertyDeclarationSyntax { ExpressionBody: { } peb })
            block = sm.GetOperation(peb) as IBlockOperation;
        _w.Line(sig + (init ?? "") + " {", file, line);
        _w.Indent++;
        if (Debug) _w.Line($"AGB_ENTER({FileIdOf(file)}, {line});");
        if (block != null) foreach (var s in block.Operations) Stmt(s);
        else if (op != null) Error(syntax, "This method body could not be translated.");
        _w.Indent--;
        _w.Line("}");
    }
}
