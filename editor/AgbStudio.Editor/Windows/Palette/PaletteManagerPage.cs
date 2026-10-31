using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Graphics;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace AgbStudio.Editor.Windows.Palette;

/// <summary>
/// Palette Manager (T-P10, 1440 x 900; Tools › Palette Manager, Assets › Optimize Palettes…, Create › Palette,
/// Sprite Editor › Palette slots). Shows the real BG and OBJ banks of the project with their used-by lists, both
/// palette RAMs, a fade preview, the colour editor with the 15-bit note, the Tools (merge near-duplicates, sort by hue,
/// lock index 0, import .pal / .gpl, export) and the palette effects that open the Palette Effect popover (T-O10).
/// Args: a <see cref="PaletteBank"/> or an asset path to select its bank.
/// </summary>
[EditorWindow("T-P10")]
public sealed class PaletteManagerPage : DockPanel
{
    public const string DocumentName = "Palettes";

    readonly EditorServices _s;
    readonly ArtLibrary? _lib;
    readonly EditorDocument? _doc;
    readonly StackPanel _tree = new() { Spacing = 1, Margin = new Thickness(6, 4, 6, 4), Name = "BankTree" };
    readonly TextBlock _title = UI.Text("", "Title/14", "text/primary").Named("BankTitle");
    readonly TextBlock _usedBy = UI.Text("", "Label/11", "text/muted").Named("BankUsedBy");
    readonly Swatch[] _swatches;
    readonly PaletteRamView _bgRam = new() { Name = "BgRam" }, _objRam = new() { Name = "ObjRam" };
    readonly Rectangle[] _fadeShade = new Rectangle[3];
    readonly TextBlock[] _fadePct = new TextBlock[3];
    readonly SectionHeader _colourHeader = new() { Title = "Colour 0", Glyph = "pipette", Name = "ColourHeader" };
    readonly ColourEditor _colour = new(withNote: true);
    readonly CheckBox _merge = new() { Content = "Merge near-duplicate colours", Height = 16, Name = "MergeNear" };
    readonly CheckBox _sort = new() { Content = "Sort by hue", Height = 16, Name = "SortByHue" };
    readonly CheckBox _lock = new() { Content = "Lock index 0 as transparent", Height = 16, Name = "LockTransparent" };
    readonly AgbButton _import = new() { Content = "Import .pal / .gpl", Glyph = "upload", Name = "ImportPal" };
    readonly AgbButton _export = new() { Content = "Export", Name = "ExportPal" };
    readonly InspectorField _fade = new() { Kind = FieldKind.Dropdown, Label = "Fade", Name = "FadeField" };
    readonly InspectorField _cycle = new() { Kind = FieldKind.Dropdown, Label = "Cycle", Name = "CycleField" };
    readonly InspectorField _flash = new() { Kind = FieldKind.Toggle, Label = "Flash on hit", Name = "FlashField" };
    readonly IconButton _cursorTool = new() { Glyph = "cursor", Name = "Tool_cursor", IsActive = true };
    readonly IconButton _pipetteTool = new() { Glyph = "pipette", Name = "Tool_pipette" };
    readonly Chip _countChip = new() { Glyph = "sliders", Name = "BankCountChip" };
    readonly StatusItem _statusBank = PageParts.Status("Status_Bank", "sliders");
    readonly HashSet<BankKind> _closed = [];
    AgbSwitch? _flashSwitch;
    bool _updating;

    public PaletteBank? Bank { get; private set; }
    public int Index { get; private set; } = 1;
    public ArtLibrary? Library => _lib;
    public ColourEditor Colour => _colour;
    public bool PipetteMode => _pipetteTool.IsActive;

    /// <summary>File pickers (tests replace them).</summary>
    public Func<Task<string?>> PickImportFile { get; set; }
    public Func<string, Task<string?>> PickExportFile { get; set; }

    public PaletteManagerPage(WindowContext ctx) : this(ctx.Services, ctx.Args) { }

    public PaletteManagerPage(EditorServices s, object? args = null)
    {
        _s = s;
        Name = "PaletteManager";
        PickImportFile = () => s.System.PickFileAsync("Import palette", ("Palettes", ["*.pal", "*.gpl"]));
        PickExportFile = name => s.System.PickSaveFileAsync("Export palette", name, "pal");
        _swatches = Enumerable.Range(0, 16).Select(i => new Swatch { Width = 40, Height = 40, Index = i.ToString(System.Globalization.CultureInfo.InvariantCulture), Name = "BankSwatch_" + i }).ToArray();
        if (s.Project.Current != null)
        {
            _lib = ArtLibrary.For(s);
            _doc = s.Documents.Open.FirstOrDefault(d => d.Name == DocumentName)
                   ?? s.Documents.Add(new EditorDocument { Name = DocumentName, Glyph = "sliders" });
            _doc.SaveHandler = _ => { _lib.SaveAll(); return Task.CompletedTask; };
            s.Documents.Active = _doc;
            Bank = args switch
            {
                PaletteBank b => b,
                string path => _lib.BankOf(path),
                _ => null,
            } ?? _lib.Of(BankKind.Obj).FirstOrDefault() ?? _lib.Banks.FirstOrDefault();
        }

        // ---- toolbar ----
        _cursorTool.Click += (_, _) => SetPipette(false);
        _pipetteTool.Click += (_, _) => SetPipette(true);
        ToolTip.SetTip(_cursorTool, new TipContent("Select"));
        ToolTip.SetTip(_pipetteTool, new TipContent("Pick colour"));
        var toolbar = PageParts.Toolbar([PageParts.ToolGroup(_cursorTool, _pipetteTool), _countChip], PageParts.PlayControls(s), [PageParts.VideoModeChip(s), PageParts.UndoRedo(s)]);
        SetDock(toolbar, Dock.Top);
        Children.Add(toolbar);

        // ---- status bar ----
        var ready = PageParts.Status("Status_Ready", "check", "Ready", dot: true);
        var status = PageParts.StatusBar([ready, _statusBank], [PageParts.Status("Status_Ram", "chip", "Palette RAM 1 KB")]);
        SetDock(status, Dock.Bottom);
        Children.Add(status);

        // ---- body ----
        var body = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("260,*,320"), Name = "Body" };
        var addBank = new PanelHeader { Title = "Palette Banks", Glyph = "sliders", ActionGlyph = "plus", Name = "BanksHeader" };
        addBank.ActionCommand = new Relay(() => AddBank());
        var left = new DockPanel();
        SetDock(addBank, Dock.Top);
        left.Children.Add(addBank);
        left.Children.Add(new ScrollViewer { Content = _tree, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });
        var leftPanel = UI.Frame(left, "bg/panel", "border/default", new Thickness(0, 0, 1, 0)).Named("BanksPanel");
        leftPanel.ClipToBounds = true;
        body.Children.Add(leftPanel);

        var center = new DockPanel { Name = "Center" };
        var tabs = PageParts.DocTabs(s, this);
        tabs.Height = 34;
        SetDock(tabs, Dock.Top);
        center.Children.Add(tabs);
        center.Children.Add(UI.Frame(new ScrollViewer { Content = BuildCenter() }, "bg/base").Named("PaletteCanvas"));
        Grid.SetColumn(center, 1);
        body.Children.Add(center);

        var right = new DockPanel();
        var colourHeader = new PanelHeader { Title = "Colour", Glyph = "pipette", Name = "ColourPanelHeader" };
        SetDock(colourHeader, Dock.Top);
        right.Children.Add(colourHeader);
        right.Children.Add(new ScrollViewer { Content = BuildRight(), HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });
        var rightPanel = UI.Frame(right, "bg/panel", "border/default", new Thickness(1, 0, 0, 0)).Named("ColourPanel");
        rightPanel.ClipToBounds = true;
        Grid.SetColumn(rightPanel, 2);
        body.Children.Add(rightPanel);
        Children.Add(body);

        if (_lib != null)
        {
            Action changed = () => { if (_doc != null && _lib.IsDirty) _doc.IsDirty = true; Refresh(); };
            AttachedToVisualTree += (_, _) => { _lib.Changed += changed; if (_doc != null) _s.Documents.Active = _doc; Refresh(); };
            DetachedFromVisualTree += (_, _) => _lib.Changed -= changed;
        }
        Refresh();
    }

    // ---- layout --------------------------------------------------------------------------------------------

    Control BuildCenter()
    {
        var title = UI.Row(11, _title, _usedBy);
        var swatchRow = UI.Row(8, _swatches);
        for (var i = 0; i < 16; i++)
        {
            var k = i;
            _swatches[i].PointerPressed += (_, e) => { SelectIndex(k); e.Handled = true; };
        }
        _bgRam.CellClicked += (b, i) => RamClicked(BankKind.Bg, b, i);
        _objRam.CellClicked += (b, i) => RamClicked(BankKind.Obj, b, i);

        var fade = UI.Column(6);
        for (var i = 0; i < 3; i++)
        {
            var art = new ArtImage { Source = "art/scene/day", Width = 180, Height = 120 };
            _fadeShade[i] = new Rectangle { Width = 180, Height = 120 };
            var frame = new Border { Width = 180, Height = 120, CornerRadius = new CornerRadius(6), ClipToBounds = true, Child = new Panel { Children = { art, _fadeShade[i] } }, Name = "FadePreview_" + i };
            _fadePct[i] = UI.Text("", "Mono/11", "text/muted").Named("FadePercent_" + i);
            fade.Children.Add(UI.Row(8, frame, _fadePct[i]));
        }

        var columns = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 24, Margin = new Thickness(0, 17, 0, 0) };
        void Col(string overline, Control content, double width)
        {
            var title = UI.Text(overline, "Overline/11", "text/secondary");
            title.TextWrapping = TextWrapping.NoWrap;
            var col = UI.Column(6, title, content);
            col.Width = width;
            col.ClipToBounds = false;
            columns.Children.Add(col);
        }
        Col("Background palette RAM", _bgRam, 270);
        Col("Sprite palette RAM", _objRam, 270);
        Col("Fade preview", fade, double.NaN);

        var stack = new StackPanel { Margin = new Thickness(16, 14, 16, 16) };
        swatchRow.Margin = new Thickness(0, 10, 0, 0);
        stack.Children.AddRange([title, swatchRow, columns]);
        return stack;
    }

    Control BuildRight()
    {
        _colour.ColourEdited += argb => { if (Bank != null && _lib != null) _lib.SetColor(Bank, Index, argb); };
        var colour = PageParts.Section(_colourHeader, _colour);

        foreach (var (box, option) in new[] { (_merge, nameof(PaletteBank.MergeNear)), (_sort, nameof(PaletteBank.SortByHue)), (_lock, nameof(PaletteBank.LockTransparent)) })
            box.IsCheckedChanged += (_, _) => { if (!_updating && Bank != null) _lib?.SetOption(Bank, option, box.IsChecked == true); };
        _import.Click += async (_, _) => { if (await PickImportFile() is { } path) await ImportThroughDialogAsync(path); };
        _export.Click += async (_, _) => { if (Bank != null && await PickExportFile(GameProjectName(Bank) + ".pal") is { } path) Export(path); };
        var buttons = UI.Row(8, _import, _export);
        var tools = PageParts.Section(new SectionHeader { Title = "Tools", Glyph = "spark", Name = "ToolsHeader" }, UI.Column(6, _merge, _sort, _lock, buttons));

        _fade.AddHandler(Button.ClickEvent, (_, e) => { OpenEffect(EffectKind.Fade, _fade); e.Handled = true; });
        _cycle.AddHandler(Button.ClickEvent, (_, e) => { OpenEffect(EffectKind.Cycle, _cycle); e.Handled = true; });
        // the switch inside the Toggle field: its state follows the effect, a click changes the effect
        _flash.TemplateApplied += (_, e) =>
        {
            _flashSwitch = e.NameScope.Find<AgbSwitch>("PART_Toggle");
            if (_flashSwitch is null) return;
            _flashSwitch.IsChecked = _lib?.Effects.Flash.Enabled ?? true;
            _flashSwitch.IsCheckedChanged += (_, _) =>
            {
                var on = _flashSwitch.IsChecked == true;
                if (!_updating && _lib != null && _lib.Effects.Flash.Enabled != on) _lib.SetEffects(fx => fx.Flash.Enabled = on);
            };
        };
        var effects = PageParts.Section(new SectionHeader { Title = "Palette effects", Glyph = "film", Name = "EffectsHeader" }, UI.Column(6, _fade, _cycle, _flash));
        return UI.Column(0, colour, tools, effects);
    }

    static string GameProjectName(PaletteBank b) => (b.Kind == BankKind.Obj ? "obj" : "bg") + b.Slot + "_" + new string(b.Name.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    // ---- state -----------------------------------------------------------------------------------------------

    public void Refresh()
    {
        _updating = true;
        try
        {
            RefreshTree();
            var lib = _lib;
            var bgCount = lib?.Of(BankKind.Bg).Count() ?? 0;
            var objCount = lib?.Of(BankKind.Obj).Count() ?? 0;
            _countChip.Content = $"BG {bgCount} / 16 · OBJ {objCount} / 16";
            for (var i = 0; i < 16; i++)
            {
                _bgRam.Banks[i] = lib?.Find(BankKind.Bg, i)?.Colors;
                _objRam.Banks[i] = lib?.Find(BankKind.Obj, i)?.Colors;
            }
            _bgRam.SelectedBank = Bank?.Kind == BankKind.Bg ? Bank.Slot : -1;
            _objRam.SelectedBank = Bank?.Kind == BankKind.Obj ? Bank.Slot : -1;
            _bgRam.Refresh();
            _objRam.Refresh();

            var b = Bank;
            _title.Text = b?.Label ?? "";
            _usedBy.Text = b is null || b.Assets.Count == 0 ? "" : "used by " + ArtLibrary.UsedBy(b);
            if (b != null) Index = Math.Clamp(Index, 0, Math.Max(0, b.Colors.Count - 1));
            for (var i = 0; i < 16; i++)
            {
                var sw = _swatches[i];
                if (b is null || i >= b.Colors.Count) { sw.State = SwatchState.Empty; sw.Color = Colors.Transparent; }
                else if (b.IsTransparentIndex(i)) sw.State = i == Index ? SwatchState.Selected : SwatchState.Transparent;
                else
                {
                    sw.State = i == Index ? SwatchState.Selected : SwatchState.Default;
                    sw.Color = Colour15.ToColor(b.Colors[i]);
                }
            }
            _colourHeader.Title = $"Colour {Index}";
            if (b != null && Index < b.Colors.Count) _colour.Show(b.Colors[Index], !b.IsTransparentIndex(Index));
            else _colour.Show(Colour15.Transparent, false);
            _merge.IsChecked = b?.MergeNear == true;
            _sort.IsChecked = b?.SortByHue == true;
            _lock.IsChecked = b?.LockTransparent != false;
            _import.IsEnabled = _export.IsEnabled = b != null;

            var fx = lib?.Effects ?? new PaletteEffectsData();
            _fade.Value = PaletteEffects.FadeText(fx.Fade);
            _cycle.Value = PaletteEffects.CycleText(fx.Cycle);
            _flash.IsOn = fx.Flash.Enabled;
            if (_flashSwitch != null) _flashSwitch.IsChecked = fx.Flash.Enabled;
            for (var i = 0; i < 3; i++)
            {
                var frames = Math.Max(1, fx.Fade.Frames);
                var frame = i * frames / 3;
                _fadePct[i].Text = $"{(frames - frame) * 100 / frames}%";
                _fadeShade[i].Fill = PageParts.DataBrush(Colour15.Argb((ushort)fx.Fade.Color));
                _fadeShade[i].Opacity = PaletteEffects.FadeAmount(frame, frames) / 16.0;
            }
            _statusBank.Content = b is null ? "" : $"{b.Id} · {b.Used} / 16 colours";
            _statusBank.IsVisible = b != null;
        }
        finally { _updating = false; }
    }

    void RefreshTree()
    {
        _tree.Children.Clear();
        if (_lib is null) return;
        foreach (var kind in new[] { BankKind.Bg, BankKind.Obj })
        {
            var banks = _lib.Of(kind).ToList();
            var open = !_closed.Contains(kind);
            var group = new TreeItem
            {
                Label = kind == BankKind.Bg ? "Background (BG)" : "Sprites (OBJ)", Glyph = kind == BankKind.Bg ? "image" : "user",
                Toggle = open ? TreeToggle.Open : TreeToggle.Closed, Meta = $"{banks.Count} / 16", Name = "Group_" + kind,
            };
            group.Activated += (_, _) => { if (!_closed.Remove(kind)) _closed.Add(kind); Refresh(); };
            _tree.Children.Add(group);
            if (!open) continue;
            foreach (var b in banks)
            {
                var item = new TreeItem { Label = b.Label, Glyph = "sliders", Meta = b.UsedText, Depth = 1, IsSelected = b == Bank, Name = $"Bank_{b.Id.Replace(' ', '_')}" };
                var bank = b;
                item.Activated += (_, _) => SelectBank(bank);
                _tree.Children.Add(item);
            }
        }
    }

    public void SelectBank(PaletteBank bank, int? index = null)
    {
        Bank = bank;
        Index = index ?? (bank.Colors.Count > 1 ? 1 : 0);
        Refresh();
    }

    public void SelectIndex(int i)
    {
        if (Bank is null || i >= Bank.Colors.Count) return;
        Index = i;
        Refresh();
    }

    void RamClicked(BankKind kind, int slot, int index)
    {
        if (_lib?.Find(kind, slot) is not { } bank || index >= bank.Colors.Count) return;
        if (PipetteMode && Bank != null && !Bank.IsTransparentIndex(Index) && !bank.IsTransparentIndex(index))
        {
            // pick: copy the clicked colour into the selected index of the current bank
            _lib.SetColor(Bank, Index, bank.Colors[index]);
            return;
        }
        SelectBank(bank, index);
    }

    void SetPipette(bool on)
    {
        _pipetteTool.IsActive = on;
        _cursorTool.IsActive = !on;
    }

    /// <summary>Palette Banks +: a new bank of the selected bank's kind (OBJ when none is selected).</summary>
    public PaletteBank? AddBank()
    {
        if (_lib?.AddBank(Bank?.Kind ?? BankKind.Obj) is not { } b) return null;
        SelectBank(b, 0);
        return b;
    }

    /// <summary>
    /// Import .pal / .gpl (T-C29): the picked file goes through Import Asset (T-D04), which copies it into
    /// assets/palettes; its colours then go into the selected bank (one undo step).
    /// </summary>
    public async Task<bool> ImportThroughDialogAsync(string path)
    {
        var written = await _s.Windows.Open("T-D04", new AgbStudio.Editor.Windows.Import.ImportAssetDialog.ImportRequest([path]), _import, source: "PaletteImport") as IReadOnlyList<string>;
        if (written?.FirstOrDefault(f => System.IO.Path.GetExtension(f).ToLowerInvariant() is ".pal" or ".gpl") is not { } copied) return false;
        Import(copied);
        return true;
    }

    public void Import(string path)
    {
        if (Bank is null || _lib is null) return;
        try { _lib.ImportColors(Bank, path); }
        catch (Exception e) when (e is IOException or InvalidDataException or FormatException or IndexOutOfRangeException)
        {
            _s.Toasts.Show($"Could not import {System.IO.Path.GetFileName(path)}: {e.Message.TrimEnd('.')}. Palettes must be JASC .pal or GIMP .gpl files.", ToastStatus.Error);
        }
    }

    public void Export(string path)
    {
        if (Bank is null) return;
        ArtLibrary.Export(Bank, path);
        _s.Toasts.Show($"Exported {System.IO.Path.GetFileName(path)}");
    }

    void OpenEffect(EffectKind kind, Control anchor) => _ = _s.Windows.Open("T-O10", kind, anchor, source: kind == EffectKind.Fade ? "FadeField" : "CycleField");

    // ---- commands of the Assets menu and Create submenu ---------------------------------------------------------

    /// <summary>Create › Palette: a new OBJ bank, shown in the Palette Manager.</summary>
    [CommandHandler("assets.create.palette")]
    public static void CreatePalette(EditorServices s)
    {
        if (s.Project.Current is null) { _ = s.Windows.Open("T-P10", source: "assets.create.palette"); return; }
        var bank = ArtLibrary.For(s).AddBank(BankKind.Obj);
        _ = s.Windows.Open("T-P10", bank, source: "assets.create.palette");
    }

    /// <summary>Assets › Optimize Palettes…: removes unused and near-duplicate colours in every bank (one undo step) and opens the Palette Manager.</summary>
    [CommandHandler("assets.optimizePalettes")]
    public static void OptimizePalettes(EditorServices s)
    {
        if (s.Project.Current != null)
        {
            var removed = ArtLibrary.For(s).Optimize();
            s.Toasts.Show(removed == 1 ? "Palettes optimized · 1 colour removed" : $"Palettes optimized · {removed} colours removed");
        }
        _ = s.Windows.Open("T-P10", source: "assets.optimizePalettes");
    }
}
