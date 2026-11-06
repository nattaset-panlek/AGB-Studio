using AgbStudio.Build;
using AgbStudio.Build.Audio;
using AgbStudio.Build.Content;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;

namespace AgbStudio.Tests;

/// <summary>
/// P3.11: the build reads the scene's music Volume and each music module's MusicMeta (assets/audio/music/&lt;file&gt;.meta.json): the loop start becomes
/// the module's restart order in the soundbank, volume and fade on scene change go into the agb_soundbank_music table, and
/// the game definition hands the table to the runtime (FixtureBuildTests.GameDefinitionHandsTheMusicMetaToTheRuntime; the
/// audio_music ROM test plays it).
/// </summary>
public sealed class MusicMetaTests
{
    static readonly Lazy<GameProject> s_project = new(() => SkyHopper.Create(Path.Combine(NativeBuild.RepoRoot, "build", "test-projects", "music_meta")));

    // MSL soundbank: u16 samples, u16 modules, "*maxmod*", u32 offsets; a module's MAS header follows an 8-byte prefix.
    static (int Orders, int Restart) ModuleHeader(byte[] bank, int module)
    {
        int samples = BitConverter.ToUInt16(bank, 0);
        var at = (int)BitConverter.ToUInt32(bank, 12 + 4 * (samples + module));
        return (bank[at + 8], bank[at + 16]);
    }

    [Fact]
    public void SoundbankCarriesVolumeLoopStartAndFade()
    {
        var p = s_project.Value;
        var dir = Path.Combine(p.BuildDir, "sb-meta");
        var r = SoundbankBuilder.Build(p.MusicFiles().Concat(p.SoundFiles()).ToList(), dir);
        Assert.True(r.Success, r.Log);
        var modules = r.Ids.Count(k => k.Key.StartsWith("MOD_", StringComparison.Ordinal));
        Assert.Equal(modules, r.Music.Count);
        var bank = File.ReadAllBytes(Path.Combine(dir, "soundbank.bin"));
        var cpp = File.ReadAllText(r.CppPath);

        // bgm_forest.xm.meta.json: volume 0.8, loop start 4, fade on scene change
        var forestId = r.Ids["MOD_BGM_FOREST"];
        var forest = r.Music[forestId];
        Assert.EndsWith("bgm_forest.xm", forest.File);
        var (orders, restart) = ModuleHeader(bank, forestId);
        Assert.True(orders > 4, $"bgm_forest.xm has {orders} orders");
        Assert.Equal((205, 4, true), (forest.Volume, forest.LoopStart, forest.FadeOnSceneChange));
        Assert.Equal(4, restart);  // maxmod continues at the loop start when the song ends
        Assert.Contains("{205, 4, true},  // bgm_forest.xm", cpp);

        // A module without a meta file gets the defaults the Audio page shows (volume 0.8, loop start 0, fade).
        var titleId = r.Ids["MOD_BGM_TITLE"];
        Assert.False(File.Exists(GameProject.MetaPath(r.Music[titleId].File)));
        Assert.Equal((205, 0, true), (r.Music[titleId].Volume, r.Music[titleId].LoopStart, r.Music[titleId].FadeOnSceneChange));
        Assert.Equal(0, ModuleHeader(bank, titleId).Restart);
        Assert.Contains($"constexpr unsigned short agb_soundbank_music_count = {modules};", File.ReadAllText(r.HeaderPath));
    }

    [Fact]
    public void LoopStartPastTheLastOrderIsClampedAndMetaEditsRebuildTheSoundbank()
    {
        var p = s_project.Value;
        var dir = Path.Combine(p.BuildDir, "sb-meta-edit");
        Directory.CreateDirectory(dir);
        var module = Path.Combine(dir, "song.xm");
        File.Copy(p.MusicFiles().Single(f => f.EndsWith("bgm_title.xm", StringComparison.Ordinal)), module, true);
        File.Delete(GameProject.MetaPath(module));
        var noMeta = GameBuilder.SoundbankKey([module]);

        GameProject.Save(GameProject.MetaPath(module), new MusicMeta { Volume = 0.25, LoopStart = 200, FadeOnSceneChange = false });
        var withMeta = GameBuilder.SoundbankKey([module]);
        var r = SoundbankBuilder.Build([module], Path.Combine(dir, "out"));
        Assert.True(r.Success, r.Log);
        var (orders, restart) = ModuleHeader(File.ReadAllBytes(Path.Combine(dir, "out", "soundbank.bin")), 0);
        Assert.Equal(orders - 1, restart);  // 200 is past the end: the last order, as the Audio page plays it
        Assert.Equal((64, orders - 1, false), (r.Music[0].Volume, r.Music[0].LoopStart, r.Music[0].FadeOnSceneChange));

        // The cache key follows the meta file, so an edit on the Audio page reaches the next build.
        GameProject.Save(GameProject.MetaPath(module), new MusicMeta { Volume = 0.25, LoopStart = 1, FadeOnSceneChange = false });
        var edited = GameBuilder.SoundbankKey([module]);
        Assert.NotEqual(noMeta, withMeta);
        Assert.NotEqual(withMeta, edited);
    }

    /// <summary>
    /// The scene's music Volume (World Map inspector, MusicSettings.Volume) goes into its SceneDef as musicVolume (0..256):
    /// the game's music volume while the scene's own track plays (the audio_music ROM test plays it).
    /// </summary>
    [Fact]
    public void SceneMusicVolumeGoesIntoTheSceneDefinition()
    {
        var p = SkyHopper.Create(Path.Combine(NativeBuild.RepoRoot, "build", "test-projects", "music_scene_volume"));
        var forest = p.SceneFiles().Single(f => GameProject.AssetName(f) == "Forest_01");
        var scene = GameProject.Load<SceneFile>(forest);
        scene.Music.Volume = 0.35;
        GameProject.Save(forest, scene);
        var r = new GameBuilder(p, new Toolchain()).Build(new BuildOptions { GenerateOnly = true });
        var gen = Path.Combine(p.BuildDir, "gen", "scenes");
        string Def(string name)
        {
            var cpp = File.ReadAllText(Path.Combine(gen, $"scene_{name}.cpp")).Replace("\r\n", "\n");
            return cpp[cpp.IndexOf($"extern const agb::SceneDef agbasset_scene_{name} = {{", StringComparison.Ordinal)..];
        }
        Assert.True(File.Exists(Path.Combine(gen, "scene_Forest_01.cpp")), string.Join('\n', r.Diagnostics));
        Assert.EndsWith(", 90};\n", Def("Forest_01"));   // 0.35 x 256 = 89.6
        Assert.EndsWith(", 205};\n", Def("Forest_02"));  // the default 0.8
        Assert.Equal(256, GameBuilder.SceneMusicVolume(new SceneFile { Music = new MusicSettings { Volume = 1.5 } }));
        Assert.Equal(0, GameBuilder.SceneMusicVolume(new SceneFile { Music = new MusicSettings { Volume = -1 } }));
    }
}
