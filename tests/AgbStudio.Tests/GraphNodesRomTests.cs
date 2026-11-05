using AgbStudio.Build;
using AgbStudio.Build.Content;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core;
using AgbStudio.Core.Project;
using AgbStudio.Emu;
using AgbStudio.Scripting;

namespace AgbStudio.Tests;

/// <summary>Phase 19: a graph using every new node runs in the booted ROM. Camera Follow is only run here: no test reads the camera.</summary>
[Collection("emulator")]
public sealed class GraphNodesRomTests(EmulatorFixture emu)
{
    const string Graph = """
        { "format": 1, "class": "NodeGraph",
          "nodes": [
            { "id": "n1", "type": "OnStart", "x": 0, "y": 0 },
            { "id": "n2", "type": "CameraFollow", "x": 10, "y": 0 },
            { "id": "n3", "type": "SetVariable", "x": 20, "y": 0, "props": { "variable": "picked" } },
            { "id": "n4", "type": "RandomRange", "x": 20, "y": 40, "props": { "lo": 3, "hi": 5 } },
            { "id": "n5", "type": "SaveGame", "x": 30, "y": 0, "props": { "slot": 0 } },
            { "id": "n6", "type": "SetVariable", "x": 40, "y": 0, "props": { "variable": "picked", "value": 99 } },
            { "id": "n7", "type": "Branch", "x": 50, "y": 0 },
            { "id": "n8", "type": "SaveExists", "x": 50, "y": 40, "props": { "slot": 0 } },
            { "id": "n9", "type": "LoadGame", "x": 60, "y": 0, "props": { "slot": 0 } },
            { "id": "n10", "type": "SetVariable", "x": 70, "y": 0, "props": { "variable": "found", "value": 1 } },
            { "id": "n11", "type": "SetTile", "x": 80, "y": 0, "props": { "layer": 0, "x": 2, "y": 1, "tile": TILE } }
          ],
          "links": [
            { "from": "n1.out", "to": "n2.in" }, { "from": "n2.out", "to": "n3.in" }, { "from": "n4.value", "to": "n3.value" },
            { "from": "n3.out", "to": "n5.in" }, { "from": "n5.out", "to": "n6.in" }, { "from": "n6.out", "to": "n7.in" },
            { "from": "n8.value", "to": "n7.condition" }, { "from": "n7.true", "to": "n9.in" }, { "from": "n9.out", "to": "n10.in" },
            { "from": "n10.out", "to": "n11.in" }
          ] }
        """;

    [Fact]
    public void AGraphUsingEveryNewNodeRunsInTheRom()
    {
        var dir = Path.Combine(NativeBuild.RepoRoot, "build", "test-projects", "graph_nodes");
        var p = SampleProjects.CreatePlatformerMini(dir);
        foreach (var f in new[] { "picked", "found", "seen" }) p.Settings.Save.Fields.Add(new SaveField { Name = f, Type = "int" });
        p.SaveSettings();

        var tile = PixelArt.TStone;
        File.WriteAllText(Path.Combine(p.ScriptsDir, "NodeGraph.graph"), Graph.Replace("TILE", tile.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        File.WriteAllText(Path.Combine(p.ScriptsDir, "TileProbe.cs"), "using GBA;\n\npublic class TileProbe : Behaviour\n{\n    void Update() => Game.seen = Scene.GetTile(0, 2, 1);\n}\n");
        var scenePath = Path.Combine(p.ScenesDir, "Forest_01.scene");
        var scene = GameProject.Load<SceneFile>(scenePath);
        var player = scene.Actors.Single(a => a.Kind == ActorKind.Player);
        player.Components.Add(new ComponentData { Type = "NodeGraph" });
        player.Components.Add(new ComponentData { Type = "TileProbe" });
        GameProject.Save(scenePath, scene);

        var r = new GameBuilder(p, new Toolchain()) { Scripts = new ScriptBackend() }.Build();
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));

        using var s = new PlaySession(emu.Core);
        s.Start(r.RomPath);
        for (var i = 0; i < 120 && Play.Actor(s, "Player") == null; i++) s.Step();
        Assert.NotNull(Play.Actor(s, "Player"));
        Play.Hold(s, 0, 10);

        int Get(string field) => Play.ReadInt(s, p.Settings.Save, field);
        // Load Game undid the 99: what comes back is what Random Range picked before the save
        Assert.InRange(Get("picked"), 3, 5);
        Assert.Equal(1, Get("found"));
        Assert.Equal(tile, Get("seen"));
    }
}
