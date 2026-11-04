namespace AgbStudio.Emu;

/// <summary>Play Mode (T-P02): runs a ROM in the embedded core at 59.73 fps with speed control (Play Speed T-M18),
/// pause, frame step, keyboard input and hot reload. The editor calls Advance() from its timer.</summary>
public sealed class PlaySession : IDisposable
{
    public const double Fps = 16777216.0 / 280896.0;  // 59.7275

    readonly LibretroCore _core;
    readonly bool _ownsCore;
    double _accumulator;
    readonly List<string> _log = new();

    public string RomPath { get; private set; } = "";
    public DebugChannel? Channel { get; private set; }
    public bool Running { get; private set; }
    public bool Paused { get; set; }
    public double Speed { get; set; } = 1;          // 0.25, 0.5, 1, 2, 4
    public double FastForward { get; set; } = 4;    // Preferences · Emulator "Fast-forward Tab 4x"
    public bool FastForwarding { get; set; }
    public GbaKeys Keys { get => _core.Keys; set => _core.Keys = value; }
    public long Frame => _core.FrameCount;
    public LibretroCore Core => _core;
    public event Action<string>? LogLine;
    /// <summary>While set, every frame is written down for a replay (Play Mode's Record).</summary>
    public ReplayRecorder? Recorder { get; set; }

    public PlaySession(LibretroCore? core = null)
    {
        _ownsCore = core == null;
        _core = core ?? new LibretroCore();
    }

    public void Start(string romPath)
    {
        RomPath = romPath;
        _core.LoadRom(romPath);
        Channel = null;
        Running = true;
        Paused = false;
        _accumulator = 0;
    }

    public void Stop()
    {
        if (!Running) return;
        _core.UnloadRom();
        Running = false;
        Channel = null;
    }

    /// <summary>Runs as many frames as <paramref name="elapsed"/> real time covers at the current speed (max 8 per call).</summary>
    public int Advance(TimeSpan elapsed)
    {
        if (!Running || Paused) return 0;
        _accumulator += elapsed.TotalSeconds * Fps * (FastForwarding ? FastForward : Speed);
        var n = 0;
        while (_accumulator >= 1 && n < 8)
        {
            RunOneFrame();
            _accumulator -= 1;
            n++;
        }
        if (_accumulator > 8) _accumulator = 0;  // never try to catch up after a stall
        return n;
    }

    /// <summary>Frame step (F10): one frame while paused.</summary>
    public void Step()
    {
        if (Running) RunOneFrame();
    }

    void RunOneFrame()
    {
        Recorder?.Before(Keys);
        _core.RunFrame();
        Recorder?.After(_core.Frame);
        Channel ??= DebugChannel.Find(_core);
        if (Channel == null) return;
        foreach (var line in Channel.ReadNewLog())
        {
            _log.Add(line);
            LogLine?.Invoke(line);
        }
    }

    public IReadOnlyList<string> Log => _log;
    public List<LiveActor> Actors() => Channel == null ? new() : Inspector.Actors(_core, Channel);
    public ProfilerStats? Profiler() => Channel == null ? null : Inspector.Profiler(_core, Channel);

    public static double[] Speeds => [0.25, 0.5, 1, 2, 4];

    /// <summary>Hot reload (D-15): reads the running game's scene, player position and state, loads the new ROM and
    /// writes the hand-over block before its first frame; the Debug runtime continues from there.</summary>
    public bool HotReload(string newRomPath)
    {
        Recorder = null;  // a replay is of one ROM
        byte[]? state = null;
        ushort scene = 0;
        short px = 0, py = 0;
        if (Running && Channel != null && Channel.Game != 0)
        {
            var g = Channel.Game;
            scene = _core.ReadU16(g);
            px = (short)_core.ReadU16(g + 2);
            py = (short)_core.ReadU16(g + 4);
            var size = _core.ReadU16(g + 6);
            var ptr = _core.ReadU32(g + 8);
            if (ptr != 0 && size > 0 && size <= 4080) state = _core.Memory(ptr, size).ToArray();
        }
        var had = Running;
        Stop();
        Start(newRomPath);
        if (!had || state == null) return false;
        // Boot clears EWRAM, so wait until the new game is running (its debug channel exists); it looks for the
        // block during its first second.
        for (var i = 0; i < 300 && Channel == null; i++) RunOneFrame();
        if (Channel == null) return false;
        var block = _core.Memory(0x0203F000, 12 + state.Length);
        BitConverter.TryWriteBytes(block, 0x544F4841u);
        BitConverter.TryWriteBytes(block[4..], scene);
        BitConverter.TryWriteBytes(block[6..], px);
        BitConverter.TryWriteBytes(block[8..], py);
        BitConverter.TryWriteBytes(block[10..], (ushort)state.Length);
        state.CopyTo(block[12..]);
        return true;
    }

    /// <summary>Maps keyboard key names (Project Settings · Input "Keyboard (emulator)") to GBA buttons.</summary>
    public static Dictionary<string, GbaKeys> KeyboardMap(IEnumerable<(string Button, string Keyboard)> input)
    {
        var map = new Dictionary<string, GbaKeys>(StringComparer.OrdinalIgnoreCase);
        foreach (var (button, keyboard) in input)
        {
            if (button == "D-pad")
            {
                if (keyboard.Equals("Arrows", StringComparison.OrdinalIgnoreCase))
                {
                    map["Up"] = GbaKeys.Up;
                    map["Down"] = GbaKeys.Down;
                    map["Left"] = GbaKeys.Left;
                    map["Right"] = GbaKeys.Right;
                }
                continue;
            }
            if (Enum.TryParse<GbaKeys>(button, out var k) && keyboard.Length > 0) map[keyboard] = k;
        }
        return map;
    }

    public void Dispose()
    {
        Stop();
        if (_ownsCore) _core.Dispose();
    }
}
