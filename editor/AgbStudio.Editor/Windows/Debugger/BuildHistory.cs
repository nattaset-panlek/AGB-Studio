using System.Text.Json;
using AgbStudio.Build.Pipeline;
using AgbStudio.Editor.Shell;

namespace AgbStudio.Editor.Windows.Debugger;

/// <summary>
/// Build Report history across builds (P13.08, T-P11 "History"): build/report-history.json, newest first, last 20.
/// Successful builds are added by the build itself (BuildReport.Save); this service adds failed builds with their first
/// error, so the history shows every build the editor ran. The Build Report page reads <see cref="Entries"/>.
/// </summary>
public sealed class BuildHistory
{
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    readonly EditorServices _s;

    BuildHistory(EditorServices s)
    {
        _s = s;
        s.Build.Finished += OnFinished;
    }

    public static BuildHistory Of(EditorServices s)
    {
        if (s.GetService(typeof(BuildHistory)) is BuildHistory h) return h;
        h = new BuildHistory(s);
        s.Set(h);
        return h;
    }

    string? FilePath => _s.Project.Current is { } p ? Path.Combine(p.BuildDir, "report-history.json") : null;

    public IReadOnlyList<BuildReport.HistoryEntry> Entries()
    {
        var path = FilePath;
        if (path is null || !File.Exists(path)) return [];
        try { return JsonSerializer.Deserialize<List<BuildReport.HistoryEntry>>(File.ReadAllText(path), Json) ?? []; }
        catch (JsonException) { return []; }
    }

    void OnFinished(BuildResult r)
    {
        if (r.Success || FilePath is not { } path) return;
        var list = Entries().ToList();
        var first = r.Errors.FirstOrDefault()?.ToString() ?? r.FailedStep;
        list.Insert(0, new BuildReport.HistoryEntry(DateTime.Now, 0, Math.Round(r.Elapsed.TotalSeconds, 2),
            r.Diagnostics.Count(d => d.Severity == Severity.Warning), r.Errors.Count(), first));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(list.Take(20).ToList(), Json));
    }
}
