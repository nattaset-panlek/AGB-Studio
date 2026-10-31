using AgbStudio.Build;
using AgbStudio.Build.Audio;
using AgbStudio.Build.Content;
using AgbStudio.Core;
using AgbStudio.Core.Project;

namespace AgbStudio.Tests;

/// <summary>P1.05: the Sky Hopper fixture holds the data the Figma frames show.</summary>
public sealed class FixtureTests
{
    static readonly Lazy<GameProject> s_project = new(() => SkyHopper.Create(Path.Combine(NativeBuild.RepoRoot, "build", "test-projects", "skyhopper_fixture")));

    [Fact]
    public void CountsMatchTheFigmaFrames()
    {
        var p = s_project.Value;
        Assert.Equal(SkyHopper.SceneNames.OrderBy(n => n), p.SceneFiles().Select(GameProject.AssetName).OrderBy(n => n));   // World Map: 5 scenes
        var links = p.SceneFiles().Sum(f => GameProject.Load<SceneFile>(f).Links.Count);
        Assert.Equal(6, links);                                                                                              // World Map: 6 links
        var forest = GameProject.Load<SceneFile>(Path.Combine(p.ScenesDir, "Forest_01.scene"));
        Assert.Equal((30, 20), (forest.Width, forest.Height));                                                               // Forest_01 · 30×20 tiles
        Assert.Equal(5, forest.Actors.Count(a => a.Group == "Coins"));                                                      // Hierarchy: Coins 5
        Assert.Equal(2, forest.Actors.Count(a => a.Group == "Enemies"));                                                    // Enemies 2
        Assert.Equal(12, p.SpriteFiles().Count());                                                                          // Assets: Sprites 12
        Assert.Equal(12, p.ScriptFiles().Count());                                                                          // Build ROM: 12 scripts
        Assert.Equal(3, p.FontFiles().Count());                                                                             // Font Manager: thai_vwf, pixel8, title_big
        Assert.Equal(9, p.MusicFiles().Count() + p.SoundFiles().Count());                                                   // Assets: Audio 9
        var en = GameProject.Load<StringTable>(Path.Combine(p.TextDir, "strings.en.json"));
        var th = GameProject.Load<StringTable>(Path.Combine(p.TextDir, "strings.th.json"));
        var ja = GameProject.Load<StringTable>(Path.Combine(p.TextDir, "strings.ja.json"));
        Assert.Equal(340, en.Strings.Count);                                                                                // 340 keys
        Assert.Equal(326, th.Strings.Count);                                                                                // ไทย 326 / 340
        Assert.Empty(ja.Strings);                                                                                           // 日本語 0 / 340
        var meta = GameProject.Load<StringMeta>(Path.Combine(p.TextDir, "strings.meta.json"));
        Assert.Equal(112, meta.Keys.Values.Count(i => i.Group == "Dialogue"));                                              // Groups: Dialogue 112
        Assert.DoesNotContain("shop.welcome", th.Strings.Keys);                                                             // missing in ไทย
        Assert.DoesNotContain("boss.summit.intro", th.Strings.Keys);
        Assert.Equal("items.db", Path.GetFileName(p.TableFiles().Single(f => GameProject.AssetName(f) == "items")));       // Database tab "items.db"
        var items = GameProject.Load<TableFile>(Path.Combine(p.DataDir, "items.db"));
        Assert.Equal(24, items.Rows.Count);                                                                                 // Items 24 rows
        Assert.Equal(9, items.Fields.Count);                                                                                // 9 fields
        Assert.Equal("0", items.Rows.Single(r => r["id"] == "Heart")["price"]);                                             // Heart · price 0
        Assert.Equal(9, GameProject.Load<TableFile>(Path.Combine(p.DataDir, "Enemies.db")).Rows.Count);
        Assert.Equal(112, GameProject.Load<TableFile>(Path.Combine(p.DataDir, "Dialogue.db")).Rows.Count);
        Assert.Equal(6, p.TableFiles().Count());
        Assert.Equal(63, p.Settings.Save.Fields.Sum(f => f.SizeBytes()));                                                   // 63 bytes per slot
        Assert.Equal("SKYHOPPER", p.Settings.General.RomTitle);
    }

    /// <summary>R7 of the windows SHOT review: the art, names and rule sets the Figma frames draw.</summary>
    [Fact]
    public void ArtNamesAndRuleSetsMatchTheFigmaFrames()
    {
        var p = s_project.Value;
        // T-P08 / T-P06: the yellow hero, 8 frames; idle 8f · loop at 6 ticks, run 6f · loop, jump 3f, hurt 2f
        var hero = Png.Read(Path.Combine(p.AssetsDir, "sprites", "hero.png"));
        Assert.Equal((128, 16), (hero.Width, hero.Height));
        Assert.Equal(0xFFFFD84Au, hero.Pixels[3 * hero.Width + 7]);                                              // yellow body (art-sprite-hero)
        var meta = p.LoadMeta<SpriteMeta>(Path.Combine(p.AssetsDir, "sprites", "hero.png"));
        Assert.Equal([("idle", 8, 6, true), ("run", 6, 6, true), ("jump", 3, 6, false), ("hurt", 2, 6, false)],
            meta.Animations.Select(a => (a.Name, a.Frames.Count, a.Ticks, a.Loop)));
        Assert.Equal((8, 16), (meta.OriginX, meta.OriginY));                                                      // Sprite settings · Origin
        // T-P01 / T-P02: Forest_01 starts on the screen of art-scene-day: the hero on the ground, three coins over the
        // bricks, the ? block, the flag, a slime and the HUD (SCORE and three hearts)
        var forest = GameProject.Load<SceneFile>(Path.Combine(p.ScenesDir, "Forest_01.scene"));
        var map = GameProject.Load<MapFile>(Path.Combine(p.MapsDir, "Forest_01.map"));
        Assert.Equal(SkyHopper.StartCamera, (0, map.Height * 16 - 160));
        var player = forest.Actors.Single(a => a.Name == "Player");
        Assert.Equal(SkyHopper.PlayerStart, (player.X, player.Y));
        Assert.Equal(SkyHopperArt.TGrass, map.Layers[2].Cells[(player.Y / 16) * map.Width + player.X / 16]);   // standing on grass
        Assert.Equal(3, forest.Actors.Count(a => a.Sprite == "heart" && a.Group == "HUD"));
        Assert.Equal("HUD", forest.Actors.Single(a => a.Name == "HUD · Score").Components.Single().Type);
        Assert.Equal(3, forest.Actors.Count(a => a.Sprite == "coin" && a.Y - 160 < 160 && a.X < 240));           // on the start screen
        // the bricks under the coins run from x 88 to 136 (art-scene-day): a left end, two bricks, a right end
        Assert.Equal([SkyHopperArt.TBricksLeft, SkyHopperArt.TBricks, SkyHopperArt.TBricks, SkyHopperArt.TBricksRight], map.Layers[2].Cells.Skip(15 * map.Width + 5).Take(4));
        var tileArt = Png.Read(Path.Combine(p.AssetsDir, "tilesets", "forest_tiles.png"));
        bool Brick(int worldX) => worldX / 16 - 5 is var i and >= 0 and < 4
            && (tileArt.Pixels[8 * tileArt.Width + map.Layers[2].Cells[15 * map.Width + 5 + i] * 16 + worldX % 16] >> 24) != 0;
        Assert.Equal((false, true, true, false), (Brick(87), Brick(88), Brick(135), Brick(136)));
        // T-P03 Collision · One-way 9; T-P26 "Grass edges": Top edge, Left edge, Right edge, Corner, Inner, no "any" cell
        var tiles = p.LoadMeta<TilesetMeta>(Path.Combine(p.AssetsDir, "tilesets", "forest_tiles.png"));
        Assert.Equal(9, map.Layers[2].Cells.Count(c => tiles.Tiles.GetValueOrDefault(c)?.Collision == CollisionType.OneWay));
        // T-P08 Sprites list (creation order): hero, slime, coin, block, heart, cloud
        Assert.Equal(["hero", "slime", "coin", "block", "heart", "cloud"],
            p.SpriteFiles().OrderBy(File.GetCreationTimeUtc).Take(6).Select(GameProject.AssetName));
        Assert.Equal(12, p.SpriteFiles().Select(File.GetCreationTimeUtc).Distinct().Count());
        // T-P25 jump.sfx: Sweep up 2 (shift 6)
        Assert.Equal(6, GameProject.Load<SfxFile>(Path.Combine(p.SfxDir, "jump.sfx")).SweepShift);
        var rules = tiles.AutoTiles.Single(s => s.Name == "Grass edges").Rules;
        Assert.Equal(["Top edge", "Left edge", "Right edge", "Corner", "Inner"], rules.Select(r => r.Name));
        Assert.DoesNotContain(rules, r => r.Mask.Contains(2));
        // T-D16 / T-D15 "NotoSansThai-Regular.ttf"; T-P09 Draw to "BG 2", title_big 16×16
        var thai = GameProject.Load<FontFile>(Path.Combine(p.FontsDir, "thai_vwf.font"));
        Assert.Equal(("NotoSansThai-Regular.ttf", 2), (thai.Source, thai.DrawTo));
        Assert.True(File.Exists(Path.Combine(p.FontsDir, thai.Source)));
        var big = GameProject.Load<FontFile>(Path.Combine(p.FontsDir, "title_big.font"));
        Assert.Equal((false, 16, 16), (big.Variable, big.FixedWidth, big.Size));
        // T-D17 "Enemies.Get(EnemyId.Bat)"
        Assert.Equal("Bat", GameProject.Load<TableFile>(Path.Combine(p.DataDir, "Enemies.db")).Rows[0]["id"]);
    }

    [Fact]
    public void GeneratedMusicIsReadByMaxmod()
    {
        var p = s_project.Value;
        var r = SoundbankBuilder.Build(new Toolchain(), p.MusicFiles().Concat(p.SoundFiles()).ToList(), Path.Combine(p.BuildDir, "sb-test"));
        Assert.True(r.Success, r.Log);
        Assert.Contains("MOD_BGM_FOREST", r.Ids.Keys);
        Assert.Contains("MOD_BGM_CAVE", r.Ids.Keys);
        Assert.Contains("SFX_JUMP", r.Ids.Keys);
        Assert.Equal(9, r.Ids.Keys.Count(k => k.StartsWith("MOD_") || k.StartsWith("SFX_")));
    }
}
