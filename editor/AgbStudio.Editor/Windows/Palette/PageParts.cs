using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace AgbStudio.Editor.Windows.Palette;

/// <summary>
/// The frame the Sprite Editor (T-P08) and the Palette Manager (T-P10) share with the Scene Editor: 44 px toolbar,
/// document tabs, 260 / 320 px side panels and the 26 px status bar.
/// </summary>
public static class PageParts
{
    /// <summary>Tool group: bg/base, border/default, radius 7, 2 px padding and gap.</summary>
    public static Border ToolGroup(params Control[] items) => UI.Frame(UI.Row(2, items), "bg/base", "border/default", new Thickness(1), new Thickness(2), 7);

    /// <summary>Toolbar: left groups, the play controls centred between them and the right groups.</summary>
    public static Border Toolbar(Control[] left, Control center, Control[] right)
    {
        var cols = new List<string>();
        var grid = new Grid { VerticalAlignment = VerticalAlignment.Center };
        void Add(Control c, string width)
        {
            Grid.SetColumn(c, cols.Count);
            c.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(c);
            cols.Add(width);
        }
        for (var i = 0; i < left.Length; i++)
        {
            if (i > 0) cols.Add("8");
            Add(left[i], "Auto");
        }
        cols.Add("*");
        Add(center, "Auto");
        cols.Add("*");
        for (var i = 0; i < right.Length; i++)
        {
            if (i > 0) cols.Add("8");
            Add(right[i], "Auto");
        }
        grid.ColumnDefinitions = ColumnDefinitions.Parse(string.Join(",", cols));
        var bar = UI.Frame(grid, "bg/panel", "border/default", new Thickness(0, 0, 0, 1), new Thickness(10, 0)).Named("Toolbar");
        bar.Height = 44;
        return bar;
    }

    /// <summary>Play / Pause / Step as in the Scene Editor toolbar (Play runs the game).</summary>
    public static StackPanel PlayControls(EditorServices s) => UI.Row(4,
        new PlayControl { Kind = PlayKind.Play, Command = s.Commands.Get("build.play"), Name = "PlayButton" },
        PlayButtons.Pause(s), PlayButtons.Step(s));

    public static Chip VideoModeChip(EditorServices s)
    {
        var chip = new Chip { Content = "GBA · Mode 0 · 240×160", Glyph = "chip", Name = "VideoModeChip" };
        chip.Click += (_, _) => _ = s.Windows.Open("T-O02", anchor: chip, source: "VideoModeChip");
        return chip;
    }

    public static Border UndoRedo(EditorServices s)
    {
        var undo = new IconButton { Glyph = "undo", Command = s.Commands.Get("edit.undo"), Name = "UndoButton" };
        var redo = new IconButton { Glyph = "redo", Command = s.Commands.Get("edit.redo"), Name = "RedoButton" };
        Tip(s, undo, "edit.undo");
        Tip(s, redo, "edit.redo");
        return ToolGroup(undo, redo);
    }

    public static void Tip(EditorServices s, Control c, string commandId)
    {
        var cmd = s.Commands.Get(commandId);
        ToolTip.SetTip(c, new TipContent(cmd.Title, s.Shortcuts.GestureOf(commandId) is { } g ? Theme.Shortcuts.Format(g) : null));
    }

    public static Border StatusBar(Control[] left, Control[] right)
    {
        var l = UI.Row(16, left);
        var r = UI.Row(16, right);
        var dock = new DockPanel();
        DockPanel.SetDock(r, Dock.Right);
        dock.Children.Add(r);
        dock.Children.Add(l);
        var bar = UI.Frame(dock, "bg/panel", "border/default", new Thickness(0, 1, 0, 0), new Thickness(12, 0)).Named("StatusBar");
        bar.Height = 26;
        return bar;
    }

    public static StatusItem Status(string name, string glyph, string? text = null, bool dot = false) =>
        new() { Name = name, Glyph = glyph, Content = text, IsDot = dot };

    /// <summary>The window a document tab shows (Editor Map #15-#17 plus sprites and palettes).</summary>
    public static string? WindowOf(EditorDocument d) =>
        d.Name == "Scene" ? "T-P01" : d.Name == "Game" ? "T-P02" : d.Name == PaletteManagerPage.DocumentName ? "T-P10"
        : d.Name.EndsWith(".map", StringComparison.OrdinalIgnoreCase) ? "T-P03"
        : d.Name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ? "T-P04"
        : d.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || d.Name.EndsWith(".aseprite", StringComparison.OrdinalIgnoreCase) ? "T-P08"
        : null;

    /// <summary>Document tabs (34 px): one Doc Tab per open document; clicking one shows its window.</summary>
    public static Border DocTabs(EditorServices s, Control owner)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Stretch };
        void Rebuild()
        {
            row.Children.Clear();
            foreach (var d in s.Documents.Open)
            {
                var tab = new DocTab { Label = d.Name, Glyph = d.Glyph, IsDirty = d.IsDirty, IsSelected = d == s.Documents.Active, Height = 33, Name = "Doc_" + d.Name };
                var doc = d;
                tab.Activated += (_, _) =>
                {
                    s.Documents.Active = doc;
                    if (WindowOf(doc) is { } w) _ = s.Windows.Open(w, w is "T-P08" or "T-P03" or "T-P04" ? doc : null, source: "Doc_" + doc.Name);
                };
                row.Children.Add(tab);
            }
        }
        Rebuild();
        Action handler = () => Avalonia.Threading.Dispatcher.UIThread.Post(Rebuild);
        owner.AttachedToVisualTree += (_, _) => { s.Documents.Changed += handler; Rebuild(); };
        owner.DetachedFromVisualTree += (_, _) => s.Documents.Changed -= handler;
        return UI.Frame(row, "bg/panel", "border/default", new Thickness(0, 0, 0, 1)).Named("DocTabs");
    }

    /// <summary>A side panel section (Inspector style): Section Header, 6 px, content, a border below (Figma T-P10).</summary>
    public static Border Section(SectionHeader header, Control content, Thickness? pad = null, double gap = 6)
    {
        var body = new StackPanel { Spacing = gap };
        body.Children.Add(header);
        body.Children.Add(content);
        header.PropertyChanged += (_, e) => { if (e.Property == SectionHeader.IsOpenProperty) content.IsVisible = header.IsOpen; };
        return UI.Frame(body, null, "border/default", new Thickness(0, 0, 0, 1), pad ?? new Thickness(12, 8, 12, 10));
    }

    /// <summary>A dashed rounded frame (drop zone, empty slots) drawn with a border/strong stroke.</summary>
    public static Panel Dashed(Control child, double radius = 8)
    {
        var rect = new Rectangle { RadiusX = radius, RadiusY = radius, StrokeThickness = 1, StrokeDashArray = [4, 3] }.Token(Shape.StrokeProperty, "border/strong");
        return new Panel { Children = { rect, child } };
    }

    public static IBrush DataBrush(uint argb) => new ImmutableSolidColorBrush(Colour15.ToColor(argb));
}
