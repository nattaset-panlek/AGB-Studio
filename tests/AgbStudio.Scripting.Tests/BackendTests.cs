using System.Globalization;
using System.Text.RegularExpressions;
using AgbStudio.Build;
using AgbStudio.Build.Content;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;
using AgbStudio.Emu;

namespace AgbStudio.Scripting.Tests;

/// <summary>
/// The scripting compiler as the game build's script step (IScriptBackend): a sample project with the Figma
/// PlayerController and a Coin script builds into a ROM through GameBuilder and plays in the emulator.
/// </summary>
[Collection("emulator")]
public sealed class BackendTests(EmulatorFixture emu)
{
    static GameProject Project(string name)
    {
        var p = SampleProjects.CreatePlatformerMini(Path.Combine(NativeBuild.RepoRoot, "build", "scripting-tests", name));
        foreach (var f in new[] { "PlayerController.cs", "Coin.cs" })
            File.Copy(Path.Combine(TestSupport.ScriptsDir, f), Path.Combine(p.ScriptsDir, f), true);
        var scenePath = Path.Combine(p.ScenesDir, "Forest_01.scene");
        var scene = GameProject.Load<SceneFile>(scenePath);
        scene.Actors[0].Components.Insert(0, new ComponentData { Type = "PlayerController", Fields = { ["lives"] = "4", ["speed"] = "2" } });
        scene.Actors[2].Components.Add(new ComponentData { Type = "Coin" });
        scene.Events.OnEnter = "Save.Checkpoint()";
        GameProject.Save(scenePath, scene);
        return p;
    }

    [Fact]
    public void GameBuilderCompilesScriptsThroughTheBackend()
    {
        var p = Project("backend_build");
        var backend = new ScriptBackend();
        var builder = new GameBuilder(p, new Toolchain()) { Scripts = backend };
        var r = builder.Build();
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        Assert.NotNull(backend.Last);
        Assert.True(backend.Last!.Success);
        Assert.Equal(["Coin", "PlayerController"], backend.Last.Behaviours.Select(b => b.Name).Order());
        var sceneCpp = File.ReadAllText(Path.Combine(builder.GenDir, "scenes", "scene_Forest_01.cpp"));
        Assert.Contains("#include \"agb_scripts.hpp\"", sceneCpp);
        Assert.Contains("agb_make_PlayerController_with(arena, agb_init_PlayerController{.speed = agb::fixed::fromRaw(512), .lives = 4})", sceneCpp);
        Assert.Contains("agb_make_Coin(arena)", sceneCpp);
        // Generated files sit in build/gen/scripts with the debugger map; ids are kept for the next build.
        var gen = Path.Combine(builder.GenDir, "scripts");
        Assert.True(File.Exists(Path.Combine(gen, "debug-map.json")));
        Assert.True(File.Exists(Path.Combine(gen, "type-ids.json")));
        Assert.Contains("agb_scene_Forest_01_onEnter", File.ReadAllText(Path.Combine(gen, "agb_scripts.hpp")));

        // Second build: nothing in the script output changes.
        var again = new GameBuilder(p, new Toolchain()) { Scripts = backend }.Build();
        Assert.True(again.Success, string.Join('\n', again.Diagnostics));
        Assert.Empty(backend.Last!.Changed);

        // Play: hold Right; the PlayerController walks into the coin, which its OnTouch destroys (+10 score).
        var core = emu.Core;
        core.LoadRom(r.RomPath);
        DebugChannel? ch = null;
        for (var f = 0; f < 240; f++)
        {
            core.Keys = f > 60 ? GbaKeys.Right : 0;
            core.RunFrame();
            ch ??= DebugChannel.Find(core);
        }
        Assert.NotNull(ch);
        var score = ReadScore(core, r.ElfPath);
        core.UnloadRom();
        Assert.Equal(10, score);
    }

    [Fact]
    public void ScriptErrorsStopTheBuildWithFileLineColumn()
    {
        var p = Project("backend_errors");
        File.WriteAllText(Path.Combine(p.ScriptsDir, "Broken.cs"), "using GBA;\n\npublic class Broken : Behaviour\n{\n    float t;\n    void Update() { Sound.Play(\"jmp\"); }\n}\n");
        var r = new GameBuilder(p, new Toolchain()) { Scripts = new ScriptBackend() }.Build();
        Assert.False(r.Success);
        var log = r.Diagnostics.Select(d => d.ToString()).ToList();
        Assert.Contains("Broken.cs(5,5): GAE002 float is slow on GBA → use fixed", log);
        Assert.Contains(log, l => l.StartsWith("Broken.cs(6,32): Sound \"jmp\" is not in the project.", StringComparison.Ordinal));
        Assert.All(r.Diagnostics.Where(d => d.File.Length > 0), d => Assert.True(Path.IsPathRooted(d.File)));
    }

    /// <summary>Reads agb_state.score through the address of agb_state in the linker map.</summary>
    static int ReadScore(LibretroCore core, string elf)
    {
        var map = File.ReadAllText(elf + ".map");
        var m = Regex.Match(map, @"0x([0-9a-fA-F]{8})\s+agb_state\b");
        Assert.True(m.Success, "agb_state is not in the map file");
        var address = uint.Parse(m.Groups[1].Value, NumberStyles.HexNumber);
        // GameState: SaveHeader (8 bytes), then the save fields largest first: score (int) comes first.
        return (int)core.ReadU32(address + 8);
    }
}
