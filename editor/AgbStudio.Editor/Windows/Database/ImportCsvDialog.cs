using System.Globalization;
using System.Text;
using AgbStudio.Build.Data;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Graphics;
using AgbStudio.Editor.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace AgbStudio.Editor.Windows.Database;

/// <summary>
/// Import CSV (T-D19, 780 x 478; Database › Import CSV): the file with its row count and encoding, the column mapping
/// (CSV column, sample, field — localized text per language, "— skip —" — and the type check) and the per-row
/// warnings of <see cref="CsvImporter"/>; "Import N rows" merges the rows into the table (undoable).
/// </summary>
[EditorWindow("T-D19")]
public sealed class ImportCsvDialog : FigmaDialog
{
    public const string Skip = "— skip —";

    readonly WindowContext _ctx;
    readonly DatabasePage? _page;
    readonly TextBlock _file = UI.Text("", "Body/12 Medium", "text/primary").Named("CsvFile");
    readonly Grid _table = new() { ColumnDefinitions = ColumnDefinitions.Parse("180,201,199,*"), Name = "MapTable" };
    readonly StackPanel _warnings = new() { Spacing = 4, Name = "Warnings" };
    readonly Border _warningBox;
    readonly DialogFooter _footer;
    string _text = "";
    List<string[]> _rows = [];

    public string? Path { get; private set; }
    public string Encoding { get; private set; } = "UTF-8";
    /// <summary>Column name and the chosen field option ("price · int", "name · TH", "— skip —").</summary>
    public List<(string Column, string Option)> Mapping { get; } = [];
    public IReadOnlyList<string> WarningTexts { get; private set; } = [];
    public int DataRows => Math.Max(0, _rows.Count - 1);

    public ImportCsvDialog(WindowContext ctx)
    {
        _ctx = ctx;
        Name = "ImportCsvDialog";
        var req = ctx.Args as ImportCsvRequest;
        _page = req?.Page ?? ctx.Services.Windows.Host?.CurrentPage as DatabasePage;

        var fileIcon = new Icon { Glyph = "file", Size = 14, VerticalAlignment = VerticalAlignment.Center }.Token(Icon.ForegroundProperty, "text/secondary");
        var another = new Button { Theme = (Avalonia.Styling.ControlTheme?)Application.Current?.FindResource("BareButton"), Content = UI.Text("Choose another…", "Body/12", "text/secondary"), Name = "ChooseAnother", VerticalAlignment = VerticalAlignment.Center };
        another.Click += async (_, _) =>
        {
            if (await FileChooser.For(ctx.Services).Open("Import CSV", "csv") is { } p) Load(p);
        };
        var fileRow = new DockPanel { Height = 20 };
        // Figma: "Choose another…" ends at x 747, 13 px inside the content edge
        another.Margin = new Thickness(0, 0, 9, 0);
        DockPanel.SetDock(another, Dock.Right);
        fileRow.Children.AddRange([another, UI.Row(8, fileIcon, _file)]);

        var warnIcon = new Icon { Glyph = "warn", Size = 14, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 1, 0, 0) }.Token(Icon.ForegroundProperty, "status/warning");
        _warningBox = UI.Frame(UI.Row(7, warnIcon, _warnings), "bg/base", pad: new Thickness(11, 9), radius: 6).Named("WarningBox");

        var content = UI.Column(0,
            fileRow.Margin(0, 2, 0, 0),
            Ui.Overline("Map columns").Margin(0, 16, 0, 0),
            _table.Margin(0, 14, 0, 0),
            _warningBox.Margin(0, 14, 0, 0));
        content.Margin = new Thickness(20, 21, 20, 0);
        _footer = new DialogFooter { ConfirmText = "Import" };
        _footer.Cancel += (_, _) => Close(null);
        _footer.Confirm += (_, _) => { if (Import()) Close(DataRows); };
        Build("Import CSV", "upload", content, _footer, 780);
        if (req?.Path is { } path) Load(path);
    }

    TableFile? Table => _page?.Current?.Table;
    ProjectData? Data => _page?.Data;

    /// <summary>Reads a CSV file (UTF-8, else the system ANSI code page) and maps its columns automatically.</summary>
    public void Load(string path)
    {
        Path = path;
        var bytes = File.ReadAllBytes(path);
        try
        {
            _text = new UTF8Encoding(false, true).GetString(bytes).TrimStart('﻿');
            Encoding = "UTF-8";
        }
        catch (DecoderFallbackException)
        {
            _text = System.Text.Encoding.Latin1.GetString(bytes);
            Encoding = "Latin-1";
        }
        _rows = CsvImporter.Parse(_text);
        Mapping.Clear();
        if (_rows.Count > 0 && Table is { } t)
        {
            var auto = CsvImporter.AutoMap(_rows[0], t);
            var langs = Data?.Languages.Select(l => l.Code).ToHashSet() ?? [];
            foreach (var m in auto)
            {
                var f = m.Field is null ? null : t.Fields.First(x => x.Name == m.Field);
                string option;
                if (f is null) option = Skip;
                else if (f.Type == "text" && f.Localized) option = OptionOf(f, SplitLanguage(m.Column, langs).Lang ?? Data?.Source);
                else option = OptionOf(f, null);
                Mapping.Add((m.Column, option));
            }
        }
        Rebuild();
    }

    /// <summary>"Name_TH" -> ("Name", "th") when "th" is a project language; ("Cost", null) otherwise.</summary>
    public static (string Base, string? Lang) SplitLanguage(string column, ICollection<string> langs)
    {
        foreach (var sep in new[] { '_', ' ', '-', '.' })
        {
            var i = column.LastIndexOf(sep);
            if (i > 0 && langs.Contains(column[(i + 1)..].ToLowerInvariant())) return (column[..i], column[(i + 1)..].ToLowerInvariant());
        }
        return (column, null);
    }

    /// <summary>Field option text: "id · enum", "price · int", "name · EN" (localized text per language).</summary>
    public static string OptionOf(FieldDef f, string? lang) => f.Type == "text" && f.Localized && lang != null ? $"{f.Name} · {lang.ToUpperInvariant()}" : $"{f.Name} · {f.Type}";

    public IReadOnlyList<string> Options()
    {
        var list = new List<string>();
        if (Table is not { } t) return list;
        var langs = Data?.Languages.Select(l => l.Code).ToList() ?? [];
        foreach (var f in t.Fields)
            if (f.Type == "text" && f.Localized) list.AddRange(langs.Select(l => OptionOf(f, l)));
            else list.Add(OptionOf(f, null));
        list.Add(Skip);
        return list;
    }

    (FieldDef? Field, string? Lang) Resolve(string option)
    {
        if (option == Skip || Table is not { } t) return (null, null);
        var parts = option.Split(" · ");
        var f = t.Fields.FirstOrDefault(x => x.Name == parts[0]);
        return f is null ? (null, null) : f.Type == "text" && f.Localized ? (f, parts.Length > 1 ? parts[1].ToLowerInvariant() : Data?.Source) : (f, null);
    }

    public void Map(string column, string option)
    {
        var i = Mapping.FindIndex(m => m.Column == column);
        if (i < 0) return;
        Mapping[i] = (column, option);
        Rebuild();
    }

    /// <summary>The mapping for <see cref="CsvImporter"/>: typed fields only (localized text is imported per language here).</summary>
    List<CsvImporter.Mapping> ImporterMapping() =>
        Mapping.Select(m => { var (f, lang) = Resolve(m.Option); return new CsvImporter.Mapping(m.Column, f is null || lang != null ? null : f.Name); }).ToList();

    static bool Valid(FieldDef f, string v) => f.Type switch
    {
        "int" => v.Length == 0 || int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
        "fixed" => v.Length == 0 || double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out _),
        "bool" => v.Length == 0 || v.ToLowerInvariant() is "true" or "false" or "yes" or "no" or "1" or "0",
        "enum" => v.Length == 0 || f.EnumValues.Contains(v),
        _ => true,
    };

    /// <summary>Rows (1-based data rows) whose value in a column does not fit the field's type.</summary>
    public IReadOnlyList<int> ProblemRows(string column)
    {
        var c = _rows.Count > 0 ? Array.IndexOf(_rows[0], column) : -1;
        var (f, lang) = Resolve(Mapping.FirstOrDefault(m => m.Column == column).Option ?? Skip);
        if (c < 0 || f is null || lang != null || f.Name == Table?.KeyField) return [];
        return Enumerable.Range(1, _rows.Count - 1).Where(r => c < _rows[r].Length && !Valid(f, _rows[r][c].Trim())).ToList();
    }

    void Rebuild()
    {
        _table.Children.Clear();
        _table.RowDefinitions.Clear();
        _table.RowDefinitions.Add(new RowDefinition(28, GridUnitType.Pixel));
        string[] heads = ["CSV column", "Sample", "Field", "Check"];
        for (var c = 0; c < 4; c++) _table.Children.Add(new TableCell { Kind = TableCellKind.Header, Text = heads[c] }.At(0, c));
        var sample = _rows.Count > 1 ? _rows[1] : [];
        for (var i = 0; i < Mapping.Count; i++)
        {
            var (column, option) = Mapping[i];
            _table.RowDefinitions.Add(new RowDefinition(30, GridUnitType.Pixel));
            var row = i + 1;
            _table.Children.Add(new TableCell { Kind = TableCellKind.Mono, Text = column, Height = 30 }.At(row, 0));
            var ci = _rows.Count > 0 ? Array.IndexOf(_rows[0], column) : -1;
            _table.Children.Add(new TableCell { Text = ci >= 0 && ci < sample.Length ? sample[ci] : "", Height = 30 }.At(row, 1));
            _table.Children.Add(Cell(FieldBox(column, option)).At(row, 2));
            var problems = ProblemRows(column).Count;
            var chip = option == Skip ? new Chip { Content = "skipped", Style = ChipStyle.Default }
                : problems > 0 ? new Chip { Content = problems == 1 ? "1 warning" : $"{problems} warnings", Style = ChipStyle.Warning }
                : new Chip { Content = "ok", Style = ChipStyle.Success };
            chip.Name = "Check_" + column;
            chip.IsHitTestVisible = false;
            _table.Children.Add(Cell(chip).At(row, 3));
        }
        var warnings = Table is { } t && _text.Length > 0 ? CsvImporter.Import(_text, t, ImporterMapping()).Warnings.Where(w => w != "The CSV file is empty.").ToList() : [];
        WarningTexts = warnings;
        _warnings.Children.Clear();
        foreach (var w in warnings)
        {
            // Figma T-D19: 11 px text on the 16 px line of the warning box
            var tb = UI.Text(w, "Label/11", "text/secondary");
            tb.LineHeight = 16;
            tb.TextWrapping = TextWrapping.Wrap;
            tb.MaxWidth = 660;
            _warnings.Children.Add(tb);
        }
        _warningBox.IsVisible = warnings.Count > 0;
        var warnRows = Mapping.SelectMany(m => ProblemRows(m.Column)).Distinct().Count();
        _footer.Hint = warnRows == 0 ? null : warnRows == 1 ? "1 row has warnings and will use the default value." : $"{warnRows} rows have warnings and will use the default value.";
        _footer.ConfirmText = DataRows == 1 ? "Import 1 row" : $"Import {DataRows} rows";
        _file.Text = Path is null ? "" : $"{System.IO.Path.GetFileName(Path)} · {DataRows} rows · {Encoding}";
    }

    static Border Cell(Control c)
    {
        c.HorizontalAlignment = HorizontalAlignment.Left;
        c.VerticalAlignment = VerticalAlignment.Center;
        return new Border { Height = 30, Padding = new Thickness(10, 0), BorderThickness = new Thickness(0, 0, 0, 1), Child = c }.Token(Border.BorderBrushProperty, "border/default");
    }

    /// <summary>The field dropdown of a row: the Figma "Field · Dropdown" box hugging its text.</summary>
    Button FieldBox(string column, string option)
    {
        var skip = option == Skip;
        var text = UI.Text(option, "Mono/11", skip ? "text/muted" : "text/primary");
        var chevron = new Icon { Glyph = "down", Size = 12, VerticalAlignment = VerticalAlignment.Center }.Token(Icon.ForegroundProperty, "text/muted");
        var box = UI.Frame(UI.Row(6, text, chevron), "bg/base", "border/default", new Thickness(1), new Thickness(8, 0), 5);
        box.Height = 24;
        var b = new Button { Theme = (Avalonia.Styling.ControlTheme?)Application.Current?.FindResource("BareButton"), Content = box, Name = "Map_" + column };
        b.Click += (_, _) => Dropdown.Show(b, Options(), option, v => Map(column, v));
        return b;
    }

    /// <summary>Imports the rows into the table (undoable); false when there is nothing to import.</summary>
    public bool Import()
    {
        if (_page is null || Table is not { } t || _rows.Count < 2) return false;
        var result = CsvImporter.Import(_text, t, ImporterMapping());
        var texts = new Dictionary<(int, string), Dictionary<string, string>>();
        for (var r = 1; r < _rows.Count; r++)
            foreach (var (column, option) in Mapping)
            {
                var (f, lang) = Resolve(option);
                var c = Array.IndexOf(_rows[0], column);
                if (f is null || lang is null || c < 0 || c >= _rows[r].Length || _rows[r][c].Trim().Length == 0) continue;
                if (!texts.TryGetValue((r - 1, f.Name), out var per)) texts[(r - 1, f.Name)] = per = [];
                per[lang] = _rows[r][c].Trim();
            }
        _page.ImportRows(result.Rows, texts);
        return true;
    }
}
