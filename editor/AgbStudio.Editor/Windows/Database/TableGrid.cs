using System.Globalization;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Theme;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;

namespace AgbStudio.Editor.Windows.Database;

/// <summary>
/// The Database grid (T-P07): "#", the sprite column, the key and the other fields of the table as Figma "Table Cell"
/// columns, 9 rows visible (the rest scroll). Click selects a row, double-click edits a cell in place (text box, or the
/// value list for enum, bool, sprite, sound and reference fields). Cut / Copy / Paste / Duplicate / Delete work on the
/// selected row through the Edit menu; Select All selects every shown row and they then work on all of them.
/// </summary>
public sealed class TableGrid : Border, IEditTarget
{
    public const int VisibleRows = 9;
    public const double RowHeight = 32, HeaderHeight = 28;

    /// <summary>A column: the field it shows (null for "#"), header text and width (NaN = fills the rest).</summary>
    public sealed record Column(FieldDef? Field, string Header, double Width);

    readonly DatabasePage _page;
    readonly Grid _header = new() { Name = "GridHeader" };
    readonly StackPanel _rows = new() { Name = "GridRows" };
    readonly ScrollViewer _scroll;
    List<Column> _columns = [];
    List<int> _visible = [];
    TextBox? _editor;

    public TableGrid(DatabasePage page)
    {
        _page = page;
        Name = "TableGrid";
        Focusable = true;
        _scroll = new ScrollViewer { Content = _rows, MaxHeight = VisibleRows * RowHeight, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
        Child = new DockPanel { Children = { _header, _scroll } };
        DockPanel.SetDock(_header, Dock.Top);
    }

    public IReadOnlyList<Column> Columns => _columns;
    /// <summary>Table row indices shown, after the filter.</summary>
    public IReadOnlyList<int> VisibleRowIndices => _visible;

    /// <summary>Column widths by field type, as the Figma grid sizes them (# 40, ICON 56, ID 120, NAME 150, TYPE 110 ...).</summary>
    public static double WidthOf(FieldDef f, bool key) => key ? 120 : f.Type switch
    {
        "text" => 150, "enum" or "reference" => 110, "int" or "fixed" => 70, "bool" => 60, "sprite" => 56, "sound" => 90, _ => 120,
    };

    /// <summary>A text field named "description" is shown in the record panel's Description section, not in the grid.</summary>
    public static bool IsDescription(FieldDef f) => f.Type == "text" && f.Name.Equals("description", StringComparison.OrdinalIgnoreCase);

    public static List<Column> ColumnsOf(TableFile t)
    {
        var cols = new List<Column> { new(null, "#", 40) };
        foreach (var f in t.Fields.Where(f => f.Type == "sprite")) cols.Add(new(f, f.Name, WidthOf(f, false)));
        if (t.Fields.FirstOrDefault(f => f.Name == t.KeyField) is { } key) cols.Add(new(key, key.Name, WidthOf(key, true)));
        foreach (var f in t.Fields.Where(f => f.Name != t.KeyField && f.Type != "sprite" && !IsDescription(f))) cols.Add(new(f, f.Name, WidthOf(f, false)));
        // the last column takes the rest of the row
        if (cols.Count > 1) cols[^1] = cols[^1] with { Width = double.NaN };
        return cols;
    }

    /// <summary>The cell text the grid shows (localized text in the source language, bool as yes / no, dashes for empty).</summary>
    public static string Display(ProjectData? data, FieldDef f, string? raw)
    {
        raw ??= "";
        if (raw.Trim().Length == 0) return f.Type is "int" or "fixed" ? "–" : "—";
        return f.Type switch
        {
            "text" when f.Localized => data?.LocalizedText(raw) ?? raw,
            "bool" => IsTrue(raw) ? "yes" : "no",
            _ => raw,
        };
    }

    public static bool IsTrue(string? v) => v != null && (v.Equals("true", StringComparison.OrdinalIgnoreCase) || v.Equals("yes", StringComparison.OrdinalIgnoreCase) || v == "1");

    static ColumnDefinitions Defs(IEnumerable<Column> cols)
    {
        var d = new ColumnDefinitions();
        d.AddRange(cols.Select(c => double.IsNaN(c.Width) ? new ColumnDefinition(new GridLength(1, GridUnitType.Star)) { MinWidth = 100 } : new ColumnDefinition(c.Width, GridUnitType.Pixel)));
        return d;
    }

    /// <summary>Header text cut to the column (Figma: "stackable" is headed "STACK").</summary>
    static string FitHeader(string name, double width)
    {
        if (double.IsNaN(width)) return name;
        var room = width - 20;
        for (var n = name.Length; n > 1; n--)
        {
            var tb = UI.Text(name[..n].ToUpperInvariant(), "Overline/11", "text/secondary");
            tb.Measure(Size.Infinity);
            if (tb.DesiredSize.Width <= room + 0.5) return name[..n];
        }
        return name[..1];
    }

    public void Rebuild()
    {
        var t = _page.Current?.Table;
        _editor = null;
        _header.Children.Clear();
        _rows.Children.Clear();
        _visible = [];
        if (t is null) { _columns = []; return; }
        _columns = ColumnsOf(t);
        _header.ColumnDefinitions = Defs(_columns);
        for (var c = 0; c < _columns.Count; c++)
        {
            var col = _columns[c];
            _header.Children.Add(new TableCell { Kind = TableCellKind.Header, Text = col.Field is null ? col.Header : FitHeader(col.Header, col.Width) }.At(0, c));
        }
        var filter = _page.Filter.Trim();
        for (var r = 0; r < t.Rows.Count; r++)
        {
            if (filter.Length > 0 && !_columns.Any(c => c.Field != null && Display(_page.Data, c.Field, t.Rows[r].GetValueOrDefault(c.Field.Name)).Contains(filter, StringComparison.OrdinalIgnoreCase))
                && !(r + 1).ToString(CultureInfo.InvariantCulture).Equals(filter, StringComparison.Ordinal)) continue;
            _visible.Add(r);
            _rows.Children.Add(BuildRow(t, r));
        }
    }

    Control BuildRow(TableFile t, int r)
    {
        var row = t.Rows[r];
        var selected = r == _page.SelectedRow || _page.AllRowsSelected;
        var g = new Grid { ColumnDefinitions = Defs(_columns), Height = RowHeight, Background = Avalonia.Media.Brushes.Transparent, Name = "Row_" + (r + 1).ToString(CultureInfo.InvariantCulture), Tag = r };
        for (var c = 0; c < _columns.Count; c++)
        {
            var col = _columns[c];
            Control cell;
            if (col.Field is { Type: "sprite" } sf)
            {
                // Figma draws the item icons about 16 px, centred under "ICON": 8 px sprites at 2x
                var img = new PixelImage { Pixels = SpriteLibrary.Frame(_page.Data?.Project, row.GetValueOrDefault(sf.Name)), MaxScale = 2, Width = 16, Height = 16, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(16, 0, 0, 0) };
                var b = new Border { Child = img, BorderThickness = new Thickness(0, 0, 0, 1), Height = RowHeight }.Token(Border.BorderBrushProperty, "border/default");
                if (selected) b.Token(Border.BackgroundProperty, "accent/selected");
                cell = b;
            }
            else
            {
                var text = col.Field is null ? (r + 1).ToString(CultureInfo.InvariantCulture) : Display(_page.Data, col.Field, row.GetValueOrDefault(col.Field.Name));
                var kind = selected ? TableCellKind.Selected : col.Field?.Type is "int" or "fixed" ? TableCellKind.Mono : TableCellKind.Default;
                cell = new TableCell { Text = text, Kind = kind };
            }
            cell.At(0, c);
            var field = col.Field;
            var target = cell;
            cell.PointerPressed += (_, e) =>
            {
                if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
                Focus();
                if (e.ClickCount >= 2 && field != null) BeginEdit(r, field, target);
                else if (_page.SelectedRow != r || _page.AllRowsSelected) _page.SelectRow(r);
                e.Handled = true;
            };
            g.Children.Add(cell);
        }
        return g;
    }

    /// <summary>The row control of a table row (tests).</summary>
    public Grid? RowControl(int r) => _rows.Children.OfType<Grid>().FirstOrDefault(g => (int)g.Tag! == r);

    /// <summary>Starts editing a cell: enum / bool / sprite / sound / reference open their value list, the rest a text box.</summary>
    public void BeginEdit(int r, FieldDef f, Control? cell = null)
    {
        var t = _page.Current?.Table;
        if (t is null || r < 0 || r >= t.Rows.Count) return;
        if (_page.SelectedRow != r) _page.SelectRow(r);
        cell ??= RowControl(r)?.Children.FirstOrDefault(c => Grid.GetColumn(c) == _columns.FindIndex(x => x.Field == f));
        if (cell is null) return;
        var current = t.Rows[r].GetValueOrDefault(f.Name) ?? "";
        var options = _page.OptionsOf(f);
        if (options != null)
        {
            Dropdown.Show(cell, options, f.Type == "bool" ? (IsTrue(current) ? "yes" : "no") : current, v => _page.SetCell(r, f, null, v));
            return;
        }
        var shown = f.Type == "text" && f.Localized ? _page.Data?.Text(_page.Data.Source, current) ?? "" : current;
        var box = new TextBox { Text = shown, Name = "CellEditor", Height = RowHeight, Padding = new Thickness(9, 0) };
        if (f.Type is "int" or "fixed") Fig.SetStyle(box, "Mono/11");
        var host = (Grid)cell.Parent!;
        var col = Grid.GetColumn(cell);
        box.At(0, col);
        host.Children.Add(box);
        _editor = box;
        var done = false;
        void Finish(bool commit, bool keyboard)
        {
            if (done) return;
            done = true;
            _editor = null;
            // keep the keyboard in the grid after Enter / Esc (focus first, so leaving the box does not finish twice)
            if (keyboard) Focus();
            host.Children.Remove(box);
            if (commit && box.Text != shown) _page.SetCell(r, f, f.Type == "text" && f.Localized ? _page.Data?.Source : null, box.Text ?? "");
        }
        box.AddHandler(Avalonia.Input.InputElement.KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Enter) { Finish(true, true); e.Handled = true; }
            else if (e.Key == Key.Escape) { Finish(false, true); e.Handled = true; }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        Ui.EditorFocus(box, () => Finish(true, false));
    }

    public TextBox? Editor => _editor;

    public void ScrollTo(int r)
    {
        var i = _visible.IndexOf(r);
        if (i >= 0) _scroll.Offset = new Vector(0, Math.Max(0, i * RowHeight - (VisibleRows - 1) * RowHeight));
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || _editor != null || _visible.Count == 0) return;
        var i = _visible.IndexOf(_page.SelectedRow);
        if (e.Key == Key.Down && i + 1 < _visible.Count) { _page.SelectRow(_visible[i + 1]); e.Handled = true; }
        else if (e.Key == Key.Up && i > 0) { _page.SelectRow(_visible[i - 1]); e.Handled = true; }
    }

    // ---- Edit menu ----
    public bool CanCopy => _page.SelectedRows.Count > 0;
    public bool CanPaste => _page.Clipboard is { Count: > 0 };
    public void Cut() { Copy(); Delete(); }
    public void Copy() => _page.CopyRow();
    public void Paste() => _page.PasteRow();
    public void Duplicate() => _page.DuplicateRow();
    public void Delete() => _page.DeleteRow();
    public void SelectAll() => _page.SelectAllRows();
}
