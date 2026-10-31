using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Windows.Database;

/// <summary>
/// The frame the Database (T-P07), Localization (T-P14) and Dialogue Editor (T-P16) pages share in Figma: toolbar
/// (44 px: tool group and chips, Play / Pause / Step, the video mode chip, Undo / Redo), a 260 px left panel,
/// document tabs ("Scene" and the page's document) over the content, a 320 px right panel and the status bar.
/// </summary>
public abstract class ToolPage : DockPanel
{
    protected EditorServices S { get; }
    public EditorServices Services => S;
    readonly StackPanel _toolbarLeft = new() { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
    readonly StackPanel _docTabs = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Stretch };
    readonly Border _left, _right, _content;
    readonly StackPanel _statusLeft = new() { Orientation = Orientation.Horizontal, Spacing = 16 };
    readonly StackPanel _statusRight = new() { Orientation = Orientation.Horizontal, Spacing = 16 };
    EditorDocument? _doc;

    protected ToolPage(EditorServices s, string name)
    {
        S = s;
        Name = name;
        LastChildFill = true;

        var play = UI.Row(4,
            new PlayControl { Kind = PlayKind.Play, Command = s.Commands.Get("build.play"), Name = "PlayButton" },
            PlayButtons.Pause(s), PlayButtons.Step(s));
        var chip = new Chip { Content = "GBA · Mode 0 · 240×160", Glyph = "chip", Name = "VideoModeChip" };
        chip.Click += (_, _) => _ = s.Windows.Open("T-O02", anchor: chip, source: name + ".VideoModeChip");
        var undo = new IconButton { Glyph = "undo", Command = s.Commands.Get("edit.undo"), Name = "UndoButton" };
        var redo = new IconButton { Glyph = "redo", Command = s.Commands.Get("edit.redo"), Name = "RedoButton" };
        var bar = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("Auto,8,Auto,8,*,8,Auto,8,*,8,Auto,8,Auto"), VerticalAlignment = VerticalAlignment.Center };
        void At(Control c, int col) { Grid.SetColumn(c, col); c.VerticalAlignment = VerticalAlignment.Center; bar.Children.Add(c); }
        At(_toolbarLeft, 0);
        At(play, 6);
        At(chip, 10);
        At(ToolGroup([undo, redo]), 12);
        var toolbar = UI.Frame(bar, "bg/panel", "border/default", new Thickness(0, 0, 0, 1), new Thickness(10, 0)).Named("Toolbar");
        toolbar.Height = 44;
        SetDock(toolbar, Dock.Top);
        Children.Add(toolbar);

        var ready = new StatusItem { Content = "Ready", IsDot = true, Name = "Status_Ready" };
        void Dot() => ready.Classes.Set("warn", !s.Status.ToolchainOk);
        Dot();
        s.Status.PropertyChanged += (_, _) => Dot();
        _statusLeft.Children.Add(ready);
        var statusDock = new DockPanel();
        SetDock(_statusRight, Dock.Right);
        statusDock.Children.Add(_statusRight);
        statusDock.Children.Add(_statusLeft);
        var status = UI.Frame(statusDock, "bg/panel", "border/default", new Thickness(0, 1, 0, 0), new Thickness(12, 0)).Named("StatusBar");
        status.Height = 26;
        SetDock(status, Dock.Bottom);
        Children.Add(status);

        var body = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("260,*,320"), Name = "Body" };
        _left = UI.Frame(null, "bg/panel", "border/default", new Thickness(0, 0, 1, 0)).Named("LeftPanel");
        _left.ClipToBounds = true;
        body.Children.Add(_left);
        _right = UI.Frame(null, "bg/panel", "border/default", new Thickness(1, 0, 0, 0)).Named("RightPanel");
        _right.ClipToBounds = true;
        Grid.SetColumn(_right, 2);
        body.Children.Add(_right);
        var center = new Grid { RowDefinitions = RowDefinitions.Parse("34,*"), Name = "Center" };
        Grid.SetColumn(center, 1);
        center.Children.Add(UI.Frame(_docTabs, "bg/panel", "border/default", new Thickness(0, 0, 0, 1)).Named("DocTabs"));
        _content = UI.Frame(null, "bg/base").Named("Content");
        _content.ClipToBounds = true;
        Grid.SetRow(_content, 1);
        center.Children.Add(_content);
        body.Children.Add(center);
        Children.Add(body);

        AttachedToVisualTree += (_, _) => OnShown();
    }

    /// <summary>The page's document (its Doc Tab, Save target and title bar name).</summary>
    protected EditorDocument? Document
    {
        get => _doc;
        set
        {
            if (_doc == value) return;
            if (_doc != null) _doc.PropertyChanged -= OnDocChanged;
            _doc = value;
            if (_doc != null) _doc.PropertyChanged += OnDocChanged;
            RebuildTabs();
            if (IsAttached && _doc != null) S.Documents.Active = _doc;
        }
    }

    bool IsAttached => this.GetVisualRoot() != null;

    void OnDocChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        RebuildTabs();
        DocumentStateChanged();
    }

    /// <summary>Called when the document's dirty state changes (status bar "Saved").</summary>
    protected virtual void DocumentStateChanged() { }

    /// <summary>Called whenever the page is shown (opened from a menu, a tab or a command).</summary>
    protected virtual void OnShown()
    {
        if (_doc != null) S.Documents.Active = _doc;
    }

    protected void SetToolbar(params Control[] items)
    {
        _toolbarLeft.Children.Clear();
        _toolbarLeft.Children.AddRange(items);
    }

    protected Control LeftContent { get => _left.Child!; set => _left.Child = value; }
    protected Control RightContent { get => _right.Child!; set => _right.Child = value; }
    protected Control MainContent { get => _content.Child!; set => _content.Child = value; }

    protected void AddStatus(StatusItem item, bool right = false) => (right ? _statusRight : _statusLeft).Children.Add(item);

    /// <summary>A Figma tool group: bg/base, border, radius 7, padding 2, icon buttons 2 px apart.</summary>
    public static Border ToolGroup(IEnumerable<Control> items) => UI.Frame(UI.Row(2, items.ToArray()), "bg/base", "border/default", new Thickness(1), new Thickness(2), 7);

    void RebuildTabs()
    {
        _docTabs.Children.Clear();
        var scene = new DocTab { Label = "Scene", Glyph = "map", Height = 33, Name = "Doc_Scene" };
        scene.Activated += (_, _) => GoToScene();
        _docTabs.Children.Add(scene);
        if (_doc == null) return;
        var tab = new DocTab { Label = _doc.Path is { } p ? Path.GetFileName(p) : _doc.Name, Glyph = _doc.Glyph, IsDirty = _doc.IsDirty, IsSelected = true, Height = 33, Name = "Doc_Page" };
        _docTabs.Children.Add(tab);
    }

    public IReadOnlyList<DocTab> DocTabs => _docTabs.Children.OfType<DocTab>().ToList();

    /// <summary>The "Scene" tab: back to the Scene Editor with the scene document active.</summary>
    public void GoToScene()
    {
        if (S.Documents.Open.FirstOrDefault(d => d.Name == "Scene") is { } scene) S.Documents.Active = scene;
        _ = S.Windows.Open("T-P01", source: Name + ".SceneTab");
    }

    /// <summary>A Figma icon button with a tooltip.</summary>
    protected static IconButton Tool(string glyph, string name, string tip, Action? click = null, bool active = false)
    {
        var b = new IconButton { Glyph = glyph, Name = name, IsActive = active };
        ToolTip.SetTip(b, new TipContent(tip));
        if (click != null) b.Click += (_, _) => click();
        return b;
    }
}
