using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using AgbStudio.Editor.Shell;

namespace AgbStudio.Editor.Windows.Menus;

/// <summary>
/// editor.json next to AGBStudio.exe (D-13): the online places Help and About open. Empty values mean "not configured"
/// (Report a Bug… and Website are then disabled; Check for Updates says "No update source configured").
/// </summary>
public sealed class EditorConfig
{
    public string BugUrl { get; set; } = "";
    public string UpdateFeed { get; set; } = "";
    public string WebsiteUrl { get; set; } = "";
    public string CommunityUrl { get; set; } = "";
    public string ReleaseNotesUrl { get; set; } = "";
    /// <summary>The devkitPro installer page ("devkitPro not found", P16.01): the devkitPro project's own page by default.</summary>
    public string DevkitProUrl { get; set; } = "https://devkitpro.org/wiki/Getting_Started";

    static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, "editor.json");

    public static EditorConfig Load(string? path = null)
    {
        path ??= DefaultPath;
        try { return File.Exists(path) ? JsonSerializer.Deserialize<EditorConfig>(File.ReadAllText(path), Json) ?? new() : new(); }
        catch (JsonException) { return new(); }
        catch (IOException) { return new(); }
    }
}

public sealed record UpdateResult(bool Ok, bool Available, string Message, string? Url);

/// <summary>
/// Opening URLs and files in the user's browser / default app, and reading the update feed. Both are injectable so tests
/// make no network calls and start no programs (EditorServices.Set(new EditorLinks { ... })).
/// </summary>
public sealed class EditorLinks
{
    public EditorConfig Config { get; init; } = EditorConfig.Load();
    /// <summary>Opens a URL or a file with the shell (browser, default app).</summary>
    public Action<string> Open { get; init; } = target => Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
    /// <summary>Downloads the update feed text.</summary>
    public Func<string, CancellationToken, Task<string>> Fetch { get; init; } = FetchHttp;

    static async Task<string> FetchHttp(string url, CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("AGBStudio/" + About.Version);
        return await http.GetStringAsync(new Uri(url), ct);
    }

    public static EditorLinks Of(EditorServices s)
    {
        if (s.GetService(typeof(EditorLinks)) is EditorLinks l) return l;
        s.Set(l = new EditorLinks());
        return l;
    }

    /// <summary>
    /// Help › Check for Updates: reads the feed (JSON {"version", "url"}, or a GitHub release {"tag_name", "html_url"}, or a
    /// list of them) and compares it with this version.
    /// </summary>
    public async Task<UpdateResult> CheckForUpdatesAsync(string current, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(Config.UpdateFeed)) return new(false, false, "No update source configured", null);
        string text;
        try { text = await Fetch(Config.UpdateFeed.Trim(), ct); }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException or UriFormatException or InvalidOperationException)
        {
            return new(false, false, "Could not check for updates: " + e.Message, null);
        }
        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement.EnumerateArray().FirstOrDefault() : doc.RootElement;
            string? Str(params string[] names) => names.Select(n => root.ValueKind == JsonValueKind.Object && root.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null).FirstOrDefault(v => v != null);
            var latest = Str("version", "tag_name")?.TrimStart('v', 'V');
            var url = Str("url", "html_url");
            if (latest is null || !Version.TryParse(Normalize(latest), out var lv) || !Version.TryParse(Normalize(current), out var cv))
                return new(false, false, "Could not check for updates: the feed has no version", null);
            return lv > cv
                ? new(true, true, $"AGB Studio {latest} is available", url)
                : new(true, false, $"AGB Studio is up to date ({current})", null);
        }
        catch (JsonException)
        {
            return new(false, false, "Could not check for updates: the feed is not JSON", null);
        }
    }

    static string Normalize(string v)
    {
        var core = new string(v.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray());
        return core.Count(c => c == '.') == 0 ? core + ".0" : core;
    }
}

/// <summary>Version and build date shown in About (T-D11) and sent with update checks.</summary>
public static class About
{
    public static string Version
    {
        get
        {
            var v = typeof(About).Assembly.GetName().Version ?? new Version(0, 1, 0);
            return string.Create(CultureInfo.InvariantCulture, $"{v.Major}.{v.Minor}.{Math.Max(0, v.Build)}");
        }
    }

    /// <summary>The day this editor was built (the assembly's file date).</summary>
    public static DateTime BuildDate
    {
        get
        {
            var loc = typeof(About).Assembly.Location;
            return loc.Length > 0 && File.Exists(loc) ? File.GetLastWriteTime(loc).Date : DateTime.Today;
        }
    }

    /// <summary>The third-party notices (docs/THIRD_PARTY.md): next to AGBStudio.exe in a release, in docs/ of a checkout.</summary>
    public static string? LicensesFile()
    {
        var beside = Path.Combine(AppContext.BaseDirectory, "THIRD_PARTY.md");
        if (File.Exists(beside)) return beside;
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var f = Path.Combine(dir.FullName, "docs", "THIRD_PARTY.md");
            if (File.Exists(f)) return f;
        }
        return null;
    }

    /// <summary>A user guide (docs/user, e.g. the GBA hardware guide): in docs/user next to AGBStudio.exe, in docs/user of a checkout.</summary>
    public static string? UserGuide(string file)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var f = Path.Combine(dir.FullName, "docs", "user", file);
            if (File.Exists(f)) return f;
        }
        return null;
    }

    /// <summary>The components listed in the notices table (first column), for checks.</summary>
    public static IReadOnlyList<string> LicensedComponents(string file) =>
        File.ReadAllLines(file).Where(l => l.StartsWith('|') && !l.StartsWith("|---", StringComparison.Ordinal))
            .Select(l => l.Split('|', StringSplitOptions.TrimEntries)[1]).Skip(1).ToList();
}
