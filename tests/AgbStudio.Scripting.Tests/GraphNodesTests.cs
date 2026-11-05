using AgbStudio.Scripting.Graph;

namespace AgbStudio.Scripting.Tests;

/// <summary>P19.01 to P19.04: the nodes that call the C# API directly generate C#, compile, and turn back into the graph.</summary>
public sealed class GraphNodesTests
{
    const string Graph = """
        { "format": 1, "class": "NewNodes",
          "nodes": [
            { "id": "n1", "type": "OnStart", "x": 0, "y": 0 },
            { "id": "n2", "type": "CameraFollow", "x": 10, "y": 0 },
            { "id": "n3", "type": "SetVariable", "x": 20, "y": 0, "props": { "variable": "score" } },
            { "id": "n4", "type": "RandomRange", "x": 20, "y": 40, "props": { "lo": -2, "hi": 5 } },
            { "id": "n5", "type": "Branch", "x": 30, "y": 0 },
            { "id": "n6", "type": "SaveExists", "x": 30, "y": 40, "props": { "slot": 1 } },
            { "id": "n7", "type": "LoadGame", "x": 40, "y": 0, "props": { "slot": 1 } },
            { "id": "n8", "type": "SetTile", "x": 50, "y": 0, "props": { "layer": 2, "x": 3, "y": 4, "tile": 5 } }
          ],
          "links": [
            { "from": "n1.out", "to": "n2.in" }, { "from": "n2.out", "to": "n3.in" }, { "from": "n4.value", "to": "n3.value" },
            { "from": "n3.out", "to": "n5.in" }, { "from": "n6.value", "to": "n5.condition" },
            { "from": "n5.true", "to": "n7.in" }, { "from": "n7.out", "to": "n8.in" }
          ] }
        """;

    [Fact]
    public void NewNodes_GenerateTheApiCalls()
    {
        var code = GraphCodeGen.Generate(LogicGraph.Parse(Graph), "NewNodes");
        foreach (var line in new[]
        {
            "Camera.Follow(actor);", "Game.score = Random.Range(-2, 5);", "if (Save.Exists(1))", "Save.Read(1);", "Scene.SetTile(2, 3, 4, 5);",
        })
            Assert.Contains(line, code);
    }

    [Fact]
    public void NewNodes_TurnBackIntoTheSameGraph()
    {
        var g = LogicGraph.Parse(Graph);
        var code = GraphCodeGen.Generate(g, "NewNodes");
        Assert.Equal(g.ToJson(), GraphFromCode.Parse(code, g).ToJson());
        var fresh = GraphFromCode.Parse(code);
        Assert.Equal(g.Nodes.Select(n => n.Type).Order(), fresh.Nodes.Select(n => n.Type).Order());
        Assert.Equal(code, GraphCodeGen.Generate(fresh, "NewNodes"));
    }

    [Fact]
    public void NewNodes_KeepTheirNumbersTypedInTheFile()
    {
        var json = LogicGraph.Parse(Graph).ToJson();
        Assert.Contains("\"lo\": -2", json);
        Assert.Contains("\"tile\": 5", json);
    }

    [Fact]
    public void NewNodes_CompileAndTranslate()
    {
        var r = TestSupport.Compile("graph-new-nodes", TestSupport.Context(), TestSupport.Inline("NewNodes.graph", LogicGraph.Parse(Graph).ToJson()));
        Assert.Empty(r.Diagnostics);
        var cpp = File.ReadAllText(Path.Combine(r.IncludeDir, "NewNodes.cpp"));
        Assert.Contains("agb::api::Camera::Follow(", cpp);
        Assert.Contains("agb::api::Random::Range(-2, 5)", cpp);
        Assert.Contains("agb::api::Save::Exists(1)", cpp);
        Assert.Contains("agb::api::Save::Read(1)", cpp);
        Assert.Contains("agb::scene::setTile(2, 3, 4,", cpp);
        Assert.Equal("", TestSupport.CompileCpp(r));
    }

    [Fact]
    public void FollowingTheTouchingActor_WiresTheTarget()
    {
        var g = LogicGraph.Parse("""
            { "format": 1, "class": "Touch",
              "nodes": [ { "id": "n1", "type": "OnTouch", "x": 0, "y": 0 }, { "id": "n2", "type": "CameraFollow", "x": 10, "y": 0 } ],
              "links": [ { "from": "n1.out", "to": "n2.in" }, { "from": "n1.other", "to": "n2.target" } ] }
            """);
        var code = GraphCodeGen.Generate(g, "Touch");
        Assert.Contains("Camera.Follow(other);", code);
        Assert.Equal(g.Links.Count, GraphFromCode.Parse(code).Links.Count);
    }
}
