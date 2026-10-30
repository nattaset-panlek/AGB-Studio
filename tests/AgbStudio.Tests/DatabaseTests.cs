using AgbStudio.Build;
using AgbStudio.Build.Content;
using AgbStudio.Build.Data;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;

namespace AgbStudio.Tests;

public sealed class DatabaseTests
{
    static TableFile Items() => new()
    {
        Name = "Items",
        Fields =
        {
            new FieldDef { Name = "id", Type = "enum" },
            new FieldDef { Name = "name", Type = "text", Localized = true },
            new FieldDef { Name = "type", Type = "enum", EnumValues = { "Currency", "Consumable", "Key item", "Equip", "Power-up", "Material", "Treasure" } },
            new FieldDef { Name = "price", Type = "int", Default = "0" },
            new FieldDef { Name = "power", Type = "fixed" },
            new FieldDef { Name = "stackable", Type = "bool" },
            new FieldDef { Name = "icon", Type = "sprite" },
            new FieldDef { Name = "effect", Type = "script" },
        },
        Rows =
        {
            new() { ["id"] = "Coin", ["name"] = "item.coin.name", ["type"] = "Currency", ["price"] = "1", ["stackable"] = "yes" },
            new() { ["id"] = "Potion", ["name"] = "item.potion.name", ["type"] = "Consumable", ["price"] = "30", ["power"] = "2.00", ["icon"] = "heart" },
        },
    };

    [Theory]
    [InlineData("Items", "Item")]
    [InlineData("Enemies", "Enemy")]
    [InlineData("Shops", "Shop")]
    [InlineData("Quests", "Quest")]
    [InlineData("Dialogue", "Dialogue")]
    public void TableNamesGiveTypedIds(string table, string single)
    {
        Assert.Equal(single, DatabaseCodegen.Singular(table));
        Assert.Equal(single + "Id", DatabaseCodegen.IdEnum(table));
    }

    [Fact]
    public void FieldBytesMatchAddField()
    {
        Assert.Equal(1, DatabaseCodegen.FieldBytes(new FieldDef { Type = "enum" }));  // "rarity · Per row · 1 B"
        Assert.Equal(4, DatabaseCodegen.FieldBytes(new FieldDef { Type = "fixed" }));
        Assert.Equal(2, DatabaseCodegen.FieldBytes(new FieldDef { Type = "text", Localized = true }));
    }

    [Fact]
    public void GeneratesEnumsStructsAndRows()
    {
        var diags = new List<Diagnostic>();
        var o = DatabaseCodegen.Generate([Items()], k => k == "item.coin.name" ? 3 : k == "item.potion.name" ? 4 : -1, _ => 0, n => n == "heart", diags, "data");
        Assert.Contains("enum class ItemId : unsigned short { Coin, Potion, Count };", o.Header);
        Assert.Contains("enum class ItemType : unsigned char { Currency, Consumable, Key_item, Equip, Power_up, Material, Treasure };", o.Header);
        Assert.Contains("static const Item& Get(ItemId id)", o.Header);
        var src = Assert.Single(o.Sources).Text;
        Assert.Contains("{3, ItemType::Currency, 1, agb::fixed(), true, nullptr, nullptr}", src);
        Assert.Contains("{4, ItemType::Consumable, 30, agb::fixed::fromRaw(512), false, &agbasset_sprite_heart, nullptr}", src);
        Assert.Empty(diags);
    }

    [Fact]
    public void ImportCsvMapsColumnsAndReportsBadRows()
    {
        // The Figma Import CSV example: Cost "N/A" on rows 7 and 15 uses the default 0.
        var csv = new System.Text.StringBuilder("ItemID,Name_EN,Name_TH,Cost,Power,Notes\n");
        for (var i = 1; i <= 24; i++)
            csv.Append($"Item{i},Item {i},ไอเทม {i},{(i is 7 or 15 ? "N/A" : (i * 10).ToString())},2.0,tastes sweet\n");
        var table = Items();
        var map = CsvImporter.AutoMap(["ItemID", "Name_EN", "Name_TH", "Cost", "Power", "Notes"], table);
        Assert.Equal("id", map[0].Field);
        Assert.Equal("name", map[1].Field);
        Assert.Equal("price", map[3].Field);
        Assert.Equal("power", map[4].Field);
        Assert.Null(map[5].Field);  // "— skip —"
        var r = CsvImporter.Import(csv.ToString(), table, map);
        Assert.Equal(24, r.Rows.Count);
        var w = Assert.Single(r.Warnings);
        Assert.Equal("Rows 7 and 15: Cost is “N/A” — will use the default 0.", w);
        Assert.Equal("0", r.Rows[6]["price"]);
        Assert.Equal("Item 1", r.Rows[0]["name"]);  // first language column wins for a localized field
        Assert.Equal(3, CsvImporter.Parse("a,\"b,c\",\"d \"\"q\"\"\"\n").Single().Length);
    }
}

[Collection("emulator")]
public sealed class DatabaseBuildTests
{
    [Fact]
    public void TablesBuildIntoTheRomAsData()
    {
        var p = SampleProjects.CreatePlatformerMini(Path.Combine(NativeBuild.RepoRoot, "build", "test-projects", "mini_db"));
        var table = new TableFile
        {
            Name = "Enemies",
            Fields = { new FieldDef { Name = "id", Type = "enum" }, new FieldDef { Name = "hp", Type = "int" }, new FieldDef { Name = "speed", Type = "fixed" },
                new FieldDef { Name = "rarity", Type = "enum", EnumValues = { "Common", "Rare", "Epic" } } },
        };
        for (var i = 0; i < 9; i++) table.Rows.Add(new() { ["id"] = $"E{i}", ["hp"] = (i * 3).ToString(), ["speed"] = "0.5", ["rarity"] = "Rare" });
        GameProject.Save(Path.Combine(p.DataDir, "Enemies.db"), table);
        var r = new GameBuilder(p, new Toolchain()).Build();
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        var report = BuildReport.Load(p.BuildDir)!;
        Assert.True(report.Breakdown["Data"] >= 9 * 12);
        Assert.Contains("struct Enemy {", File.ReadAllText(Path.Combine(p.BuildDir, "gen", "agb_db.hpp")));
    }
}
