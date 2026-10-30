using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgbStudio.Build.Fonts;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;

namespace AgbStudio.Editor.Windows.Font;

/// <summary>A group of glyphs shown together in the Font Manager grid ("Thai consonants").</summary>
public sealed record GlyphGroup(string Name, string Set, IReadOnlyList<int> CodePoints);

/// <summary>
/// One fonts/&lt;name&gt;.font of the project, the <see cref="GameFont"/> FontBuilder makes from it (the same glyphs the
/// ROM gets) and undoable edits of its settings and glyph pixels.
/// </summary>
public sealed class FontEntry
{
    JsonObject _extras = new();
    GameFont? _built;
    readonly UndoService _undo;

    public string Path { get; }
    public FontFile File { get; private set; }
    public GameProject Project { get; }
    public string Name => File.Name.Length > 0 ? File.Name : GameProject.AssetName(Path);
    public string FileName => System.IO.Path.GetFileName(Path);
    public bool IsDirty { get; private set; }
    /// <summary>Rendering · Draw to: the BG of the text layer (stored as "drawTo"; the runtime box default is BG 0).</summary>
    public int DrawTo { get; private set; }
    public event Action? Changed;
    public event Action? DirtyChanged;

    FontEntry(GameProject project, string path, FontFile file, UndoService undo)
    {
        Project = project;
        Path = System.IO.Path.GetFullPath(path);
        File = file;
        _undo = undo;
    }

    public static FontEntry Load(GameProject project, string path, UndoService undo)
    {
        var text = System.IO.File.ReadAllText(path);
        var file = JsonSerializer.Deserialize<FontFile>(text, GameProject.Json) ?? new FontFile();
        var e = new FontEntry(project, path, file, undo);
        if (JsonNode.Parse(text, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) is JsonObject o)
        {
            var known = JsonSerializer.SerializeToNode(file, GameProject.Json)!.AsObject();
            foreach (var (k, v) in o)
                if (!known.ContainsKey(k)) e._extras[k] = v?.DeepClone();
        }
        e.DrawTo = e._extras["drawTo"] is JsonValue d && d.TryGetValue<int>(out var bg) ? Math.Clamp(bg, 0, 3) : 0;
        return e;
    }

    /// <summary>The built font (rebuilt after every change).</summary>
    public GameFont Font => _built ??= Build(File);

    public GameFont Build(FontFile f)
    {
        try { return FontBuilder.Build(f, Project.FontsDir); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or FormatException or IndexOutOfRangeException or ArgumentException)
        {
            // an unreadable source: every glyph is missing (the font still opens so it can be fixed)
            var empty = new GameFont { Name = f.Name, Bpp = f.Bpp, LineHeight = f.LineHeight, Baseline = f.Baseline, Settings = f, Variable = f.Variable, Spacing = f.Spacing, Shadow = f.DropShadow };
            foreach (var cp in FontBuilder.CodePointsOf(f)) if (cp != ' ') empty.Missing.Add(cp);
            return empty;
        }
    }

    public string Serialize()
    {
        var node = JsonSerializer.SerializeToNode(File, GameProject.Json)!.AsObject();
        foreach (var (k, v) in _extras) if (k != "drawTo") node[k] = v?.DeepClone();
        if (DrawTo != 0) node["drawTo"] = DrawTo;
        return node.ToJsonString(GameProject.Json) + "\n";
    }

    public void Save()
    {
        var text = Serialize();
        if (!System.IO.File.Exists(Path) || System.IO.File.ReadAllText(Path) != text) System.IO.File.WriteAllText(Path, text);
        SetDirty(false);
    }

    void SetDirty(bool d)
    {
        if (IsDirty == d) return;
        IsDirty = d;
        DirtyChanged?.Invoke();
    }

    void Touched()
    {
        _built = null;
        SetDirty(true);
        Changed?.Invoke();
    }

    static FontFile Clone(FontFile f) => JsonSerializer.Deserialize<FontFile>(JsonSerializer.Serialize(f, GameProject.Json), GameProject.Json)!;

    /// <summary>An undoable change of the .font settings.</summary>
    public void Edit(string name, Action<FontFile> change)
    {
        var before = Clone(File);
        var after = Clone(File);
        change(after);
        if (JsonSerializer.Serialize(before, GameProject.Json) == JsonSerializer.Serialize(after, GameProject.Json)) return;
        _undo.Do(new DelegateAction(name, () => { File = Clone(before); Touched(); }, () => { File = Clone(after); Touched(); }));
    }

    public void SetDrawTo(int bg)
    {
        var old = DrawTo;
        if (old == bg) return;
        _undo.Do(new DelegateAction("Draw to", () => { DrawTo = old; Touched(); }, () => { DrawTo = bg; Touched(); }));
    }

    public static string Key(int cp) => "U+" + cp.ToString("X4", CultureInfo.InvariantCulture);

    /// <summary>
    /// Writes edited pixels of a glyph as a glyph override: rows from the glyph's top in the source font (line top for a
    /// glyph the source does not have), columns from the pen (x = 0), as FontBuilder applies them.
    /// </summary>
    public void SetGlyphPixels(int cp, int width, byte[] shades)
    {
        var rows = new List<string>();
        for (var y = 0; y < shades.Length / Math.Max(1, width); y++)
            rows.Add(string.Concat(Enumerable.Range(0, width).Select(x => (char)('0' + shades[y * width + x]))));
        // drop empty rows at the bottom; keep the top row so the glyph keeps its place
        while (rows.Count > 1 && rows[^1].All(c => c == '0')) rows.RemoveAt(rows.Count - 1);
        var max = rows.Max(r => r.TrimEnd('0').Length);
        rows = rows.Select(r => r[..Math.Max(1, max)]).ToList();
        Edit("Edit glyph", f =>
        {
            var advance = f.Glyphs.TryGetValue(Key(cp), out var o) ? o.Advance : -1;
            f.Glyphs[Key(cp)] = new GlyphOverride { Advance = advance, Rows = rows };
        });
    }

    // ---- glyph groups ------------------------------------------------------------------------------

    public static readonly IReadOnlyList<int> ThaiConsonants = Enumerable.Range(0x0E01, 0x2E).Where(c => c is not (0x0E24 or 0x0E26)).ToList();
    public static readonly IReadOnlyList<int> ThaiSigns = [0x0E2F, 0x0E3F, 0x0E46, 0x0E4F, .. Enumerable.Range(0x0E50, 10), 0x0E5A, 0x0E5B];

    /// <summary>The grid groups of a font: per character set (Thai split into consonants, vowels and marks, digits and signs), then extra characters.</summary>
    public static IReadOnlyList<GlyphGroup> Groups(FontFile f)
    {
        var list = new List<GlyphGroup>();
        var inSets = new HashSet<int>();
        foreach (var set in f.CharacterSets.Distinct())
        {
            if (!FontBuilder.CharacterSets.TryGetValue(set, out var cps)) continue;
            inSets.UnionWith(cps);
            if (set == "Thai")
            {
                list.Add(new GlyphGroup("Thai consonants", set, ThaiConsonants));
                list.Add(new GlyphGroup("Thai vowels and marks", set, cps.Where(c => !ThaiConsonants.Contains(c) && !ThaiSigns.Contains(c)).ToList()));
                list.Add(new GlyphGroup("Thai digits and signs", set, cps.Where(ThaiSigns.Contains).ToList()));
            }
            else list.Add(new GlyphGroup(set, set, cps.ToList()));
        }
        var extra = ThaiText.CodePoints(f.ExtraCharacters).Concat(f.Glyphs.Keys.Select(KeyToCp)).Where(c => c > 0 && !inSets.Contains(c)).Distinct().OrderBy(c => c).ToList();
        if (extra.Count > 0) list.Add(new GlyphGroup("Extra characters", "", extra));
        return list;
    }

    static int KeyToCp(string key) =>
        key.StartsWith("U+", StringComparison.OrdinalIgnoreCase) && int.TryParse(key[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var cp) ? cp
        : key.Length > 0 ? char.ConvertToUtf32(key, 0) : -1;

    /// <summary>Glyphs in the font (character sets and extras, built or missing).</summary>
    public int GlyphCount => Font.Glyphs.Count + Font.Missing.Count;

    /// <summary>Characters of <paramref name="text"/> the font has no glyph for (Add Characters; spaces and line breaks excluded).</summary>
    public IReadOnlyList<int> MissingIn(string text)
    {
        var font = Font;
        return ThaiText.CodePoints(text).Where(c => c > ' ' && c != 0x3000 && c != 0x00A0 && (font.Get(c) is null || font.Missing.Contains(c))).Distinct().ToList();
    }
}

/// <summary>The fonts of the open project (Font Manager list), each with its Doc Tab when it is edited.</summary>
public sealed class FontLibrary
{
    readonly EditorServices _s;
    readonly Dictionary<string, FontEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public FontEntry? Selected { get; private set; }
    public event Action? ListChanged;

    FontLibrary(EditorServices s)
    {
        _s = s;
        s.Project.Changed += () => { _entries.Clear(); Selected = null; ListChanged?.Invoke(); };
    }

    public static FontLibrary Of(EditorServices s)
    {
        if (s.GetService(typeof(FontLibrary)) is FontLibrary l) return l;
        l = new FontLibrary(s);
        s.Set(l);
        return l;
    }

    /// <summary>Every .font in fonts/, in name order.</summary>
    public IReadOnlyList<FontEntry> Fonts
    {
        get
        {
            var p = _s.Project.Current;
            if (p is null) return [];
            var list = new List<FontEntry>();
            foreach (var file in p.FontFiles())
            {
                var full = System.IO.Path.GetFullPath(file);
                if (!_entries.TryGetValue(full, out var e))
                {
                    try { e = FontEntry.Load(p, full, _s.Undo); }
                    catch (JsonException) { continue; }
                    _entries[full] = e;
                }
                list.Add(e);
            }
            return list;
        }
    }

    /// <summary>The font a window was opened for: an entry, a document or path of a .font, a name, else the selected or default font.</summary>
    public FontEntry? Resolve(object? args)
    {
        var fonts = Fonts;
        if (fonts.Count == 0) return null;
        FontEntry? ByPath(string? path) => path is null ? null : fonts.FirstOrDefault(f => string.Equals(f.Path, System.IO.Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase));
        var hit = args switch
        {
            FontEntry e => e,
            EditorDocument d => ByPath(d.Path),
            string s when s.EndsWith(".font", StringComparison.OrdinalIgnoreCase) => ByPath(System.IO.Path.IsPathRooted(s) ? s : System.IO.Path.Combine(_s.Project.Current!.FontsDir, s)),
            string s => fonts.FirstOrDefault(f => f.Name == s),
            _ => null,
        };
        hit ??= Selected;
        hit ??= ByPath(_s.Documents.Active?.Path);
        hit ??= _s.Documents.Open.Select(d => ByPath(d.Path)).FirstOrDefault(f => f != null);
        var def = _s.Project.Current!.Settings.General.DefaultFont;
        hit ??= fonts.FirstOrDefault(f => f.Name == def) ?? fonts[0];
        return hit;
    }

    public FontEntry Select(FontEntry e)
    {
        Selected = e;
        var docs = _s.Documents;
        var ed = docs.Open.FirstOrDefault(d => d.Path != null && string.Equals(System.IO.Path.GetFullPath(d.Path), e.Path, StringComparison.OrdinalIgnoreCase))
                 ?? docs.Open.FirstOrDefault(d => d.Path is null && d.Name == e.FileName)
                 ?? docs.Add(new EditorDocument { Name = e.FileName, Glyph = "type", Path = e.Path });
        if (ed.SaveHandler is null)
        {
            ed.SaveHandler = _ => { e.Save(); return Task.CompletedTask; };
            e.DirtyChanged += () => ed.IsDirty = e.IsDirty;
        }
        docs.Active = ed;
        return e;
    }

    /// <summary>New Font: writes fonts/&lt;name&gt;.font (copying the source file into fonts/ when it comes from elsewhere).</summary>
    public FontEntry Create(FontFile file, string? sourcePath)
    {
        var p = _s.Project.Current ?? throw new InvalidOperationException("No project is open.");
        Directory.CreateDirectory(p.FontsDir);
        if (sourcePath != null && System.IO.File.Exists(sourcePath))
        {
            var dest = System.IO.Path.Combine(p.FontsDir, System.IO.Path.GetFileName(sourcePath));
            if (!string.Equals(System.IO.Path.GetFullPath(sourcePath), System.IO.Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase)) System.IO.File.Copy(sourcePath, dest, true);
            file.Source = System.IO.Path.GetFileName(sourcePath);
        }
        var names = Fonts.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var name = file.Name.Length > 0 ? file.Name : "font";
        var unique = name;
        for (var n = 2; names.Contains(unique) || System.IO.File.Exists(System.IO.Path.Combine(p.FontsDir, unique + ".font")); n++) unique = $"{name}_{n}";
        file.Name = unique;
        var path = System.IO.Path.Combine(p.FontsDir, unique + ".font");
        GameProject.Save(path, file);
        var entry = FontEntry.Load(p, path, _s.Undo);
        _entries[entry.Path] = entry;
        ListChanged?.Invoke();
        return entry;
    }
}
