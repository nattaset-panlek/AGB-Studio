using System.Globalization;
using AgbStudio.Build.Fonts;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Windows.Tilemap;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace AgbStudio.Editor.Windows.Font;

/// <summary>
/// Font Manager (T-P09, 1440 x 900; from a font asset, Tools › Font Manager, Create › Font). Shows the project's real
/// fonts as FontBuilder builds them for the ROM: font list (+ New Font), character sets, Add characters from text…,
/// the glyph grid with code points (missing glyphs flagged), the glyph pixels (editable), font settings (source, size,
/// width, metrics, Thai layout, rendering), a dialogue-box preview drawn with the ROM's TextLayout / TextRenderer,
/// and ROM / VRAM numbers. Every change is undoable and saved to the .font file.
/// </summary>
[EditorWindow("T-P09")]
public sealed class FontManagerPage : DockPanel
{
    readonly EditorServices _s;
    readonly FontLibrary _lib;
    readonly List<Action> _refresh = [];
    readonly IconButton[] _tools;
    readonly Chip _summary;
    readonly StackPanel _fontList = new() { Spacing = 1, Margin = new Thickness(6, 4, 6, 0), Name = "FontList" };
    readonly WrapPanel _glyphs = new() { ItemSpacing = 6, LineSpacing = 6, Name = "GlyphGrid" };
    readonly TextBlock _groupText;
    readonly TextBlock _detailHeader;
    readonly Image _preview = new() { Width = 480, Height = 320, Name = "Preview", HorizontalAlignment = HorizontalAlignment.Left };
    int _group;

    public FontEntry? Font { get; private set; }
    public int SelectedGlyph { get; private set; }
    public GlyphEditor Editor { get; } = new();
    public IReadOnlyList<GlyphGroup> Groups => Font is null ? [] : Font.GroupsWithText();
    public GlyphGroup? Group => _group < Groups.Count ? Groups[_group] : null;

    public static readonly string[] ToolGlyphs = ["pencil", "eraser", "pipette", "grid"];
    static readonly string[] ToolTitles = ["Pencil", "Eraser", "Pick shade", "Pixel grid"];
    static readonly string[] SetNames = ["Basic Latin", "Thai", "Symbols", "Japanese kana"];
    static readonly string[] ColourChoices = ["2bpp · 4 shades", "1bpp · 2 shades"];

    public FontManagerPage(WindowContext ctx) : this(ctx.Services, ctx.Args) { }

    public FontManagerPage(EditorServices s, object? args = null)
    {
        _s = s;
        _lib = FontLibrary.Of(s);
        Name = "FontManager";
        LastChildFill = true;

        _tools = ToolGlyphs.Select((g, i) => new IconButton { Glyph = g, Name = "Tool_" + g }).ToArray();
        for (var i = 0; i < _tools.Length; i++)
        {
            var k = i;
            ToolTip.SetTip(_tools[i], new TipContent(ToolTitles[i]));
            _tools[i].Click += (_, _) =>
            {
                if (k == 3) { Editor.ShowGrid = !Editor.ShowGrid; return; }
                Editor.Tool = (FontTool)k;
                Refresh();
            };
        }
        Editor.ShadePicked += () => { Editor.Tool = FontTool.Pencil; Refresh(); };
        _summary = new Chip { Glyph = "type", Name = "FontSummaryChip" };
        Children.Add(PageChrome.Toolbar(s, [PageChrome.ToolGroup(_tools), _summary], MapLibrary.Of(s).SceneMap()?.Scene?.VideoMode ?? 0));

        var glyphCount = new StatusItem { Name = "Status_Glyphs" };
        glyphCount.Click += (_, _) => ShowMissing();
        var rom = new StatusItem { Glyph = "chip", Name = "Status_Rom" };
        var saved = new StatusItem { Glyph = "check", Content = "Saved", Name = "Status_Saved" };
        Children.Add(PageChrome.StatusBar([PageChrome.ReadyItem(s.Status), glyphCount], [rom, saved]));
        _refresh.Add(() =>
        {
            var f = Font;
            glyphCount.IsVisible = rom.IsVisible = f != null;
            saved.IsVisible = f is { IsDirty: false };
            if (f is null) return;
            var missing = f.AllMissing.Count;
            glyphCount.Content = $"{f.GlyphCount} glyphs · {missing} missing";
            glyphCount.Glyph = missing > 0 ? "warn" : "type";
            rom.Content = "ROM " + Kb(f.Font.RomBytes);
        });

        _groupText = UI.Text(null, "Label/11", "text/muted");
        _detailHeader = UI.Text(null, "Overline/11", "text/secondary");
        _detailHeader.Name = "GlyphDetailHeader";

        var body = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("260,*,320"), Name = "Body" };
        body.Children.Add(LeftPanel());
        var center = new Grid { RowDefinitions = RowDefinitions.Parse("34,*"), Name = "Center" };
        Grid.SetColumn(center, 1);
        center.Children.Add(PageChrome.DocTabs(s));
        var content = Center();
        Grid.SetRow(content, 1);
        center.Children.Add(content);
        body.Children.Add(center);
        var right = RightPanel();
        Grid.SetColumn(right, 2);
        body.Children.Add(right);
        Children.Add(body);

        PageChrome.WhileAttached(this, () =>
        {
            _attached = true;
            // the project's strings may have changed while another page was shown
            Font?.ReloadText();
            _lib.ListChanged += Refresh;
            Listen(Font, true);
            if (Font is { } current) _s.System.Selection = () => current.Path;
            Refresh();
        }, () =>
        {
            _attached = false;
            _lib.ListChanged -= Refresh;
            Listen(Font, false);
        });
        if (_lib.Resolve(args) is { } font) SwitchTo(font);
        else Refresh();
    }

    bool _attached;

    void Listen(FontEntry? f, bool on)
    {
        if (f is null) return;
        if (on) { f.Changed += Refresh; f.DirtyChanged += Refresh; }
        else { f.Changed -= Refresh; f.DirtyChanged -= Refresh; }
    }

    public static string Kb(int bytes) => (bytes / 1024.0).ToString("0.0", CultureInfo.InvariantCulture) + " KB";

    public static string Meta(FontFile f) => f.Variable ? $"VWF · {f.Size}px" : $"{f.FixedWidth}×{f.Size} fixed";

    public static string Summary(FontFile f) => f.Variable ? $"VWF · {f.Size} px · {f.Bpp}bpp" : $"Fixed {f.FixedWidth}×{f.Size} · {f.Bpp}bpp";

    /// <summary>Shows a font (font list click, New Font, a .font Doc Tab).</summary>
    public void SwitchTo(FontEntry font)
    {
        if (_attached) Listen(Font, false);
        Font = _lib.Select(font);
        font.ReloadText();
        if (_attached) Listen(font, true);
        var groups = Groups;
        _group = Math.Max(0, groups.ToList().FindIndex(g => g.Name == "Thai consonants"));
        SelectedGlyph = Group?.CodePoints.FirstOrDefault() ?? 0;
        _s.System.Selection = () => font.Path;
        Refresh();
    }

    public void SelectGroup(int index)
    {
        _group = Math.Clamp(index, 0, Math.Max(0, Groups.Count - 1));
        SelectedGlyph = Group?.CodePoints.FirstOrDefault() ?? 0;
        Refresh();
    }

    /// <summary>The status item "N missing": shows the glyphs the project's text needs (else the first missing one's group).</summary>
    public void ShowMissing()
    {
        if (Font is not { } f || f.AllMissing is not [var first, ..]) return;
        var groups = Groups.ToList();
        var i = groups.FindIndex(g => g.Name == FontEntry.TextMissingGroup);
        if (i < 0) i = groups.FindIndex(g => g.CodePoints.Contains(first));
        if (i >= 0) SelectGroup(i);
    }

    public void SelectGlyph(int cp)
    {
        SelectedGlyph = cp;
        Refresh();
    }

    void Refresh()
    {
        if (Font != null && _group >= Groups.Count) _group = 0;
        for (var i = 0; i < 3; i++) _tools[i].IsActive = Font != null && (int)Editor.Tool == i;
        _summary.Content = Font is null ? null : Summary(Font.File);
        _summary.IsVisible = Font != null;
        foreach (var r in _refresh) r();
    }

    // ---- left: fonts and character sets -------------------------------------------------------------

    Control LeftPanel()
    {
        var header = new PanelHeader { Title = "Fonts", Glyph = "type", ActionGlyph = "plus", Name = "FontsHeader" };
        header.ActionCommand = new Relay(() => _ = NewFont(header));
        var setsHeader = new PanelHeader { Title = "Character sets", Glyph = "globe", Name = "CharacterSetsHeader", Margin = new Thickness(0, 4, 0, 0) };
        var sets = new StackPanel { Margin = new Thickness(12, 5, 12, 0), Name = "CharacterSets" };
        var boxes = SetNames.Select(n =>
        {
            var box = new CheckBox { Height = 22, Name = "Set_" + n.Replace(" ", "") };
            box.IsCheckedChanged += (_, _) =>
            {
                if (Font is null || box.IsChecked is not { } on || on == Font.File.CharacterSets.Contains(n)) return;
                Font.Edit(on ? "Add character set" : "Remove character set", f =>
                {
                    if (on) f.CharacterSets.Add(n);
                    else f.CharacterSets.RemoveAll(x => x == n);
                    f.CharacterSets = SetNames.Where(f.CharacterSets.Contains).Concat(f.CharacterSets.Where(x => !SetNames.Contains(x))).ToList();
                });
            };
            sets.Children.Add(box);
            return (Name: n, Box: box);
        }).ToList();
        var add = new Button { Theme = PageChrome.Bare, Name = "AddCharacters", Height = 26, Margin = new Thickness(23, 3, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
        add.Content = UI.Row(8, PageChrome.Glyph("plus", 12, "text/secondary"), UI.Text("Add characters from text…", "Body/12", "text/secondary"));
        add.Click += (_, _) => _ = AddCharacters(add);
        var stack = new StackPanel { Children = { _fontList, setsHeader, sets, add } };
        _refresh.Add(() =>
        {
            _fontList.Children.Clear();
            var def = _s.Project.Current?.Settings.General.DefaultFont;
            foreach (var f in _lib.Fonts.OrderBy(f => f.Name != def))
            {
                var item = new TreeItem { Label = f.Name, Glyph = "type", Meta = Meta(f.File), IsSelected = f == Font, Name = "Font_" + f.Name };
                var entry = f;
                item.Activated += (_, _) => SwitchTo(entry);
                _fontList.Children.Add(item);
            }
            foreach (var (n, box) in boxes)
            {
                var on = Font?.File.CharacterSets.Contains(n) == true;
                box.IsChecked = on;
                box.Content = $"{n} · {(on ? FontBuilder.CharacterSets[n].Count : 0)}";
                box.IsEnabled = Font != null;
            }
            add.IsEnabled = Font != null;
        });
        return PageChrome.SidePanel(header, stack, true, "FontsPanel");
    }

    async Task NewFont(Control anchor)
    {
        if (await _s.Windows.Open("T-D15", null, anchor, source: "FontsPlus") is FontEntry created) SwitchTo(created);
    }

    async Task AddCharacters(Control anchor)
    {
        if (Font is null) return;
        var font = Font;
        if (await _s.Windows.Open("T-D16", font, anchor, source: "AddCharacters") is IReadOnlyList<int> added && added.Count > 0 && Font == font)
        {
            var g = Groups.ToList().FindIndex(gr => gr.CodePoints.Contains(added[0]));
            if (g >= 0) _group = g;
            SelectGlyph(added[0]);
        }
    }

    // ---- centre: glyphs, detail, preview ------------------------------------------------------------

    Control Center()
    {
        var title = UI.Text("Glyphs", "Title/14", "text/primary");
        var groupButton = new Button { Theme = PageChrome.Bare, Content = _groupText, Name = "GlyphGroupButton", VerticalAlignment = VerticalAlignment.Center };
        groupButton.Click += (_, _) => PageChrome.Choose(groupButton, Groups.Select(g => g.Name).ToList(), Group?.Name, SelectGroup);
        var titleRow = UI.Row(8, title, groupButton);
        var grid = new ScrollViewer { Content = _glyphs, MaxHeight = 156, Margin = new Thickness(0, 10, 0, 0), Name = "GlyphScroll", VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
        var detail = UI.Column(6, _detailHeader, Editor);
        detail.MinWidth = 180;
        var previewHeader = UI.Text("Preview · Dialogue box on GBA", "Overline/11", "text/secondary");
        RenderOptions.SetBitmapInterpolationMode(_preview, BitmapInterpolationMode.None);
        var lower = UI.Row(16, detail, UI.Column(6, previewHeader, _preview));
        lower.Margin = new Thickness(0, 16, 0, 0);
        var stack = new StackPanel { Margin = new Thickness(16, 14, 16, 16), Children = { titleRow, grid, lower } };
        var scroll = new ScrollViewer { Content = stack, Name = "FontCenter", HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        var frame = UI.Frame(scroll, "bg/base");
        _refresh.Add(() =>
        {
            var f = Font;
            _glyphs.Children.Clear();
            _groupText.Text = Group is { } gr ? $"{gr.Name} · click a glyph to edit" : null;
            if (f is null) { _preview.Source = null; _detailHeader.Text = null; return; }
            var font = f.Font;
            foreach (var cp in Group?.CodePoints ?? [])
            {
                var missing = font.Missing.Contains(cp) || font.Get(cp) is null;
                var cell = new GlyphCell
                {
                    Char = char.ConvertFromUtf32(cp), Code = cp.ToString("X4", CultureInfo.InvariantCulture), Name = "Glyph_" + cp.ToString("X4", CultureInfo.InvariantCulture),
                    State = missing ? GlyphCellState.Missing : cp == SelectedGlyph ? GlyphCellState.Selected : GlyphCellState.Default,
                };
                var c = cp;
                cell.PointerPressed += (_, e) => { SelectGlyph(c); e.Handled = true; };
                _glyphs.Children.Add(cell);
            }
            var g = font.Get(SelectedGlyph);
            _detailHeader.Text = $"{char.ConvertFromUtf32(SelectedGlyph)} · U+{SelectedGlyph:X4} · Advance {g?.Advance ?? 0}";
            Editor.Load(f, SelectedGlyph);
            var lib = MapLibrary.Of(_s);
            var ui = _s.Project.Current?.Settings.Ui ?? new UiSettings();
            _preview.Source = Pixels.ToBitmap(TextPreview.Dialogue(f, lib.SceneMap(), lib.Renderer, ui), 240, 160);
        });
        return frame;
    }

    // ---- right: font settings -----------------------------------------------------------------------

    Control RightPanel()
    {
        var header = new PanelHeader { Title = "Font settings", Glyph = "sliders", Name = "FontSettingsHeader" };
        var dock = new DockPanel();

        var rom = new Meter { Label = "ROM", Name = "Meter_ROM" };
        var vram = new Meter { Label = "VRAM text", Name = "Meter_VRAMtext" };
        var bottom = UI.Frame(UI.Column(7, rom, vram), "bg/sunken", pad: new Thickness(12, 10, 12, 11)).Named("FontBudget");
        DockPanel.SetDock(bottom, Dock.Bottom);
        dock.Children.Add(bottom);

        // Source
        var from = PageChrome.Field("From", FieldKind.Dropdown, null, "FromField");
        PageChrome.OnDropdown(from, () =>
        {
            if (Font is not { } f) return;
            var files = SourceFiles(f.Project);
            PageChrome.Choose(from, files, from.Value, i => f.Edit("Font source", ff =>
            {
                ff.Source = files[i];
                ff.SourceKind = SourceKindOf(files[i]);
            }));
        });
        var size = PageChrome.Field("Size", FieldKind.Text, null, "SizeField");
        PageChrome.OnCommit(size, () =>
        {
            if (Font is { } f && int.TryParse((size.Value ?? "").Replace("px", "").Trim(), out var px) && px is >= 4 and <= 64) f.Edit("Font size", ff => ff.Size = px);
            else Refresh();
        });
        var fixedSeg = new Segment { Content = "Fixed 8×8", GroupName = "FontWidth", Name = "Width_Fixed" };
        var varSeg = new Segment { Content = "Variable", GroupName = "FontWidth", Name = "Width_Variable" };
        fixedSeg.IsCheckedChanged += (_, _) => { if (fixedSeg.IsChecked == true && Font is { File.Variable: true } f) f.Edit("Fixed width", ff => { ff.Variable = false; ff.FixedWidth = 8; }); };
        varSeg.IsCheckedChanged += (_, _) => { if (varSeg.IsChecked == true && Font is { File.Variable: false } f) f.Edit("Variable width", ff => ff.Variable = true); };
        var segs = UI.Frame(UI.Row(2, fixedSeg, varSeg), "bg/base", "border/default", new Thickness(1), new Thickness(2), 7);
        segs.HorizontalAlignment = HorizontalAlignment.Left;
        var widthRow = new DockPanel { HorizontalSpacing = 8, Name = "WidthRow" };
        var widthLabel = UI.Text("Width", "Body/12", "text/secondary", 86);
        DockPanel.SetDock(widthLabel, Dock.Left);
        widthRow.Children.Add(widthLabel);
        widthRow.Children.Add(segs);
        var source = PageChrome.Section(new SectionHeader { Title = "Source", Glyph = "file", Name = "SourceHeader" }, [from, size, widthRow], 12, "SourceSection");

        // Metrics
        var heightBase = PageChrome.Field("Height · base", FieldKind.XY, null, "HeightBaseField");
        PageChrome.OnCommit(heightBase, () =>
        {
            if (Font is { } f && int.TryParse(heightBase.Value, out var h) && int.TryParse(heightBase.Value2, out var b) && h is >= 4 and <= 64 && b is >= 0 and <= 64)
                f.Edit("Font metrics", ff => { ff.Size = h; ff.Baseline = b; });
            else Refresh();
        });
        var spacingLine = PageChrome.Field("Spacing · line", FieldKind.XY, null, "SpacingLineField");
        PageChrome.OnCommit(spacingLine, () =>
        {
            if (Font is { } f && int.TryParse(spacingLine.Value, out var sp) && int.TryParse(spacingLine.Value2, out var lh) && sp is >= 0 and <= 16 && lh is >= 1 and <= 64)
                f.Edit("Font metrics", ff => { ff.Spacing = sp; ff.LineHeight = lh; });
            else Refresh();
        });
        var metrics = PageChrome.Section(new SectionHeader { Title = "Metrics", Glyph = "sliders", Name = "MetricsHeader" }, [heightBase, spacingLine], 12, "MetricsSection");

        // Thai layout
        CheckBox Toggle(string text, string name, Func<FontFile, bool> get, Action<FontFile, bool> set)
        {
            // Thai marks in the label need the Thai face for the whole cluster
            object content = text.Any(c => ThaiText.IsThai(c)) ? new TextBlock { Text = text, FontFamily = AgbStudio.Editor.Theme.Fonts.Thai, FontSize = 12, LineHeight = 16 } : text;
            var box = new CheckBox { Content = content, Height = 20, Name = name };
            box.IsCheckedChanged += (_, _) =>
            {
                if (Font is not { } f || box.IsChecked is not { } on || on == get(f.File)) return;
                f.Edit(text, ff => set(ff, on));
            };
            _refresh.Add(() => box.IsChecked = Font is { } f && get(f.File));
            return box;
        }
        var stack = Toggle("Stack upper vowels & tone marks", "StackMarks", f => f.StackUpperMarks, (f, v) => f.StackUpperMarks = v);
        var lower = Toggle("Place lower vowels (ุ)", "LowerVowels", f => f.PlaceLowerVowels, (f, v) => f.PlaceLowerVowels = v);
        var wrap = Toggle("Word-wrap Thai by dictionary", "ThaiWrap", f => f.ThaiWordWrap, (f, v) => f.ThaiWordWrap = v);
        var note = UI.Text("Thai has no spaces between words, so line breaks use a word list — text never splits mid-word.", "Label/11", "text/muted");
        note.TextWrapping = TextWrapping.Wrap;
        var checks = new StackPanel { Spacing = 2, Margin = new Thickness(0, -2, 0, 0), Children = { stack, lower, wrap } };
        note.Margin = new Thickness(0, -1, 0, 0);
        var thai = PageChrome.Section(new SectionHeader { Title = "Thai layout", Glyph = "globe", Name = "ThaiHeader" }, [checks, note], 12, "ThaiSection");

        // Rendering
        var colours = PageChrome.Field("Colours", FieldKind.Dropdown, null, "ColoursField");
        PageChrome.OnDropdown(colours, () => PageChrome.Choose(colours, ColourChoices, colours.Value, i => Font?.Edit("Colours", ff => ff.Bpp = i == 0 ? 2 : 1)));
        var shadow = PageChrome.Field("Drop shadow", FieldKind.Toggle, null, "ShadowField");
        shadow.PropertyChanged += (_, e) =>
        {
            if (e.Property == InspectorField.IsOnProperty && Font is { } f && f.File.DropShadow != shadow.IsOn) f.Edit("Drop shadow", ff => ff.DropShadow = shadow.IsOn);
        };
        var drawTo = PageChrome.Field("Draw to", FieldKind.Dropdown, null, "DrawToField");
        string[] layers = ["BG 0 text layer", "BG 1 text layer", "BG 2 text layer", "BG 3 text layer"];
        PageChrome.OnDropdown(drawTo, () => PageChrome.Choose(drawTo, layers, drawTo.Value, i => Font?.SetDrawTo(i)));
        var rendering = PageChrome.Section(new SectionHeader { Title = "Rendering", Glyph = "image", Name = "RenderingHeader" }, [colours, shadow, drawTo], 12, "RenderingSection");

        dock.Children.Add(new StackPanel { Name = "FontSettingsBody", Children = { source, metrics, thai, rendering } });

        _refresh.Add(() =>
        {
            if (Font is not { } f) return;
            var ff = f.File;
            from.Value = ff.Source;
            if (!size.IsKeyboardFocusWithin) size.Value = $"{ff.Size} px";
            fixedSeg.IsChecked = !ff.Variable;
            varSeg.IsChecked = ff.Variable;
            if (!heightBase.IsKeyboardFocusWithin) { heightBase.Value = ff.Size.ToString(CultureInfo.InvariantCulture); heightBase.Value2 = ff.Baseline.ToString(CultureInfo.InvariantCulture); }
            if (!spacingLine.IsKeyboardFocusWithin) { spacingLine.Value = ff.Spacing.ToString(CultureInfo.InvariantCulture); spacingLine.Value2 = ff.LineHeight.ToString(CultureInfo.InvariantCulture); }
            colours.Value = ff.Bpp == 1 ? ColourChoices[1] : ColourChoices[0];
            shadow.IsOn = ff.DropShadow;
            drawTo.Value = layers[f.DrawTo];
            var bytes = f.Font.RomBytes;
            rom.ValueText = Kb(bytes);
            rom.Percent = Math.Min(100, 100.0 * bytes / (32 * 1024));
            var box = TextVramBytes;
            vram.ValueText = $"{box / 1024} / 16 KB";
            vram.Percent = 100.0 * box / (16 * 1024);
        });
        return PageChrome.SidePanel(header, dock, false, "FontSettingsPanel");
    }

    /// <summary>Create › Font: the Font Manager with New Font (T-D15) on top; the new font is shown when it is created.</summary>
    [CommandHandler("assets.create.font")]
    public static async void CreateFont(EditorServices s)
    {
        await s.Windows.Open("T-P09", source: "assets.create.font");
        if (s.Project.Current is null) return;
        var page = s.Windows.Host?.CurrentPage as FontManagerPage;
        if (await s.Windows.Open("T-D15", null, source: "assets.create.font") is FontEntry created) page?.SwitchTo(created);
    }

    /// <summary>VRAM of the text layer's dialogue box: 28 x 5 tiles of 4bpp (runtime BoxStyle defaults).</summary>
    public const int TextVramBytes = 28 * 5 * 32;

    /// <summary>Font source files in fonts/ (TrueType / OpenType, BDF, PNG image sheets).</summary>
    public static IReadOnlyList<string> SourceFiles(GameProject p) =>
        Directory.Exists(p.FontsDir)
            ? Directory.EnumerateFiles(p.FontsDir).Select(Path.GetFileName).OfType<string>()
                .Where(f => f.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".otf", StringComparison.OrdinalIgnoreCase)
                            || f.EndsWith(".bdf", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList()
            : [];

    public static FontSourceKind SourceKindOf(string file) =>
        file.EndsWith(".bdf", StringComparison.OrdinalIgnoreCase) ? FontSourceKind.Bdf : file.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? FontSourceKind.Image : FontSourceKind.Ttf;
}
