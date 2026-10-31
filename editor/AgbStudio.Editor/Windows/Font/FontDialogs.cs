using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgbStudio.Build;
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
/// New Font (T-D15, 560 x 506; Font Manager › Fonts +): source kind, source file, size, width, character sets and a
/// preview drawn with the font FontBuilder makes from these settings, with its ROM estimate. Create Font writes
/// fonts/&lt;name&gt;.font (copying the default Thai font into fonts/ when it is chosen) and returns the new entry.
/// </summary>
[EditorWindow("T-D15")]
public sealed class NewFontDialog : FigmaDialog
{
    public const string DefaultThaiFont = "NotoSansThai-Regular.ttf";
    public const string Sample = "สวัสดี AGB Studio 0123";
    static readonly string[] Kinds = ["TTF / OTF", "BDF", "Image sheet"];
    static readonly string[] WidthChoices = ["Variable (VWF)", "Fixed 8×8"];
    static readonly (string Label, string Set)[] SetChoices = [("Latin", "Basic Latin"), ("Thai", "Thai"), ("Symbols", "Symbols")];

    readonly FontLibrary _lib;
    readonly GameProject? _project;
    readonly Segment[] _kinds;
    readonly CheckBox[] _sets;
    readonly Image _sample = new() { Name = "SamplePreview", HorizontalAlignment = HorizontalAlignment.Left };
    readonly TextBlock _caption = UI.Text(null, "Label/11", "text/secondary");
    readonly DialogFooter _footer;

    public SelectField File { get; } = new() { Label = "File", Name = "FileField" };
    public TextField Size { get; } = new() { Label = "Size", Text = "12 px", Name = "SizeField" };
    public SelectField WidthField { get; } = new() { Label = "Width", Selected = WidthChoices[0], Name = "WidthField" };
    public FontSourceKind Kind { get; private set; } = FontSourceKind.Ttf;
    public int RomBytes { get; private set; }
    public string Caption => _caption.Text ?? "";

    public NewFontDialog(WindowContext ctx) : this(FontLibrary.Of(ctx.Services), ctx.Services.Project.Current) { }

    public NewFontDialog(FontLibrary lib, GameProject? project)
    {
        _lib = lib;
        _project = project;
        Name = "NewFontDialog";
        _kinds = Kinds.Select((k, i) => new Segment { Content = k, GroupName = "NewFontKind", IsChecked = i == 0, Name = "Kind_" + i }).ToArray();
        for (var i = 0; i < _kinds.Length; i++)
        {
            var k = (FontSourceKind)i;
            _kinds[i].IsCheckedChanged += (_, _) => { if (_kinds[(int)k].IsChecked == true && Kind != k) { Kind = k; File.Selected = Files().FirstOrDefault(); Update(); } };
        }
        var kinds = UI.Frame(UI.Row(2, _kinds), "bg/base", "border/default", new Thickness(1), new Thickness(2), 7);
        kinds.HorizontalAlignment = HorizontalAlignment.Left;
        File.Selected = Files().FirstOrDefault(f => f == DefaultThaiFont) ?? Files().FirstOrDefault();
        PageChrome.OnDropdown(File, () => PageChrome.Choose(File, Files(), File.Selected, i => { File.Selected = Files()[i]; Update(); }));
        PageChrome.OnDropdown(WidthField, () => PageChrome.Choose(WidthField, WidthChoices, WidthField.Selected, i => { WidthField.Selected = WidthChoices[i]; Update(); }));
        Size.PropertyChanged += (_, e) => { if (e.Property == TextField.TextProperty) Update(); };
        var sizeRow = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("*,10,*") };
        Grid.SetColumn(WidthField, 2);
        sizeRow.Children.Add(Size);
        sizeRow.Children.Add(WidthField);
        _sets = SetChoices.Select(s => new CheckBox { Content = $"{s.Label} · {FontBuilder.CharacterSets[s.Set].Count}", IsChecked = true, Name = "NewFontSet_" + s.Label }).ToArray();
        foreach (var b in _sets) b.IsCheckedChanged += (_, _) => Update();
        // Figma: Latin, Thai and Symbols 16 px apart, the row 1 px lower than a bare column gap (boxes at x 21, 112, 199; y 306)
        var sets = UI.Row(16, _sets);
        sets.Margin = new Thickness(0, 1, 0, 0);
        _caption.Name = "PreviewCaption";
        RenderOptions.SetBitmapInterpolationMode(_sample, BitmapInterpolationMode.None);
        // Figma: a 62 px box in the GBA text box colour; the sample at the top, the caption 13 px above the bottom edge
        _sample.VerticalAlignment = VerticalAlignment.Top;
        DockPanel.SetDock(_caption, Dock.Bottom);
        var body = new DockPanel { Children = { _caption, _sample } };
        var preview = new Border { Height = 62, CornerRadius = new CornerRadius(6), Padding = new Thickness(12, 8, 12, 13), Name = "PreviewBox", Child = body };
        preview.Background = new SolidColorBrush(Avalonia.Media.Color.FromUInt32(PreviewPaper));
        var content = new StackPanel
        {
            Spacing = 14, Margin = new Thickness(20, 20, 20, 17),
            Children =
            {
                UI.Text("Source", "Overline/11", "text/secondary"), kinds, File, sizeRow,
                UI.Text("Characters", "Overline/11", "text/secondary"), sets,
                UI.Text("Preview", "Overline/11", "text/secondary"), preview,
            },
        };
        _footer = new DialogFooter { ConfirmText = "Create Font" };
        _footer.Cancel += (_, _) => Close(null);
        _footer.Confirm += (_, _) => { if (CreateFont() is { } e) Close(e); };
        Build("New Font", "type", content, _footer, 560);
        Update();
    }

    /// <summary>The paper of the sample: Figma draws every GBA text preview (New Font, Font Manager, dialogue) on #1B1630.</summary>
    const uint PreviewPaper = 0xFF1B1630;

    static uint TextPreviewColor(string? hex, uint fallback)
    {
        var h = hex?.TrimStart('#');
        return h is { Length: 6 } && uint.TryParse(h, NumberStyles.HexNumber, null, out var rgb) ? 0xFF000000u | rgb : fallback;
    }

    /// <summary>Source files for the chosen kind: files in fonts/, plus the bundled default Thai font for TrueType.</summary>
    public IReadOnlyList<string> Files()
    {
        var list = _project is null ? [] : FontManagerPage.SourceFiles(_project).Where(f => FontManagerPage.SourceKindOf(f) == Kind).ToList();
        if (Kind == FontSourceKind.Ttf && !list.Contains(DefaultThaiFont, StringComparer.OrdinalIgnoreCase) && System.IO.File.Exists(BundledThai)) list.Insert(0, DefaultThaiFont);
        return list;
    }

    static string BundledThai => Path.Combine(NativeBuild.RepoRoot, "third_party", "fonts", DefaultThaiFont);

    string? SourcePath(string? file)
    {
        if (file is null) return null;
        var inProject = _project is null ? null : Path.Combine(_project.FontsDir, file);
        if (inProject != null && System.IO.File.Exists(inProject)) return inProject;
        return file == DefaultThaiFont && System.IO.File.Exists(BundledThai) ? BundledThai : inProject;
    }

    public static string NameFor(string file, int size) =>
        new string(Path.GetFileNameWithoutExtension(file).ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray()).Trim('_') + "_" + size.ToString(CultureInfo.InvariantCulture);

    /// <summary>The .font these settings describe (not yet written).</summary>
    public FontFile Settings()
    {
        var size = int.TryParse((Size.Text ?? "").Replace("px", "").Trim(), out var px) ? Math.Clamp(px, 4, 64) : 12;
        var variable = WidthField.Selected != WidthChoices[1];
        var f = new FontFile
        {
            Name = NameFor(File.Selected ?? "font", size), SourceKind = Kind, Source = File.Selected ?? "", Size = size, Variable = variable,
            FixedWidth = 8, CellHeight = variable ? size : 8, LineHeight = size + 2, Baseline = (int)Math.Round(size * 0.82), Spacing = 1, Bpp = 2,
            CharacterSets = SetChoices.Where((_, i) => _sets[i].IsChecked == true).Select(s => s.Set).ToList(),
        };
        return f;
    }

    void Update()
    {
        var f = Settings();
        GameFont? font = null;
        if (SourcePath(f.Source) is { } src && System.IO.File.Exists(src))
        {
            try { font = FontBuilder.Build(f, Path.GetDirectoryName(src)!); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or FormatException or IndexOutOfRangeException or ArgumentException) { font = null; }
        }
        RomBytes = font?.RomBytes ?? 0;
        if (font != null)
        {
            var ui = _project?.Settings.Ui ?? new UiSettings();
            var (px, w, h) = TextPreview.Line(Sample, font, TextPreviewColor(ui.ScreenInk, 0xFFE6E8EE), PreviewPaper);
            var (top, bottom) = Rows(px, w, h);
            var cropped = px.Skip(top * w).Take((bottom - top) * w).ToArray();
            _sample.Source = Pixels.ToBitmap(cropped, w, bottom - top);
            _sample.Width = w * 2;
            _sample.Height = (bottom - top) * 2;
        }
        else _sample.Source = null;
        _caption.Text = $"{f.Size} px · {f.Bpp}bpp · about {Math.Max(1, (int)Math.Round(RomBytes / 1024.0))} KB of ROM";
    }

    static (int Top, int Bottom) Rows(uint[] px, int w, int h)
    {
        int top = 0, bottom = h;
        while (top < h - 1 && Enumerable.Range(0, w).All(x => px[top * w + x] == 0)) top++;
        while (bottom > top + 1 && Enumerable.Range(0, w).All(x => px[(bottom - 1) * w + x] == 0)) bottom--;
        return (top, bottom);
    }

    /// <summary>Writes the font (Create Font); null when no project is open or no source file is chosen.</summary>
    public FontEntry? CreateFont()
    {
        if (_project is null || File.Selected is null) return null;
        return _lib.Create(Settings(), SourcePath(File.Selected));
    }
}

/// <summary>
/// Add Characters (T-D16, 600 x 388; Font Manager › Add characters from text…): paste text, see which characters
/// the font has no glyph for (with code points), optionally scan every dialogue and localization string, then add
/// them: rendered from the font's source file, or as empty glyphs to draw by hand. Returns the added code points.
/// </summary>
[EditorWindow("T-D16")]
public sealed class AddCharactersDialog : FigmaDialog
{
    readonly FontEntry _font;
    readonly WrapPanel _cells = new() { ItemSpacing = 6, LineSpacing = 6, Name = "MissingCells" };
    readonly StackPanel _warning;
    readonly TextBlock _warningText = UI.Text(null, "Body/12 Semibold", "text/primary");
    readonly DialogFooter _footer;

    public TextBox Text { get; } = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 55, Name = "PasteText", VerticalContentAlignment = VerticalAlignment.Top };
    public CheckBox RenderFromSource { get; }
    public CheckBox Scan { get; } = new() { Content = "Also scan all dialogue and localization strings", Name = "ScanStrings" };
    public IReadOnlyList<int> Missing { get; private set; } = [];

    public AddCharactersDialog(WindowContext ctx) : this((ctx.Args as FontEntry) ?? FontLibrary.Of(ctx.Services).Resolve(null) ?? throw new InvalidOperationException("No font is open.")) { }

    public AddCharactersDialog(FontEntry font)
    {
        _font = font;
        Name = "AddCharactersDialog";
        AgbStudio.Editor.Theme.Fig.SetStyle(Text, "Thai/13");
        Text.Padding = new Thickness(10, 5);  // two 20 px lines must fit inside the 55 px box with the 1.5 px focus border (Avalonia drops lines that do not fit)
        RenderFromSource = new CheckBox { Content = $"Render the new glyphs from {font.File.Source}", IsChecked = true, Name = "RenderFromSource" };
        _warning = UI.Row(8, PageChrome.Glyph("warn", 14, "status/warning"), _warningText);
        _warning.Name = "MissingWarning";
        Text.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Update(); };
        Scan.IsCheckedChanged += (_, _) => Update();
        var content = new StackPanel
        {
            Spacing = 14, Margin = new Thickness(20),
            Children = { UI.Text("Paste text from your game", "Overline/11", "text/secondary"), Text, _warning, _cells, RenderFromSource, Scan },
        };
        _footer = new DialogFooter { ConfirmText = "Add 0 glyphs" };
        _footer.Cancel += (_, _) => Close(null);
        _footer.Confirm += (_, _) => { if (Missing.Count > 0) Close(AddGlyphs()); };
        Build("Add Characters", "type", content, _footer, 600);
        Update();
    }

    /// <summary>Every string of the localization tables and dialogue files (the "Also scan" option).</summary>
    public static string ProjectStrings(GameProject p)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var file in p.StringTables())
        {
            try { foreach (var v in GameProject.Load<StringTable>(file).Strings.Values) sb.Append(v).Append('\n'); }
            catch (JsonException) { }
        }
        foreach (var file in p.DialogueFiles())
        {
            try { Collect(JsonNode.Parse(System.IO.File.ReadAllText(file)), sb); }
            catch (JsonException) { }
        }
        return sb.ToString();
    }

    static void Collect(JsonNode? n, System.Text.StringBuilder sb)
    {
        switch (n)
        {
            case JsonObject o: foreach (var (_, v) in o) Collect(v, sb); break;
            case JsonArray a: foreach (var v in a) Collect(v, sb); break;
            case JsonValue v when v.TryGetValue<string>(out var s): sb.Append(s).Append('\n'); break;
        }
    }

    void Update()
    {
        var text = (Text.Text ?? "") + (Scan.IsChecked == true ? "\n" + ProjectStrings(_font.Project) : "");
        Missing = _font.MissingIn(text);
        var n = Missing.Count;
        _warningText.Text = n == 1 ? $"1 character is not in {_font.Name} yet" : $"{n} characters are not in {_font.Name} yet";
        _warning.IsVisible = _cells.IsVisible = n > 0;
        _cells.Children.Clear();
        foreach (var cp in Missing)
            _cells.Children.Add(new GlyphCell { Char = char.ConvertFromUtf32(cp), Code = cp.ToString("X4", CultureInfo.InvariantCulture), State = GlyphCellState.Missing, Name = "Missing_" + cp.ToString("X4", CultureInfo.InvariantCulture) });
        _footer.ConfirmText = n == 1 ? "Add 1 glyph" : $"Add {n} glyphs";
    }

    /// <summary>Adds the missing characters to the font (one undo step) and returns them.</summary>
    public IReadOnlyList<int> AddGlyphs()
    {
        var add = Missing.ToList();
        var render = RenderFromSource.IsChecked == true;
        _font.Edit("Add characters", f =>
        {
            if (render)
            {
                var existing = ThaiText.CodePoints(f.ExtraCharacters).ToHashSet();
                f.ExtraCharacters += string.Concat(add.Where(c => !existing.Contains(c)).Select(char.ConvertFromUtf32));
                return;
            }
            // without the source font: empty glyphs to draw in the Font Manager
            foreach (var cp in add)
                if (!f.Glyphs.ContainsKey(FontEntry.Key(cp))) f.Glyphs[FontEntry.Key(cp)] = new GlyphOverride { Advance = Math.Max(2, f.Size / 2), Rows = [] };
        });
        return add;
    }
}
