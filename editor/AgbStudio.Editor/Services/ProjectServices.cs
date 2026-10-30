using AgbStudio.Build;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;
using AgbStudio.Emu;

namespace AgbStudio.Editor.Services;

/// <summary>The open project (AgbStudio.Core <see cref="GameProject"/>) and its settings store.</summary>
public sealed class ProjectService(RecentProjectsStore recent)
{
    public GameProject? Current { get; private set; }
    public IProjectSettingsStore Settings { get; private set; } = new MemoryProjectSettingsStore();
    public string? Name => Current?.Settings.Name;
    public event Action? Changed;

    /// <summary>Opens a project folder or .agbproj, records it in recent projects and makes it current.</summary>
    public GameProject Open(string path)
    {
        var p = GameProject.Open(path);
        Set(p);
        recent.Touch(p.Settings.Name, p.ProjectPath);
        return p;
    }

    public GameProject Create(string folder, string name)
    {
        var p = GameProject.Create(folder, name);
        Set(p);
        recent.Touch(p.Settings.Name, p.ProjectPath);
        return p;
    }

    public void Close() => Set(null);

    void Set(GameProject? p)
    {
        Current = p;
        Settings = p is null ? new MemoryProjectSettingsStore() : new GameProjectSettingsStore(p);
        Changed?.Invoke();
    }
}

/// <summary>Builds the open project with AgbStudio.Build (<see cref="GameBuilder"/>) off the UI thread.</summary>
public sealed class BuildService(ProjectService project, Func<string?> devkitPro)
{
    public bool IsBuilding { get; private set; }
    public BuildResult? Last { get; private set; }
    public event Action? Started;
    public event Action<BuildResult>? Finished;

    /// <summary>The C# scripting compiler; its last result feeds the Script Editor, Inspector and Debugger.</summary>
    public AgbStudio.Scripting.ScriptBackend Scripts { get; } = new();

    /// <summary>Runs a build; <paramref name="observer"/> receives step / file progress (Build ROM dialog, T-D06).</summary>
    public async Task<BuildResult> BuildAsync(BuildOptions? options = null, IBuildObserver? observer = null)
    {
        var p = project.Current ?? throw new InvalidOperationException("No project is open.");
        if (IsBuilding) throw new InvalidOperationException("A build is already running.");
        IsBuilding = true;
        Started?.Invoke();
        try
        {
            var result = await Task.Run(() => new GameBuilder(p, new Toolchain(devkitPro()), observer) { Scripts = Scripts }.Build(options));
            Last = result;
            Finished?.Invoke(result);
            return result;
        }
        finally { IsBuilding = false; }
    }

    /// <summary>The last build report of the open project (Build Report page, T-P11), or null.</summary>
    public BuildReport? LoadReport() => project.Current is { } p ? BuildReport.Load(p.BuildDir) : null;
}

/// <summary>
/// The single mGBA libretro core of the process (D-06: one <see cref="LibretroCore"/> per process). Play Mode and the
/// Debugger (Phase 12-13) acquire it here; the shell only owns its lifetime.
/// </summary>
public sealed class EmulatorHost : IDisposable
{
    LibretroCore? _core;
    public bool IsLoaded => _core != null;

    /// <summary>Creates the core on first use. Throws if mgba_libretro.dll cannot be loaded.</summary>
    public LibretroCore Acquire(string? corePath = null) => _core ??= new LibretroCore(corePath);

    public void Release()
    {
        _core?.Dispose();
        _core = null;
    }

    public void Dispose() => Release();
}
