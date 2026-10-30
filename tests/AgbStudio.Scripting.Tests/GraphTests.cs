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
}
