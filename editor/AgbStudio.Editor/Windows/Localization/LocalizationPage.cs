using System.Globalization;
using System.Text;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Windows.Database;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace AgbStudio.Editor.Windows.Localization;

/// <summary>
/// Localization (T-P14, 1440 x 900; Tools › Localization): languages with translation progress, groups and "Missing
/// only", the string table (key, source, target, fit) with search, the STRING panel (speaker, font, source and target
/// text with their width in the box, notes) and the GBA preview drawn like the ROM. Export .csv writes every key with
/// its texts; Auto-check fit runs the build's fit check while you type.
/// </summary>
[EditorWindow("T-P14")]
public sealed class LocalizationPage : ToolPage
{
    public const string AllGroup = "All", MissingOnly = "Missing only";

    /// <summary>One row of the string table.</summary>
    public sealed record Row(string Key, string Source, string? Target, GbaText.FitResult? Fit, bool Selected);

    readonly StackPanel _languages = new() { Spacing = 1, Margin = new Thickness(6, 4, 6, 3), Name = "LanguageList" };
    readonly StackPanel _groups = new() { Spacing = 1, Margin = new Thickness(6, 5, 6, 4), Name = "GroupList" };
    readonly ItemsControl _rows;
    readonly ScrollViewer _rowScroll;
    readonly Chip _progressChip;
    readonly StatusItem _statusMissing, _statusKeys;
    readonly GbaScreen _screen = new() { Backdrop = "art/scene/dusk", Width = 240, Height = 160, Name = "GbaPreview" };
    readonly TextBlock _caption = UI.Text("", "Label/11", "text/secondary").Named("FitCaption");
    readonly StackPanel _inspector = new() { Name = "StringInspector" };
    readonly Dictionary<(string Lang, string Key, string Text), GbaText.FitResult?> _fits = [];
    ProjectData? _data;
    GbaText? _text;
    List<string> _visible = [];

    public SearchField SearchBox { get; } = new() { Watermark = "Search keys or text…", Width = 240, Name = "SearchStrings", HorizontalAlignment = HorizontalAlignment.Left };
    public AgbButton ExportButton { get; }
    public AgbButton AutoCheckButton { get; }
    public string Target { get; private set; } = "";
    public string Group { get; private set; } = AllGroup;
    public string? SelectedKey { get; private set; }
    public bool AutoCheck { get; private set; } = true;
    public ProjectData? Data => _data;
    public GbaText? Gba => _text;
    public IReadOnlyList<string> VisibleKeys => _visible;

    public TextBox? SourceBox { get; private set; }
    public TextBox? TargetBox { get; private set; }
    public TextBox? NotesBox { get; private set; }
    public InspectorField? SpeakerField { get; private set; }
    public InspectorField? FontField { get; private set; }
    public Meter? SourceMeter { get; private set; }
    public Meter? TargetMeter { get; private set; }
    public GbaScreen Screen => _screen;
    public string Caption => _caption.Text ?? "";

    public LocalizationPage(WindowContext ctx) : this(ctx.Services) { }

    public LocalizationPage(EditorServices s) : base(s, "LocalizationPage")
    {
        _progressChip = new Chip { Glyph = "globe", Style = ChipStyle.Warning, Name = "ProgressChip" };
        _progressChip.Click += (_, _) => Dropdown.Show(_progressChip, _data?.Languages.Select(l => _data.LanguageLabel(l.Code)) ?? [], _data?.LanguageLabel(Target),
            v => { if (_data?.Loc.Languages.FirstOrDefault(l => _data.LanguageLabel(l.Code) == v) is { } l) SelectLanguage(l.Code); });
        SetToolbar(
            ToolGroup([
                Tool("search", "Tool_Search", "Search keys or text", () => SearchBox.Focus(), active: true),
                Tool("filter", "Tool_Missing", "Missing only", () => SelectGroup(Group == MissingOnly ? AllGroup : MissingOnly)),
            ]),
            _progressChip);

        var langHeader = new PanelHeader { Title = "Languages", Glyph = "globe", ActionGlyph = "plus", Name = "LanguagesHeader" };
        langHeader.ActionCommand = new Relay(() => _ = s.Windows.Open("T-D20", this, langHeader, "Localization.LanguagesAdd"));
        var groupHeader = new PanelHeader { Title = "Groups", Glyph = "folder", Name = "GroupsHeader" };
        LeftContent = new ScrollViewer { Content = UI.Column(0, langHeader, _languages, groupHeader, _groups) };

        ExportButton = new AgbButton { Content = "Export .csv", Glyph = "upload", Kind = ButtonKind.Secondary, Name = "ExportCsvButton" };
        ExportButton.Click += (_, _) => _ = ExportCsv();
        AutoCheckButton = new AgbButton { Content = "Auto-check fit", Glyph = "check", Kind = ButtonKind.Primary, Name = "AutoCheckButton" };
        AutoCheckButton.Click += (_, _) => ToggleAutoCheck();
        var buttons = UI.Row(8, ExportButton, AutoCheckButton);
        buttons.HorizontalAlignment = HorizontalAlignment.Right;
        var filterRow = new Panel { Height = 28, Margin = new Thickness(16, 10, 16, 10), Children = { SearchBox, buttons } };
        SearchBox.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) RefreshRows(); };

        _rows = new ItemsControl { ItemsPanel = new FuncTemplate<Panel?>(() => new VirtualizingStackPanel()), ItemTemplate = new FuncDataTemplate<Row>((r, _) => r is null ? new Panel() : RowView(r)), Name = "StringRows" };
        _rowScroll = new ScrollViewer { Content = _rows };
        var header = new Grid { ColumnDefinitions = Columns(), Name = "StringHeader" };
        var table = new DockPanel { Width = 770, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(16, 0, 0, 0), Children = { header, _rowScroll } };
        DockPanel.SetDock(header, Dock.Top);
        _tableHeader = header;

        var clip = new Border { CornerRadius = new CornerRadius(8), ClipToBounds = true, Child = _screen, Width = 240, Height = 160, VerticalAlignment = VerticalAlignment.Top };
        var previewText = UI.Column(6, Ui.Overline("Preview on GBA · 240×160"), _caption);
        previewText.Margin = new Thickness(0, 1, 0, 0);
        var preview = UI.Frame(UI.Row(16, clip, previewText), "bg/panel", "border/default", new Thickness(0, 1, 0, 0), new Thickness(16, 11, 16, 11)).Named("PreviewPanel");
        var center = new DockPanel();
        DockPanel.SetDock(filterRow, Dock.Top);
        DockPanel.SetDock(preview, Dock.Bottom);
        center.Children.AddRange([filterRow, preview, table]);
        MainContent = center;

        var stringHeader = new PanelHeader { Title = "String", Glyph = "type", Name = "StringHeaderPanel" };
        var right = new DockPanel();
        DockPanel.SetDock(stringHeader, Dock.Top);
        right.Children.AddRange([stringHeader, new ScrollViewer { Content = _inspector }]);
        RightContent = right;

        _statusMissing = new StatusItem { Glyph = "warn", Name = "Status_Missing" };
        AddStatus(_statusMissing);
        _statusKeys = new StatusItem { Glyph = "globe", Name = "Status_Keys" };
        AddStatus(_statusKeys, right: true);

        s.Project.Changed += () => { Attach(); Refresh(); };
        Attach();
        Refresh();
    }

    readonly Grid _tableHeader;

    static ColumnDefinitions Columns() => ColumnDefinitions.Parse("200,250,250,70");

    void Attach()
    {
        var d = ProjectData.For(S);
        if (d == _data) return;
        if (_data != null) _data.Changed -= Refresh;
        _data = d;
        _text = d is null ? null : new GbaText(d);
        _fits.Clear();
        if (_data != null)
        {
            _data.Changed += Refresh;
            var data = _data;
            Document = data.Document("Localization", "globe", null, () =>
            {
                data.SaveStrings();
                S.ProjectSettings.Save();
                return Task.CompletedTask;
            });
            var def = data.Project.Settings.General.DefaultLanguage;
            Target = def != data.Source && data.Loc.Languages.Any(l => l.Code == def) ? def : data.Loc.Languages.FirstOrDefault(l => l.Code != data.Source)?.Code ?? data.Source;
            SelectedKey = data.Keys().FirstOrDefault();
        }
        else Document = null;
    }

    protected override void OnShown()
    {
        Attach();
        Refresh();
        base.OnShown();
    }

    // ---- model -----------------------------------------------------------------------------------------

    public int TotalKeys => _data?.Keys().Count ?? 0;

    public IReadOnlyList<string> MissingKeys(string lang) =>
        _data is null ? [] : _data.Keys().Where(k => _data.Text(lang, k) is null).ToList();

    /// <summary>Groups as listed: by number of keys, largest first (Dialogue 112, UI &amp; menus 86, Items 48).</summary>
    public IReadOnlyList<(string Group, int Count)> Groups()
    {
        if (_data is null) return [];
        var keys = _data.Keys();
        return keys.Select(k => _data.Info(k).Group).Where(g => g.Length > 0).GroupBy(g => g).Select(g => (g.Key, g.Count()))
            .OrderByDescending(g => g.Item2).ThenBy(g => g.Key, StringComparer.Ordinal).ToList();
    }

    IEnumerable<string> FilteredKeys()
    {
        if (_data is null) return [];
        var q = (SearchBox.Text ?? "").Trim();
        return _data.Keys().Where(k =>
        {
            if (Group == MissingOnly && _data.Text(Target, k) != null) return false;
            if (Group != AllGroup && Group != MissingOnly && _data.Info(k).Group != Group) return false;
            if (q.Length == 0) return true;
            return k.Contains(q, StringComparison.OrdinalIgnoreCase) || (_data.Text(_data.Source, k) ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)
                || (_data.Text(Target, k) ?? "").Contains(q, StringComparison.OrdinalIgnoreCase);
        });
    }

    /// <summary>The fit of a key's text in a language (cached; recomputed while Auto-check fit is on).</summary>
    public GbaText.FitResult? FitOf(string lang, string key, string text)
    {
        if (_text is null) return null;
        if (_fits.TryGetValue((lang, key, text), out var f)) return f;
        if (!AutoCheck)
        {
            var last = _fits.Where(x => x.Key.Lang == lang && x.Key.Key == key).Select(x => x.Value).LastOrDefault();
            if (last != null || _fits.Keys.Any(x => x.Lang == lang && x.Key == key)) return last;
        }
        return _fits[(lang, key, text)] = _text.Fit(lang, key, text);
    }

    public void SelectLanguage(string code)
    {
        Target = code;
        Refresh();
    }

    public void SelectGroup(string group)
    {
        Group = group;
        Refresh();
    }

    public void SelectKey(string? key, bool scroll = true)
    {
        SelectedKey = key;
        RefreshRows();
        RefreshInspector();
        if (scroll && key != null && _visible.IndexOf(key) is >= 0 and var i) _rowScroll.Offset = new Vector(0, Math.Max(0, (i - 1) * 32));
    }

    // ---- view ------------------------------------------------------------------------------------------

    readonly Reentry _refresh = new(), _refreshInspector = new();

    public void Refresh() => _refresh.Run(RefreshCore);

    void RefreshCore()
    {
        if (_data != null && !_data.Loc.Languages.Any(l => l.Code == Target)) Target = _data.Loc.Languages.FirstOrDefault(l => l.Code != _data.Source)?.Code ?? _data.Source;
        RefreshLeft();
        RefreshRows();
        RefreshInspector();
        var missing = _data is null ? 0 : MissingKeys(Target).Count;
        var total = TotalKeys;
        _progressChip.Content = _data is null ? "" : $"{_data.LanguageName(Target)} {(total - missing).ToString(CultureInfo.InvariantCulture)} / {total.ToString(CultureInfo.InvariantCulture)}";
        _progressChip.Style = missing > 0 ? ChipStyle.Warning : ChipStyle.Success;
        _statusMissing.Content = _data is null ? null : missing == 1 ? $"1 string missing in {_data.LanguageName(Target)}" : $"{missing} strings missing in {_data.LanguageName(Target)}";
        _statusMissing.IsVisible = _data != null && missing > 0;
        _statusKeys.Content = $"{total} keys";
        _tableHeader.Children.Clear();
        string[] heads = ["Key", _data?.LanguageName(_data.Source) is { } src ? LanguageNames.Find(_data.Source)?.English ?? src : "Source", _data?.LanguageName(Target) ?? "", "Fit"];
        for (var c = 0; c < 4; c++) _tableHeader.Children.Add(new TableCell { Kind = TableCellKind.Header, Text = heads[c] }.At(0, c));
        AutoCheckButton.Kind = AutoCheck ? ButtonKind.Primary : ButtonKind.Secondary;
    }

    void RefreshLeft()
    {
        _languages.Children.Clear();
        _groups.Children.Clear();
        if (_data is null) return;
        var keys = _data.Keys();
        foreach (var l in _data.Languages)
        {
            var item = new TreeItem { Label = _data.LanguageLabel(l.Code), Glyph = "globe", Meta = $"{_data.Translated(l.Code, keys)} / {keys.Count}", IsSelected = l.Code == Target, Name = "Lang_" + l.Code };
            // Figma writes the translated count in the caption style, not the Tree Item's mono meta
            item.TemplateApplied += (_, e) => { if (e.NameScope.Find<TextBlock>("PART_Meta") is { } meta) AgbStudio.Editor.Theme.Fig.SetStyle(meta, "Caption/10"); };
            var code = l.Code;
            item.Activated += (_, _) => SelectLanguage(code);
            _languages.Children.Add(item);
        }
        void Add(string label, string glyph, int count, string name)
        {
            var row = new ListRow { Label = label, Glyph = glyph, Meta = count.ToString(CultureInfo.InvariantCulture), IsSelected = Group == label, Name = name };
            row.Activated += (_, _) => SelectGroup(label);
            _groups.Children.Add(row);
        }
        Add(AllGroup, "folder", keys.Count, "Group_All");
        foreach (var (g, n) in Groups()) Add(g, "folder", n, "Group_" + g.Replace(" ", "").Replace("&", ""));
        Add(MissingOnly, "warn", MissingKeys(Target).Count, "Group_Missing");
    }

    public IReadOnlyList<TreeItem> LanguageItems => _languages.Children.OfType<TreeItem>().ToList();
    public IReadOnlyList<ListRow> GroupItems => _groups.Children.OfType<ListRow>().ToList();

    void RefreshRows()
    {
        if (_data is null) { _rows.ItemsSource = null; _visible = []; return; }
        _visible = FilteredKeys().ToList();
        _rows.ItemsSource = _visible.Select(RowOf).ToList();
    }

    public Row RowOf(string key)
    {
        var src = _data!.Text(_data.Source, key) ?? "";
        var target = _data.Text(Target, key);
        return new Row(key, src, target, target is null ? null : FitOf(Target, key, target), key == SelectedKey);
    }

    Control RowView(Row r)
    {
        var g = new Grid { ColumnDefinitions = Columns(), Height = 32, Background = Brushes.Transparent, Name = "Key_" + r.Key };
        var kind = r.Selected ? TableCellKind.Selected : TableCellKind.Default;
        g.Children.Add(new TableCell { Text = r.Key, Kind = r.Selected ? TableCellKind.Selected : TableCellKind.Mono }.At(0, 0));
        g.Children.Add(new TableCell { Text = r.Source, Kind = kind }.At(0, 1));
        var target = new TableCell { Text = r.Target ?? "— missing —", Kind = kind }.At(0, 2);
        if (r.Target is null) target.Token(TableCell.ForegroundProperty, "status/warning");
        g.Children.Add(target);
        var chip = r.Target is null ? new Chip { Content = "missing", Style = ChipStyle.Warning }
            : r.Fit is null ? null
            : new Chip { Content = r.Fit.Fits ? "ok" : "long", Style = r.Fit.Fits ? ChipStyle.Success : ChipStyle.Warning };
        if (chip != null) { chip.IsHitTestVisible = false; chip.HorizontalAlignment = HorizontalAlignment.Left; chip.VerticalAlignment = VerticalAlignment.Center; }
        var fit = new Border { Child = chip, Padding = new Thickness(10, 0, 0, 0), BorderThickness = new Thickness(0, 0, 0, 1), Name = "Fit_" + r.Key }.Token(Border.BorderBrushProperty, "border/default").At(0, 3);
        if (r.Selected) fit.Token(Border.BackgroundProperty, "accent/selected");
        g.Children.Add(fit);
        g.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(g).Properties.IsLeftButtonPressed) return;
            SelectKey(r.Key, scroll: false);
            if (e.ClickCount >= 2) TargetBox?.Focus();
            e.Handled = true;
        };
        return g;
    }

    // ---- inspector ------------------------------------------------------------------------------------

    string? _shape;

    void RefreshInspector() => _refreshInspector.Run(RefreshInspectorCore);

    void RefreshInspectorCore()
    {
        if (_data is null || SelectedKey is null || _text is null)
        {
            _inspector.Children.Clear();
            _shape = null;
            _screen.Box = null;
            _caption.Text = "";
            return;
        }
        var data = _data;
        var key = SelectedKey;
        var shape = key + "|" + Target;
        if (_shape != shape)
        {
            _shape = shape;
            BuildInspector(data, key);
        }
        var info = data.Info(key);
        SpeakerField!.Value = info.Speaker.Length == 0 ? "—" : info.Speaker;
        FontField!.Value = _text.FontName(Target);
        if (!SourceBox!.IsFocused) SourceBox.Text = data.Text(data.Source, key) ?? "";
        if (!TargetBox!.IsFocused) TargetBox.Text = data.Text(Target, key) ?? "";
        if (!NotesBox!.IsFocused) NotesBox.Text = info.Notes;
        UpdateMeters();
    }

    void BuildInspector(ProjectData data, string key)
    {
        _inspector.Children.Clear();
        SpeakerField = new InspectorField { Kind = FieldKind.Dropdown, Label = "Speaker", Name = "SpeakerField" };
        Dropdown.Attach(SpeakerField, Speakers, v => SetSpeaker(v == "—" ? "" : v));
        FontField = new InspectorField { Kind = FieldKind.Dropdown, Label = "Font", Name = "FontField" };
        Dropdown.Attach(FontField, () => _text!.FontNames(), SetFont);
        _inspector.Children.Add(Section(key, "type", UI.Column(6, SpeakerField, FontField), new Thickness(12, 8, 12, 10)));

        _inspector.Children.Add(Ui.Rule());
        SourceBox = TextArea("SourceText", data.Source);
        SourceMeter = new Meter { Label = "Box width", Name = "SourceMeter" };
        Live(SourceBox, () => data.Source, "Edit text");
        _inspector.Children.Add(Section(LanguageNames.Find(data.Source)?.English ?? data.LanguageName(data.Source), "globe", UI.Column(8, SourceBox, SourceMeter), new Thickness(12, 8, 12, 10)));

        _inspector.Children.Add(Ui.Rule());
        TargetBox = TextArea("TargetText", Target);
        TargetMeter = new Meter { Label = "Box width", Name = "TargetMeter" };
        Live(TargetBox, () => Target, "Edit text");
        _inspector.Children.Add(Section(data.LanguageName(Target), "globe", UI.Column(8, TargetBox, TargetMeter), new Thickness(12, 8, 12, 10)));

        _inspector.Children.Add(Ui.Rule());
        NotesBox = new TextBox { Name = "NotesText", TextWrapping = TextWrapping.Wrap, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0), Height = double.NaN, MinHeight = 16 };
        AgbStudio.Editor.Theme.Fig.SetStyle(NotesBox, "Label/11");
        NotesBox.Token(TextBox.ForegroundProperty, "text/secondary");
        string? notesBefore = null;
        string? notesKey = null;
        NotesBox.GotFocus += (_, _) => { notesKey = SelectedKey; notesBefore = notesKey is null ? null : data.Info(notesKey).Notes; };
        NotesBox.LostFocus += (_, _) =>
        {
            if (notesKey is not { } k) return;
            notesKey = null;
            var now = NotesBox.Text ?? "";
            var old = notesBefore ?? data.Info(k).Notes;
            notesBefore = null;
            if (now == old) return;
            data.Edit("Edit notes", Document, () => data.EnsureInfo(k).Notes = now, () => data.EnsureInfo(k).Notes = old);
        };
        _inspector.Children.Add(Section("Notes", "book", NotesBox, new Thickness(12, 8, 12, 10)));
        _inspector.Children.Add(Ui.Rule());
    }

    /// <summary>A text box for a language: Thai text in the Figma "Thai/13" style, other scripts in "Body/12".</summary>
    static TextBox TextArea(string name, string lang)
    {
        var thai = LanguageNames.Find(lang)?.Script == "Thai";
        var box = new TextBox { Name = name, TextWrapping = TextWrapping.Wrap, AcceptsReturn = false, Height = double.NaN, MinHeight = thai ? 41 : 38, Padding = new Thickness(11, 9) };
        AgbStudio.Editor.Theme.Fig.SetStyle(box, thai ? "Thai/13" : "Body/12");
        return box;
    }

    static StackPanel Section(string title, string glyph, Control content, Thickness margin)
    {
        var header = new SectionHeader { Title = title, Glyph = glyph, Name = "Section_" + glyph + "_" + title.Replace(" ", "") };
        var s = UI.Column(6, header, content);   // Figma: 6 px under the Section Header, 10 px above the rule
        s.Margin = margin;
        header.PropertyChanged += (_, e) => { if (e.Property == SectionHeader.IsOpenProperty) content.IsVisible = header.IsOpen; };
        return s;
    }

    /// <summary>Typing updates the table, the box width and the preview at once; leaving the box records one undo step.</summary>
    void Live(TextBox box, Func<string> lang, string undoName)
    {
        string? before = null;
        string? key = null;
        string? language = null;
        box.GotFocus += (_, _) => { key = SelectedKey; language = lang(); before = key is null ? null : _data?.Text(language, key); };
        box.PropertyChanged += (_, e) =>
        {
            if (e.Property != TextBox.TextProperty || !box.IsFocused || key is null || language is null || _data is null) return;
            var now = box.Text ?? "";
            _data.SetText(language, key, now.Length == 0 ? null : now);
            if (Document != null) Document.IsDirty = true;
            UpdateMeters();
            RefreshRow(key);
        };
        box.LostFocus += (_, _) =>
        {
            if (key is null || language is null || _data is null) return;
            var data = _data;
            var k = key;
            var l = language;
            var old = before;
            var now = data.Text(l, k);
            key = null;
            if (now == old) return;
            data.Applied(undoName, Document, () => data.SetText(l, k, now), () => data.SetText(l, k, old));
        };
        box.AddHandler(Avalonia.Input.InputElement.KeyDownEvent, (_, e) => { if (e.Key == Key.Enter) { _rowScroll.Focus(); e.Handled = true; } }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    void RefreshRow(string key)
    {
        if (_rows.ItemsSource is not List<Row> list) return;
        var i = list.FindIndex(r => r.Key == key);
        if (i < 0) return;
        list[i] = RowOf(key);
        _rows.ItemsSource = list.ToList();
    }

    IEnumerable<string> Speakers()
    {
        var names = new SortedSet<string>(StringComparer.Ordinal);
        if (_data != null)
        {
            foreach (var d in _data.Dialogues) foreach (var s in d.File.Speakers) names.Add(s.Name);
            foreach (var i in _data.Meta.Keys.Values) if (i.Speaker.Length > 0) names.Add(i.Speaker);
        }
        return ["—", .. names];
    }

    void SetSpeaker(string speaker)
    {
        if (_data is null || SelectedKey is null) return;
        var data = _data;
        var k = SelectedKey;
        var old = data.Info(k).Speaker;
        if (old == speaker) return;
        data.Edit("Set speaker", Document, () => data.EnsureInfo(k).Speaker = speaker, () => data.EnsureInfo(k).Speaker = old);
    }

    void SetFont(string font)
    {
        if (_data is null || _data.Loc.Languages.FirstOrDefault(l => l.Code == Target) is not { } lang) return;
        var old = lang.Font;
        if (old == font) return;
        _fits.Clear();
        _data.Edit("Set font", Document, () => { lang.Font = font; _fits.Clear(); }, () => { lang.Font = old; _fits.Clear(); });
    }

    void UpdateMeters()
    {
        if (_data is null || SelectedKey is null || _text is null) return;
        var key = SelectedKey;
        var src = _data.Text(_data.Source, key) ?? "";
        var target = _data.Text(Target, key);
        Meter(SourceMeter!, _text.Fit(_data.Source, key, src));
        var fit = target is null ? null : _text.Fit(Target, key, target);
        Meter(TargetMeter!, fit);
        // the ROM shows the fallback language until the key is translated
        var shownLang = target != null ? Target : _data.Loc.Languages.FirstOrDefault(l => l.Code == Target)?.Fallback is { Length: > 0 } fb ? fb : _data.Source;
        var shown = target ?? _data.Text(shownLang, key) ?? "";
        _screen.Box = _text.RenderBox(shownLang, shown);
        _caption.Text = (target != null ? fit : _text.Fit(shownLang, key, shown))?.Caption(_data.Loc.ThaiWrap) ?? "";
    }

    static void Meter(Meter m, GbaText.FitResult? f)
    {
        if (f is null) { m.ValueText = "—"; m.Percent = 0; return; }
        m.ValueText = $"{f.Width} / {f.BoxWidth} px";
        m.Percent = Math.Min(100, f.BoxWidth == 0 ? 0 : f.Width * 100.0 / f.BoxWidth);
        m.Status = f.Fits ? ProgressStatus.OK : ProgressStatus.Warn;
    }

    // ---- actions ---------------------------------------------------------------------------------------

    /// <summary>Auto-check fit on: the FIT column follows every edit. Off: it keeps the last results until switched on again.</summary>
    public void ToggleAutoCheck()
    {
        AutoCheck = !AutoCheck;
        if (AutoCheck) _fits.Clear();
        Refresh();
    }

    /// <summary>Export .csv (T-Y02 save dialog): key, group, speaker, notes and one column per language.</summary>
    public async Task<string?> ExportCsv()
    {
        if (_data is null) return null;
        var path = await FileChooser.For(S).Save("Export .csv", GameProject.FileName(_data.Project.Settings.Name) + "_strings.csv");
        if (path is null) return null;
        File.WriteAllText(path, Csv(_data), new UTF8Encoding(true));
        S.Toasts.Show($"{Path.GetFileName(path)} exported · {TotalKeys} keys");
        return path;
    }

    public static string Csv(ProjectData data)
    {
        var langs = data.Languages.Select(l => l.Code).ToList();
        var sb = new StringBuilder();
        sb.Append(string.Join(",", new[] { "key", "group", "speaker", "notes" }.Concat(langs))).Append("\r\n");
        foreach (var k in data.Keys())
        {
            var i = data.Info(k);
            sb.Append(string.Join(",", new[] { k, i.Group, i.Speaker, i.Notes }.Concat(langs.Select(l => data.Text(l, k) ?? "")).Select(Quote))).Append("\r\n");
        }
        return sb.ToString();
    }

    static string Quote(string s) => s.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
}
