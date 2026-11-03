using AgbStudio.Build;
using AgbStudio.Core;
using AgbStudio.Core.Project;
using AgbStudio.Emu;

namespace AgbStudio.Mcp;

public sealed class McpContext : IDisposable
{
    #region [Variables/Fields]
    readonly Toolchain _toolchain = new();
    GameProject? _project;
    LibretroCore? _core;
    PlaySession? _session;
    #endregion

    #region [Properties]
    public GameProject? CurrentProject => _project;
    public Toolchain Toolchain => _toolchain;
    public LibretroCore? Core => _core;
    public PlaySession? Session => _session;
    public bool IsSimulating => _session != null && _session.Running;
    #endregion

    #region [Public Methods]
    public GameProject OpenProject(string path)
    {
        _project = GameProject.Open(path);
        return _project;
    }

    public GameProject CreateProject(string folder, string name)
    {
        _project = GameProject.Create(folder, name);
        return _project;
    }

    public void CloseProject()
    {
        StopSimulation();
        _project = null;
    }

    public PlaySession StartSimulation(string? romPath = null)
    {
        StopSimulation();
        var targetRom = romPath;
        if (string.IsNullOrEmpty(targetRom) && _project != null)
        {
            var gb = new AgbStudio.Build.Pipeline.GameBuilder(_project, _toolchain);
            targetRom = gb.RomPath;
        }

        if (string.IsNullOrEmpty(targetRom) || !File.Exists(targetRom))
            throw new FileNotFoundException($"ROM not found: {targetRom}");

        _core = new LibretroCore();
        _session = new PlaySession(_core);
        _session.Start(targetRom);
        return _session;
    }

    public int StepSimulation(int frames = 1)
    {
        if (_session == null || !_session.Running)
            throw new InvalidOperationException("Simulation is not running.");

        var count = Math.Max(1, Math.Min(600, frames));
        for (var i = 0; i < count; i++)
            _session.Step();

        return count;
    }

    public string CaptureScreenBase64()
    {
        if (_core == null)
            throw new InvalidOperationException("Emulator core is not initialized.");

        var pngBytes = Png.EncodeArgb(_core.Frame, LibretroCore.Width, LibretroCore.Height);
        return Convert.ToBase64String(pngBytes);
    }

    public void SendInput(GbaKeys keys)
    {
        if (_session == null)
            throw new InvalidOperationException("Simulation is not running.");

        _session.Keys = keys;
    }

    public void StopSimulation()
    {
        if (_session != null)
        {
            _session.Stop();
            _session.Dispose();
            _session = null;
        }

        if (_core != null)
        {
            _core.Dispose();
            _core = null;
        }
    }

    public void Dispose()
    {
        StopSimulation();
    }
    #endregion
}
