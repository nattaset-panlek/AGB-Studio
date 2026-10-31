using System.Globalization;
using AgbStudio.Build.Data;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace AgbStudio.Editor.Windows.Database;

/// <summary>
/// Database (T-P07, 1440 x 900; #23 Database tab, Tools › Database, Create › Data Table): the tables of
/// <c>data/*.db</c> with row counts, the grid of the selected table with filter and Add row, the record panel, the
/// schema with Add Field, "Use it in a script" and the ROM size of the table. Every edit is undoable and marks the
/// table's document dirty; Save writes the .db file (and the string tables for localized text).
/// </summary>
[EditorWindow("T-P07")]
public sealed class DatabasePage : ToolPage
{
    readonly StackPanel _tables = new() { Spacing = 1, Margin = new Thickness(6, 4, 6, 3), Name = "TablesList" };
    readonly StackPanel _schema = new() { Spacing = 1, Margin = new Thickness(6, 5, 6, 4), Name = "SchemaList" };
    readonly PanelHeader _schemaHeader;
    readonly Chip _romChip;
    readonly StatusItem _statusTable, _statusRom, _statusSaved;
    readonly ContentControl _sample = new() { Name = "ScriptSample" };
    ProjectData? _data;

    public SearchField FilterBox { get; } = new() { Watermark = "Filter rows…", Width = 240, Name = "FilterRows", HorizontalAlignment = HorizontalAlignment.Left };
    public TableGrid Grid { get; }
    public RecordPanel Record { get; }
    public AgbButton ImportButton { get; }
    public AgbButton AddRowButton { get; }

    public ProjectData? Data => _data;
    public TableDoc? Current { get; private set; }
    public int SelectedRow { get; private set; } = -1;
    public string Filter => FilterBox.Text ?? "";
    public Dictionary<string, string>? Clipboard { get; private set; }

    public DatabasePage(WindowContext ctx) : this(ctx.Services) { }

    public DatabasePage(EditorServices s) : base(s, "DatabasePage")
    {
        SetToolbar(
            ToolGroup([
                Tool("plus", "Tool_AddRow", "Add row", AddRow),
                Tool("table", "Tool_Grid", "Table", active: true),
                Tool("filter", "Tool_Filter", "Filter rows", () => FilterBox.Focus()),
                Tool("upload", "Tool_Import", "Import CSV", () => _ = ImportCsv()),
            ]),
            _romChip = new Chip { Content = "Stored in ROM as const data", Glyph = "db", Name = "RomChip" });

        // left: tables and schema
        var tablesHeader = new PanelHeader { Title = "Tables", Glyph = "db", ActionGlyph = "plus", Name = "TablesHeader" };
        tablesHeader.ActionCommand = new Relay(() => _ = s.Windows.Open("T-D17", this, tablesHeader, "Database.TablesAdd"));
        _schemaHeader = new PanelHeader { Title = "Schema", Glyph = "sliders", ActionGlyph = "plus", Name = "SchemaHeader" };
        _schemaHeader.ActionCommand = new Relay(() => { if (Current != null) _ = s.Windows.Open("T-D18", this, _schemaHeader, "Database.SchemaAdd"); });
        LeftContent = new ScrollViewer { Content = UI.Column(0, tablesHeader, _tables, _schemaHeader, _schema) };

        // center: filter, grid, script sample
        ImportButton = new AgbButton { Content = "Import CSV", Glyph = "upload", Kind = ButtonKind.Secondary, Name = "ImportCsvButton" };
        ImportButton.Click += (_, _) => _ = ImportCsv();
        AddRowButton = new AgbButton { Content = "Add row", Glyph = "plus", Kind = ButtonKind.Primary, Name = "AddRowButton" };
        AddRowButton.Click += (_, _) => AddRow();
        var buttons = UI.Row(8, ImportButton, AddRowButton);
        buttons.HorizontalAlignment = HorizontalAlignment.Right;
        var filterRow = new Panel { Height = 28, Margin = new Thickness(16, 10, 16, 10), Children = { FilterBox, buttons } };
        FilterBox.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Grid!.Rebuild(); };
        Grid = new TableGrid(this) { Margin = new Thickness(16, 0, 0, 0) };
        var sample = UI.Frame(UI.Column(9, Ui.Overline("Use it in a script"), _sample), "bg/panel", pad: new Thickness(16, 13, 16, 14)).Named("ScriptSamplePanel");
        MainContent = UI.Column(0, filterRow, Grid, sample);

        Record = new RecordPanel(this);
        RightContent = Record;

        _statusTable = new StatusItem { Glyph = "table", Name = "Status_Table" };
        AddStatus(_statusTable);
        _statusRom = new StatusItem { Glyph = "chip", Name = "Status_Rom" };
        _statusSaved = new StatusItem { Glyph = "check", Content = "Saved", Name = "Status_Saved" };
        AddStatus(_statusRom, right: true);
        AddStatus(_statusSaved, right: true);

        s.Project.Changed += () => { Attach(); Refresh(); };
        Attach();
        Refresh();
    }

    void Attach()
    {
        var d = ProjectData.For(S);
        if (d == _data) return;
        if (_data != null) _data.Changed -= Refresh;
        _data = d;
        if (_data != null) _data.Changed += Refresh;
        Current = null;
        SelectedRow = -1;
        Document = null;
        if (_data?.Tables.FirstOrDefault() is { } first) Select(first, 0, refresh: false);
    }

    protected override void OnShown()
    {
        Attach();
        Refresh();
        base.OnShown();
    }

    protected override void DocumentStateChanged() => _statusSaved.IsVisible = Document is not { IsDirty: true };

    // ---- selection -------------------------------------------------------------------------------------

    /// <summary>Shows a table (its document becomes the active one) and selects a row.</summary>
    public void Select(TableDoc table, int row = 0, bool refresh = true)
    {
        if (Current != table)
        {
            var old = Document;
            Current = table;
            var t = table;
            Document = _data!.Document(table.FileName, "db", table.Path, () =>
            {
                t.Save();
                if (_data.StringsDirty) _data.SaveStrings();
                return Task.CompletedTask;
            });
            if (old != null && old != Document && !old.IsDirty) S.Documents.Close(old);
        }
        SelectedRow = table.Table.Rows.Count == 0 ? -1 : Math.Clamp(row, 0, table.Table.Rows.Count - 1);
        if (refresh) Refresh();
    }

    public void SelectTable(string name)
    {
        if (_data?.Table(name) is { } t) Select(t, 0);
    }

    public void SelectRow(int row)
    {
        if (Current is null) return;
        SelectedRow = Math.Clamp(row, -1, Current.Table.Rows.Count - 1);
        Refresh();
    }

    readonly Reentry _refresh = new();

    public void Refresh() => _refresh.Run(RefreshCore);

    void RefreshCore()
    {
        if (_data != null && Current != null && !_data.Tables.Contains(Current))
        {
            Current = null;
            if (_data.Tables.FirstOrDefault() is { } first) { Select(first, 0, refresh: false); }
            else Document = null;
        }
        if (Current != null && SelectedRow >= Current.Table.Rows.Count) SelectedRow = Current.Table.Rows.Count - 1;
        RefreshLists();
        Grid.Rebuild();
        Record.Refresh();
        var t = Current?.Table;
        _sample.Content = t is null || _data is null ? null : ScriptSample.View(ScriptSample.Lines(t, SelectedRow, _data.Project.Settings));
        _romChip.IsVisible = t?.KeepInRom ?? false;
        _statusTable.Content = t is null ? null : $"{t.Name} · {t.Rows.Count} rows · {t.Fields.Count} fields";
        _statusTable.IsVisible = t != null;
        _statusRom.Content = t is null ? null : Sizes.Rom(Sizes.TableBytes(t));
        _statusRom.IsVisible = t != null;
        DocumentStateChanged();
    }

    void RefreshLists()
    {
        _tables.Children.Clear();
        _schema.Children.Clear();
        if (_data is null) return;
        foreach (var t in _data.Tables)
        {
            var item = new TreeItem { Label = t.Name, Glyph = "table", Meta = t.Table.Rows.Count.ToString(CultureInfo.InvariantCulture), IsSelected = t == Current, Name = "Table_" + t.Name };
            var table = t;
            item.Activated += (_, _) => Select(table, 0);
            _tables.Children.Add(item);
        }
        _schemaHeader.Title = Current is null ? "Schema" : "Schema · " + Current.Name;
        if (Current is null) return;
        foreach (var f in Current.Table.Fields)
            _schema.Children.Add(new TreeItem { Label = f.Name, Glyph = "dots", Meta = f.Type + (f.Localized ? " · loc" : ""), Name = "Field_" + f.Name });
    }

    public IReadOnlyList<TreeItem> TableItems => _tables.Children.OfType<TreeItem>().ToList();
    public IReadOnlyList<TreeItem> SchemaItems => _schema.Children.OfType<TreeItem>().ToList();
    public string? StatusText => _statusTable.Content as string;
    public string? RomText => _statusRom.Content as string;
    public bool SavedVisible => _statusSaved.IsVisible;

    /// <summary>Values offered for a field (enum values, yes / no, sprites, sounds, rows of the referenced table); null for typed text.</summary>
    public IReadOnlyList<string>? OptionsOf(FieldDef f) => f.Type switch
    {
        "enum" when Current != null && f.Name == Current.Table.KeyField => null,
        "enum" => f.EnumValues,
        "bool" => ["yes", "no"],
        "sprite" => SpriteLibrary.Names(_data?.Project),
        "sound" => _data is null ? [] : _data.Project.SoundFiles().Concat(_data.Project.SfxFiles()).Select(GameProject.AssetName).Distinct().Order(StringComparer.Ordinal).ToList(),
        "reference" => _data?.Table(f.Reference) is { } rt ? rt.Table.Rows.Select(r => r.GetValueOrDefault(rt.Table.KeyField) ?? "").ToList() : [],
        _ => null,
    };

    // ---- edits -----------------------------------------------------------------------------------------

    static string StringKeyFor(TableFile t, string id, FieldDef f) =>
        $"{DatabaseCodegen.Singular(t.Name).ToLowerInvariant()}.{id.ToLowerInvariant()}.{f.Name}";

    /// <summary>Sets a cell (localized text: the text of the row's key in <paramref name="lang"/>). Invalid numbers and duplicate ids are refused.</summary>
    public bool SetCell(int row, FieldDef f, string? lang, string value)
    {
        if (_data is null || Current is null || row < 0 || row >= Current.Table.Rows.Count) return false;
        var doc = Document;
        var t = Current.Table;
        var r = t.Rows[row];
        var data = _data;
        if (f.Type == "text" && f.Localized && lang != null)
        {
            var oldKey = r.GetValueOrDefault(f.Name);
            var key = string.IsNullOrEmpty(oldKey) ? StringKeyFor(t, r.GetValueOrDefault(t.KeyField) ?? "row", f) : oldKey;
            var oldText = data.Text(lang, key);
            var hadInfo = data.Meta.Keys.ContainsKey(key);
            if (oldText == value) return false;
            data.Edit($"Edit {f.Name}", doc, () =>
            {
                r[f.Name] = key;
                data.SetText(lang, key, value);
                if (!hadInfo) data.EnsureInfo(key).Group = t.Name;
            }, () =>
            {
                if (oldKey is null) r.Remove(f.Name); else r[f.Name] = oldKey;
                data.SetText(lang, key, oldText);
                if (!hadInfo) data.Meta.Keys.Remove(key);
            });
            return true;
        }
        value = value.Trim();
        if (f.Type == "int" && value.Length > 0 && !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)) return Refuse($"{f.Name} must be a whole number.");
        if (f.Type == "fixed" && value.Length > 0)
        {
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return Refuse($"{f.Name} must be a number.");
            value = RecordPanel.Format(d);
        }
        if (f.Type == "bool") value = TableGrid.IsTrue(value) ? "yes" : "no";
        if (f.Name == t.KeyField)
        {
            if (value.Length == 0 || AgbStudio.Build.Pipeline.Cpp.Ident(value) != value) return Refuse($"“{value}” is not a valid id. An id uses only letters A-Z, digits and _, and does not start with a digit.");
            if (t.Rows.Where((x, i) => i != row).Any(x => x.GetValueOrDefault(t.KeyField) == value)) return Refuse($"{value} is already a row of {t.Name}.");
        }
        var old = r.GetValueOrDefault(f.Name);
        if ((old ?? "") == value) return false;
        data.Edit($"Edit {f.Name}", doc, () => r[f.Name] = value, () => { if (old is null) r.Remove(f.Name); else r[f.Name] = old; });
        return true;
    }

    bool Refuse(string message)
    {
        S.Toasts.Show(message, ToastStatus.Error);
        Refresh();
        return false;
    }

    /// <summary>A new row after the last one: a fresh id ("Item25") and the field defaults; it becomes selected.</summary>
    public void AddRow()
    {
        if (_data is null || Current is null) return;
        var t = Current.Table;
        var single = AgbStudio.Build.Pipeline.Cpp.Ident(DatabaseCodegen.Singular(t.Name));
        var n = t.Rows.Count + 1;
        while (t.Rows.Any(r => r.GetValueOrDefault(t.KeyField) == single + n)) n++;
        var row = new Dictionary<string, string> { [t.KeyField] = single + n };
        foreach (var f in t.Fields.Where(f => f.Name != t.KeyField && f.Default.Length > 0)) row[f.Name] = f.Default;
        InsertRow(row, t.Rows.Count, "Add row");
        Grid.ScrollTo(SelectedRow);
    }

    void InsertRow(Dictionary<string, string> row, int at, string name)
    {
        var t = Current!.Table;
        var before = SelectedRow;
        _data!.Edit(name, Document, () => { t.Rows.Insert(at, row); SelectedRow = at; }, () => { t.Rows.Remove(row); SelectedRow = Math.Min(before, t.Rows.Count - 1); });
    }

    public void DeleteRow()
    {
        if (_data is null || Current is null || SelectedRow < 0) return;
        var t = Current.Table;
        var at = SelectedRow;
        var row = t.Rows[at];
        _data.Edit("Delete row", Document, () => { t.Rows.RemoveAt(at); SelectedRow = Math.Min(at, t.Rows.Count - 1); }, () => { t.Rows.Insert(at, row); SelectedRow = at; });
    }

    public void DuplicateRow()
    {
        if (Current is null || SelectedRow < 0) return;
        PasteCopy(new Dictionary<string, string>(Current.Table.Rows[SelectedRow]), "Duplicate row");
    }

    public void CopyRow()
    {
        if (Current is null || SelectedRow < 0) return;
        Clipboard = new Dictionary<string, string>(Current.Table.Rows[SelectedRow]);
    }

    public void PasteRow()
    {
        if (Clipboard is null || Current is null) return;
        PasteCopy(new Dictionary<string, string>(Clipboard.Where(kv => Current.Table.Fields.Any(f => f.Name == kv.Key)).ToDictionary()), "Paste row");
    }

    void PasteCopy(Dictionary<string, string> row, string name)
    {
        var t = Current!.Table;
        var id = row.GetValueOrDefault(t.KeyField) ?? "Row";
        var copy = id + "Copy";
        for (var i = 2; t.Rows.Any(r => r.GetValueOrDefault(t.KeyField) == copy); i++) copy = id + "Copy" + i;
        row[t.KeyField] = copy;
        InsertRow(row, SelectedRow < 0 ? t.Rows.Count : SelectedRow + 1, name);
    }

    /// <summary>Add Field (T-D18): appends the field to the schema.</summary>
    public void AddField(FieldDef f)
    {
        if (_data is null || Current is null) return;
        var t = Current.Table;
        _data.Edit("Add field", Document, () => t.Fields.Add(f), () => t.Fields.Remove(f));
    }

    /// <summary>New Table (T-D17): writes data/&lt;Name&gt;.db and shows it. Undo removes the file again.</summary>
    public TableDoc? CreateTable(TableFile table, IReadOnlyDictionary<(string Lang, string Key), string>? texts = null)
    {
        if (_data is null) return null;
        var data = _data;
        var doc = new TableDoc(Path.Combine(data.Project.DataDir, GameProject.FileName(table.Name) + ".db"), table);
        var previous = Current;
        data.Edit("New table", null, () =>
        {
            doc.Save();
            data.Tables.Add(doc);
            foreach (var ((lang, key), text) in texts ?? new Dictionary<(string, string), string>()) data.SetText(lang, key, text);
            if (texts is { Count: > 0 }) data.SaveStrings();
            Current = null;
            Select(doc, 0, refresh: false);
        }, () =>
        {
            data.Tables.Remove(doc);
            if (File.Exists(doc.Path)) File.Delete(doc.Path);
            foreach (var (lang, key) in (texts ?? new Dictionary<(string, string), string>()).Keys) data.SetText(lang, key, null);
            if (texts is { Count: > 0 }) data.SaveStrings();
            if (Document != null && Document.Path == doc.Path) S.Documents.Close(Document);
            Current = null;
            if (previous != null && data.Tables.Contains(previous)) Select(previous, 0, refresh: false);
        });
        return doc;
    }

    /// <summary>Import CSV (T-D19): updates rows with the same id and appends the others; localized texts go to the string tables.</summary>
    public void ImportRows(IReadOnlyList<Dictionary<string, string>> rows, IReadOnlyDictionary<(int Row, string Field), Dictionary<string, string>> texts)
    {
        if (_data is null || Current is null) return;
        var data = _data;
        var t = Current.Table;
        var before = t.Rows.Select(r => new Dictionary<string, string>(r)).ToList();
        var oldTexts = new Dictionary<(string, string), string?>();
        var newRows = before.Select(r => new Dictionary<string, string>(r)).ToList();
        var newTexts = new Dictionary<(string Lang, string Key), string>();
        for (var i = 0; i < rows.Count; i++)
        {
            var src = rows[i];
            var id = src.GetValueOrDefault(t.KeyField) ?? "";
            if (id.Length == 0) continue;
            var target = newRows.FirstOrDefault(r => r.GetValueOrDefault(t.KeyField) == id);
            if (target is null) newRows.Add(target = new Dictionary<string, string> { [t.KeyField] = id });
            foreach (var (k, v) in src)
            {
                if (k == t.KeyField) continue;
                var f = t.Fields.FirstOrDefault(x => x.Name == k);
                target[k] = f?.Type == "fixed" && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? RecordPanel.Format(d) : v;
            }
            foreach (var f in t.Fields.Where(f => f.Type == "text" && f.Localized))
            {
                if (!texts.TryGetValue((i, f.Name), out var perLang)) continue;
                var key = target.GetValueOrDefault(f.Name) is { Length: > 0 } k2 ? k2 : StringKeyFor(t, id, f);
                target[f.Name] = key;
                foreach (var (lang, text) in perLang)
                {
                    oldTexts.TryAdd((lang, key), data.Text(lang, key));
                    newTexts[(lang, key)] = text;
                }
            }
        }
        data.Edit("Import CSV", Document, () =>
        {
            t.Rows.Clear();
            t.Rows.AddRange(newRows.Select(r => new Dictionary<string, string>(r)));
            foreach (var ((lang, key), text) in newTexts)
            {
                data.SetText(lang, key, text);
                if (!data.Meta.Keys.ContainsKey(key)) data.EnsureInfo(key).Group = t.Name;
            }
            SelectedRow = Math.Clamp(SelectedRow, 0, t.Rows.Count - 1);
        }, () =>
        {
            t.Rows.Clear();
            t.Rows.AddRange(before.Select(r => new Dictionary<string, string>(r)));
            foreach (var ((lang, key), text) in oldTexts) data.SetText(lang, key, text);
            SelectedRow = Math.Clamp(SelectedRow, -1, t.Rows.Count - 1);
        });
    }

    /// <summary>Database › Import CSV: picks a .csv file, then the mapping dialog (T-D19).</summary>
    public async Task ImportCsv()
    {
        if (Current is null) return;
        var path = await FileChooser.For(S).Open("Import CSV", "csv");
        if (path is null) return;
        await S.Windows.Open("T-D19", new ImportCsvRequest(this, path), ImportButton, "Database.ImportCsv");
    }

    /// <summary>Opens what a "Used by" entry points at.</summary>
    public void OpenReference(UsedBy.Reference r)
    {
        switch (r.Kind)
        {
            case UsedBy.Kind.Table:
                if (_data?.Tables.FirstOrDefault(x => x.Path == r.Path) is { } t)
                    Select(t, Math.Max(0, t.Table.Rows.FindIndex(x => x.GetValueOrDefault(t.Table.KeyField) == r.Detail)));
                break;
            case UsedBy.Kind.Script:
                _ = Scripts.ScriptEditorPage.OpenScriptFile(S, r.Path, source: "Database.UsedBy");
                break;
            case UsedBy.Kind.Scene:
                _ = S.Windows.Open("T-P01", source: "Database.UsedBy");
                break;
            case UsedBy.Kind.Dialogue:
                _ = S.Windows.Open("T-P16", r.Detail, source: "Database.UsedBy");
                break;
        }
    }

    /// <summary>Create › Data Table: the Database with the New Table dialog.</summary>
    [CommandHandler("assets.create.dataTable")]
    public static void CreateDataTable(EditorServices s)
    {
        _ = s.Windows.Open("T-P07", source: "assets.create.dataTable");
        if (s.Windows.Host?.CurrentPage is DatabasePage page) _ = s.Windows.Open("T-D17", page, source: "assets.create.dataTable");
    }
}

/// <summary>What Database › Import CSV passes to the Import CSV dialog.</summary>
public sealed record ImportCsvRequest(DatabasePage Page, string Path);
