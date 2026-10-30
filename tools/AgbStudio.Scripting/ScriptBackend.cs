using System.Text.Json;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;
using BuildCpp = AgbStudio.Build.Pipeline.Cpp;
using BuildDiagnostic = AgbStudio.Build.Pipeline.Diagnostic;

namespace AgbStudio.Scripting;

/// <summary>
/// The scripting compiler as the game build's script step (GameBuilder.Scripts = new ScriptBackend()). Reads the
/// project's .cs, .graph and .dlg files, the Localization keys, database tables and scene events, and returns the
/// generated C++ sources, the factory calls for scene actors and the registry header.
/// </summary>
public sealed class ScriptBackend : IScriptBackend
{
    /// <summary>Result of the last compile (Script Editor diagnostics, Inspector fields, Debugger maps).</summary>
    public ScriptCompileResult? Last { get; private set; }

    public ScriptOutput Compile(GameProject project, ScriptInput input, IBuildObserver observer)
    {
        var output = new ScriptOutput();
        var ctx = Context(project, input);
        var sources = project.ScriptFiles().Concat(project.GraphFiles()).Concat(project.DialogueFiles())
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).Select(ScriptSource.FromFile).ToList();
        var r = new ScriptCompiler().Compile(sources, ctx, input.OutputDir);
        Last = r;
        foreach (var d in r.Diagnostics)
        {
            var bd = ToBuild(d, project.Root);
            output.Diagnostics.Add(bd);
            observer.Message(bd);
        }
        output.ScriptCount = sources.Count;
        if (!r.Success) return output;
        SaveTypeIndices(input.OutputDir, r.TypeIndices);
        output.Sources.AddRange(r.Sources);
        output.IncludeDirs.Add(r.IncludeDir);
        output.FactoryCall = r.FactoryCall;
        output.RegistryHeader = ScriptCompileResult.RegistryHeader;
        foreach (var f in r.Changed) observer.Line("generated " + Path.GetFileName(f));
        // P14.02: keep the external IDE project current once the user created it.
        if (File.Exists(CsprojGenerator.ProjectFile(project.Root, project.Settings.Name)))
            CsprojGenerator.Write(project.Root, project.Settings.Name, ctx, r);
        return output;
    }

    static BuildDiagnostic ToBuild(ScriptDiagnostic d, string root)
    {
        var sev = d.Severity switch { ScriptSeverity.Error => Severity.Error, ScriptSeverity.Warning => Severity.Warning, _ => Severity.Info };
        var file = d.File.Length == 0 ? "" : Path.IsPathRooted(d.File) ? d.File : Path.GetFullPath(Path.Combine(root, d.File));
        var gae = d.Id.StartsWith("GAE", StringComparison.Ordinal);
        return new BuildDiagnostic(sev, file, d.Line, d.Column, gae ? d.Id : "", gae ? d.ShortMessage ?? d.Message : d.Message);
    }

    /// <summary>Fills the compile context from the project and the build's ids.</summary>
    public static ScriptCompileContext Context(GameProject project, ScriptInput input)
    {
        var s = project.Settings;
        var ctx = new ScriptCompileContext
        {
            ProjectName = s.Name,
            ProjectRoot = project.Root,
            Namespace = s.Scripting.Namespace,
            ShopCurrencyField = s.Scripting.ShopCurrency,
            DebugBuild = input.Debug,
            Analyzer = AnalyzerSettings.From(s.Scripting.AnalyzerRules, s.Scripting.TreatWarningsAsErrors),
            Scenes = new Dictionary<string, int>(input.Scenes),
            Tags = new Dictionary<string, int>(input.Tags),
            SaveFields = input.GameState,
            TypeIndices = LoadTypeIndices(input.OutputDir),
        };
        // Sound and music names are the file names; the build keys its ids by the soundbank symbol (JUMP).
        ctx.Sounds = Named(project.SoundFiles(), input.Sounds);
        ctx.Music = Named(project.MusicFiles(), input.Music);
        // String ids: every key of the Localization tables, sorted (same order as the build's agb_text ids).
        var keys = project.StringTables().Select(GameProject.Load<StringTable>).SelectMany(t => t.Strings.Keys).Distinct().OrderBy(k => k, StringComparer.Ordinal);
        ctx.Strings = ScriptCompileContext.Indexed(keys);
        ctx.Tables = project.TableFiles().Select(GameProject.Load<TableFile>).ToList();
        foreach (var f in project.SceneFiles())
        {
            var scene = GameProject.Load<SceneFile>(f);
            var e = new SceneEventsDef
            {
                Scene = GameProject.AssetName(f),
                File = f,
                OnLoad = scene.Events.OnLoad,
                OnEnter = scene.Events.OnEnter,
                OnExit = scene.Events.OnExit,
            };
            for (var i = 0; i < scene.Links.Count; i++)
                if (scene.Links[i].OnlyIf.Trim().Length > 0) e.LinkConditions[i] = scene.Links[i].OnlyIf.Trim();
            ctx.SceneEvents.Add(e);
        }
        return ctx;
    }

    static Dictionary<string, int> Named(IEnumerable<string> files, Dictionary<string, int> ids)
    {
        var d = new Dictionary<string, int>();
        foreach (var f in files)
        {
            var stem = Path.GetFileNameWithoutExtension(f);
            if (ids.TryGetValue(BuildCpp.Ident(stem).ToUpperInvariant(), out var id) || ids.TryGetValue(stem, out id)) d.TryAdd(stem, id);
        }
        foreach (var (k, v) in ids) if (!d.ContainsValue(v)) d.TryAdd(k, v);
        return d;
    }

    static string TypeIdFile(string outDir) => Path.Combine(outDir, "type-ids.json");

    static Dictionary<string, int> LoadTypeIndices(string outDir)
    {
        try
        {
            var path = TypeIdFile(outDir);
            return File.Exists(path) ? JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(path)) ?? new() : new();
        }
        catch (JsonException)
        {
            return new();
        }
    }

    static void SaveTypeIndices(string outDir, Dictionary<string, int> map)
    {
        var text = JsonSerializer.Serialize(map.OrderBy(x => x.Value).ToDictionary(x => x.Key, x => x.Value), new JsonSerializerOptions { WriteIndented = true });
        var path = TypeIdFile(outDir);
        if (!File.Exists(path) || File.ReadAllText(path) != text) File.WriteAllText(path, text);
    }
}
