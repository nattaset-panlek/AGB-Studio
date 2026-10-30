using System.Globalization;
using AgbStudio.Build.Data;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using BuildCpp = AgbStudio.Build.Pipeline.Cpp;

namespace AgbStudio.Editor.Windows.Database;

/// <summary>
/// Add Field (T-D18, 520 x 516; Database › Schema +): field name, type (int · fixed · bool · text · enum · sprite · sound
/// · script · reference), enum values with "Add value", default, "Show in the record inspector", "Localized" and the
/// ROM cost per row (DatabaseCodegen.FieldBytes, e.g. "1 B · 24 rows" for an enum).
/// </summary>
[EditorWindow("T-D18")]
public sealed class AddFieldDialog : FigmaDialog
{
    public static readonly IReadOnlyList<string> Types = ["int", "fixed", "bool", "text", "enum", "sprite", "sound", "script", "reference"];

    readonly DatabasePage? _page;
    readonly int _rows;
    readonly StackPanel _enumSection;
    readonly WrapPanel _values = new() { Name = "EnumValues", ItemSpacing = 8, LineSpacing = 8 };
    readonly AgbButton _addValue = new() { Content = "Add value", Glyph = "plus", Kind = ButtonKind.Ghost, Name = "AddValue", Padding = new Thickness(6, 0) };
    readonly AgbProgress _bar = new() { Status = ProgressStatus.OK, VerticalAlignment = VerticalAlignment.Center, Name = "PerRowBar" };
    readonly TextBlock _perRow = UI.Text("", "Mono/10", "text/secondary").Named("PerRowText");
    readonly Panel _defaultHost = new() { Name = "DefaultHost" };

    public TextField NameField { get; } = new() { Label = "Field name", Name = "FieldName" };
    public SelectField TypeField { get; } = new() { Label = "Type", Options = Types, Selected = "enum", Help = "int · fixed · bool · text · enum · sprite · sound · script · reference", Name = "FieldType" };
    public SelectField DefaultSelect { get; } = new() { Label = "Default", Name = "DefaultSelect" };
    public TextField DefaultText { get; } = new() { Label = "Default", Name = "DefaultText" };
    public SelectField ReferenceTable { get; } = new() { Label = "Table", Name = "ReferenceTable" };
    public CheckBox ShowInInspector { get; } = new() { Content = "Show in the record inspector", IsChecked = true, Name = "ShowInInspector" };
    public CheckBox Localized { get; } = new() { Content = "Localized", IsChecked = false, Name = "Localized" };
    public List<string> EnumValues { get; } = [];
    public string? Error { get; private set; }

    public AddFieldDialog(WindowContext ctx)
    {
        Name = "AddFieldDialog";
        _page = ctx.Args as DatabasePage ?? ctx.Services.Windows.Host?.CurrentPage as DatabasePage;
        _rows = _page?.Current?.Table.Rows.Count ?? 0;
        Dropdown.Attach(TypeField, () => Types, _ => Update());
        Dropdown.Attach(DefaultSelect, () => DefaultOptions(), _ => { });
        ReferenceTable.Options = _page?.Data?.Tables.Select(t => t.Name).ToList() ?? [];
        ReferenceTable.Selected = ReferenceTable.Options.FirstOrDefault();
        Dropdown.Attach(ReferenceTable, () => ReferenceTable.Options ?? [], _ => Update());
        NameField.PropertyChanged += (_, e) => { if (e.Property == TextField.TextProperty && Error != null) { Error = null; NameField.Help = null; } };
        _addValue.Click += (_, _) => BeginAddValue();

        _enumSection = UI.Column(14, Ui.Overline("Enum values"), _values);
        _enumSection.Name = "EnumSection";
        var perRow = new DockPanel { HorizontalSpacing = 0, Height = 16 };
        var label = UI.Text("Per row", "Label/11", "text/secondary", 82);
        DockPanel.SetDock(label, Dock.Left);
        _perRow.Margin = new Thickness(8, 0, 0, 0);
        DockPanel.SetDock(_perRow, Dock.Right);
        perRow.Children.AddRange([label, _perRow, _bar]);

        var content = UI.Column(0,
            NameField,
            TypeField.Margin(0, 14, 0, 0),
            _enumSection.Margin(0, 14, 0, 0),
            _defaultHost.Margin(0, 14, 0, 0),
            ShowInInspector.Margin(0, 14, 0, 0),
            Localized.Margin(0, 15, 0, 0),
            perRow.Margin(0, 15, 0, 0));
        content.Margin = new Thickness(20, 20, 20, 0);
        var footer = new DialogFooter { ConfirmText = "Add Field" };
        footer.Cancel += (_, _) => Close(null);
        footer.Confirm += (_, _) => { if (Create() is { } f) Close(f); };
        Build("Add Field", "plus", content, footer, 520);
        Ui.FocusOnOpen(this, NameField);
        Update();
    }

    IReadOnlyList<string> DefaultOptions() => TypeField.Selected switch
    {
        "enum" => EnumValues,
        "bool" => ["no", "yes"],
        "sprite" => SpriteLibrary.Names(_page?.Data?.Project),
        "sound" => _page?.Data is { } d ? d.Project.SoundFiles().Concat(d.Project.SfxFiles()).Select(GameProject.AssetName).Distinct().Order(StringComparer.Ordinal).ToList() : [],
        "reference" => _page?.Data?.Table(ReferenceTable.Selected ?? "") is { } t ? t.Table.Rows.Select(r => r.GetValueOrDefault(t.Table.KeyField) ?? "").ToList() : [],
        _ => [],
    };

    public void SetType(string type)
    {
        TypeField.Selected = type;
        Update();
    }

    public void AddValue(string value)
    {
        value = value.Trim();
        if (value.Length == 0 || EnumValues.Contains(value)) return;
        EnumValues.Add(value);
        if (EnumValues.Count == 1 || string.IsNullOrEmpty(DefaultSelect.Selected)) DefaultSelect.Selected = value;
        Update();
    }

    void BeginAddValue()
    {
        var box = new TextBox { Width = 110, Height = 24, Padding = new Thickness(8, 0), Name = "NewEnumValue" };
        _values.Children.Insert(_values.Children.Count - 1, box);
        var done = false;
        void Finish(bool commit)
        {
            if (done) return;
            done = true;
            _values.Children.Remove(box);
            if (commit) AddValue(box.Text ?? "");
        }
        box.AddHandler(Avalonia.Input.InputElement.KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Enter) { Finish(true); e.Handled = true; }
            else if (e.Key == Key.Escape) { Finish(false); e.Handled = true; }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        Ui.EditorFocus(box, () => Finish(true));
    }

    void Update()
    {
        var type = TypeField.Selected ?? "int";
        _values.Children.Clear();
        foreach (var v in EnumValues)
        {
            var chip = new Chip { Content = v, Style = ChipStyle.Accent, Name = "Value_" + v };
            ToolTip.SetTip(chip, new TipContent("Remove " + v));
            var value = v;
            chip.Click += (_, _) => { EnumValues.Remove(value); if (DefaultSelect.Selected == value) DefaultSelect.Selected = EnumValues.FirstOrDefault(); Update(); };
            _values.Children.Add(chip);
        }
        _values.Children.Add(_addValue);
        _enumSection.IsVisible = type == "enum";
        _defaultHost.Children.Clear();
        if (type == "reference") _defaultHost.Children.Add(UI.Column(14, ReferenceTable, DefaultSelect));
        else if (type is "enum" or "bool" or "sprite" or "sound") _defaultHost.Children.Add(DefaultSelect);
        else if (type != "script") _defaultHost.Children.Add(DefaultText);
        if (type == "bool" && string.IsNullOrEmpty(DefaultSelect.Selected)) DefaultSelect.Selected = "no";
        Localized.IsEnabled = type == "text";
        if (type != "text") Localized.IsChecked = false;
        var bytes = DatabaseCodegen.FieldBytes(Def());
        _bar.Value = Math.Min(100, bytes * 10);
        _perRow.Text = $"{bytes.ToString(CultureInfo.InvariantCulture)} B · {_rows.ToString(CultureInfo.InvariantCulture)} rows";
    }

    /// <summary>The field the dialog describes.</summary>
    public FieldDef Def()
    {
        var type = TypeField.Selected ?? "int";
        var def = type is "enum" or "bool" or "sprite" or "sound" or "reference" ? DefaultSelect.Selected ?? "" : type == "script" ? "" : DefaultText.Text ?? "";
        if (type == "bool") def = TableGrid.IsTrue(def) ? "yes" : "no";
        return new FieldDef
        {
            Name = (NameField.Text ?? "").Trim(), Type = type, EnumValues = type == "enum" ? [.. EnumValues] : [], Default = def.Trim(),
            Localized = type == "text" && Localized.IsChecked == true, ShowInInspector = ShowInInspector.IsChecked == true,
            Reference = type == "reference" ? ReferenceTable.Selected ?? "" : "",
        };
    }

    /// <summary>Validates and adds the field to the table; returns it, or null with <see cref="Error"/> set.</summary>
    public FieldDef? Create()
    {
        var f = Def();
        var t = _page?.Current?.Table;
        Error = f.Name.Length == 0 ? "Enter a field name."
            : BuildCpp.Ident(f.Name) != f.Name || !char.IsLetter(f.Name[0]) ? "Use letters, digits and _ (a C# name)."
            : t is null ? "Select a table first."
            : t.Fields.Any(x => string.Equals(x.Name, f.Name, StringComparison.OrdinalIgnoreCase)) ? $"{t.Name} already has a field named {f.Name}."
            : f.Type == "enum" && f.EnumValues.Count == 0 ? "Add at least one value."
            : f.Type == "int" && f.Default.Length > 0 && !int.TryParse(f.Default, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) ? "The default must be a whole number."
            : f.Type == "fixed" && f.Default.Length > 0 && !double.TryParse(f.Default, NumberStyles.Float, CultureInfo.InvariantCulture, out _) ? "The default must be a number."
            : null;
        if (Error != null) { NameField.Help = Error; return null; }
        _page!.AddField(f);
        return f;
    }
}
