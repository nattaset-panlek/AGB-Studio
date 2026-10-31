using AgbStudio.Editor.Commands;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Graphics;
using AgbStudio.Editor.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;

namespace AgbStudio.Editor.Windows.Scripts;

/// <summary>
/// The page frame the Figma editor pages share with the Scene Editor (toolbar 44 px with tool groups, Play / Pause / Step
/// centred between the left and right groups, side panels, status bar 26 px), for the Script Editor, Scripting API,
/// Logic Graph and World Map pages.
/// </summary>
public static class PageChrome
{
    /// <summary>Toolbar: left groups, the centre group between the stars, right groups (as SceneWorkspace lays it out).</summary>
    public static Border Toolbar(IReadOnlyList<Control> left, Control center, IReadOnlyList<Control> right)
    {
        var cols = new List<string>();
        var bar = new Grid { VerticalAlignment = VerticalAlignment.Center };
        void Add(Control c)
        {
            Grid.SetColumn(c, cols.Count);
            c.VerticalAlignment = VerticalAlignment.Center;
            bar.Children.Add(c);
            cols.Add("Auto");
        }
        foreach (var c in left) { Add(c); cols.Add("8"); }
        cols.Add("*");
        Add(center);
        cols.Add("*");
        foreach (var c in right) { cols.Add("8"); Add(c); }
        bar.ColumnDefinitions = ColumnDefinitions.Parse(string.Join(",", cols));
        var toolbar = UI.Frame(bar, "bg/panel", "border/default", new Thickness(0, 0, 0, 1), new Thickness(10, 0)).Named("Toolbar");
        toolbar.Height = 44;
        DockPanel.SetDock(toolbar, Dock.Top);
        return toolbar;
    }

    /// <summary>A Figma toolbar group: bg/base frame, border, radius 7, padding 2.</summary>
    public static Border ToolGroup(params Control[] items) => UI.Frame(UI.Row(2, items), "bg/base", "border/default", new Thickness(1), new Thickness(2), 7);

    public static StackPanel PlayGroup(EditorServices s)
    {
        var play = new PlayControl { Kind = PlayKind.Play, Command = s.Commands.Get("build.play"), Name = "PlayButton" };
        Tip(s, play, "build.play");
        return UI.Row(4, play, PlayButtons.Pause(s), PlayButtons.Step(s));
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
        ToolTip.SetTip(c, new TipContent(cmd.Title, s.Shortcuts.GestureOf(commandId) is { } g ? AgbStudio.Editor.Theme.Shortcuts.Format(g) : null));
    }

    /// <summary>Status bar: items at the left and at the right.</summary>
    public static Border StatusBar(IReadOnlyList<Control> left, IReadOnlyList<Control> right)
    {
        var l = UI.Row(16, [.. left]);
        var r = UI.Row(16, [.. right]);
        var dock = new DockPanel();
        DockPanel.SetDock(r, Dock.Right);
        dock.Children.Add(r);
        dock.Children.Add(l);
        var bar = UI.Frame(dock, "bg/panel", "border/default", new Thickness(0, 1, 0, 0), new Thickness(12, 0)).Named("StatusBar");
        bar.Height = 26;
        DockPanel.SetDock(bar, Dock.Bottom);
        return bar;
    }

    /// <summary>A side panel (260 or 320 px) with bg/panel and a border on the inner edge.</summary>
    public static Border SidePanel(Control content, bool left, string name)
    {
        var b = UI.Frame(content, "bg/panel", "border/default", left ? new Thickness(0, 0, 1, 0) : new Thickness(1, 0, 0, 0)).Named(name);
        b.ClipToBounds = true;
        return b;
    }

    public static Border Divider(bool horizontal = true)
    {
        var b = new Border { Height = horizontal ? 1 : double.NaN, Width = horizontal ? double.NaN : 1 };
        return b.Token(Border.BackgroundProperty, "border/default");
    }

    public static void DockTo(DockPanel p, Control c, Dock d)
    {
        DockPanel.SetDock(c, d);
        p.Children.Add(c);
    }

    /// <summary>
    /// Opens a list of choices under a button-like control (the Select / Field Dropdown components have no list of their
    /// own): a context menu with the current choice checked.
    /// </summary>
    public static void ShowChoices(Control anchor, IEnumerable<string> options, string? current, Action<string> select)
    {
        var items = new List<MenuItem>();
        foreach (var o in options)
        {
            var value = o;
            var mi = new MenuItem { Header = o, ToggleType = MenuItemToggleType.CheckBox, IsChecked = o == current };
            mi.Click += (_, _) => select(value);
            items.Add(mi);
        }
        var cm = new ContextMenu { ItemsSource = items, Placement = PlacementMode.BottomEdgeAlignedLeft };
        cm.Open(anchor);
    }

    /// <summary>Wires the Dropdown kind of an Inspector Field to a list of choices.</summary>
    public static InspectorField Choices(this InspectorField f, Func<IEnumerable<string>> options, Action<string> select)
    {
        f.TemplateApplied += (_, e) =>
        {
            if (e.NameScope.Find<Button>("PART_Dropdown") is { } b) b.Click += (_, _) => ShowChoices(b, options(), f.Value, v => { f.Value = v; select(v); });
        };
        return f;
    }

    /// <summary>The Toggle kind of a Field: its Switch shows IsOn, and a click changes IsOn and reports the new value.</summary>
    public static InspectorField Toggle(InspectorField f, bool on, Action<bool> changed)
    {
        f.Kind = FieldKind.Toggle;
        f.IsOn = on;
        f.PropertyChanged += (_, e) => { if (e.Property == InspectorField.IsOnProperty) changed(f.IsOn); };
        return f;
    }

    /// <summary>A row of a side panel: a Figma Field with the label column of the Inspector.</summary>
    public static InspectorField Field(string label, FieldKind kind, string? value, string? name = null) =>
        new() { Label = label, Kind = kind, Value = value, Name = name };

    public static TextBlock Muted(string text, string style = "Label/11") => UI.Text(text, style, "text/muted");

    public static Icon Glyph(string glyph, double size, string token)
    {
        var i = new Icon { Glyph = glyph, Size = size, VerticalAlignment = VerticalAlignment.Center };
        return i.Token(Icon.ForegroundProperty, token);
    }

    /// <summary>The menu items a menu definition shows (SHOT capture of T-M21 / T-M22 with the Figma state).</summary>
    public static MenuSurface MenuSurfaceOf(EditorServices s, IReadOnlyList<MenuEntry> entries) =>
        new() { ItemsSource = new MenuBuilder(s.Commands, s.Shortcuts).BuildItems(entries) };
}
