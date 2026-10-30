using AgbStudio.Core.Project;

namespace AgbStudio.Editor.Services;

/// <summary>
/// Project Settings storage (P10.14, T-D08 / T-S01..T-S05): one <see cref="ProjectFile"/> per project, kept in the
/// project's .agbproj (D-03), separate from the per-user <see cref="PreferencesStore"/>.
/// </summary>
public interface IProjectSettingsStore
{
    /// <summary>Where the settings live (the .agbproj path, or "(memory)").</summary>
    string Location { get; }
    ProjectFile Settings { get; }
    event Action? Saved;
    void Save();
}

/// <summary>Settings of an open <see cref="GameProject"/>; Save writes the .agbproj through AgbStudio.Core.</summary>
public sealed class GameProjectSettingsStore(GameProject project) : IProjectSettingsStore
{
    public string Location => project.ProjectPath;
    public ProjectFile Settings => project.Settings;
    public event Action? Saved;

    public void Save()
    {
        project.SaveSettings();
        Saved?.Invoke();
    }
}

/// <summary>Settings of a project that is not on disk (no project open, tests).</summary>
public sealed class MemoryProjectSettingsStore : IProjectSettingsStore
{
    public string Location => "(memory)";
    public ProjectFile Settings { get; } = new();
    public int SaveCount { get; private set; }
    public event Action? Saved;

    public void Save()
    {
        SaveCount++;
        Saved?.Invoke();
    }
}
