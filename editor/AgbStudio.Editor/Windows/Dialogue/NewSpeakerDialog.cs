using AgbStudio.Build.Graphics;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Graphics;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Windows.Database;
using AgbStudio.Scripting.Dialogue;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace AgbStudio.Editor.Windows.Dialogue;

/// <summary>
/// New Speaker (T-D21, 520 x 438; Dialogue Editor › Speakers +): the name in English and Thai, the portrait (a sprite of
/// the project, "+" adds a PNG to assets/sprites), the name tag colour and the blip sound. Add Speaker adds it to the
/// open dialogue file (undoable).
/// </summary>
[EditorWindow("T-D21")]
public sealed class NewSpeakerDialog : FigmaDialog
{
    /// <summary>Name tag colours (game palette entries, BGR555) in the order Figma shows them: violet, yellow, green, pink.</summary>
    public static readonly ushort[] TagColors = [0x7E56, 0x235F, 0x4728, 0x59FF];

    readonly DialogueEditorPage? _page;
    readonly WindowContext _ctx;
    readonly StackPanel _portraits = new() { Orientation = Orientation.Horizontal, Spacing = 8, Name = "Portraits" };
    readonly StackPanel _colors = new() { Orientation = Orientation.Horizontal, Spacing = 8, Name = "TagColours" };
    bool _portraitChosen;

    public TextField NameEn { get; } = new() { Label = "Name · EN", Name = "NameEn" };
    public TextField NameTh { get; } = new() { Label = "Name · TH", Name = "NameTh" };
    public SelectField Blip { get; } = new() { Label = "Blip sound", Name = "BlipSound" };
    public string? Portrait { get; private set; }
    public int ColorIndex { get; private set; }
    public string? Error { get; private set; }

    public NewSpeakerDialog(WindowContext ctx)
    {
        _ctx = ctx;
        Name = "NewSpeakerDialog";
        _page = ctx.Args as DialogueEditorPage ?? ctx.Services.GetService(typeof(DialogueEditorPage)) as DialogueEditorPage;
        var sounds = _page?.Sounds() ?? [];
        Blip.Options = sounds;
        Blip.Selected = sounds.FirstOrDefault(s => s.StartsWith("blip_low", StringComparison.Ordinal)) ?? sounds.FirstOrDefault(s => s.StartsWith("blip", StringComparison.Ordinal)) ?? sounds.FirstOrDefault();
        Dropdown.Attach(Blip, () => Blip.Options ?? [], _ => { });
        NameEn.PropertyChanged += (_, e) =>
        {
            if (e.Property != TextField.TextProperty) return;
            if (!_portraitChosen && SpriteLibrary.Names(_page?.Data?.Project).FirstOrDefault(n => string.Equals(n, (NameEn.Text ?? "").Trim(), StringComparison.OrdinalIgnoreCase)) is { } match)
            {
                Portrait = match;
                RebuildPortraits();
            }
        };

        var names = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("*,10,*") };
        names.Children.Add(NameEn.At(0, 0));
        names.Children.Add(NameTh.At(0, 2));
        Portrait = SpriteLibrary.Names(_page?.Data?.Project).FirstOrDefault();
        RebuildPortraits();
        RebuildColors();
        var content = UI.Column(0,
            names,
            Ui.Overline("Portrait").Margin(0, 14, 0, 0),
            PortraitRow().Margin(0, 14, 0, 0),
            Ui.Overline("Name tag colour").Margin(0, 14, 0, 0),
            _colors.Margin(0, 14, 0, 0),
            Blip.Margin(0, 14, 0, 0));
        content.Margin = new Thickness(20, 20, 20, 0);
        var footer = new DialogFooter { ConfirmText = "Add Speaker" };
        footer.Cancel += (_, _) => Close(null);
        footer.Confirm += (_, _) => { if (Create() is { } s) Close(s); };
        Build("New Speaker", "user", content, footer, 520);
        Ui.FocusOnOpen(this, NameEn);
    }

    public void ChoosePortrait(string sprite)
    {
        Portrait = sprite;
        _portraitChosen = true;
        RebuildPortraits();
    }

    public void ChooseColor(int i)
    {
        ColorIndex = Math.Clamp(i, 0, TagColors.Length - 1);
        RebuildColors();
    }

    /// <summary>The chosen sprite first, then the portraits of the file's speakers, then the other sprites.</summary>
    IEnumerable<string> PortraitOrder()
    {
        var all = SpriteLibrary.Names(_page?.Data?.Project);
        var used = _page?.Current?.File.Speakers.Select(s => s.Portrait).OfType<string>() ?? [];
        return new[] { Portrait }.OfType<string>().Concat(used).Concat(all).Where(all.Contains).Distinct();
    }

    void RebuildPortraits()
    {
        _portraits.Children.Clear();
        var project = _page?.Data?.Project;
        foreach (var name in PortraitOrder())
        {
            var tile = new Border
            {
                Width = 56, Height = 56, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(name == Portrait ? 1.5 : 1),
                Child = new PixelImage { Pixels = SpriteLibrary.Frame(project, name), MaxScale = 2 }, Name = "Portrait_" + name, Cursor = new Cursor(StandardCursorType.Hand),
            }.Token(Border.BackgroundProperty, "bg/base").Token(Border.BorderBrushProperty, name == Portrait ? "accent/default" : "border/default");
            ToolTip.SetTip(tile, new TipContent(name));
            var n = name;
            tile.PointerPressed += (_, _) => ChoosePortrait(n);
            _portraits.Children.Add(tile);
        }
    }

    /// <summary>The sprite tiles (scrolling when there are more than fit) and the "+" tile at the end of the row.</summary>
    Control PortraitRow()
    {
        var dash = new Rectangle { RadiusX = 8, RadiusY = 8, StrokeThickness = 1, StrokeDashArray = [4, 3] }.Token(Shape.StrokeProperty, "border/strong");
        var plus = new Icon { Glyph = "plus", Size = 14, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }.Token(Icon.ForegroundProperty, "text/muted");
        var add = new Border { Width = 56, Height = 56, Margin = new Thickness(8, 0, 0, 0), Background = Brushes.Transparent, Child = new Panel { Children = { dash, plus } }, Name = "Portrait_Add", Cursor = new Cursor(StandardCursorType.Hand) };
        add.PointerPressed += async (_, _) => await AddPortrait();
        var scroll = new ScrollViewer { Content = _portraits, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, Name = "PortraitScroll" };
        var row = new DockPanel { HorizontalAlignment = HorizontalAlignment.Left };
        DockPanel.SetDock(add, Dock.Right);
        row.Children.AddRange([add, scroll]);
        return row;
    }

    /// <summary>"+": copies a PNG into assets/sprites and uses it as the portrait.</summary>
    public async Task AddPortrait()
    {
        if (_page?.Data?.Project is not { } p) return;
        if (await FileChooser.For(_ctx.Services).Open("Add portrait", "png") is not { } file) return;
        var dest = System.IO.Path.Combine(p.AssetsDir, "sprites", System.IO.Path.GetFileName(file));
        if (!File.Exists(dest)) File.Copy(file, dest);
        ChoosePortrait(GameProject.AssetName(dest));
    }

    void RebuildColors()
    {
        _colors.Children.Clear();
        for (var i = 0; i < TagColors.Length; i++)
        {
            var sw = new Swatch { Width = 26, Height = 26, Color = Color.FromUInt32(Gba.ToArgb(TagColors[i])), State = i == ColorIndex ? SwatchState.Selected : SwatchState.Default, Name = "Tag_" + i, Cursor = new Cursor(StandardCursorType.Hand) };
            var k = i;
            sw.PointerPressed += (_, _) => ChooseColor(k);
            _colors.Children.Add(sw);
        }
    }

    /// <summary>Validates and adds the speaker; returns it, or null with <see cref="Error"/> set.</summary>
    public DialogueSpeaker? Create()
    {
        var name = (NameEn.Text ?? "").Trim();
        Error = name.Length == 0 ? "Enter a name." : _page?.Current is null ? "Open a dialogue first."
            : _page.Current.File.Speakers.Any(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)) ? $"{name} is already a speaker of this dialogue." : null;
        if (Error != null) { NameEn.Help = Error; return null; }
        var sp = new DialogueSpeaker { Name = name, Portrait = Portrait, Blip = Blip.Selected is { } b ? DialogueEditorPage.SoundName(b) : null };
        _page!.AddSpeaker(sp, name, (NameTh.Text ?? "").Trim(), Gba.Hex(TagColors[ColorIndex]));
        return sp;
    }
}
