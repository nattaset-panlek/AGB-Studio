using AgbStudio.Editor.Commands;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Graphics;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Windows.Launcher;

/// <summary>
/// The Launcher (1440 x 900): Projects (T-P12, the start page and File › Open Project…), Templates (T-P20) and Learn
/// (T-P21). One page per tab id; the top bar, brand block and navigation are shared. The page replaces the whole
/// window, so the Title Bar is hidden while it is shown.
/// </summary>
[EditorWindow("T-P12")]
[EditorWindow("T-P20")]
[EditorWindow("T-P21")]
public sealed class LauncherPage : DockPanel, IShortcutScope
{
    readonly EditorServices _s;
    readonly LauncherService _launcher;
    readonly TextBlock _status = UI.Text(null, "Label/11", "text/muted").Named("LauncherStatus");
    readonly ContentControl _main = new() { Name = "LauncherMain" };
    readonly ContentControl _side = new() { Name = "LauncherSide" };
    readonly SearchField _search;
    readonly StackPanel _cards = new() { Name = "ProjectCards", Spacing = 16 };
    readonly StackPanel _templateRows = new() { Name = "TemplateGrid", Spacing = 16 };
    readonly StackPanel _filters = new() { Orientation = Orientation.Horizontal, Spacing = 6, Name = "TemplateFilters" };
    readonly StackPanel _tutorialRows = new() { Spacing = 18, Name = "TutorialRows" };
    readonly StackPanel _homeLearn = new() { Spacing = 12, Name = "HomeLearn" };
    TemplateCategory? _category;
    string _templateId = TemplateCatalog.All[0].Id;
    bool _refreshQueued;

    /// <summary>"T-P12" Projects, "T-P20" Templates or "T-P21" Learn.</summary>
    public string Tab { get; }

    /// <summary>Keys run the Launcher's commands (F2 Rename, Enter Open), never the Scene Editor's.</summary>
    public ShortcutContext ShortcutContext => ShortcutContext.Launcher;

    public LauncherPage(WindowContext ctx) : this(ctx.Services, ctx.Info.Id, ctx.Args as string) { }

    public LauncherPage(EditorServices s, string tab = "T-P12", string? templateId = null)
    {
        _s = s;
        Tab = tab;
        Name = "Launcher_" + tab.Replace('-', '_');
        _launcher = LauncherService.Of(s);
        if (templateId != null) _templateId = TemplateCatalog.Find(templateId).Id;
        this.Token(BackgroundProperty, "bg/base");
        _search = new SearchField
        {
            Width = 240, Name = "LauncherSearch", HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
            Watermark = tab == "T-P20" ? "Search templates…" : "Search projects…",
        };
        _search.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Refresh(); };

        var top = TopBar();
        SetDock(top, Dock.Top);
        var side = Sidebar();
        SetDock(side, Dock.Left);
        var right = UI.Frame(_side, "bg/panel", "border/default", new Thickness(1, 0, 0, 0)).Named("LauncherRight");
        right.Width = 320;
        SetDock(right, Dock.Right);
        var scroll = new ScrollViewer { Content = _main, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, Name = "LauncherScroll" };
        Children.AddRange([top, side, right, scroll]);

        _main.Content = tab switch { "T-P20" => TemplatesMain(), "T-P21" => LearnMain(), _ => ProjectsMain() };
        _side.Content = tab switch { "T-P20" => TemplateDetails(), "T-P21" => LearnSide(), _ => HomeSide() };

        _launcher.Changed += Refresh;
        _launcher.SelectionChanged += () => Dispatcher.UIThread.Post(UpdateSelection);
        _launcher.Tutorials.Changed += Refresh;
        _launcher.RenameRequested += BeginRename;
        AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Bubble);
        Fill();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // the Launcher has its own top bar instead of the Title Bar
        if (TopLevel.GetTopLevel(this) is MainWindow w) w.TitleBar.IsVisible = false;
        Fill();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (e.Root is MainWindow w) w.TitleBar.IsVisible = true;
        base.OnDetachedFromVisualTree(e);
    }

    void Refresh()
    {
        if (_refreshQueued) return;
        _refreshQueued = true;
        Dispatcher.UIThread.Post(() => { _refreshQueued = false; Fill(); });
    }

    void Fill()
    {
        _status.Text = _launcher.StatusLine();
        switch (Tab)
        {
            case "T-P20": FillTemplates(); break;
            case "T-P21": FillLearn(); break;
            default: FillProjects(); FillHomeLearn(); break;
        }
    }

    // ---- shared chrome -----------------------------------------------------------------------------

    Border TopBar()
    {
        var open = new AgbButton { Content = "Open…", Glyph = "folder", Kind = ButtonKind.Secondary, Name = "LauncherOpen" };
        open.Click += async (_, _) => await OpenFolderAsync();
        var create = new AgbButton { Content = "New Project", Glyph = "plus", Kind = ButtonKind.Primary, Name = "LauncherNewProject" };
        create.Click += (_, _) => _ = _s.Windows.Open("T-D01", source: "LauncherNewProject");
        var buttons = UI.Row(12, open, create);
        buttons.VerticalAlignment = VerticalAlignment.Center;
        var dock = new DockPanel();
        SetDock(buttons, Dock.Right);
        dock.Children.AddRange([buttons, _status]);
        var bar = UI.Frame(dock, "bg/panel", "border/default", new Thickness(0, 0, 0, 1), new Thickness(16, 0)).Named("LauncherTopBar");
        bar.Height = 48;
        return bar;
    }

    Border Sidebar()
    {
        var icon = new AppIcon { Variant = AppIconTheme.Light, Width = 36, Height = 36, VerticalAlignment = VerticalAlignment.Center };
        var word = new TextBlock { Text = "AGB Studio", FontFamily = AgbStudio.Editor.Theme.Fonts.Wordmark, FontWeight = FontWeight.ExtraBold, FontSize = 17, LineHeight = 20 }
            .Token(TextBlock.ForegroundProperty, "text/primary");
        var sub = UI.Text($"{EditorVersion.Short} · for Game Boy Advance", "Caption/10", "text/muted");
        var brand = UI.Row(10, icon, UI.Column(2, word, sub));
        brand.Margin = new Thickness(6, 18, 0, 22);
        brand.Name = "LauncherBrand";
        var nav = UI.Column(4);
        foreach (var (id, label, glyph) in new[] { ("T-P12", "Projects", "folder"), ("T-P20", "Templates", "tiles"), ("T-P21", "Learn", "book"), ("T-D09", "Settings", "sliders") })
        {
            var item = new NavItem { Label = label, Glyph = glyph, IsSelected = id == Tab, Name = "Nav_" + label };
            var target = id;
            item.Activated += (_, _) => _ = _s.Windows.Open(target, source: "LauncherNav_" + label);
            nav.Children.Add(item);
        }
        var col = UI.Column(0, brand, nav);
        col.Margin = new Thickness(12, 0);
        var b = UI.Frame(col, "bg/panel", "border/default", new Thickness(0, 0, 1, 0)).Named("LauncherSidebar");
        b.Width = 220;
        return b;
    }

    static Button Bare(Control content, string? name = null) => new()
    {
        Theme = (Avalonia.Styling.ControlTheme?)Application.Current?.FindResource("BareButton"), Content = content,
        HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Name = name, Cursor = new Cursor(StandardCursorType.Hand),
    };

    /// <summary>Long project names wrap onto a second line beside the time, as the Figma cards show ("Cave Quest / RPG").</summary>
    static void WrapName(SceneCard card) => card.TemplateApplied += (_, _) =>
    {
        foreach (var t in card.GetVisualDescendants().OfType<TextBlock>().Where(t => t.TextTrimming != TextTrimming.None && t.Text == card.NameText))
        {
            t.TextTrimming = TextTrimming.None;
            t.TextWrapping = TextWrapping.Wrap;
            t.MaxLines = 2;
        }
    };

    static Control Header(string title, Control? right = null)
    {
        if (right is null) return UI.Text(title, "Title/18", "text/primary");
        var dock = new DockPanel { Height = 28 };
        if (right != null) { SetDock(right, Dock.Right); dock.Children.Add(right); }
        dock.Children.Add(UI.Text(title, "Title/18", "text/primary"));
        return dock;
    }

    async Task OpenFolderAsync()
    {
        var folder = await _launcher.PickFolder("Open Project");
        if (folder is null) return;
        var file = Directory.Exists(folder) ? Directory.EnumerateFiles(folder, "*.agbproj").FirstOrDefault() : null;
        if (file is null) { _s.Toasts.Show($"No .agbproj file in {folder}.", ToastStatus.Error); return; }
        await _launcher.OpenAsync(file, "LauncherOpen");
    }

    void OnKey(object? sender, KeyEventArgs e)
    {
        if (e.Handled || Tab != "T-P12" || TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is TextBox) return;
        if (e.Key == Key.Return && e.KeyModifiers == KeyModifiers.None && _launcher.Selected != null) { _ = _launcher.OpenSelectedAsync("Enter"); e.Handled = true; }
        else if (e.Key == Key.F2 && e.KeyModifiers == KeyModifiers.None && _launcher.Selected is { } r) { BeginRename(r); e.Handled = true; }
    }

    // ---- Projects (T-P12) ----------------------------------------------------------------------------

    Control ProjectsMain()
    {
        var all = Bare(UI.Row(6, new Icon { Glyph = "right", Size = 12 }.Token(Icon.ForegroundProperty, "text/secondary"), UI.Text("All templates", "Body/12", "text/secondary")), "AllTemplates");
        all.HorizontalAlignment = HorizontalAlignment.Right;
        all.VerticalAlignment = VerticalAlignment.Center;
        all.Margin = new Thickness(0, 0, 9, 0);
        all.Click += (_, _) => _ = _s.Windows.Open("T-P20", source: "AllTemplates");
        var templates = UI.Row(16);
        templates.Name = "HomeTemplates";
        foreach (var t in TemplateCatalog.All.Take(3))
        {
            var card = new TemplateCard { Title = t.Title, Description = t.HomeDescription, Preview = t.Preview, VerticalAlignment = VerticalAlignment.Center, Name = "Template_" + t.Id };
            var id = t.Id;
            card.Activated += (_, _) => _ = _s.Windows.Open("T-D01", id, source: "LauncherTemplate");
            templates.Children.Add(card);
        }
        var col = new StackPanel { Margin = new Thickness(32, 32, 32, 32) };
        col.Children.Add(Header("Recent projects", _search));
        _cards.Margin = new Thickness(0, 18, 0, 0);
        col.Children.Add(_cards);
        var h2 = Header("Start from a template", all);
        h2.Margin = new Thickness(0, 17, 0, 17);
        col.Children.Add(h2);
        col.Children.Add(templates);
        return col;
    }

    void FillProjects()
    {
        var cards = _launcher.Cards(_search.Text);
        if (_launcher.Selected is null && cards.Count > 0 && string.IsNullOrEmpty(_search.Text)) _launcher.Selected = cards[0].Recent;
        _cards.Children.Clear();
        StackPanel? line = null;
        foreach (var c in cards)
        {
            if (line is null || line.Children.Count == 4) _cards.Children.Add(line = UI.Row(16));
            var card = new SceneCard
            {
                NameText = c.Name, Meta = c.Ago, Preview = c.Thumbnail, Name = "Project_" + c.Name,
                IsSelected = _launcher.Selected is { } sel && string.Equals(sel.Path, c.Path, StringComparison.OrdinalIgnoreCase), Tag = c,
            };
            ToolTip.SetTip(card, c.Path);
            WrapName(card);
            var recent = c.Recent;
            card.Activated += (_, e) =>
            {
                if (_launcher.Selected?.Path != recent.Path) _launcher.Selected = recent;
                if (e.ClickCount >= 2) _ = _launcher.OpenAsync(recent.Path, "ProjectCard");
            };
            card.AddHandler(PointerPressedEvent, (_, e) =>
            {
                if (!e.GetCurrentPoint(card).Properties.IsRightButtonPressed) return;
                _launcher.Selected = recent;
                _ = _s.Windows.Open("T-M17", anchor: FindCard(recent.Path) ?? card, source: "ProjectCard");
                e.Handled = true;
            }, RoutingStrategies.Tunnel);
            var box = new TextBox { Height = 26, Margin = new Thickness(6, 110, 6, 0), VerticalAlignment = VerticalAlignment.Top, IsVisible = false, Name = "RenameBox", Padding = new Thickness(6, 0) };
            box.AddHandler(KeyDownEvent, (_, e) =>
            {
                if (e.Key == Key.Return) { box.IsVisible = false; _launcher.Rename(recent, box.Text ?? ""); e.Handled = true; }
                else if (e.Key == Key.Escape) { box.IsVisible = false; e.Handled = true; }
            }, RoutingStrategies.Tunnel);
            box.LostFocus += (_, _) => box.IsVisible = false;
            line.Children.Add(new Panel { Children = { card, box }, Tag = c.Path, VerticalAlignment = VerticalAlignment.Top });
        }
    }

    /// <summary>The visible project cards (tests).</summary>
    public IReadOnlyList<SceneCard> ProjectCards => CardPanels.SelectMany(p => p.Children.OfType<SceneCard>()).ToList();

    IEnumerable<Panel> CardPanels => _cards.Children.OfType<StackPanel>().SelectMany(r => r.Children.OfType<Panel>());

    SceneCard? FindCard(string path) => CardPanels.Where(p => string.Equals(p.Tag as string, path, StringComparison.OrdinalIgnoreCase))
        .SelectMany(p => p.Children.OfType<SceneCard>()).FirstOrDefault();

    void UpdateSelection()
    {
        foreach (var card in ProjectCards)
            card.IsSelected = _launcher.Selected is { } sel && card.Tag is ProjectCard c && string.Equals(sel.Path, c.Path, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Rename (F2): an editor over the card's name; Enter renames, Esc keeps the name.</summary>
    public void BeginRename(RecentProject r)
    {
        if (Tab != "T-P12" || !IsAttachedToVisualTree()) return;
        var panel = CardPanels.FirstOrDefault(p => string.Equals(p.Tag as string, r.Path, StringComparison.OrdinalIgnoreCase));
        if (panel?.Children.OfType<TextBox>().FirstOrDefault() is not { } box) return;
        box.Text = panel.Children.OfType<SceneCard>().First().NameText;
        box.IsVisible = true;
        box.Focus();
        box.SelectAll();
    }

    bool IsAttachedToVisualTree() => TopLevel.GetTopLevel(this) != null;

    Control HomeSide()
    {
        var col = new StackPanel { Margin = new Thickness(20, 20, 20, 20) };
        col.Children.Add(UI.Text("Learn", "Title/14", "text/primary"));
        _homeLearn.Margin = new Thickness(0, 12, 0, 0);
        col.Children.Add(_homeLearn);
        var news = UI.Text("What's new", "Title/14", "text/primary");
        news.Margin = new Thickness(0, 12, 0, 11);
        col.Children.Add(news);
        var bullets = UI.Column(0);
        bullets.Name = "WhatsNew";
        foreach (var line in ReleaseNotes.WhatsNew(_launcher.ReleaseNotesFolder)) bullets.Children.Add(UI.Text("• " + line, "Label/11", "text/secondary"));
        col.Children.Add(bullets);
        return col;
    }

    void FillHomeLearn()
    {
        _homeLearn.Children.Clear();
        foreach (var t in TutorialCatalog.All.Take(4))
        {
            var tile = UI.Frame(new Icon { Glyph = t.Glyph, Size = 15, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }.Token(Icon.ForegroundProperty, "accent/default"), "bg/hover", radius: 7);
            tile.Width = tile.Height = 34;
            var text = UI.Column(2, UI.Text(t.Title, "Body/12 Semibold", "text/primary"), UI.Text(t.HomeSubtitle, "Label/11", "text/muted"));
            text.VerticalAlignment = VerticalAlignment.Center;
            var row = UI.Frame(UI.Row(12, tile, text), "bg/raised", pad: new Thickness(12, 0), radius: 8);
            row.Height = 58;
            var b = Bare(row, "Learn_" + t.Id);
            var info = t;
            b.Click += (_, _) => _launcher.TutorialHost.Show(info, "LauncherLearn");
            _homeLearn.Children.Add(b);
        }
    }

    // ---- Templates (T-P20) ---------------------------------------------------------------------------

    Control TemplatesMain()
    {
        foreach (var (cat, label) in TemplateCatalog.Filters)
        {
            var chip = new Chip { Content = label, Name = "Filter_" + (cat?.ToString() ?? "All"), Tag = cat };
            var c = cat;
            chip.Click += (_, _) => { _category = c; Fill(); };
            _filters.Children.Add(chip);
        }
        var col = new StackPanel { Margin = new Thickness(32, 32, 32, 32) };
        col.Children.Add(Header("Templates", _search));
        _filters.Margin = new Thickness(0, 18, 0, 0);
        col.Children.Add(_filters);
        _templateRows.Margin = new Thickness(0, 18, 0, 0);
        col.Children.Add(_templateRows);
        return col;
    }

    void FillTemplates()
    {
        foreach (var chip in _filters.Children.OfType<Chip>()) chip.Style = Equals(chip.Tag, _category) ? ChipStyle.Accent : ChipStyle.Default;
        var list = TemplateCatalog.Filter(_category, _search.Text).ToList();
        if (list.Count > 0 && list.All(t => t.Id != _templateId)) _templateId = list[0].Id;
        _templateRows.Children.Clear();
        for (var i = 0; i < list.Count; i += 3)
        {
            var row = UI.Row(16);
            foreach (var t in list.Skip(i).Take(3))
            {
                var card = new TemplateCard { Title = t.Title, Description = t.Description, Preview = t.Preview, IsSelected = t.Id == _templateId, VerticalAlignment = VerticalAlignment.Center, Name = "Template_" + t.Id };
                var id = t.Id;
                card.Activated += (_, e) =>
                {
                    _templateId = id;
                    if (e.ClickCount >= 2) _ = _s.Windows.Open("T-D01", id, source: "TemplateCard");
                    else Fill();
                };
                row.Children.Add(card);
            }
            _templateRows.Children.Add(row);
        }
        _side.Content = TemplateDetails();
    }

    /// <summary>The template cards shown (tests).</summary>
    public IReadOnlyList<TemplateCard> TemplateCards => _templateRows.Children.OfType<StackPanel>().SelectMany(r => r.Children.OfType<TemplateCard>()).ToList();

    Control TemplateDetails()
    {
        var t = TemplateCatalog.Find(_templateId);
        var col = new StackPanel { Margin = new Thickness(20, 20, 20, 20), Name = "TemplateDetails" };
        col.Children.Add(UI.Text(t.Title, "Title/14", "text/primary").Named("TemplateTitle"));
        var preview = new Border { Width = 279, Height = 187, CornerRadius = new CornerRadius(8), ClipToBounds = true, Margin = new Thickness(0, 12, 0, 0), Child = new ArtImage { Source = t.Preview, Width = 279, Height = 186 } };
        col.Children.Add(preview);
        var details = UI.Text(t.Details, "Body/12", "text/secondary");
        details.TextWrapping = TextWrapping.Wrap;
        details.Margin = new Thickness(0, 13, 0, 0);
        col.Children.Add(details);
        var inc = UI.Text("Includes", "Overline/11", "text/secondary");
        inc.Margin = new Thickness(0, 11, 0, 12);
        col.Children.Add(inc);
        var list = UI.Column(12);
        list.Name = "TemplateIncludes";
        foreach (var i in t.Includes)
            list.Children.Add(UI.Row(8, new Icon { Glyph = "check", Size = 14, VerticalAlignment = VerticalAlignment.Center }.Token(Icon.ForegroundProperty, "status/success"), UI.Text(i, "Body/12", "text/primary")));
        col.Children.Add(list);
        var stats = UI.Text(t.Stats, "Label/11", "text/muted");
        stats.Margin = new Thickness(0, 13, 0, 11);
        col.Children.Add(stats);
        var use = new AgbButton { Content = "Use this template", Glyph = "plus", Kind = ButtonKind.Primary, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Name = "UseTemplate" };
        var id = t.Id;
        use.Click += (_, _) => _ = _s.Windows.Open("T-D01", id, source: "UseThisTemplate");
        col.Children.Add(use);
        return col;
    }

    public string SelectedTemplate => _templateId;

    // ---- Learn (T-P21) -------------------------------------------------------------------------------

    Control LearnMain()
    {
        var col = new StackPanel { Margin = new Thickness(32, 32, 32, 32) };
        col.Children.Add(Header("Learn"));
        var tut = UI.Text("Tutorials", "Overline/11", "text/secondary");
        tut.Margin = new Thickness(0, 18, 0, 18);
        col.Children.Add(tut);
        col.Children.Add(_tutorialRows);
        var samples = UI.Text("Sample projects", "Overline/11", "text/secondary");
        samples.Margin = new Thickness(0, 18, 0, 18);
        col.Children.Add(samples);
        var cards = UI.Row(16);
        cards.Name = "SampleCards";
        foreach (var sm in SampleCatalog.All)
        {
            var card = new SceneCard { NameText = sm.Name, Meta = sm.Meta, Preview = sm.Preview, Name = "Sample_" + sm.Name.Replace(' ', '_'), VerticalAlignment = VerticalAlignment.Top };
            WrapName(card);
            var info = sm;
            card.Activated += (_, _) => _ = _launcher.OpenSampleAsync(info);
            cards.Children.Add(card);
        }
        col.Children.Add(cards);
        return col;
    }

    void FillLearn()
    {
        _tutorialRows.Children.Clear();
        foreach (var t in TutorialCatalog.All)
        {
            var (text, done, button) = _launcher.Tutorials.Row(t);
            var tile = UI.Frame(new Icon { Glyph = t.Glyph, Size = 16, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }.Token(Icon.ForegroundProperty, "accent/default"), "bg/raised", radius: 8);
            tile.Width = tile.Height = 40;
            var title = UI.Column(2, UI.Text(t.Title, "Body/12 Semibold", "text/primary"), UI.Text(t.Subtitle, "Label/11", "text/muted"));
            title.VerticalAlignment = VerticalAlignment.Center;
            var started = _launcher.Tutorials.Get(t.Id).Started;
            var bar = new AgbProgress { Width = 180, Value = done ? 100 : started ? _launcher.Tutorials.Percent(t) : 0, Status = done ? ProgressStatus.OK : ProgressStatus.Accent };
            var progress = UI.Column(6, UI.Text(text, "Label/11", done ? "status/success" : "text/secondary"), bar);
            progress.VerticalAlignment = VerticalAlignment.Center;
            progress.Width = 180;
            var go = new AgbButton { Content = button, Kind = button == "Continue" ? ButtonKind.Primary : ButtonKind.Secondary, VerticalAlignment = VerticalAlignment.Center, Name = "Tutorial_" + t.Id };
            var info = t;
            go.Click += (_, _) => _launcher.TutorialHost.Show(info, "Learn_" + button);
            var dock = new DockPanel { HorizontalSpacing = 14 };
            SetDock(tile, Dock.Left);
            SetDock(go, Dock.Right);
            SetDock(progress, Dock.Right);
            dock.Children.AddRange([tile, go, progress, title]);
            var row = UI.Frame(dock, "bg/panel", "border/default", pad: new Thickness(14, 0), radius: 10);
            row.Height = 70;
            row.Name = "TutorialRow_" + t.Id;
            _tutorialRows.Children.Add(row);
        }
    }

    Control LearnSide()
    {
        var col = new StackPanel { Margin = new Thickness(20, 20, 20, 20) };
        col.Children.Add(UI.Text("Documentation", "Title/14", "text/primary"));
        var docs = UI.Column(12);
        docs.Margin = new Thickness(0, 12, 0, 0);
        foreach (var (title, sub, glyph, run) in new (string, string, string, Action)[]
                 {
                     ("Scripting API", "every class and method, with examples", "book", () => _s.Commands.Execute("help.scriptingApi")),
                     ("GBA hardware guide", "VRAM, OAM, palettes, sound channels", "chip", OpenHardwareGuide),
                     ("Keyboard shortcuts", "all shortcuts in one list", "type", () => _s.Commands.Execute("help.keyboardShortcuts")),
                 })
        {
            var text = UI.Column(2, UI.Text(title, "Body/12 Semibold", "text/primary"), UI.Text(sub, "Label/11", "text/muted"));
            text.VerticalAlignment = VerticalAlignment.Center;
            var row = UI.Frame(UI.Row(10, new Icon { Glyph = glyph, Size = 16, VerticalAlignment = VerticalAlignment.Center }.Token(Icon.ForegroundProperty, "accent/default"), text), "bg/raised", pad: new Thickness(14, 0), radius: 8);
            row.Height = 56;
            var b = Bare(row, "Doc_" + title.Replace(' ', '_'));
            b.Click += (_, _) => run();
            docs.Children.Add(b);
        }
        col.Children.Add(docs);
        var community = UI.Text("Community", "Title/14", "text/primary");
        community.Margin = new Thickness(0, 12, 0, 6);
        col.Children.Add(community);
        foreach (var (label, glyph, run) in new (string, string, Action)[] { ("Release notes", "file", OpenReleaseNotes), ("Report a bug", "bug", () => _s.Commands.Execute("help.reportBug")) })
        {
            var link = Bare(UI.Row(10, new Icon { Glyph = glyph, Size = 13, VerticalAlignment = VerticalAlignment.Center }.Token(Icon.ForegroundProperty, "text/muted"), UI.Text(label, "Body/12", "text/secondary")), "Community_" + label.Replace(' ', '_'));
            link.Height = 28;
            link.Padding = new Thickness(2, 0);
            link.Click += (_, _) => run();
            col.Children.Add(link);
        }
        return col;
    }

    void OpenReleaseNotes()
    {
        var path = Path.Combine(_launcher.DataFolder, ReleaseNotes.FileName);
        Directory.CreateDirectory(_launcher.DataFolder);
        File.WriteAllText(path, ReleaseNotes.Text(_launcher.ReleaseNotesFolder));
        _launcher.OpenDocument(path);
    }

    void OpenHardwareGuide()
    {
        var guide = new[] { AppContext.BaseDirectory, Path.Combine(AppContext.BaseDirectory, "docs") }.Select(d => Path.Combine(d, "gba-hardware-guide.md")).FirstOrDefault(File.Exists);
        if (guide is null) _s.Toasts.Show("The GBA hardware guide is not installed.", ToastStatus.Error);
        else _launcher.OpenDocument(guide);
    }
}
