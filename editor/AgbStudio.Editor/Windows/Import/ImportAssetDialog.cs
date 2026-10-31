using System.Globalization;
using AgbStudio.Build.Graphics;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Graphics;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Windows.Build;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;

namespace AgbStudio.Editor.Windows.Import;

/// <summary>
/// Import Asset (T-D04, 900 x 460; File › Import Asset…, Assets › Import…, Sprite Editor › Drop zone, Palette Manager ›
/// Import .pal / .gpl, Audio › Music +). Files are dropped or browsed, each is detected and listed with its size and
/// state; the selected file's options are on the right. Import converts the ready files; when a file can't be
/// converted, Import Asset · Errors (T-D24) opens with the fixes (P11.64).
/// </summary>
[EditorWindow("T-D04")]
public sealed class ImportAssetDialog : FigmaDialog
{
    readonly EditorServices _s;
    readonly Grid _table = new() { ColumnDefinitions = ColumnDefinitions.Parse("180,160,80,100"), Name = "ImportTable" };
    readonly ContentControl _options = new() { Name = "ImportOptions" };
    readonly TextBlock _dropText = UI.Text("", "Body/12", "text/secondary");
    readonly DialogFooter _footer;
    readonly List<ImportItem> _items = [];

    public IReadOnlyList<ImportItem> Items => _items;
    public ImportItem? Selected { get; private set; }

    public ImportAssetDialog(WindowContext ctx) : this(ctx.Services, ctx.Args as IEnumerable<string>) { }

    public ImportAssetDialog(EditorServices s, IEnumerable<string>? files = null)
    {
        _s = s;
        Name = "ImportAssetDialog";
        var drop = new Border
        {
            Height = 63, CornerRadius = new CornerRadius(8), Background = Brushes.Transparent, Cursor = new Cursor(StandardCursorType.Hand), Name = "DropZone",
            Child = UI.Row(10, PageFrame.Glyph("upload", 14, "text/secondary"), _dropText),
        };
        ((StackPanel)drop.Child).HorizontalAlignment = HorizontalAlignment.Center;
        ((StackPanel)drop.Child).VerticalAlignment = VerticalAlignment.Center;
        var dashed = new DashedBorder { Child = drop };
        DragDrop.SetAllowDrop(drop, true);
        drop.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
#pragma warning disable CS0618 // DataTransfer replaces Data in Avalonia 11.3; GetFiles is still the documented way to read dropped files
            var items = e.Data.GetFiles();
#pragma warning restore CS0618
            if (items != null) Add(items.Select(i => i.TryGetLocalPath()).Where(p => p != null).Select(p => p!));
        });
        drop.PointerPressed += async (_, _) => await BrowseAsync();
        var left = UI.Column(11, dashed, _table);
        left.Width = 520;
        var right = new StackPanel { Width = 318 };
        right.Children.Add(_options);
        var content = UI.Row(20, left, right);
        content.Margin = new Thickness(21, 20, 21, 0);
        _footer = new DialogFooter { CancelText = "Cancel", ConfirmText = "Import", Name = "ImportFooter" };
        _footer.Cancel += (_, _) => Close(null);
        _footer.Confirm += (_, _) => _ = ImportAsync();
        Build("Import Asset", "upload", content, _footer, 900);
        if (files != null) Add(files);
        Refresh();
    }

    async Task BrowseAsync()
    {
        if (_s.Top?.StorageProvider is not { CanOpen: true } storage) return;
        var r = await storage.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Import Asset", AllowMultiple = true });
        Add(r.Select(f => f.TryGetLocalPath()).Where(p => p != null).Select(p => p!));
    }

    /// <summary>Adds files (dropped, browsed or passed by the caller) and detects them.</summary>
    public void Add(IEnumerable<string> files)
    {
        foreach (var f in files)
        {
            if (_items.Any(i => string.Equals(i.Source, f, StringComparison.OrdinalIgnoreCase))) continue;
            _items.Add(AssetImporter.Inspect(f, _s.Project.Current));
        }
        Selected ??= _items.FirstOrDefault();
        Refresh();
    }

    public void Select(ImportItem item)
    {
        Selected = item;
        Refresh();
    }

    void Refresh()
    {
        _dropText.Text = _items.Count == 0 ? "Drop files here or browse..." : "Drop more files here or browse...";
        _table.Children.Clear();
        _table.RowDefinitions.Clear();
        _table.RowDefinitions.Add(new RowDefinition(28, GridUnitType.Pixel));
        string[] heads = ["File", "Detected as", "Size", ""];
        for (var c = 0; c < 4; c++) Put(new TableCell { Kind = TableCellKind.Header, Text = heads[c] }, 0, c);
        for (var i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            var sel = item == Selected;
            _table.RowDefinitions.Add(new RowDefinition(34, GridUnitType.Pixel));
            var kind = sel ? TableCellKind.Selected : TableCellKind.Default;
            Put(Clickable(new TableCell { Text = item.FileName, Kind = kind, Height = 34 }, item), i + 1, 0);
            Put(Clickable(new TableCell { Text = item.Detected, Kind = kind, Height = 34 }, item), i + 1, 1);
            var size = new TableCell { Text = PageFrame.Kb(item.Size), Kind = sel ? TableCellKind.Selected : TableCellKind.Mono, Height = 34 };
            if (sel) size.TemplateApplied += (_, e) => { if (e.NameScope.Find<TextBlock>("PART_Text") is { } t) AgbStudio.Editor.Theme.Fig.SetStyle(t, "Mono/11"); };
            Put(Clickable(size, item), i + 1, 2);
            Put(Clickable(new TableCell { Kind = kind, Height = 34 }, item), i + 1, 3);
            var chip = new Chip
            {
                Content = item.Error != null ? "error" : item.HasProblem ? "needs fix" : "ready",
                Style = item.Error != null ? ChipStyle.Danger : item.HasProblem ? ChipStyle.Warning : ChipStyle.Success,
                IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(10, 0, 0, 0), Name = "State_" + item.FileName,
            };
            Put(chip, i + 1, 3);
        }
        _options.Content = Selected is null ? null : Options(Selected);
        var all = _items.Count;
        _footer.ConfirmText = all == 1 ? "Import 1 file" : $"Import {all} files";
    }

    Control Clickable(TableCell cell, ImportItem item)
    {
        cell.PointerPressed += (_, e) => { Select(item); e.Handled = true; };
        cell.Cursor = new Cursor(StandardCursorType.Hand);
        return cell;
    }

    void Put(Control c, int row, int col)
    {
        Grid.SetRow(c, row);
        Grid.SetColumn(c, col);
        _table.Children.Add(c);
    }

    // ---- options of the selected file -------------------------------------------------------------------

    Control Options(ImportItem item)
    {
        var col = UI.Column(0);
        col.Name = "Options_" + item.FileName;
        col.Children.Add(UI.Text($"{item.FileName} · {item.Detected}", "Overline/11", "text/secondary"));
        var preview = UI.Frame(Preview(item), "bg/base", "border/default", new Thickness(1), radius: 8).Named("ImportPreview");
        preview.Height = 80;
        preview.Margin = new Thickness(0, 12, 0, 0);
        col.Children.Add(preview);
        var fields = UI.Column(12);
        fields.Margin = new Thickness(0, 12, 0, 0);
        col.Children.Add(fields);
        if (item.Error != null)
        {
            fields.Children.Add(UI.Text(item.Error, "Label/11", "status/danger").Wrap());
            return col;
        }
        switch (item.Kind)
        {
            case ImportKind.SpriteSheet:
                var size = new InspectorField { Kind = FieldKind.Dropdown, Label = "Frame size", Value = $"{item.FrameWidth} × {item.FrameHeight}", Name = "Opt_FrameSize" };
                var sizes = Gba.OamSizes.Where(z => z.W <= item.Width && z.H <= item.Height && item.Width % z.W == 0 && item.Height % z.H == 0).Select(z => $"{z.W} × {z.H}").ToList();
                if (sizes.Count == 0) sizes.Add($"{item.FrameWidth} × {item.FrameHeight}");
                BuildReportPage.Dropdown(size, sizes, v => { var p = v.Split(" × "); item.FrameWidth = int.Parse(p[0], CultureInfo.InvariantCulture); item.FrameHeight = int.Parse(p[1], CultureInfo.InvariantCulture); Refresh(); });
                var colourOptions = new List<string> { "Reduce to 16 (4bpp)" };
                if (item.Colors > 15) colourOptions.Add($"Split into {AssetImporter.SplitCount(item)} palettes");
                var colours = new InspectorField { Kind = FieldKind.Dropdown, Label = "Colours", Value = item.Fix ?? (item.Warning != null && item.SharePalette.Length == 0 ? $"Uses {item.Colors} colours" : "Reduce to 16 (4bpp)"), Name = "Opt_Colours" };
                BuildReportPage.Dropdown(colours, colourOptions, v => { if (v.StartsWith("Split", StringComparison.Ordinal) || item.Colors > 15) AssetImporter.ApplyFix(item, v.StartsWith("Reduce", StringComparison.Ordinal) ? "Reduce to 16" : v); Refresh(); });
                var palette = new InspectorField { Kind = FieldKind.Dropdown, Label = "Palette", Value = PaletteText(item.SharePalette), Name = "Opt_Palette" };
                var proj = _s.Project.Current;
                var owners = proj?.SpriteFiles().Where(f => proj.LoadMeta<AgbStudio.Core.Project.SpriteMeta>(f).Palette.Length == 0)
                    .Select(AgbStudio.Core.Project.GameProject.AssetName).Where(n => n != item.Name).Order(StringComparer.Ordinal).ToList() ?? [];
                BuildReportPage.Dropdown(palette, ["Own palette", .. owners.Select(PaletteText)], v =>
                {
                    item.SharePalette = v == "Own palette" ? "" : owners.First(o => PaletteText(o) == v);
                    if (item.SharePalette.Length > 0 && item.Warning != null && item.Fix is null) item.Fix = "Share palette";
                    Refresh();
                });
                var frames = new InspectorField { Kind = FieldKind.Text, Label = "Frames found", Value = $"{item.FramesFound} · {item.FrameRows} row{(item.FrameRows == 1 ? "" : "s")}", Name = "Opt_Frames", IsHitTestVisible = false };
                var trim = new CheckBox { Content = "Trim empty frames", IsChecked = item.TrimEmptyFrames, Name = "Opt_Trim" };
                trim.IsCheckedChanged += (_, _) => { item.TrimEmptyFrames = trim.IsChecked == true; Refresh(); };
                var anim = new CheckBox { Content = $"Create animation “{item.AnimationName}”", IsChecked = item.CreateAnimation, Name = "Opt_Animation" };
                anim.IsCheckedChanged += (_, _) => item.CreateAnimation = anim.IsChecked == true;
                fields.Children.AddRange([size, colours, palette, frames]);
                var checks = UI.Column(12, trim, anim);
                checks.Margin = new Thickness(0, 2, 0, 0);
                fields.Children.Add(checks);
                break;
            default:
                var detail = item.Kind switch
                {
                    ImportKind.SoundEffect => ("Converted to", "11 kHz mono 8-bit"),
                    ImportKind.Music => ("Format", $"{Path.GetExtension(item.Source).TrimStart('.').ToUpperInvariant()} → Maxmod bank"),
                    ImportKind.Tilemap => ("Map", item.Detail),
                    ImportKind.Tileset => ("Tiles", "16 × 16, 4bpp"),
                    ImportKind.Background => ("Size", $"{item.Width} × {item.Height}"),
                    _ => ("Goes to", Destination(item.Kind)),
                };
                fields.Children.Add(new InspectorField { Kind = FieldKind.Text, Label = detail.Item1, Value = detail.Item2, IsHitTestVisible = false, Name = "Opt_Detail" });
                if (item.Kind == ImportKind.SoundEffect) fields.Children.Add(new InspectorField { Kind = FieldKind.Text, Label = "Length", Value = item.Detail, IsHitTestVisible = false });
                fields.Children.Add(UI.Text(item.HasProblem ? item.Warning : item.Ready, "Label/11", item.HasProblem ? "status/warning" : "text/muted").Wrap());
                break;
        }
        return col;
    }

    string PaletteText(string owner)
    {
        if (owner.Length == 0) return "Own palette";
        var slot = _s.Project.Current is { } p ? AssetImporter.ObjPaletteSlot(p, owner) : -1;
        return slot >= 0 ? $"OBJ {slot} · share with {owner}" : $"Share with {owner}";
    }

    static string Destination(ImportKind k) => k switch
    {
        ImportKind.Palette => @"assets\palettes",
        ImportKind.Font => "fonts",
        ImportKind.Script => "scripts",
        _ => "assets",
    };

    /// <summary>The preview: sprite frames at 2x, other images scaled to fit, an icon for other files.</summary>
    static Control Preview(ImportItem item)
    {
        if (item.Image is { } img && item.Pixels is { } px)
        {
            if (item.Kind == ImportKind.SpriteSheet && item.FrameWidth > 0 && item.FrameHeight > 0)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 22, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                var scale = Math.Max(1, Math.Min(2, 64 / Math.Max(item.FrameWidth, item.FrameHeight)));
                var cols = Math.Max(1, img.Width / item.FrameWidth);
                var shown = 0;
                for (var f = 0; f < cols * item.FrameRows && shown < 4; f++)
                {
                    var fx = f % cols * item.FrameWidth;
                    var fy = f / cols * item.FrameHeight;
                    if (item.TrimEmptyFrames && img.IsEmpty(fx, fy, item.FrameWidth, item.FrameHeight)) continue;
                    row.Children.Add(Pixels(px, img.Width, fx, fy, item.FrameWidth, item.FrameHeight, scale));
                    shown++;
                }
                return row;
            }
            var k = Math.Min(Math.Min(300.0 / img.Width, 70.0 / img.Height), 2);
            var whole = Pixels(px, img.Width, 0, 0, img.Width, img.Height, 1);
            whole.Width = img.Width * k;
            whole.Height = img.Height * k;
            whole.HorizontalAlignment = HorizontalAlignment.Center;
            whole.VerticalAlignment = VerticalAlignment.Center;
            return whole;
        }
        var glyph = item.Kind switch { ImportKind.SoundEffect => "sound", ImportKind.Music => "music", ImportKind.Tilemap => "tiles", ImportKind.Font => "type", ImportKind.Script => "code", ImportKind.Palette => "sliders", _ => "file" };
        var icon = PageFrame.Glyph(glyph, 28, "text/muted");
        icon.HorizontalAlignment = HorizontalAlignment.Center;
        return icon;
    }

    /// <summary>Game pixels (data, not UI colours) as a nearest-neighbour bitmap.</summary>
    static Image Pixels(uint[] px, int stride, int x0, int y0, int w, int h, double scale)
    {
        var bmp = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        using (var fb = bmp.Lock())
        {
            var row = new int[w];
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var i = (y0 + y) * stride + x0 + x;
                    row[x] = i < px.Length ? unchecked((int)px[i]) : 0;
                }
                System.Runtime.InteropServices.Marshal.Copy(row, 0, fb.Address + y * fb.RowBytes, w);
            }
        }
        var image = new Image { Source = bmp, Width = w * scale, Height = h * scale, Stretch = Stretch.Fill };
        RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.None);
        return image;
    }

    // ---- import -----------------------------------------------------------------------------------------

    /// <summary>Imports when every file is ready; otherwise opens Import Asset · Errors with the problems.</summary>
    public async Task<IReadOnlyList<string>?> ImportAsync()
    {
        if (_items.Count == 0) return null;
        if (_items.Any(i => i.HasProblem))
        {
            Close(null);
            return await _s.Windows.Open("T-D24", _items.ToList(), source: "ImportAssetErrors") as IReadOnlyList<string>;
        }
        var written = ImportErrorsDialog.ImportReady(_s, _items);
        Close(written);
        return written;
    }
}

/// <summary>The dashed 1 px border/strong frame of the Figma drop zone.</summary>
public sealed class DashedBorder : Decorator
{
    public override void Render(DrawingContext ctx)
    {
        var pen = new Pen(this.TryFindResource("border/strong", ActualThemeVariant, out var b) ? b as IBrush : null, 1, new DashStyle([4, 3], 0));
        ctx.DrawRectangle(null, pen, new Rect(0.5, 0.5, Bounds.Width - 1, Bounds.Height - 1), 8, 8);
    }
}
