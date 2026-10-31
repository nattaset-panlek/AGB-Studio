using System.Text.Json;
using AgbStudio.Build.Pipeline;
using AgbStudio.Editor.Shell;

namespace AgbStudio.Editor.Windows.Debugger;

/// <summary>
/// Build Report history across builds (P13.08, T-P11 "History"): build/report-history.json, newest first, last 20.
/// GameBuilder adds every build to it, failed ones with their first error (BuildReport.AddHistory), so the history shows
/// every build the editor ran. The Build Report page reads <see cref="Entries"/>.
/// </summary>
public sealed class BuildHistory
{
    readonly EditorServices _s;

    BuildHistory(EditorServices s) => _s = s;

    public static BuildHistory Of(EditorServices s)
    {
        if (s.GetService(typeof(BuildHistory)) is BuildHistory h) return h;
        h = new BuildHistory(s);
        s.Set(h);
        return h;
    }

    public IReadOnlyList<BuildReport.HistoryEntry> Entries()
    {
        if (_s.Project.Current is not { } p) return [];
        try { return BuildReport.LoadHistory(p.BuildDir); }
        catch (Exception e) when (e is IOException or JsonException) { return []; }
    }
}
