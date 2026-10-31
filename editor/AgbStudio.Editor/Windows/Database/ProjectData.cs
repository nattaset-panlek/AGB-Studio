using System.Text.Json.Nodes;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using AgbStudio.Scripting.Dialogue;

namespace AgbStudio.Editor.Windows.Database;

/// <summary>A database table of the open project (<c>data/&lt;Name&gt;.db</c>, T-P07).</summary>
public sealed class TableDoc(string path, TableFile table)
{
    public string Path { get; set; } = path;
    public TableFile Table { get; } = table;
    public string Name => Table.Name;
    public string FileName => System.IO.Path.GetFileName(Path);
    public void Save() => GameProject.Save(Path, Table);
}

/// <summary>
/// A dialogue file of the open project (<c>dialogue/&lt;name&gt;.dlg</c>, T-P16), read with the scripting model
/// (<see cref="DialogueFile"/>). Speaker properties the model does not know (the name per language and the name tag
/// colour of New Speaker, T-D21) are kept as JSON next to it and written back on save.
/// </summary>
public sealed class DialogueDoc
{
    static readonly HashSet<string> KnownSpeakerProps = new(StringComparer.OrdinalIgnoreCase) { "name", "portrait", "blip", "textSpeed" };

    public string Path { get; set; }
    public DialogueFile File { get; private set; }
    /// <summary>Extra speaker properties by speaker name ("names": {"en", "th"}, "color").</summary>
    public Dictionary<string, JsonObject> SpeakerExtras { get; } = new(StringComparer.Ordinal);

    public DialogueDoc(string path, DialogueFile file)
    {
        Path = path;
        File = file;
    }

    public string Name => System.IO.Path.GetFileNameWithoutExtension(Path);
    public string FileName => System.IO.Path.GetFileName(Path);

    public static DialogueDoc Load(string path)
    {
        var doc = new DialogueDoc(path, new DialogueFile());
        doc.Restore(System.IO.File.ReadAllText(path));
        return doc;
    }

    /// <summary>Replaces the content with a JSON text of the file (undo / redo of dialogue edits).</summary>
    public void Restore(string text)
    {
        File = DialogueFile.Parse(text);
        SpeakerExtras.Clear();
        if (JsonNode.Parse(text, documentOptions: new System.Text.Json.JsonDocumentOptions { CommentHandling = System.Text.Json.JsonCommentHandling.Skip, AllowTrailingCommas = true })?["speakers"] is JsonArray speakers)
            foreach (var s in speakers.OfType<JsonObject>())
            {
                var name = s["name"]?.GetValue<string>() ?? "";
                var extra = new JsonObject();
                foreach (var (k, v) in s.ToList())
                    if (!KnownSpeakerProps.Contains(k)) extra[k] = v?.DeepClone();
                if (extra.Count > 0) SpeakerExtras[name] = extra;
            }
    }

    /// <summary>The file as written to disk: the scripting model's JSON plus the extra speaker properties.</summary>
    public string ToJson()
    {
        var node = JsonNode.Parse(File.ToJson())!.AsObject();
        node.Remove("lineOf");
        if (node["speakers"] is JsonArray speakers)
            foreach (var s in speakers.OfType<JsonObject>())
            {
                // "names" and "color" (DialogueSpeaker, docs/schemas/dlg.schema.json) are edited in the extras: they win
                s.Remove("names");
                s.Remove("color");
                if (SpeakerExtras.TryGetValue(s["name"]?.GetValue<string>() ?? "", out var extra))
                    foreach (var (k, v) in extra) s[k] = v?.DeepClone();
            }
        return node.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n";
    }

    public void Save()
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        System.IO.File.WriteAllText(Path, ToJson());
    }

    /// <summary>Speaker name in a language (New Speaker "Name · EN" / "Name · TH"); the speaker id otherwise.</summary>
    public string SpeakerName(string speaker, string lang) =>
        SpeakerExtras.TryGetValue(speaker, out var x) && x["names"] is JsonObject names && names[lang]?.GetValue<string>() is { Length: > 0 } n ? n : speaker;

    public string? SpeakerColor(string speaker) =>
        SpeakerExtras.TryGetValue(speaker, out var x) ? x["color"]?.GetValue<string>() : null;
}

/// <summary>
/// The data files of the open project that the Database (T-P07), Localization (T-P14) and Dialogue Editor (T-P16)
/// edit: tables, string tables per language with their meta, and dialogue files. One instance per open project,
/// shared through <see cref="EditorServices"/>; every edit goes through <see cref="Edit"/> so it can be undone,
/// marks the owning document dirty and refreshes the windows that show it.
/// </summary>
public sealed class ProjectData
{
    readonly EditorServices _s;
    public GameProject Project { get; }
    public List<TableDoc> Tables { get; } = [];
    public Dictionary<string, StringTable> Strings { get; } = new(StringComparer.Ordinal);
    public StringMeta Meta { get; private set; } = new();
    public List<DialogueDoc> Dialogues { get; } = [];
    public bool StringsDirty { get; set; }
    /// <summary>Raised after any edit, undo or redo (windows refresh what they show).</summary>
    public event Action? Changed;

    ProjectData(EditorServices s, GameProject p)
    {
        _s = s;
        Project = p;
        Reload();
    }

    /// <summary>The data of the open project (created on first use, replaced when another project opens); null when none is open.</summary>
    public static ProjectData? For(EditorServices s)
    {
        if (s.Project.Current is not { } p) return null;
        if (s.GetService(typeof(ProjectData)) is ProjectData d && d.Project == p) return d;
        d = new ProjectData(s, p);
        s.Set(d);
        return d;
    }

    public EditorServices Services => _s;
    public LocalizationSettings Loc => Project.Settings.Localization;
    /// <summary>The project's languages, each code once (in settings order).</summary>
    public IReadOnlyList<LanguageInfo> Languages => Loc.Languages.DistinctBy(l => l.Code).ToList();
    public string Source => Loc.Source;

    public void Reload()
    {
        Tables.Clear();
        foreach (var f in CreationOrder(Project.TableFiles()))
        {
            try { Tables.Add(new TableDoc(f, GameProject.Load<TableFile>(f))); }
            catch (Exception e) when (e is IOException or System.Text.Json.JsonException or InvalidDataException) { }
        }
        Strings.Clear();
        foreach (var f in Project.StringTables())
        {
            try
            {
                var t = GameProject.Load<StringTable>(f);
                var lang = LanguageOfFile(f);
                t.Language = lang;
                Strings[lang] = t;
            }
            catch (Exception e) when (e is IOException or System.Text.Json.JsonException or InvalidDataException) { }
        }
        var meta = MetaPath;
        Meta = File.Exists(meta) ? GameProject.Load<StringMeta>(meta) : new StringMeta();
        Dialogues.Clear();
        foreach (var f in CreationOrder(Project.DialogueFiles()))
        {
            try { Dialogues.Add(DialogueDoc.Load(f)); }
            catch (Exception e) when (e is IOException or FormatException or System.Text.Json.JsonException) { }
        }
        StringsDirty = false;
    }

    /// <summary>Files in the order they were created (tables and conversations are listed as the user added them).</summary>
    static IEnumerable<string> CreationOrder(IEnumerable<string> files) => AgbStudio.Editor.Services.CreationOrder.Of(files, StringComparer.Ordinal);

    static string LanguageOfFile(string path)
    {
        var name = Path.GetFileName(path);              // strings.th.json
        return name.Length > "strings..json".Length ? name["strings.".Length..^".json".Length] : "";
    }

    public string MetaPath => Path.Combine(Project.TextDir, "strings.meta.json");
    public string StringsPath(string lang) => Path.Combine(Project.TextDir, $"strings.{lang}.json");

    // ---- strings ---------------------------------------------------------------------------------------

    /// <summary>Every string key of the project (all languages), sorted as the build numbers them.</summary>
    public IReadOnlyList<string> Keys() =>
        Strings.Values.SelectMany(t => t.Strings.Keys).Distinct().OrderBy(k => k, StringComparer.Ordinal).ToList();

    public string? Text(string lang, string key) =>
        Strings.TryGetValue(lang, out var t) && t.Strings.TryGetValue(key, out var v) ? v : null;

    /// <summary>Sets (or with null removes) the text of a key in one language; creates the language table if needed.</summary>
    public void SetText(string lang, string key, string? text)
    {
        if (!Strings.TryGetValue(lang, out var t)) Strings[lang] = t = new StringTable { Language = lang };
        if (text is null) t.Strings.Remove(key);
        else t.Strings[key] = text;
        StringsDirty = true;
    }

    public StringInfo Info(string key) => Meta.Keys.TryGetValue(key, out var i) ? i : new StringInfo();

    public StringInfo EnsureInfo(string key)
    {
        if (!Meta.Keys.TryGetValue(key, out var i)) Meta.Keys[key] = i = new StringInfo();
        StringsDirty = true;
        return i;
    }

    /// <summary>Number of keys with a text in a language (Localization "326 / 340").</summary>
    public int Translated(string lang, IReadOnlyList<string>? keys = null)
    {
        keys ??= Keys();
        return Strings.TryGetValue(lang, out var t) ? keys.Count(k => t.Strings.ContainsKey(k)) : 0;
    }

    public void SaveStrings()
    {
        foreach (var (lang, t) in Strings) GameProject.Save(StringsPath(lang), t);
        GameProject.Save(MetaPath, Meta);
        StringsDirty = false;
    }

    /// <summary>Language name as Localization lists it: "English (source)", "ไทย Thai", "日本語 Japanese".</summary>
    public string LanguageLabel(string code)
    {
        var info = Loc.Languages.FirstOrDefault(l => l.Code == code);
        var known = LanguageNames.Find(code);
        if (code == Source) return (known?.English ?? info?.Name ?? code) + " (source)";
        var native = info?.Name is { Length: > 0 } n ? n : known?.Native ?? code;
        var english = known?.English;
        return english is null || english == native ? native : native + " " + english;
    }

    /// <summary>Language name as the table header and status bar show it: the name in the project settings ("ไทย").</summary>
    public string LanguageName(string code) =>
        Loc.Languages.FirstOrDefault(l => l.Code == code)?.Name is { Length: > 0 } n ? n : LanguageNames.Find(code)?.Native ?? code;

    // ---- tables ----------------------------------------------------------------------------------------

    public TableDoc? Table(string name) => Tables.FirstOrDefault(t => t.Name == name);

    /// <summary>Text of a localized cell: the key's text in the language, or the key itself when it has none.</summary>
    public string LocalizedText(string key, string? lang = null) => key.Length == 0 ? "" : Text(lang ?? Source, key) ?? key;

    // ---- dialogue --------------------------------------------------------------------------------------

    public DialogueDoc? Dialogue(string name) => Dialogues.FirstOrDefault(d => d.Name == name);

    // ---- edits -----------------------------------------------------------------------------------------

    /// <summary>Applies an undoable edit: runs <paramref name="redo"/> now, records the step, marks the document dirty and raises <see cref="Changed"/>.</summary>
    public void Edit(string name, EditorDocument? doc, Action redo, Action undo)
    {
        _s.Undo.Do(new DelegateAction(name,
            () => { undo(); if (doc != null) doc.IsDirty = true; Changed?.Invoke(); },
            () => { redo(); if (doc != null) doc.IsDirty = true; Changed?.Invoke(); }));
    }

    /// <summary>Records an edit that is already applied (live typing); undo restores the old state.</summary>
    public void Applied(string name, EditorDocument? doc, Action redo, Action undo)
    {
        if (doc != null) doc.IsDirty = true;
        _s.Undo.Push(new DelegateAction(name,
            () => { undo(); if (doc != null) doc.IsDirty = true; Changed?.Invoke(); },
            () => { redo(); if (doc != null) doc.IsDirty = true; Changed?.Invoke(); }));
        Changed?.Invoke();
    }

    public void NotifyChanged() => Changed?.Invoke();

    /// <summary>Opens (or returns) the editor document of a file so Save, Save All and the Unsaved Changes dialog see it.</summary>
    public EditorDocument Document(string name, string glyph, string? path, Func<Task> save)
    {
        var docs = _s.Documents;
        var existing = docs.Open.FirstOrDefault(d => path != null ? string.Equals(d.Path, path, StringComparison.OrdinalIgnoreCase) : d.Path == null && d.Name == name);
        if (existing != null) return existing;
        var doc = new EditorDocument { Name = name, Glyph = glyph, Path = path, SaveHandler = _ => save() };
        docs.Add(doc);
        return doc;
    }
}

/// <summary>Languages AGB Studio knows by code: the name in the language itself, in English, and the character set its text needs.</summary>
public sealed record LanguageName(string Code, string Native, string English, string Script)
{
    /// <summary>"日本語 Japanese", "English".</summary>
    public string Label => Native == English ? English : Native + " " + English;
}

public static class LanguageNames
{
    public static readonly IReadOnlyList<LanguageName> All =
    [
        new("en", "English", "English", "Basic Latin"),
        new("th", "ไทย", "Thai", "Thai"),
        new("ja", "日本語", "Japanese", "Japanese kana"),
        new("fr", "Français", "French", "Basic Latin"),
        new("de", "Deutsch", "German", "Basic Latin"),
        new("es", "Español", "Spanish", "Basic Latin"),
        new("it", "Italiano", "Italian", "Basic Latin"),
        new("pt", "Português", "Portuguese", "Basic Latin"),
        new("nl", "Nederlands", "Dutch", "Basic Latin"),
        new("id", "Bahasa Indonesia", "Indonesian", "Basic Latin"),
        new("ms", "Bahasa Melayu", "Malay", "Basic Latin"),
        new("vi", "Tiếng Việt", "Vietnamese", "Basic Latin"),
    ];

    public static LanguageName? Find(string code) => All.FirstOrDefault(l => l.Code == code);
    public static LanguageName? ByLabel(string label) => All.FirstOrDefault(l => l.Label == label);

    /// <summary>Short name of a character set for messages ("kana", "Thai", "Latin").</summary>
    public static string ScriptShort(string set) => set switch { "Japanese kana" => "kana", "Basic Latin" => "Latin", _ => set };
}
