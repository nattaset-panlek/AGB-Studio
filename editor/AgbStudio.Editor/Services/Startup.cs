using AgbStudio.Editor.Shell;

namespace AgbStudio.Editor.Services;

/// <summary>
/// P10.15 / T-S07 "Open last project on start": opens the last project when the preference is on and the project is
/// still there, and says which page starts: the Scene Editor with a project, else the Launcher (T-P12).
/// </summary>
public static class Startup
{
    public static string Choose(EditorServices s)
    {
        var prefs = s.Preferences.Current;
        if (prefs.OpenLastProjectOnStart && prefs.LastProject is { } last && File.Exists(last))
        {
            try { s.Project.Open(last); }
            catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException) { }
        }
        return s.Project.Current is null && s.Windows.IsRegistered("T-P12") ? "T-P12" : "T-P01";
    }
}
