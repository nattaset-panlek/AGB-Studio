using System.Diagnostics;
using AgbStudio.Build;
using AgbStudio.Build.Content;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;
using AgbStudio.Emu;

namespace AgbStudio.Tests;

[Collection("emulator")]
public sealed class PipelineTests(EmulatorFixture emu)
{
    static readonly string Root = Path.Combine(NativeBuild.RepoRoot, "build", "test-projects");

    static (GameProject Project, BuildResult Result) BuildMini(string name)
    {
        var p = SampleProjects.CreatePlatformerMini(Path.Combine(Root, name));
        var r = new GameBuilder(p, new Toolchain()).Build();
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        return (p, r);
    }

    public sealed record ActorState(string Name, int X, int Y, bool OnGround, bool Alive);

    static List<ActorState> ReadActors(LibretroCore core, DebugChannel ch)
    {
        var list = new List<ActorState>();
        var table = ch.ActorTable;
        var stride = ch.ActorStride;
        for (uint i = 0; i < ch.ActorCount; i++)
        {
            var a = table + i * stride;
            var namePtr = core.ReadU32(a);
            var name = ReadString(core, namePtr);
            var x = (int)core.ReadU32(a + 4) >> 8;
            var y = (int)core.ReadU32(a + 8) >> 8;
            var flags = core.Memory(a + 22, 1)[0];
            list.Add(new ActorState(name, x, y, (flags & 2) != 0, (flags & 1) != 0));
        }
        return list;
    }

    static string ReadString(LibretroCore core, uint ptr)
    {
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < 64; i++)
        {
            var b = core.Memory(ptr + (uint)i, 1)[0];
            if (b == 0) break;
            sb.Append((char)b);
        }
        return sb.ToString();
    }

    [Fact]
    public void MiniPlatformerBuildsAndPlays()
    {
        var (_, r) = BuildMini("mini_play");
        var core = emu.Core;
        core.LoadRom(r.RomPath);
        DebugChannel? ch = null;
        List<ActorState> actors = new();
        for (var f = 0; f < 240; f++)
        {
            core.RunFrame();
            ch ??= DebugChannel.Find(core);
        }
        Assert.NotNull(ch);
        actors = ReadActors(core, ch!);
        core.UnloadRom();
        Assert.Contains(actors, a => a.Name == "Player");
        var player = actors.First(a => a.Name == "Player");
        Assert.True(player.OnGround, $"player at {player.X},{player.Y}");
        Assert.Equal(144, player.Y);  // origin at the bottom of the box = top of ground row 9 (9 * 16)
        var slime = actors.First(a => a.Name == "Slime");
        Assert.True(slime.OnGround);
        Assert.InRange(slime.X, 224, 320);  // patrols between the gap and the wall
        Assert.Equal(4, actors.Count);
    }

    [Fact]
    public void SecondBuildWithoutChangesConvertsNothingAndIsFast()
    {
        var (p, first) = BuildMini("mini_incremental");
        Assert.True(first.AssetsConverted > 0);
        var sw = Stopwatch.StartNew();
        var second = new GameBuilder(p, new Toolchain()).Build();
        sw.Stop();
        Assert.True(second.Success);
        Assert.Equal(0, second.AssetsConverted);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(1), $"no-change build took {sw.Elapsed.TotalSeconds:0.00} s");
        // Changing one sprite option reconverts exactly that sprite.
        var hero = Path.Combine(p.AssetsDir, "sprites", "hero.png");
        var meta = p.LoadMeta<SpriteMeta>(hero);
        meta.OriginY = 15;
        GameProject.Save(GameProject.MetaPath(hero), meta);
        var third = new GameBuilder(p, new Toolchain()).Build();
        Assert.True(third.Success);
        Assert.Equal(1, third.AssetsConverted);
    }

    [Fact]
    public void ReportAttributesRomToAssets()
    {
        var (p, r) = BuildMini("mini_report");
        var report = BuildReport.Load(p.BuildDir)!;
        Assert.Equal(new FileInfo(r.RomPath).Length, report.RomBytes);
        Assert.True(report.Breakdown["Graphics"] > 0);
        Assert.True(report.Breakdown["Code"] > 0);
        Assert.True(report.Breakdown["Audio"] > 0);
        Assert.Contains(report.LargestAssets, a => a.Name == "forest_tiles" && a.Type == "Tileset");
        Assert.Contains(report.LargestAssets, a => a.Name == "hero" && a.Type == "Sprite");
        Assert.True(report.IwramBytes > 0 && report.IwramBytes < 32 * 1024);
        Assert.True(report.EwramBytes > 0 && report.EwramBytes < 256 * 1024);
        Assert.True(report.Scenes.ContainsKey("Forest_01"));
        Assert.True(File.Exists(Path.Combine(p.BuildDir, "report-history.json")));
        // ROM header written by gbafix from Project Settings · General.
        var rom = File.ReadAllBytes(r.RomPath);
        Assert.Equal("MINIPLAT", System.Text.Encoding.ASCII.GetString(rom, 0xA0, 8));
        Assert.Equal("AMPE", System.Text.Encoding.ASCII.GetString(rom, 0xAC, 4));
        Assert.Equal("SRAM_V113", System.Text.Encoding.ASCII.GetString(rom, rom.AsSpan().IndexOf("SRAM_V"u8), 9));
    }

    [Fact]
    public void ErrorsNameTheFileAndTheProblem()
    {
        var p = SampleProjects.CreatePlatformerMini(Path.Combine(Root, "mini_errors"));
        var scenePath = Path.Combine(p.ScenesDir, "Forest_01.scene");
        var scene = GameProject.Load<SceneFile>(scenePath);
        scene.Actors[0].Sprite = "missing_sprite";
        scene.Actors[1].Components.Add(new ComponentData { Type = "NoSuchBehaviour" });
        GameProject.Save(scenePath, scene);
        var r = new GameBuilder(p, new Toolchain()).Build();
        Assert.False(r.Success);
        Assert.Contains(r.Errors, d => d.Message.Contains("uses sprite 'missing_sprite', which is not in assets/sprites") && d.File.EndsWith("Forest_01.scene"));
        Assert.Contains(r.Errors, d => d.Message.Contains("behaviour 'NoSuchBehaviour' was not found"));
        Assert.Equal("Generate game", r.FailedStep);
    }

    [Fact]
    public void GccErrorsBecomeClickableDiagnostics()
    {
        var d = GameBuilder.ParseGccLine(@"D:\proj\scripts\PlayerController.cs:22:9: error: expected ';' before '}' token");
        Assert.NotNull(d);
        Assert.Equal(22, d!.Line);
        Assert.Equal(9, d.Column);
        Assert.Equal("PlayerController.cs(22,9): expected ';' before '}' token", d.ToString());
    }
}
