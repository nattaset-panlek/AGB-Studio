using AgbStudio.Editor.Commands;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Shell;

/// <summary>
/// The editor window (P10.01): Title Bar with the 7 menus (no brand text; the OS title shows "AGB Studio"), the current
/// page (Scene Editor workspace by default) and the overlay layer for dialogs, popovers and toasts. Routes shortcuts,
/// Edit commands, Save / Save All / Exit with Unsaved Changes, undo / redo and autosave.
/// </summary>
public sealed class MainWindow : Window, IShellHost
{
    readonly EditorServices _s;
    readonly ContentControl _page = new() { Name = "PageHost" };
    readonly StackPanel _toasts = new() { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(16, 0, 16, 42), Name = "ToastHost" };
    readonly LayoutTransformControl _scale = new();
    readonly EditRouter _edit;
    bool _exitConfirmed;

    public TitleBar TitleBar { get; }
    public Menu MenuBar { get; }
    public OverlayHost Overlays { get; } = new() { Name = "Overlays" };
    public Control? CurrentPage => _page.Content as Control;
    public string? CurrentPageId { get; private set; }
    public EditorServices Services => _s;

    public MainWindow(EditorServices s, string? startPage = "T-P01")
    {
        _s = s;
        Title = "AGB Studio";
        Icon = Graphics.WindowIcons.App();
        Width = 1440;
        Height = 900;
        MinWidth = 960;
        MinHeight = 600;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        // Figma draws text with grayscale anti-aliasing, not ClearType colour fringes
        RenderOptions.SetTextRenderingMode(this, TextRenderingMode.Antialias);
        s.Windows.Host = this;
        s.Top = this;
        _edit = new EditRouter(() => this);

        var builder = new MenuBuilder(s.Commands, s.Shortcuts, id => id == "scene.setStart" && s.Documents.Active?.Name == "Scene");
        MenuBar = builder.BuildMenuBar(MenuModel.TitleBarMenus(RecentEntries));
        TitleBar = new TitleBar
        {
            Name = "TitleBar", Menus = MenuBar,
            DebugCommand = s.Commands.Get("tools.debugger"), BuildCommand = s.Commands.Get("build.buildRom"),
            BreadcrumbCommand = new Relay(() => _ = s.Windows.Open("T-O01", anchor: TitleBar, source: "Breadcrumb")),
        };
        DockPanel.SetDock(TitleBar, Dock.Top);
        var root = new DockPanel { Children = { TitleBar, _page } };
        _scale.Child = new Panel { Children = { root, Overlays, _toasts } };
        Content = _scale;

        WireCommands();
        s.Windows.RunStartup();
        s.Project.Changed += UpdateTitle;
        s.Documents.Changed += UpdateTitle;
        s.Toasts.Visible.CollectionChanged += (_, _) => RebuildToasts();
        s.Toolchain.Changed += () => Avalonia.Threading.Dispatcher.UIThread.Post(() => s.Status.ToolchainOk = s.Toolchain.IsComplete);
        s.Theme?.Apply(s.Preferences.Current);
        if (s.Theme != null) s.Theme.Changed += ApplyScale;
        ApplyScale();
        s.Autosave.Refresh();
        s.Preferences.Saved += () => { s.Autosave.Refresh(); s.Undo.Trim(); };
        AddHandler(KeyDownEvent, OnShortcutKey, RoutingStrategies.Bubble);
        AddHandler(KeyDownEvent, (_, e) => { if (e.Key == Key.Escape && Overlays.IsOpen && !e.Handled) { Overlays.CloseTop(); e.Handled = true; } }, RoutingStrategies.Tunnel);
        UpdateTitle();
        if (startPage != null) _ = s.Windows.Open(startPage, source: "startup");
    }

    IReadOnlyList<MenuEntry> RecentEntries()
    {
        var list = new List<MenuEntry>();
        foreach (var p in _s.Recent.Items)
        {
            var path = p.Path;
            list.Add(new RecentEntry(p.Name, path, _s.Project.Current?.ProjectPath is { } cur && string.Equals(Path.GetFullPath(cur), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase),
                new Relay(() => _ = _s.Windows.Open("T-D22", path, source: "OpenRecent"))));
        }
        if (list.Count > 0) list.Add(SeparatorEntry.Instance);
        list.Add(new CommandEntry("file.clearRecent"));
        return list;
    }

    void WireCommands()
    {
        var c = _s.Commands;
        c.Get("file.save").Handle(() => _ = _s.Documents.SaveActiveAsync(), () => _s.Documents.Active?.IsDirty == true);
        c.Get("file.saveAll").Handle(() => _ = _s.Documents.SaveAllAsync(), () => _s.Documents.HasUnsaved);
        c.Get("file.exit").Handle(Close);
        c.Get("file.clearRecent").Handle(_s.Recent.Clear);
        c.Get("assets.showInExplorer").Handle(() => _s.System.ShowInExplorer(_s.System.Selection?.Invoke() ?? _s.Project.Current?.Root));
        c.Get("edit.undo").Handle(() => _s.Undo.Undo(), () => _s.Undo.CanUndo);
        c.Get("edit.redo").Handle(() => _s.Undo.Redo(), () => _s.Undo.CanRedo);
        c.Get("edit.cut").Handle(() => _edit.Run(EditAction.Cut));
        c.Get("edit.copy").Handle(() => _edit.Run(EditAction.Copy));
        c.Get("edit.paste").Handle(() => _edit.Run(EditAction.Paste));
        c.Get("edit.duplicate").Handle(() => _edit.Run(EditAction.Duplicate));
        c.Get("edit.delete").Handle(() => _edit.Run(EditAction.Delete));
        c.Get("edit.selectAll").Handle(() => _edit.Run(EditAction.SelectAll));
        _s.Undo.Changed += () => { c.Get("edit.undo").RaiseCanExecuteChanged(); c.Get("edit.redo").RaiseCanExecuteChanged(); };
        _s.Documents.Changed += () => { c.Get("file.save").RaiseCanExecuteChanged(); c.Get("file.saveAll").RaiseCanExecuteChanged(); };
    }

    public EditRouter Edit => _edit;

    /// <summary>Active shortcut context: the shown page's own (the Launcher), else from the active document (Scene by default, Script for .cs).</summary>
    public ShortcutContext ActiveContext => CurrentPage is IShortcutScope scope ? scope.ShortcutContext
        : _s.Documents.Active?.Name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) == true ? ShortcutContext.Script : ShortcutContext.Scene;

    void OnShortcutKey(object? sender, KeyEventArgs e)
    {
        if (e.Handled || Overlays.IsOpen) return;
        var mods = e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Alt);
        // a text field keeps plain typing keys (V, M, B, G, F, Del ...) for itself
        if (FocusManager?.GetFocusedElement() is TextBox && (mods & (KeyModifiers.Control | KeyModifiers.Alt)) == 0 && e.Key is not (>= Key.F1 and <= Key.F24)) return;
        if (_s.Shortcuts.Resolve(e.Key, mods, ActiveContext) is { } cmd && cmd.CanExecute())
        {
            _s.Commands.Execute(cmd.Id);
            e.Handled = true;
        }
    }

    public void ShowPage(string id, Control page)
    {
        CurrentPageId = id;
        EnsureDocumentForPage(id);
        _page.Content = page;
        // a page that changes the documents while it is being detached (it closes its own Doc Tab) could leave the new
        // page out of the window, an empty workspace: put it in again
        if (page.GetVisualRoot() is null && IsVisible) { _page.Content = null; _page.Content = page; }
        SettleActiveDocument(id);
        UpdateTitle();
        PageShown?.Invoke(id);
    }

    void EnsureDocumentForPage(string id)
    {
        if (_s.Documents.Open.Any(d => Windows.Tilemap.PageChrome.WindowFor(d) == id)) return;
        switch (id)
        {
            case "T-P07": _s.Documents.EnsureToolDocument("Database", "db"); break;
            case "T-P14": _s.Documents.EnsureToolDocument("Localization", "globe"); break;
            case "T-P18": _s.Documents.EnsureToolDocument("Scripting API", "book"); break;
            case "T-P13": _s.Documents.EnsureToolDocument("World Map", "map"); break;
            case "T-P10": _s.Documents.EnsureToolDocument(Windows.Palette.PaletteManagerPage.DocumentName, "sliders"); break;
            case "T-P15": _s.Documents.EnsureToolDocument("Audio", "sound"); break;
            case "T-P16": _s.Documents.EnsureToolDocument("Dialogue Editor", "nodes"); break;
            case "T-P17": _s.Documents.EnsureToolDocument("Logic Graph", "nodes"); break;
            case "T-P19": _s.Documents.EnsureToolDocument("Debugger", "bug"); break;
        }
    }

    /// <summary>After a page was shown (navigation tests, crash logs).</summary>
    public event Action<string>? PageShown;

    /// <summary>
    /// Keeps the active document in step with the shown page, so every Doc Tab row, the breadcrumb, Save and the shortcut
    /// context agree: only Play Mode keeps the Game document active (other pages showed "Game" selected and the Scene Editor
    /// looked stuck in Play), the Scene Editor always edits its scene, and a document that is not open is never active.
    /// </summary>
    void SettleActiveDocument(string id)
    {
        var docs = _s.Documents;
        if (docs.Active is { } a && !docs.Open.Contains(a)) docs.Active = null;
        if (id == "T-P02")
        {
            var game = docs.Open.FirstOrDefault(d => d.IsGame);
            if (game != null) docs.Active = game;
            return;
        }
        var match = docs.Open.FirstOrDefault(d => Windows.Tilemap.PageChrome.WindowFor(d) == id);
        if (match != null)
        {
            docs.Active = match;
            return;
        }
        var scene = docs.Open.FirstOrDefault(EditorDocument.IsSceneDocument);
        if (docs.Active?.IsGame == true) docs.Active = scene ?? docs.Open.FirstOrDefault(d => !d.IsGame);
        else if (id == "T-P01" && scene != null && !EditorDocument.IsSceneDocument(docs.Active)) docs.Active = scene;
    }

    public bool OpenTitleMenu(string id)
    {
        foreach (MenuItem top in MenuBar.Items.OfType<MenuItem>())
        {
            if ((string?)top.Tag == id) { top.Focus(); top.Open(); return true; }
            foreach (var sub in top.Items.OfType<MenuItem>())
                if (sub.Tag is SubmenuEntry se && se.Id == id) { top.Open(); sub.Open(); return true; }
        }
        return false;
    }

    void UpdateTitle()
    {
        TitleBar.Project = _s.Project.Name;
        TitleBar.Document = CurrentPage is IPageBreadcrumb page ? page.Breadcrumb
            : _s.Documents.Active is { } d ? (d.Path is { } p ? Path.GetFileName(p) : d.Name) : null;
    }

    void ApplyScale()
    {
        var k = _s.Theme?.Scale ?? 1;
        _scale.LayoutTransform = Math.Abs(k - 1) < 0.001 ? null : new ScaleTransform(k, k);
    }

    void RebuildToasts()
    {
        _toasts.Children.Clear();
        foreach (var t in _s.Toasts.Visible)
        {
            var toast = new Toast { Text = t.Text, Status = t.Status };
            var msg = t;
            toast.CloseRequested += (_, _) => _s.Toasts.Close(msg);
            _toasts.Children.Add(toast);
        }
    }

    /// <summary>Exit / close with Unsaved Changes (T-D12): Save All saves then closes, Don't Save closes, X / Esc stays.</summary>
    public async Task<bool> ConfirmExitAsync()
    {
        if (!_s.Documents.HasUnsaved) return true;
        var dirty = _s.Documents.Dirty;
        var result = await _s.Windows.Open("T-D12", new UnsavedChangesArgs(dirty, UnsavedReason.Exit), source: "Exit") as string;
        if (result == "save") { await _s.Documents.SaveAllAsync(); return true; }
        return result == "discard";
    }

    /// <summary>Closing the window (X, Alt+F4, File › Exit) with unsaved files asks first (T-D12); a second close while it asks only waits for that answer.</summary>
    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (_exitConfirmed || !_s.Documents.HasUnsaved) return;
        e.Cancel = true;
        if (_asking) return;
        _asking = true;
        bool close;
        try { close = await ConfirmExitAsync(); }
        finally { _asking = false; }
        if (close) { _exitConfirmed = true; Close(); }
    }

    bool _asking;

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _ = _s.Toolchain.CheckAsync();
        if (ZipLaunch.Current() is { } zip) _ = WarnZipLaunchAsync(zip);
    }

    /// <summary>Started from inside a zip (Explorer "Run" without extracting): say to extract first; Close quits.</summary>
    public async Task WarnZipLaunchAsync(string zip)
    {
        if (await Overlays.Show(new ZipLaunchDialog(zip)) as string == "quit") Close();
    }
}
