namespace AgbStudio.Editor.Windows.Launcher;

/// <summary>One version of the release notes: its number, the highlights the Launcher shows and every line.</summary>
public sealed record ReleaseVersion(string Version, IReadOnlyList<string> Highlights, IReadOnlyList<string> Lines);

/// <summary>
/// The release notes (D-13: bundled offline). The Launcher "What's new" panel shows the highlights of the newest
/// version that is not newer than the running editor. A <c>release-notes.md</c> next to AGBStudio.exe (written by
/// packaging) replaces the bundled text, so a release can update the notes without a code change.
/// </summary>
public static class ReleaseNotes
{
    public const string FileName = "release-notes.md";

    /// <summary>Bundled notes in the same Markdown form as <see cref="FileName"/>: "## version", "### Highlights", "- item".</summary>
    public const string Bundled = """
        # AGB Studio release notes

        ## 0.1

        ### Highlights
        - Thai variable-width fonts
        - Logic Graph ⇄ C# sync
        - Flash to EZ-Flash / EverDrive

        ### Editor
        - Launcher with recent projects, genre templates and tutorials
        - Scene Editor with Hierarchy, Inspector, Assets and Console
        - Command Palette (Ctrl+K) and rebindable shortcuts
        - Dark and light themes

        ### Toolchain
        - C# scripting for the GBA, translated to C++ (GAE001-GAE006 analyzer rules)
        - Logic Graph files generate C#, and generated C# reads back into the graph
        - Thai text with dictionary word wrap and variable-width fonts
        - Build Report with ROM, VRAM and RAM budgets

        ### Hardware
        - Copy the ROM to EZ-Flash and EverDrive SD cards
        - The builds are not code-signed
        """;

    /// <summary>Notes text: the file next to the executable if there is one, else the bundled notes.</summary>
    public static string Text(string? folder = null)
    {
        var path = Path.Combine(folder ?? AppContext.BaseDirectory, FileName);
        try { if (File.Exists(path)) return File.ReadAllText(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return Bundled;
    }

    /// <summary>Parses "## version" sections; bullets under "### Highlights" are the highlights (else the first three bullets).</summary>
    public static IReadOnlyList<ReleaseVersion> Parse(string markdown)
    {
        var list = new List<ReleaseVersion>();
        string? version = null;
        List<string> highlights = [], lines = [];
        var inHighlights = false;
        void Flush()
        {
            if (version != null) list.Add(new ReleaseVersion(version, highlights.Count > 0 ? highlights : lines.Take(3).ToList(), lines));
            highlights = [];
            lines = [];
        }
        foreach (var raw in markdown.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("## ", StringComparison.Ordinal)) { Flush(); version = line[3..].Trim(); inHighlights = false; }
            else if (line.StartsWith("### ", StringComparison.Ordinal)) inHighlights = line[4..].Trim().Equals("Highlights", StringComparison.OrdinalIgnoreCase);
            else if (version != null && (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal)))
            {
                var item = line[2..].Trim();
                if (inHighlights) highlights.Add(item);
                else lines.Add(item);
            }
        }
        Flush();
        return list;
    }

    /// <summary>The version the Launcher shows: the newest one not newer than <paramref name="running"/>.</summary>
    public static ReleaseVersion? Current(IReadOnlyList<ReleaseVersion> versions, Version running)
    {
        static Version? V(string s) => Version.TryParse(s.Count(c => c == '.') == 0 ? s + ".0" : s, out var v) ? v : null;
        return versions.Where(v => V(v.Version) is { } x && x <= running).OrderByDescending(v => V(v.Version)).FirstOrDefault() ?? versions.FirstOrDefault();
    }

    /// <summary>The highlights of the running editor's version, as the What's new panel lists them.</summary>
    public static IReadOnlyList<string> WhatsNew(string? folder = null, Version? running = null) =>
        Current(Parse(Text(folder)), running ?? EditorVersion.Value)?.Highlights ?? [];
}

/// <summary>The editor version (Directory.Build.props), shown as "v0.1".</summary>
public static class EditorVersion
{
    public static Version Value => typeof(EditorVersion).Assembly.GetName().Version ?? new Version(0, 1);
    public static string Short => $"v{Value.Major}.{Value.Minor}";
}
