using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgbStudio.Core.Project;
using StringInfo = AgbStudio.Core.Project.StringInfo;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Windows.Database;
using AgbStudio.Scripting.Dialogue;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace AgbStudio.Editor.Windows.Dialogue;

/// <summary>
/// Dialogue Editor (T-P16, 1440 x 900; Tools › Dialogue Editor, Create › Dialogue): the conversations of
/// <c>dialogue/*.dlg</c>, their lines with speaker and text in two languages (the toolbar chip swaps Thai / English),
/// choices with their targets, commands, the speakers of the file with New Speaker, and the LINE inspector (GBA
/// preview drawn like the ROM, speaker, portrait, text speed, blip sound, "Show if" / "Then set"). Line texts are kept
/// in the Localization tables under the line's key. Every edit is undoable.
/// </summary>
[EditorWindow("T-P16")]
public sealed class DialogueEditorPage : ToolPage, IEditTarget
{
    readonly StackPanel _conversations = new() { Spacing = 1, Margin = new Thickness(6, 4, 6, 3), Name = "ConversationList" };
    readonly StackPanel _speakers = new() { Spacing = 1, Margin = new Thickness(6, 5, 6, 4), Name = "SpeakerList" };
    readonly StackPanel _cards = new() { Spacing = 10, Margin = new Thickness(40, 16, 40, 16), Name = "LineCards" };
    readonly Chip _langChip;
    readonly StatusItem _statusLines, _statusSaved;
    readonly LineInspector _inspector;
    ProjectData? _data;
    DialogueLine? _clipboard;

    public ProjectData? Data => _data;
    public DialogueDoc? Current { get; private set; }
    public int ConversationIndex { get; private set; }
    public int SelectedLine { get; private set; } = -1;
    public int SelectedChoice { get; private set; } = -1;
    public string Primary { get; private set; } = "en";
    public string Secondary { get; private set; } = "en";
    public GbaText? Gba { get; private set; }
    public Conversation? Conversation => Current is { } d && ConversationIndex < d.File.Conversations.Count ? d.File.Conversations[ConversationIndex] : null;
    public DialogueLine? Line => Conversation is { } c && SelectedLine >= 0 && SelectedLine < c.Lines.Count ? c.Lines[SelectedLine] : null;
    public LineInspector Inspector => _inspector;
    public IReadOnlyList<Control> Cards => _cards.Children.ToList();
    public Border AddBox { get; private set; } = null!;

    public DialogueEditorPage(WindowContext ctx) : this(ctx.Services, ctx.Args as string) { }

    public DialogueEditorPage(EditorServices s, string? open = null) : base(s, "DialogueEditorPage")
    {
        s.Set(this);
        _langChip = new Chip { Glyph = "globe", Name = "LanguageChip" };
        _langChip.Click += (_, _) => SwapLanguages();
        var addTool = Tool("plus", "Tool_Add", "Add line · choice · command");
        addTool.Click += (_, _) => _ = s.Windows.Open("T-M23", anchor: addTool, source: "Dialogue.Toolbar");
        SetToolbar(
            ToolGroup([
                Tool("cursor", "Tool_Select", "Select", active: true),
                addTool,
                Tool("link", "Tool_Condition", "Condition", AddCondition),
            ]),
            _langChip);

        var convHeader = new PanelHeader { Title = "Conversations", Glyph = "book", ActionGlyph = "plus", Name = "ConversationsHeader" };
        convHeader.ActionCommand = new Relay(() => NewConversation());
        var speakerHeader = new PanelHeader { Title = "Speakers", Glyph = "user", ActionGlyph = "plus", Name = "SpeakersHeader" };
        speakerHeader.ActionCommand = new Relay(() => { if (Current != null) _ = s.Windows.Open("T-D21", this, speakerHeader, "Dialogue.SpeakersAdd"); });
        LeftContent = new ScrollViewer { Content = UI.Column(0, convHeader, _conversations, speakerHeader, _speakers) };

        MainContent = new ScrollViewer { Content = _cards, Name = "CardScroll" };

        _inspector = new LineInspector(this);
        RightContent = _inspector;

        _statusLines = new StatusItem { Glyph = "book", Name = "Status_Lines" };
        AddStatus(_statusLines);
        _statusSaved = new StatusItem { Glyph = "check", Content = "Saved", Name = "Status_Saved" };
        AddStatus(_statusSaved, right: true);

        s.Project.Changed += () => { Attach(null); Refresh(); };
        Attach(open);
        Refresh();
    }

    void Attach(string? open)
    {
        var d = ProjectData.For(S);
        if (d == _data && open is null) return;
        if (d != _data)
        {
            if (_data != null) _data.Changed -= Refresh;
            _data = d;
            if (_data != null) _data.Changed += Refresh;
            Gba = d is null ? null : new GbaText(d);
            Current = null;
            Document = null;
            if (d != null)
            {
                var def = d.Project.Settings.General.DefaultLanguage;
                Primary = d.Loc.Languages.Any(l => l.Code == def) ? def : d.Source;
                Secondary = Primary != d.Source ? d.Source : d.Loc.Languages.FirstOrDefault(l => l.Code != d.Source)?.Code ?? d.Source;
            }
        }
        if (_data is null) return;
        var doc = (open != null ? _data.Dialogue(open) : null) ?? Current ?? _data.Dialogues.FirstOrDefault();
        if (doc != null) Select(doc, 0, refresh: false);
    }

    protected override void OnShown()
    {
        Attach(null);
        Refresh();
        base.OnShown();
    }

    protected override void DocumentStateChanged() => _statusSaved.IsVisible = Document is not { IsDirty: true };

    // ---- selection -------------------------------------------------------------------------------------

    public void Select(DialogueDoc doc, int conversation = 0, bool refresh = true)
    {
        if (Current != doc)
        {
            var old = Document;
            Current = doc;
            var data = _data!;
            Document = data.Document(doc.FileName, "book", doc.Path, () =>
            {
                doc.Save();
                if (data.StringsDirty) data.SaveStrings();
                return Task.CompletedTask;
            });
            if (old != null && old != Document && !old.IsDirty) S.Documents.Close(old);
            SelectedLine = -1;
        }
        ConversationIndex = Math.Clamp(conversation, 0, Math.Max(0, doc.File.Conversations.Count - 1));
        var lines = Conversation?.Lines;
        SelectedLine = lines is null || lines.Count == 0 ? -1 : Math.Clamp(SelectedLine, 0, lines.Count - 1);
        SelectedChoice = -1;
        if (refresh) Refresh();
    }

    public void SelectLine(int index, int choice = -1)
    {
        SelectedLine = Conversation is { } c ? Math.Clamp(index, -1, c.Lines.Count - 1) : -1;
        SelectedChoice = choice;
        Refresh();
    }

    public void SwapLanguages()
    {
        (Primary, Secondary) = (Secondary, Primary);
        Refresh();
    }

    readonly Reentry _refresh = new();

    public void Refresh() => _refresh.Run(RefreshCore);

    void RefreshCore()
    {
        if (_data != null && Current != null && !_data.Dialogues.Contains(Current))
        {
            Current = null;
            Document = null;
            if (_data.Dialogues.FirstOrDefault() is { } first) Select(first, 0, refresh: false);
        }
        if (Conversation is { } conv && SelectedLine >= conv.Lines.Count) SelectedLine = conv.Lines.Count - 1;
        if (Line?.Choices is not { } ch || SelectedChoice >= ch.Count) SelectedChoice = -1;
        RefreshLeft();
        RefreshCards();
        _inspector.Refresh();
        _langChip.Content = _data is null ? "" : $"{_data.LanguageName(Primary)} / {_data.LanguageName(Secondary)}";
        if (Conversation is { } c && Current != null)
        {
            var choices = c.Lines.Sum(l => l.Choices?.Count ?? 0);
            _statusLines.Content = $"{ConversationLabel(Current, ConversationIndex)} · {Plural(c.Lines.Count, "line")} · {Plural(choices, "choice")}";
        }
        else _statusLines.Content = null;
        _statusLines.IsVisible = _statusLines.Content != null;
        DocumentStateChanged();
    }

    static string Plural(int n, string word) => n == 1 ? $"1 {word}" : $"{n.ToString(CultureInfo.InvariantCulture)} {word}s";

    static string ConversationLabel(DialogueDoc d, int i) => d.File.Conversations.Count <= 1 ? d.Name : $"{d.Name} · {d.File.Conversations[i].Id}";

    void RefreshLeft()
    {
        _conversations.Children.Clear();
        _speakers.Children.Clear();
        if (_data is null) return;
        foreach (var d in _data.Dialogues)
            for (var i = 0; i < Math.Max(1, d.File.Conversations.Count); i++)
            {
                var label = d.File.Conversations.Count == 0 ? d.Name : ConversationLabel(d, i);
                var count = d.File.Conversations.Count == 0 ? 0 : d.File.Conversations[i].Lines.Count;
                var item = new TreeItem { Label = label, Glyph = "book", Meta = count.ToString(CultureInfo.InvariantCulture), IsSelected = d == Current && i == ConversationIndex, Name = "Conversation_" + label.Replace(" · ", "_") };
                var (doc, conv) = (d, i);
                item.Activated += (_, _) => { SelectedLine = -1; Select(doc, conv); };
                _conversations.Children.Add(item);
            }
        if (Current is null) return;
        foreach (var sp in Current.File.Speakers)
            _speakers.Children.Add(new TreeItem { Label = sp.Name, Glyph = "user", Meta = sp.Portrait ?? "", Name = "Speaker_" + sp.Name.Replace(" ", "") });
    }

    public IReadOnlyList<TreeItem> ConversationItems => _conversations.Children.OfType<TreeItem>().ToList();
    public IReadOnlyList<TreeItem> SpeakerItems => _speakers.Children.OfType<TreeItem>().ToList();
    public string? StatusText => _statusLines.Content as string;
    public string LanguageChipText => _langChip.Content as string ?? "";

    void RefreshCards()
    {
        _cards.Children.Clear();
        if (Conversation is not { } c || Current is null) return;
        for (var i = 0; i < c.Lines.Count; i++)
        {
            _cards.Children.Add(LineCards.Card(this, Current, c, i, i == SelectedLine && SelectedChoice < 0));
            if (c.Lines[i].Choices is { Count: > 0 } choices) _cards.Children.Add(LineCards.Choices(this, c, i, choices));
        }
        AddBox = LineCards.AddBox(this);
        _cards.Children.Add(AddBox);
    }

    // ---- text ------------------------------------------------------------------------------------------

    /// <summary>The key of a line's text (its own, or the one the compiler derives: &lt;file&gt;.&lt;conversation&gt;.&lt;line&gt;).</summary>
    public string KeyOf(DialogueLine l, int index) => l.Key ?? $"{Current!.Name}.{Conversation!.Id}.{l.Id ?? "line" + index}";

    /// <summary>Text of a line or choice in a language: the Localization table first, the .dlg copy otherwise.</summary>
    public string? TextOf(string? key, Dictionary<string, string>? texts, string lang) =>
        (key is null ? null : _data?.Text(lang, key)) ?? texts?.GetValueOrDefault(lang);

    /// <summary>Sets the text of a line or choice in a language (also in the Localization table under its key).</summary>
    public void SetText(int line, int choice, string lang, string text)
    {
        if (Conversation is not { } c || line < 0 || line >= c.Lines.Count || _data is null) return;
        var data = _data;
        EditDialogue("Edit text", () =>
        {
            var l = c.Lines[line];
            if (choice >= 0 && l.Choices is { } ch && choice < ch.Count)
            {
                var o = ch[choice];
                o.Key ??= $"{KeyOf(l, line)}.choice{choice}";
                (o.Text ??= [])[lang] = text;
                data.SetText(lang, o.Key, text.Length == 0 ? null : text);
                Meta(o.Key, l.Speaker);
            }
            else
            {
                l.Key ??= KeyOf(l, line);
                (l.Text ??= [])[lang] = text;
                data.SetText(lang, l.Key, text.Length == 0 ? null : text);
                Meta(l.Key, l.Speaker);
            }
        });
    }

    void Meta(string key, string? speaker)
    {
        var info = _data!.EnsureInfo(key);
        if (info.Group.Length == 0) info.Group = "Dialogue";
        if (speaker is { Length: > 0 }) info.Speaker = speaker;
    }

    // ---- edits -----------------------------------------------------------------------------------------

    /// <summary>Applies an edit to the current dialogue (and the strings it touches) as one undo step.</summary>
    public void EditDialogue(string name, Action mutate)
    {
        if (Current is not { } doc || _data is null) return;
        var data = _data;
        var before = doc.ToJson();
        var keysBefore = Keys(doc);
        var stringsBefore = Snapshot(keysBefore);
        var metaBefore = keysBefore.ToDictionary(k => k, k => data.Meta.Keys.TryGetValue(k, out var i) ? Clone(i) : null);
        mutate();
        var after = doc.ToJson();
        var keys = keysBefore.Union(Keys(doc)).ToList();
        foreach (var k in keys.Except(keysBefore)) { stringsBefore[k] = data.Languages.ToDictionary(l => l.Code, _ => (string?)null); metaBefore[k] = null; }
        var stringsAfter = Snapshot(keys);
        var metaAfter = keys.ToDictionary(k => k, k => data.Meta.Keys.TryGetValue(k, out var i) ? Clone(i) : null);
        if (after == before && stringsAfter.All(kv => kv.Value.All(x => stringsBefore[kv.Key].GetValueOrDefault(x.Key) == x.Value))) return;
        void Apply(string json, Dictionary<string, Dictionary<string, string?>> strings, Dictionary<string, StringInfo?> meta)
        {
            doc.Restore(json);
            foreach (var (k, per) in strings) foreach (var (l, t) in per) data.SetText(l, k, t);
            foreach (var (k, i) in meta) { if (i is null) data.Meta.Keys.Remove(k); else data.Meta.Keys[k] = Clone(i); }
        }
        data.Applied(name, Document, () => Apply(after, stringsAfter, metaAfter), () => Apply(before, stringsBefore, metaBefore));
    }

    static StringInfo Clone(StringInfo i) => new() { Group = i.Group, Speaker = i.Speaker, Notes = i.Notes, BoxWidth = i.BoxWidth, MaxLines = i.MaxLines };

    List<string> Keys(DialogueDoc doc) =>
        doc.File.Conversations.SelectMany(c => c.Lines.SelectMany(l => (l.Choices ?? []).Select(ch => ch.Key).Prepend(l.Key))).OfType<string>().Distinct().ToList();

    Dictionary<string, Dictionary<string, string?>> Snapshot(IEnumerable<string> keys) =>
        keys.ToDictionary(k => k, k => _data!.Languages.ToDictionary(l => l.Code, l => _data.Text(l.Code, k)));

    string NewLineId(Conversation c)
    {
        var n = c.Lines.Count + 1;
        while (c.Lines.Any(l => l.Id == "l" + n)) n++;
        return "l" + n;
    }

    /// <summary>Add to Dialogue › Line: a new line after the selected one, same speaker.</summary>
    public void AddLine()
    {
        if (Conversation is not { } c || Current is null) return;
        var at = SelectedLine < 0 ? c.Lines.Count : SelectedLine + 1;
        var speaker = Line?.Speaker ?? Current.File.Speakers.FirstOrDefault()?.Name;
        var id = NewLineId(c);
        var file = Current.Name;
        EditDialogue("Add line", () =>
        {
            var l = new DialogueLine { Id = id, Speaker = speaker, Key = $"dlg.{file}.{id}" };
            c.Lines.Insert(at, l);
            foreach (var lang in _data!.Languages) _data.SetText(lang.Code, l.Key, null);
            Meta(l.Key, speaker);
        });
        SelectedLine = at;
        SelectedChoice = -1;
        Refresh();
    }

    /// <summary>Add to Dialogue › Choice: a new choice on the selected line (it ends the conversation until a target is set).</summary>
    public void AddChoice()
    {
        if (Line is not { Command: null } l || Conversation is null) return;
        var index = SelectedLine;
        EditDialogue("Add choice", () =>
        {
            l.Id ??= NewLineId(Conversation);
            (l.Choices ??= []).Add(new DialogueChoice { Key = $"{KeyOf(l, index)}.choice{l.Choices.Count + 1}", End = true });
        });
        SelectedChoice = l.Choices!.Count - 1;
        Refresh();
    }

    /// <summary>Add to Dialogue › Command › …: adds the command after the selected line and opens its fields.</summary>
    public void AddCommand(string command)
    {
        if (Conversation is not { } c || _data is null) return;
        var at = SelectedLine < 0 ? c.Lines.Count : SelectedLine + 1;
        var line = new DialogueLine { Command = command };
        switch (command)
        {
            case "goToScene": line.Scene = _data.Project.SceneFiles().Select(GameProject.AssetName).FirstOrDefault() ?? ""; break;
            case "setFlag": line.Flag = "NewFlag"; line.Value = true; break;
            case "giveItem":
                var t = _data.Tables.FirstOrDefault();
                line.Table = t?.Name ?? "";
                line.Item = t?.Table.Rows.FirstOrDefault()?.GetValueOrDefault(t.Table.KeyField) ?? "";
                line.Count = 1;
                break;
            case "playSound": line.Sound = SoundName(Sounds().FirstOrDefault() ?? ""); break;
            case "wait": line.Frames = 30; break;
            case "shakeCamera": line.Amplitude = 2; line.Frames = 20; break;
        }
        EditDialogue("Add command", () => c.Lines.Insert(at, line));
        SelectedLine = at;
        SelectedChoice = -1;
        Refresh();
        _inspector.FocusFirst();
    }

    /// <summary>Add to Dialogue › Condition: opens "Show if" of the selected line or choice.</summary>
    public void AddCondition()
    {
        if (Line is null) return;
        _inspector.FocusCondition();
    }

    /// <summary>Conversations +: a new dialogue file with one empty conversation and the current file's speakers.</summary>
    public DialogueDoc? NewConversation()
    {
        if (_data is null) return null;
        var data = _data;
        var n = 1;
        string Path_() => Path.Combine(data.Project.DialogueDir, $"conversation_{n}.dlg");
        while (File.Exists(Path_()) || data.Dialogues.Any(d => d.Path == Path_())) n++;
        var file = new DialogueFile { Name = $"conversation_{n}", Conversations = { new Conversation { Id = "main" } } };
        foreach (var sp in Current?.File.Speakers ?? []) file.Speakers.Add(new DialogueSpeaker { Name = sp.Name, Portrait = sp.Portrait, Blip = sp.Blip, TextSpeed = sp.TextSpeed });
        var doc = new DialogueDoc(Path_(), file);
        if (Current != null) foreach (var (k, v) in Current.SpeakerExtras) doc.SpeakerExtras[k] = (JsonObject)v.DeepClone();
        var previous = Current;
        data.Edit("New conversation", null, () =>
        {
            doc.Save();
            data.Dialogues.Add(doc);
            SelectedLine = -1;
            Select(doc, 0, refresh: false);
        }, () =>
        {
            data.Dialogues.Remove(doc);
            if (File.Exists(doc.Path)) File.Delete(doc.Path);
            if (Document?.Path == doc.Path) S.Documents.Close(Document);
            Current = null;
            if (previous != null) Select(previous, 0, refresh: false);
        });
        return doc;
    }

    /// <summary>New Speaker (T-D21): adds a speaker to the current file.</summary>
    public void AddSpeaker(DialogueSpeaker sp, string? nameEn, string? nameTh, string? color)
    {
        if (Current is not { } doc) return;
        EditDialogue("New speaker", () =>
        {
            doc.File.Speakers.Add(sp);
            var extra = new JsonObject();
            var names = new JsonObject();
            if (nameEn is { Length: > 0 }) names["en"] = nameEn;
            if (nameTh is { Length: > 0 }) names["th"] = nameTh;
            if (names.Count > 0) extra["names"] = names;
            if (color != null) extra["color"] = color;
            if (extra.Count > 0) doc.SpeakerExtras[sp.Name] = extra;
        });
    }

    public IReadOnlyList<string> Sounds() =>
        _data is null ? [] : _data.Project.SoundFiles().Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal)
            .Concat(_data.Project.SfxFiles().Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal)).Distinct().ToList();

    /// <summary>A sound value as the dialogue stores it ("blip.wav" -> "blip").</summary>
    public static string SoundName(string file) => GameProject.AssetName(file);

    public string? SoundFile(string? name) => name is null ? null : Sounds().FirstOrDefault(s => SoundName(s) == name) ?? name;

    /// <summary>Target text of a choice: <c>Scene.Load("Cave_01")</c>, <c>goto line 2</c> or <c>end</c>.</summary>
    public static string TargetText(Conversation c, DialogueChoice ch)
    {
        if (ch.Scene is { Length: > 0 } s) return $"Scene.Load(\"{s}\")";
        if (ch.Goto is { } g && c.Lines.FindIndex(l => l.Id == g) is >= 0 and var i) return $"goto line {i + 1}";
        if (ch.Goto is { } g2) return $"goto {g2}";
        return "end";
    }

    /// <summary>Targets a choice can have: every line of the conversation, every scene, and end.</summary>
    public IReadOnlyList<string> Targets(Conversation c) =>
        Enumerable.Range(1, c.Lines.Count).Select(i => $"goto line {i}")
            .Concat(_data?.Project.SceneFiles().Select(f => $"Scene.Load(\"{GameProject.AssetName(f)}\")") ?? [])
            .Append("end").ToList();

    public void SetTarget(int line, int choice, string target)
    {
        if (Conversation is not { } c) return;
        EditDialogue("Set target", () =>
        {
            var l = c.Lines[line];
            var ch = l.Choices![choice];
            ch.Goto = null;
            ch.Scene = null;
            ch.End = false;
            if (target.StartsWith("goto line ", StringComparison.Ordinal) && int.TryParse(target["goto line ".Length..], out var n) && n >= 1 && n <= c.Lines.Count)
            {
                var t = c.Lines[n - 1];
                t.Id ??= NewLineId(c);
                ch.Goto = t.Id;
            }
            else if (target.StartsWith("Scene.Load(\"", StringComparison.Ordinal)) ch.Scene = target["Scene.Load(\"".Length..^2];
            else ch.End = true;
        });
    }

    // ---- Edit menu (cards) ----------------------------------------------------------------------------

    static DialogueLine CloneLine(DialogueLine l)
    {
        var f = new DialogueFile { Conversations = { new Conversation { Lines = { l } } } };
        return DialogueFile.Parse(f.ToJson()).Conversations[0].Lines[0];
    }

    public bool CanCopy => Line != null;
    public bool CanPaste => _clipboard != null && Conversation != null;
    public void Cut() { Copy(); Delete(); }
    public void Copy() { if (Line is { } l) _clipboard = CloneLine(l); }

    public void Paste()
    {
        if (_clipboard is null || Conversation is not { } c) return;
        var copy = CloneLine(_clipboard);
        copy.Id = NewLineId(c);
        if (copy.Key != null)
        {
            var oldKey = copy.Key;
            copy.Key = $"dlg.{Current!.Name}.{copy.Id}";
            foreach (var lang in _data!.Languages)
                if (_data.Text(lang.Code, oldKey) is { } t) (copy.Text ??= [])[lang.Code] = t;
        }
        var at = SelectedLine < 0 ? c.Lines.Count : SelectedLine + 1;
        EditDialogue("Paste", () =>
        {
            c.Lines.Insert(at, copy);
            if (copy.Key != null) foreach (var (lang, t) in copy.Text ?? []) _data!.SetText(lang, copy.Key, t);
        });
        SelectedLine = at;
        Refresh();
    }

    public void Duplicate()
    {
        Copy();
        Paste();
    }

    public void Delete()
    {
        if (Conversation is not { } c || Line is not { } l) return;
        var choice = SelectedChoice;
        EditDialogue(choice >= 0 ? "Delete choice" : "Delete line", () =>
        {
            if (choice >= 0 && l.Choices is { } ch && choice < ch.Count) { ch.RemoveAt(choice); if (ch.Count == 0) l.Choices = null; }
            else c.Lines.Remove(l);
        });
        SelectedChoice = -1;
        Refresh();
    }

    public void SelectAll() { }

    /// <summary>Create › Dialogue: the Dialogue Editor with a new conversation.</summary>
    [CommandHandler("assets.create.dialogue")]
    public static void CreateDialogue(EditorServices s)
    {
        _ = s.Windows.Open("T-P16", source: "assets.create.dialogue");
        if (s.Windows.Host?.CurrentPage is DialogueEditorPage page) page.NewConversation();
    }

    internal static JsonElement Statements(string text)
    {
        var parts = text.Split(';').Select(p => p.Trim()).Where(p => p.Length > 0).ToArray();
        return JsonSerializer.SerializeToElement(parts);
    }

    internal static string StatementsText(JsonElement? e) => e is not { } v ? "" : v.ValueKind switch
    {
        JsonValueKind.String => v.GetString() ?? "",
        JsonValueKind.Array => string.Join("; ", v.EnumerateArray().Select(x => x.GetString() ?? "")),
        _ => "",
    };
}
