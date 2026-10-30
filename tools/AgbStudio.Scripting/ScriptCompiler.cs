using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AgbStudio.Analyzers;
using AgbStudio.Scripting.Bytecode;
using AgbStudio.Scripting.Dialogue;
using AgbStudio.Scripting.Graph;
using AgbStudio.Scripting.Translation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using BuildCpp = AgbStudio.Build.Pipeline.Cpp;

namespace AgbStudio.Scripting;

/// <summary>A behaviour class for the scene generator and the Inspector.</summary>
public sealed record BehaviourInfo(string Name, string CppName, int TypeId, string File, int Line, string Factory, string FactoryWith,
    string InitStruct, IReadOnlyList<FieldInfo> Fields);

/// <summary>An Inspector field: Kind 0 int (or enum), 1 fixed, 2 bool.</summary>
public sealed record FieldInfo(string Name, string Type, int Kind, string? Default, double? Min, double? Max, IReadOnlyList<string>? EnumValues);

/// <summary>A compiled Event Script.</summary>
public sealed record ScriptInfo(int Id, string Name, string Symbol, string File, int Line, int Locals, int Args, byte[] Code,
    IReadOnlyList<(int Pc, int Line)> Lines);

public sealed class ScriptCompileResult
{
    /// <summary>Header that declares factories, Init structs, scene hooks and agb_scripts_init (include from scene sources).</summary>
    public const string RegistryHeader = "agb_scripts.hpp";

    public List<ScriptDiagnostic> Diagnostics { get; } = new();
    public bool Success => !Diagnostics.Any(d => d.IsError);
    /// <summary>C++ sources to compile (translated classes, registry, tables; stand-ins when EmitProjectHeaders).</summary>
    public List<string> Sources { get; } = new();
    public string IncludeDir { get; set; } = "";
    /// <summary>Generated files whose content changed in this run.</summary>
    public List<string> Changed { get; } = new();
    public List<BehaviourInfo> Behaviours { get; } = new();
    public List<ScriptInfo> Scripts { get; } = new();
    public List<NativeInfo> Natives { get; } = new();
    public List<string> SceneScripts { get; } = new();
    /// <summary>Updated stable type indexes (store them in the project).</summary>
    public Dictionary<string, int> TypeIndices { get; set; } = new();
    /// <summary>The generated C# project API (also written for external IDEs).</summary>
    public string ProjectApiSource { get; set; } = "";
    /// <summary>C# generated from dialogue and graph files, by input path.</summary>
    public Dictionary<string, string> GeneratedCSharp { get; } = new();
    /// <summary>Flags created by dialogue "Set flag" commands that were not declared (name to id).</summary>
    public Dictionary<string, int> AutoFlags { get; } = new();
    public int ScriptFileCount { get; set; }

    /// <summary>
    /// C++ expression that creates a behaviour with Inspector overrides (uses a variable <c>arena</c>), or null when
    /// no script defines the class. Values are the Inspector strings ("1.5", "true", "3", enum member names).
    /// </summary>
    public string? FactoryCall(string className, IReadOnlyDictionary<string, string> fields)
    {
        var b = Behaviours.FirstOrDefault(x => x.Name == className);
        if (b == null) return null;
        var parts = new List<string>();
        foreach (var f in b.Fields)
        {
            if (!fields.TryGetValue(f.Name, out var text)) continue;
            var v = FieldValue(f, text.Trim());
            if (v != null) parts.Add($".{Names.Cpp(f.Name)} = {v}");
        }
        return parts.Count == 0 ? $"{b.Factory}(arena)" : $"{b.FactoryWith}(arena, {b.InitStruct}{{{string.Join(", ", parts)}}})";
    }

    static string? FieldValue(FieldInfo f, string text)
    {
        switch (f.Kind)
        {
            case 1:
                return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? $"agb::fixed::fromRaw({(int)Math.Round(d * 256)})" : null;
            case 2:
                return text.Equals("true", StringComparison.OrdinalIgnoreCase) || text == "1" ? "true" : text.Equals("false", StringComparison.OrdinalIgnoreCase) || text == "0" ? "false" : null;
            default:
                if (f.EnumValues != null)
                {
                    var i = f.EnumValues.ToList().IndexOf(text);
                    if (i < 0 && int.TryParse(text, out var n) && n >= 0 && n < f.EnumValues.Count) i = n;
                    return i >= 0 ? $"{f.Type}::{Names.Cpp(f.EnumValues[i])}" : null;
                }
                return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ? ((int)x).ToString(CultureInfo.InvariantCulture) : null;
        }
    }
}

/// <summary>
/// Compiles the scripts of a project: binds all C# against the GBA API, runs the GAE analyzers, translates classes to
/// C++20 under the output folder (written only when changed), compiles [Cutscene] methods and dialogues to Event
/// Script bytecode and writes the registry (factories, script / native tables, scene hooks).
/// </summary>
public sealed class ScriptCompiler
{
    public ScriptCompileResult Compile(IEnumerable<ScriptSource> sources, ScriptCompileContext ctx, string outDir, CancellationToken cancel = default)
    {
        var result = new ScriptCompileResult();
        outDir = Path.GetFullPath(outDir);
        result.IncludeDir = outDir;
        var all = sources.ToList();
        result.ScriptFileCount = all.Count;
        string Display(string path) => DisplayPath(ctx, path);

        // ---- 1. inputs: C# files, dialogues and graphs (both become C#), scene events -----------------------
        var vars = new List<ScriptVarDef>(ctx.Vars);
        var csharp = new List<(string Path, string Text, bool Generated)>();
        var dialogues = new List<(ScriptSource Src, DialogueFile File)>();
        foreach (var s in all)
        {
            var ext = Path.GetExtension(s.Path).ToLowerInvariant();
            try
            {
                switch (ext)
                {
                    case ".dlg":
                        dialogues.Add((s, DialogueFile.Parse(s.Text)));
                        break;
                    case ".graph":
                        {
                            var g = LogicGraph.Parse(s.Text);
                            var code = GraphCodeGen.Generate(g, Path.GetFileNameWithoutExtension(s.Path));
                            result.GeneratedCSharp[s.Path] = code;
                            csharp.Add((Path.ChangeExtension(s.Path, ".cs"), code, false));
                            break;
                        }
                    default:
                        // Files written by the Logic Graph carry a header; the .graph file is compiled instead (D-11).
                        if (GraphCodeGen.IsGenerated(s.Text) && all.Any(o => o.Path.EndsWith(".graph", StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(Path.ChangeExtension(o.Path, null), Path.ChangeExtension(s.Path, null), StringComparison.OrdinalIgnoreCase))) break;
                        csharp.Add((s.Path, s.Text, false));
                        break;
                }
            }
            catch (FormatException e)
            {
                result.Diagnostics.Add(new ScriptDiagnostic("AGB0001", ScriptSeverity.Error, e.Message, Display(s.Path), 1, 1));
            }
        }
        foreach (var (src, dlg) in dialogues)
        {
            var code = DialogueCompiler.ToCSharp(dlg, Display(src.Path), ctx, vars, result.AutoFlags, out var dlgDiags);
            foreach (var d in dlgDiags) result.Diagnostics.Add(d with { File = Display(src.Path) });
            result.GeneratedCSharp[src.Path] = code;
            csharp.Add((src.Path + ".cs", code, true));
        }
        var events = SceneEventsSource(ctx);
        if (events != null) csharp.Add((Path.Combine(outDir, "SceneEvents.g.cs"), events, true));
        var dbScripts = DbScripts.Source(ctx, DbScripts.ActionClasses(csharp.Select(c => c.Text)));
        if (dbScripts != null) csharp.Add((Path.Combine(outDir, "DbScripts.g.cs"), dbScripts, true));
        result.ProjectApiSource = ProjectApi.CSharp(ctx, vars);
        if (result.Diagnostics.Any(d => d.IsError)) return result;

        // ---- 2. parse and bind ------------------------------------------------------------------------------
        var parse = new CSharpParseOptions(LanguageVersion.Latest, preprocessorSymbols: ["AGB_STUDIO"]);
        var apiTree = CSharpSyntaxTree.ParseText(result.ProjectApiSource, parse, Path.Combine(outDir, ProjectApi.CSharpFileName), Encoding.UTF8, cancel);
        var userTrees = csharp.Select(c => CSharpSyntaxTree.ParseText(FixedKeyword.Rewrite(c.Text), parse, c.Path, Encoding.UTF8, cancel)).ToList();
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
            nullableContextOptions: NullableContextOptions.Disable,
            generalDiagnosticOption: ctx.Analyzer.TreatWarningsAsErrors ? ReportDiagnostic.Error : ReportDiagnostic.Default,
            specificDiagnosticOptions: SeverityOptions(ctx.Analyzer));
        var compilation = CSharpCompilation.Create(Names.Identifier(ctx.ProjectName) + "Scripts", userTrees.Prepend(apiTree), ApiReference.All, options);
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new GbaSubsetAnalyzer());
        var withAnalyzers = compilation.WithAnalyzers(analyzers, new CompilationWithAnalyzersOptions(new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty), null, true, false));
        var diagnostics = withAnalyzers.GetAllDiagnosticsAsync(cancel).GetAwaiter().GetResult();
        foreach (var d in diagnostics.Where(d => d.Severity != DiagnosticSeverity.Hidden && !d.IsSuppressed).OrderBy(d => d.Location.SourceTree?.FilePath).ThenBy(d => d.Location.SourceSpan.Start))
        {
            // Unused-variable style warnings of generated code are noise; everything else is reported.
            if (d.Location.SourceTree == apiTree) continue;
            if (d.Id == "CS1998") continue;  // async cutscene without await
            result.Diagnostics.Add(ScriptDiagnostic.From(d, Display));
        }
        if (!result.Success) return result;

        // ---- 3. model, cutscenes, natives ---------------------------------------------------------------------
        var diag = new DiagnosticSink(Display);
        var model = new ScriptModel(compilation, ctx, userTrees, Display);
        var ops = OpcodeTable.Load(ctx.RuntimeIncludeDir);
        var tables = new ScriptTables();
        tables.Natives.AddRange(NativeCatalog.Builtins(ctx.RuntimeIncludeDir));
        tables.BuiltinCount = tables.Natives.Count;
        tables.Natives.Add(new NativeInfo("GameGet", false, "agbgen_native_GameGet", "field: reads a Game field"));
        tables.Natives.Add(new NativeInfo("GameSet", false, "agbgen_native_GameSet", "field, value: writes a Game field"));
        var types = model.UserTypes.OrderBy(t => t.ToDisplayString(), StringComparer.Ordinal).ToList();
        foreach (var t in types)
            foreach (var m in t.GetMembers().OfType<IMethodSymbol>().OrderBy(m => m.Name, StringComparer.Ordinal))
            {
                if (ScriptModel.IsCutscene(m) && m.IsAsync)
                {
                    if (!model.IsTask(m.ReturnType))
                    {
                        diag.Error("AGB0301", m.Locations.FirstOrDefault(), $"Cutscene {m.Name} must return Task or Task<int> (async void is not supported).");
                        continue;
                    }
                    tables.Scripts.Add(new CompiledScript
                    {
                        Method = m,
                        Name = $"{t.Name}.{m.Name}",
                        Symbol = $"agb_cutscene::{Names.Identifier(t.Name)}_{Names.Identifier(m.Name)}",
                        Id = tables.Scripts.Count,
                        HasSelf = !m.IsStatic && model.DerivesFrom(t, model.Behaviour),
                    });
                }
                var waits = ScriptModel.NativeWaits(m);
                if (waits != null)
                {
                    if (!m.IsStatic) { diag.Error("AGB0302", m.Locations.FirstOrDefault(), $"[ScriptNative] method {m.Name} must be static."); continue; }
                    tables.UserNatives.Add(m);
                    tables.Natives.Add(new NativeInfo("User." + t.Name + "." + m.Name, waits.Value, $"agbgen_native_{Names.Identifier(t.Name)}_{Names.Identifier(m.Name)}", m.Name));
                }
            }
        var compiler = new BytecodeCompiler(model, diag, ops, tables);
        foreach (var s in tables.Scripts) compiler.Compile(s);

        // ---- 4. translate types ------------------------------------------------------------------------------
        var indices = new Dictionary<string, int>(ctx.TypeIndices);
        var behaviours = types.Where(t => model.Role(t) == TypeRole.Behaviour && !t.IsAbstract).Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
        foreach (var n in behaviours.Where(n => !indices.ContainsKey(n)))
            indices[n] = Enumerable.Range(0, indices.Count + 1).First(i => !indices.ContainsValue(i));
        result.TypeIndices = indices;
        foreach (var dup in types.GroupBy(t => t.Name).Where(g => g.Count() > 1))
            diag.Error("AGB0002", dup.Last().Locations.FirstOrDefault(), $"Two script types are named {dup.Key}. Type names must be unique because scenes refer to behaviours by name.");
        var translator = new CppTranslator(model, diag, m => tables.Find(m)?.Id ?? -1);
        var outputs = new List<TypeOutput>();
        foreach (var t in types)
        {
            cancel.ThrowIfCancellationRequested();
            outputs.Add(translator.Translate(t, indices.TryGetValue(t.Name, out var i) ? i : -1));
        }
        result.Diagnostics.AddRange(diag.Items);
        if (!result.Success) return result;

        // ---- 5. write -----------------------------------------------------------------------------------------
        Directory.CreateDirectory(outDir);
        var files = new GeneratedFiles(outDir);
        files.Write("agb_prelude.hpp", ProjectApi.Prelude(ctx));
        foreach (var o in outputs.Where(o => o.Emitted))
        {
            files.Write(o.Name + ".hpp", o.Header);
            if (o.Source != null) result.Sources.Add(files.Write(o.Name + ".cpp", o.Source));
        }
        // Saved flags and variables live in GameState::scriptStore (runtime agb::script::PersistentStore).
        var saved = vars.Where(v => v.Id < 0x4000).ToList();
        if (saved.Any(v => v.Type == "bool" && v.Id >= 256) || saved.Any(v => v.Type != "bool" && v.Id >= 32))
            result.Diagnostics.Add(new ScriptDiagnostic("AGB0002", ScriptSeverity.Error,
                "Too many saved script flags or variables: the save keeps at most 256 flags and 32 variables.", "", 1, 1));
        var registry = new RegistryWriter(model, ctx, tables, outputs, ops, vars);
        files.Write(ScriptCompileResult.RegistryHeader, registry.Header());
        result.Sources.Add(files.Write("agb_scripts.cpp", registry.Source()));
        files.Write(ProjectApi.CSharpFileName, result.ProjectApiSource);
        if (ctx.EmitProjectHeaders)
            foreach (var (file, text) in ProjectApi.StandInHeaders(ctx))
            {
                var p = files.Write(file, text);
                if (file.EndsWith(".cpp", StringComparison.Ordinal)) result.Sources.Add(p);
            }

        foreach (var o in outputs.Where(o => o.Role == TypeRole.Behaviour && !o.Abstract))
            result.Behaviours.Add(new BehaviourInfo(o.Name, o.CppName, 64 + o.TypeIndex, o.File, o.Line, $"agb_make_{o.Name}", $"agb_make_{o.Name}_with",
                $"agb_init_{o.Name}", o.Fields.Select(f => new FieldInfo(f.Name, f.EnumCpp ?? f.CsType, f.Kind, f.Default, f.Min, f.Max, f.EnumValues)).ToList()));
        foreach (var s in tables.Scripts)
            result.Scripts.Add(new ScriptInfo(s.Id, s.Name, s.Symbol, s.File, s.Line, s.Locals, s.Args, s.Code, s.Lines));
        result.Natives.AddRange(tables.Natives);
        result.SceneScripts.AddRange(outputs.Where(o => o.Role == TypeRole.SceneScript).Select(o => o.Name));
        files.Write("debug-map.json", DebugMap(model));
        files.Write("scripts.json", Manifest(result, model, files));
        RemoveStale(outDir, files);
        result.Changed.AddRange(files.Changed);
        return result;
    }

    static string DisplayPath(ScriptCompileContext ctx, string path)
    {
        if (ctx.ProjectRoot.Length > 0 && Path.IsPathRooted(path))
        {
            var rel = Path.GetRelativePath(ctx.ProjectRoot, path);
            if (!rel.StartsWith("..", StringComparison.Ordinal)) return rel.Replace('\\', '/');
        }
        return path.Replace('\\', '/');
    }

    static ImmutableDictionary<string, ReportDiagnostic> SeverityOptions(AnalyzerSettings s)
    {
        var b = ImmutableDictionary.CreateBuilder<string, ReportDiagnostic>();
        // Calling a [Cutscene] method without await starts it (Scripting API): not a mistake here.
        b["CS4014"] = ReportDiagnostic.Suppress;
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

    /// <summary>C# class holding the Scene Settings event statements and link conditions of every scene.</summary>
    static string? SceneEventsSource(ScriptCompileContext ctx)
    {
        var scenes = ctx.SceneEvents.Where(e => e.OnLoad.Trim().Length + e.OnEnter.Trim().Length + e.OnExit.Trim().Length > 0 || e.LinkConditions.Count > 0).ToList();
        if (scenes.Count == 0) return null;
        var sb = new StringBuilder("// <auto-generated/>\nusing GBA;\n\nnamespace AgbGenerated\n{\n");
        foreach (var e in scenes)
        {
            sb.Append($"    public sealed class SceneEvents_{BuildCpp.Ident(e.Scene)} : ScriptBase\n    {{\n");
            foreach (var (name, text) in new[] { ("OnLoad", e.OnLoad), ("OnEnter", e.OnEnter), ("OnExit", e.OnExit) })
            {
                if (text.Trim().Length == 0) continue;
                sb.Append($"#line 1 \"{(e.File.Length > 0 ? e.File : e.Scene + ".scene").Replace('\\', '/')}\"\n");
                sb.Append($"        public static void {name}() {{ {SceneStatements(text)} }}\n#line default\n");
            }
            foreach (var (index, cond) in e.LinkConditions.OrderBy(x => x.Key))
            {
                sb.Append($"#line 1 \"{(e.File.Length > 0 ? e.File : e.Scene + ".scene").Replace('\\', '/')}\"\n");
                sb.Append($"        public static bool Link{index}() => {Seconds(cond)};\n#line default\n");
            }
            sb.Append("    }\n");
        }
        sb.Append("}\n");
        return sb.ToString();
    }

    // Scene Settings shows durations as "0.5s"; in C# that is the number of seconds.
    static string Seconds(string text) => Regex.Replace(text, @"(?<![\w.])(\d+(?:\.\d+)?)s\b", "$1");

    static string SceneStatements(string text)
    {
        var lines = Seconds(text).Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0)
            .Select(l => l.EndsWith(';') || l.EndsWith('}') ? l : l + ";");
        return string.Join(" ", lines);
    }

    /// <summary>
    /// debug-map.json for the editor: breakpoints are (fileId &lt;&lt; 16) | line, where fileId indexes "files" (file names;
    /// "paths" holds the project-relative paths), and AGB_SCOPE method ids index "methods".
    /// </summary>
    static string DebugMap(ScriptModel model)
    {
        var files = model.FileIds.OrderBy(f => f.Value).ToList();
        var doc = new
        {
            files = files.Select(f => Path.GetFileName(f.Key)),
            paths = files.Select(f => f.Key),
            methods = model.MethodNames,
        };
        return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true }) + "\n";
    }

    static string Manifest(ScriptCompileResult r, ScriptModel model, GeneratedFiles files)
    {
        var doc = new
        {
            format = 1,
            files = model.FileIds.OrderBy(f => f.Value).Select(f => new { id = f.Value, path = f.Key }),
            behaviours = r.Behaviours.Select(b => new
            {
                name = b.Name, cpp = b.CppName, typeId = b.TypeId, file = b.File, line = b.Line, factory = b.Factory, factoryWith = b.FactoryWith,
                init = b.InitStruct,
                fields = b.Fields.Select(f => new { name = f.Name, type = f.Type, kind = f.Kind, @default = f.Default, min = f.Min, max = f.Max, values = f.EnumValues }),
            }),
            sceneScripts = r.SceneScripts,
            scripts = r.Scripts.Select(s => new { id = s.Id, name = s.Name, symbol = s.Symbol, file = s.File, line = s.Line, locals = s.Locals, args = s.Args, bytes = s.Code.Length,
                lines = s.Lines.Select(l => new[] { l.Pc, l.Line }) }),
            natives = r.Natives.Select((n, i) => new { id = i, name = n.Name, waits = n.Waits, function = n.CppFunction }),
            generated = files.All.Select(Path.GetFileName).Append("scripts.json"),
        };
        return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull }) + "\n";
    }

    /// <summary>Deletes files generated by an earlier run that this run no longer writes (removed classes).</summary>
    static void RemoveStale(string outDir, GeneratedFiles files)
    {
        var manifest = Path.Combine(outDir, "scripts.json");
        var keep = new HashSet<string>(files.All.Select(Path.GetFileName).OfType<string>(), StringComparer.OrdinalIgnoreCase);
        foreach (var f in Directory.EnumerateFiles(outDir))
        {
            var name = Path.GetFileName(f);
            if (keep.Contains(name) || !(name.EndsWith(".hpp", StringComparison.Ordinal) || name.EndsWith(".cpp", StringComparison.Ordinal) || name.EndsWith(".g.cs", StringComparison.Ordinal))) continue;
            var head = File.ReadLines(f).FirstOrDefault() ?? "";
            if (head.StartsWith("// Generated by AGB Studio", StringComparison.Ordinal) || head.StartsWith("// <auto-generated/>", StringComparison.Ordinal)) File.Delete(f);
        }
        _ = manifest;
    }
}
