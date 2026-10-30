using AgbStudio.Core.Project;

namespace AgbStudio.Tests;

public sealed class ProjectModelTests
{
    [Fact]
    public void EveryFileFormatRoundTrips()
    {
        var dir = Directory.CreateTempSubdirectory("agbproj").FullName;
        var p = GameProject.Create(dir, "Sky Hopper");
        p.Settings.General.RomTitle = "SKYHOPPER";
        p.Settings.Save.Fields.Add(new SaveField { Name = "score", Type = "int" });
        p.SaveSettings();
        var reopened = GameProject.Open(dir);
        Assert.Equal("SKYHOPPER", reopened.Settings.General.RomTitle);
        Assert.EndsWith("SkyHopper.agbproj", reopened.ProjectPath);
        Assert.Single(reopened.Settings.Save.Fields);

        void RoundTrip<T>(string name, T value)
        {
            var path = Path.Combine(dir, name);
            GameProject.Save(path, value);
            var text = File.ReadAllText(path);
            GameProject.Save(path, GameProject.Load<T>(path));
            Assert.Equal(text, File.ReadAllText(path));
            Assert.Contains("\"format\": 1", text);
        }

        RoundTrip("a.scene", new SceneFile
        {
            Name = "Forest_01",
            Actors = { new ActorData { Name = "Player", Kind = ActorKind.Player, Components = { new ComponentData { Type = "PlatformerBody", Fields = { ["gravity"] = "0.25" } } } } },
            Links = { new LinkData { From = "Goal Flag", To = "Forest_02", OnlyIf = "Game.coins >= 10" } },
        });
        RoundTrip("a.map", new MapFile { Name = "Forest_01", Layers = { new MapLayerData { Name = "Ground", Cells = { 1, 2, 3 } } } });
        RoundTrip("a.meta.json", new SpriteMeta { Animations = { new AnimationData { Name = "run", Frames = { 0, 1, 2 } } } });
        RoundTrip("t.meta.json", new TilesetMeta { Tiles = { [3] = new TileProperties { Collision = CollisionType.OneWay } } });
        RoundTrip("a.sfx", new SfxFile { Name = "jump" });
        RoundTrip("a.font", new FontFile { Name = "thai_vwf", CharacterSets = { "Thai" } });
        RoundTrip("a.db", new TableFile { Fields = { new FieldDef { Name = "rarity", Type = "enum", EnumValues = { "Common", "Rare" } } } });
        RoundTrip("strings.th.json", new StringTable { Language = "th", Strings = { ["ui.menu.start"] = "เริ่มเกม" } });
    }

    [Fact]
    public void SaveFieldSizesMatchProjectSettings()
    {
        // Figma Save data tab: score int 4 B, lives 1 B, coins 2 B, flags bitset 128 16 B, inventory Items[16] 32 B, position fixed2 8 B.
        Assert.Equal(4, new SaveField { Type = "int" }.SizeBytes());
        Assert.Equal(1, new SaveField { Type = "byte" }.SizeBytes());
        Assert.Equal(2, new SaveField { Type = "short" }.SizeBytes());
        Assert.Equal(16, new SaveField { Type = "bitset 128" }.SizeBytes());
        Assert.Equal(32, new SaveField { Type = "Items[16]" }.SizeBytes());
        Assert.Equal(8, new SaveField { Type = "fixed2" }.SizeBytes());
    }

    [Fact]
    public void NewerFormatIsRefusedWithAClearMessage()
    {
        var dir = Directory.CreateTempSubdirectory("agbproj").FullName;
        File.WriteAllText(Path.Combine(dir, "X.agbproj"), "{ \"format\": 9 }");
        var e = Assert.Throws<InvalidDataException>(() => GameProject.Open(dir));
        Assert.Contains("newer AGB Studio", e.Message);
    }
}
