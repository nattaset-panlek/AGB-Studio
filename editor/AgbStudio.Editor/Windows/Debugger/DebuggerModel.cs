using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Windows.Play;
using AgbStudio.Emu;

namespace AgbStudio.Editor.Windows.Debugger;

public enum BreakpointKind { CSharpLine, ScriptLine, SceneLoad }

/// <summary>A Debugger breakpoint: a C# line, an Event Script (VM) line or "On scene load" (P13.06).</summary>
public sealed class Breakpoint : INotifyPropertyChanged
{
    bool _enabled = true;

    public BreakpointKind Kind { get; init; }
    /// <summary>File name as the Debugger lists it ("PlayerController.cs").</summary>
    public string File { get; init; } = "";
    public int Line { get; init; }
    public bool Enabled { get => _enabled; set { if (_enabled == value) return; _enabled = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Enabled))); } }
    public string Label => Kind == BreakpointKind.SceneLoad ? "On scene load" : $"{File}:{Line}";
    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>One call stack row: method and line ("PlayerController.Update() :19"); engine frames have no line.</summary>
public sealed record StackRow(string Method, int Line)
{
    public string Text => Line > 0 ? $"{Method} :{Line}" : Method;
}

/// <summary>A watched value (Debugger · Memory "Watch memory"): a name the game knows or an address.</summary>
public sealed record WatchItem(string Name, uint Address, int Size, string Value, string? Owner);

/// <summary>A memory map segment: category, bytes and the colour token of its swatch.</summary>
public sealed record MemorySegment(string Category, long Bytes, string Token);

/// <summary>
/// The Debugger's live state (P13.01-P13.06): breakpoints bound through build/gen/scripts (debug-map.json, the AGB_PROBE
/// lines of the generated C++ and the VM line tables in scripts.json), pause location and C# call stack, palette
/// changes, watches and the work RAM map. Fed by <see cref="PlayController.FrameUpdated"/>.
/// </summary>
public sealed class DebuggerModel
{
    readonly EditorServices _s;
    readonly Dictionary<string, SpriteMeta> _metas = new(StringComparer.Ordinal);
    DebugMap? _map;
    string? _mapRom;
    bool _dirty = true;

    DebuggerModel(EditorServices s)
    {
        _s = s;
        Play = PlayController.Of(s);
        Breakpoints.CollectionChanged += (_, e) =>
        {
            if (e.NewItems != null) foreach (Breakpoint b in e.NewItems) b.PropertyChanged += (_, _) => { _dirty = true; Updated?.Invoke(); };
            _dirty = true;
            Updated?.Invoke();
        };
        Play.FrameUpdated += OnFrame;
        Play.Changed += () => { _dirty = true; Updated?.Invoke(); };
        Play.SceneLoaded += OnSceneLoaded;
    }

    public static DebuggerModel Of(EditorServices s)
    {
        if (s.GetService(typeof(DebuggerModel)) is DebuggerModel m) return m;
        m = new DebuggerModel(s);
        s.Set(m);
        return m;
    }

    public PlayController Play { get; }
    public ObservableCollection<Breakpoint> Breakpoints { get; } = [];
    /// <summary>Toolbar bug button: breakpoints armed.</summary>
    public bool Armed { get => _armed; set { _armed = value; _dirty = true; Updated?.Invoke(); } }
    bool _armed = true;

    public bool AtBreakpoint { get; private set; }
    public bool IsPaused => AtBreakpoint || Play.IsPaused;
    /// <summary>"PlayerController.cs:19" while stopped at a breakpoint.</summary>
    public string? PausedAt { get; private set; }
    public long PausedFrame { get; private set; }
    public string? PauseReason { get; private set; }
    public IReadOnlyList<StackRow> CallStack { get; private set; } = [];

    // palettes
    public bool[] ChangedThisFrame { get; } = new bool[512];
    public long[] LastChanged { get; } = Enumerable.Repeat(-1L, 512).ToArray();
    public int SelectedColour { get; set; } = 16 + 7;
    public int? BreakOnColour { get; set; }

    // memory
    public ObservableCollection<string> WatchNames { get; } = [];
    public IReadOnlyList<WatchItem> Watches { get; private set; } = [];
    public int SelectedWatch { get; set; }

    public event Action? Updated;

    // ---- breakpoints -----------------------------------------------------------------------------------

    public Breakpoint AddBreakpoint(string file, int line, bool enabled = true)
    {
        var name = Path.GetFileName(file);
        var existing = Breakpoints.FirstOrDefault(b => b.Kind != BreakpointKind.SceneLoad && b.File == name && b.Line == line);
        if (existing != null) { existing.Enabled = enabled; return existing; }
        var kind = name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && Map?.BindVm(name, line) is null ? BreakpointKind.CSharpLine : BreakpointKind.ScriptLine;
        var b = new Breakpoint { Kind = kind, File = name, Line = line, Enabled = enabled };
        Breakpoints.Add(b);
        return b;
    }

    /// <summary>Script Editor gutter / F9: removes the line's breakpoint, or adds one. True when one was added.</summary>
    public bool ToggleBreakpoint(string file, int line)
    {
        var name = Path.GetFileName(file);
        if (Breakpoints.FirstOrDefault(b => b.Kind != BreakpointKind.SceneLoad && b.File == name && b.Line == line) is { } b)
        {
            RemoveBreakpoint(b);
            return false;
        }
        AddBreakpoint(name, line);
        return true;
    }

    public void RemoveBreakpoint(Breakpoint b)
    {
        if (Breakpoints.Remove(b)) { _dirty = true; Updated?.Invoke(); }
    }

    /// <summary>True when a script line has a breakpoint (the Script Editor gutter dot).</summary>
    public bool HasBreakpoint(string file, int line) => Breakpoints.Any(b => b.Kind != BreakpointKind.SceneLoad && b.File == Path.GetFileName(file) && b.Line == line);

    /// <summary>The list order of the Debugger: line breakpoints as added, "On scene load" last.</summary>
    public static IEnumerable<Breakpoint> InListOrder(IEnumerable<Breakpoint> all) => all.OrderBy(b => b.Kind == BreakpointKind.SceneLoad ? 1 : 0);

    /// <summary>Where a breakpoint stops, as the Breakpoints view says it.</summary>
    public string StopsAt(Breakpoint b)
    {
        if (b.Kind == BreakpointKind.SceneLoad) return "each scene load";
        if (b.Kind == BreakpointKind.ScriptLine) return Map?.BindVm(b.File, b.Line) is { } vm ? $"script line {vm.Item2}" : $"line {b.Line} · not in the build yet";
        return BindCSharp(b) is { } at ? $"line {at.Line}" : $"line {b.Line} · not in the build yet";
    }

    public Breakpoint AddSceneLoad(bool enabled = true)
    {
        var b = Breakpoints.FirstOrDefault(x => x.Kind == BreakpointKind.SceneLoad);
        if (b != null) { b.Enabled = enabled; return b; }
        b = new Breakpoint { Kind = BreakpointKind.SceneLoad, Enabled = enabled };
        Breakpoints.Add(b);
        return b;
    }

    /// <summary>Uses another debug map (tests: a VM script table the fixture has no source for).</summary>
    internal void UseMap(DebugMap map)
    {
        _map = map;
        _mapRom = Play.RomPath;
        _dirty = true;
    }

    DebugMap? Map
    {
        get
        {
            if (Play.RomPath != _mapRom || _map == null)
            {
                _mapRom = Play.RomPath;
                _map = _s.Project.Current is { } p ? DebugMap.Load(Path.Combine(p.BuildDir, "gen", "scripts")) : null;
            }
            return _map;
        }
    }

    /// <summary>Where a breakpoint stops: the probed line itself, or the first statement after a line without one (an opening brace).</summary>
    public (int File, int Line)? BindCSharp(Breakpoint b) => b.Kind == BreakpointKind.CSharpLine ? Map?.BindCSharp(b.File, b.Line) : null;

    void ApplyBreakpoints()
    {
        if (Play.Session?.Channel is not { } ch || Map is not { } map) return;
        var core = Play.Session.Core;
        var cs = new CSharpDebugger(core, ch);
        var vm = new ScriptDebugger(core, ch);
        var enabled = Armed ? Breakpoints.Where(b => b.Enabled).ToList() : [];
        if (cs.Available)
            cs.SetBreakpoints(enabled.Where(b => b.Kind == BreakpointKind.CSharpLine).Select(b => map.BindCSharp(b.File, b.Line)).OfType<(int, int)>().Distinct().ToList());
        if (vm.Available)
            vm.SetBreakpoints(enabled.Where(b => b.Kind != BreakpointKind.SceneLoad).Select(b => map.BindVm(b.File, b.Line)).OfType<(int, int)>().Distinct().ToList());
        _dirty = !(cs.Available && vm.Available);
    }

    void OnSceneLoaded(string scene)
    {
        if (!Armed || !Breakpoints.Any(b => b.Kind == BreakpointKind.SceneLoad && b.Enabled)) return;
        Play.SetPaused(true);
        PauseReason = "On scene load · " + scene;
        Updated?.Invoke();
    }

    // ---- per frame ----------------------------------------------------------------------------------------

    void OnFrame()
    {
        if (Play.Session?.Channel is not { } ch) { AtBreakpoint = false; Updated?.Invoke(); return; }
        try
        {
            if (_dirty) ApplyBreakpoints();
            ReadPause(ch);
            TrackPalettes();
            Watches = ResolveWatches();
        }
        catch (ArgumentOutOfRangeException) { }
        Updated?.Invoke();
    }

    void ReadPause(DebugChannel ch)
    {
        var core = Play.Session!.Core;
        var cs = new CSharpDebugger(core, ch);
        var vm = new ScriptDebugger(core, ch);
        AtBreakpoint = false;
        if (cs.Available && cs.Paused)
        {
            AtBreakpoint = true;
            var (file, line) = cs.PausedAt;
            var name = Map?.FileName(file) ?? file.ToString(CultureInfo.InvariantCulture);
            var shown = Breakpoints.FirstOrDefault(b => b.Kind == BreakpointKind.CSharpLine && b.Enabled && b.File == name && Map?.BindCSharp(b.File, b.Line) == (file, line))?.Line ?? line;
            PausedAt = $"{name}:{shown}";
            PausedFrame = cs.PausedFrame;
            var rows = cs.Stack().Select((f, i) => new StackRow(f.Method, i == 0 ? shown : f.Line)).ToList();
            rows.AddRange(EngineFrames(rows.LastOrDefault()?.Method));
            CallStack = rows;
            PauseReason = "Breakpoint " + PausedAt;
            return;
        }
        if (vm.Available && vm.Paused is { } p)
        {
            AtBreakpoint = true;
            var script = Map?.ScriptName(p.Script) ?? p.Script.ToString(CultureInfo.InvariantCulture);
            PausedAt = $"{script}:{p.Line}";
            PausedFrame = Play.Frame;
            CallStack = [new StackRow(script, p.Line), new StackRow("Script.Run()", 0), new StackRow("Scene.Update()", 0), new StackRow("Game.Frame()", 0)];
            PauseReason = "Breakpoint " + PausedAt;
            return;
        }
        PausedAt = null;
        CallStack = [];
        if (Play.IsPaused) PausedFrame = Play.Frame;
        else PauseReason = null;
    }

    /// <summary>
    /// Engine frames below the outermost script frame. The game loop calls behaviours from fixed places
    /// (runtime game.cpp: game::frame, the scene screen's update, the actor update / touch passes), named as the C#
    /// API names them.
    /// </summary>
    static IEnumerable<StackRow> EngineFrames(string? outermost)
    {
        if (outermost == null) yield break;
        var method = outermost.Contains('.') ? outermost[(outermost.LastIndexOf('.') + 1)..] : outermost;
        yield return new StackRow(method switch
        {
            "OnTouch()" => "Actor.Touch()",
            "Start()" => "Actor.Start()",
            "OnEnter()" or "OnExit()" or "OnLoad()" => "Scene.Load()",
            _ => "Actor.Tick()",
        }, 0);
        yield return new StackRow("Scene.Update()", 0);
        yield return new StackRow("Game.Frame()", 0);
    }

    void TrackPalettes()
    {
        var frame = Play.Frame;
        for (var i = 0; i < 512; i++)
        {
            ChangedThisFrame[i] = Play.Palette[i] != Play.PreviousPalette[i];
            if (ChangedThisFrame[i]) LastChanged[i] = frame;
        }
        if (BreakOnColour is { } c && ChangedThisFrame[c])
        {
            Play.SetPaused(true);
            PauseReason = $"{PaletteName(c)} changed";
        }
    }

    public static string PaletteName(int index) => $"{(index < 256 ? "BG" : "OBJ")} {index / 16 % 16} · index {index % 16}";

    // ---- resume / step / restart ----------------------------------------------------------------------

    public void Resume()
    {
        if (Play.Session?.Channel is { } ch)
        {
            var cs = new CSharpDebugger(Play.Session.Core, ch);
            var vm = new ScriptDebugger(Play.Session.Core, ch);
            if (cs.Available && cs.Paused) cs.Resume();
            if (vm.Available && vm.Paused != null) vm.Resume();
        }
        if (Play.UserPaused) Play.SetPaused(false);
        PauseReason = null;
        Play.RunFrames(1);
    }

    /// <summary>Step to the next line (C# or script) when stopped at a breakpoint, else one frame.</summary>
    public void StepLine()
    {
        if (Play.Session?.Channel is { } ch && AtBreakpoint)
        {
            var cs = new CSharpDebugger(Play.Session.Core, ch);
            var vm = new ScriptDebugger(Play.Session.Core, ch);
            if (cs.Available && cs.Paused) cs.Step();
            else if (vm.Available && vm.Paused != null) vm.Step();
            Play.RunFrames(1);
            return;
        }
        Play.StepFrame();
    }

    public void Restart()
    {
        if (Play.RomPath is { } rom) Play.Run(rom, null, Play.StartedAs);
        _dirty = true;
    }

    // ---- palettes: used by ----------------------------------------------------------------------------

    /// <summary>"BG1 Hills · tiles 12–40" for BG banks (layers whose screen entries use the bank), actors for OBJ banks.</summary>
    public IReadOnlyList<string> UsedBy(int index)
    {
        var list = new List<string>();
        if (Play.Core is not { } core) return list;
        var bank = index / 16 % 16;
        try
        {
            if (index < 256)
            {
                var (_, bg, _) = Inspector.Display(core);
                var map = MapLayers();
                for (var n = 0; n < 4; n++)
                {
                    if (!bg[n]) continue;
                    var cnt = core.ReadU16(GbaMemory.Io + 8 + (uint)n * 2);
                    var sbb = (uint)((cnt >> 8) & 31);
                    var size = (cnt >> 14) & 3;
                    var entries = size switch { 0 => 1024, 3 => 4096, _ => 2048 };
                    var data = core.Memory(GbaMemory.Vram + sbb * 0x800, entries * 2);
                    int lo = int.MaxValue, hi = -1;
                    for (var i = 0; i < entries; i++)
                    {
                        var e = data[i * 2] | (data[i * 2 + 1] << 8);
                        if (e >> 12 != bank || (e & 0x3FF) == 0) continue;
                        lo = Math.Min(lo, e & 0x3FF);
                        hi = Math.Max(hi, e & 0x3FF);
                    }
                    if (hi < 0) continue;
                    var layer = map.GetValueOrDefault(n);
                    list.Add($"BG{n}{(layer is null ? "" : " " + layer)} · tiles {lo}–{hi}");
                }
            }
            else
            {
                foreach (var row in OamRows().Where(r => r.Entry.Palette == bank).GroupBy(r => r.Actor))
                    list.Add($"{row.Key} · OAM {string.Join(", ", row.Select(r => r.Entry.Index))}");
            }
        }
        catch (ArgumentOutOfRangeException) { }
        list.AddRange(EffectsOn(index));
        return list;
    }

    /// <summary>
    /// The project's palette effects that change a colour (Palette Manager · Fade / Cycle / Flash on one bank):
    /// "Palette effect · water cycle (6–9)". Effects on every bank (a screen fade) are not listed per colour.
    /// </summary>
    public IReadOnlyList<string> EffectsOn(int index)
    {
        if (_s.Project.Current is null) return [];
        PaletteEffectsData effects;
        try { effects = Palette.ArtLibrary.For(_s).Effects; }
        catch (Exception e) when (e is IOException or System.Text.Json.JsonException or InvalidOperationException) { return []; }
        var kind = index < 256 ? BankKind.Bg : BankKind.Obj;
        int bank = index / 16 % 16, entry = index % 16;
        return new[] { effects.Fade, effects.Cycle, effects.Flash }
            .Where(e => e.Enabled && e.Bank >= 0 && e.BankKind == kind && e.Bank == bank && entry >= e.First && entry <= e.Last)
            .Select(e => $"Palette effect · {e.Name} {e.Kind.ToString().ToLowerInvariant()} ({e.First}–{e.Last})")
            .ToList();
    }

    Dictionary<int, string> MapLayers()
    {
        var d = new Dictionary<int, string>();
        var p = _s.Project.Current;
        var scene = Play.WorldReader?.CurrentScene(Play.World);
        if (p == null || scene == null || scene.Map.Length == 0) return d;
        try
        {
            var path = Path.Combine(p.MapsDir, scene.Map + ".map");
            if (!File.Exists(path)) return d;
            foreach (var l in GameProject.Load<MapFile>(path).Layers) d.TryAdd(l.Bg, l.Name);
        }
        catch (JsonException) { }
        return d;
    }

    // ---- OAM: actor of each entry ----------------------------------------------------------------------

    /// <summary>OAM entries with the actor drawn by each (matched by screen position, sprite origin and size); "—" when none.</summary>
    public IReadOnlyList<(OamEntry Entry, string Actor)> OamRows()
    {
        var w = Play.World;
        var p = _s.Project.Current;
        var rows = new List<(OamEntry, string)>();
        foreach (var e in Play.Oam)
        {
            string? best = null;
            var bestD = 3.0;
            foreach (var a in w.Actors)
            {
                if (a.Data?.Sprite is not { Length: > 0 } sprite || p == null) continue;
                var meta = Meta(p, sprite);
                if (meta == null) continue;
                var x = Math.Floor(a.Actor.X) - w.CameraX - meta.OriginX;
                var y = Math.Floor(a.Actor.Y) - w.CameraY - meta.OriginY;
                var d = Math.Abs(x - e.X) + Math.Abs(y - e.Y);
                if (d < bestD) { bestD = d; best = a.Name; }
            }
            rows.Add((e, best ?? "—"));
        }
        return rows;
    }

    SpriteMeta? Meta(GameProject p, string sprite)
    {
        if (_metas.TryGetValue(sprite, out var m)) return m;
        var file = p.SpriteFiles().FirstOrDefault(f => GameProject.AssetName(f) == sprite);
        m = file == null ? null! : p.LoadMeta<SpriteMeta>(file);
        _metas[sprite] = m;
        return m;
    }

    // ---- memory ---------------------------------------------------------------------------------------

    /// <summary>The work RAM by category from the ELF symbols; IWRAM's stack from the live high-water mark.</summary>
    public (IReadOnlyList<MemorySegment> Ewram, IReadOnlyList<MemorySegment> Iwram) MemoryMap()
    {
        var elf = Play.Elf;
        var ew = new Dictionary<string, long> { ["Actor pool"] = 0, ["Tilemap buffers"] = 0, ["Audio mixer"] = 0, ["Strings"] = 0, ["Other"] = 0 };
        var iw = new Dictionary<string, long> { ["Fast code (ARM)"] = 0, ["Stack"] = 0, ["Interrupts"] = 0, ["Globals"] = 0 };
        if (elf != null)
        {
            foreach (var s in elf.Symbols.Where(s => s.Size > 0))
            {
                if (s.Address is >= 0x02000000 and < 0x02040000) ew[EwramCategory(s.Name)] += s.Size;
                else if (s.Address is >= 0x03000000 and < 0x03008000)
                {
                    var isr = s.Name.Contains("isr", StringComparison.OrdinalIgnoreCase) || s.Name.Contains("irq", StringComparison.OrdinalIgnoreCase);
                    iw[isr ? "Interrupts" : s.IsFunction ? "Fast code (ARM)" : "Globals"] += s.Size;
                }
            }
            iw["Interrupts"] += 0x60;  // IRQ stack below the BIOS area (devkitARM crt0: 0x03007FA0)
            iw["Stack"] = StackBytes(elf);
        }
        string[] ewTokens = ["accent/default", "status/success", "kind/audio", "kind/data", "text/muted"];
        string[] iwTokens = ["kind/script", "lit/accent-pink", "lit/accent-orange", "kind/data"];
        return (ew.Select((kv, i) => new MemorySegment(kv.Key, kv.Value, ewTokens[i])).Where(x => x.Category != "Other" || x.Bytes > 0).ToList(),
                iw.Select((kv, i) => new MemorySegment(kv.Key, kv.Value, iwTokens[i])).ToList());
    }

    static string EwramCategory(string name)
    {
        if (Regex.IsMatch(name, @"actor|g_debug$|g_screenArena|sprites::|script::", RegexOptions.IgnoreCase)) return "Actor pool";
        if (Regex.IsMatch(name, @"render::|bg::|commit::|palettes::|map", RegexOptions.IgnoreCase)) return "Tilemap buffers";
        if (Regex.IsMatch(name, @"audio::|^mm|^mp|mixer", RegexOptions.IgnoreCase)) return "Audio mixer";
        if (Regex.IsMatch(name, @"text::|ui::|string", RegexOptions.IgnoreCase)) return "Strings";
        return "Other";
    }

    /// <summary>Bytes of IWRAM stack the game has used: from the user stack top (0x03007F00) down to the deepest non-zero word above the static data.</summary>
    long StackBytes(ElfSymbols elf)
    {
        if (Play.Core is not { } core) return 0;
        var end = elf.Sections.Where(s => s.Allocated && s.Address is >= 0x03000000 and < 0x03008000).Select(s => s.Address + s.Size).DefaultIfEmpty(0x03000000u).Max();
        const uint top = 0x03007F00;
        try
        {
            var deepest = top;
            for (var a = top - 4; a >= end && a > 0x03000000; a -= 4)
                if (core.ReadU32(a) != 0) deepest = a;
            return top - deepest;
        }
        catch (ArgumentOutOfRangeException) { return 0; }
    }

    // ---- watches -------------------------------------------------------------------------------------------

    /// <summary>The watch list of Figma: player position, velocity and lives, Game.score and the save buffer (when the game has them).</summary>
    public void AddDefaultWatches()
    {
        if (WatchNames.Count > 0) return;
        foreach (var n in new[] { "player.position", "player.velocity", "player.lives", "Game.score", "Save.buffer" }) WatchNames.Add(n);
        Watches = ResolveWatches();
    }

    /// <summary>Adds a watch by name ("player.position", "Game.score", "Save.buffer") or address ("0x02001A40"); false if it does not resolve.</summary>
    public bool AddWatch(string text)
    {
        text = text.Trim();
        if (text.Length == 0 || WatchNames.Contains(text)) return false;
        if (Play.Core != null && Resolve(text) == null) return false;
        WatchNames.Add(text);
        Watches = ResolveWatches();
        Updated?.Invoke();
        return true;
    }

    IReadOnlyList<WatchItem> ResolveWatches() => WatchNames.Select(n => Resolve(n) ?? new WatchItem(n, 0, 0, "—", null)).ToList();

    WatchItem? Resolve(string name)
    {
        if (Play.Core is not { } core) return null;
        try
        {
            if (name.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && uint.TryParse(name.AsSpan(2).ToString().Replace(" ", "").Replace("_", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var addr))
                return new WatchItem(name, addr, 4, "0x" + core.ReadU32(addr).ToString("X8", CultureInfo.InvariantCulture), null);
            var dot = name.IndexOf('.');
            if (dot <= 0) return null;
            var owner = name[..dot];
            var member = name[(dot + 1)..];
            var state = Play.Session?.Channel?.Game is { } g and not 0 ? core.ReadU32(g + 8) : 0;
            if (owner.Equals("Save", StringComparison.OrdinalIgnoreCase) && member == "buffer" && state != 0)
            {
                var size = SaveFields().Sum(f => f.SizeBytes());
                return new WatchItem(name, state, size, $"{size} bytes", null);
            }
            if (owner.Equals("Game", StringComparison.OrdinalIgnoreCase) && state != 0)
            {
                var offset = 8;  // SaveHeader
                foreach (var f in SaveFields())
                {
                    var size = f.SizeBytes();
                    var align = f.Type is "int" or "fixed" or "fixed2" ? 4 : f.Type is "short" || f.Type.Contains('[') ? 2 : 1;
                    offset = (offset + align - 1) / align * align;
                    if (f.Name == member)
                    {
                        var at = state + (uint)offset;
                        var v = f.Type switch
                        {
                            "byte" or "bool" => core.Memory(at, 1)[0].ToString(CultureInfo.InvariantCulture),
                            "short" => ((short)core.ReadU16(at)).ToString(CultureInfo.InvariantCulture),
                            "fixed" => ((int)core.ReadU32(at) / 256.0).ToString("0.00", CultureInfo.InvariantCulture),
                            _ => ((int)core.ReadU32(at)).ToString(CultureInfo.InvariantCulture),
                        };
                        return new WatchItem(name, at, size, v, null);
                    }
                    offset += size;
                }
                return null;
            }
            var actor = Play.World.Actors.FirstOrDefault(a => a.Name.Equals(owner, StringComparison.OrdinalIgnoreCase));
            if (actor == null) return null;
            if (member is "position" or "velocity")
            {
                var at = actor.Address + (member == "position" ? 12u : 20u);
                double x = (int)core.ReadU32(at) / 256.0, y = (int)core.ReadU32(at + 4) / 256.0;
                return new WatchItem(name, at, 8, string.Create(CultureInfo.InvariantCulture, $"({x:0.00}, {y:0.00})"), actor.Name);
            }
            var field = actor.Behaviours.Select(b => b.Field(member)).FirstOrDefault(f => f != null);
            return field == null ? null : new WatchItem(name, field.Address, 4, field.Text, actor.Name);
        }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    IEnumerable<SaveField> SaveFields() => _s.Project.Current?.Settings.Save.Fields ?? [];

    /// <summary>"0x0200 1A40" as the Debugger writes addresses.</summary>
    public static string Address(uint a) => string.Create(CultureInfo.InvariantCulture, $"0x{a >> 16:X4} {a & 0xFFFF:X4}");
}

/// <summary>build/gen/scripts: file ids (debug-map.json), probed lines per file (AGB_PROBE in the generated C++) and the VM scripts' line tables (scripts.json).</summary>
public sealed class DebugMap
{
    readonly List<string> _files = [];
    readonly Dictionary<int, SortedSet<int>> _probes = [];
    readonly List<(int Id, string Name, string File, HashSet<int> Lines)> _scripts = [];

    public static DebugMap? Load(string genDir)
    {
        var map = Path.Combine(genDir, "debug-map.json");
        if (!File.Exists(map)) return null;
        var m = new DebugMap();
        try
        {
            using (var doc = JsonDocument.Parse(File.ReadAllText(map)))
                foreach (var f in doc.RootElement.GetProperty("files").EnumerateArray()) m._files.Add(f.GetString() ?? "");
            var rx = new Regex(@"AGB_PROBE\((\d+), (\d+)\)", RegexOptions.Compiled);
            foreach (var cpp in Directory.EnumerateFiles(genDir, "*.cpp"))
                foreach (Match x in rx.Matches(File.ReadAllText(cpp)))
                {
                    var id = int.Parse(x.Groups[1].Value, CultureInfo.InvariantCulture);
                    if (!m._probes.TryGetValue(id, out var set)) m._probes[id] = set = [];
                    set.Add(int.Parse(x.Groups[2].Value, CultureInfo.InvariantCulture));
                }
            var manifest = Path.Combine(genDir, "scripts.json");
            if (File.Exists(manifest))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(manifest));
                if (doc.RootElement.TryGetProperty("scripts", out var scripts))
                    foreach (var s in scripts.EnumerateArray())
                    {
                        var lines = new HashSet<int>();
                        if (s.TryGetProperty("lines", out var ls)) foreach (var l in ls.EnumerateArray()) lines.Add(l[1].GetInt32());
                        m._scripts.Add((s.GetProperty("id").GetInt32(), s.GetProperty("name").GetString() ?? "", Path.GetFileName(s.GetProperty("file").GetString() ?? ""), lines));
                    }
            }
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or IOException) { }
        return m;
    }

    public string? FileName(int id) => id >= 0 && id < _files.Count ? _files[id] : null;
    public string? ScriptName(int id) => _scripts.FirstOrDefault(s => s.Id == id).Name;

    public (int File, int Line)? BindCSharp(string file, int line)
    {
        var id = _files.FindIndex(f => f.Equals(file, StringComparison.OrdinalIgnoreCase));
        if (id < 0 || !_probes.TryGetValue(id, out var lines)) return null;
        if (lines.Contains(line)) return (id, line);
        var next = lines.GetViewBetween(line + 1, line + 3);
        return next.Count > 0 ? (id, next.Min) : null;
    }

    public (int Script, int Line)? BindVm(string file, int line)
    {
        foreach (var s in _scripts)
            if (s.File.Equals(file, StringComparison.OrdinalIgnoreCase) && s.Lines.Contains(line)) return (s.Id, line);
        return null;
    }
}
