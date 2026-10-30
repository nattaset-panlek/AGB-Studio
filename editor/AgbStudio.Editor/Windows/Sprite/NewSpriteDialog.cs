using System.Globalization;
using AgbStudio.Build.Graphics;
using AgbStudio.Core;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Windows.Palette;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace AgbStudio.Editor.Windows.Sprite;

/// <summary>
/// New Sprite (T-D13, 520 x 506; Sprite Editor › Sprites +, Create › Sprite). Name, GBA size 8-64 px, frame count,
/// the OBJ palette to share (or a new one), start from a blank sheet, a copy of the current sprite or an imported PNG,
/// an "idle" animation and a collider. Writes the PNG and its meta and closes with the new sprite's path.
/// Args: the path of the sprite the editor shows (the copy source and the default palette).
/// </summary>
[EditorWindow("T-D13")]
public sealed class NewSpriteDialog : FigmaDialog
{
    public const string NewPalette = "New palette";
    public enum StartFrom { Blank, Copy, Import }

    readonly ArtLibrary? _lib;
    readonly SpriteDoc? _context;
    readonly Segment[] _start;
    readonly List<PaletteBank> _banks;

    public TextField NameField { get; } = new() { Label = "Name", Name = "SpriteName" };
    public SelectField SizeField { get; } = new() { Label = "Size", Help = "GBA sizes: 8 to 64 px", Name = "SpriteSize" };
    public TextField FramesField { get; } = new() { Label = "Frames", Name = "SpriteFrames" };
    public SelectField PaletteField { get; } = new() { Label = "Palette", Name = "SpritePalette" };
    public CheckBox CreateIdle { get; } = new() { Content = "Create animation “idle”", IsChecked = true, Height = 16, Name = "CreateIdle" };
    public CheckBox AddCollider { get; } = new() { IsChecked = false, Height = 16, Name = "AddCollider" };
    public DialogFooter Footer { get; } = new() { ConfirmText = "Create Sprite" };
    public StartFrom Start { get; private set; } = StartFrom.Blank;
    public string? ImportPath { get; set; }
    public IReadOnlyList<Segment> StartSegments => _start;
    /// <summary>File picker for Import PNG… (tests replace it).</summary>
    public Func<Task<string?>> PickImport { get; set; }

    public static readonly IReadOnlyList<(int W, int H)> Sizes = OamSizeMenu.Square.Concat(OamSizeMenu.Wide).Concat(OamSizeMenu.Tall).ToList();
    public static string SizeText(int w, int h) => $"{w} × {h}";

    public NewSpriteDialog(WindowContext ctx) : this(ctx.Services.Project.Current is null ? null : ArtLibrary.For(ctx.Services), ctx.Args as string, ctx.Services) { }

    public NewSpriteDialog(ArtLibrary? lib, string? contextSprite, EditorServices? services = null)
    {
        _lib = lib;
        Name = "NewSpriteDialog";
        PickImport = () => services?.System.PickFileAsync("Import PNG", ("PNG", ["*.png"])) ?? Task.FromResult<string?>(null);
        var ctxPath = contextSprite ?? lib?.SpriteFiles().FirstOrDefault();
        _context = lib != null && ctxPath != null && File.Exists(lib.Full(lib.Rel(ctxPath))) ? new SpriteDoc(lib, ctxPath) : null;
        _banks = lib?.Of(BankKind.Obj).ToList() ?? [];

        NameField.Text = lib is null ? "new_sprite" : Path.GetFileNameWithoutExtension(SpriteImport.FreePath(lib, "new_sprite"));
        SizeField.Options = Sizes.Select(s => SizeText(s.W, s.H)).ToList();
        var (cw, ch) = _context is null ? (16, 16) : SpriteDoc.OamFor(_context.FrameW, _context.FrameH);
        SizeField.Selected = SizeText(cw, ch);
        FramesField.Text = (_context?.FrameCount ?? 1).ToString(CultureInfo.InvariantCulture);
        PaletteField.Options = [.. _banks.Select(b => b.Label), NewPalette];
        PaletteField.Selected = _context?.Bank?.Label ?? _banks.FirstOrDefault()?.Label ?? NewPalette;
        SizeField.AddHandler(Button.ClickEvent, (_, e) => { OpenOptions(SizeField); e.Handled = true; });
        PaletteField.AddHandler(Button.ClickEvent, (_, e) => { OpenOptions(PaletteField); e.Handled = true; });
        SizeField.PropertyChanged += (_, e) => { if (e.Property == SelectField.SelectedProperty) UpdateTexts(); };
        PaletteField.PropertyChanged += (_, e) => { if (e.Property == SelectField.SelectedProperty) UpdateTexts(); };

        var group = Guid.NewGuid().ToString("N");
        _start =
        [
            new Segment { Content = "Blank", GroupName = group, IsChecked = true, Name = "Start_Blank" },
            new Segment { Content = $"Copy of {_context?.FileName ?? "sprite"}", GroupName = group, IsEnabled = _context != null, Name = "Start_Copy" },
            new Segment { Content = "Import PNG…", GroupName = group, Name = "Start_Import" },
        ];
        for (var i = 0; i < 3; i++)
        {
            var k = (StartFrom)i;
            _start[i].IsCheckedChanged += async (_, _) =>
            {
                if (_start[(int)k].IsChecked != true) return;
                Start = k;
                if (k == StartFrom.Import && ImportPath is null)
                {
                    ImportPath = await PickImport();
                    if (ImportPath is null) _start[0].IsChecked = true;
                }
            };
        }

        var sizeRow = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("*,12,*") };
        Grid.SetColumn(FramesField, 2);
        FramesField.VerticalAlignment = VerticalAlignment.Top;
        sizeRow.Children.AddRange([SizeField, FramesField]);
        var startGroup = PageParts.ToolGroup(_start);
        startGroup.HorizontalAlignment = HorizontalAlignment.Left;
        var content = UI.Column(14, NameField, sizeRow, PaletteField, UI.Text("Start from", "Overline/11", "text/secondary"), startGroup, CreateIdle, AddCollider);
        content.Margin = new Thickness(20, 20, 20, 21);
        Footer.Cancel += (_, _) => Close(null);
        Footer.Confirm += (_, _) => Close(Create());
        Build("New Sprite", "image", content, Footer, 520);
        UpdateTexts();
    }

    public (int W, int H) Size
    {
        get
        {
            var parts = (SizeField.Selected ?? "").Split('×', StringSplitOptions.TrimEntries);
            return parts.Length == 2 && int.TryParse(parts[0], out var w) && int.TryParse(parts[1], out var h) ? (w, h) : (16, 16);
        }
    }

    public int Frames => int.TryParse(FramesField.Text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? Math.Clamp(n, 1, 64) : 1;
    public PaletteBank? Bank => _banks.FirstOrDefault(b => b.Label == PaletteField.Selected);
    public (int W, int H) ColliderSize => (Math.Max(1, Size.W - 4), Math.Max(1, Size.H - 4));

    void UpdateTexts()
    {
        var (w, h) = ColliderSize;
        AddCollider.Content = $"Add a {w} × {h} collider";
        PaletteField.Help = Bank is { Assets.Count: > 0 } b ? $"Shares 16 colours with {Path.GetFileName(b.Assets[0])}" : "Gets its own 16 colours";
    }

    public void SetStart(StartFrom s) => _start[(int)s].IsChecked = true;

    void OpenOptions(SelectField field)
    {
        var items = (field.Options ?? []).Select(o =>
        {
            var mi = new MenuItem { Header = o, Command = new Relay(() => field.Selected = o) };
            if (o == field.Selected) { mi.ToggleType = MenuItemToggleType.CheckBox; mi.IsChecked = true; }
            return mi;
        }).ToList();
        new ContextMenu { ItemsSource = items }.Open(field);
    }

    /// <summary>Creates the sprite (PNG + meta) and returns its path, or null when no project is open.</summary>
    public string? Create()
    {
        if (_lib is null) return null;
        var (w, h) = Size;
        var n = Frames;
        var px = new uint[w * n * h];
        var bank = Bank;
        switch (Start)
        {
            case StartFrom.Copy when _context != null:
                for (var f = 0; f < n; f++)
                {
                    var src = f % _context.FrameCount;
                    for (var y = 0; y < Math.Min(h, _context.FrameH); y++)
                        for (var x = 0; x < Math.Min(w, _context.FrameW); x++)
                            px[y * w * n + f * w + x] = _context.Get(src, x, y);
                }
                break;
            case StartFrom.Import when ImportPath != null:
                var png = Png.Read(ImportPath);
                var src2 = png.Pixels.Select(p => Colour15.IsTransparent(p) ? Colour15.Transparent : p | 0xFF000000u).ToArray();
                SpriteImport.Reduce(src2);
                if (bank != null) SpriteImport.FitToBank(src2, bank);
                var cols = Math.Max(1, png.Width / w);
                for (var f = 0; f < n; f++)
                {
                    int ox = f % cols * w, oy = f / cols * h;
                    for (var y = 0; y < h; y++)
                        for (var x = 0; x < w; x++)
                            if (ox + x < png.Width && oy + y < png.Height) px[y * w * n + f * w + x] = src2[(oy + y) * png.Width + ox + x];
                }
                break;
        }
        var path = SpriteImport.FreePath(_lib, string.IsNullOrWhiteSpace(NameField.Text) ? "new_sprite" : NameField.Text!.Trim());
        return SpriteImport.Create(_lib, path, w * n, h, px, w, h, bank, CreateIdle.IsChecked == true, AddCollider.IsChecked == true ? ColliderSize : null);
    }

    /// <summary>True for every size the dialog offers (the 12 hardware sprite sizes).</summary>
    public static bool IsGbaSize(int w, int h) => Gba.OamSize(w, h) != null;
}
