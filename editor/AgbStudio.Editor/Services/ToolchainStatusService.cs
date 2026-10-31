using AgbStudio.Build;

namespace AgbStudio.Editor.Services;

/// <summary>
/// Toolchain detection (P10.16) through AgbStudio.Build's <see cref="Toolchain"/>. The result feeds the status bar
/// dot now and the Preferences · Paths / Emulator and Launcher status lines in Phase 11.
/// </summary>
public sealed class ToolchainStatusService(Func<string?> devkitPro)
{
    public IReadOnlyList<ToolStatus> Tools { get; private set; } = [];
    public bool IsComplete => Tools.Count > 0 && Tools.All(t => t.Found);
    public event Action? Changed;

    public ToolchainStatusService() : this(() => null) { }

    /// <summary>Runs a tool for its version (gcc -dumpversion); null starts the real program. Tests pass a stub.</summary>
    public Func<string, string, ProcResult>? RunTool { get; set; }

    public IReadOnlyList<ToolStatus> CheckNow()
    {
        var dkp = devkitPro();
        Tools = (RunTool is { } run ? new Toolchain(dkp) { RunTool = run } : new Toolchain(dkp)).Check();
        Changed?.Invoke();
        return Tools;
    }

    /// <summary>Runs the check off the UI thread (gcc -dumpversion can take a moment).</summary>
    public Task<IReadOnlyList<ToolStatus>> CheckAsync() => Task.Run(CheckNow);

    public ToolStatus? Find(string name) => Tools.FirstOrDefault(t => t.Name == name);
}
