using System.Globalization;
using System.Runtime.CompilerServices;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Graphics;
using AgbStudio.Editor.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace AgbStudio.Editor.Windows.Settings;

/// <summary>
/// A dropdown on a Figma "Field" (Dropdown) or "Select": a click opens the options as a menu; choosing one sets the
/// field's text and calls back. <see cref="Choose"/> does the same from code (tests).
/// </summary>
public sealed class Dropdown
{
    static readonly ConditionalWeakTable<Control, Dropdown> ByControl = new();

    readonly Func<IReadOnlyList<string>> _options;
    readonly Action<string> _show;
    readonly Action<string> _changed;

    public Control Host { get; }
    public string Value { get; private set; }
    public IReadOnlyList<string> Options => _options();
    public ContextMenu? Menu { get; private set; }

    Dropdown(Control host, Func<IReadOnlyList<string>> options, string value, Action<string> show, Action<string> changed)
    {
        Host = host;
        _options = options;
        _show = show;
        _changed = changed;
        Value = value;
        show(value);
    }

    /// <summary>The dropdown of a field built by <see cref="SettingsUi"/>, or null.</summary>
    public static Dropdown? Of(Control c) => ByControl.TryGetValue(c, out var d) ? d : null;

    public static Dropdown Attach(TemplatedControl host, string part, Func<IReadOnlyList<string>> options, string value, Action<string> show, Action<string> changed)
    {
        var d = new Dropdown(host, options, value, show, changed);
        ByControl.AddOrUpdate(host, d);
        host.TemplateApplied += (_, e) =>
        {
            if (e.NameScope.Find<Button>(part) is { } b) b.Click += (_, _) => d.Open(b);
        };
        return d;
    }

    /// <summary>A dropdown on a plain button (table cells such as Gamepad or Severity).</summary>
    public static Dropdown Attach(Button button, Func<IReadOnlyList<string>> options, string value, Action<string> show, Action<string> changed)
    {
        var d = new Dropdown(button, options, value, show, changed);
        ByControl.AddOrUpdate(button, d);
        button.Click += (_, _) => d.Open(button);
        return d;
    }

    /// <summary>Opens the option menu under <paramref name="anchor"/>.</summary>
    public void Open(Control anchor)
    {
        var items = new List<Control>();
        foreach (var o in Options)
        {
            var option = o;
            var mi = new MenuItem { Header = option, ToggleType = MenuItemToggleType.CheckBox, IsChecked = option == Value, Command = new Relay(() => Choose(option)) };
            items.Add(mi);
        }
        Menu = new ContextMenu { ItemsSource = items, Placement = PlacementMode.BottomEdgeAlignedLeft };
        Menu.Open(anchor);
    }

    /// <summary>Selects an option (what a click on the menu item does).</summary>
    public void Choose(string option)
    {
        Menu?.Close();
        if (option == Value) return;
        Value = option;
        _show(option);
        _changed(option);
    }

    /// <summary>Shows a value without calling back (the model changed elsewhere).</summary>
    public void Show(string option)
    {
        Value = option;
        _show(option);
    }
}

/// <summary>Builders for the rows of the settings dialogs, all made of the Figma components (Field, Select, Checkbox, Switch, Table Cell, Meter).</summary>
public static class SettingsUi
{
    public const double FieldLabel = 94;   // Figma Field: 86 px label + 8 px gap

    public static TextBlock Title(string text) => UI.Text(text, "Title/14", "text/primary");

    /// <summary>Page title with the caption beside it ("General  Shown on the cartridge header ...").</summary>
    public static Control TitleWithCaption(string title, string caption)
    {
        var t = Title(title);
        var c = UI.Text(caption, "Label/11", "text/muted");
        c.VerticalAlignment = VerticalAlignment.Bottom;
        c.Margin = new Thickness(0, 0, 0, 2);
        var row = UI.Row(10, t, c);
        return row;
    }

    public static TextBlock Overline(string text) => UI.Text(text, "Overline/11", "text/secondary");

    public static TextBlock Caption(string text, bool wrap = false)
    {
        var t = UI.Text(text, "Label/11", "text/muted");
        if (wrap) t.TextWrapping = TextWrapping.Wrap;
        return t;
    }

    public static CheckBox Check(string label, bool value, Action<bool> changed, string? name = null)
    {
        // Figma checkbox rows are 16 px (Body/12 line height); the plain content presenter would make them 15
        var c = new CheckBox { Content = label, IsChecked = value, Name = name, HorizontalAlignment = HorizontalAlignment.Left, MinHeight = 16 };
        c.IsCheckedChanged += (_, _) => changed(c.IsChecked == true);
        return c;
    }

    /// <summary>Figma "Field" (Text): label left, 24 px mono box.</summary>
    public static InspectorField TextRow(string label, string value, Action<string> changed, string? name = null, double width = double.NaN)
    {
        var f = new InspectorField { Kind = FieldKind.Text, Label = label, Value = value, Name = name, Width = width, HorizontalAlignment = double.IsNaN(width) ? HorizontalAlignment.Stretch : HorizontalAlignment.Left };
        f.PropertyChanged += (_, e) => { if (e.Property == InspectorField.ValueProperty) changed(f.Value ?? ""); };
        return f;
    }

    /// <summary>Figma "Field" (Dropdown) with a working option menu.</summary>
    public static InspectorField DropdownRow(string label, Func<IReadOnlyList<string>> options, string value, Action<string> changed, string? name = null, double width = double.NaN)
    {
        var f = new InspectorField { Kind = FieldKind.Dropdown, Label = label, Name = name, Width = width, HorizontalAlignment = double.IsNaN(width) ? HorizontalAlignment.Stretch : HorizontalAlignment.Left };
        Dropdown.Attach(f, "PART_Dropdown", options, value, v => f.Value = v, changed);
        return f;
    }

    public static InspectorField DropdownRow(string label, IReadOnlyList<string> options, string value, Action<string> changed, string? name = null, double width = double.NaN) =>
        DropdownRow(label, () => options, value, changed, name, width);

    /// <summary>Figma "Select": label above a 32 px box with a working option menu.</summary>
    public static SelectField Select(string label, Func<IReadOnlyList<string>> options, string value, Action<string> changed, string? name = null)
    {
        var f = new SelectField { Label = label, Name = name };
        Dropdown.Attach(f, "PART_Box", options, value, v => f.Selected = v, changed);
        return f;
    }

    /// <summary>Figma "Text Input": label above a 32 px box, optional help line.</summary>
    public static TextField Text(string label, string value, Action<string> changed, string? help = null, string? name = null)
    {
        var f = new TextField { Label = label, Text = value, Help = help, Name = name };
        f.PropertyChanged += (_, e) => { if (e.Property == TextField.TextProperty) changed(f.Text ?? ""); };
        return f;
    }

    /// <summary>Figma "Field" (Toggle): label left and a Switch.</summary>
    public static InspectorField ToggleRow(string label, bool value, Action<bool> changed, string? name = null)
    {
        var f = new InspectorField { Kind = FieldKind.Toggle, Label = label, IsOn = value, Name = name };
        f.TemplateApplied += (_, e) =>
        {
            if (e.NameScope.Find<AgbSwitch>("PART_Toggle") is not { } sw) return;
            sw.IsChecked = f.IsOn;
            sw.IsCheckedChanged += (_, _) =>
            {
                if (f.IsOn == (sw.IsChecked == true)) return;
                f.IsOn = sw.IsChecked == true;
                changed(f.IsOn);
            };
        };
        return f;
    }

    /// <summary>Label left (86 px, wraps like "Pause when unfocused") and a Figma "Switch".</summary>
    public static Control SwitchRow(string label, bool value, Action<bool> changed, string? name = null)
    {
        var sw = new AgbSwitch { IsChecked = value, Name = name, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };
        sw.IsCheckedChanged += (_, _) => changed(sw.IsChecked == true);
        var t = UI.Text(label, "Body/12", "text/secondary", 86);
        t.TextWrapping = TextWrapping.Wrap;
        var row = new DockPanel { HorizontalSpacing = 8, MinHeight = 24 };
        DockPanel.SetDock(t, Dock.Left);
        row.Children.AddRange([t, sw]);
        return row;
    }

    /// <summary>A Figma Segment group: rounded box (bg/base, border/default) holding Segments.</summary>
    public static Border Segments(IReadOnlyList<string> labels, int selected, Action<int> changed, string name)
    {
        var group = name + "_" + Guid.NewGuid().ToString("N");
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        for (var i = 0; i < labels.Count; i++)
        {
            var index = i;
            var s = new Segment { Content = labels[i], GroupName = group, IsChecked = i == selected, Name = name + "_" + i };
            s.IsCheckedChanged += (_, _) => { if (s.IsChecked == true) changed(index); };
            row.Children.Add(s);
        }
        var b = UI.Frame(row, "bg/base", "border/default", pad: new Thickness(2), radius: 7);
        b.HorizontalAlignment = HorizontalAlignment.Left;
        b.Name = name;
        return b;
    }

    /// <summary>A small rounded card (bg/base) used for the Toolchain and Build cache rows.</summary>
    public static Border Card(Control child) => UI.Frame(child, "bg/base", pad: new Thickness(14, 0, 12, 0), radius: 8);

    /// <summary>Header row of a settings table: Figma "Table Cell" Header per column.</summary>
    public static Grid TableHeader(IReadOnlyList<string> titles, string columns)
    {
        var g = new Grid { ColumnDefinitions = ColumnDefinitions.Parse(columns) };
        for (var i = 0; i < g.ColumnDefinitions.Count; i++)
        {
            var c = new TableCell { Kind = TableCellKind.Header, Text = i < titles.Count ? titles[i] : "" };
            Grid.SetColumn(c, i);
            g.Children.Add(c);
        }
        return g;
    }

    /// <summary>One table row: cells in columns, a bottom border/default line, Figma row height.</summary>
    public static Border TableRow(string columns, double height, params Control[] cells)
    {
        var g = new Grid { ColumnDefinitions = ColumnDefinitions.Parse(columns) };
        for (var i = 0; i < cells.Length; i++)
        {
            var c = cells[i];
            if (c is not TableCell) c.Margin = new Thickness(10, 0, 0, 0);
            c.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(c, i);
            g.Children.Add(c);
        }
        var b = UI.Frame(g, stroke: "border/default", strokes: new Thickness(0, 0, 0, 1));
        b.Height = height;
        return b;
    }

    /// <summary>A Figma "Table Cell" without its own border (the row draws it).</summary>
    public static TableCell Cell(string text, bool mono = false)
    {
        var c = new TableCell { Text = text, Kind = mono ? TableCellKind.Mono : TableCellKind.Default, Height = double.NaN, BorderThickness = default };
        c.TemplateApplied += (_, e) =>
        {
            if (Avalonia.VisualTree.VisualExtensions.GetVisualChildren(c).FirstOrDefault() is Border b) b.BorderThickness = default;
        };
        return c;
    }

    /// <summary>A text box drawn as plain table text until it is focused (editable table cells).</summary>
    public static TextBox CellBox(string text, string style, Action<string> changed, string? name = null)
    {
        var t = new TextBox { Text = text, Name = name, Theme = (Avalonia.Styling.ControlTheme?)Application.Current?.FindResource("BareTextBox"), VerticalAlignment = VerticalAlignment.Center };
        AgbStudio.Editor.Theme.Fig.SetStyle(t, style);
        t.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) changed(t.Text ?? ""); };
        return t;
    }

    /// <summary>A status mark: check (found) or warning (not set / not found).</summary>
    public static Icon StatusIcon(bool ok)
    {
        var i = new Icon { Glyph = ok ? "check" : "warn", Size = 16, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        return i.Token(Icon.ForegroundProperty, ok ? "status/success" : "status/warning");
    }

    /// <summary>The first whole number in a text ("16 MB" -> 16), or null.</summary>
    public static int? Number(string text)
    {
        var digits = new string(text.SkipWhile(c => !char.IsDigit(c)).TakeWhile(char.IsDigit).ToArray());
        return digits.Length > 0 && digits.Length < 9 ? int.Parse(digits, CultureInfo.InvariantCulture) : null;
    }
}
