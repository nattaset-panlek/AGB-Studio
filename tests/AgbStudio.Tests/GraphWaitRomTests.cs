using AgbStudio.Build;
using AgbStudio.Build.Content;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;
using AgbStudio.Emu;
using AgbStudio.Scripting;

namespace AgbStudio.Tests;

/// <summary>
/// Phase 20 in the booted ROM: a graph chain that waits plays through every Choice option (P20.04), runs beside a chain
/// that only waits, and a save made while it waits in Say goes on from that Say when loaded (P20.06).
/// </summary>
[Collection("emulator")]
public sealed class GraphWaitRomTests(EmulatorFixture emu)
{
    static readonly string Root = Path.Combine(NativeBuild.RepoRoot, "build", "test-projects");

    static GameProject Project(string name, string graph, bool saveCutscenes, params string[] fields)
    {
        var p = SampleProjects.CreatePlatformerMini(Path.Combine(Root, name));
        SampleText.AddTo(p, "en");
        p.Settings.General.BootSplash = false;
        p.Settings.Ui.PauseMenu = false;
        p.Settings.Save.Fields.Clear();
        foreach (var f in fields) p.Settings.Save.Fields.Add(new SaveField { Name = f, Type = "int" });
        p.Settings.Save.Cutscenes = saveCutscenes;
        p.SaveSettings();
        var table = GameProject.Load<StringTable>(Path.Combine(p.TextDir, "strings.en.json"));
        foreach (var k in new[] { "talk.a", "talk.b", "talk.yes", "talk.no", "talk.maybe" }) table.Strings[k] = k;
        GameProject.Save(Path.Combine(p.TextDir, "strings.en.json"), table);
        File.WriteAllText(Path.Combine(p.ScriptsDir, "Talk.graph"), graph);
        var scenePath = Path.Combine(p.ScenesDir, "Forest_01.scene");
        var scene = GameProject.Load<SceneFile>(scenePath);
        scene.Links.Clear();
        scene.Music = new MusicSettings();
        scene.Actors.Single(a => a.Kind == ActorKind.Player).Components.Add(new ComponentData { Type = "Talk" });
        GameProject.Save(scenePath, scene);
        return p;
    }

    static string Build(GameProject p)
    {
        var r = new GameBuilder(p, new Toolchain()) { Scripts = new ScriptBackend() }.Build();
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        return r.RomPath;
    }

    static void Press(PlaySession s, GbaKeys key)
    {
        s.Keys = key;
        s.Step();
        s.Step();
        s.Keys = 0;
        for (var i = 0; i < 20; i++) s.Step();
    }

    static void Run(PlaySession s, int frames)
    {
        for (var i = 0; i < frames; i++) s.Step();
    }

    // Say, then a Choice of three; each option sets "picked". Beside it, a chain that only waits sets "beside".
    const string ChoiceGraph = """
        { "format": 1, "class": "Talk",
          "nodes": [
            { "id": "n1", "type": "OnStart", "x": 0, "y": 0 },
            { "id": "n2", "type": "Say", "x": 10, "y": 0, "props": { "key": "talk.a" } },
            { "id": "n3", "type": "Choice", "x": 20, "y": 0, "props": { "option1": "talk.yes", "option2": "talk.no", "option3": "talk.maybe" } },
            { "id": "n4", "type": "SetVariable", "x": 30, "y": 0, "props": { "variable": "picked", "value": 1 } },
            { "id": "n5", "type": "SetVariable", "x": 30, "y": 40, "props": { "variable": "picked", "value": 2 } },
            { "id": "n6", "type": "SetVariable", "x": 30, "y": 80, "props": { "variable": "picked", "value": 3 } },
            { "id": "n7", "type": "OnStart", "x": 0, "y": 200 },
            { "id": "n8", "type": "Wait", "x": 10, "y": 200, "props": { "frames": 30 } },
            { "id": "n9", "type": "SetVariable", "x": 20, "y": 200, "props": { "variable": "beside", "value": 1 } }
          ],
          "links": [
            { "from": "n1.out", "to": "n2.in" }, { "from": "n2.out", "to": "n3.in" },
            { "from": "n3.option1", "to": "n4.in" }, { "from": "n3.option2", "to": "n5.in" }, { "from": "n3.option3", "to": "n6.in" },
            { "from": "n7.out", "to": "n8.in" }, { "from": "n8.out", "to": "n9.in" }
          ] }
        """;

    [Fact]
    public void AGraphChainPlaysThroughEveryChoice_BesideAChainThatOnlyWaits()
    {
        var p = Project("graph_waits_choice", ChoiceGraph, false, "picked", "beside");
        var rom = Build(p);
        for (var option = 1; option <= 3; option++)
        {
            using var s = new PlaySession(emu.Core);
            s.Start(rom);
            for (var i = 0; i < 120 && Play.Actor(s, "Player") == null; i++) s.Step();
            Assert.NotNull(Play.Actor(s, "Player"));
            int Get(string field) => Play.ReadInt(s, p.Settings.Save, field);
            Run(s, 90);
            Assert.Equal(1, Get("beside"));  // the Wait chain ran out while the Say waits
            Assert.Equal(0, Get("picked"));
            Press(s, GbaKeys.A);             // closes the Say
            Run(s, 60);
            Assert.Equal(0, Get("picked"));  // the Choice waits
            for (var i = 1; i < option; i++) Press(s, GbaKeys.Down);
            Press(s, GbaKeys.A);
            Run(s, 30);
            Assert.Equal(option, Get("picked"));
        }
    }

    // Say a, step += 1, Say b, done = 1. Beside it: Wait 240 frames, then Save Game slot 1 (while Say b waits).
    const string SaveGraph = """
        { "format": 1, "class": "Talk",
          "nodes": [
            { "id": "n1", "type": "OnStart", "x": 0, "y": 0 },
            { "id": "n2", "type": "Say", "x": 10, "y": 0, "props": { "key": "talk.a" } },
            { "id": "n3", "type": "AddToVariable", "x": 20, "y": 0, "props": { "variable": "step", "amount": 1 } },
            { "id": "n4", "type": "Say", "x": 30, "y": 0, "props": { "key": "talk.b" } },
            { "id": "n5", "type": "SetVariable", "x": 40, "y": 0, "props": { "variable": "done", "value": 1 } },
            { "id": "n6", "type": "OnStart", "x": 0, "y": 200 },
            { "id": "n7", "type": "Wait", "x": 10, "y": 200, "props": { "frames": 240 } },
            { "id": "n8", "type": "SaveGame", "x": 20, "y": 200, "props": { "slot": 1 } }
          ],
          "links": [
            { "from": "n1.out", "to": "n2.in" }, { "from": "n2.out", "to": "n3.in" }, { "from": "n3.out", "to": "n4.in" },
            { "from": "n4.out", "to": "n5.in" }, { "from": "n6.out", "to": "n7.in" }, { "from": "n7.out", "to": "n8.in" }
          ] }
        """;

    [Fact]
    public void ASaveMadeWhileTheChainWaitsInSay_GoesOnFromThatSay()
    {
        var p = Project("graph_waits_save", SaveGraph, true, "step", "done");
        // The scene loads slot 1 when there is one and nothing has run yet.
        File.WriteAllText(Path.Combine(p.ScriptsDir, "Forest_01.cs"), """
            using GBA;

            public class Forest_01 : SceneScript
            {
                void OnEnter()
                {
                    if (Game.step != 0) return;
                    if (Save.Exists(1))
                    {
                        Save.Read(1);
                        Scene.Load("Forest_01");
                    }
                }
            }
            """);
        var rom = Build(p);

        byte[] chip;
        using (var s = new PlaySession(emu.Core))
        {
            s.Start(rom);
            for (var i = 0; i < 120 && Play.Actor(s, "Player") == null; i++) s.Step();
            int Get(string field) => Play.ReadInt(s, p.Settings.Save, field);
            Run(s, 60);
            Press(s, GbaKeys.A);  // closes Say a: step = 1, Say b waits
            Assert.Equal(1, Get("step"));
            Run(s, 300);          // the Wait chain saves while Say b waits
            Assert.Equal(0, Get("done"));
            chip = s.Core.SaveRam.ToArray();
        }

        using (var s = new PlaySession(emu.Core))
        {
            s.Start(rom);
            chip.CopyTo(s.Core.SaveRam);  // before the first frame, as a cartridge would have it
            for (var i = 0; i < 120 && Play.Actor(s, "Player") == null; i++) s.Step();
            int Get(string field) => Play.ReadInt(s, p.Settings.Save, field);
            for (var i = 0; i < 240 && Get("step") != 1; i++) s.Step();
            Assert.Equal(1, Get("step"));
            Run(s, 60);
            Press(s, GbaKeys.A);  // closes Say b, shown again from the save
            Run(s, 30);
            Assert.Equal(1, Get("done"));
            Assert.Equal(1, Get("step"));  // Say a did not run again: the chain went on from Say b
        }
    }
}
