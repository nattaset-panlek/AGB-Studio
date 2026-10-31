using System.Diagnostics;
using System.Globalization;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using AgbStudio.Emu;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace AgbStudio.Editor.Windows.Play;

/// <summary>How Play Mode was started: Build › Play, Play from Here, Build &amp; Run in Emulator, or Build Report › Run in mGBA (no build).</summary>
public enum PlayStart { Play, FromHere, BuildAndRun, RunBuilt }

public enum PlayState { Stopped, Building, Running, BuildFailed }

/// <summary>
/// Play / Simulate (Phase 12, D-16): builds the open project incrementally (Debug), runs the ROM in the editor's mGBA
/// core and publishes everything the Play Mode page, the Debugger and the Budget read each frame: picture, audio,
/// input, live actors, profiler, console, save file and hot reload. One per editor (<see cref="Of"/>).
/// </summary>
public sealed class PlayController : IDisposable
{
    readonly EditorServices _s;
    readonly Stopwatch _clock = new();
    readonly Queue<(double T, long Frames)> _fpsWindow = new();
    DispatcherTimer? _timer;
    TimeSpan _lastTick;
    IAudioSink? _audio;
    GbaKeys _keyboardKeys, _padKeys;
    readonly HashSet<Key> _held = [];
    readonly Dictionary<EditorDocument, bool> _dirty = [];
    Window? _window;
    bool _userPaused, _autoPaused;
    int _lastScene = -1, _sceneSettle;
    long _paletteFrame = -1;
    bool _quietHandover;
    // Stop and Dispose bump it, so a Play whose build is still running does not start the game when the build ends
    int _starts;
    // true while this controller builds (Play, hot reload); other builds of the project reload the running game
    bool _ownBuild;

    PlayController(EditorServices s)
    {
        _s = s;
        Console = s.GetService(typeof(PlayConsole)) as PlayConsole ?? new PlayConsole();
        s.Set(Console);
        s.Documents.Open.CollectionChanged += (_, _) => WatchDocuments();
        WatchDocuments();
        s.Build.Finished += OnBuildFinished;
        s.Preferences.Saved += ApplyPreferences;
        Debugger.BuildHistory.Of(s);
        // Pause / Step / Stop act on a running game only (Scene Editor and Play Mode toolbars, F10)
        if (s.Commands.TryGet("play.pause", out var pause)) pause.Handle(TogglePause, () => IsRunning);
        if (s.Commands.TryGet("play.frameStep", out var step)) step.Handle(StepFrame, () => IsRunning);
        if (s.Commands.TryGet("play.stop", out var stop)) stop.Handle(Stop);
        Changed += () =>
        {
            foreach (var id in new[] { "play.pause", "play.frameStep", "play.stop" })
                if (s.Commands.TryGet(id, out var c)) c.RaiseCanExecuteChanged();
        };
    }

    /// <summary>The controller of these services (created on first use and shared with <c>Services.Get&lt;PlayController&gt;()</c>).</summary>
    public static PlayController Of(EditorServices s)
    {
        if (s.GetService(typeof(PlayController)) is PlayController c) return c;
        c = new PlayController(s);
        s.Set(c);
        return c;
    }

    public EditorServices Services => _s;
    public PlayConsole Console { get; }
    /// <summary>The emulator settings of Play Mode, from Preferences · Emulator (updated when Preferences are saved).</summary>
    public EmulatorOptions Options => _s.GetService(typeof(EmulatorOptions)) as EmulatorOptions ?? SetDefaultOptions();
    EmulatorOptions SetDefaultOptions() { var o = new EmulatorOptions().ReadFrom(_s.Preferences.Current); _s.Set(o); return o; }

    /// <summary>Preferences were saved: the running game takes the new emulator settings at once (scale, filter,
    /// audio on / off, pause when unfocused, fast-forward key and speed).</summary>
    void ApplyPreferences()
    {
        var o = Options.ReadFrom(_s.Preferences.Current);
        if (Session != null)
        {
            Session.FastForward = o.FastForwardSpeed;
            if (!o.Audio && _audio != null) { _audio.Dispose(); _audio = null; }
            else if (o.Audio && _audio == null && IsRunning) OpenAudio(Session.Core);
            if (!o.PauseWhenUnfocused && _autoPaused) { _autoPaused = false; ApplyPause(); }
        }
        Changed?.Invoke();
    }

    /// <summary>The running session (null when stopped).</summary>
    public PlaySession? Session { get; private set; }
    public LibretroCore? Core => Session?.Core;
    public PlayState State { get; private set; }
    public PlayStart StartedAs { get; private set; }
    public BuildResult? LastBuild { get; private set; }
    public string? RomPath { get; private set; }
    public ElfSymbols? Elf { get; private set; }
    public LiveWorldReader? WorldReader { get; private set; }
    public LiveWorld World { get; private set; } = LiveWorld.Empty;
    public ProfilerStats? Profile { get; private set; }
    public IReadOnlyList<OamEntry> Oam { get; private set; } = [];
    /// <summary>Palette RAM of this frame and of the previous one (Debugger · Palettes "changed this frame").</summary>
    public ushort[] Palette { get; private set; } = new ushort[512];
    public ushort[] PreviousPalette { get; private set; } = new ushort[512];

    /// <summary>When false (tests), no timer runs: call <see cref="RunFrames"/> or <see cref="Tick"/> to advance.</summary>
    public bool UseTimer { get; set; } = true;
    /// <summary>Creates the audio output when a session starts (WaveOut by default; tests count samples).</summary>
    public Func<IAudioSink> AudioFactory { get; set; } = () => new WaveOutSink();
    public IAudioSink? Audio => _audio;
    /// <summary>Polls the gamepad each tick (XInput by default).</summary>
    public Func<XInputPad?> Gamepad { get; set; } = XInputPad.Poll;

    /// <summary>Overlays of the game view (toolbar chips "Hitboxes" and "Tile collision").</summary>
    public bool Hitboxes { get; set; } = true;
    public bool TileCollision { get; set; }
    /// <summary>The actor shown in Inspector · Live (the Hierarchy selection; Player by default).</summary>
    public string? SelectedActor { get; set; } = "Player";

    public bool IsRunning => State == PlayState.Running && Session?.Running == true;
    public bool IsPaused => IsRunning && Session!.Paused;
    public bool UserPaused => _userPaused;
    public double Speed
    {
        get => Session?.Speed ?? _speed;
        set { _speed = value; if (Session != null) Session.Speed = value; Changed?.Invoke(); }
    }
    double _speed = 1;
    public bool FastForwarding => Session?.FastForwarding == true;
    public bool HotReloadEnabled => _s.Project.Current?.Settings.Scripting.HotReload ?? true;
    /// <summary>The game's frame counter (debug channel) or the emulator's when the game has none.</summary>
    public long Frame => Session?.Channel is { } ch ? SafeGameFrame(ch) : Session?.Frame ?? 0;
    public double Fps { get; private set; }
    public double VBlankFreeMs => Profile is { } p ? ProfilerStats.Ms(p.VBlankFree) : 0;
    public double CpuPercent => Profile?.CpuPercent ?? 0;
    public GbaKeys Keys => Session?.Keys ?? GbaKeys.None;
    /// <summary>The pending hot reload (tests await it).</summary>
    public Task? PendingReload { get; private set; }
    public string SaveFilePath => Path.Combine(_s.Project.Current?.BuildDir ?? Path.GetTempPath(), "play", Path.GetFileNameWithoutExtension(RomPath ?? "game") + ".sav");

    /// <summary>State changes (start, stop, pause, speed).</summary>
    public event Action? Changed;
    /// <summary>After each batch of frames, with the new picture and live data.</summary>
    public event Action? FrameUpdated;
    /// <summary>When a scene finished loading (Debugger "On scene load").</summary>
    public event Action<string>? SceneLoaded;

    static long SafeGameFrame(DebugChannel ch)
    {
        try { return ch.GameFrame; }
        catch (ArgumentOutOfRangeException) { return 0; }
    }

    // ---- start / stop -------------------------------------------------------------------------------

    /// <summary>Builds (except RunBuilt) and starts the game. Returns false when there is no project or the build failed.</summary>
    public async Task<bool> StartAsync(PlayStart how, string? romPath = null)
    {
        if (State == PlayState.Building) return false;
        var project = _s.Project.Current;
        StopSession(save: true);
        var start = ++_starts;
        StartedAs = how;
        if (how != PlayStart.RunBuilt || romPath == null)
        {
            if (project == null) { Console.Add(ConsoleLevel.Error, "No project is open."); return Fail(); }
            if (how == PlayStart.RunBuilt && File.Exists(LastRomOf(project))) romPath = LastRomOf(project);
            else
            {
                State = PlayState.Building;
                Changed?.Invoke();
                BuildResult r;
                _ownBuild = true;
                try { r = await _s.Build.BuildAsync(new BuildOptions { Configuration = BuildConfiguration.Debug }); }
                catch (InvalidOperationException e) { Console.Add(ConsoleLevel.Error, e.Message); return Fail(); }
                finally { _ownBuild = false; }
                if (start != _starts)
                {
                    // stopped (or the editor closed) while building
                    State = PlayState.Stopped;
                    Changed?.Invoke();
                    return false;
                }
                LastBuild = r;
                ReportBuild(r);
                if (!r.Success) return Fail();
                romPath = r.RomPath;
            }
        }
        return Run(romPath!, how == PlayStart.FromHere ? CurrentSceneName() : null, how);
    }

    static string LastRomOf(GameProject p) => new GameBuilder(p, new AgbStudio.Build.Toolchain()).RomPath;

    bool Fail()
    {
        State = PlayState.BuildFailed;
        _s.Toasts.Show("Build failed · see Console", ToastStatus.Error, TimeSpan.FromSeconds(4));
        Changed?.Invoke();
        return false;
    }

    void ReportBuild(BuildResult r)
    {
        foreach (var d in r.Diagnostics.Where(d => d.Severity == Severity.Error)) Console.Add(ConsoleLevel.Error, d.ToString());
        if (!r.Success) { Console.Add(ConsoleLevel.Error, $"Build failed at {r.FailedStep}"); return; }
        var kb = File.Exists(r.RomPath) ? new FileInfo(r.RomPath).Length / 1024 : 0;
        Console.Add(ConsoleLevel.Success, string.Create(CultureInfo.InvariantCulture, $"Build OK in {r.Elapsed.TotalSeconds:0.00} s · {Path.GetFileName(r.RomPath)} {kb} KB"));
        _s.Status.Build = string.Create(CultureInfo.InvariantCulture, $"Build OK · {r.Elapsed.TotalSeconds:0.0} s");
        _s.Status.Rom = $"ROM {kb} KB / 32 MB";
    }

    /// <summary>Runs a built ROM (no build), optionally starting in <paramref name="scene"/> through the hand-over block.</summary>
    public bool Run(string romPath, string? scene = null, PlayStart how = PlayStart.RunBuilt)
    {
        StopSession(save: true);
        StartedAs = how;
        var core = _s.GetService(typeof(LibretroCore)) as LibretroCore ?? _s.Emulator.Acquire();
        RomPath = romPath;
        Elf = ElfSymbols.TryLoad(Path.ChangeExtension(Path.Combine(Path.GetDirectoryName(romPath)!, "obj", Path.GetFileName(romPath)), ".elf"))
              ?? ElfSymbols.TryLoad(Path.ChangeExtension(romPath, ".elf"));
        WorldReader = _s.Project.Current is { } p ? new LiveWorldReader(p, Elf) : null;
        Session = new PlaySession(core) { Speed = _speed, FastForward = Options.FastForwardSpeed };
        Session.LogLine += ReceiveGameLine;
        Session.Start(romPath);
        LoadSaveFile();
        State = PlayState.Running;
        _userPaused = false;
        _autoPaused = false;
        _lastScene = -1;
        World = LiveWorld.Empty;
        if (scene != null) StartInScene(scene);
        OpenAudio(core);
        HookWindow();
        _clock.Restart();
        _lastTick = TimeSpan.Zero;
        _fpsWindow.Clear();
        Fps = PlaySession.Fps;
        if (UseTimer) StartTimer();
        Publish();
        Changed?.Invoke();
        return true;
    }

    /// <summary>The scene of the Scene Editor (the open .scene document, else the project's last scene).</summary>
    public string? CurrentSceneName()
    {
        var doc = _s.Documents.Open.FirstOrDefault(d => d.Path?.EndsWith(".scene", StringComparison.OrdinalIgnoreCase) == true);
        if (doc?.Path is { } path) return GameProject.AssetName(path);
        var last = _s.Project.Current?.Settings.LastScene;
        return string.IsNullOrEmpty(last) ? null : last;
    }

    /// <summary>Play from Here: once the game runs, the Debug runtime's hand-over block (D-15) loads the scene the Scene Editor shows.</summary>
    void StartInScene(string scene)
    {
        var index = WorldReader?.SceneNames.ToList().IndexOf(scene) ?? -1;
        if (index < 0 || Session is null) return;
        for (var i = 0; i < 120 && Session.Channel == null; i++) Session.Step();
        if (Session.Channel == null) return;
        var block = Session.Core.Memory(0x0203F000, 12);
        BitConverter.TryWriteBytes(block, 0x544F4841u);
        BitConverter.TryWriteBytes(block[4..], (ushort)index);
        BitConverter.TryWriteBytes(block[6..], (short)-1);
        BitConverter.TryWriteBytes(block[8..], (short)-1);
        BitConverter.TryWriteBytes(block[10..], (ushort)0);
        _quietHandover = true;
    }

    /// <summary>Stop: saves the save file, unloads the ROM and returns to the Scene Editor (T-C11).</summary>
    public void Stop()
    {
        _starts++;
        StopSession(save: true);
        Changed?.Invoke();
        _ = _s.Windows.Open("T-P01", source: "PlayStop");
    }

    void StopSession(bool save)
    {
        StopTimer();
        if (Session != null)
        {
            if (save) WriteSaveFile();
            Session.LogLine -= ReceiveGameLine;
            Session.Stop();
            Session = null;
        }
        _audio?.Dispose();
        _audio = null;
        UnhookWindow();
        if (State != PlayState.Building) State = PlayState.Stopped;
        World = LiveWorld.Empty;
        Profile = null;
        _keyboardKeys = _padKeys = GbaKeys.None;
        _held.Clear();
    }

    // ---- pause, step, speed ----------------------------------------------------------------------------

    public void SetPaused(bool paused)
    {
        _userPaused = paused;
        ApplyPause();
    }

    public void TogglePause() => SetPaused(!_userPaused);

    void ApplyPause()
    {
        if (Session == null) return;
        Session.Paused = _userPaused || _autoPaused;
        Changed?.Invoke();
    }

    /// <summary>Frame step (Play Speed › Frame step, F10, the Step control): pauses and runs exactly one frame.</summary>
    public void StepFrame()
    {
        if (Session == null) return;
        if (!_userPaused) SetPaused(true);
        Session.Step();
        Publish();
    }

    /// <summary>Runs <paramref name="frames"/> frames now, paused or not (tests and Play from Here).</summary>
    public void RunFrames(int frames)
    {
        if (Session == null) return;
        for (var i = 0; i < frames; i++)
        {
            Session.Step();
            if (i % 8 == 7) ReadFrameState();
        }
        Publish();
    }

    /// <summary>One timer tick: input, as many frames as the elapsed time covers at the current speed, then the new picture.</summary>
    public int Tick(TimeSpan elapsed)
    {
        if (Session == null || !Session.Running) return 0;
        PollInput();
        var n = Session.Advance(elapsed);
        _fpsWindow.Enqueue((_clock.Elapsed.TotalSeconds, Session.Frame));
        while (_fpsWindow.Count > 1 && _clock.Elapsed.TotalSeconds - _fpsWindow.Peek().T > 1) _fpsWindow.Dequeue();
        if (_fpsWindow.Count > 1)
        {
            var (t0, f0) = _fpsWindow.Peek();
            var dt = _clock.Elapsed.TotalSeconds - t0;
            if (dt > 0.25) Fps = (Session.Frame - f0) / dt;
        }
        if (n > 0 || Session.Paused) Publish();
        return n;
    }

    void StartTimer()
    {
        StopTimer();
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(1000 / 120.0), DispatcherPriority.Render, (_, _) =>
        {
            var now = _clock.Elapsed;
            var dt = now - _lastTick;
            _lastTick = now;
            Tick(dt);
        });
        _timer.Start();
    }

    void StopTimer()
    {
        _timer?.Stop();
        _timer = null;
    }

    // ---- input -----------------------------------------------------------------------------------------

    InputMap Map => new(_s.ProjectSettings.Settings.Input);

    /// <summary>A key went down in the game view; true when Play Mode used it (a GBA button or fast-forward).</summary>
    public bool KeyDown(Key key)
    {
        if (Session == null) return false;
        if (InputMap.KeyName(key) == Options.FastForwardKey) { Session.FastForwarding = true; Changed?.Invoke(); return true; }
        var k = Map.Key(key);
        if (k == GbaKeys.None) return false;
        _held.Add(key);
        _keyboardKeys |= k;
        Session.Keys = _keyboardKeys | _padKeys;
        return true;
    }

    public bool KeyUp(Key key)
    {
        if (Session == null) return false;
        if (InputMap.KeyName(key) == Options.FastForwardKey) { Session.FastForwarding = false; Changed?.Invoke(); return true; }
        var k = Map.Key(key);
        if (k == GbaKeys.None) return false;
        _held.Remove(key);
        _keyboardKeys = GbaKeys.None;
        foreach (var h in _held) _keyboardKeys |= Map.Key(h);
        Session.Keys = _keyboardKeys | _padKeys;
        return true;
    }

    void PollInput()
    {
        if (Session == null) return;
        var pad = Gamepad();
        _padKeys = pad is { } p ? Map.Pad(p) : GbaKeys.None;
        Session.Keys = _keyboardKeys | _padKeys;
    }

    // ---- pause when unfocused ------------------------------------------------------------------------

    void HookWindow()
    {
        UnhookWindow();
        if (_s.Top is not Window w) return;
        _window = w;
        w.Activated += OnActivated;
        w.Deactivated += OnDeactivated;
        w.Closed += OnWindowClosed;
    }

    void UnhookWindow()
    {
        if (_window == null) return;
        _window.Activated -= OnActivated;
        _window.Deactivated -= OnDeactivated;
        _window.Closed -= OnWindowClosed;
        _window = null;
    }

    // closing the editor while playing keeps the save, as Stop does
    void OnWindowClosed(object? sender, EventArgs e) => StopSession(save: true);

    void OnActivated(object? sender, EventArgs e) => SetFocused(true);
    void OnDeactivated(object? sender, EventArgs e) => SetFocused(false);

    /// <summary>The editor window gained or lost focus (Preferences · Emulator "Pause when unfocused").</summary>
    public void SetFocused(bool focused)
    {
        _autoPaused = !focused && Options.PauseWhenUnfocused;
        if (!focused && Session != null) { Session.FastForwarding = false; _held.Clear(); _keyboardKeys = GbaKeys.None; }
        ApplyPause();
    }

    // ---- audio -------------------------------------------------------------------------------------------

    void OpenAudio(LibretroCore core)
    {
        _audio?.Dispose();
        _audio = null;
        core.DrainAudio();
        if (!Options.Audio) return;
        _audio = AudioFactory();
        _audio.Open(core.SampleRate > 0 ? (int)core.SampleRate : 32768);
    }

    // ---- per-frame publishing -------------------------------------------------------------------------

    /// <summary>A line from the game's debug channel (Debug.Log, engine warnings, asserts): Play Mode's console and the Console panel.</summary>
    public void ReceiveGameLine(string line)
    {
        if (_quietHandover && line.StartsWith("Hot reload: continued in ", StringComparison.Ordinal)) { _quietHandover = false; return; }
        // the game's own lines (Debug.Log, engine warnings, asserts) also go to the Console panel (T-P05)
        var e = Console.AddGameLine(line);
        SceneEditor.ConsoleLog.Of(_s).Add(new SceneEditor.ConsoleEntry(e.Time, e.Level, e.Text));
    }

    void ReadFrameState()
    {
        if (Session?.Channel is not { } ch || WorldReader is null) return;
        var w = WorldReader.Read(Session.Core, ch);
        if (w.SceneIndex != _lastScene && w.SceneIndex >= 0)
        {
            _lastScene = w.SceneIndex;
            _sceneSettle = 8;
        }
        else if (_sceneSettle > 0 && --_sceneSettle == 0) AnnounceScene(w);
        World = w;
    }

    void AnnounceScene(LiveWorld w)
    {
        if (Session == null) return;
        var (_, bg, _) = Inspector.Display(Session.Core);
        var objs = Inspector.Oam(Session.Core).Count;
        Console.Add(ConsoleLevel.Info, $"Scene {w.SceneName} loaded (BG {bg.Count(b => b)} · OBJ {objs})");
        SceneLoaded?.Invoke(w.SceneName);
    }

    /// <summary>Reads the frame's live data and raises <see cref="FrameUpdated"/>.</summary>
    public void Publish()
    {
        if (Session == null) return;
        var core = Session.Core;
        if (_audio != null) _audio.Write(core.DrainAudio());
        else core.DrainAudio();
        try
        {
            ReadFrameState();
            Profile = Session.Profiler();
            Oam = Inspector.Oam(core);
            // "changed this frame": compare with the palette of the previous frame, however often a frame is published
            if (Frame != _paletteFrame) { PreviousPalette = Palette; _paletteFrame = Frame; }
            Palette = Inspector.PaletteRam(core);
        }
        catch (ArgumentOutOfRangeException) { }
        FrameUpdated?.Invoke();
    }

    // ---- save file -------------------------------------------------------------------------------------

    /// <summary>Copies the project's save file into the cartridge save RAM before the first frame (P12.10).</summary>
    void LoadSaveFile()
    {
        if (Session == null || !File.Exists(SaveFilePath)) return;
        var data = File.ReadAllBytes(SaveFilePath);
        var ram = Session.Core.SaveRam;
        if (ram.Length == 0) return;
        data.AsSpan(0, Math.Min(data.Length, ram.Length)).CopyTo(ram);
    }

    /// <summary>Writes the cartridge save RAM to build/play/&lt;rom&gt;.sav, so saves survive Stop and the next Play.</summary>
    public void WriteSaveFile()
    {
        if (Session == null) return;
        var ram = Session.Core.SaveRam;
        if (ram.Length == 0) return;
        Directory.CreateDirectory(Path.GetDirectoryName(SaveFilePath)!);
        File.WriteAllBytes(SaveFilePath, ram.ToArray());
    }

    // ---- hot reload (D-15) ---------------------------------------------------------------------------

    void WatchDocuments()
    {
        foreach (var d in _s.Documents.Open)
        {
            if (_dirty.ContainsKey(d)) continue;
            _dirty[d] = d.IsDirty;
            d.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(EditorDocument.IsDirty)) return;
                var was = _dirty.GetValueOrDefault(d);
                _dirty[d] = d.IsDirty;
                if (was && !d.IsDirty) OnSaved(d);
            };
        }
    }

    void OnSaved(EditorDocument d)
    {
        if (!IsRunning || !HotReloadEnabled || d.Path is null) return;
        PendingReload = HotReloadAsync(Path.GetFileNameWithoutExtension(d.Path));
    }

    /// <summary>Rebuilds and continues the running game in the new ROM with the same scene, player position and game state.</summary>
    public async Task<bool> HotReloadAsync(string changed)
    {
        if (Session == null || _s.Project.Current == null || _s.Build.IsBuilding) return false;
        BuildResult r;
        _ownBuild = true;
        try { r = await _s.Build.BuildAsync(new BuildOptions { Configuration = BuildConfiguration.Debug }); }
        catch (InvalidOperationException e) { Console.Add(ConsoleLevel.Error, e.Message); return false; }
        finally { _ownBuild = false; }
        LastBuild = r;
        if (!r.Success) { ReportBuild(r); return false; }
        return Reload(r, changed);
    }

    /// <summary>
    /// A build this controller did not start (Build ROM, a script saved in an external editor) finished while the game
    /// runs: the game continues in the new ROM, as after a save (D-15).
    /// </summary>
    void OnBuildFinished(BuildResult r)
    {
        if (_ownBuild || !IsRunning || !HotReloadEnabled || !r.Success || !File.Exists(r.RomPath)) return;
        if (!Dispatcher.UIThread.CheckAccess()) { Dispatcher.UIThread.Post(() => OnBuildFinished(r)); return; }
        LastBuild = r;
        Reload(r, Path.GetFileNameWithoutExtension(r.RomPath));
    }

    bool Reload(BuildResult r, string changed)
    {
        if (Session == null) return false;
        WriteSaveFile();
        var ok = Session.HotReload(r.RomPath);
        LoadSaveFile();
        RomPath = r.RomPath;
        Elf = ElfSymbols.TryLoad(r.ElfPath) ?? Elf;
        if (_s.Project.Current is { } p) WorldReader = new LiveWorldReader(p, Elf);
        _lastScene = -1;
        Console.Add(ok ? ConsoleLevel.Success : ConsoleLevel.Warning,
            ok ? $"Hot reload: {changed} updated in the running game" : $"Hot reload: {changed} rebuilt; the game restarted");
        Publish();
        Changed?.Invoke();
        return ok;
    }

    // ---- live edits ----------------------------------------------------------------------------------

    /// <summary>Writes an Inspector · Live edit into the game (D-15: no rebuild).</summary>
    public void WriteField(LiveField field, double value)
    {
        if (Session == null) return;
        LiveWorldReader.WriteValue(Session.Core, field.Address, field.Kind, value);
        Publish();
    }

    public void WritePosition(LiveActorInfo actor, double x, double y)
    {
        if (Session == null) return;
        LiveWorldReader.WritePosition(Session.Core, actor, x, y);
        Publish();
    }

    public void Dispose()
    {
        _starts++;
        StopSession(save: true);
    }
}
