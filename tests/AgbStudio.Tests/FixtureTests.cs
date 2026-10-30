using AgbStudio.Build;
using AgbStudio.Build.Audio;
using AgbStudio.Build.Content;
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
        var items = GameProject.Load<TableFile>(Path.Combine(p.DataDir, "Items.db"));
        Assert.Equal(24, items.Rows.Count);                                                                                 // Items 24 rows
        Assert.Equal(9, items.Fields.Count);                                                                                // 9 fields
        Assert.Equal(9, GameProject.Load<TableFile>(Path.Combine(p.DataDir, "Enemies.db")).Rows.Count);
        Assert.Equal(112, GameProject.Load<TableFile>(Path.Combine(p.DataDir, "Dialogue.db")).Rows.Count);
        Assert.Equal(6, p.TableFiles().Count());
        Assert.Equal(63, p.Settings.Save.Fields.Sum(f => f.SizeBytes()));                                                   // 63 bytes per slot
        Assert.Equal("SKYHOPPER", p.Settings.General.RomTitle);
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
