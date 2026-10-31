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

        void RoundTrip<T>(string name, T value, bool versioned = true)
        {
            var path = Path.Combine(dir, name);
            GameProject.Save(path, value);
            var text = File.ReadAllText(path);
            GameProject.Save(path, GameProject.Load<T>(path));
            Assert.Equal(text, File.ReadAllText(path));
            if (versioned) Assert.Contains("\"format\": 1", text);
            // P1.04: the file is valid against its schema (docs/schemas).
            var schema = SchemaTests.SchemaFor(dir, path);
            Assert.NotNull(schema);
            var errors = SchemaTests.Validate(schema!, text);
            Assert.True(errors.Count == 0, $"{name}: {string.Join("; ", errors)}");
        }

        RoundTrip("SkyHopper.agbproj", p.Settings);
        RoundTrip("scenes/a.scene", new SceneFile
        {
            Name = "Forest_01",
            Actors = { new ActorData { Name = "Player", Kind = ActorKind.Player, Components = { new ComponentData { Type = "PlatformerBody", Fields = { ["gravity"] = "0.25" } } } } },
            Links = { new LinkData { From = "Goal Flag", To = "Forest_02", OnlyIf = "Game.coins >= 10" } },
        });
        RoundTrip("maps/a.map", new MapFile { Name = "Forest_01", Layers = { new MapLayerData { Name = "Ground", Cells = { 1, 2, 3 } } } });
        RoundTrip("assets/sprites/a.png.meta.json", new SpriteMeta { Animations = { new AnimationData { Name = "run", Frames = { 0, 1, 2 } } } });
        RoundTrip("assets/tilesets/t.png.meta.json", new TilesetMeta { Tiles = { [3] = new TileProperties { Collision = CollisionType.OneWay } } });
        RoundTrip("audio/sfx/a.sfx", new SfxFile { Name = "jump" });
        RoundTrip("fonts/a.font", new FontFile { Name = "thai_vwf", CharacterSets = { "Thai" } });
        RoundTrip("data/a.db", new TableFile { Fields = { new FieldDef { Name = "rarity", Type = "enum", EnumValues = { "Common", "Rare" } } } });
        RoundTrip("text/strings.th.json", new StringTable { Language = "th", Strings = { ["ui.menu.start"] = "เริ่มเกม" } });
        RoundTrip("text/strings.meta.json", new StringMeta { Keys = { ["dlg.forest.hopper_01"] = new StringInfo { Group = "Dialogue", Speaker = "Hopper", BoxWidth = 200, MaxLines = 2 } } });
        RoundTrip("assets/audio/music/bgm_forest.xm.meta.json", new MusicMeta { Volume = 0.5, LoopStart = 2, FadeOnSceneChange = false });
        RoundTrip("assets/audio/sfx/jump.wav.meta.json", new SoundMeta { Rate = 11025, Volume = 0.75 });
        // The Palette Manager library (editor only): banks with their colours, assets and options, and the three effects.
        RoundTrip("assets/palettes/palettes.json", new PaletteLibraryFile
        {
            Banks =
            {
                new PaletteBankData { Kind = BankKind.Bg, Slot = 0, Name = "forest", Colors = { "none", "#2E7D32", "#A5D6A7" }, Assets = { "assets/tilesets/forest_tiles.png" } },
                new PaletteBankData { Kind = BankKind.Obj, Slot = 3, Name = "hero", Colors = { "none", "#FFCC80" }, LockTransparent = false, MergeNear = true, SortByHue = true },
            },
            Effects = new PaletteEffectsData { Cycle = new PaletteEffectData { Kind = EffectKind.Cycle, Name = "lava", Bank = 2, First = 4, Last = 7, Direction = CycleDirection.PingPong, Enabled = false } },
        });
        // Make Prefab writes the actor as it is in the scene (no format field of its own).
        RoundTrip("prefabs/Slime.prefab", new ActorData { Name = "Slime", Kind = ActorKind.Enemy, Sprite = "slime", Components = { new ComponentData { Type = "Patrol" } } }, versioned: false);
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
