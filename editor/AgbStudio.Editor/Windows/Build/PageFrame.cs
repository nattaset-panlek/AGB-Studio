using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Graphics;
using AgbStudio.Editor.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace AgbStudio.Editor.Windows.Build;

/// <summary>
/// The page chrome the Figma pages share with the Scene Editor frame (T-P01): toolbar (44 px) with the Play / Pause /
/// Step group centred between the left and right groups, document tabs (34 px), the 260 / * / 320 panel layout and the
/// status bar (26 px). Used by Audio, SFX Designer and Build Report.
/// </summary>
public static class PageFrame
{
    public static DockPanel Page(Control toolbar, Control status, Control body)
    {
        var dock = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(toolbar, Dock.Top);
        DockPanel.SetDock(status, Dock.Bottom);
        dock.Children.AddRange([toolbar, status, body]);
        return dock;
    }

    /// <summary>Toolbar: <paramref name="left"/>, the play group, the video mode chip and Undo / Redo.</summary>
    public static Border Toolbar(EditorServices s, Control left)
    {
        var play = UI.Row(4,
            new PlayControl { Kind = PlayKind.Play, Command = s.Commands.Get("build.play"), Name = "PlayButton" },
            new PlayControl { Kind = PlayKind.Pause, Name = "PauseButton" },
            new PlayControl { Kind = PlayKind.Step, Name = "StepButton" });
        var chip = new Chip { Content = VideoModeText(s), Glyph = "chip", Name = "VideoModeChip" };
        chip.Click += (_, _) => _ = s.Windows.Open("T-O02", anchor: chip, source: "VideoModeChip");
        var undo = new IconButton { Glyph = "undo", Command = s.Commands.Get("edit.undo"), Name = "UndoButton" };
        var redo = new IconButton { Glyph = "redo", Command = s.Commands.Get("edit.redo"), Name = "RedoButton" };
        var bar = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("Auto,8,*,8,Auto,8,*,8,Auto,8,Auto"), VerticalAlignment = VerticalAlignment.Center };
        void At(Control c, int col) { Grid.SetColumn(c, col); c.VerticalAlignment = VerticalAlignment.Center; bar.Children.Add(c); }
        At(left, 0);
        At(play, 4);
        At(chip, 8);
        At(ToolGroup(undo, redo), 10);
        var toolbar = UI.Frame(bar, "bg/panel", "border/default", new Thickness(0, 0, 0, 1), new Thickness(10, 0)).Named("Toolbar");
        toolbar.Height = 44;
        return toolbar;
    }

    /// <summary>The scene's video mode as the Scene Editor chip writes it.</summary>
    public static string VideoModeText(EditorServices s)
    {
        var mode = 0;
        if (s.Project.Current is { } p && p.Settings.LastScene.Length > 0)
        {
            var file = Path.Combine(p.ScenesDir, p.Settings.LastScene + ".scene");
            try { if (File.Exists(file)) mode = GameProject.Load<SceneFile>(file).VideoMode; }
            catch (Exception e) when (e is IOException or System.Text.Json.JsonException or InvalidDataException) { }
        }
        return $"GBA · Mode {mode} · 240×160";
    }

    public static Border ToolGroup(params Control[] items) =>
        UI.Frame(UI.Row(2, items), "bg/base", "border/default", new Thickness(1), new Thickness(2), 7);

    public static Border DocTabs(params Control[] tabs)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.AddRange(tabs);
        var b = UI.Frame(row, "bg/panel", "border/default", new Thickness(0, 0, 0, 1)).Named("DocTabs");
        b.Height = 34;
        return b;
    }

    public static DocTab Tab(string label, string glyph, bool selected, Action? activate = null)
    {
        var t = new DocTab { Label = label, Glyph = glyph, IsSelected = selected, Height = 33, Name = "Doc_" + label };
        if (activate != null) t.Activated += (_, _) => activate();
        return t;
    }

    /// <summary>Left panel 260, centre, right panel 320 (the Scene Editor docking widths).</summary>
    public static Grid Body(Control left, Control center, Control right)
    {
        var g = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("260,*,320"), Name = "Body" };
        var l = UI.Frame(left, "bg/panel", "border/default", new Thickness(0, 0, 1, 0)).Named("LeftPanel");
        l.ClipToBounds = true;
        var c = UI.Frame(center, "bg/base").Named("CenterPanel");
        c.ClipToBounds = true;
        Grid.SetColumn(c, 1);
        var r = UI.Frame(right, "bg/panel", "border/default", new Thickness(1, 0, 0, 0)).Named("RightPanel");
        r.ClipToBounds = true;
        Grid.SetColumn(r, 2);
        g.Children.AddRange([l, c, r]);
        return g;
    }

    public static Border StatusBar(Control[] left, Control[] right)
    {
        var l = UI.Row(16, left);
        var r = UI.Row(16, right);
        var dock = new DockPanel();
        DockPanel.SetDock(r, Dock.Right);
        dock.Children.AddRange([r, l]);
        var bar = UI.Frame(dock, "bg/panel", "border/default", new Thickness(0, 1, 0, 0), new Thickness(12, 0)).Named("StatusBar");
        bar.Height = 26;
        return bar;
    }

    public static StatusItem Status(string name, string glyph, string? text, bool dot = false)
    {
        var i = new StatusItem { Glyph = glyph, Content = text, IsDot = dot, Name = name };
        return i;
    }

    /// <summary>An Inspector section (Section Header and its rows) with the bottom border of the Figma panels.</summary>
    public static Border Section(SectionHeader header, Control content, Thickness? pad = null)
    {
        content.IsVisible = header.IsOpen;
        header.PropertyChanged += (_, e) => { if (e.Property == SectionHeader.IsOpenProperty) content.IsVisible = header.IsOpen; };
        var stack = UI.Column(6, header, content);
        var b = UI.Frame(stack, null, "border/default", new Thickness(0, 0, 0, 1), pad ?? new Thickness(12, 7, 12, 10));
        return b;
    }

    /// <summary>A label (Body/12, text/secondary, 86 px) and an editor, as the Figma "Field" row.</summary>
    public static DockPanel FieldRow(string label, Control editor, double height = 24)
    {
        var t = UI.Text(label, "Body/12", "text/secondary", 86);
        t.TextWrapping = TextWrapping.Wrap;
        var row = new DockPanel { HorizontalSpacing = 8, MinHeight = height };
        DockPanel.SetDock(t, Dock.Left);
        row.Children.AddRange([t, editor]);
        return row;
    }

    public static Icon Glyph(string glyph, double size, string token)
    {
        var i = new Icon { Glyph = glyph, Size = size, VerticalAlignment = VerticalAlignment.Center };
        return i.Token(Icon.ForegroundProperty, token);
    }

    /// <summary>KB with one decimal under 10 KB ("6.2 KB", "41 KB").</summary>
    public static string Kb(long bytes)
    {
        var kb = bytes / 1024.0;
        return kb < 10 ? $"{kb.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)} KB" : $"{Math.Round(kb).ToString(System.Globalization.CultureInfo.InvariantCulture)} KB";
    }

    public static TextBlock Wrap(this TextBlock t, double? width = null)
    {
        t.TextWrapping = TextWrapping.Wrap;
        if (width is { } w) t.Width = w;
        return t;
    }

    public static IBrush? Brush(Control c, string token) => c.TryFindResource(token, c.ActualThemeVariant, out var v) ? v as IBrush : null;
}
