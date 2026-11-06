using System.Text.RegularExpressions;
using AgbStudio.Scripting.Graph;

namespace AgbStudio.Scripting.Tests;

/// <summary>P6.16 / P6.17: Logic Graph -> C# (the Figma "Generated C#" panel) and C# -> Graph for generated files (D-11).</summary>
public sealed class GraphTests
{
    static LogicGraph Figma() => LogicGraph.Parse(File.ReadAllText(Path.Combine(TestSupport.ScriptsDir, "PlayerLogic.graph")));

    static string Squash(string s) => Regex.Replace(s, @"\s+", "");

    [Fact]
    public void FigmaGraphGeneratesTheFigmaCode()
    {
        var code = GraphCodeGen.Generate(Figma(), "PlayerLogic");
        // Text of the Figma "Generated C#" panel.
        const string update = "void Update() { if (Input.Pressed(Key.A) && body.OnGround) { body.vy = -3.50; Sound.Play(\"jump\", 0.8); } }";
        const string onTouch = "void OnTouch(Actor other) { if (other.tag == \"Coin\") { Game.score += 10; other.Destroy(); } }";
        Assert.Contains(Squash(update), Squash(code));
        Assert.Contains(Squash(onTouch), Squash(code));
        TestSupport.AssertGolden("PlayerLogic.cs", code);
        Assert.True(GraphCodeGen.IsGenerated(code));
    }

    [Fact]
    public void GeneratedCodeCompilesAndTranslates()
    {
        var r = TestSupport.Compile("graph", TestSupport.Context(), TestSupport.Script("PlayerLogic.graph"));
        Assert.Empty(r.Diagnostics);
        var cpp = File.ReadAllText(Path.Combine(r.IncludeDir, "PlayerLogic.cpp"));
        Assert.Contains("body->vy = agb::fixed::lit(-3.5);", cpp);
        Assert.Contains("agb::api::Sound::Play(agb_sound::SFX_JUMP, agb::fixed::lit(0.8));", cpp);
        Assert.Contains("if (other.tag == agb_tag::Coin) {", cpp);
        Assert.Contains("#line 14 \"Scripts/PlayerLogic.cs\"", cpp);
        Assert.Equal("", TestSupport.CompileCpp(r));
    }

    [Fact]
    public void RoundTripKeepsTheGraph()
    {
        var g = Figma();
        var code = GraphCodeGen.Generate(g, "PlayerLogic");
        // With the previous graph: identical file (ids, positions, "jump.wav", "-3.50").
        var back = GraphFromCode.Parse(code, g);
        Assert.Equal(g.ToJson(), back.ToJson());
        // Without it: same nodes and links up to ids and layout, and the same C#.
        var fresh = GraphFromCode.Parse(code);
        Assert.Equal(g.Nodes.Select(n => n.Type).OrderBy(t => t), fresh.Nodes.Select(n => n.Type).OrderBy(t => t));
        Assert.Equal(g.Links.Count, fresh.Links.Count);
        Assert.Equal(code, GraphCodeGen.Generate(fresh, "PlayerLogic"));
    }

    [Fact]
    public void EditedGeneratedCodeUpdatesTheGraph()
    {
        var g = Figma();
        var code = GraphCodeGen.Generate(g, "PlayerLogic").Replace("body.vy = -3.50;", "body.vy = -4.25;").Replace("Game.score += 10;", "Game.score += 10;\n            Save.Checkpoint();");
        var back = GraphFromCode.Parse(code, g);
        Assert.Equal("-4.25", back.Nodes.Single(n => n.Type == "SetVelocityY").Props["value"]);
        Assert.Single(back.Nodes, n => n.Type == "Checkpoint");
        Assert.Equal(code, GraphCodeGen.Generate(back, "PlayerLogic"));
    }

    [Fact]
    public void OnlyGraphGeneratedFilesConvertBack()
    {
        var src = File.ReadAllText(Path.Combine(TestSupport.ScriptsDir, "PlayerController.cs"));
        Assert.Throws<FormatException>(() => GraphFromCode.Parse(src));
    }

    [Fact]
    public void JsonKeepsTypedNumbers()
    {
        var json = File.ReadAllText(Path.Combine(TestSupport.ScriptsDir, "PlayerLogic.graph"));
        var g = LogicGraph.Parse(json);
        Assert.Equal("-3.50", g.Node("n6")!.Props["value"]);
        Assert.Contains("\"value\": -3.50", g.ToJson());
        Assert.Equal(g.ToJson(), LogicGraph.Parse(g.ToJson()).ToJson());
    }

    // P17.01: every property that names something in the project, with values that look like numbers to catch a typed write.
    const string ReferenceGraph = """
        { "format": 1, "class": "Refs",
          "nodes": [
            { "id": "n1", "type": "OnTouch", "x": 0, "y": 0, "props": { "tag": "Coin" } },
            { "id": "n2", "type": "CompareTag", "x": 10, "y": 0, "props": { "tag": "Player" } },
            { "id": "n3", "type": "PlaySound", "x": 20, "y": 0, "props": { "sound": "jump", "volume": 1 } },
            { "id": "n4", "type": "PlayMusic", "x": 30, "y": 0, "props": { "music": "town" } },
            { "id": "n5", "type": "SetVariable", "x": 40, "y": 0, "props": { "variable": "coins", "value": 0 } },
            { "id": "n6", "type": "GetVariable", "x": 40, "y": 40, "props": { "variable": "lives" } },
            { "id": "n7", "type": "AddToVariable", "x": 50, "y": 0, "props": { "variable": "score", "amount": 2 } },
            { "id": "n8", "type": "ShowText", "x": 60, "y": 0, "props": { "key": "intro.hello" } },
            { "id": "n9", "type": "LoadScene", "x": 70, "y": 0, "props": { "scene": "2" } }
          ],
          "links": [
            { "from": "n1.out", "to": "n2.in" }, { "from": "n1.other", "to": "n2.actor" }, { "from": "n2.match", "to": "n3.in" },
            { "from": "n3.out", "to": "n4.in" }, { "from": "n4.out", "to": "n5.in" }, { "from": "n6.value", "to": "n5.value" },
            { "from": "n5.out", "to": "n7.in" }, { "from": "n7.out", "to": "n8.in" }, { "from": "n8.out", "to": "n9.in" }
          ] }
        """;

    [Fact]
    public void ReferenceProps_KeepTheFileFormatAndTheGeneratedCode()
    {
        var g = LogicGraph.Parse(ReferenceGraph);
        var json = g.ToJson();
        Assert.Contains("\"format\": 1", json);
        Assert.Contains("\"scene\": \"2\"", json);
        Assert.Contains("\"sound\": \"jump\"", json);
        Assert.Contains("\"variable\": \"coins\"", json);
        Assert.Contains("\"key\": \"intro.hello\"", json);
        Assert.Equal(json, LogicGraph.Parse(json).ToJson());

        var code = GraphCodeGen.Generate(g, "Refs");
        foreach (var line in new[]
        {
            "if (other.tag != \"Coin\") return;", "if (other.tag == \"Player\")", "Sound.Play(\"jump\");", "Music.Play(\"town\");",
            "Game.coins = Game.lives;", "Game.score += 2;", "Text.Show(\"intro.hello\");", "Scene.Load(\"2\");",
        })
            Assert.Contains(line, code);
        Assert.Equal(json, GraphFromCode.Parse(code, g).ToJson());
    }

    [Theory]
    [InlineData("OnTouch", "tag", PropKind.Tag)]
    [InlineData("CompareTag", "tag", PropKind.Tag)]
    [InlineData("PlaySound", "sound", PropKind.Sound)]
    [InlineData("PlayMusic", "music", PropKind.Music)]
    [InlineData("GetVariable", "variable", PropKind.Variable)]
    [InlineData("SetVariable", "variable", PropKind.Variable)]
    [InlineData("AddToVariable", "variable", PropKind.Variable)]
    [InlineData("ShowText", "key", PropKind.TextKey)]
    [InlineData("LoadScene", "scene", PropKind.Scene)]
    [InlineData("DebugLog", "text", PropKind.Text)]
    public void ReferenceProps_HaveTheirKindInTheCatalog(string node, string prop, PropKind kind) =>
        Assert.Equal(kind, NodeCatalog.Find(node)!.Props.Single(p => p.Name == prop).Kind);

    [Fact]
    public void CatalogCoversEveryFigmaCategory()
    {
        Assert.Equal(["Events", "Input", "Physics", "Flow", "Math", "Actor", "Sound", "Data & Save", "Text & UI", "Other"], NodeCatalog.Categories);
        foreach (var c in NodeCatalog.Categories) Assert.Contains(NodeCatalog.All, n => n.Category == c);
        foreach (var t in new[] { "OnUpdate", "ButtonPressed", "IsOnGround", "And", "Branch", "SetVelocityY", "PlaySound", "OnTouch", "CompareTag", "AddToVariable", "Destroy" })
            Assert.NotNull(NodeCatalog.Find(t));
        Assert.All(NodeCatalog.All.SelectMany(n => n.Pins), p => Assert.True(Enum.IsDefined(p.Kind)));
    }

    [Fact]
    public void EveryActionAndValueNodeGeneratesCompilableCode()
    {
        // One graph that chains every action node after On Update, with value nodes feeding a Branch.
        var g = new LogicGraph { Class = "AllNodes" };
        GraphNode Add(string type, params (string, string)[] props)
        {
            var n = new GraphNode { Id = "n" + (g.Nodes.Count + 1), Type = type };
            foreach (var (k, v) in props) n.Props[k] = v;
            g.Nodes.Add(n);
            return n;
        }
        void Link(GraphNode a, string pa, GraphNode b, string pb) => g.Links.Add(new GraphLink(a.Id + "." + pa, b.Id + "." + pb));
        var ev = Add("OnUpdate");
        var br = Add("Branch");
        Link(ev, "out", br, "in");
        var or = Add("Or");
        var not = Add("Not");
        var cmp = Add("Compare", ("op", ">="));
        var add = Add("Add");
        var mul = Add("Multiply", ("b", "2"));
        Link(Add("AxisX"), "value", add, "a");
        Link(Add("VelocityX"), "value", add, "b");
        Link(add, "value", mul, "a");
        Link(mul, "value", cmp, "a");
        Link(Add("GetVariable", ("variable", "score")), "value", cmp, "b");
        Link(cmp, "value", or, "a");
        Link(Add("AnyKey"), "value", not, "a");
        Link(not, "value", or, "b");
        Link(or, "value", br, "condition");
        GraphNode prev = br;
        var pin = "true";
        foreach (var (type, props) in new (string, (string, string)[])[]
        {
            ("SetVelocityX", [("value", "1.25")]), ("Jump", []), ("PlayMusic", [("music", "help.xm")]), ("StopMusic", []),
            ("SetVariable", [("variable", "coins"), ("value", "3")]), ("SaveGame", [("slot", "1")]), ("Checkpoint", []),
            ("ShowText", [("key", "forest.help_me")]), ("DebugLog", [("text", "hi")]), ("LoadScene", [("scene", "Cave_01")]),
            ("ShakeCamera", [("amplitude", "3"), ("frames", "12")]), ("PlaySound", [("sound", "coin.wav")]),
        })
        {
            var n = Add(type, props);
            Link(prev, pin, n, "in");
            prev = n;
            pin = "out";
        }
        var r = TestSupport.Compile("graph-all", TestSupport.Context(), TestSupport.Inline("AllNodes.graph", g.ToJson()));
        Assert.Empty(r.Diagnostics);
        Assert.Equal("", TestSupport.CompileCpp(r));
        var code = r.GeneratedCSharp.Single().Value;
        Assert.Contains("if ((Input.AxisX + body.vx) * 2 >= Game.score || !Input.AnyKey)", code);
        var back = GraphFromCode.Parse(code);
        Assert.Equal(code, GraphCodeGen.Generate(back, "AllNodes"));
    }

    [Fact]
    public void TagButtonsTargetsAndVelocityNodesGenerateCompileAndRoundTrip()
    {
        // Figma T-P17 / T-O08: On Touch with a Tag, On Button, Target inputs, Set Velocity Y modes, Set Velocity, Add Force.
        var g = new LogicGraph { Class = "PhysicsLogic" };
        GraphNode Add(string type, params (string, string)[] props)
        {
            var n = new GraphNode { Id = "n" + (g.Nodes.Count + 1), Type = type };
            foreach (var (k, v) in props) n.Props[k] = v;
            g.Nodes.Add(n);
            return n;
        }
        void Link(GraphNode a, string pa, GraphNode b, string pb) => g.Links.Add(new GraphLink(a.Id + "." + pa, b.Id + "." + pb));
        var touch = Add("OnTouch", ("tag", "Spring"));
        var up = Add("SetVelocityY", ("value", "-1.50"), ("mode", "Add"));
        Link(touch, "out", up, "in");
        Link(touch, "other", up, "target");
        var push = Add("AddForce", ("x", "1"), ("y", "0"));
        Link(up, "out", push, "in");
        var stop = Add("SetVelocity", ("x", "0"), ("y", "-2"));
        Link(push, "out", stop, "in");
        Link(touch, "other", stop, "target");
        var button = Add("OnButton", ("button", "B"));
        var clamp = Add("SetVelocityY", ("value", "2"), ("mode", "Clamp"));
        Link(button, "out", clamp, "in");
        var update = Add("OnUpdate");
        var br = Add("Branch");
        Link(update, "out", br, "in");
        var ground = Add("IsOnGround");
        Link(ground, "value", br, "condition");
        var log = Add("DebugLog", ("text", "landed"));
        Link(br, "true", log, "in");

        var code = GraphCodeGen.Generate(g, "PhysicsLogic");
        Assert.Contains(Squash("void OnTouch(Actor other) { if (other.tag != \"Spring\") return; other.Get<PlatformerBody>().vy += -1.50; body.AddForce(1, 0); other.Get<PlatformerBody>().SetVelocity(0, -2); }"), Squash(code));
        Assert.Contains(Squash("if (Input.Pressed(Key.B)) OnButtonB();"), Squash(code));
        Assert.Contains(Squash("void OnButtonB() { body.vy = fixed.Min(body.vy, 2); }"), Squash(code));
        Assert.Contains(Squash("void Update() { if (body.OnGround) { Debug.Log(\"landed\"); }"), Squash(code));

        // C# -> graph -> C#: the same nodes and links, and the same code.
        var back = GraphFromCode.Parse(code);
        Assert.Equal(g.Nodes.Select(n => n.Type).Order(), back.Nodes.Select(n => n.Type).Order());
        Assert.Equal("Spring", back.Nodes.Single(n => n.Type == "OnTouch").Props["tag"]);
        Assert.Equal(["Add", "Clamp"], back.Nodes.Where(n => n.Type == "SetVelocityY").Select(n => n.Props["mode"]).Order());
        Assert.Equal(code, GraphCodeGen.Generate(back, "PhysicsLogic"));

        // Get Velocity and Vector from Velocity are in the Add Node list (Figma T-O08).
        Assert.Equal(PinKind.Vector, NodeCatalog.Find("GetVelocity")!.Pin("value")!.Kind);
        Assert.Equal("Math", NodeCatalog.Find("VectorFromVelocity")!.Category);
    }

    [Fact]
    public void TargetNodesCompileToTheRuntime()
    {
        var g = new LogicGraph { Class = "Springy" };
        g.Nodes.Add(new GraphNode { Id = "n1", Type = "OnTouch", Props = { ["tag"] = "Player" } });
        g.Nodes.Add(new GraphNode { Id = "n2", Type = "SetVelocity", Props = { ["x"] = "0", ["y"] = "-4" } });
        g.Nodes.Add(new GraphNode { Id = "n3", Type = "AddForce", Props = { ["x"] = "1", ["y"] = "0" } });
        g.Nodes.Add(new GraphNode { Id = "n4", Type = "OnButton", Props = { ["button"] = "A" } });
        g.Nodes.Add(new GraphNode { Id = "n5", Type = "SetVelocityY", Props = { ["value"] = "3", ["mode"] = "Clamp" } });
        g.Links.Add(new GraphLink("n1.out", "n2.in"));
        g.Links.Add(new GraphLink("n1.other", "n2.target"));
        g.Links.Add(new GraphLink("n2.out", "n3.in"));
        g.Links.Add(new GraphLink("n4.out", "n5.in"));
        var r = TestSupport.Compile("graph-targets", TestSupport.Context(), TestSupport.Inline("Springy.graph", g.ToJson()));
        Assert.Empty(r.Diagnostics);
        Assert.Equal("", TestSupport.CompileCpp(r));
    }
}
