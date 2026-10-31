using AgbStudio.Editor.Graphics;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Theme;
using AgbStudio.Editor.Windows.Database;
using AgbStudio.Scripting.Dialogue;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace AgbStudio.Editor.Windows.Dialogue;

/// <summary>The center of the Dialogue Editor: line cards (portrait, speaker, text in both languages), choice cards with their targets, command cards and the "Add line · choice · command" box.</summary>
public static class LineCards
{
    public static readonly IReadOnlyDictionary<string, (string Title, string Glyph)> CommandInfo = new Dictionary<string, (string, string)>
    {
        ["goToScene"] = ("Go to scene", "map"), ["setFlag"] = ("Set flag", "flag"), ["giveItem"] = ("Give item", "gift"),
        ["playSound"] = ("Play sound", "sound"), ["wait"] = ("Wait", "clock"), ["shakeCamera"] = ("Shake camera", "camera"), ["end"] = ("End", "x"),
    };

    /// <summary>What a command does, as C#-like text.</summary>
    public static string Summary(DialogueLine l) => l.Command switch
    {
        "goToScene" => $"Scene.Load(\"{l.Scene}\")",
        "setFlag" => $"Flags.{l.Flag} = {((l.Value ?? true) ? "true" : "false")}",
        "giveItem" => $"{l.Table}.Give({AgbStudio.Build.Data.DatabaseCodegen.IdEnum(l.Table ?? "Items")}.{l.Item}, {l.Count ?? 1})",
        "playSound" => $"Sound.Play(\"{l.Sound}\")",
        "wait" => l.Seconds is { } s ? $"Wait({s.ToString(System.Globalization.CultureInfo.InvariantCulture)}s)" : $"Wait({l.Frames ?? 0} frames)",
        "shakeCamera" => $"Camera.Shake({l.Amplitude ?? 2}, {l.Frames ?? 20})",
        "end" => "End of conversation",
        _ => l.Command ?? "",
    };

    static Border Frame(Control child, bool selected, bool accentBorder = false)
    {
        var b = new Border { Child = child, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), Padding = new Thickness(12) };
        b.Token(Border.BackgroundProperty, selected ? "accent/selected" : "bg/panel");
        b.Token(Border.BorderBrushProperty, selected || accentBorder ? "accent/default" : "border/default");
        return b;
    }

    public static Control Card(DialogueEditorPage page, DialogueDoc doc, Conversation c, int index, bool selected)
    {
        var l = c.Lines[index];
        var portrait = new Border { Width = 44, Height = 44, CornerRadius = new CornerRadius(8), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) }.Token(Border.BackgroundProperty, "bg/base");
        DockPanel.SetDock(portrait, Dock.Left);
        Control body;
        if (l.Command != null)
        {
            var (title, glyph) = CommandInfo.GetValueOrDefault(l.Command, (l.Command, "code"));
            portrait.Child = new Icon { Glyph = glyph, Size = 18, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }.Token(Icon.ForegroundProperty, "kind/script");
            body = UI.Column(4, UI.Text(title, "Body/12 Semibold", "text/primary"), UI.Text(Summary(l), "Mono/11", "text/secondary"));
        }
        else
        {
            var sprite = l.Portrait is { } p && SpriteLibrary.Frame(page.Data?.Project, p) is { } own ? own
                : SpriteLibrary.Frame(page.Data?.Project, doc.File.Speakers.FirstOrDefault(s => s.Name == l.Speaker)?.Portrait);
            portrait.Child = new PixelImage { Pixels = sprite, MaxScale = 2 };
            var key = page.KeyOf(l, index);
            var primary = EditableText(page, page.TextOf(key, l.Text, page.Primary) ?? "", "Thai/13", "text/primary", index, -1, page.Primary);
            var secondary = EditableText(page, page.TextOf(key, l.Text, page.Secondary) ?? "", "Label/11", "text/muted", index, -1, page.Secondary);
            primary.Margin = new Thickness(0, 2, 0, 0);
            secondary.Margin = new Thickness(0, 4, 0, 0);
            body = UI.Column(0, UI.Text(l.Speaker ?? "—", "Body/12 Semibold", "text/primary"), primary, secondary);
        }
        body.VerticalAlignment = VerticalAlignment.Center;
        var card = Frame(new DockPanel { Children = { portrait, body } }, selected);
        card.Name = "Line_" + (index + 1);
        card.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(card).Properties.IsLeftButtonPressed) return;
            if (page.SelectedLine != index || page.SelectedChoice >= 0 || page.AllLinesSelected) page.SelectLine(index);
            card.Focus();
            e.Handled = true;
        };
        card.Focusable = true;
        return card;
    }

    public static Control Choices(DialogueEditorPage page, Conversation c, int index, List<DialogueChoice> choices)
    {
        var grid = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("*,10,*"), Margin = new Thickness(56, 0, 0, 0), Name = "Choices_" + (index + 1) };
        var line = c.Lines[index];
        var key = page.KeyOf(line, index);
        for (var i = 0; i < choices.Count; i++)
        {
            if (i % 2 == 0) grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var ch = choices[i];
            var ck = ch.Key ?? $"{key}.choice{i}";
            var chevron = new Icon { Glyph = "right", Size = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) }.Token(Icon.ForegroundProperty, "accent/default");
            var primary = EditableText(page, page.TextOf(ck, ch.Text, page.Primary) ?? "", "Thai/13", "text/primary", index, i, page.Primary);
            var secondary = EditableText(page, page.TextOf(ck, ch.Text, page.Secondary) ?? "", "Label/11", "text/muted", index, i, page.Secondary);
            secondary.Margin = new Thickness(0, 4, 0, 0);
            var target = TargetChip(page, c, index, i, ch);
            target.Margin = new Thickness(0, 4, 0, 0);
            var head = new DockPanel();
            DockPanel.SetDock(chevron, Dock.Left);
            head.Children.AddRange([chevron, primary]);
            var selected = page.SelectedLine == index && page.SelectedChoice == i;
            var card = Frame(UI.Column(0, head, secondary, target), selected, accentBorder: true);
            card.Name = $"Choice_{index + 1}_{i + 1}";
            var choice = i;
            card.PointerPressed += (_, e) =>
            {
                if (!e.GetCurrentPoint(card).Properties.IsLeftButtonPressed) return;
                if (page.SelectedLine != index || page.SelectedChoice != choice) page.SelectLine(index, choice);
                e.Handled = true;
            };
            Grid.SetRow(card, i / 2);
            Grid.SetColumn(card, i % 2 * 2);
            if (i >= 2) card.Margin = new Thickness(0, 10, 0, 0);
            grid.Children.Add(card);
        }
        return grid;
    }

    /// <summary>The target of a choice (<c>Scene.Load("Cave_01")</c>, <c>goto line 2</c>, <c>end</c>); click to change it.</summary>
    static Button TargetChip(DialogueEditorPage page, Conversation c, int line, int choice, DialogueChoice ch)
    {
        var text = DialogueEditorPage.TargetText(c, ch);
        var icon = new Icon { Glyph = "code", Size = 12, VerticalAlignment = VerticalAlignment.Center }.Token(Icon.ForegroundProperty, "kind/script");
        var label = UI.Text(text, "Mono/10", "kind/script");
        var box = UI.Frame(UI.Row(6, icon, label), "bg/base", pad: new Thickness(8, 0), radius: 5);
        box.Height = 22;
        var b = new Button { Theme = (Avalonia.Styling.ControlTheme?)Application.Current?.FindResource("BareButton"), Content = box, HorizontalAlignment = HorizontalAlignment.Left, Name = $"Target_{line + 1}_{choice + 1}" };
        b.Click += (_, e) =>
        {
            e.Handled = true;
            Dropdown.Show(b, page.Targets(c), text, v => page.SetTarget(line, choice, v));
        };
        return b;
    }

    /// <summary>Text that turns into a text box on double-click (Enter or leaving the box saves it as one undo step).</summary>
    static Control EditableText(DialogueEditorPage page, string text, string style, string token, int line, int choice, string lang)
    {
        var tb = UI.Text(text, style, token);
        tb.TextWrapping = TextWrapping.Wrap;
        var host = new Panel { Children = { tb }, Name = $"Text_{lang}_{line + 1}_{choice + 1}" };
        tb.PointerPressed += (_, e) =>
        {
            if (e.ClickCount < 2) return;
            e.Handled = true;
            var box = new TextBox { Text = text, Padding = new Thickness(6, 2), Height = double.NaN, MinHeight = 24, Name = "LineEditor" };
            Fig.SetStyle(box, style);
            host.Children.Add(box);
            var done = false;
            void Finish(bool commit)
            {
                if (done) return;
                done = true;
                host.Children.Remove(box);
                if (commit && (box.Text ?? "") != text) page.SetText(line, choice, lang, box.Text ?? "");
            }
            box.AddHandler(Avalonia.Input.InputElement.KeyDownEvent, (_, k) =>
            {
                if (k.Key == Key.Enter) { Finish(true); k.Handled = true; }
                else if (k.Key == Key.Escape) { Finish(false); k.Handled = true; }
            }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            Ui.EditorFocus(box, () => Finish(true));
        };
        return host;
    }

    public static Border AddBox(DialogueEditorPage page)
    {
        var dash = new Rectangle { RadiusX = 8, RadiusY = 8, StrokeThickness = 1, StrokeDashArray = [4, 3] }.Token(Shape.StrokeProperty, "border/strong");
        var plus = new Icon { Glyph = "plus", Size = 13, VerticalAlignment = VerticalAlignment.Center }.Token(Icon.ForegroundProperty, "text/muted");
        var row = UI.Row(8, plus, UI.Text("Add line · choice · command", "Body/12", "text/muted"));
        row.HorizontalAlignment = HorizontalAlignment.Center;
        row.VerticalAlignment = VerticalAlignment.Center;
        var b = new Border { Height = 40, Background = Brushes.Transparent, Child = new Panel { Children = { dash, row } }, Name = "AddLineBox", Cursor = new Cursor(StandardCursorType.Hand) };
        b.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(b).Properties.IsLeftButtonPressed) return;
            e.Handled = true;
            _ = page.Services.Windows.Open("T-M23", anchor: b, source: "Dialogue.AddBox");
        };
        return b;
    }
}
