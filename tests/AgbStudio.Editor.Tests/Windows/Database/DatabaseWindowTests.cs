using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Windows.Database;
using AgbStudio.Scripting;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.Database;

/// <summary>11.H Database: P11.39 (tables, grid, filter, Add row, record, schema, "Use it in a script", "Used by") and P11.40 (New Table, Add Field, Import CSV).</summary>
public class DatabaseWindowTests : DataE2E
{
    DatabasePage Page => Assert.IsType<DatabasePage>(Win.CurrentPage);
    TableFile Items => Page.Data!.Table("Items")!.Table;

    async Task<DatabasePage> OpenDatabase()
    {
        await Open("T-P07");
        return Page;
    }

    [AvaloniaFact]
    public async Task P11_39_OpensFromDatabaseTabToolsMenuAndCreateDataTable()
    {
        var ws = Assert.IsType<SceneWorkspace>(Win.CurrentPage);
        Click(ws.PanelTabs[3]);
        Assert.IsType<DatabasePage>(Win.CurrentPage);
        Assert.Contains(("T-P07", (string?)"DatabaseTab"), S.Windows.Requests);
        Assert.Equal("Items.db", Win.TitleBar.Document);
        // the "Scene" tab goes back to the Scene Editor
        Click(Page.DocTabs[0]);
        Assert.IsType<SceneWorkspace>(Win.CurrentPage);
        Assert.Equal("Scene", S.Documents.Active!.Name);
        S.Commands.Execute("tools.database");
        Capture.Pump();
        Assert.IsType<DatabasePage>(Win.CurrentPage);
        S.Commands.Execute("assets.create.dataTable");
        Capture.Pump();
        Assert.IsType<NewTableDialog>(Win.Overlays.Open.Single());
    }

    [AvaloniaFact]
    public async Task P11_39_TablesWithCountsSchemaStatusAndRomSize()
    {
        var page = await OpenDatabase();
        Assert.Equal(["Items", "Enemies", "Shops", "Quests", "Levels", "Dialogue"], page.TableItems.Select(t => t.Label));
        Assert.Equal(["24", "9", "3", "7", "5", "112"], page.TableItems.Select(t => t.Meta));
        Assert.True(page.TableItems[0].IsSelected);
        Assert.Equal(["id", "name", "icon", "type", "price", "power", "stackable", "effect", "description"], page.SchemaItems.Select(t => t.Label));
        Assert.Equal(["enum", "text · loc", "sprite", "enum", "int", "fixed", "bool", "script", "text · loc"], page.SchemaItems.Select(t => t.Meta));
        Assert.Equal("Items · 24 rows · 9 fields", page.StatusText);
        // name 2 + icon 4 + type 1 + price 4 + power 4 + stackable 1 + effect 4 + description 2 = 22 B per row
        Assert.Equal("ROM 528 B", page.RomText);
        Assert.True(Find<Chip>("RomChip").IsVisible);
        Assert.Equal(["#", "icon", "id", "name", "type", "price", "power", "stackable", "effect"], page.Grid.Columns.Select(c => c.Field?.Name ?? "#"));
        Assert.Equal(24, page.Grid.VisibleRowIndices.Count);
        Assert.Equal(TableGrid.VisibleRows * TableGrid.RowHeight, page.Grid.GetVisualDescendants().OfType<ScrollViewer>().First().Bounds.Height);
        var header = page.Grid.GetVisualDescendants().OfType<TableCell>().Where(c => c.Kind == TableCellKind.Header).Select(c => c.Text).ToList();
        Assert.Contains("stack", header);   // "stackable" cut to its 60 px column, as Figma heads it
        Assert.Equal("Gold Coin", TableGrid.Display(page.Data, Items.Fields[1], Items.Rows[0]["name"]));

        Click(Find<TreeItem>("Table_Enemies"));
        Assert.Equal("Enemies · 9 rows · 4 fields", page.StatusText);
        Assert.Equal("Schema · Enemies", Find<PanelHeader>("SchemaHeader").Title);
        Assert.Equal("Enemies.db", Win.TitleBar.Document);
        Assert.Equal("ROM 108 B", page.RomText);
    }

    [AvaloniaFact]
    public async Task P11_39_FilterAndAddRowWithUndo()
    {
        var page = await OpenDatabase();
        page.FilterBox.Focus();
        Type("Consum");
        Assert.Equal([1, 2, 5], page.Grid.VisibleRowIndices);
        page.FilterBox.Text = "";
        Capture.Pump();
        Click(page.AddRowButton);
        Assert.Equal(25, Items.Rows.Count);
        Assert.Equal("Item25", Items.Rows[24]["id"]);
        Assert.Equal("0", Items.Rows[24]["price"]);   // the field default
        Assert.Equal(24, page.SelectedRow);
        Assert.True(S.Documents.Active!.IsDirty);
        Assert.False(page.SavedVisible);
        S.Commands.Execute("edit.undo");
        Capture.Pump();
        Assert.Equal(24, Items.Rows.Count);
        // the toolbar + adds a row too
        Click(Find<IconButton>("Tool_AddRow"));
        Assert.Equal(25, Items.Rows.Count);
    }

    [AvaloniaFact]
    public async Task P11_39_EditCellsInPlaceAndSaveWritesTheDbFile()
    {
        var page = await OpenDatabase();
        var price = Items.Fields.First(f => f.Name == "price");
        var row = page.Grid.RowControl(2)!;
        var cell = row.Children.First(c => Grid.GetColumn(c) == page.Grid.Columns.ToList().FindIndex(x => x.Field == price));
        Click(cell, clicks: 2);
        var editor = Assert.IsType<TextBox>(page.Grid.Editor);
        editor.SelectAll();
        Type("35");
        Key(PhysicalKey.Enter);
        Assert.Equal("35", Items.Rows[2]["price"]);
        // not a number: refused with an error toast
        Assert.False(page.SetCell(2, price, null, "lots"));
        Assert.Contains(S.Toasts.Visible, t => t.Status == ToastStatus.Error);
        Assert.Equal("35", Items.Rows[2]["price"]);
        // duplicate ids are refused
        Assert.False(page.SetCell(2, Items.Fields[0], null, "Coin"));
        Save();
        Assert.False(S.Documents.Active!.IsDirty);
        Assert.True(page.SavedVisible);
        var saved = GameProject.Load<TableFile>(Path.Combine(Root, "data", "Items.db"));
        Assert.Equal("35", saved.Rows[2]["price"]);
        // enum cells offer their values
        var type = Items.Fields.First(f => f.Name == "type");
        page.Grid.BeginEdit(2, type);
        Assert.True(Dropdown.Choose("Treasure"));
        Assert.Equal("Treasure", Items.Rows[2]["type"]);
    }

    [AvaloniaFact]
    public async Task P11_39_RecordPanelShowsTheRowAndEditsIt()
    {
        var page = await OpenDatabase();
        page.SelectRow(2);
        Capture.Pump();
        var r = page.Record;
        Assert.Equal(["id", "name · EN", "name · TH", "type", "price", "power", "stackable"], r.Fields.Keys);
        Assert.Equal("Potion", Find<TextBlock>("RecordTitle").Text);
        Assert.Equal("Items · row 3 · ItemId.Potion", Find<TextBlock>("RecordSubtitle").Text);
        Assert.NotNull(Find<PixelImage>("RecordIcon").Pixels);
        Assert.Equal("ยาฟื้นพลัง", r.Fields["name · TH"].Value);
        Assert.Equal(FieldKind.Dropdown, r.Fields["type"].Kind);
        Assert.Equal("Consumable", r.Fields["type"].Value);
        Assert.Equal(FieldKind.Slider, r.Fields["power"].Kind);
        Assert.Equal("2.00", r.Fields["power"].Value);
        Assert.Equal(25, r.Fields["power"].SliderValue);   // 0 .. 8.00, the largest power of the table
        Assert.True(r.Fields["stackable"].IsOn);
        Assert.Equal("Restores 2 hearts. Tastes like strawberries.", r.Description!.Text);
        Assert.Equal(["Shop_01 · slot2", "Chest · Cave_01", "Slime.cs · drop table"], r.UsedByRows.Select(u => u.Label));

        // Thai typed into the record goes to the string table
        var th = r.Fields["name · TH"].GetVisualDescendants().OfType<TextBox>().First(b => b.Name == "PART_Text");
        th.Focus();
        th.SelectAll();
        Type("ยาวิเศษ");
        page.Grid.Focus();
        Capture.Pump();
        Assert.Equal("ยาวิเศษ", page.Data!.Text("th", "item.potion.name"));
        // switch and slider
        r.Fields["stackable"].IsOn = false;
        Capture.Pump();
        Assert.Equal("no", Items.Rows[2]["stackable"]);
        // drag the power knob to the middle of the track
        var slider = r.Fields["power"].GetVisualDescendants().OfType<AgbSlider>().Single();
        var mid = slider.TranslatePoint(new Avalonia.Point(slider.Bounds.Width / 2, 6), Win)!.Value;
        Win.MouseMove(mid);
        Win.MouseDown(mid, MouseButton.Left);
        Win.MouseUp(mid, MouseButton.Left);
        Capture.Pump();
        Assert.Equal("4.00", Items.Rows[2]["power"]);
        S.Commands.Execute("edit.undo");
        S.Commands.Execute("edit.undo");
        S.Commands.Execute("edit.undo");
        Capture.Pump();
        Assert.Equal("ยาฟื้นพลัง", page.Data.Text("th", "item.potion.name"));
        Assert.Equal("yes", Items.Rows[2]["stackable"]);
        Assert.Equal("2.00", Items.Rows[2]["power"]);
        // a "Used by" entry opens what it names
        Click(r.UsedByRows.First());
        Assert.Equal("Shops", page.Current!.Name);
        Assert.Equal(0, page.SelectedRow);
    }

    [AvaloniaFact]
    public async Task P11_39_RecordEditCommitsToItsRowWhenAnotherRowIsClicked_EditMenuOnRows()
    {
        var page = await OpenDatabase();
        page.SelectRow(2);
        Capture.Pump();
        var price = page.Record.Fields["price"].GetVisualDescendants().OfType<TextBox>().First(b => b.Name == "PART_Text");
        price.Focus();
        price.SelectAll();
        Type("99");
        Click(page.Grid.RowControl(4)!.Children[0]);
        Assert.Equal(4, page.SelectedRow);
        Assert.Equal("99", Items.Rows[2]["price"]);
        Assert.Equal("120", Items.Rows[4]["price"]);
        // Edit menu on the selected row (focus in the grid)
        page.Grid.Focus();
        S.Commands.Execute("edit.duplicate");
        Capture.Pump();
        Assert.Equal(25, Items.Rows.Count);
        Assert.Equal("FeatherCopy", Items.Rows[5]["id"]);
        Assert.Equal(5, page.SelectedRow);
        page.Grid.Focus();
        S.Commands.Execute("edit.delete");
        Capture.Pump();
        Assert.Equal(24, Items.Rows.Count);
        Assert.DoesNotContain(Items.Rows, r => r["id"] == "FeatherCopy");
        // arrow keys move the selection
        page.Grid.Focus();
        Key(PhysicalKey.ArrowDown);
        Assert.Equal(6, page.SelectedRow);
    }

    /// <summary>
    /// P10.11 on table rows with the keyboard in the grid: Copy / Paste puts a copy after the row, Cut / Paste moves the
    /// row (it keeps its id); Select All selects every row the grid shows (after the filter) and Copy / Paste, Duplicate,
    /// Delete and Cut / Paste then act on all of them in one undo step; a click selects one row again.
    /// </summary>
    [AvaloniaFact]
    public async Task P10_11_EditMenuOnRows_CutCopyPasteAndSelectAll()
    {
        var page = await OpenDatabase();
        List<string> Ids() => Items.Rows.Select(r => r["id"]).ToList();
        var original = Ids();
        Assert.Equal(24, original.Count);
        void Ctrl(PhysicalKey k) { page.Grid.Focus(); Key(k, RawInputModifiers.Control); }
        void Undo() { S.Undo.Undo(); Capture.Pump(); }
        bool Highlighted(int r) => page.Grid.RowControl(r)!.Children.OfType<TableCell>().All(c => c.Kind == TableCellKind.Selected);

        // Copy / Paste on one row
        Click(page.Grid.RowControl(2)!.Children[0]);
        Ctrl(PhysicalKey.C);
        Ctrl(PhysicalKey.V);
        Assert.Equal(25, Items.Rows.Count);
        Assert.Equal(original[2] + "Copy", Items.Rows[3]["id"]);
        Assert.Equal(3, page.SelectedRow);
        Undo();
        Assert.Equal(original, Ids());
        // Cut / Paste: the row leaves, then comes back after the selected row with its own id
        Click(page.Grid.RowControl(2)!.Children[0]);
        Ctrl(PhysicalKey.X);
        Assert.Equal(23, Items.Rows.Count);
        Assert.DoesNotContain(original[2], Ids());
        Click(page.Grid.RowControl(5)!.Children[0]);
        Ctrl(PhysicalKey.V);
        Assert.Equal(original[2], Items.Rows[6]["id"]);
        Assert.Equal(24, Items.Rows.Count);
        Undo();
        Undo();
        Assert.Equal(original, Ids());

        // Select All: every shown row
        Click(page.Grid.RowControl(0)!.Children[0]);
        Ctrl(PhysicalKey.A);
        Assert.True(page.AllRowsSelected);
        Assert.Equal(Enumerable.Range(0, 24), page.SelectedRows);
        Assert.All(Enumerable.Range(0, 24), r => Assert.True(Highlighted(r), $"row {r + 1} is not shown selected"));
        // Copy / Paste: a copy of every row after the last one, one undo step
        Ctrl(PhysicalKey.C);
        Assert.Equal(24, page.Clipboard!.Count);
        Ctrl(PhysicalKey.V);
        Assert.Equal(original.Concat(original.Select(id => id + "Copy")), Ids());
        Assert.False(page.AllRowsSelected);
        Assert.Equal(24, page.SelectedRow);
        Undo();
        Assert.Equal(original, Ids());
        // Duplicate
        Ctrl(PhysicalKey.A);
        Ctrl(PhysicalKey.D);
        Assert.Equal(48, Items.Rows.Count);
        Undo();
        Assert.Equal(original, Ids());
        // Delete: every row, one undo step brings them all back in order
        Ctrl(PhysicalKey.A);
        page.Grid.Focus();
        Key(PhysicalKey.Delete);
        Assert.Empty(Items.Rows);
        Undo();
        Assert.Equal(original, Ids());
        // Cut / Paste: all rows go, then come back with their ids
        Ctrl(PhysicalKey.A);
        Ctrl(PhysicalKey.X);
        Assert.Empty(Items.Rows);
        Ctrl(PhysicalKey.V);
        Assert.Equal(original, Ids());

        // with a filter, Select All takes the rows shown
        page.FilterBox.Text = "Potion";
        Capture.Pump();
        var shown = page.Grid.VisibleRowIndices.ToList();
        Assert.InRange(shown.Count, 1, 23);
        Click(page.Grid.RowControl(shown[0])!.Children[0]);
        Ctrl(PhysicalKey.A);
        Assert.Equal(shown, page.SelectedRows);
        page.Grid.Focus();
        Key(PhysicalKey.Delete);
        Assert.Equal(24 - shown.Count, Items.Rows.Count);
        Undo();
        page.FilterBox.Text = "";
        Capture.Pump();
        Assert.Equal(original, Ids());
        // a click selects one row again
        Ctrl(PhysicalKey.A);
        Click(page.Grid.RowControl(4)!.Children[0]);
        Assert.False(page.AllRowsSelected);
        Assert.Equal(new[] { 4 }, page.SelectedRows);
        Assert.False(Highlighted(3));
    }

    [AvaloniaFact]
    public async Task P11_39_UseItInAScriptNamesTheGeneratedApi()
    {
        var page = await OpenDatabase();
        page.SelectRow(2);
        var text = ScriptSample.Text(Items, 2, page.Data!.Project.Settings);
        Assert.Equal(
            "var potion = Items.Get(ItemId.Potion);   // typed, autocompleted\n" +
            "player.Heal(potion.power);              // fixed 2.00\n" +
            "if (Game.coins >= potion.price) Shop.Buy(ItemId.Potion);", text);
        // every name of the sample exists in the C# API the scripting compiler generates for the project
        var ctx = new ScriptCompileContext { ProjectName = "Sky Hopper", Tables = [.. page.Data.Tables.Select(t => t.Table)], SaveFields = page.Data.Project.Settings.Save.Fields };
        var api = ProjectApi.CSharp(ctx, []);
        Assert.Contains("public static class Items", api);
        Assert.Contains("public static Item Get(ItemId id)", api);
        Assert.Contains("public enum ItemId", api);
        Assert.Contains("Potion,", api);
        Assert.Contains("public @fixed power;", api);
        Assert.Contains("public int price;", api);
        Assert.Contains("public static bool Buy(ItemId id)", api);
        Assert.Contains("public static short coins;", api);
        Assert.Contains("Items.Get(ItemId.Potion)", Find<TextBlock>("ScriptSampleText").Inlines!.Select(i => (i as Avalonia.Controls.Documents.Run)?.Text).Aggregate("", (a, b) => a + b));
        // a table without a script action or a price shows a typed field
        Assert.Equal("var enemy1 = Enemies.Get(EnemyId.Enemy1);   // typed, autocompleted\nvar hp = enemy1.hp;                     // int 1",
            ScriptSample.Text(page.Data.Table("Enemies")!.Table, 0, page.Data.Project.Settings));
        Assert.Equal("drop table", UsedBy.Humanize("dropTable"));
    }

    [AvaloniaFact]
    public async Task P11_40_NewTableFromEmptyCsvAndCopy_UndoRemovesTheFile()
    {
        var page = await OpenDatabase();
        Find<PanelHeader>("TablesHeader").ActionCommand!.Execute(null);
        Capture.Pump();
        var dlg = Assert.IsType<NewTableDialog>(Win.Overlays.Open.Single());
        Type("Weapons");
        Assert.Equal("Weapons", dlg.NameField.Text);
        Assert.Equal("Scripts use it as Weapons.Get(WeaponId.Weapon1)", dlg.NameField.Help);
        Click(dlg.GetVisualDescendants().OfType<AgbButton>().First(b => (string?)b.Content == "Create Table"));
        Assert.False(Win.Overlays.IsOpen);
        var file = Path.Combine(Root, "data", "Weapons.db");
        var t = GameProject.Load<TableFile>(file);
        Assert.Equal(["id", "name", "description"], t.Fields.Select(f => f.Name));
        Assert.True(t.Fields[1].Localized && t.KeepInRom);
        Assert.Equal("Weapons", page.Current!.Name);
        Assert.Equal("Weapons", page.TableItems[^1].Label);
        S.Commands.Execute("edit.undo");
        Capture.Pump();
        Assert.False(File.Exists(file));
        Assert.Equal("Items", page.Current!.Name);

        // a name that exists is refused and the dialog stays open
        dlg = Dialog<NewTableDialog>("T-D17", page);
        dlg.NameField.Text = "Items";
        Assert.Null(dlg.Create());
        Assert.Equal("A table named Items already exists.", dlg.NameField.Help);
        // copy of the selected table
        dlg.NameField.Text = "Loot";
        dlg.StartFrom(NewTableDialog.Start.Copy);
        dlg.KeepInRom.IsChecked = false;
        var loot = dlg.Create()!;
        Assert.Equal(24, loot.Table.Rows.Count);
        Assert.False(loot.Table.KeepInRom);
        Win.Overlays.CloseTop();
        // from a CSV file: columns become typed fields, Name_EN / Name_TH one localized field
        dlg = Dialog<NewTableDialog>("T-D17", page);
        dlg.NameField.Text = "Shop";
        dlg.StartFrom(NewTableDialog.Start.Csv, DataFixture.WriteItemsCsv(Fx.Folder));
        Assert.Equal("Scripts use it as Shop.Get(ShopId.Potion)", dlg.NameField.Help);
        var shop = dlg.Create()!.Table;
        Assert.Equal([("id", "enum"), ("name", "text"), ("cost", "text"), ("power", "fixed"), ("notes", "text")], shop.Fields.Select(f => (f.Name, f.Type)));
        Assert.Equal(24, shop.Rows.Count);
        Assert.Equal("ยาฟื้นพลัง", page.Data!.Text("th", shop.Rows[0]["name"]));
        Assert.Equal("Potion", page.Data.Text("en", shop.Rows[0]["name"]));
    }

    [AvaloniaFact]
    public async Task P11_40_AddFieldRarityEnumIsOneBytePerRow()
    {
        var page = await OpenDatabase();
        Find<PanelHeader>("SchemaHeader").ActionCommand!.Execute(null);
        Capture.Pump();
        var dlg = Assert.IsType<AddFieldDialog>(Win.Overlays.Open.Single());
        Type("rarity");
        dlg.SetType("enum");
        Click(dlg.GetVisualDescendants().OfType<AgbButton>().First(b => b.Name == "AddValue"));
        Type("Common");
        Key(PhysicalKey.Enter);
        dlg.AddValue("Rare");
        dlg.AddValue("Epic");
        Assert.Equal(["Common", "Rare", "Epic"], dlg.EnumValues);
        Assert.Equal("Common", dlg.DefaultSelect.Selected);
        Assert.Equal("1 B · 24 rows", Find<TextBlock>("PerRowText").Text);
        Assert.Equal("int · fixed · bool · text · enum · sprite · sound · script · reference", dlg.TypeField.Help);
        Click(dlg.GetVisualDescendants().OfType<AgbButton>().First(b => (string?)b.Content == "Add Field"));
        var f = Items.Fields.Last();
        Assert.Equal(("rarity", "enum", "Common"), (f.Name, f.Type, f.Default));
        Assert.Equal(["Common", "Rare", "Epic"], f.EnumValues);
        Assert.Equal("rarity", page.SchemaItems[^1].Label);
        Assert.Equal("ROM 552 B", page.RomText);
        // fixed costs 4 bytes; a duplicate name is refused
        dlg = Dialog<AddFieldDialog>("T-D18", page);
        dlg.NameField.Text = "speed";
        dlg.SetType("fixed");
        Assert.Equal("4 B · 24 rows", Find<TextBlock>("PerRowText").Text);
        dlg.NameField.Text = "price";
        Assert.Null(dlg.Create());
        Win.Overlays.CloseTop();
        S.Commands.Execute("edit.undo");
        Capture.Pump();
        Assert.DoesNotContain(Items.Fields, x => x.Name == "rarity");
    }

    [AvaloniaFact]
    public async Task P11_40_ImportCsvWarnsOnRows7And15ThenImports()
    {
        var page = await OpenDatabase();
        var csv = DataFixture.WriteItemsCsv(Fx.Folder);
        FileChooser.For(S).OpenOverride = (_, _) => Task.FromResult<string?>(csv);
        Click(page.ImportButton);
        var dlg = Assert.IsType<ImportCsvDialog>(Win.Overlays.Open.Single());
        Assert.Equal("items_2026-10-31.csv · 24 rows · UTF-8", Find<TextBlock>("CsvFile").Text);
        Assert.Equal([("ItemID", "id · enum"), ("Name_EN", "name · EN"), ("Name_TH", "name · TH"), ("Cost", "price · int"), ("Power", "power · fixed"), ("Notes", ImportCsvDialog.Skip)], dlg.Mapping);
        Assert.Equal("2 warnings", Find<Chip>("Check_Cost").Content);
        Assert.Equal("ok", Find<Chip>("Check_Power").Content);
        Assert.Equal("skipped", Find<Chip>("Check_Notes").Content);
        Assert.Equal(["Rows 7 and 15: Cost is “N/A”, not a whole number — will use the default 0. Fix the CSV and choose it again to keep the value."], dlg.WarningTexts);
        var footer = dlg.GetVisualDescendants().OfType<DialogFooter>().Single();
        Assert.Equal("2 rows have warnings and will use the default value.", footer.Hint);
        Assert.Equal("Import 24 rows", footer.ConfirmText);
        // remap Cost to skip: no warnings
        dlg.Map("Cost", ImportCsvDialog.Skip);
        Assert.Empty(dlg.WarningTexts);
        Assert.Null(footer.Hint);
        dlg.Map("Cost", "price · int");
        Click(dlg.GetVisualDescendants().OfType<AgbButton>().First(b => (string?)b.Content == "Import 24 rows"));
        Assert.False(Win.Overlays.IsOpen);
        // Potion and Item10..Item24 are rows already (updated), Item2..Item9 are new
        Assert.Equal(24 + 8, Items.Rows.Count);
        var item7 = Items.Rows.First(r => r["id"] == "Item7");
        Assert.Equal("0", item7["price"]);
        Assert.Equal("1.50", RecordPanel.Format(RecordPanel.ParseFixed(item7["power"])));
        Assert.Equal("ไอเทม 7", page.Data!.Text("th", item7["name"]));
        Assert.Equal("Item 7", page.Data.Text("en", item7["name"]));
        Assert.Equal("30", Items.Rows[2]["price"]);
        S.Commands.Execute("edit.undo");
        Capture.Pump();
        Assert.Equal(24, Items.Rows.Count);
        Assert.Null(page.Data.Text("th", "item.item7.name"));
    }
}
