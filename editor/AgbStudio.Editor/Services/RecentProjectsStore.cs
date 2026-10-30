using System.Globalization;
using System.Text.Json;

namespace AgbStudio.Editor.Services;

public sealed record RecentProject(string Name, string Path, DateTime LastOpened);

/// <summary>Recent projects (P10.19): File › Open Recent, Clear Recent and the Launcher list with times.</summary>
public sealed class RecentProjectsStore
{
    public const int Max = 10;
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    readonly List<RecentProject> _items = [];
    readonly Func<DateTime> _now;

    public string FilePath { get; }
    public IReadOnlyList<RecentProject> Items => _items;
    public event Action? Changed;

    public RecentProjectsStore(string? filePath = null, Func<DateTime>? now = null)
    {
        FilePath = filePath ?? System.IO.Path.Combine(PreferencesStore.DefaultFolder, "recent-projects.json");
        _now = now ?? (() => DateTime.Now);
        if (File.Exists(FilePath))
        {
            try { _items.AddRange(JsonSerializer.Deserialize<List<RecentProject>>(File.ReadAllText(FilePath), Json) ?? []); }
            catch (JsonException) { }
        }
    }

    /// <summary>Moves a project to the top (or adds it) with the current time.</summary>
    public void Touch(string name, string path)
    {
        _items.RemoveAll(p => Same(p.Path, path));
        _items.Insert(0, new RecentProject(name, path, _now()));
        if (_items.Count > Max) _items.RemoveRange(Max, _items.Count - Max);
        Save();
    }

    public bool Remove(string path)
    {
        var n = _items.RemoveAll(p => Same(p.Path, path));
        if (n > 0) Save();
        return n > 0;
    }

    public void Clear()
    {
        _items.Clear();
        Save();
    }

    void Save()
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(_items, Json));
        Changed?.Invoke();
    }

    static bool Same(string a, string b) => string.Equals(System.IO.Path.GetFullPath(a), System.IO.Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

    public string Ago(RecentProject p) => Ago(p.LastOpened, _now());

    /// <summary>Relative time as the Launcher shows it: "2 h ago", "yesterday", "3 days ago", "last week".</summary>
    public static string Ago(DateTime then, DateTime now)
    {
        var d = now - then;
        if (d < TimeSpan.FromMinutes(1)) return "just now";
        if (d < TimeSpan.FromHours(1)) return ((int)d.TotalMinutes).ToString(CultureInfo.InvariantCulture) + " min ago";
        var days = (now.Date - then.Date).Days;
        if (days == 0) return ((int)d.TotalHours).ToString(CultureInfo.InvariantCulture) + " h ago";
        if (days == 1) return "yesterday";
        if (days < 7) return days.ToString(CultureInfo.InvariantCulture) + " days ago";
        if (days < 14) return "last week";
        return (days / 7).ToString(CultureInfo.InvariantCulture) + " weeks ago";
    }
}
