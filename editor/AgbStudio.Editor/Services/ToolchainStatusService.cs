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

    public IReadOnlyList<ToolStatus> CheckNow()
    {
        Tools = new Toolchain(devkitPro()).Check();
        Changed?.Invoke();
        return Tools;
    }

    /// <summary>Runs the check off the UI thread (gcc -dumpversion can take a moment).</summary>
    public Task<IReadOnlyList<ToolStatus>> CheckAsync() => Task.Run(CheckNow);

    public ToolStatus? Find(string name) => Tools.FirstOrDefault(t => t.Name == name);
}
