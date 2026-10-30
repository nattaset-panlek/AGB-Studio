using System.Globalization;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Graphics;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Windows.Palette;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace AgbStudio.Editor.Windows.Sprite;

/// <summary>
/// Sprite Editor (T-P08, 1440 x 900; Asset double-click, Create › Sprite, Asset Context Menu › Open, Animation List ›
/// Edit Animations…). Edits the real sprite PNG and its meta: pixel canvas with the toolbar tools, 8x8 tile grid,
/// zoom and 1x / 2x / 4x previews; the OBJ palette bank with R / G / B, hex and 15-bit value; palette slots; OAM size,
/// origin, collider and VRAM per frame; animations with ticks, loop, ping-pong and onion skin; PNG import.
/// Args: a sprite path, asset name or <see cref="EditorDocument"/>.
/// </summary>
[EditorWindow("T-P08")]
public sealed class SpriteEditorPage : DockPanel
{
    readonly EditorServices _s;
    readonly ArtLibrary? _lib;
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1 / 60.0) };
    SpriteDoc? _doc;
    EditorDocument? _edoc;
    EditableImage? _watched;
    int _anim, _pos, _tick, _colorIndex = 1;
    bool _playing, _updating;

    // left
    readonly SearchField _search = new() { Watermark = "Search sprites…", Name = "SpriteSearch" };
    readonly StackPanel _list = new() { Spacing = 1, Margin = new Thickness(6, 4, 6, 4), Name = "SpriteList" };
    readonly CheckBox _reduce = new() { Content = "Auto-reduce to 16 colours", IsChecked = true, Height = 16, Name = "AutoReduce" };
    readonly CheckBox _split = new() { IsChecked = true, Height = 16, Name = "SplitSheet" };
    readonly CheckBox _share = new() { IsChecked = true, Height = 16, Name = "SharePalette" };
    readonly Panel _dropZone;
    // toolbar
    readonly IconButton[] _tools;
    readonly IconButton _mirrorTool = new() { Glyph = "flip", Name = "Tool_mirror" };
    readonly IconButton _gridTool = new() { Glyph = "grid", Name = "Tool_grid", IsActive = true };
    readonly IconButton _onionTool = new() { Glyph = "layers", Name = "Tool_onion" };
    readonly Chip _formatChip = new() { Glyph = "image", Name = "FormatChip" };
    // center
    readonly PixelCanvas _canvas = new() { Name = "PixelCanvas" };
    readonly Chip _frameChip = new() { Glyph = "film", Name = "FrameChip" };
    readonly Chip _gridChip = new() { Content = "8×8 tile grid", Name = "TileGridChip" };
    readonly Chip _cursorChip = new() { Name = "CursorChip" };
    readonly Chip _colourChip = new() { Glyph = "pipette", Name = "ColourChip" };
    readonly Chip _mirrorChip = new() { Glyph = "flip", Content = "Mirror X", Name = "MirrorChip" };
    readonly Chip _zoomChip = new() { Name = "ZoomChip" };
    readonly PixelView[] _previews = [new() { Name = "Preview_1x" }, new() { Name = "Preview_2x" }, new() { Name = "Preview_4x" }];
    // bottom
    readonly PanelTab[] _panelTabs;
    readonly ContentControl _bottomContent = new();
    readonly Control _animationPanel;
    readonly StackPanel _animList = new() { Spacing = 1, Name = "AnimationList" };
    readonly IconButton _play = new() { Glyph = "play", Name = "AnimPlay" };
    readonly IconButton _step = new() { Glyph = "step", Name = "AnimStep" };
    readonly TextBox _ticks = new() { MinWidth = 105, Name = "TicksField" };
    readonly Chip _onionChip = new() { Glyph = "layers", Content = "Onion skin", Name = "OnionChip" };
    readonly Chip _pingChip = new() { Glyph = "refresh", Content = "Ping-pong", Name = "PingPongChip" };
    readonly StackPanel _frames = new() { Orientation = Orientation.Horizontal, Spacing = 8, Name = "FrameStrip" };
    // right
    readonly SectionHeader _bankHeader = new() { Glyph = "sliders", Meta = "16 colours", Name = "BankHeader" };
    readonly Swatch[] _swatches;
    readonly SectionHeader _colourHeader = new() { Glyph = "pipette", Name = "ColourHeader" };
    readonly ColourEditor _colour = new(withNote: false);
    readonly SectionHeader _slotsHeader = new() { Title = "Palette slots", Glyph = "layers", Name = "SlotsHeader" };
    readonly StackPanel _slots = new() { Spacing = 6, Name = "PaletteSlots" };
    readonly InspectorField _oam = new() { Kind = FieldKind.Dropdown, Label = "OAM size", Name = "OamField" };
    readonly InspectorField _origin = new() { Kind = FieldKind.XY, Label = "Origin", Name = "OriginField" };
    readonly InspectorField _collider = new() { Kind = FieldKind.XY, Label = "Collider", Name = "ColliderField" };
    readonly InspectorField _vram = new() { Kind = FieldKind.Text, Label = "VRAM/frame", Name = "VramField" };
    // status
    readonly StatusItem _statusSprite = PageParts.Status("Status_Sprite", "image");
    readonly StatusItem _statusVram = PageParts.Status("Status_Vram", "chip");
    readonly StatusItem _statusSaved = PageParts.Status("Status_Saved", "check");

    public SpriteDoc? Doc => _doc;
    public ArtLibrary? Library => _lib;
    public PixelCanvas Canvas => _canvas;
    public ColourEditor Colour => _colour;
    public int AnimationIndex => _anim;
    /// <summary>Position in the current animation (0-based); the canvas shows that frame.</summary>
    public int FramePosition => _pos;
    public int CurrentFrame => _doc is null ? 0 : Anim is { } a && a.Frames.Count > 0 ? Math.Clamp(a.Frames[Math.Clamp(_pos, 0, a.Frames.Count - 1)], 0, _doc.FrameCount - 1) : 0;
    public int ColorIndex => _colorIndex;
    public bool IsPlaying => _playing;
    public bool OnionSkin { get; private set; }
    public DrawTool Tool => _canvas.Tool;
    public IReadOnlyList<PanelTab> PanelTabs => _panelTabs;

    /// <summary>File picker for the drop zone (tests replace it).</summary>
    public Func<Task<IReadOnlyList<string>>> PickImportFiles { get; set; }

    AnimationData? Anim => _doc is null ? null : _doc.Animations.Count == 0 ? null : _doc.Animations[Math.Clamp(_anim, 0, _doc.Animations.Count - 1)];

    public SpriteEditorPage(WindowContext ctx) : this(ctx.Services, ctx.Args) { }

    public SpriteEditorPage(EditorServices s, object? args = null)
    {
        _s = s;
        Name = "SpriteEditor";
        PickImportFiles = async () => await s.System.PickFileAsync("Import sprite", ("PNG", ["*.png"])) is { } f ? [f] : [];
        _swatches = Enumerable.Range(0, 16).Select(i => new Swatch { Index = i.ToString(CultureInfo.InvariantCulture), Name = "Swatch_" + i, Cursor = new Cursor(StandardCursorType.Hand) }).ToArray();
        string[] glyphs = ["pencil", "eraser", "bucket", "line", "square", "pipette"];
        string[] tips = ["Pencil", "Eraser", "Fill", "Line", "Rectangle", "Pick colour"];
        _tools = glyphs.Select(g => new IconButton { Glyph = g, Name = "Tool_" + g }).ToArray();
        for (var i = 0; i < _tools.Length; i++)
        {
            var t = (DrawTool)i;
            _tools[i].Click += (_, _) => SetTool(t);
            ToolTip.SetTip(_tools[i], new TipContent(tips[i]));
        }
        _tools[0].IsActive = true;

        _panelTabs =
        [
            new PanelTab { Label = "Animation", Glyph = "film", Name = "Tab_Animation", Tag = "animation", IsSelected = true },
            new PanelTab { Label = "Assets", Glyph = "folder", Name = "Tab_Assets", Tag = "T-P01:Assets" },
            new PanelTab { Label = "Console", Glyph = "code", Name = "Tab_Console", Tag = "T-P05" },
        ];
        _dropZone = BuildDropZone();
        _animationPanel = BuildAnimationPanel();

        if (s.Project.Current != null) _lib = ArtLibrary.For(s);

        // ---- toolbar ----
        _mirrorTool.Click += (_, _) => SetMirror(!_canvas.Mirror);
        _gridTool.Click += (_, _) => SetGrid(!_canvas.ShowTileGrid);
        _onionTool.Click += (_, _) => SetOnion(!OnionSkin);
        ToolTip.SetTip(_mirrorTool, new TipContent("Mirror X"));
        ToolTip.SetTip(_gridTool, new TipContent("8×8 tile grid"));
        ToolTip.SetTip(_onionTool, new TipContent("Onion skin"));
        _formatChip.Click += (_, _) => OpenPaletteChoice();
        var toolbar = PageParts.Toolbar([PageParts.ToolGroup(_tools), PageParts.ToolGroup(_mirrorTool, _gridTool, _onionTool), _formatChip],
            PageParts.PlayControls(s), [PageParts.VideoModeChip(s), PageParts.UndoRedo(s)]);
        SetDock(toolbar, Dock.Top);
        Children.Add(toolbar);

        // ---- status bar ----
        var status = PageParts.StatusBar([PageParts.Status("Status_Ready", "check", "Ready", dot: true), _statusSprite], [_statusVram, _statusSaved]);
        SetDock(status, Dock.Bottom);
        Children.Add(status);

        // ---- body ----
        var body = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("260,*,320"), Name = "Body" };
        var left = UI.Frame(BuildLeft(), "bg/panel", "border/default", new Thickness(0, 0, 1, 0)).Named("SpritesPanel");
        left.ClipToBounds = true;
        body.Children.Add(left);
        var center = BuildCenter();
        Grid.SetColumn(center, 1);
        body.Children.Add(center);
        var right = UI.Frame(BuildRight(), "bg/panel", "border/default", new Thickness(1, 0, 0, 0)).Named("PalettePanel");
        right.ClipToBounds = true;
        Grid.SetColumn(right, 2);
        body.Children.Add(right);
        Children.Add(body);

        _timer.Tick += (_, _) => Tick(1);
        if (_lib != null)
        {
            Action changed = () => Dispatcher.UIThread.Post(OnLibraryChanged);
            Action docs = () => Dispatcher.UIThread.Post(UpdateStatus);
            AttachedToVisualTree += (_, _) => { _lib.Changed += changed; _s.Documents.Changed += docs; if (_edoc != null) _s.Documents.Active = _edoc; Refresh(); };
            DetachedFromVisualTree += (_, _) => { _lib.Changed -= changed; _s.Documents.Changed -= docs; Stop(); };
            var path = args switch
            {
                string p => ResolveSprite(p),
                EditorDocument d => d.Path,
                _ => null,
            } ?? (s.Documents.Active is { Path: { } ap } && ap.EndsWith(".png", StringComparison.OrdinalIgnoreCase) && File.Exists(ap) ? ap : null)
              ?? _lib.SpriteFiles().OrderBy(File.GetCreationTimeUtc).FirstOrDefault();
            if (path != null) Open(path);
        }
        Refresh();
    }

    string? ResolveSprite(string p)
    {
        if (_lib is null) return null;
        if (File.Exists(p)) return Path.GetFullPath(p);
        var rel = _lib.Full(p);
        if (File.Exists(rel)) return rel;
        return _lib.SpriteFiles().FirstOrDefault(f => string.Equals(GameProject.AssetName(f), p, StringComparison.OrdinalIgnoreCase)
                                                     || string.Equals(Path.GetFileName(f), p, StringComparison.OrdinalIgnoreCase));
    }

    // ---- layout ----------------------------------------------------------------------------------------------

    Control BuildLeft()
    {
        var header = new PanelHeader { Title = "Sprites", Glyph = "image", ActionGlyph = "plus", Name = "SpritesHeader" };
        header.ActionCommand = new Relay(() => _ = NewSprite());
        _search.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) RefreshList(); };
        var importHeader = new PanelHeader { Title = "Import", Glyph = "upload", Name = "ImportHeader" };
        var checks = UI.Column(8, _reduce, _split, _share);
        checks.Margin = new Thickness(10, 10, 10, 0);
        _dropZone.Margin = new Thickness(10, 10, 10, 0);
        _dropZone.Height = 91;
        var grid = new Grid { RowDefinitions = RowDefinitions.Parse("Auto,Auto,Auto,Auto,Auto,Auto,*") };
        void Row(Control c, int r) { Grid.SetRow(c, r); grid.Children.Add(c); }
        Row(header, 0);
        Row(new Border { Padding = new Thickness(10, 8, 10, 4), Child = _search }, 1);
        Row(new ScrollViewer { Content = _list, MaxHeight = 380, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled }, 2);
        Row(importHeader, 3);
        Row(_dropZone, 4);
        Row(checks, 5);
        return grid;
    }

    Panel BuildDropZone()
    {
        var icon = new Icon { Glyph = "upload", Size = 16, HorizontalAlignment = HorizontalAlignment.Center }.Token(Icon.ForegroundProperty, "text/secondary");
        var t1 = UI.Text("Drop PNG / Aseprite here", "Body/12", "text/secondary");
        var t2 = UI.Text("or File › Import Asset", "Caption/10", "text/muted");
        t1.HorizontalAlignment = t2.HorizontalAlignment = HorizontalAlignment.Center;
        var stack = new StackPanel { Spacing = 0, VerticalAlignment = VerticalAlignment.Center, Children = { icon, t1, t2 } };
        t1.Margin = new Thickness(0, 8, 0, 5);
        var zone = PageParts.Dashed(new Border { Background = Brushes.Transparent, Child = stack });
        zone.Name = "DropZone";
        zone.Cursor = new Cursor(StandardCursorType.Hand);
        DragDrop.SetAllowDrop(zone, true);
        zone.AddHandler(DragDrop.DragOverEvent, (_, e) => { e.DragEffects = DroppedFiles(e).Any() ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; });
        zone.AddHandler(DragDrop.DropEvent, (_, e) => { ImportFiles(DroppedFiles(e)); e.Handled = true; });
        zone.PointerPressed += async (_, e) => { e.Handled = true; ImportFiles(await PickImportFiles()); };
        return zone;
    }

    /// <summary>Files dropped from Explorer, or file paths dropped as text by other tools.</summary>
    static List<string> DroppedFiles(DragEventArgs e)
    {
        var files = e.DataTransfer.TryGetFiles()?.Select(f => f.TryGetLocalPath()).OfType<string>().ToList() ?? [];
        if (files.Count == 0 && e.DataTransfer.TryGetText() is { } text)
            files.AddRange(text.Split((char[])[(char)13, (char)10], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(File.Exists));
        return files;
    }

    Control BuildCenter()
    {
        var grid = new Grid { RowDefinitions = RowDefinitions.Parse("34,*,220"), Name = "Center" };
        var tabs = PageParts.DocTabs(_s, this);
        grid.Children.Add(tabs);

        _canvas.HoverChanged += UpdateChips;
        _canvas.Picked += i => SelectColor(i);
        _canvas.ZoomChanged += UpdateChips;
        _gridChip.Click += (_, _) => SetGrid(!_canvas.ShowTileGrid);
        _mirrorChip.Click += (_, _) => SetMirror(!_canvas.Mirror);
        _zoomChip.Click += (_, _) => { _canvas.Fit = true; _canvas.Refresh(); UpdateChips(); };
        var topLeft = UI.Row(6, _frameChip, _gridChip);
        topLeft.Margin = new Thickness(12);
        topLeft.HorizontalAlignment = HorizontalAlignment.Left;
        topLeft.VerticalAlignment = VerticalAlignment.Top;
        var bottomLeft = UI.Row(8, _cursorChip, _colourChip);
        bottomLeft.Margin = new Thickness(12);
        bottomLeft.HorizontalAlignment = HorizontalAlignment.Left;
        bottomLeft.VerticalAlignment = VerticalAlignment.Bottom;
        var bottomRight = UI.Row(6, _mirrorChip, _zoomChip);
        bottomRight.Margin = new Thickness(12);
        bottomRight.HorizontalAlignment = HorizontalAlignment.Right;
        bottomRight.VerticalAlignment = VerticalAlignment.Bottom;
        var previews = UI.Row(12);
        string[] labels = ["1×", "2×", "4×"];
        for (var i = 0; i < 3; i++)
        {
            _previews[i].VerticalAlignment = VerticalAlignment.Bottom;
            _previews[i].HorizontalAlignment = HorizontalAlignment.Center;
            var label = UI.Text(labels[i], "Caption/10", "text/muted");
            label.HorizontalAlignment = HorizontalAlignment.Center;
            previews.Children.Add(UI.Column(6, new Panel { Height = 72, Children = { _previews[i] } }, label));
        }
        var previewBox = UI.Frame(previews, "bg/panel", "border/default", new Thickness(1), new Thickness(10), 8).Named("PreviewBox");
        previewBox.HorizontalAlignment = HorizontalAlignment.Right;
        previewBox.VerticalAlignment = VerticalAlignment.Top;
        previewBox.Margin = new Thickness(0, 14, 14, 0);
        var area = UI.Frame(new Panel { Children = { _canvas, topLeft, previewBox, bottomLeft, bottomRight } }, "bg/canvas").Named("CanvasArea");
        Grid.SetRow(area, 1);
        grid.Children.Add(area);

        foreach (var t in _panelTabs) t.Activated += (_, _) => SelectPanel(t);
        var tabRow = new Border { Height = 32, Padding = new Thickness(6, 0), Child = UI.Row(2, _panelTabs) }.Token(Border.BorderBrushProperty, "border/default");
        tabRow.BorderThickness = new Thickness(0, 0, 0, 1);
        var bottom = new DockPanel();
        SetDock(tabRow, Dock.Top);
        bottom.Children.Add(tabRow);
        bottom.Children.Add(_bottomContent);
        _bottomContent.Content = _animationPanel;
        var bottomPanel = UI.Frame(bottom, "bg/panel").Named("BottomPanel");
        Grid.SetRow(bottomPanel, 2);
        grid.Children.Add(bottomPanel);
        return grid;
    }

    Control BuildAnimationPanel()
    {
        _play.Click += (_, _) => { if (_playing) Stop(); else Play(); };
        _step.Click += (_, _) => StepFrame();
        ToolTip.SetTip(_play, new TipContent("Play animation"));
        ToolTip.SetTip(_step, new TipContent("Next frame"));
        _ticks.Theme = (Avalonia.Styling.ControlTheme?)Application.Current?.FindResource("FieldBox");
        _ticks.KeyDown += (_, e) => { if (e.Key == Key.Enter) { CommitTicks(); e.Handled = true; } };
        _ticks.LostFocus += (_, _) => CommitTicks();
        _onionChip.Click += (_, _) => SetOnion(!OnionSkin);
        _pingChip.Click += (_, _) => TogglePingPong();
        var speed = UI.Text("Speed", "Body/12", "text/secondary", 132);
        speed.Margin = new Thickness(13, 0, 8, 0);
        var leftRow = UI.Row(0, UI.Row(2, _play, _step), speed, _ticks);
        var rightRow = UI.Row(8, _onionChip, _pingChip);
        var header = new DockPanel { Height = 26 };
        DockPanel.SetDock(rightRow, Dock.Right);
        header.Children.AddRange([rightRow, leftRow]);
        var timeline = UI.Column(11, header, _frames);
        timeline.Margin = new Thickness(0, 2, 0, 0);
        var grid = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("158,20,*"), Margin = new Thickness(6, 8, 15, 8), Name = "AnimationPanel" };
        grid.Children.Add(_animList);
        Grid.SetColumn(timeline, 2);
        grid.Children.Add(timeline);
        return grid;
    }

    Control BuildRight()
    {
        var swatchGrid = UI.Column(4, UI.Row(4, _swatches[..8]), UI.Row(4, _swatches[8..]));
        for (var i = 0; i < 16; i++)
        {
            var k = i;
            _swatches[i].PointerPressed += (_, e) => { SelectColor(k); e.Handled = true; };
        }
        _colour.ColourEdited += argb => { if (_doc?.Bank is { } b) _lib?.SetColor(b, _colorIndex, argb); };
        _oam.AddHandler(Button.ClickEvent, (_, e) => { OpenOamMenu(_oam); e.Handled = true; });
        _origin.PropertyChanged += (_, e) => { if (e.Property == InspectorField.ValueProperty || e.Property == InspectorField.Value2Property) CommitOrigin(); };
        _collider.PropertyChanged += (_, e) => { if (e.Property == InspectorField.ValueProperty || e.Property == InspectorField.Value2Property) CommitCollider(); };
        _vram.TemplateApplied += (_, e) => { if (e.NameScope.Find<TextBox>("PART_Text") is { } t) t.IsReadOnly = true; };
        Thickness pad = new(12, 7, 12, 10);
        var column = UI.Column(0,
            PageParts.Section(_bankHeader, swatchGrid, pad, 7),
            PageParts.Section(_colourHeader, _colour, pad, 7),
            PageParts.Section(_slotsHeader, _slots, pad, 7),
            PageParts.Section(new SectionHeader { Title = "Sprite settings", Glyph = "cube", Name = "SettingsHeader" }, UI.Column(6, _oam, _origin, _collider, _vram), pad, 7));
        var dock = new DockPanel();
        var header = new PanelHeader { Title = "Palette", Glyph = "sliders", Name = "PaletteHeader" };
        SetDock(header, Dock.Top);
        dock.Children.AddRange([header, new ScrollViewer { Content = column, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled }]);
        return dock;
    }

    // ---- documents ---------------------------------------------------------------------------------------------

    /// <summary>Opens a sprite of the project in this page (adds its document tab).</summary>
    public void Open(string path)
    {
        if (_lib is null) return;
        try { _ = _lib.Image(path); }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _s.Toasts.Show($"Could not open {Path.GetFileName(path)}: {e.Message}", ToastStatus.Error);
            return;
        }
        Stop();
        if (_watched != null) _watched.Changed -= OnImageChanged;
        _doc = new SpriteDoc(_lib, path);
        _watched = _doc.Image;
        _watched.Changed += OnImageChanged;
        _edoc = _s.Documents.Open.FirstOrDefault(d => d.Path != null && string.Equals(Path.GetFullPath(d.Path), _doc.Path, StringComparison.OrdinalIgnoreCase))
                ?? _s.Documents.Add(new EditorDocument { Name = _doc.FileName, Glyph = "image", Path = _doc.Path });
        var lib = _lib;
        _edoc.SaveHandler = _ => { lib.SaveAll(); return Task.CompletedTask; };
        _s.Documents.Active = _edoc;
        _s.System.Selection = () => _doc?.Path;
        _anim = 0;
        _pos = 0;
        _colorIndex = _doc.Bank is { } b && b.Colors.Count > 1 ? Math.Min(_colorIndex, b.Colors.Count - 1) : 0;
        if (_colorIndex == 0 && _doc.Bank is { Colors.Count: > 1 }) _colorIndex = 1;
        _canvas.Doc = _doc;
        _canvas.Fit = true;
        Refresh();
    }

    void OnImageChanged()
    {
        if (_edoc != null && _watched is { IsDirty: true }) _edoc.IsDirty = true;
        _canvas.Refresh();
        RefreshPreviews();
        RefreshFrames();
        UpdateStatus();
    }

    void OnLibraryChanged()
    {
        if (_doc != null && !File.Exists(_doc.Path) && !_doc.Image.IsDirty) return;
        if (_doc != null && _watched != _doc.Image)
        {
            if (_watched != null) _watched.Changed -= OnImageChanged;
            _watched = _doc.Image;
            _watched.Changed += OnImageChanged;
        }
        if (_edoc != null && _lib is { IsDirty: true }) _edoc.IsDirty = true;
        Refresh();
    }

    async Task NewSprite()
    {
        var result = await _s.Windows.Open("T-D13", _doc?.Path, source: "SpritesAdd");
        if (result is string path) Open(path);
    }

    // ---- refresh ----------------------------------------------------------------------------------------------------

    public void Refresh()
    {
        _updating = true;
        try
        {
            RefreshList();
            var d = _doc;
            var bank = d?.Bank;
            _formatChip.Content = d is null ? "" : $"{d.SizeText} · 4bpp · {(bank is null ? "OBJ" : bank.Id.Replace("OBJ ", "OBJ pal "))}";
            _split.Content = $"Split sheet into {d?.SizeText ?? "16×16"} frames";
            _share.Content = $"Share palette with {bank?.Id ?? "OBJ 0"}";
            _share.IsEnabled = bank != null;
            _canvas.Frame = CurrentFrame;
            _canvas.ColorIndex = _colorIndex;
            _canvas.OnionFrame = OnionSkin ? PreviousFrame() : -1;
            _canvas.Refresh();
            RefreshPreviews();
            RefreshAnimations();
            RefreshFrames();
            RefreshPalette();
            RefreshSettings();
            UpdateChips();
            UpdateStatus();
        }
        finally { _updating = false; }
    }

    void RefreshList()
    {
        _list.Children.Clear();
        if (_lib is null) return;
        var q = _search.Text?.Trim() ?? "";
        foreach (var f in _lib.SpriteFiles().OrderBy(File.GetCreationTimeUtc).ThenBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            var name = Path.GetFileName(f);
            if (q.Length > 0 && !name.Contains(q, StringComparison.OrdinalIgnoreCase)) continue;
            var doc = new SpriteDoc(_lib, f);
            string meta;
            try { meta = doc.ListMeta; }
            catch (Exception e) when (e is IOException or InvalidDataException) { meta = ""; }
            var item = new TreeItem { Label = name, Glyph = "image", Meta = meta, IsSelected = _doc != null && string.Equals(_doc.Path, doc.Path, StringComparison.OrdinalIgnoreCase), Name = "Sprite_" + name };
            var path = f;
            item.Activated += (_, _) => Open(path);
            _list.Children.Add(item);
        }
    }

    int PreviousFrame()
    {
        if (Anim is not { Frames.Count: > 1 } a) return -1;
        return a.Frames[(_pos - 1 + a.Frames.Count) % a.Frames.Count];
    }

    uint? Backdrop()
    {
        if (_lib is null) return null;
        var p = _lib.Project;
        foreach (var name in new[] { p.Settings.LastScene, p.Settings.General.StartScene })
        {
            if (string.IsNullOrEmpty(name)) continue;
            var file = Path.Combine(p.ScenesDir, name + ".scene");
            if (!File.Exists(file)) continue;
            try { if (Colour15.TryParse24(GameProject.Load<SceneFile>(file).Backdrop, out var c)) return c; }
            catch (Exception e) when (e is IOException or System.Text.Json.JsonException) { }
        }
        return null;
    }

    void RefreshPreviews()
    {
        if (_doc is not { } d) return;
        var px = d.FramePixels(CurrentFrame);
        var backdrop = Backdrop();
        int[] scales = [1, 2, 4];
        for (var i = 0; i < 3; i++)
        {
            var s = Math.Max(1, scales[i] * 16 / Math.Max(16, Math.Max(d.FrameW, d.FrameH)));
            _previews[i].Show(px, d.FrameW, d.FrameH, s, backdrop, 4, _canvas.Mirror);
        }
    }

    void RefreshAnimations()
    {
        _animList.Children.Clear();
        if (_doc is null) return;
        var anims = _doc.Animations;
        for (var i = 0; i < anims.Count; i++)
        {
            var a = anims[i];
            var row = new ListRow
            {
                Label = a.Name, Glyph = "film", IsSelected = i == _anim, Name = "Anim_" + a.Name,
                Meta = $"{a.Frames.Count}f" + (a.Loop ? " · loop" : ""),
            };
            var k = i;
            row.Activated += (_, e) =>
            {
                if (e.ClickCount == 2) ToggleLoop(k);
                else SelectAnimation(k);
            };
            _animList.Children.Add(row);
        }
        var anim = Anim;
        if (!_ticks.IsFocused) _ticks.Text = anim is null ? "" : TicksText(anim.Ticks);
        _onionChip.Style = OnionSkin ? ChipStyle.Accent : ChipStyle.Default;
        _onionTool.IsActive = OnionSkin;
        _pingChip.Style = anim?.PingPong == true ? ChipStyle.Accent : ChipStyle.Default;
        _play.IsActive = _playing;
        _play.Glyph = _playing ? "pause" : "play";
    }

    /// <summary>"6 ticks ≈ 10 fps" (60 frames per second on the GBA).</summary>
    public static string TicksText(int ticks)
    {
        ticks = Math.Max(1, ticks);
        var fps = 60.0 / ticks;
        return $"{ticks} ticks ≈ {fps.ToString(fps == Math.Floor(fps) ? "0" : "0.#", CultureInfo.InvariantCulture)} fps";
    }

    void RefreshFrames()
    {
        _frames.Children.Clear();
        if (_doc is not { } d || Anim is not { } a) return;
        var scale = Math.Max(1, 32 / Math.Max(d.FrameW, d.FrameH));
        if (d.FrameW * scale > 60 || d.FrameH * scale > 60) scale = 1;
        for (var k = 0; k < a.Frames.Count; k++)
        {
            var frame = Math.Clamp(a.Frames[k], 0, d.FrameCount - 1);
            var ticks = a.FrameTicks != null && k < a.FrameTicks.Count ? a.FrameTicks[k] : a.Ticks;
            var thumb = new FrameThumb { Label = $"{k + 1} · {ticks}t", Preview = null, IsSelected = k == _pos, Name = "Frame_" + k };
            var view = new PixelView { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false };
            view.Show(d.FramePixels(frame), d.FrameW, d.FrameH, scale);
            view.Margin = new Thickness(0, Math.Max(0, (66 - d.FrameH * scale) / 2), 0, 0);
            var pos = k;
            thumb.Activated += (_, _) => SelectFrame(pos);
            _frames.Children.Add(new Panel { Children = { thumb, view } });
        }
    }

    void RefreshPalette()
    {
        var bank = _doc?.Bank;
        _bankHeader.Title = bank?.Label ?? "No palette";
        if (bank != null) _colorIndex = Math.Clamp(_colorIndex, 0, Math.Max(0, bank.Colors.Count - 1));
        for (var i = 0; i < 16; i++)
        {
            var sw = _swatches[i];
            if (bank is null || i >= bank.Colors.Count) { sw.State = SwatchState.Empty; sw.Color = Colors.Transparent; }
            else if (bank.IsTransparentIndex(i)) sw.State = i == _colorIndex ? SwatchState.Selected : SwatchState.Transparent;
            else
            {
                sw.State = i == _colorIndex ? SwatchState.Selected : SwatchState.Default;
                sw.Color = Colour15.ToColor(bank.Colors[i]);
            }
        }
        _colourHeader.Title = $"Colour {_colorIndex}";
        if (bank != null && _colorIndex < bank.Colors.Count) _colour.Show(bank.Colors[_colorIndex], !bank.IsTransparentIndex(_colorIndex));
        else _colour.Show(Colour15.Transparent, false);

        _slots.Children.Clear();
        if (_lib is null) return;
        var objBanks = _lib.Of(BankKind.Obj).ToList();
        _slotsHeader.Meta = $"{objBanks.Count} / 16 used";
        foreach (var b in objBanks)
        {
            var label = UI.Text(b.Label, "Body/12", "text/secondary", 118);
            label.TextTrimming = TextTrimming.CharacterEllipsis;
            var strip = new ColourStrip { Colors = b.Colors.Where((_, i) => !b.IsTransparentIndex(i)).ToList(), VerticalAlignment = VerticalAlignment.Center };
            var row = new DockPanel { Height = 24, Background = Brushes.Transparent, Cursor = new Cursor(StandardCursorType.Hand), Name = "Slot_" + b.Id.Replace(' ', '_') };
            DockPanel.SetDock(label, Dock.Left);
            row.Children.AddRange([label, strip]);
            var bank2 = b;
            row.PointerPressed += (_, e) => { e.Handled = true; _ = _s.Windows.Open("T-P10", bank2, row, source: "PaletteSlots"); };
            _slots.Children.Add(row);
        }
    }

    void RefreshSettings()
    {
        if (_doc is not { } d) return;
        var m = d.Meta;
        var (ow, oh) = d.Oam;
        _oam.Value = SpriteDoc.OamLabel(ow, oh);
        _origin.Value = m.OriginX.ToString(CultureInfo.InvariantCulture);
        _origin.Value2 = m.OriginY.ToString(CultureInfo.InvariantCulture);
        _collider.Value = $"{m.Collider.W} × {m.Collider.H}";
        _collider.Value2 = $"{m.Collider.X}, {m.Collider.Y}";
        _vram.Value = d.VramText;
    }

    void UpdateChips()
    {
        var a = Anim;
        _frameChip.Content = a is null ? "" : $"{a.Name} · frame {_pos + 1} / {a.Frames.Count}";
        _cursorChip.Content = _canvas.Hover is { } h ? $"x {h.X}  y {h.Y}" : "x -  y -";
        var bank = _doc?.Bank;
        var color = bank != null && _colorIndex < bank.Colors.Count ? bank.Colors[_colorIndex] : Colour15.Transparent;
        _colourChip.Content = $"index {_colorIndex} · {(Colour15.IsTransparent(color) ? "transparent" : Colour15.Hex24(color))}";
        _mirrorChip.Style = _canvas.Mirror ? ChipStyle.Accent : ChipStyle.Default;
        _mirrorTool.IsActive = _canvas.Mirror;
        _gridTool.IsActive = _canvas.ShowTileGrid;
        _zoomChip.Content = $"{_canvas.Zoom * 100}%";
    }

    void UpdateStatus()
    {
        if (_doc is not { } d || _lib is null)
        {
            _statusSprite.IsVisible = _statusVram.IsVisible = _statusSaved.IsVisible = false;
            return;
        }
        _statusSprite.IsVisible = _statusVram.IsVisible = _statusSaved.IsVisible = true;
        _statusSprite.Content = $"{d.FileName} · {d.SizeText} · {d.FrameCount} {(d.FrameCount == 1 ? "frame" : "frames")} · 4bpp";
        var bytes = 0;
        foreach (var f in _lib.SpriteFiles())
        {
            try { var sd = new SpriteDoc(_lib, f); bytes += sd.FrameCount * sd.BytesPerFrame; }
            catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException) { }
        }
        _statusVram.Content = $"VRAM OBJ {(bytes + 1023) / 1024} / 32 KB";
        var dirty = _edoc?.IsDirty == true;
        _statusSaved.Content = dirty ? "Unsaved changes" : "Saved";
        _statusSaved.Glyph = dirty ? "dots" : "check";
    }

    // ---- actions --------------------------------------------------------------------------------------------------

    public void SetTool(DrawTool t)
    {
        _canvas.Tool = t;
        for (var i = 0; i < _tools.Length; i++) _tools[i].IsActive = i == (int)t;
    }

    public void SelectColor(int index)
    {
        if (_doc?.Bank is not { } b || index < 0 || index >= b.Colors.Count) return;
        _colorIndex = index;
        _canvas.ColorIndex = index;
        if (_canvas.Tool == DrawTool.Pipette) SetTool(DrawTool.Pencil);
        RefreshPalette();
        UpdateChips();
    }

    public void SelectAnimation(int i)
    {
        _anim = i;
        _pos = 0;
        _tick = 0;
        _dir = 1;
        Refresh();
    }

    public void SelectFrame(int pos)
    {
        _tick = 0;
        ShowFrame(pos);
    }

    void ShowFrame(int pos)
    {
        if (Anim is not { } a || a.Frames.Count == 0) return;
        _pos = (pos % a.Frames.Count + a.Frames.Count) % a.Frames.Count;
        _canvas.Frame = CurrentFrame;
        _canvas.OnionFrame = OnionSkin ? PreviousFrame() : -1;
        _canvas.Refresh();
        RefreshPreviews();
        RefreshFrames();
        UpdateChips();
    }

    public void StepFrame() => SelectFrame(_pos + 1);

    public void SetGrid(bool on)
    {
        _canvas.ShowTileGrid = on;
        _canvas.Refresh();
        UpdateChips();
    }

    public void SetMirror(bool on)
    {
        _canvas.Mirror = on;
        _canvas.Refresh();
        RefreshPreviews();
        UpdateChips();
    }

    public void SetOnion(bool on)
    {
        OnionSkin = on;
        _canvas.OnionFrame = on ? PreviousFrame() : -1;
        _canvas.Refresh();
        RefreshAnimations();
    }

    public void Play()
    {
        if (Anim is null) return;
        _playing = true;
        _tick = 0;
        _timer.Start();
        RefreshAnimations();
    }

    public void Stop()
    {
        if (!_playing) return;
        _playing = false;
        _timer.Stop();
        RefreshAnimations();
    }

    int _dir = 1;

    /// <summary>Advances the animation preview by <paramref name="frames"/> GBA frames (60 per second): ticks per frame, loop and ping-pong.</summary>
    public void Tick(int frames)
    {
        if (Anim is not { Frames.Count: > 0 } a) return;
        for (var f = 0; f < frames; f++)
        {
            var ticks = a.FrameTicks != null && _pos < a.FrameTicks.Count ? a.FrameTicks[_pos] : a.Ticks;
            if (++_tick < Math.Max(1, ticks)) continue;
            _tick = 0;
            var n = a.Frames.Count;
            if (a.PingPong && n > 1)
            {
                if (_pos + _dir >= n || _pos + _dir < 0) _dir = -_dir;
                _pos += _dir;
            }
            else if (_pos + 1 < n) _pos++;
            else if (a.Loop) _pos = 0;
            else { Stop(); break; }
        }
        ShowFrame(_pos);
    }

    void CommitTicks()
    {
        if (_updating || _doc is null || Anim is null) return;
        var digits = new string((_ticks.Text ?? "").TrimStart().TakeWhile(char.IsDigit).ToArray());
        if (!int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var t) || t < 1 || t > 255) { RefreshAnimations(); return; }
        SetTicks(t);
    }

    /// <summary>Ticks per frame of the current animation (every frame).</summary>
    public void SetTicks(int ticks)
    {
        if (_doc is null || Anim is null) return;
        var index = _anim;
        EnsureAnimations();
        _doc.EditMeta("Animation speed", m => { m.Animations[index].Ticks = ticks; m.Animations[index].FrameTicks = null; });
        _ticks.Text = TicksText(ticks);
    }

    public void ToggleLoop(int index)
    {
        if (_doc is null) return;
        EnsureAnimations();
        _doc.EditMeta("Loop", m => m.Animations[index].Loop = !m.Animations[index].Loop);
    }

    public void TogglePingPong()
    {
        if (_doc is null || Anim is null) return;
        var index = _anim;
        EnsureAnimations();
        _doc.EditMeta("Ping-pong", m => m.Animations[index].PingPong = !m.Animations[index].PingPong);
    }

    // a sheet without animations shows a default one; editing it makes it real
    void EnsureAnimations()
    {
        if (_doc is { } d && d.Meta.Animations.Count == 0) d.Meta.Animations.Add(new AnimationData { Name = "idle", Frames = Enumerable.Range(0, d.FrameCount).ToList() });
    }

    void CommitOrigin()
    {
        if (_updating || _doc is null) return;
        if (!int.TryParse(_origin.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) || !int.TryParse(_origin.Value2, NumberStyles.Integer, CultureInfo.InvariantCulture, out var y)) return;
        SetOrigin(x, y);
    }

    public void SetOrigin(int x, int y)
    {
        if (_doc is null || (_doc.Meta.OriginX == x && _doc.Meta.OriginY == y)) return;
        _doc.EditMeta("Origin", m => { m.OriginX = x; m.OriginY = y; });
    }

    void CommitCollider()
    {
        if (_updating || _doc is null) return;
        var size = (_collider.Value ?? "").Split(['×', 'x', 'X', '*'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var pos = (_collider.Value2 ?? "").Split([',', ' '], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (size.Length != 2 || pos.Length != 2) return;
        if (!int.TryParse(size[0], out var w) || !int.TryParse(size[1], out var h) || !int.TryParse(pos[0], out var x) || !int.TryParse(pos[1], out var y)) return;
        SetCollider(x, y, w, h);
    }

    public void SetCollider(int x, int y, int w, int h)
    {
        if (_doc is null || w < 1 || h < 1) return;
        var c = _doc.Meta.Collider;
        if (c.X == x && c.Y == y && c.W == w && c.H == h) return;
        _doc.EditMeta("Collider", m => m.Collider = new ColliderData { X = x, Y = y, W = w, H = h });
    }

    void OpenOamMenu(Control anchor)
    {
        if (_doc != null) _ = _s.Windows.Open("T-M19", _doc, anchor, source: "OamField");
    }

    // the toolbar chip names the OBJ palette; choosing another bank shares that palette
    void OpenPaletteChoice()
    {
        if (_doc is null || _lib is null) return;
        var items = new List<MenuItem>();
        foreach (var b in _lib.Of(BankKind.Obj))
        {
            var bank = b;
            var mi = new MenuItem { Header = b.Label, Command = new Relay(() => SharePalette(bank)), Name = "Share_" + b.Id.Replace(' ', '_') };
            if (b == _doc.Bank) { mi.ToggleType = MenuItemToggleType.CheckBox; mi.IsChecked = true; }
            items.Add(mi);
        }
        new ContextMenu { ItemsSource = items }.Open(_formatChip);
    }

    /// <summary>Moves the sprite into another OBJ bank so it shares that palette (one undo step).</summary>
    public void SharePalette(PaletteBank bank)
    {
        if (_doc is null || _lib is null) return;
        _lib.Assign(_doc.Path, bank);
    }

    /// <summary>Imports PNG files dropped on (or picked from) the drop zone with the three import options, then opens the last one.</summary>
    public void ImportFiles(IEnumerable<string> files)
    {
        if (_lib is null) return;
        string? last = null;
        foreach (var f in files)
        {
            try
            {
                last = SpriteImport.Import(_lib, f, new SpriteImportOptions(_reduce.IsChecked == true, _split.IsChecked == true,
                    _doc?.FrameW ?? 16, _doc?.FrameH ?? 16, _share.IsChecked == true ? _doc?.Bank : null));
            }
            catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException)
            {
                _s.Toasts.Show($"Could not import {Path.GetFileName(f)}: {e.Message}", ToastStatus.Error);
            }
        }
        if (last != null) Open(last);
    }

    void SelectPanel(PanelTab tab)
    {
        foreach (var t in _panelTabs) t.IsSelected = t == tab;
        var id = (string)tab.Tag!;
        _bottomContent.Content = id == "animation" ? _animationPanel : _s.Windows.CreatePanel(id) ?? new Panel();
        if (id != "animation") _s.Windows.Requests.Add((id, tab.Name));
    }

    // ---- commands ------------------------------------------------------------------------------------------------

    /// <summary>Create › Sprite: the Sprite Editor with the New Sprite dialog; the new sprite opens when it is created.</summary>
    [CommandHandler("assets.create.sprite")]
    public static async void CreateSprite(EditorServices s)
    {
        await s.Windows.Open("T-P08", source: "assets.create.sprite");
        if (s.Project.Current is null) return;
        var page = s.Windows.Host?.CurrentPage as SpriteEditorPage;
        var result = await s.Windows.Open("T-D13", page?.Doc?.Path, source: "assets.create.sprite");
        if (result is string path) page?.Open(path);
    }
}
