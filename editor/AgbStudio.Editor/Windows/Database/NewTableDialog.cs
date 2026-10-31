using System.Globalization;
using AgbStudio.Build.Data;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using BuildCpp = AgbStudio.Build.Pipeline.Cpp;

namespace AgbStudio.Editor.Windows.Database;

/// <summary>
/// New Table (T-D17, 520 x 440; Database › Tables +): table name with the C# name it gets, start from Empty / a CSV
/// file / a copy of the selected table, the key field (id · enum), "Localize text fields (name, description)" and
/// "Keep in ROM as const data". Create Table writes data/&lt;Name&gt;.db (undoable).
/// </summary>
[EditorWindow("T-D17")]
public sealed class NewTableDialog : FigmaDialog
{
    public enum Start { Empty, Csv, Copy }

    readonly DatabasePage? _page;
    readonly ProjectData? _data;
    readonly TableDoc? _source;
    readonly Segment _empty, _csv, _copy;
    public TextField NameField { get; } = new() { Label = "Table name", Name = "TableName" };
    public SelectField KeyField { get; } = new() { Label = "Key field", Options = ["id · enum"], Selected = "id · enum", Help = "Every row gets a name you can autocomplete in C#", Name = "KeyField" };
    public CheckBox Localize { get; } = new() { Content = "Localize text fields (name, description)", IsChecked = true, Name = "LocalizeText" };
    public CheckBox KeepInRom { get; } = new() { Content = "Keep in ROM as const data", IsChecked = true, Name = "KeepInRom" };
    public Start From { get; private set; } = Start.Empty;
    public string? CsvPath { get; private set; }
    public string? Error { get; private set; }

    public NewTableDialog(WindowContext ctx)
    {
        Name = "NewTableDialog";
        _page = ctx.Args as DatabasePage ?? ctx.Services.Windows.Host?.CurrentPage as DatabasePage;
        _data = _page?.Data ?? ProjectData.For(ctx.Services);
        _source = _page?.Current;
        var group = "NewTableStart" + GetHashCode().ToString(CultureInfo.InvariantCulture);
        _empty = new Segment { Content = "Empty", GroupName = group, IsChecked = true, Name = "Start_Empty" };
        _csv = new Segment { Content = "CSV file…", GroupName = group, Name = "Start_Csv" };
        _copy = new Segment { Content = "Copy " + (_source?.Name ?? "Items"), GroupName = group, Name = "Start_Copy", IsVisible = _source != null };
        _empty.IsCheckedChanged += (_, _) => { if (_empty.IsChecked == true) { From = Start.Empty; UpdateHelp(); } };
        _copy.IsCheckedChanged += (_, _) => { if (_copy.IsChecked == true) { From = Start.Copy; UpdateHelp(); } };
        _csv.Click += async (_, _) =>
        {
            var path = await FileChooser.For(ctx.Services).Open("New Table from CSV", "csv");
            if (path is null) { (From == Start.Copy ? _copy : _empty).IsChecked = true; return; }
            CsvPath = path;
            From = Start.Csv;
            if (string.IsNullOrWhiteSpace(NameField.Text)) NameField.Text = Pascal(Path.GetFileNameWithoutExtension(path));
            UpdateHelp();
        };
        NameField.PropertyChanged += (_, e) => { if (e.Property == TextField.TextProperty) { Error = null; UpdateHelp(); } };

        var segments = TableGroup(UI.Row(2, _empty, _csv, _copy));
        var content = UI.Column(0,
            NameField,
            Ui.Overline("Start from").Margin(0, 13, 0, 0),
            segments.Margin(0, 15, 0, 0),
            KeyField.Margin(0, 15, 0, 0),
            Localize.Margin(0, 13, 0, 0),
            KeepInRom.Margin(0, 15, 0, 0));
        content.Margin = new Thickness(20, 20, 20, 0);
        var footer = new DialogFooter { ConfirmText = "Create Table" };
        footer.Cancel += (_, _) => Close(null);
        footer.Confirm += (_, _) => { if (Create() is { } t) Close(t); };
        Build("New Table", "table", content, footer, 520);
        Ui.FocusOnOpen(this, NameField);
        UpdateHelp();
    }

    /// <summary>Segments in a Figma group frame (bg/base, border, radius 7, padding 2), hugging their width.</summary>
    static Border TableGroup(Control row)
    {
        var b = ToolPage.ToolGroup([row]);
        b.HorizontalAlignment = HorizontalAlignment.Left;
        return b;
    }

    public void StartFrom(Start s, string? csv = null)
    {
        if (csv != null) CsvPath = csv;
        From = s;
        (s switch { Start.Csv => _csv, Start.Copy => _copy, _ => _empty }).IsChecked = true;
        From = s;
        UpdateHelp();
    }

    static string Pascal(string s) => AgbStudio.Scripting.ProjectApi.Pascal(s);

    string SampleId()
    {
        var name = (NameField.Text ?? "").Trim();
        if (From == Start.Copy && _source?.Table.Rows.FirstOrDefault() is { } r) return r.GetValueOrDefault(_source.Table.KeyField) ?? "";
        if (From == Start.Csv && CsvPath != null && File.Exists(CsvPath))
        {
            var rows = CsvImporter.Parse(File.ReadAllText(CsvPath));
            if (rows.Count > 1 && rows[1].Length > 0) return BuildCpp.Ident(rows[1][0]);
        }
        // a table of that name in the project: its first row (Figma: "Enemies" gives EnemyId.Bat); else the first new row
        if (_data?.Tables.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)) is { } same
            && same.Table.Rows.FirstOrDefault()?.GetValueOrDefault(same.Table.KeyField) is { Length: > 0 } first) return BuildCpp.Ident(first);
        return BuildCpp.Ident(DatabaseCodegen.Singular(name.Length == 0 ? "Items" : name)) + "1";
    }

    void UpdateHelp()
    {
        var name = (NameField.Text ?? "").Trim();
        NameField.Help = Error ?? (name.Length == 0 ? "Scripts use it as Enemies.Get(EnemyId.Bat)" : $"Scripts use it as {BuildCpp.Ident(name)}.Get({DatabaseCodegen.IdEnum(name)}.{SampleId()})");
    }

    /// <summary>Validates and creates the table; returns it, or null with <see cref="Error"/> set.</summary>
    public TableDoc? Create()
    {
        var name = (NameField.Text ?? "").Trim();
        Error = name.Length == 0 ? "Enter a table name."
            : BuildCpp.Ident(name) != name || !char.IsLetter(name[0]) ? "Use letters, digits and _ (a C# name)."
            : _data is null ? "Open a project first."
            : _data.Tables.Any(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)) ? $"A table named {name} already exists."
            : null;
        if (Error != null) { UpdateHelp(); return null; }
        var (table, texts) = Build(name);
        return _page?.CreateTable(table, texts);
    }

    (TableFile Table, Dictionary<(string Lang, string Key), string> Texts) Build(string name)
    {
        var localize = Localize.IsChecked == true;
        var table = new TableFile { Name = name, KeyField = "id", KeepInRom = KeepInRom.IsChecked == true };
        var texts = new Dictionary<(string, string), string>();
        switch (From)
        {
            case Start.Copy when _source != null:
                foreach (var f in _source.Table.Fields)
                    table.Fields.Add(new FieldDef { Name = f.Name, Type = f.Type, EnumValues = [.. f.EnumValues], Default = f.Default, Localized = f.Type == "text" && f.Name != _source.Table.KeyField ? localize || f.Localized : f.Localized, ShowInInspector = f.ShowInInspector, Reference = f.Reference });
                table.KeyField = _source.Table.KeyField;
                foreach (var r in _source.Table.Rows) table.Rows.Add(new Dictionary<string, string>(r));
                break;
            case Start.Csv when CsvPath != null && File.Exists(CsvPath):
                FromCsv(table, File.ReadAllText(CsvPath), localize, texts);
                break;
            default:
                table.Fields.Add(new FieldDef { Name = "id", Type = "enum" });
                table.Fields.Add(new FieldDef { Name = "name", Type = "text", Localized = localize });
                table.Fields.Add(new FieldDef { Name = "description", Type = "text", Localized = localize });
                break;
        }
        return (table, texts);
    }

    /// <summary>Fields from the CSV header (first column = id, types guessed from the values; "Name_EN" / "Name_TH" become one localized field) and its rows.</summary>
    void FromCsv(TableFile table, string csv, bool localize, Dictionary<(string, string), string> texts)
    {
        var rows = CsvImporter.Parse(csv);
        table.Fields.Add(new FieldDef { Name = "id", Type = "enum" });
        if (rows.Count == 0) return;
        var header = rows[0];
        var langs = _data?.Languages.Select(l => l.Code).ToHashSet() ?? ["en"];
        var columns = new List<(int Col, string Field, string? Lang)>();
        for (var c = 1; c < header.Length; c++)
        {
            var (baseName, lang) = ImportCsvDialog.SplitLanguage(header[c], langs);
            var field = BuildCpp.Ident(baseName.ToLowerInvariant());
            var values = rows.Skip(1).Select(r => c < r.Length ? r[c].Trim() : "").Where(v => v.Length > 0).ToList();
            if (table.Fields.FirstOrDefault(f => f.Name == field) is null)
            {
                var type = lang != null ? "text" : Guess(values);
                table.Fields.Add(new FieldDef { Name = field, Type = type, Localized = type == "text" && (localize || lang != null) });
            }
            columns.Add((c, field, lang));
        }
        var src = _data?.Source ?? "en";
        for (var r = 1; r < rows.Count; r++)
        {
            var id = BuildCpp.Ident(rows[r].Length > 0 ? rows[r][0].Trim() : "");
            if (id.Length == 0) continue;
            var row = new Dictionary<string, string> { ["id"] = id };
            foreach (var (c, field, lang) in columns)
            {
                var v = c < rows[r].Length ? rows[r][c].Trim() : "";
                var f = table.Fields.First(x => x.Name == field);
                if (f.Type == "text" && f.Localized)
                {
                    var key = $"{DatabaseCodegen.Singular(table.Name).ToLowerInvariant()}.{id.ToLowerInvariant()}.{field}";
                    row[field] = key;
                    if (v.Length > 0) texts[(lang ?? src, key)] = v;
                }
                else row[field] = v;
            }
            table.Rows.Add(row);
        }
    }

    static string Guess(List<string> values)
    {
        if (values.Count == 0) return "text";
        if (values.All(v => int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))) return "int";
        if (values.All(v => double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out _))) return "fixed";
        if (values.All(v => v.ToLowerInvariant() is "true" or "false" or "yes" or "no")) return "bool";
        return "text";
    }
}
