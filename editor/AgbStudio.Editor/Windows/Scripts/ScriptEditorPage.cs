using System.Diagnostics;
using System.Globalization;
using AgbStudio.Editor.Commands;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Graphics;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using AgbStudio.Scripting;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace AgbStudio.Editor.Windows.Scripts;

/// <summary>Opens a script at a position (Problems, Console, Find &amp; Replace, Outline).</summary>
public sealed record ScriptLocation(string Path, int Line, int Column = 1);

/// <summary>
/// Script Editor (T-P04, 1440 x 900; P11.52-P11.55): Scripts list and Outline, Doc Tabs, breadcrumb to the member at the
/// caret, the built-in code editor with live GAE analysis and the diagnostic popup with quick fixes, the Script side panel
/// (attached to, compiles to, exposed fields, API quick reference, Open Scripting API), Problems / Terminal / Output and
/// the status line. Open externally starts the configured external editor at the caret line.
/// </summary>
[EditorWindow("T-P04")]
public sealed class ScriptEditorPage : DockPanel
{
    readonly EditorServices _s;
    readonly ScriptWorkspace _ws;
    readonly StackPanel _scriptList = new() { Spacing = 1, Margin = new Thickness(6, 4, 6, 4), Name = "ScriptList" };
    readonly StackPanel _outline = new() { Spacing = 1, Margin = new Thickness(6, 4, 6, 4), Name = "Outline" };
    readonly StackPanel _docTabs = new() { Orientation = Orientation.Horizontal, Name = "DocTabs" };
    readonly StackPanel _breadcrumb = new() { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center, Name = "Breadcrumb" };
    readonly StackPanel _side = new() { Name = "SidePanel" };
    readonly StackPanel _problems = new() { Spacing = 2, Margin = new Thickness(12, 8, 12, 8), Name = "Problems" };
    readonly StackPanel _output = new() { Spacing = 2, Margin = new Thickness(12, 8, 12, 8), Name = "Output" };
    readonly StackPanel _terminal = new() { Spacing = 2, Margin = new Thickness(12, 8, 12, 4), Name = "TerminalLines" };
    readonly ContentControl _bottomContent = new();
    readonly PanelTab[] _bottomTabs;
    readonly StatusItem _statusProblems = new() { Glyph = "warn", Name = "Status_Problems" };
    readonly StatusItem _statusCaret = new() { Glyph = "code", Name = "Status_Caret" };
    readonly StatusItem _statusLanguage = new() { Glyph = "code", Name = "Status_Language" };
    readonly StatusItem _statusToolchain = new() { Glyph = "chip", Name = "Status_Toolchain" };
    readonly Border _popupHost;
    DispatcherTimer? _analysisTimer;
    CancellationTokenSource? _analysisCancel;
    bool _loading;

    public CodeEditor Editor { get; } = new() { Name = "CodeEditor" };
    public ScriptBuffer? Buffer { get; private set; }
    public TextBox TerminalInput { get; } = new() { Name = "TerminalInput", Watermark = "Run a command in the project folder…" };
    public DiagnosticPopup? Popup { get; private set; }
    /// <summary>The running background analysis (tests await it).</summary>
    public Task AnalysisTask { get; private set; } = Task.CompletedTask;
    public ScriptWorkspace Workspace => _ws;
    public IReadOnlyList<PanelTab> BottomTabs => _bottomTabs;

    public ScriptEditorPage(WindowContext ctx) : this(ctx.Services, ctx.Args) { }

    public ScriptEditorPage(EditorServices s, object? args = null)
    {
        _s = s;
        _ws = ScriptWorkspace.Of(s);
        Name = "ScriptEditorPage";
        LastChildFill = true;
        Editor.Undo = s.Undo;
        ApplySettings();
        Editor.CompletionProvider = (text, pos, cancel) => Buffer is { } b ? _ws.Analysis.Complete(b.Path, text, pos, cancel) : [];

        // ---- toolbar ----
        var find = new IconButton { Glyph = "search", Name = "Tool_Find" };
        find.Click += (_, _) => OpenFind(replace: false);
        PageChrome.Tip(s, find, "script.find");
        var code = new IconButton { Glyph = "code", IsActive = true, Name = "Tool_Code" };
        var debug = new IconButton { Glyph = "bug", Command = s.Commands.Get("tools.debugger"), Name = "Tool_Debug" };
        var analyzer = new Chip { Content = "Analyzer: GBA rules on", Glyph = "check", Style = ChipStyle.Success, Name = "AnalyzerChip" };
        analyzer.Click += (_, _) => _ = s.Windows.Open("T-S04", source: "AnalyzerChip");
        var external = new AgbButton { Content = "Open externally", Glyph = "expand", Kind = ButtonKind.Secondary, Name = "OpenExternally" };
        external.Click += (_, _) => OpenExternally();
        var pipeline = new Chip { Content = "C# → C++ → devkitARM", Glyph = "chip", Name = "PipelineChip" };
        pipeline.Click += (_, _) => _ = s.Windows.Open("T-P11", source: "PipelineChip");
        Children.Add(PageChrome.Toolbar([PageChrome.ToolGroup(find, code, debug), analyzer, external], PageChrome.PlayGroup(s), [pipeline, PageChrome.UndoRedo(s)]));

        // ---- status bar ----
        _statusProblems.Click += (_, _) => SelectBottom(_bottomTabs![0]);
        Children.Add(PageChrome.StatusBar([_statusProblems, _statusCaret], [_statusLanguage, _statusToolchain]));

        // ---- left: Scripts + Outline ----
        var scriptsHeader = new PanelHeader { Title = "Scripts", Glyph = "code", ActionGlyph = "plus", Name = "ScriptsHeader" };
        scriptsHeader.ActionCommand = new Relay(() => _ = s.Windows.Open("T-D14", source: "ScriptsPlus"));
        var outlineHeader = new PanelHeader { Title = "Outline", Glyph = "nodes", Name = "OutlineHeader" };
        var left = new DockPanel();
        PageChrome.DockTo(left, scriptsHeader, Avalonia.Controls.Dock.Top);
        // Figma T-P04 draws no scrollbar: the list scrolls with the wheel
        var scriptsScroll = new ScrollViewer { Content = _scriptList, Height = 195, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden };
        PageChrome.DockTo(left, scriptsScroll, Avalonia.Controls.Dock.Top);
        PageChrome.DockTo(left, outlineHeader, Avalonia.Controls.Dock.Top);
        left.Children.Add(new ScrollViewer { Content = _outline });

        // ---- centre: doc tabs, breadcrumb, editor, bottom panel ----
        var tabs = UI.Frame(_docTabs, "bg/panel", "border/default", new Thickness(0, 0, 0, 1)).Named("DocTabStrip");
        tabs.Height = 34;
        var crumbRow = new Border { Height = 26, Padding = new Thickness(16, 0), Child = _breadcrumb };
        crumbRow.Token(Border.BackgroundProperty, "bg/base");
        _popupHost = new Border();
        // the code area takes Preferences › Editor › Theme (Studio Dark / Studio Light) whatever the app theme is
        _codeTheme.Child = Editor;
        var editorArea = new Panel { Children = { _codeTheme } };
        _bottomTabs =
        [
            new PanelTab { Label = "Problems", Glyph = "warn", Name = "Tab_Problems" },
            new PanelTab { Label = "Terminal", Glyph = "code", Name = "Tab_Terminal" },
            new PanelTab { Label = "Output", Glyph = "file", Name = "Tab_Output" },
        ];
        foreach (var t in _bottomTabs) t.Activated += (_, _) => SelectBottom(t);
        var tabRow = new Border { Height = 33, Padding = new Thickness(6, 0), BorderThickness = new Thickness(0, 0, 0, 1), Child = UI.Row(2, _bottomTabs) }.Token(Border.BorderBrushProperty, "border/default");
        var bottom = new DockPanel();
        PageChrome.DockTo(bottom, tabRow, Avalonia.Controls.Dock.Top);
        bottom.Children.Add(_bottomContent);
        var bottomPanel = UI.Frame(bottom, "bg/panel", "border/default", new Thickness(0, 1, 0, 0)).Named("BottomPanel");
        var center = new Grid { RowDefinitions = RowDefinitions.Parse("34,26,*,190"), Name = "Center" };
        center.Children.Add(tabs);
        Grid.SetRow(crumbRow, 1);
        center.Children.Add(crumbRow);
        Grid.SetRow(editorArea, 2);
        center.Children.Add(editorArea);
        Grid.SetRow(bottomPanel, 3);
        center.Children.Add(bottomPanel);

        // ---- right: Script side panel ----
        var sideHeader = new PanelHeader { Title = "Script", Glyph = "sliders", Name = "ScriptHeader" };
        var right = new DockPanel();
        PageChrome.DockTo(right, sideHeader, Avalonia.Controls.Dock.Top);
        right.Children.Add(new ScrollViewer { Content = _side });

        var body = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("260,*,320"), Name = "Body" };
        body.Children.Add(PageChrome.SidePanel(left, true, "LeftPanel"));
        Grid.SetColumn(center, 1);
        body.Children.Add(center);
        var rightPanel = PageChrome.SidePanel(right, false, "RightPanel");
        Grid.SetColumn(rightPanel, 2);
        body.Children.Add(rightPanel);
        Children.Add(body);

        // ---- behaviour ----
        Editor.TextChanged += OnTextChanged;
        Editor.CaretMoved += UpdateCaret;
        // Project Settings › Scripting › "Show inline hints in the editor": the analyzer hint opens next to the code
        Editor.IssueActivated += issue => { if (InlineHints) _ = ShowPopupAsync(issue); };
        // breakpoints (Debugger, P13.06): click a line number or press F9
        Editor.GutterClicked += line => ToggleBreakpoint(line);
        Editor.HasBreakpoint = line => Buffer is { } b && s.GetService(typeof(Debugger.DebuggerModel)) is Debugger.DebuggerModel dm && dm.HasBreakpoint(b.Path, line);
        TerminalInput.Theme = (Avalonia.Styling.ControlTheme?)Application.Current?.FindResource("BareTextBox");
        AgbStudio.Editor.Theme.Fig.SetStyle(TerminalInput, "Mono/11");
        TerminalInput.KeyDown += (_, e) => { if (e.Key == Key.Enter) { RunTerminal(TerminalInput.Text ?? ""); TerminalInput.Text = ""; e.Handled = true; } };
        AttachedToVisualTree += (_, _) =>
        {
            _ws.Changed += WorkspaceChanged; _ws.Reloaded += BufferReloaded; _ws.Formatted += BufferFormatted; _ws.Settings.Changed += ApplySettings;
            s.Documents.Changed += RebuildTabs; s.Toolchain.Changed += ToolchainChanged;
            ApplySettings();
        };
        DetachedFromVisualTree += (_, _) =>
        {
            _ws.Changed -= WorkspaceChanged; _ws.Reloaded -= BufferReloaded; _ws.Formatted -= BufferFormatted; _ws.Settings.Changed -= ApplySettings;
            s.Documents.Changed -= RebuildTabs; s.Toolchain.Changed -= ToolchainChanged;
        };

        SelectBottom(_bottomTabs[0]);
        OpenFromArgs(args);
        RebuildTabs();
        RebuildScripts();
        RefreshPanels();
    }

    /// <summary>Preferences · Editor › Built-in code editor: tab size, autocomplete as you type, underline analyzer warnings.</summary>
    void ApplySettings()
    {
        var settings = _ws.Settings;
        Editor.TabSize = settings.TabSize;
        Editor.AutocompleteAsYouType = settings.AutocompleteAsYouType;
        Editor.UnderlineIssues = settings.UnderlineAnalyzerWarnings;
        // Preferences › Appearance › Code font size and Editor › Font / Theme
        var size = _s.Preferences.Current.CodeFontSize;
        Editor.FontSizeOverride = size is >= 8 and <= 32 ? size : 0;
        Editor.FontFamilyOverride = CodeFontFamily(settings.Font);
        _codeTheme.RequestedThemeVariant = CodeThemeVariant(settings.Theme);
    }

    readonly Avalonia.Controls.ThemeVariantScope _codeTheme = new() { Name = "CodeThemeScope" };

    /// <summary>The code font: the bundled JetBrains Mono, or an installed font of that name (Thai falls back to the bundled face).</summary>
    public static Avalonia.Media.FontFamily? CodeFontFamily(string name) =>
        string.IsNullOrWhiteSpace(name) || name == "JetBrains Mono" ? AgbStudio.Editor.Theme.Fonts.Mono : new Avalonia.Media.FontFamily(name + ", " + AgbStudio.Editor.Theme.Fonts.Mono);

    /// <summary>"Studio Light" draws the code with the light tokens, "Studio Dark" with the dark ones.</summary>
    public static Avalonia.Styling.ThemeVariant CodeThemeVariant(string theme) =>
        theme == "Studio Light" ? Avalonia.Styling.ThemeVariant.Light : Avalonia.Styling.ThemeVariant.Dark;

    // the toolchain check runs off the UI thread
    void ToolchainChanged() => Dispatcher.UIThread.Post(UpdateStatus);

    // ---- opening -------------------------------------------------------------------------------------------

    void OpenFromArgs(object? args)
    {
        string? path = null;
        ScriptLocation? at = null;
        switch (args)
        {
            case ScriptLocation l: at = l; path = _ws.Resolve(l.Path); break;
            case EditorDocument d when d.Path != null: path = _ws.Resolve(d.Path) ?? d.Path; break;
            case string str: path = _ws.Resolve(str); break;
        }
        path ??= _ws.Current?.Path;
        if (path is null && _s.Documents.Active is { Path: { } ap } && ap.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && File.Exists(ap)) path = ap;
        path ??= _ws.ScriptFiles().FirstOrDefault();
        if (path is null || !File.Exists(path)) return;
        Open(path);
        if (at != null) Editor.MoveCaret(at.Line, at.Column);
    }

    /// <summary>Shows a script in the editor (from the Scripts list, a Doc Tab, Problems or Find).</summary>
    public void Open(string path)
    {
        if (Buffer != null) Buffer.CaretOffset = Editor.CaretOffset;
        var b = _ws.Open(path);
        Buffer = b;
        _loading = true;
        Editor.IsReadOnly = b.IsGenerated;
        Editor.Text = b.Text;
        Editor.Spans = b.Analysis.Spans;
        Editor.SetIssues(b.Analysis.Issues);
        Editor.Select(b.CaretOffset, b.CaretOffset);
        _loading = false;
        ClosePopup();
        RebuildScripts();
        RebuildTabs();
        RefreshPanels();
        ScheduleAnalysis(immediate: true);
    }

    void BufferReloaded(ScriptBuffer b)
    {
        if (b != Buffer) return;
        var caret = Editor.CaretOffset;
        _loading = true;
        Editor.Text = b.Text;
        Editor.Select(Math.Min(caret, b.Text.Length), Math.Min(caret, b.Text.Length));
        _loading = false;
        ScheduleAnalysis(immediate: true);
    }

    void BufferFormatted(ScriptBuffer b, string formatted)
    {
        if (b != Buffer) return;
        _loading = true;
        Editor.ReplaceAll(formatted, "Format document");
        _loading = false;
    }

    void OnTextChanged()
    {
        if (_loading || Buffer is null) return;
        _ws.SetText(Buffer, Editor.Text);
        ClosePopup();
        ScheduleAnalysis(immediate: false);
        RebuildScripts();
    }

    // ---- analysis ------------------------------------------------------------------------------------------

    void ScheduleAnalysis(bool immediate)
    {
        _analysisTimer?.Stop();
        if (immediate) { AnalysisTask = RunAnalysisAsync(); return; }
        _analysisTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(350), DispatcherPriority.Background, (_, _) => { _analysisTimer?.Stop(); AnalysisTask = RunAnalysisAsync(); });
        _analysisTimer.Start();
    }

    async Task RunAnalysisAsync()
    {
        if (Buffer is not { } b) return;
        _analysisCancel?.Cancel();
        var cts = _analysisCancel = new CancellationTokenSource();
        var r = await _ws.AnalyzeAsync(b, cts.Token);
        if (r is null || b != Buffer || cts.IsCancellationRequested) return;
        Apply(r);
    }

    /// <summary>Analyzes the current script now (tests and SHOT).</summary>
    public void AnalyzeNow()
    {
        if (Buffer is not { } b) return;
        _analysisTimer?.Stop();
        Apply(_ws.Analyze(b));
    }

    void Apply(AnalysisResult r)
    {
        Editor.Spans = r.Spans;
        Editor.SetIssues(r.Issues);
        RefreshPanels();
    }

    // ---- panels --------------------------------------------------------------------------------------------

    void WorkspaceChanged() => Dispatcher.UIThread.Post(() => { RebuildBottom(); UpdateStatus(); RebuildScripts(); });

    void RefreshPanels()
    {
        RebuildOutline();
        RebuildSide();
        RebuildBottom();
        UpdateCaret();
        UpdateStatus();
    }

    void RebuildScripts()
    {
        _scriptList.Children.Clear();
        foreach (var f in _ws.ScriptFiles())
        {
            var path = f;
            var open = _ws.Buffers.FirstOrDefault(x => string.Equals(x.Path, Path.GetFullPath(f), StringComparison.OrdinalIgnoreCase));
            var item = new TreeItem
            {
                Label = Path.GetFileName(f), Glyph = "code", Meta = open?.IsDirty == true ? "M" : null, Name = "Script_" + Path.GetFileNameWithoutExtension(f),
                IsSelected = Buffer != null && string.Equals(Buffer.Path, Path.GetFullPath(f), StringComparison.OrdinalIgnoreCase),
            };
            if (!item.IsSelected) item.Token(TreeItem.ForegroundProperty, "kind/script");
            item.Activated += (_, _) => Open(path);
            _scriptList.Children.Add(item);
        }
    }

    void RebuildOutline()
    {
        _outline.Children.Clear();
        if (Buffer is null) return;
        foreach (var o in Buffer.Analysis.Outline)
        {
            var item = new TreeItem
            {
                Label = o.Name, Meta = o.Meta, Depth = o.Kind == OutlineKind.Class ? 0 : 1,
                Glyph = o.Kind switch { OutlineKind.Class => "cube", OutlineKind.Method => "play", _ => o.HasIssue ? "warn" : "sliders" },
                Name = "Outline_" + o.Name.Split('(')[0],
            };
            var target = o;
            item.Activated += (_, _) => { Editor.Select(target.Start, target.Start); Editor.Focus(); };
            _outline.Children.Add(item);
        }
    }

    void RebuildTabs()
    {
        _docTabs.Children.Clear();
        foreach (var d in _s.Documents.Open)
        {
            var tab = new DocTab { Label = d.Name, Glyph = d.Glyph, IsDirty = d.IsDirty, IsSelected = d == _s.Documents.Active, Height = 33, Name = "Doc_" + d.Name };
            var doc = d;
            tab.Activated += (_, _) => ActivateDocument(doc);
            _docTabs.Children.Add(tab);
        }
    }

    void ActivateDocument(EditorDocument d)
    {
        if (d.Path != null && d.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && File.Exists(d.Path)) { Open(d.Path); return; }
        _s.Documents.Active = d;
        // the same routes as the Scene Editor Doc Tabs: Game, .map, .db, the scene
        var target = d.Name == "Game" ? "T-P02" : d.Name.EndsWith(".map", StringComparison.OrdinalIgnoreCase) ? "T-P03"
            : d.Name.EndsWith(".db", StringComparison.OrdinalIgnoreCase) ? "T-P07" : d.Name.EndsWith(".graph", StringComparison.OrdinalIgnoreCase) ? "T-P17" : "T-P01";
        _ = _s.Windows.Open(target, target == "T-P01" ? null : d, source: "Doc_" + d.Name);
    }

    void UpdateCaret()
    {
        _statusCaret.Content = $"Ln {Editor.CaretLine}, Col {Editor.CaretColumn}";
        _breadcrumb.Children.Clear();
        if (Buffer is null) return;
        var root = _s.Project.Current?.Root;
        var folder = root is null ? "scripts" : Path.GetRelativePath(root, Path.GetDirectoryName(Buffer.Path)!).Replace('\\', '/');
        var parts = new List<string> { folder, Buffer.Name };
        if (ScriptAnalysis.MemberAt(Editor.Text, Editor.CaretOffset) is { } member) parts.Add(member);
        for (var i = 0; i < parts.Count; i++)
        {
            if (i > 0) _breadcrumb.Children.Add(UI.Text("›", "Label/11", "text/muted"));
            _breadcrumb.Children.Add(UI.Text(parts[i], "Label/11", i == parts.Count - 1 && parts.Count > 2 ? "text/secondary" : "text/muted"));
        }
    }

    /// <summary>Breadcrumb parts ("scripts", "PlayerController.cs", "Update()").</summary>
    public IReadOnlyList<string> BreadcrumbParts => _breadcrumb.Children.OfType<TextBlock>().Select(t => t.Text ?? "").Where(t => t != "›").ToList();

    void UpdateStatus()
    {
        var problems = _ws.Problems();
        var errors = problems.Count(p => p.Issue.Severity == ScriptSeverity.Error);
        var warnings = problems.Count - errors;
        _statusProblems.Content = errors > 0 ? $"{errors} error{(errors == 1 ? "" : "s")}" + (warnings > 0 ? $", {warnings} warning{(warnings == 1 ? "" : "s")}" : "")
            : warnings > 0 ? $"{warnings} warning{(warnings == 1 ? "" : "s")}" : "No problems";
        _statusProblems.Glyph = problems.Count > 0 ? "warn" : "check";
        _statusLanguage.Content = _s.Project.Current?.Settings.Scripting.Language ?? "C# · GBA subset";
        _statusToolchain.Content = _s.Toolchain.Find("devkitARM") is { Found: true } t ? t.Version ?? "devkitARM" : _s.Toolchain.Tools.Count == 0 ? "devkitARM" : "devkitARM not found · set it in Preferences › Paths";
        _bottomTabs[0].Count = problems.Count > 0 ? problems.Count : null;
    }

    // ---- side panel ----------------------------------------------------------------------------------------

    void RebuildSide()
    {
        _side.Children.Clear();
        if (Buffer is null) return;
        var r = Buffer.Analysis;
        var cls = r.ClassName ?? Buffer.ClassName;
        var section = Section(cls, "code", r.ClassKind ?? "", "Section_Class",
            PageChrome.Field("Attached to", FieldKind.Text, _ws.AttachedTo(cls), "AttachedTo"),
            PageChrome.Field("Compiles to", FieldKind.Text, cls + ".cpp", "CompilesTo"));
        foreach (var f in section.Children.OfType<InspectorField>()) f.IsHitTestVisible = false;
        _side.Children.Add(section);
        if (r.Fields.Count > 0)
            _side.Children.Add(Section("Exposed fields", "sliders", "", "Section_Fields", [.. r.Fields.Select(ExposedRow)]));
        var api = new List<Control>();
        foreach (var member in QuickReference)
        {
            if (_ws.Analysis.Describe(member) is not var (sig, returns)) continue;
            var row = new DockPanel { Height = 24, Background = Brushes.Transparent, Name = "Api_" + member, Cursor = new Cursor(StandardCursorType.Hand) };
            var type = UI.Text(returns, "Mono/10", "kind/script");
            DockPanel.SetDock(type, Avalonia.Controls.Dock.Right);
            row.Children.Add(type);
            row.Children.Add(UI.Text(sig, "Mono/11", "text/primary"));
            var target = member;
            row.PointerPressed += (_, e) => { _ = _s.Windows.Open("T-P18", target, source: "QuickReference"); e.Handled = true; };
            api.Add(row);
        }
        var openApi = new AgbButton { Content = "Open Scripting API", Glyph = "book", Kind = ButtonKind.Secondary, Name = "OpenScriptingApi", HorizontalAlignment = HorizontalAlignment.Left };
        openApi.Click += (_, _) => _ = _s.Windows.Open("T-P18", source: "OpenScriptingApi");
        api.Add(openApi);
        _side.Children.Add(Section("API quick reference", "book", "", "Section_Api", [.. api]));
    }

    /// <summary>The members listed under API quick reference (the calls scripts use most).</summary>
    public static readonly IReadOnlyList<string> QuickReference = ["Input.Pressed", "Input.AxisX", "Sound.Play", "Scene.Load", "Save.Write", "Text.Show", "Items.Get"];

    internal static StackPanel Section(string title, string glyph, string meta, string name, params Control[] rows)
    {
        var header = new SectionHeader { Title = title, Glyph = glyph, Meta = meta, Height = 38 };
        var content = UI.Column(6, rows);
        content.Margin = new Thickness(0, 0, 0, 10);
        header.PropertyChanged += (_, e) => { if (e.Property == SectionHeader.IsOpenProperty) content.IsVisible = header.IsOpen; };
        var panel = UI.Column(0, header, content);
        panel.Margin = new Thickness(13, 0);
        var outer = UI.Column(0, panel, PageChrome.Divider());
        outer.Name = name;
        return outer;
    }

    Control ExposedRow(ExposedField f)
    {
        var field = new InspectorField { Label = f.Name, Value = f.Value, Name = "Field_" + f.Name };
        if (f.Min is { } min && f.Max is { } max && max > min && double.TryParse(f.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
        {
            field.Kind = FieldKind.Slider;
            field.SliderValue = Math.Clamp((v - min) / (max - min) * 100, 0, 100);
            IDisposable? commit = null;
            field.PropertyChanged += (_, e) =>
            {
                if (e.Property != InspectorField.SliderValueProperty || _loading) return;
                var value = Math.Round(min + field.SliderValue / 100 * (max - min), f.Type == "int" ? 0 : 1);
                field.Value = value.ToString(CultureInfo.InvariantCulture);
                commit?.Dispose();
                commit = DispatcherTimer.RunOnce(() => SetFieldValue(f.Name, field.Value!), TimeSpan.FromMilliseconds(250));
            };
        }
        else if (f.Type == "bool")
        {
            field.Kind = FieldKind.Toggle;
            PageChrome.Toggle(field, f.Value == "true", on => { if (!_loading) SetFieldValue(f.Name, on ? "true" : "false"); });
        }
        else
        {
            field.Kind = FieldKind.Text;
            field.TemplateApplied += (_, e) =>
            {
                if (e.NameScope.Find<TextBox>("PART_Text") is not { } box) return;
                box.LostFocus += (_, _) => SetFieldValue(f.Name, field.Value ?? "");
                box.KeyDown += (_, ke) => { if (ke.Key == Key.Enter) { SetFieldValue(f.Name, field.Value ?? ""); ke.Handled = true; } };
            };
        }
        return field;
    }

    /// <summary>Writes an exposed field's default value into the script (an undoable edit), e.g. speed = 2.</summary>
    public bool SetFieldValue(string name, string value)
    {
        if (Buffer is null || Editor.IsReadOnly) return false;
        value = value.Trim();
        var r = Buffer.Analysis.Text == Editor.Text ? Buffer.Analysis : _ws.Analyze(Buffer);
        if (r.Fields.FirstOrDefault(x => x.Name == name) is not { } f) return false;
        var ok = f.Type switch
        {
            "int" => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
            "fixed" => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _),
            "bool" => value is "true" or "false",
            _ => value.Length > 0,
        };
        if (!ok || value == f.Value) return false;
        if (f.ValueStart >= 0) Editor.Replace(f.ValueStart, f.ValueLength, value, "Change " + name);
        else
        {
            // no initializer yet: add " = value" after the name
            var decl = Editor.Text.IndexOf(" " + name + ";", StringComparison.Ordinal);
            if (decl < 0) return false;
            Editor.Replace(decl + 1 + name.Length, 0, " = " + value, "Change " + name);
        }
        AnalyzeNow();
        return true;
    }

    // ---- bottom panel --------------------------------------------------------------------------------------

    void SelectBottom(PanelTab tab)
    {
        foreach (var t in _bottomTabs) t.IsSelected = t == tab;
        _bottomContent.Content = tab.Name switch
        {
            "Tab_Problems" => new ScrollViewer { Content = _problems },
            "Tab_Output" => new ScrollViewer { Content = _output },
            _ => TerminalView(),
        };
        RebuildBottom();
    }

    Control TerminalView()
    {
        var prompt = new DockPanel { Margin = new Thickness(12, 0, 12, 8), HorizontalSpacing = 8 };
        var sign = UI.Text(">", "Mono/11", "text/muted");
        DockPanel.SetDock(sign, Avalonia.Controls.Dock.Left);
        prompt.Children.Add(sign);
        prompt.Children.Add(TerminalInput);
        var dock = new DockPanel();
        PageChrome.DockTo(dock, prompt, Avalonia.Controls.Dock.Bottom);
        dock.Children.Add(new ScrollViewer { Content = _terminal });
        return dock;
    }

    void RebuildBottom()
    {
        _problems.Children.Clear();
        foreach (var (b, issue) in _ws.Problems())
        {
            var line = new ConsoleLine { Time = _ws.Clock().ToString("HH:mm:ss", CultureInfo.InvariantCulture), Level = issue.Severity == ScriptSeverity.Error ? ConsoleLevel.Error : ConsoleLevel.Warning, Text = ScriptWorkspace.ProblemText(b, issue), Cursor = new Cursor(StandardCursorType.Hand) };
            if (_ws.LastCheckTime != default) line.Time = _ws.LastCheckTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            var (path, l, c) = (b.Path, issue.Line, issue.Column);
            // the line's text is selectable and takes the press itself: listen to handled presses too
            line.AddHandler(PointerPressedEvent, (_, e) => { GoTo(path, l, c); e.Handled = true; }, RoutingStrategies.Bubble, handledEventsToo: true);
            _problems.Children.Add(line);
        }
        foreach (var l in _ws.CheckLines) _problems.Children.Add(Line(l));
        _output.Children.Clear();
        foreach (var l in _ws.Output) _output.Children.Add(Line(l));
    }

    ConsoleLine Line(ScriptLogLine l)
    {
        var c = new ConsoleLine { Time = l.TimeText, Level = l.Level, Text = l.Text };
        if (l.File != null)
        {
            c.Cursor = new Cursor(StandardCursorType.Hand);
            c.AddHandler(PointerPressedEvent, (_, e) => { GoTo(l.File, l.Line, l.Column); e.Handled = true; }, RoutingStrategies.Bubble, handledEventsToo: true);
        }
        return c;
    }

    /// <summary>Opens a script and puts the caret at a line and column (Problems click).</summary>
    public void GoTo(string path, int line, int column)
    {
        if (Buffer is null || !string.Equals(Buffer.Path, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase)) Open(path);
        Editor.MoveCaret(line, column);
        Editor.Focus();
    }

    /// <summary>Runs a command in the project folder (Terminal tab) and streams its output.</summary>
    public Task RunTerminal(string command)
    {
        command = command.Trim();
        if (command.Length == 0) return Task.CompletedTask;
        var dir = _s.Project.Current?.Root ?? Environment.CurrentDirectory;
        AddTerminal(ConsoleLevel.Info, "> " + command);
        var psi = new ProcessStartInfo("cmd.exe", "/d /c " + command) { WorkingDirectory = dir, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        return Task.Run(() =>
        {
            try
            {
                using var p = Process.Start(psi)!;
                p.OutputDataReceived += (_, e) => { if (e.Data != null) Dispatcher.UIThread.Post(() => AddTerminal(ConsoleLevel.Info, e.Data)); };
                p.ErrorDataReceived += (_, e) => { if (e.Data != null) Dispatcher.UIThread.Post(() => AddTerminal(ConsoleLevel.Error, e.Data)); };
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                p.WaitForExit();
                var code = p.ExitCode;
                Dispatcher.UIThread.Post(() => AddTerminal(code == 0 ? ConsoleLevel.Success : ConsoleLevel.Error, $"exit code {code}"));
            }
            catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                Dispatcher.UIThread.Post(() => AddTerminal(ConsoleLevel.Error, e.Message));
            }
        });
    }

    void AddTerminal(ConsoleLevel level, string text) =>
        _terminal.Children.Add(new ConsoleLine { Time = _ws.Clock().ToString("HH:mm:ss", CultureInfo.InvariantCulture), Level = level, Text = text });

    /// <summary>Lines shown in the Terminal tab.</summary>
    public IEnumerable<string> TerminalText => _terminal.Children.OfType<ConsoleLine>().Select(c => c.Text ?? "");

    // ---- diagnostic popup ----------------------------------------------------------------------------------

    /// <summary>Shows the popup of an analyzer message with its quick fixes ("Change to fixed", "Ignore").</summary>
    /// <summary>Project Settings › Scripting › Show inline hints (on by default): clicking an underlined issue opens its hint.</summary>
    public bool InlineHints => _s.Project.Current?.Settings.Scripting.InlineHints ?? true;

    public async Task ShowPopupAsync(CodeIssue issue)
    {
        ClosePopup();
        if (Buffer is null) return;
        var popup = Popup = new DiagnosticPopup(issue);
        var p = Editor.PointOf(issue.End);
        Canvas.SetLeft(popup, Math.Max(Editor.GutterWidth, p.X + 10));
        Canvas.SetTop(popup, p.Y + Editor.LineHeight + 6);
        Editor.Overlay.Children.Add(popup);
        popup.Dismissed += ClosePopup;
        popup.FixChosen += title => _ = ApplyFixAsync(issue, title);
        IReadOnlyList<string> fixes = [];
        if (issue.IsGae && !Editor.IsReadOnly)
        {
            var text = Editor.Text;
            var ctx = _ws.Analysis.Context;
            try { fixes = await Task.Run(() => ScriptCodeFixes.FixesAsync(text, issue.Id, issue.Line, issue.Column, ctx)); }
            catch (Exception e) when (e is InvalidOperationException or ArgumentException) { }
        }
        if (Popup == popup) popup.SetFixes(fixes);
    }

    public void ClosePopup()
    {
        if (Popup is null) return;
        Editor.Overlay.Children.Remove(Popup);
        Popup = null;
    }

    /// <summary>Applies a quick fix of the popup as one undoable edit.</summary>
    public async Task<bool> ApplyFixAsync(CodeIssue issue, string title)
    {
        if (Buffer is null) return false;
        var text = Editor.Text;
        var ctx = _ws.Analysis.Context;
        string? fixedText;
        try { fixedText = await Task.Run(() => ScriptCodeFixes.ApplyAsync(text, issue.Id, issue.Line, issue.Column, title, ctx)); }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException) { fixedText = null; }
        ClosePopup();
        if (fixedText is null || Editor.Text != text) return false;
        Editor.ReplaceAll(fixedText, title);
        AnalyzeNow();
        return true;
    }

    // ---- actions -------------------------------------------------------------------------------------------

    /// <summary>Opens the current script in the configured external editor at the caret line (T-Y03).</summary>
    public bool OpenExternally()
    {
        if (Buffer is null) return false;
        return ExternalEditorService.Of(_s).Open(Buffer.Path, Editor.CaretLine, Editor.CaretColumn);
    }

    /// <summary>
    /// Opens a script the way Preferences · Editor › "Open .cs with" says (P14.03): in the external editor at the line when
    /// the Script editor is External and the editor is found, otherwise in the built-in Script Editor. For the Assets panel,
    /// the Console, Build ROM's "go to error" and other places that open scripts (<paramref name="source"/> names the place).
    /// </summary>
    public static async Task OpenScriptFile(EditorServices s, string path, int line = 1, int column = 1, string source = "OpenScriptFile")
    {
        var settings = ScriptEditorSettings.Of(s);
        var external = ExternalEditorService.Of(s);
        if (settings.UseExternal && settings.OpenCsExternally && File.Exists(path) && !Scripting.Graph.GraphCodeGen.IsGenerated(File.ReadAllText(path))
            && external.Executable() != null && external.Open(path, line, column)) return;
        await s.Windows.Open("T-P04", new ScriptLocation(path, line, column), source: source);
    }

    /// <summary>Find &amp; Replace popover (T-O11) for this editor.</summary>
    public void OpenFind(bool replace)
    {
        if (Buffer is null) return;
        _ = _s.Windows.Open("T-O11", new FindContext(this, Editor.HasSelection && !Editor.SelectedText.Contains('\n') ? Editor.SelectedText : null, replace), source: replace ? "Ctrl+H" : "Ctrl+F");
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape && Popup != null) { ClosePopup(); e.Handled = true; }
    }

    // ---- commands ------------------------------------------------------------------------------------------

    static ScriptEditorPage? Current(EditorServices s) => (s.Top as MainWindow)?.CurrentPage as ScriptEditorPage;

    [EditorCommand("script.find", "Find", Category = "Script", Glyph = "search", Keys = "Ctrl+F", Context = ShortcutContext.Script, InPalette = false)]
    public static void Find(EditorServices s) => Current(s)?.OpenFind(replace: false);

    [EditorCommand("script.replace", "Replace", Category = "Script", Glyph = "search", Keys = "Ctrl+H", Context = ShortcutContext.Script, InPalette = false)]
    public static void ReplaceCommand(EditorServices s) => Current(s)?.OpenFind(replace: true);

    /// <summary>Adds or removes the breakpoint of a line of this script (Debugger › Breakpoints lists it). True when added.</summary>
    public bool ToggleBreakpoint(int line)
    {
        if (Buffer is not { } b) return false;
        var added = Debugger.DebuggerModel.Of(_s).ToggleBreakpoint(b.Path, line);
        Editor.InvalidateVisual();
        return added;
    }

    [EditorCommand("script.toggleBreakpoint", "Toggle breakpoint", Category = "Script", Glyph = "bug", Keys = "F9", Context = ShortcutContext.Script)]
    public static void ToggleBreakpointCommand(EditorServices s) { if (Current(s) is { } page) page.ToggleBreakpoint(page.Editor.CaretLine); }

    [EditorCommand("script.openExternally", "Open externally", Category = "Script", Glyph = "expand")]
    public static void OpenExternallyCommand(EditorServices s) => Current(s)?.OpenExternally();

    /// <summary>Create › C# Script: the Script Editor with the New Script dialog (T-D14) on top.</summary>
    [CommandHandler("assets.create.script")]
    public static async void CreateScript(EditorServices s)
    {
        await s.Windows.Open("T-P04", source: "assets.create.script");
        _ = s.Windows.Open("T-D14", source: "assets.create.script");
    }
}

/// <summary>
/// The analyzer message popup of the Script Editor (Figma T-P04): bulb, "GAE002 · float is not supported on GBA", the rule's
/// explanation and its quick fixes as buttons ("Change to fixed" primary, "Ignore" ghost).
/// </summary>
public sealed class DiagnosticPopup : Border
{
    readonly StackPanel _buttons = new() { Orientation = Orientation.Horizontal, Spacing = 8 };   // 8 px under the text, as in Figma

    public CodeIssue Issue { get; }
    public event Action? Dismissed;
    public event Action<string>? FixChosen;
    public IReadOnlyList<string> Fixes { get; private set; } = [];

    public DiagnosticPopup(CodeIssue issue)
    {
        Issue = issue;
        Name = "DiagnosticPopup";
        Theme = (Avalonia.Styling.ControlTheme?)Application.Current?.FindResource("MenuSurfaceBorder");
        Width = 360;
        Padding = new Thickness(13, 12, 13, 12);
        Cursor = new Cursor(StandardCursorType.Arrow);
        var bulb = PageChrome.Glyph("bulb", 16, issue.Severity == ScriptSeverity.Error ? "status/danger" : "status/warning");
        var title = UI.Text(issue.Title, "Body/12 Semibold", "text/primary");
        title.TextWrapping = TextWrapping.Wrap;
        var head = UI.Row(10, bulb, title);
        var body = UI.Text(issue.Description, "Label/11", "text/secondary");
        body.TextWrapping = TextWrapping.Wrap;
        Child = UI.Column(8, head, body, _buttons);
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { Dismissed?.Invoke(); e.Handled = true; } };
    }

    public void SetFixes(IReadOnlyList<string> fixes)
    {
        Fixes = fixes;
        _buttons.Children.Clear();
        for (var i = 0; i < fixes.Count; i++)
        {
            var title = fixes[i];
            var b = new AgbButton { Content = title, Kind = i == 0 ? ButtonKind.Primary : ButtonKind.Ghost, Glyph = i == 0 ? "spark" : null, Name = "Fix_" + title.Replace(' ', '_') };
            b.Click += (_, e) => { FixChosen?.Invoke(title); e.Handled = true; };
            _buttons.Children.Add(b);
        }
        _buttons.IsVisible = fixes.Count > 0;
    }

    protected override void OnPointerPressed(Avalonia.Input.PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        e.Handled = true;
    }
}
