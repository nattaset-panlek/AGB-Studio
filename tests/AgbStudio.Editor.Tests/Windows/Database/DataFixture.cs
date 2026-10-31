using System.Text;
using AgbStudio.Build.Content;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Tests.Support;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.Database;

/// <summary>
/// The full Sky Hopper fixture (AgbStudio.Build.Content.SkyHopper: tables, strings, fonts, sprites, scenes, scripts)
/// plus the Figma data of the Database, Localization and Dialogue Editor frames that the generator does not write yet
/// (item names, icons and schema order of Items, "Used by" references, the four dialogue files, blip_low.wav).
/// </summary>
public static class DataFixture
{
    public static readonly string[] ItemNames = ["Gold Coin", "Heart", "Potion", "Old Key", "Feather", "Bomb", "Star", "Slime Gel", "Blue Gem"];
    static readonly string[] ItemIcons = ["coin", "heart", "block", "coin", "hero", "block", "heart", "slime", "coin"];
    static readonly string[] ItemNamesTh = ["เหรียญทอง", "หัวใจ", "ยาฟื้นพลัง", "กุญแจเก่า", "ขนนก", "ระเบิด", "ดาว", "เจลสไลม์", "อัญมณีสีฟ้า"];

    /// <summary>Creates, patches and opens the project; documents and status as in the Figma Scene Editor.</summary>
    public static string Open(Fixture fx)
    {
        var root = Path.Combine(fx.Folder, "SkyHopper");
        var p = SkyHopper.Create(root);
        Patch(p);
        fx.Services.Project.Open(root);
        var d = fx.Services.Documents;
        d.Add(new EditorDocument { Name = "Scene", Glyph = "map", Path = Path.Combine(root, "scenes", "Forest_01.scene") }, activate: true);
        d.Add(new EditorDocument { Name = "Game", Glyph = "pad" });
        var s = fx.Services.Status;
        s.Build = "Build OK · 0.8 s";
        s.Rom = "ROM 412 KB / 32 MB";
        s.Performance = "59.7 fps · CPU 38%";
        s.ToolchainOk = true;
        return root;
    }

    static void Patch(GameProject p)
    {
        // Items: the Figma schema order (id, name, icon, type, price, power, stackable, effect) and row data
        var itemsPath = Path.Combine(p.DataDir, "items.db");
        var items = GameProject.Load<TableFile>(itemsPath);
        string[] order = ["id", "name", "icon", "type", "price", "power", "stackable", "effect", "description"];
        items.Fields = [.. items.Fields.OrderBy(f => Array.IndexOf(order, f.Name))];
        var en = GameProject.Load<StringTable>(Path.Combine(p.TextDir, "strings.en.json"));
        var th = GameProject.Load<StringTable>(Path.Combine(p.TextDir, "strings.th.json"));
        for (var i = 0; i < ItemNames.Length; i++)
        {
            var row = items.Rows[i];
            row["icon"] = ItemIcons[i];
            en.Strings[row["name"]] = ItemNames[i];
            th.Strings[row["name"]] = ItemNamesTh[i];
        }
        items.Rows[7]["price"] = "5";
        GameProject.Save(itemsPath, items);

        // Used by: a shop slot, a chest in Cave_01 and the drop table of Slime.cs
        var shopsPath = Path.Combine(p.DataDir, "Shops.db");
        var shops = GameProject.Load<TableFile>(shopsPath);
        for (var s = 1; s <= 3; s++) shops.Fields.Add(new FieldDef { Name = "slot" + s, Type = "reference", Reference = "Items" });
        for (var r = 0; r < shops.Rows.Count; r++) shops.Rows[r]["id"] = $"Shop_{r + 1:D2}";
        shops.Rows[0]["slot1"] = "Heart";
        shops.Rows[0]["slot2"] = "Potion";
        GameProject.Save(shopsPath, shops);
        var cavePath = Path.Combine(p.ScenesDir, "Cave_01.scene");
        var cave = GameProject.Load<SceneFile>(cavePath);
        cave.Actors.Add(new ActorData { Name = "Chest", Kind = ActorKind.Empty, Sprite = "block", X = 120, Y = 600, Components = { new ComponentData { Type = "Chest", Fields = { ["item"] = "Potion" } } } });
        GameProject.Save(cavePath, cave);
        File.WriteAllText(Path.Combine(p.ScriptsDir, "Slime.cs"),
            "using GBA;\n\npublic class Slime : Behaviour\n{\n    public fixed speed = 0.5;\n    public int damage = 1;\n    ItemId[] dropTable = { ItemId.SlimeGel, ItemId.Potion };\n}\n");

        // Localization: the note Figma shows for dlg.forest.hopper_02
        var metaPath = Path.Combine(p.TextDir, "strings.meta.json");
        var meta = GameProject.Load<StringMeta>(metaPath);
        meta.Keys["dlg.forest.hopper_02"].Notes = "Hopper is worried; keep it short so it fits two lines.";
        GameProject.Save(metaPath, meta);
        GameProject.Save(Path.Combine(p.TextDir, "strings.en.json"), en);
        GameProject.Save(Path.Combine(p.TextDir, "strings.th.json"), th);

        // a lower blip for New Speaker
        var sfx = Path.Combine(p.AssetsDir, "audio", "sfx");
        File.Copy(Path.Combine(sfx, "blip.wav"), Path.Combine(sfx, "blip_low.wav"), true);

        var created = new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);
        foreach (var (name, json) in Dialogues())
        {
            var path = Path.Combine(p.DialogueDir, name + ".dlg");
            File.WriteAllText(path, json, new UTF8Encoding(false));
            File.SetCreationTimeUtc(path, created = created.AddMinutes(1));
        }
        // the tables in the order the generator creates them (Figma lists them so)
        foreach (var table in new[] { "Items", "Enemies", "Shops", "Quests", "Levels", "Dialogue" })
            File.SetCreationTimeUtc(Path.Combine(p.DataDir, table + ".db"), created = created.AddMinutes(1));
    }

    const string Speakers = """
      "speakers": [
        { "name": "Hopper", "portrait": "hero" },
        { "name": "Owl", "portrait": "block" },
        { "name": "Slime King", "portrait": "slime" }
      ],
""";

    static string Line(string speaker, string key) => $"        {{ \"speaker\": \"{speaker}\", \"key\": \"{key}\" }}";

    static string File_(string name, IEnumerable<string> lines) =>
        $"{{\n  \"format\": 1,\n  \"name\": \"{name}\",\n  \"textSpeed\": 2,\n  \"blip\": \"blip\",\n{Speakers}\n  \"conversations\": [\n    {{\n      \"id\": \"main\",\n      \"lines\": [\n{string.Join(",\n", lines)}\n      ]\n    }}\n  ]\n}}\n";

    /// <summary>forest_intro (the Figma conversation), shop_welcome, cave_hint and boss_summit.</summary>
    public static IEnumerable<(string Name, string Json)> Dialogues()
    {
        yield return ("forest_intro", File_("forest_intro",
        [
            "        { \"id\": \"hopper_01\", \"speaker\": \"Hopper\", \"key\": \"dlg.forest.hopper_01\" }",
            """
                    { "id": "owl_01", "speaker": "Owl", "key": "dlg.forest.owl_01", "portrait": "owl_happy", "speed": 2, "blip": "blip",
                      "showIf": "!Flags.MetOwl", "thenSet": [ "Flags.MetOwl = true" ],
                      "choices": [
                        { "key": "dlg.forest.choice_cave", "scene": "Cave_01" },
                        { "key": "dlg.forest.choice_again", "goto": "owl_01" }
                      ] }
            """.TrimEnd(),
            "        { \"id\": \"hopper_02\", \"speaker\": \"Hopper\", \"key\": \"dlg.forest.hopper_02\" }",
        ]));
        yield return ("shop_welcome", File_("shop_welcome", [Line("Hopper", "shop.welcome"), Line("Owl", "dlg.extra.010"), Line("Hopper", "dlg.extra.011"), Line("Owl", "dlg.extra.012")]));
        yield return ("cave_hint", File_("cave_hint", [Line("Owl", "dlg.extra.020"), Line("Hopper", "dlg.extra.021"), Line("Owl", "dlg.extra.022")]));
        yield return ("boss_summit", File_("boss_summit", [Line("Slime King", "boss.summit.intro"), .. Enumerable.Range(30, 8).Select(i => Line(i % 2 == 0 ? "Hopper" : "Slime King", $"dlg.extra.{i:D3}"))]));
    }

    /// <summary>The Figma Import CSV file: ItemID, Name_EN, Name_TH, Cost, Power, Notes; 24 rows; Cost "N/A" on rows 7 and 15.</summary>
    public static string WriteItemsCsv(string folder)
    {
        var sb = new StringBuilder("ItemID,Name_EN,Name_TH,Cost,Power,Notes\n");
        sb.Append("Potion,Potion,ยาฟื้นพลัง,30,2.0,tastes sweet\n");
        for (var i = 2; i <= 24; i++)
            sb.Append($"Item{i},Item {i},ไอเทม {i},{(i is 7 or 15 ? "N/A" : (i * 10).ToString(System.Globalization.CultureInfo.InvariantCulture))},1.5,\n");
        var path = Path.Combine(folder, "items_2026-10-31.csv");
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        return path;
    }
}

/// <summary>A headless main window on the Figma fixture, with click / key / text helpers (as Shell/ShellE2ETests).</summary>
public abstract class DataE2E : IDisposable
{
    protected readonly Fixture Fx = new();
    protected readonly MainWindow Win;
    protected readonly string Root;

    protected DataE2E()
    {
        Root = DataFixture.Open(Fx);
        Win = new MainWindow(Fx.Services) { Width = 1440, Height = 900 };
        Win.Show();
        Capture.Pump();
    }

    public void Dispose()
    {
        Win.Close();
        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        Fx.Dispose();
        GC.SuppressFinalize(this);
    }

    protected EditorServices S => Fx.Services;

    protected T Find<T>(string name) where T : Control => Win.GetVisualDescendants().OfType<T>().First(c => c.Name == name);
    protected T? TryFind<T>(string name) where T : Control => Win.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == name);

    protected void Click(Control c, int clicks = 1)
    {
        var p = c.TranslatePoint(new Point(Math.Min(c.Bounds.Width / 2, 20), c.Bounds.Height / 2), Win)!.Value;
        Win.MouseMove(p);
        for (var i = 0; i < clicks; i++)
        {
            Win.MouseDown(p, MouseButton.Left);
            Win.MouseUp(p, MouseButton.Left);
        }
        Capture.Pump();
    }

    protected void Press(Button b)
    {
        b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Capture.Pump();
    }

    protected void Type(string text)
    {
        Win.KeyTextInput(text);
        Capture.Pump();
    }

    protected void Key(PhysicalKey k, RawInputModifiers m = RawInputModifiers.None)
    {
        Win.KeyPressQwerty(k, m);
        Win.KeyReleaseQwerty(k, m);
        Capture.Pump();
    }

    protected async Task Open(string id, object? args = null)
    {
        await S.Windows.Open(id, args, source: "test");
        Capture.Pump();
    }

    /// <summary>Opens a dialog (it stays open while the test drives it) and returns it.</summary>
    protected T Dialog<T>(string id, object? args = null) where T : Control
    {
        _ = S.Windows.Open(id, args, source: "test");
        Capture.Pump();
        return Assert.IsType<T>(Win.Overlays.Open.Last());
    }

    /// <summary>Saves the active document with Ctrl+S.</summary>
    protected void Save()
    {
        Win.Focus();
        Key(PhysicalKey.S, RawInputModifiers.Control);
    }
}
