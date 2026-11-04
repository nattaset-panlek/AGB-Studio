using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Graphics;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Styling;

namespace AgbStudio.Editor.Windows.Tilemap;

/// <summary>
/// The page chrome the editor pages share with the Scene Editor frame (T-P01): 44 px toolbar with tool groups, play
/// controls, video-mode chip and undo / redo; document tabs; side panels; Inspector-style sections; status bar.
/// Used by the Tilemap Editor (T-P03) and the Font Manager (T-P09).
/// </summary>
public static class PageChrome
{
    public static ControlTheme? Bare => Application.Current?.FindResource("BareButton") as ControlTheme;

    /// <summary>
    /// Subscribes while the control is on screen: the shell makes a new page each time a document opens it, so a page
    /// that left the window stops listening (and can be collected) instead of redrawing in the background.
    /// </summary>
    public static void WhileAttached(Control c, Action attach, Action detach)
    {
        c.AttachedToVisualTree += (_, _) => attach();
        c.DetachedFromVisualTree += (_, _) => detach();
    }

    /// <summary>Toolbar: [left groups] · play controls centred in the free space · [video chip] [undo / redo].</summary>
    public static Border Toolbar(EditorServices s, IReadOnlyList<Control> left, int videoMode)
    {
        var play = UI.Row(4,
            new PlayControl { Kind = PlayKind.Play, Command = s.Commands.Get("build.play"), Name = "PlayButton" },
            PlayButtons.Pause(s), PlayButtons.Step(s));
        Tip(s, play.Children[0], "build.play");
        var chip = new Chip { Content = $"GBA · Mode {videoMode} · 240×160", Glyph = "chip", Name = "VideoModeChip" };
        chip.Click += (_, _) => _ = s.Windows.Open("T-O02", anchor: chip, source: "VideoModeChip");
        var undo = new IconButton { Glyph = "undo", Command = s.Commands.Get("edit.undo"), Name = "UndoButton" };
        var redo = new IconButton { Glyph = "redo", Command = s.Commands.Get("edit.redo"), Name = "RedoButton" };
        Tip(s, undo, "edit.undo");
        Tip(s, redo, "edit.redo");

        var cols = string.Join(",", left.Select((_, i) => i == 0 ? "Auto" : "8,Auto")) + ",*,Auto,*,Auto,8,Auto";
        var bar = new Grid { ColumnDefinitions = ColumnDefinitions.Parse(cols), VerticalAlignment = VerticalAlignment.Center };
        void At(Control c, int col) { Grid.SetColumn(c, col); c.VerticalAlignment = VerticalAlignment.Center; bar.Children.Add(c); }
        var col = 0;
        foreach (var c in left) { At(c, col); col += 2; }
        col--;
        At(play, col + 1);
        At(chip, col + 3);
        At(ToolGroup([undo, redo]), col + 5);
        var toolbar = UI.Frame(bar, "bg/panel", "border/default", new Thickness(0, 0, 0, 1), new Thickness(10, 0)).Named("Toolbar");
        toolbar.Height = 44;
        DockPanel.SetDock(toolbar, Dock.Top);
        return toolbar;
    }

    public static void Tip(EditorServices s, Control c, string commandId)
    {
        var cmd = s.Commands.Get(commandId);
        ToolTip.SetTip(c, new TipContent(cmd.Title, s.Shortcuts.GestureOf(commandId) is { } g ? AgbStudio.Editor.Theme.Shortcuts.Format(g) : null));
    }

    public static Border ToolGroup(IEnumerable<Control> items) =>
        UI.Frame(UI.Row(2, items.ToArray()), "bg/base", "border/default", new Thickness(1), new Thickness(2), 7);

    /// <summary>Document tabs (34 px) of the open documents; clicking one opens its editor.</summary>
    public static Border DocTabs(EditorServices s, string? page = null, string pageGlyph = "book")
    {
        if (page != null) s.Documents.EnsureToolDocument(page, pageGlyph);
        var strip = new DocTabStrip(s);
        var frame = UI.Frame(strip, "bg/panel", "border/default", new Thickness(0, 0, 0, 1)).Named("DocTabs");
        frame.Height = 34;
        return frame;
    }

    /// <summary>The window a document opens in (Editor Map #15-#17 and the asset editors).</summary>
    public static string? WindowFor(EditorDocument d)
    {
        var n = d.Path ?? d.Name;
        if (d.Name == "Game") return "T-P02";
        if (d.Name == "Scene" || n.EndsWith(".scene", StringComparison.OrdinalIgnoreCase)) return "T-P01";
        if (n.EndsWith(".map", StringComparison.OrdinalIgnoreCase)) return "T-P03";
        if (n.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) return "T-P04";
        if (n.EndsWith(".font", StringComparison.OrdinalIgnoreCase)) return "T-P09";
        if (d.Name == "UI Editor" || n.EndsWith(".uipage", StringComparison.OrdinalIgnoreCase)) return "T-P27";
        if (n.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || n.EndsWith(".aseprite", StringComparison.OrdinalIgnoreCase)) return "T-P08";
        // the other editors' documents: a tab of theirs used to make the document active without showing its page
        if (d.Name == "Database" || n.EndsWith(".db", StringComparison.OrdinalIgnoreCase)) return "T-P07";
        if (d.Name == "Localization") return "T-P14";
        if (d.Name == "Scripting API") return "T-P18";
        if (d.Name == "World Map") return "T-P13";
        if (d.Name == "Debugger") return "T-P19";
        if (d.Name == "Audio" || n.EndsWith(".xm", StringComparison.OrdinalIgnoreCase) || n.EndsWith(".mod", StringComparison.OrdinalIgnoreCase)
            || n.EndsWith(".s3m", StringComparison.OrdinalIgnoreCase) || n.EndsWith(".it", StringComparison.OrdinalIgnoreCase)) return "T-P15";
        if (d.Name == "Dialogue Editor" || n.EndsWith(".dlg", StringComparison.OrdinalIgnoreCase)) return "T-P16";
        if (n.EndsWith(".sfx", StringComparison.OrdinalIgnoreCase)) return "T-P25";
        if (d.Name == Palette.PaletteManagerPage.DocumentName) return "T-P10";
        if (d.Name == "Build Report" || n.EndsWith("report.json", StringComparison.OrdinalIgnoreCase)) return "T-P11";
        if (d.Name == "Logic Graph" || (d.Path is null && d.Glyph == "nodes") || n.EndsWith(".graph", StringComparison.OrdinalIgnoreCase)) return "T-P17";
        return null;
    }

    /// <summary>What a document's window is opened with: nothing for the pages that keep one instance (the Scene Editor,
    /// Database, Palette Manager), the file path for the audio and dialogue pages, else the document.</summary>
    static object? ArgsFor(string window, EditorDocument d) => window switch
    {
        "T-P01" or "T-P07" or "T-P10" or "T-P11" or "T-P13" or "T-P14" or "T-P18" or "T-P19" => null,
        "T-P15" or "T-P16" or "T-P25" => d.Path,
        _ => d,
    };

    /// <summary>Shows a document's window. The Scene Editor is reopened without an argument: an argument rebuilt the page
    /// and lost its tool, bottom panel tab and selection. A tab that is already active but whose page is not shown (the
    /// Scene tab on a page without its own document) still goes there instead of doing nothing.</summary>
    public static void Activate(EditorServices s, EditorDocument d)
    {
        var w = WindowFor(d);
        if (s.Documents.Active == d && (w is null || s.Windows.CurrentPage == w)) return;
        s.Documents.Active = d;
        if (w != null) _ = s.Windows.Open(w, ArgsFor(w, d), source: "Doc_" + d.Name);
    }

    /// <summary>A side panel (Hierarchy / Inspector width) with a Panel Header.</summary>
    public static Border SidePanel(PanelHeader header, Control body, bool left, string name)
    {
        var dock = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        dock.Children.Add(header);
        dock.Children.Add(body);
        var b = UI.Frame(dock, "bg/panel", "border/default", left ? new Thickness(0, 0, 1, 0) : new Thickness(1, 0, 0, 0)).Named(name);
        b.ClipToBounds = true;
        return b;
    }

    /// <summary>An Inspector section: Section Header, fields 30 px apart, divider under it (padding as in Figma).</summary>
    public static Border Section(SectionHeader header, IEnumerable<Control> rows, double padX = 12, string? name = null)
    {
        var stack = new StackPanel { Spacing = 6 };
        var body = new StackPanel { Spacing = 6 };
        foreach (var r in rows) body.Children.Add(r);
        header.Margin = new Thickness(0, 0, 0, 1);
        stack.Children.Add(header);
        stack.Children.Add(body);
        header.PropertyChanged += (_, e) => { if (e.Property == SectionHeader.IsOpenProperty) body.IsVisible = header.IsOpen; };
        var b = UI.Frame(stack, null, "border/default", new Thickness(0, 0, 0, 1), new Thickness(padX, 7, padX, 10));
        if (name != null) b.Name = name;
        return b;
    }

    /// <summary>Status bar (26 px): items on the left and the right.</summary>
    public static Border StatusBar(IEnumerable<Control> left, IEnumerable<Control> right)
    {
        var l = UI.Row(16, left.ToArray());
        var r = UI.Row(16, right.ToArray());
        var dock = new DockPanel();
        DockPanel.SetDock(r, Dock.Right);
        dock.Children.Add(r);
        dock.Children.Add(l);
        var bar = UI.Frame(dock, "bg/panel", "border/default", new Thickness(0, 1, 0, 0), new Thickness(12, 0)).Named("StatusBar");
        bar.Height = 26;
        DockPanel.SetDock(bar, Dock.Bottom);
        return bar;
    }

    public static StatusItem ReadyItem(StatusModel m)
    {
        var ready = new StatusItem { Glyph = "check", IsDot = true, Content = m.Ready, Name = "Status_Ready" };
        void Update() { ready.Content = m.Ready; ready.Classes.Set("warn", !m.ToolchainOk); }
        void OnChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => Update();
        Update();
        WhileAttached(ready, () => { m.PropertyChanged += OnChanged; Update(); }, () => m.PropertyChanged -= OnChanged);
        return ready;
    }

    /// <summary>Opens a list of choices under an anchor (dropdown fields); <paramref name="pick"/> gets the chosen index.</summary>
    public static ContextMenu Choose(Control anchor, IReadOnlyList<string> options, string? current, Action<int> pick)
    {
        var menu = new ContextMenu { Name = "Choices", Placement = PlacementMode.BottomEdgeAlignedLeft };
        for (var i = 0; i < options.Count; i++)
        {
            var k = i;
            var item = new MenuItem { Header = options[i], Tag = options[i] };
            if (options[i] == current) { item.ToggleType = MenuItemToggleType.CheckBox; item.IsChecked = true; }
            item.Click += (_, _) => pick(k);
            menu.Items.Add(item);
        }
        menu.Open(anchor);
        LastChoices = menu;
        return menu;
    }

    /// <summary>The choice list opened last (E2E tests pick from it).</summary>
    internal static ContextMenu? LastChoices { get; private set; }

    /// <summary>Calls <paramref name="commit"/> with the text when a field's text box loses focus or Enter is pressed.</summary>
    public static void OnCommit(Control field, Action commit)
    {
        field.AddHandler(InputElement.LostFocusEvent, (_, _) => commit(), RoutingStrategies.Bubble);
        field.AddHandler(InputElement.KeyDownEvent, (_, e) => { if (e.Key == Key.Enter) { commit(); e.Handled = true; } }, RoutingStrategies.Tunnel);
    }

    /// <summary>Raised when the dropdown button inside an Inspector Field or Select is clicked.</summary>
    public static void OnDropdown(Control field, Action open) =>
        field.AddHandler(Button.ClickEvent, (_, e) => { open(); e.Handled = true; }, RoutingStrategies.Bubble);

    public static InspectorField Field(string label, FieldKind kind, string? value = null, string? name = null)
    {
        var f = new InspectorField { Label = label, Kind = kind, Value = value, Name = name ?? "Field_" + label.Replace(" ", "").Replace("·", "") };
        f.TemplateApplied += (_, e) =>
        {
            // The Field's switch sets IsChecked locally, which hides the IsOn binding.
            if (e.NameScope.Find<AgbSwitch>("PART_Toggle") is { } sw)
            {
                sw.ClearValue(ToggleButton.IsCheckedProperty);
                sw.IsCheckedChanged += (_, _) => { if (sw.IsChecked is { } on && on != f.IsOn) f.IsOn = on; };
                sw.IsChecked = f.IsOn;
                f.PropertyChanged += (_, pe) => { if (pe.Property == InspectorField.IsOnProperty && sw.IsChecked != f.IsOn) sw.IsChecked = f.IsOn; };
            }
        };
        return f;
    }

    public static Icon Glyph(string glyph, double size, string token)
    {
        var i = new Icon { Glyph = glyph, Size = size, VerticalAlignment = VerticalAlignment.Center };
        i.Token(Icon.ForegroundProperty, token);
        return i;
    }
}
