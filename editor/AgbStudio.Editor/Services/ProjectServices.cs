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

    /// <summary>The tools the last build could not find (it did not start then); empty when it had them all.</summary>
    public IReadOnlyList<(string Tool, string Path)> MissingTools { get; private set; } = [];

    /// <summary>The tools a build runs that are not at their path: the compiler (arm-none-eabi-g++) and maxmod's
    /// sources. Since P24.02-P24.04 the start code, hardware header and ROM header writer are AGB Studio's own.</summary>
    public static IReadOnlyList<(string Tool, string Path)> FindMissingTools(Toolchain tc) =>
        new[]
        {
            ("arm-none-eabi-g++", tc.Gxx),
            ("maxmod", Path.Combine(NativeBuild.MaxmodDir, "source_gba", "mm_main_gba.s")),
        }.Where(t => !File.Exists(t.Item2)).ToList();

    CancellationTokenSource? _cancel;

    /// <summary>
    /// Runs when a build starts, before it looks for the tools (tests hold a build "in progress" with it, so Build ROM
    /// and Play Mode can be checked while they build without starting a tool). Cancel ends the wait with "Build cancelled.".
    /// </summary>
    public Func<CancellationToken, Task>? BeforeBuild { get; set; }

    /// <summary>
    /// G-ERR.04: stops the running build (closing Build ROM while it builds). The build ends with "Build cancelled.";
    /// the ROM of the last good build stays as it was. False when nothing is building.
    /// </summary>
    public bool Cancel()
    {
        if (_cancel is null) return false;
        _cancel.Cancel();
        return true;
    }

    /// <summary>Runs a build; <paramref name="observer"/> receives step / file progress (Build ROM dialog, T-D06).</summary>
    public async Task<BuildResult> BuildAsync(BuildOptions? options = null, IBuildObserver? observer = null)
    {
        var p = project.Current ?? throw new InvalidOperationException("No project is open.");
        if (IsBuilding) throw new InvalidOperationException("A build is already running. Wait for it to finish, then try again.");
        IsBuilding = true;
        _cancel = CancellationTokenSource.CreateLinkedTokenSource(options?.Cancel ?? CancellationToken.None);
        options = new BuildOptions { Configuration = options?.Configuration, Clean = options?.Clean ?? false, Cancel = _cancel.Token };
        Started?.Invoke();
        BuildResult result;
        try { result = await RunAsync(p, options, observer, _cancel.Token); }
        finally
        {
            IsBuilding = false;
            _cancel?.Dispose();
            _cancel = null;
        }
        // the build is over before anyone hears of it: Play and Build ROM read IsBuilding when Finished re-enables them,
        // and a handler may start the next build
        Last = result;
        Finished?.Invoke(result);
        return result;
    }

    async Task<BuildResult> RunAsync(GameProject p, BuildOptions options, IBuildObserver? observer, CancellationToken cancel)
    {
        if (BeforeBuild is { } gate)
        {
            try { await gate(cancel); }
            catch (OperationCanceledException)
            {
                var cancelled = new BuildResult { Success = false };
                cancelled.Diagnostics.Add(new Diagnostic(Severity.Error, "", 0, 0, "", "Build cancelled."));
                return cancelled;
            }
        }
        var tc = new Toolchain(devkitPro());
        // without devkitPro's tools the build cannot start: one error per missing tool names its path (G-ERR.05)
        MissingTools = FindMissingTools(tc);
        if (MissingTools.Count > 0)
        {
            var failed = new BuildResult { Success = false, FailedStep = GameBuilder.StepCompile };
            foreach (var (tool, path) in MissingTools)
                failed.Diagnostics.Add(new Diagnostic(Severity.Error, "", 0, 0, "", $"{tool} is not at {path}. Set the toolchain folder in Preferences › Paths, or reinstall AGB Studio."));
            return failed;
        }
        return await Task.Run(() => new GameBuilder(p, tc, observer) { Scripts = Scripts }.Build(options));
    }

    /// <summary>The last build report of the open project (Build Report page, T-P11), or null.</summary>
    public BuildReport? LoadReport() => project.Current is { } p ? BuildReport.Load(p.BuildDir) : null;
}

/// <summary>
/// The single mGBA libretro core of the process (D-06: one <see cref="LibretroCore"/> per process). Play Mode and the
/// Debugger (Phase 12-13) acquire it here; the shell only owns its lifetime. Hosts share the one core: the first
/// <see cref="Acquire"/> creates it and the last <see cref="Release"/> disposes it, so several editor instances in one
/// process (the tests) never try to create a second core.
/// </summary>
public sealed class EmulatorHost : IDisposable
{
    static readonly object s_lock = new();
    static LibretroCore? s_core;
    static int s_users;

    LibretroCore? _core;
    public bool IsLoaded => _core != null;

    /// <summary>Takes the process core, creating it on first use. Throws if mgba_libretro.dll cannot be loaded.</summary>
    public LibretroCore Acquire(string? corePath = null)
    {
        if (_core != null) return _core;
        lock (s_lock)
        {
            s_core ??= new LibretroCore(corePath);
            s_users++;
            return _core = s_core;
        }
    }

    /// <summary>Gives the core back; the last host that holds it disposes it.</summary>
    public void Release()
    {
        if (_core is null) return;
        lock (s_lock)
        {
            _core = null;
            if (--s_users > 0) return;
            s_core?.Dispose();
            s_core = null;
        }
    }

    public void Dispose() => Release();
}
