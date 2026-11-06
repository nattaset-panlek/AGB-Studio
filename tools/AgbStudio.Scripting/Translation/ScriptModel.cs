using System.Globalization;
using Microsoft.CodeAnalysis;
using BuildCpp = AgbStudio.Build.Pipeline.Cpp;

namespace AgbStudio.Scripting.Translation;

/// <summary>Kinds of names resolved at build time (mirror of GBA.AssetKind).</summary>
internal enum AssetKind { Sound, Music, Scene, Tag, Text, Speaker, Portrait, Path, ActorName, Picture }

/// <summary>A [Cpp] mapping read from the API or the generated project API.</summary>
internal sealed record CppMap(string Template, string? Set, int Prec);

internal enum TypeRole { Behaviour, SceneScript, ScriptHolder, StaticClass, Class, Struct, Enum }

/// <summary>Collects diagnostics of the translator and the bytecode compiler.</summary>
internal sealed class DiagnosticSink
{
    readonly Func<string, string> _display;
    public List<ScriptDiagnostic> Items { get; } = new();
    public DiagnosticSink(Func<string, string> display) => _display = display;

    public void Error(string id, Location? location, string message) => Add(id, ScriptSeverity.Error, location, message);
    public void Warning(string id, Location? location, string message) => Add(id, ScriptSeverity.Warning, location, message);

    void Add(string id, ScriptSeverity sev, Location? location, string message)
    {
        if (location == null || !location.IsInSource)
        {
            Items.Add(new ScriptDiagnostic(id, sev, message, "", 0, 0));
            return;
        }
        var span = location.GetMappedLineSpan();
        Items.Add(new ScriptDiagnostic(id, sev, message, _display(span.Path), span.StartLinePosition.Line + 1,
            span.StartLinePosition.Character + 1, span.EndLinePosition.Line + 1, span.EndLinePosition.Character + 1));
    }

    public bool HasErrors => Items.Any(d => d.IsError);
}

/// <summary>Symbols of the GBA API and the user's types, shared by the C++ translator and the bytecode compiler.</summary>
internal sealed class ScriptModel
{
    public Compilation Compilation { get; }
    public ScriptCompileContext Ctx { get; }
    public INamedTypeSymbol? Behaviour, SceneScript, ScriptBase, Fixed, Fixed2, Actor, Task, TaskOfT;
    public HashSet<INamedTypeSymbol> UserTypes { get; } = new(SymbolEqualityComparer.Default);
    public Dictionary<string, int> FileIds { get; } = new(StringComparer.OrdinalIgnoreCase);
    readonly Func<string, string> _display;

    public ScriptModel(Compilation compilation, ScriptCompileContext ctx, IEnumerable<SyntaxTree> userTrees, Func<string, string> display)
    {
        Compilation = compilation;
        Ctx = ctx;
        _display = display;
        Behaviour = compilation.GetTypeByMetadataName("GBA.Behaviour");
        SceneScript = compilation.GetTypeByMetadataName("GBA.SceneScript");
        ScriptBase = compilation.GetTypeByMetadataName("GBA.ScriptBase");
        Fixed = compilation.GetTypeByMetadataName("GBA.fixed");
        Fixed2 = compilation.GetTypeByMetadataName("GBA.fixed2");
        Actor = compilation.GetTypeByMetadataName("GBA.Actor");
        Task = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");
        TaskOfT = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1");
        foreach (var tree in userTrees)
        {
            var sm = compilation.GetSemanticModel(tree);
            foreach (var decl in tree.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.BaseTypeDeclarationSyntax>())
                if (sm.GetDeclaredSymbol(decl) is INamedTypeSymbol t) UserTypes.Add(t);
            var path = display(tree.FilePath);
            if (!FileIds.ContainsKey(path)) FileIds[path] = FileIds.Count;
        }
    }

    public string DisplayPath(string path) => _display(path);

    /// <summary>Method names for the Debugger call stack ("PlayerController.Update()"), indexed by AGB_SCOPE.</summary>
    public List<string> MethodNames { get; } = new();

    public int MethodNameId(string name)
    {
        var i = MethodNames.IndexOf(name);
        if (i >= 0) return i;
        MethodNames.Add(name);
        return MethodNames.Count - 1;
    }

    public int FileId(SyntaxTree tree) => FileIds.TryGetValue(_display(tree.FilePath), out var id) ? id : 0;

    public bool IsUser(ITypeSymbol? t) => t is INamedTypeSymbol n && UserTypes.Contains(n.OriginalDefinition);
    public bool IsFixed(ITypeSymbol? t) => Same(t, Fixed);
    public static bool Same(ISymbol? a, ISymbol? b) => a != null && b != null && SymbolEqualityComparer.Default.Equals(a, b);

    public bool DerivesFrom(ITypeSymbol? t, INamedTypeSymbol? baseType)
    {
        for (var c = t; c != null; c = c.BaseType)
            if (Same(c, baseType)) return true;
        return false;
    }

    public bool IsTask(ITypeSymbol? t) => t is INamedTypeSymbol n && (Same(n, Task) || Same(n.OriginalDefinition, TaskOfT));

    public TypeRole Role(INamedTypeSymbol t)
    {
        if (t.TypeKind == TypeKind.Enum) return TypeRole.Enum;
        if (t.TypeKind == TypeKind.Struct) return TypeRole.Struct;
        if (DerivesFrom(t, Behaviour)) return TypeRole.Behaviour;
        if (DerivesFrom(t, SceneScript)) return TypeRole.SceneScript;
        if (DerivesFrom(t, ScriptBase)) return TypeRole.ScriptHolder;
        if (t.IsStatic) return TypeRole.StaticClass;
        return TypeRole.Class;
    }

    // ---- attributes -----------------------------------------------------------------------------------

    static AttributeData? Attr(ISymbol s, string name) =>
        s.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == name);

    public static CppMap? Cpp(ISymbol s)
    {
        var a = Attr(s, "GBA.CppAttribute") ?? (s is IMethodSymbol m ? Attr(m.OriginalDefinition, "GBA.CppAttribute") : null);
        if (a == null || a.ConstructorArguments.Length == 0) return null;
        string? set = null;
        var prec = 16;
        foreach (var (k, v) in a.NamedArguments)
        {
            if (k == "Set") set = v.Value as string;
            if (k == "Prec" && v.Value is int p) prec = p;
        }
        return new CppMap((string)a.ConstructorArguments[0].Value!, set, prec);
    }

    public static AssetKind? Asset(ISymbol s)
    {
        var a = Attr(s, "GBA.AssetAttribute");
        return a != null && a.ConstructorArguments.Length == 1 && a.ConstructorArguments[0].Value is int k ? (AssetKind)k : null;
    }

    public static string? ScriptOp(ISymbol s)
    {
        var a = Attr(s, "GBA.ScriptOpAttribute") ?? (s is IMethodSymbol m ? Attr(m.OriginalDefinition, "GBA.ScriptOpAttribute") : null);
        return a?.ConstructorArguments.FirstOrDefault().Value as string;
    }

    public static bool IsCutscene(IMethodSymbol m) => Attr(m, "GBA.CutsceneAttribute") != null;

    /// <summary>[Cutscene(Background = ..., Once = ...)]: false when not set.</summary>
    public static (bool Background, bool Once) CutsceneOptions(IMethodSymbol m)
    {
        var a = Attr(m, "GBA.CutsceneAttribute");
        if (a == null) return (false, false);
        bool Named(string name) => a.NamedArguments.Any(x => x.Key == name && x.Value.Value is true);
        return (Named("Background"), Named("Once"));
    }

    /// <summary>The C# symbol of an operator for error texts ("~", "&lt;&lt;"), not the Roslyn enum name (G-ERR.01).</summary>
    public static string OperatorText(Microsoft.CodeAnalysis.Operations.UnaryOperatorKind k) => k switch
    {
        Microsoft.CodeAnalysis.Operations.UnaryOperatorKind.BitwiseNegation => "~",
        Microsoft.CodeAnalysis.Operations.UnaryOperatorKind.Not => "!",
        Microsoft.CodeAnalysis.Operations.UnaryOperatorKind.Plus => "+",
        Microsoft.CodeAnalysis.Operations.UnaryOperatorKind.Minus => "-",
        Microsoft.CodeAnalysis.Operations.UnaryOperatorKind.Hat => "^ (index from end)",
        _ => k.ToString(),
    };

    public static string OperatorText(Microsoft.CodeAnalysis.Operations.BinaryOperatorKind k) => k switch
    {
        Microsoft.CodeAnalysis.Operations.BinaryOperatorKind.Add => "+",
        Microsoft.CodeAnalysis.Operations.BinaryOperatorKind.Subtract => "-",
        Microsoft.CodeAnalysis.Operations.BinaryOperatorKind.Multiply => "*",
        Microsoft.CodeAnalysis.Operations.BinaryOperatorKind.Divide => "/",
        Microsoft.CodeAnalysis.Operations.BinaryOperatorKind.Remainder => "%",
        Microsoft.CodeAnalysis.Operations.BinaryOperatorKind.LeftShift => "<<",
        Microsoft.CodeAnalysis.Operations.BinaryOperatorKind.RightShift => ">>",
        Microsoft.CodeAnalysis.Operations.BinaryOperatorKind.UnsignedRightShift => ">>>",
        Microsoft.CodeAnalysis.Operations.BinaryOperatorKind.And => "&",
        Microsoft.CodeAnalysis.Operations.BinaryOperatorKind.Or => "|",
        Microsoft.CodeAnalysis.Operations.BinaryOperatorKind.ExclusiveOr => "^",
        Microsoft.CodeAnalysis.Operations.BinaryOperatorKind.ConditionalAnd => "&&",
        Microsoft.CodeAnalysis.Operations.BinaryOperatorKind.ConditionalOr => "||",
        Microsoft.CodeAnalysis.Operations.BinaryOperatorKind.Equals or Microsoft.CodeAnalysis.Operations.BinaryOperatorKind.ObjectValueEquals => "==",
        Microsoft.CodeAnalysis.Operations.BinaryOperatorKind.NotEquals or Microsoft.CodeAnalysis.Operations.BinaryOperatorKind.ObjectValueNotEquals => "!=",
        Microsoft.CodeAnalysis.Operations.BinaryOperatorKind.LessThan => "<",
        Microsoft.CodeAnalysis.Operations.BinaryOperatorKind.LessThanOrEqual => "<=",
        Microsoft.CodeAnalysis.Operations.BinaryOperatorKind.GreaterThan => ">",
        Microsoft.CodeAnalysis.Operations.BinaryOperatorKind.GreaterThanOrEqual => ">=",
        _ => k.ToString(),
    };

    /// <summary>The first line of a piece of code, cut to 40 characters, to name it in an error text.</summary>
    public static string Excerpt(SyntaxNode? syntax)
    {
        var text = syntax?.ToString().Split('\n')[0].Trim() ?? "";
        return text.Length > 40 ? text[..37] + "..." : text;
    }

    public static bool? NativeWaits(IMethodSymbol m)
    {
        var a = Attr(m, "GBA.ScriptNativeAttribute");
        if (a == null) return null;
        if (a.ConstructorArguments.Length > 0 && a.ConstructorArguments[0].Value is bool b) return b;
        foreach (var (k, v) in a.NamedArguments) if (k == "Waits" && v.Value is bool w) return w;
        return false;
    }

    public static (double Min, double Max)? Range(ISymbol s)
    {
        var a = Attr(s, "GBA.RangeAttribute");
        if (a == null || a.ConstructorArguments.Length != 2) return null;
        return (Convert.ToDouble(a.ConstructorArguments[0].Value, CultureInfo.InvariantCulture),
            Convert.ToDouble(a.ConstructorArguments[1].Value, CultureInfo.InvariantCulture));
    }

    // ---- C++ names ------------------------------------------------------------------------------------

    /// <summary>C++ namespace of a user type ("" = global).</summary>
    public string CppNamespace(INamedTypeSymbol t)
    {
        var ns = t.ContainingNamespace;
        if (ns == null || ns.IsGlobalNamespace) return Ctx.Namespace.Trim();
        return string.Join("::", ns.ToDisplayString().Split('.').Select(Names.Cpp));
    }

    /// <summary>Qualified C++ name of a type without pointer (::Ns::Name, agb::fixed).</summary>
    public string CppTypeName(INamedTypeSymbol t)
    {
        var map = Cpp(t.OriginalDefinition);
        if (map != null) return map.Template;
        if (IsUser(t))
        {
            var ns = CppNamespace(t);
            var outer = new List<string>();
            for (var c = t; c != null; c = c.ContainingType) outer.Insert(0, Names.Cpp(c.Name));
            return "::" + (ns.Length > 0 ? ns + "::" : "") + string.Join("::", outer);
        }
        return t.ToDisplayString();
    }

    /// <summary>C++ type for a value of this C# type (classes are pointers). Null when not representable.</summary>
    public string? CppType(ITypeSymbol t)
    {
        switch (t.SpecialType)
        {
            case SpecialType.System_Int32: return "agb::s32";
            case SpecialType.System_Boolean: return "bool";
            case SpecialType.System_Byte: return "agb::u8";
            case SpecialType.System_SByte: return "agb::s8";
            case SpecialType.System_Int16: return "agb::s16";
            case SpecialType.System_UInt16: return "agb::u16";
            case SpecialType.System_UInt32: return "agb::u32";
            case SpecialType.System_Int64: return "agb::s64";
            case SpecialType.System_UInt64: return "agb::u64";
            case SpecialType.System_Single: return "float";
            case SpecialType.System_Double: return "double";
            case SpecialType.System_String: return "const char*";
            case SpecialType.System_Char: return "char16_t";
            case SpecialType.System_Void: return "void";
        }
        if (t is not INamedTypeSymbol n) return null;
        if (IsTask(n)) return null;
        var name = CppTypeName(n);
        if (Cpp(n.OriginalDefinition) == null && !IsUser(n)) return null;
        return n.IsReferenceType ? name + "*" : name;
    }

    /// <summary>Underlying C++ type of an enum.</summary>
    public string EnumBase(INamedTypeSymbol e) => CppType(e.EnumUnderlyingType!) ?? "agb::s32";

    // ---- build-time names -----------------------------------------------------------------------------

    /// <summary>Looks a name up in an id table: exact, ignoring case, then as an identifier ignoring case and extension.</summary>
    public static string? FindKey(Dictionary<string, int> table, string name)
    {
        if (table.ContainsKey(name)) return name;
        var k = table.Keys.FirstOrDefault(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));
        if (k != null) return k;
        var id = BuildCpp.Ident(Names.StripExtension(name)).ToUpperInvariant();
        return table.Keys.FirstOrDefault(x => BuildCpp.Ident(Names.StripExtension(x)).ToUpperInvariant() == id);
    }

    public Dictionary<string, int> Table(AssetKind kind) => kind switch
    {
        AssetKind.Sound => Ctx.Sounds,
        AssetKind.Music => Ctx.Music,
        AssetKind.Scene => Ctx.Scenes,
        AssetKind.Tag => Ctx.Tags,
        AssetKind.Text => Ctx.Strings,
        AssetKind.Speaker => Ctx.Speakers,
        AssetKind.Portrait => Ctx.Portraits,
        AssetKind.Picture => Ctx.Pictures,
        AssetKind.Path => Ctx.Paths.Select((p, i) => (p.Name, i)).GroupBy(x => x.Name).ToDictionary(g => g.Key, g => g.First().i),
        _ => new Dictionary<string, int>(),
    };

    static string Describe(AssetKind kind) => kind switch
    {
        AssetKind.Sound => "Sound",
        AssetKind.Music => "Music",
        AssetKind.Scene => "Scene",
        AssetKind.Tag => "Tag",
        AssetKind.Text => "String key",
        AssetKind.Speaker => "Speaker",
        AssetKind.Portrait => "Portrait",
        AssetKind.Path => "Path",
        AssetKind.Picture => "Picture",
        _ => "Actor",
    };

    static string Where(AssetKind kind) => kind switch
    {
        AssetKind.Sound => "Sound names are the .wav files in assets/audio/sfx without the extension",
        AssetKind.Music => "Music names are the .mod / .xm files in assets/audio/music without the extension",
        AssetKind.Scene => "Scene names are listed in the World Map",
        AssetKind.Tag => "Tags are set in Project Settings · Tags",
        AssetKind.Text => "String keys come from the Localization tables",
        AssetKind.Speaker => "Speakers are created in the Dialogue Editor",
        AssetKind.Portrait => "Portraits are added to speakers in the Dialogue Editor",
        AssetKind.Path => "Paths are drawn in the Scene Editor",
        AssetKind.Picture => "Picture names are the images in assets/images without the extension",
        _ => "",
    };

    /// <summary>Resolves a name to its id and C++ expression; reports a "what, why, how to fix" error when missing.</summary>
    public (int Id, string Cpp)? Resolve(AssetKind kind, string name, Location? location, DiagnosticSink diag)
    {
        var table = Table(kind);
        var key = FindKey(table, name);
        if (key == null)
        {
            var hint = Names.Closest(name, table.Keys);
            diag.Error("AGB0010", location, $"{Describe(kind)} \"{name}\" is not in the project. {Where(kind)}." +
                (hint != null ? $" Did you mean \"{hint}\"?" : table.Count == 0 ? " The project has none yet." : ""));
            return null;
        }
        var id = table[key];
        if (kind == AssetKind.Picture) lock (Ctx.UsedPictures) Ctx.UsedPictures.Add(key);
        var ident = BuildCpp.Ident(Names.StripExtension(key));
        var cpp = kind switch
        {
            AssetKind.Sound => "agb_sound::SFX_" + ident.ToUpperInvariant(),
            AssetKind.Music => "agb_sound::MOD_" + ident.ToUpperInvariant(),
            AssetKind.Scene => "agb_scene::" + BuildCpp.Ident(key),
            AssetKind.Tag => "agb_tag::" + BuildCpp.Ident(key),
            AssetKind.Text => "agb_text::" + BuildCpp.Ident(key),
            _ => id.ToString(CultureInfo.InvariantCulture),
        };
        return (id, cpp);
    }
}
