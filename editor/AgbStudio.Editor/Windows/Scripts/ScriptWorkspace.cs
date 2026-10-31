using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using AgbStudio.Scripting;
using AgbStudio.Scripting.Graph;
using Avalonia.Threading;

namespace AgbStudio.Editor.Windows.Scripts;

/// <summary>One open script: its text, saved state, analysis and Doc Tab.</summary>
public sealed class ScriptBuffer
{
    public required string Path { get; init; }
    public required EditorDocument Document { get; init; }
    public string Text { get; set; } = "";
    public string SavedText { get; set; } = "";
    public AnalysisResult Analysis { get; set; } = AnalysisResult.Empty;
    public int CaretOffset { get; set; }
    /// <summary>Written by the Logic Graph: read-only, the .graph file is the source (D-11).</summary>
    public bool IsGenerated => GraphCodeGen.IsGenerated(Text);
    public string Name => System.IO.Path.GetFileName(Path);
    public string ClassName => System.IO.Path.GetFileNameWithoutExtension(Path);
    public bool IsDirty => Text != SavedText;
}

/// <summary>A line of the Problems / Output panels (Console Line: time, level, text) with an optional source position.</summary>
public sealed record ScriptLogLine(DateTime Time, ConsoleLevel Level, string Text, string? File = null, int Line = 0, int Column = 0)
{
    public string TimeText => Time.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
}

/// <summary>
/// The scripts of the open project as the Script Editor works on them (P11.52-P11.55, P14.04): open buffers and their Doc
/// Tabs, live analysis, save with format on save, the script check (C# -> C++), the Problems / Output log and hot reload
/// when a script is saved by an external editor. One per editor instance (pages are views over it).
/// </summary>
public sealed class ScriptWorkspace : IDisposable
{
    readonly EditorServices _s;
    readonly Dictionary<string, ScriptBuffer> _buffers = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, string> _ownWrites = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, DateTime> _pendingExternal = new(StringComparer.OrdinalIgnoreCase);
    ScriptAnalysis? _analysis;
    FileSystemWatcher? _watcher;
    string? _root;
    ScriptBuffer? _current;

    public ScriptWorkspace(EditorServices s)
    {
        _s = s;
        s.Project.Changed += ProjectChanged;
        s.Build.Finished += BuildFinished;
        ProjectChanged();
    }

    public static ScriptWorkspace Of(EditorServices s)
    {
        if (s.GetService(typeof(ScriptWorkspace)) is ScriptWorkspace w) return w;
        var created = new ScriptWorkspace(s);
        s.Set(created);
        return created;
    }

    /// <summary>Clock for log times (tests fix it).</summary>
    public Func<DateTime> Clock { get; set; } = () => DateTime.Now;
    public ScriptEditorSettings Settings => ScriptEditorSettings.Of(_s);

    /// <summary>Full history: analysis summaries, checks, builds, hot reloads (Output tab).</summary>
    public ObservableCollection<ScriptLogLine> Output { get; } = [];
    /// <summary>Step lines of the last check (C# -> C++, g++, hot reload), shown under the problems.</summary>
    public ObservableCollection<ScriptLogLine> CheckLines { get; } = [];
    public DateTime LastCheckTime { get; private set; }
    public event Action? Changed;
    public event Action<ScriptBuffer>? Reloaded;

    public ScriptBuffer? Current
    {
        get => _current;
        set { if (_current == value) return; _current = value; Changed?.Invoke(); }
    }

    public IReadOnlyCollection<ScriptBuffer> Buffers => _buffers.Values;

    void ProjectChanged()
    {
        var root = _s.Project.Current?.Root;
        if (root == _root) return;
        _root = root;
        _analysis = null;
        _buffers.Clear();
        _current = null;
        _watcher?.Dispose();
        _watcher = null;
        if (_s.Project.Current is { } p && Directory.Exists(p.ScriptsDir))
        {
            _watcher = new FileSystemWatcher(p.ScriptsDir, "*.cs") { IncludeSubdirectories = true, NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size };
            _watcher.Changed += (_, e) => External(e.FullPath);
            _watcher.Created += (_, e) => External(e.FullPath);
            _watcher.Renamed += (_, e) => External(e.FullPath);
            _watcher.EnableRaisingEvents = true;
        }
        Changed?.Invoke();
    }

    public ScriptAnalysis Analysis
    {
        get
        {
            if (_analysis != null) return _analysis;
            _analysis = new ScriptAnalysis(ScriptAnalysis.ContextFor(_s.Project.Current));
            if (_s.Project.Current is { } p)
                foreach (var f in p.ScriptFiles()) _analysis.OtherScripts[f] = ReadText(f);
            return _analysis;
        }
    }

    static string ReadText(string path)
    {
        try { return File.ReadAllText(path).Replace("\r\n", "\n"); }
        catch (IOException) { return ""; }
    }

    // ---- files -------------------------------------------------------------------------------------------

    /// <summary>Scripts of the project for the Scripts list: open ones first (in tab order), then the rest in the order they were created.</summary>
    public IReadOnlyList<string> ScriptFiles()
    {
        if (_s.Project.Current is not { } p) return [];
        var all = p.ScriptFiles().ToList();
        var open = _s.Documents.Open.Where(d => d.Path != null && d.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Select(d => all.FirstOrDefault(f => string.Equals(Path.GetFullPath(f), Path.GetFullPath(d.Path!), StringComparison.OrdinalIgnoreCase))).OfType<string>().ToList();
        var rest = AgbStudio.Editor.Services.CreationOrder.Of(all.Except(open, StringComparer.OrdinalIgnoreCase));
        return [.. open, .. rest];
    }

    /// <summary>Finds a script by path, file name or class name ("PlayerController").</summary>
    public string? Resolve(string nameOrPath)
    {
        if (File.Exists(nameOrPath)) return Path.GetFullPath(nameOrPath);
        var name = Path.GetFileName(nameOrPath);
        if (!name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) name += ".cs";
        return _s.Project.Current?.ScriptFiles().FirstOrDefault(f => string.Equals(Path.GetFileName(f), name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Opens a script (or returns its open buffer), adds its Doc Tab and makes it current.</summary>
    public ScriptBuffer Open(string path)
    {
        path = Path.GetFullPath(path);
        if (!_buffers.TryGetValue(path, out var b))
        {
            var text = ReadText(path);
            var doc = _s.Documents.Open.FirstOrDefault(d => d.Path != null && string.Equals(Path.GetFullPath(d.Path), path, StringComparison.OrdinalIgnoreCase))
                ?? _s.Documents.Add(new EditorDocument { Name = Path.GetFileName(path), Glyph = "code", Path = path });
            b = new ScriptBuffer { Path = path, Document = doc, Text = text, SavedText = text };
            doc.SaveHandler = _ => SaveAsync(b);
            _buffers[path] = b;
        }
        _s.Documents.Active = b.Document;
        Current = b;
        return b;
    }

    /// <summary>Updates a buffer's text from the editor and its Doc Tab dirty dot.</summary>
    public void SetText(ScriptBuffer b, string text)
    {
        b.Text = text;
        b.Document.IsDirty = b.IsDirty;
    }

    // ---- analysis ----------------------------------------------------------------------------------------

    /// <summary>Analyzes a buffer now (colours, messages, outline, exposed fields).</summary>
    public AnalysisResult Analyze(ScriptBuffer b)
    {
        var text = b.Text;
        var r = Analysis.Analyze(b.Path, text);
        if (b.Text == text) b.Analysis = r;
        return r;
    }

    /// <summary>Analyzes off the UI thread and applies the result if the text did not change meanwhile.</summary>
    public async Task<AnalysisResult?> AnalyzeAsync(ScriptBuffer b, CancellationToken cancel = default)
    {
        var text = b.Text;
        var analysis = Analysis;
        try
        {
            var r = await Task.Run(() => analysis.Analyze(b.Path, text, cancel), cancel);
            if (b.Text != text || cancel.IsCancellationRequested) return null;
            b.Analysis = r;
            return r;
        }
        catch (OperationCanceledException) { return null; }
    }

    /// <summary>Warnings and errors of the open scripts (Problems).</summary>
    public IReadOnlyList<(ScriptBuffer Buffer, CodeIssue Issue)> Problems() =>
        _buffers.Values.SelectMany(b => b.Analysis.Issues.Where(i => i.Severity != ScriptSeverity.Info).Select(i => (b, i))).ToList();

    /// <summary>Problems line as in Figma: "GAE002  PlayerController.cs(8,5): float is not supported on GBA — use fixed".</summary>
    public static string ProblemText(ScriptBuffer b, CodeIssue i)
    {
        var hint = "";
        if (i.IsGae && AgbStudio.Analyzers.GaeRules.ShortMessage(i.Id, "", "") is var s && s.Contains('→')) hint = " — " + s[(s.IndexOf('→') + 1)..].Trim();
        return $"{i.Id}  {b.Name}({i.Line},{i.Column}): {i.Message}{hint}";
    }

    // ---- save and check ----------------------------------------------------------------------------------

    /// <summary>Writes a script (format on save first, when on) and runs the script check.</summary>
    public async Task SaveAsync(ScriptBuffer b)
    {
        if (b.IsGenerated) return;
        if (Settings.FormatOnSave)
        {
            var formatted = ScriptAnalysis.Format(b.Text, Settings.TabSize);
            if (formatted != b.Text) Formatted?.Invoke(b, formatted);
            b.Text = formatted;
        }
        Write(b.Path, b.Text);
        b.SavedText = b.Text;
        b.Document.IsDirty = false;
        Analysis.OtherScripts[b.Path] = b.Text;
        Changed?.Invoke();
        await CheckAsync();
    }

    /// <summary>Raised before saving when format on save changed the text (the page applies it as an undoable edit).</summary>
    public event Action<ScriptBuffer, string>? Formatted;

    void Write(string path, string text)
    {
        lock (_ownWrites) _ownWrites[path] = text;
        File.WriteAllText(path, text, new UTF8Encoding(false));
    }

    /// <summary>
    /// The script check: compiles every script of the project like the build's first step (Roslyn + GAE rules, C# -> C++),
    /// and logs "C# → C++  12 scripts translated · 0.21 s" or the errors.
    /// </summary>
    public async Task<ScriptCompileResult?> CheckAsync()
    {
        if (_s.Project.Current is not { } p) return null;
        var ctx = ScriptAnalysis.ContextFor(p);
        var sources = p.ScriptFiles().Concat(p.GraphFiles()).Concat(p.DialogueFiles()).OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .Select(f => _buffers.TryGetValue(Path.GetFullPath(f), out var b) ? new ScriptSource(Path.GetFullPath(f), b.Text) : ScriptSource.FromFile(f)).ToList();
        var outDir = Path.Combine(p.BuildDir, "gen", "scripts");
        var sw = Stopwatch.StartNew();
        ScriptCompileResult r;
        try { r = await Task.Run(() => new ScriptCompiler().Compile(sources, ctx, outDir)); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Log(ConsoleLevel.Error, $"Script check failed: {e.Message.TrimEnd('.')}. Close programs that use the project's build folder, then save the script again to check it.", check: true);
            return null;
        }
        sw.Stop();
        LastCheckTime = Clock();
        CheckLines.Clear();
        var csFiles = sources.Count(x => x.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase));
        if (r.Success)
            Log(ConsoleLevel.Info, string.Create(CultureInfo.InvariantCulture, $"C# → C++  {csFiles} scripts translated · {sw.Elapsed.TotalSeconds:0.00} s"), check: true);
        foreach (var d in r.Diagnostics.Where(d => d.IsError))
            Log(ConsoleLevel.Error, d.ToLogLine(), check: true, file: d.File.Length == 0 ? null : Path.Combine(p.Root, d.File), line: d.Line, column: d.Column);
        Changed?.Invoke();
        return r;
    }

    void BuildFinished(BuildResult r)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var gcc = _s.Toolchain.Find("devkitARM")?.Version ?? "devkitARM";
            var opt = _s.Project.Current?.Settings.Build.Optimise ?? "-O2";
            if (r.StepTimes.TryGetValue(GameBuilder.StepCompile, out var t))
                Log(r.Success ? ConsoleLevel.Info : ConsoleLevel.Error, string.Create(CultureInfo.InvariantCulture, $"g++ {gcc}  {opt} -mthumb  · {t.TotalSeconds:0.00} s"), check: true);
            Changed?.Invoke();
        });
    }

    /// <summary>Adds a log line (Output; with <paramref name="check"/> also the step lines under the problems).</summary>
    public ScriptLogLine Log(ConsoleLevel level, string text, bool check = false, string? file = null, int line = 0, int column = 0)
    {
        var l = new ScriptLogLine(Clock(), level, text, file, line, column);
        Output.Add(l);
        if (check) CheckLines.Add(l);
        Changed?.Invoke();
        return l;
    }

    // ---- new scripts ------------------------------------------------------------------------------------

    /// <summary>Creates scripts/&lt;ClassName&gt;.cs from a template (T-D14) and attaches a Behaviour to an actor ("Bat · Cave_01").</summary>
    public string CreateScript(string className, ScriptTemplates.Kind kind, string? attachTo)
    {
        var p = _s.Project.Current ?? throw new InvalidOperationException("No project is open.");
        var path = Path.Combine(p.ScriptsDir, className + ".cs");
        if (File.Exists(path)) throw new IOException($"{className}.cs already exists.");
        Directory.CreateDirectory(p.ScriptsDir);
        Write(path, ScriptTemplates.Create(kind, className, p.Settings.Scripting.Namespace));
        Analysis.OtherScripts[path] = ReadText(path);
        if (kind == ScriptTemplates.Kind.Behaviour && attachTo is { } target && ParseActor(target) is var (actor, scene))
        {
            var file = Path.Combine(p.ScenesDir, scene + ".scene");
            if (File.Exists(file))
            {
                var sf = GameProject.Load<SceneFile>(file);
                if (sf.Actors.FirstOrDefault(a => a.Name == actor) is { } a && a.Components.All(c => c.Type != className))
                {
                    a.Components.Add(new ComponentData { Type = className });
                    GameProject.Save(file, sf);
                }
            }
        }
        Log(ConsoleLevel.Success, $"Created {Path.GetRelativePath(p.Root, path).Replace('\\', '/')}");
        return path;
    }

    static (string Actor, string Scene)? ParseActor(string text)
    {
        var i = text.LastIndexOf(" · ", StringComparison.Ordinal);
        return i < 0 ? null : (text[..i].Trim(), text[(i + 3)..].Trim());
    }

    /// <summary>Actors of the project as "Actor · Scene" (New Script › Attach to, Script side panel › Attached to).</summary>
    public IReadOnlyList<(string Label, ActorData Actor, string Scene)> Actors()
    {
        if (_s.Project.Current is not { } p) return [];
        var list = new List<(string, ActorData, string)>();
        foreach (var f in p.SceneFiles())
        {
            SceneFile sf;
            try { sf = GameProject.Load<SceneFile>(f); }
            catch (Exception e) when (e is IOException or System.Text.Json.JsonException or InvalidDataException) { continue; }
            var scene = GameProject.AssetName(f);
            foreach (var a in sf.Actors) list.Add(($"{a.Name} · {scene}", a, scene));
        }
        return list;
    }

    /// <summary>"Player · Forest_01": the actors that use a behaviour class (first one, "+N more" when several).</summary>
    public string AttachedTo(string className)
    {
        // the scene being edited first; the same actor (Player) in several scenes counts once
        var last = _s.Project.Current?.Settings.LastScene ?? "";
        var users = Actors().Where(a => a.Actor.Components.Any(c => c.Type == className)).OrderBy(a => a.Scene == last ? 0 : 1).ToList();
        if (users.Count == 0) return "Not attached";
        var names = users.Select(a => a.Actor.Name).Distinct().Count();
        return names == 1 ? users[0].Label : $"{users[0].Label} +{names - 1}";
    }

    // ---- hot reload (P14.04) --------------------------------------------------------------------------

    void External(string path)
    {
        path = Path.GetFullPath(path);
        lock (_pendingExternal) _pendingExternal[path] = DateTime.UtcNow;
        // editors save in several writes: wait until the file is quiet (watcher events arrive on a pool thread)
        _ = Task.Delay(150).ContinueWith(_ => Dispatcher.UIThread.Post(() => FlushExternal(path)), TaskScheduler.Default);
    }

    void FlushExternal(string path)
    {
        lock (_pendingExternal)
        {
            if (!_pendingExternal.TryGetValue(path, out var at)) return;
            if ((DateTime.UtcNow - at).TotalMilliseconds < 120) { External(path); return; }
            _pendingExternal.Remove(path);
        }
        OnExternalSave(path);
    }

    /// <summary>A script changed on disk: reload its buffer (unless it has unsaved edits) and hot reload.</summary>
    public void OnExternalSave(string path)
    {
        if (!File.Exists(path)) return;
        var text = ReadText(path);
        lock (_ownWrites)
            if (_ownWrites.TryGetValue(path, out var own) && own.Replace("\r\n", "\n") == text) return;
        if (_analysis != null) _analysis.OtherScripts[path] = text;
        var name = Path.GetFileNameWithoutExtension(path);
        if (_buffers.TryGetValue(path, out var b))
        {
            if (b.Text == text) return;
            if (b.IsDirty)
            {
                Log(ConsoleLevel.Warning, $"{b.Name} changed on disk; your unsaved edits are kept");
                return;
            }
            b.Text = b.SavedText = text;
            b.Document.IsDirty = false;
            Reloaded?.Invoke(b);
        }
        if (!Settings.HotReloadOnExternalSave || _s.Project.Current?.Settings.Scripting.HotReload == false)
        {
            Log(ConsoleLevel.Info, $"{name}.cs reloaded from disk");
            return;
        }
        _ = HotReloadAsync(name);
    }

    async Task HotReloadAsync(string className)
    {
        var r = await CheckAsync();
        if (r is null || !r.Success) return;
        // D-15: in Play mode the game is rebuilt (Debug) and continues in the new ROM with its state; otherwise the next
        // Play uses the new code
        if (_s.GetService(typeof(Play.PlayController)) is Play.PlayController { IsRunning: true } play && !_s.Build.IsBuilding)
        {
            await play.HotReloadAsync(className);
            Log(ConsoleLevel.Success, $"Hot reload: {className} updated in the running game", check: true);
        }
        else Log(ConsoleLevel.Success, $"Hot reload: {className} updated", check: true);
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _s.Project.Changed -= ProjectChanged;
        _s.Build.Finished -= BuildFinished;
    }
}
