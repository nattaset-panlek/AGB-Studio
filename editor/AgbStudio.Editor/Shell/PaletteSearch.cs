using AgbStudio.Editor.Commands;

namespace AgbStudio.Editor.Shell;

/// <summary>A file of the open project, as listed by the Command Palette ("player.png" in "Sprites").</summary>
public sealed record ProjectFile(string Name, string Folder, string Path, string Glyph);

/// <summary>One result row of the Command Palette.</summary>
public sealed record PaletteResult(string Label, string? Glyph, string? Right, EditorCommand? Command, ProjectFile? File);

/// <summary>Lists the files of a project folder for the palette (and Find in Project).</summary>
public sealed class ProjectFileIndex(string? root)
{
    static readonly HashSet<string> Skip = new(StringComparer.OrdinalIgnoreCase) { "build", "bin", "obj", ".git" };

    public IReadOnlyList<ProjectFile> Files()
    {
        if (root is null || !Directory.Exists(root)) return [];
        var list = new List<ProjectFile>();
        var stack = new Stack<string>([root]);
        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            foreach (var d in Directory.EnumerateDirectories(dir).Order(StringComparer.OrdinalIgnoreCase))
                if (!Skip.Contains(Path.GetFileName(d))) stack.Push(d);
            foreach (var f in Directory.EnumerateFiles(dir).Order(StringComparer.OrdinalIgnoreCase))
            {
                var folder = Path.GetFileName(Path.GetDirectoryName(f)) ?? "";
                list.Add(new ProjectFile(Path.GetFileName(f), Display(folder), f, GlyphOf(f, folder)));
            }
        }
        return list;
    }

    /// <summary>Folder label as the palette shows it: project folders are lower case on disk (assets/sprites), "Sprites" on screen.</summary>
    public static string Display(string folder) => folder.Length == 0 ? folder : char.ToUpperInvariant(folder[0]) + folder[1..];

    /// <summary>Icon of a file in lists, matching the Figma asset icons.</summary>
    public static string GlyphOf(string file, string folder)
    {
        if (folder.Contains("tile", StringComparison.OrdinalIgnoreCase)) return "tiles";
        return Path.GetExtension(file).ToLowerInvariant() switch
        {
            ".cs" => "code",
            ".png" or ".bmp" or ".gif" => "image",
            ".wav" or ".mod" or ".xm" or ".s3m" or ".it" => "music",
            ".sfx" => "sound",
            ".font" or ".ttf" => "type",
            ".db" or ".csv" => "table",
            ".map" => "tiles",
            ".scene" => "map",
            ".dlg" => "book",
            ".graph" => "nodes",
            _ => "file",
        };
    }
}

/// <summary>
/// Command Palette matching (P10.12, T-O07): commands and files, a leading "&gt;" for commands only. Prefix matches rank
/// first, then word starts, then contiguous text, then letters in order ("pla" finds Palette Manager). Ties keep menu order for
/// commands and put recently opened files first.
/// </summary>
public static class PaletteSearch
{
    public const int MaxPerGroup = 3;
    public const int MaxCommandsOnly = 8;

    /// <summary>Higher is better; -1 = no match.</summary>
    public static int Score(string text, string query)
    {
        if (query.Length == 0) return 0;
        if (text.StartsWith(query, StringComparison.OrdinalIgnoreCase)) return 1000;
        var i = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (i > 0 && !char.IsLetterOrDigit(text[i - 1])) return 800;
        if (i > 0) return 600 - i;
        // letters in order; first letter must start a word
        int pos = -1, gaps = 0;
        foreach (var ch in query)
        {
            var next = text.IndexOf(ch.ToString(), pos + 1, StringComparison.OrdinalIgnoreCase);
            if (next < 0) return -1;
            if (pos >= 0) gaps += next - pos - 1;
            else if (next > 0 && char.IsLetterOrDigit(text[next - 1])) return -1;
            pos = next;
        }
        return 400 - gaps;
    }

    public static (IReadOnlyList<PaletteResult> Commands, IReadOnlyList<PaletteResult> Files) Search(
        string query, IEnumerable<EditorCommand> commands, IEnumerable<ProjectFile> files, Func<EditorCommand, string?> shortcut, Func<ProjectFile, int>? recency = null)
    {
        query = query.TrimStart();
        var commandsOnly = query.StartsWith('>');
        if (commandsOnly) query = query[1..].Trim();
        var cmds = commands.Where(c => c.InPalette)
            .Select((c, order) => (c, order, s: Score(c.Title, query))).Where(x => x.s >= 0)
            // SearchLast: a better name match must not push out the results a short query is known for
            .OrderBy(x => x.c.SearchLast).ThenByDescending(x => x.s).ThenBy(x => x.order)
            .Take(commandsOnly ? MaxCommandsOnly : MaxPerGroup)
            .Select(x => new PaletteResult(x.c.Title, x.c.Glyph ?? "dots", shortcut(x.c), x.c, null)).ToList();
        if (commandsOnly) return (cmds, []);
        var fs = files.Select((f, order) => (f, order, s: Score(f.Name, query))).Where(x => x.s >= 0)
            .OrderByDescending(x => x.s).ThenBy(x => recency?.Invoke(x.f) ?? int.MaxValue).ThenBy(x => x.order).Take(MaxPerGroup)
            .Select(x => new PaletteResult(x.f.Name, x.f.Glyph, x.f.Folder, null, x.f)).ToList();
        return (cmds, fs);
    }
}
