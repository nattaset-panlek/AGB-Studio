using AgbStudio.Scripting.Bytecode;
using AgbStudio.Scripting.Graph;

namespace AgbStudio.Scripting.Tests;

/// <summary>
/// P20.03 and P20.04: a graph chain that waits becomes a [Cutscene] method from its first waiting node
/// (docs/design/P20-graph-chains-that-wait.md), compiles to bytecode, and turns back into the same graph.
/// </summary>
public sealed class GraphWaitTests
{
    // On Touch (Player): Play Sound, Say, Choice (yes: Add to Variable, Say; no: Fade Out, Load Scene).
    // On Start: Wait, Shake Camera (no Say / Choice / Move: runs beside the game).
    // On Button A: Branch on Button Held B; true: Fade In, Move the player; false: Debug Log (stays in OnButtonA).
    internal const string Graph = """
        { "format": 1, "class": "Talker",
          "nodes": [
            { "id": "n1", "type": "OnTouch", "x": 0, "y": 0, "props": { "tag": "Player" } },
            { "id": "n2", "type": "PlaySound", "x": 10, "y": 0, "props": { "sound": "coin" } },
            { "id": "n3", "type": "Say", "x": 20, "y": 0, "props": { "key": "forest.help_me" } },
            { "id": "n4", "type": "Choice", "x": 30, "y": 0, "props": { "option1": "forest.yes", "option2": "forest.no" } },
            { "id": "n5", "type": "AddToVariable", "x": 40, "y": 0, "props": { "variable": "coins", "amount": -5 } },
            { "id": "n6", "type": "Say", "x": 50, "y": 0, "props": { "key": "forest.help_me" } },
            { "id": "n7", "type": "FadeOut", "x": 40, "y": 40, "props": { "seconds": 0.5 } },
            { "id": "n8", "type": "LoadScene", "x": 50, "y": 40, "props": { "scene": "Cave_01" } },
            { "id": "n9", "type": "OnStart", "x": 0, "y": 100 },
            { "id": "n10", "type": "Wait", "x": 10, "y": 100, "props": { "frames": 30 } },
            { "id": "n11", "type": "ShakeCamera", "x": 20, "y": 100, "props": { "amplitude": 2, "frames": 10 } },
            { "id": "n12", "type": "OnButton", "x": 0, "y": 200, "props": { "button": "A" } },
            { "id": "n13", "type": "Branch", "x": 10, "y": 200 },
            { "id": "n14", "type": "ButtonHeld", "x": 0, "y": 240, "props": { "button": "B" } },
            { "id": "n15", "type": "FadeIn", "x": 20, "y": 200, "props": { "seconds": 1 } },
            { "id": "n16", "type": "Move", "x": 30, "y": 200, "props": { "actor": "Player", "path": "EnterScene" } },
            { "id": "n17", "type": "DebugLog", "x": 20, "y": 240, "props": { "text": "not held" } }
          ],
          "links": [
            { "from": "n1.out", "to": "n2.in" }, { "from": "n2.out", "to": "n3.in" }, { "from": "n3.out", "to": "n4.in" },
            { "from": "n4.option1", "to": "n5.in" }, { "from": "n5.out", "to": "n6.in" },
            { "from": "n4.option2", "to": "n7.in" }, { "from": "n7.out", "to": "n8.in" },
            { "from": "n9.out", "to": "n10.in" }, { "from": "n10.out", "to": "n11.in" },
            { "from": "n12.out", "to": "n13.in" }, { "from": "n14.value", "to": "n13.condition" },
            { "from": "n13.true", "to": "n15.in" }, { "from": "n15.out", "to": "n16.in" }, { "from": "n13.false", "to": "n17.in" }
          ] }
        """;

    const string Expected = """
        using GBA;
        using System.Threading.Tasks;

        public class Talker : Behaviour
        {
            void Start()
            {
                StartWaits();
            }

            [Cutscene(Background = true, Once = true)]
            async Task StartWaits()
            {
                await Wait(30);
                Camera.Shake(2, 10);
            }

            void Update()
            {
                if (Input.Pressed(Key.A)) OnButtonA();
            }

            void OnTouch(Actor other)
            {
                if (other.tag != "Player") return;
                Sound.Play("coin");
                OnTouchWaits();
            }

            [Cutscene(Once = true)]
            async Task OnTouchWaits()
            {
                await Say("forest.help_me");
                var choice1 = await Choice("forest.yes", "forest.no");
                if (choice1 == 0)
                {
                    Game.coins += -5;
                    await Say("forest.help_me");
                }
                else if (choice1 == 1)
                {
                    await FadeOut(0.5);
                    Scene.Load("Cave_01");
                }
            }

            void OnButtonA()
            {
                if (Input.Held(Key.B))
                {
                    OnButtonAWaits();
                }
                else
                {
                    Debug.Log("not held");
                }
            }

            [Cutscene(Once = true)]
            async Task OnButtonAWaits()
            {
                await FadeIn(1);
                await Move(Player, "EnterScene");
            }
        }

        """;

    static string Body(string code) => code[(code.IndexOf('\n') + 1)..].Replace("\r\n", "\n");

    [Fact]
    public void WaitingChains_BecomeCutscenesFromTheirFirstWait()
    {
        var code = GraphCodeGen.Generate(LogicGraph.Parse(Graph), "Talker");
        Assert.Equal(Expected.Replace("\r\n", "\n"), Body(code));
    }

    [Fact]
    public void WaitingChains_TurnBackIntoTheSameGraph()
    {
        var g = LogicGraph.Parse(Graph);
        var code = GraphCodeGen.Generate(g, "Talker");
        Assert.Equal(g.ToJson(), GraphFromCode.Parse(code, g).ToJson());
        var fresh = GraphFromCode.Parse(code);
        Assert.Equal(g.Nodes.Select(n => n.Type).Order(), fresh.Nodes.Select(n => n.Type).Order());
        Assert.Equal(g.Links.Count, fresh.Links.Count);
        Assert.Equal(code, GraphCodeGen.Generate(fresh, "Talker"));
    }

    [Fact]
    public void WaitingChains_CompileToBytecodeAndTranslate()
    {
        var r = TestSupport.Compile("graph-waits", TestSupport.Context(), TestSupport.Inline("Talker.graph", LogicGraph.Parse(Graph).ToJson()));
        Assert.True(r.Success, TestSupport.Errors(r));
        Assert.Empty(r.Diagnostics);
        Assert.Equal(["OnButtonAWaits", "OnTouchWaits", "StartWaits"], r.Scripts.Select(s => s.Name.Split('.', '_').Last()).Order());
        var cpp = File.ReadAllText(Path.Combine(r.IncludeDir, "Talker.cpp"));
        Assert.Contains("agb_start_script_as(agb::script::Priority::Trigger, actor->index, agb_cutscene::Talker_StartWaits, actor->index)", cpp);
        Assert.Contains("agb_start_script_as(agb::script::Priority::Cutscene, actor->index, agb_cutscene::Talker_OnTouchWaits, actor->index)", cpp);
        var listing = OpcodeTable.Load().Disassemble(r.Scripts.Single(s => s.Name.EndsWith("OnTouchWaits")).Code);
        Assert.Contains("WaitFrames", OpcodeTable.Load().Disassemble(r.Scripts.Single(s => s.Name.EndsWith("StartWaits")).Code));
        Assert.Contains("Native", listing);
        Assert.Equal("", TestSupport.CompileCpp(r));
    }

    [Fact]
    public void Choice_HidesEmptyOptionsAndSkipsUnconnectedOnes()
    {
        var g = LogicGraph.Parse("""
            { "format": 1, "class": "Ask",
              "nodes": [
                { "id": "n1", "type": "OnStart", "x": 0, "y": 0 },
                { "id": "n2", "type": "Choice", "x": 10, "y": 0, "props": { "option1": "forest.yes", "option3": "forest.no" } },
                { "id": "n3", "type": "Choice", "x": 20, "y": 0, "props": { "option1": "forest.yes", "option2": "forest.no" } },
                { "id": "n4", "type": "Wait", "x": 30, "y": 0, "props": { "frames": 5 } }
              ],
              "links": [ { "from": "n1.out", "to": "n2.in" }, { "from": "n2.option3", "to": "n3.in" }, { "from": "n3.option2", "to": "n4.in" } ] }
            """);
        var code = GraphCodeGen.Generate(g, "Ask");
        Assert.Contains("var choice1 = await Choice(\"forest.yes\", null, \"forest.no\");", code);
        Assert.Contains("if (choice1 == 2)", code);
        Assert.Contains("var choice2 = await Choice(\"forest.yes\", \"forest.no\");", code);
        Assert.Contains("if (choice2 == 1)", code);
        Assert.DoesNotContain("choice1 == 0", code);
        Assert.Equal(g.ToJson(), GraphFromCode.Parse(code, g).ToJson());
    }

    // P20.05 ------------------------------------------------------------------------------------------------

    const string Misplaced = """
        { "format": 1, "class": "Bad",
          "nodes": [
            { "id": "n1", "type": "OnUpdate", "x": 0, "y": 0 },
            { "id": "n2", "type": "Wait", "x": 10, "y": 0, "props": { "frames": 5 } },
            { "id": "n3", "type": "OnTouch", "x": 0, "y": 100 },
            { "id": "n4", "type": "Say", "x": 10, "y": 100, "props": { "key": "forest.help_me" } },
            { "id": "n5", "type": "Destroy", "x": 20, "y": 100 },
            { "id": "n6", "type": "Branch", "x": 30, "y": 100 },
            { "id": "n7", "type": "Not", "x": 30, "y": 140 },
            { "id": "n8", "type": "ButtonHeld", "x": 20, "y": 140, "props": { "button": "B" } },
            { "id": "n9", "type": "OnStart", "x": 0, "y": 200 },
            { "id": "n10", "type": "SetVelocityX", "x": 10, "y": 200, "props": { "value": 1 } },
            { "id": "n11", "type": "Choice", "x": 20, "y": 200 }
          ],
          "links": [
            { "from": "n1.out", "to": "n2.in" },
            { "from": "n3.out", "to": "n4.in" }, { "from": "n4.out", "to": "n5.in" }, { "from": "n3.other", "to": "n5.actor" },
            { "from": "n5.out", "to": "n6.in" }, { "from": "n7.value", "to": "n6.condition" }, { "from": "n8.value", "to": "n7.a" },
            { "from": "n9.out", "to": "n10.in" }, { "from": "n10.out", "to": "n11.in" }
          ] }
        """;

    [Fact]
    public void Checks_NameEachNodeThatCantWaitOrCantRunAfterAWait()
    {
        var problems = GraphChecks.Check(LogicGraph.Parse(Misplaced)).ToDictionary(p => p.Node, p => p.Message);
        Assert.Equal(["n11", "n2", "n5", "n8"], problems.Keys.Order(StringComparer.Ordinal));
        Assert.StartsWith("Wait can't wait here: On Update runs every frame.", problems["n2"]);
        Assert.StartsWith("Destroy can't run after a wait", problems["n5"]);
        Assert.StartsWith("Button Held can't be read after a wait", problems["n8"]);
        Assert.StartsWith("Choice has no options.", problems["n11"]);
        // before the first wait, anything goes: Set Velocity X stays in Start
        Assert.DoesNotContain("n10", problems.Keys);
        Assert.Empty(GraphChecks.Check(LogicGraph.Parse(Graph)));
    }

    [Fact]
    public void Checks_UsingTheTouchingActorAfterAWait_IsNamed()
    {
        var g = LogicGraph.Parse("""
            { "format": 1, "class": "T",
              "nodes": [ { "id": "n1", "type": "OnTouch", "x": 0, "y": 0 }, { "id": "n2", "type": "Wait", "x": 10, "y": 0 },
                         { "id": "n3", "type": "CameraFollow", "x": 20, "y": 0 } ],
              "links": [ { "from": "n1.out", "to": "n2.in" }, { "from": "n2.out", "to": "n3.in" }, { "from": "n1.other", "to": "n3.target" } ] }
            """);
        Assert.Equal("n3", Assert.Single(GraphChecks.Check(g)).Node);
    }

    [Fact]
    public void Checks_StopTheBuildWithTheGraphsMessage()
    {
        var r = TestSupport.Compile("graph-waits-bad", TestSupport.Context(), TestSupport.Inline("Bad.graph", LogicGraph.Parse(Misplaced).ToJson()));
        Assert.False(r.Success);
        var ours = r.Diagnostics.Where(d => d.Id == "AGB0303").ToList();
        Assert.Equal(4, ours.Count);
        Assert.All(ours, d => Assert.EndsWith("Bad.graph", d.File));
        Assert.Contains(ours, d => d.Message.StartsWith("Wait can't wait here", StringComparison.Ordinal));
    }
}
