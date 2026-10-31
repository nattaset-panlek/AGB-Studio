using System.Text.RegularExpressions;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;
using AgbStudio.Scripting.Bytecode;

namespace AgbStudio.Scripting.Tests;

/// <summary>P6.12 WaitUntil in cutscenes, P4.08 stable ids of saved flags.</summary>
public sealed class FlowTests
{
    const string Head = "using System.Threading.Tasks;\nusing GBA;\n\npublic class Gate : SceneScript\n{\n";

    [Fact]
    public void WaitUntilCompilesItsConditionInPlace()
    {
        // The lambda is not a delegate at run time: its body is compiled where it stands, followed by WaitUntil, which
        // jumps back to it and yields while it is false. It can read the cutscene's own variables (no GAE006 warning).
        var src = Head + "    [Cutscene]\n    static async Task Open()\n    {\n        int need = 2;\n" +
                  "        await WaitUntil(() => Story.Visits >= need && Game.coins > 0);\n        Story.Visits = 0;\n    }\n}\n";
        var r = TestSupport.Compile("flow-waituntil", TestSupport.Context(), TestSupport.Inline("Gate.cs", src));
        Assert.True(r.Success, TestSupport.Errors(r));
        Assert.DoesNotContain(r.Diagnostics, d => d.Id == "GAE006");
        var lines = OpcodeTable.Load().Disassemble(r.Scripts.Single().Code).Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
        var wait = lines.FindIndex(l => l.Contains(": WaitUntil -> ", StringComparison.Ordinal));
        Assert.True(wait > 0, string.Join('\n', lines));
        var target = lines[wait][^4..];
        var condition = lines.FindIndex(l => l.StartsWith(target + ":", StringComparison.Ordinal));
        Assert.Equal("LoadVar 0x0002", lines[condition][6..]);  // the condition starts with Story.Visits
        Assert.Equal("LoadLocal 0", lines[condition + 1][6..]);  // need
        Assert.Contains("Ge", lines[condition + 2]);
        Assert.Contains(lines.Skip(condition).Take(wait - condition), l => l.EndsWith("And", StringComparison.Ordinal));
        Assert.Matches(new Regex("StoreVar 0x0002$"), lines[wait + 3]);  // then Story.Visits = 0
    }

    [Fact]
    public void WaitUntilOnlyInCutscenesAndWithALambda()
    {
        var r = TestSupport.Compile("flow-waituntil-err1", TestSupport.Context(), TestSupport.Inline("Hero.cs",
            "using GBA;\n\npublic class Hero : Behaviour\n{\n    void Update() { WaitUntil(() => Game.coins > 3); }\n}\n"));
        Assert.Contains(r.Diagnostics, d => d.IsError && d.Message.StartsWith("WaitUntil can only be used inside a [Cutscene]", StringComparison.Ordinal));

        var r2 = TestSupport.Compile("flow-waituntil-err2", TestSupport.Context(), TestSupport.Inline("Gate.cs",
            Head + "    static bool Ready() => Game.coins > 3;\n    [Cutscene]\n    static async Task Open()\n    {\n        await WaitUntil(Ready);\n    }\n}\n"));
        Assert.Contains(r2.Diagnostics, d => d.Id == "AGB0300" && d.Message.StartsWith("WaitUntil takes a lambda written in place", StringComparison.Ordinal));
    }

    static ScriptSource Dialogue(string file, params string[] flags)
    {
        var lines = string.Join(",\n", flags.Select(f => $"        {{ \"command\": \"setFlag\", \"flag\": \"{f}\", \"value\": true }}"));
        var name = Path.GetFileNameWithoutExtension(file);
        return TestSupport.Inline(file, $"{{\n  \"format\": 1,\n  \"name\": \"{name}\",\n  \"conversations\": [\n    {{ \"id\": \"talk\", \"lines\": [\n{lines}\n    ] }}\n  ]\n}}\n");
    }

    static ScriptCompileContext Context(Dictionary<string, int> flagIds)
    {
        var ctx = TestSupport.Context();
        ctx.FlagIds = new Dictionary<string, int>(flagIds);
        return ctx;
    }

    [Fact]
    public void AddingAFlagKeepsTheIdsOfTheOthersAndTheSaveFormat()
    {
        // Build 1: the elder's dialogue declares two saved flags.
        var elder = Dialogue("elder.dlg", "MetElder", "Helped");
        var r1 = TestSupport.Compile("flow-flags-1", Context([]), elder);
        Assert.True(r1.Success, TestSupport.Errors(r1));
        Assert.Equal(new Dictionary<string, int> { ["Helped"] = 0, ["MetElder"] = 1 }, r1.FlagIds);
        // A save written by build 1: MetElder is set (bit 1 of the persistent flag store).
        var store = new byte[32];
        store[r1.FlagIds["MetElder"] >> 3] |= (byte)(1 << (r1.FlagIds["MetElder"] & 7));

        // Build 2: a new dialogue that compiles first adds flags whose names sort first. Without the stored ids they
        // would take 0 and 1 and the old save's bit would read as "Asked".
        var owl = Dialogue("a_owl.dlg", "MetOwl", "Asked");
        var fresh = TestSupport.Compile("flow-flags-fresh", Context([]), owl, elder);
        Assert.Equal(0, fresh.FlagIds["Asked"]);
        var r2 = TestSupport.Compile("flow-flags-2", Context(r1.FlagIds), owl, elder);
        Assert.True(r2.Success, TestSupport.Errors(r2));
        Assert.Equal(0, r2.FlagIds["Helped"]);
        Assert.Equal(1, r2.FlagIds["MetElder"]);
        Assert.Equal(2, r2.FlagIds["Asked"]);
        Assert.Equal(3, r2.FlagIds["MetOwl"]);
        Assert.True((store[r2.FlagIds["MetElder"] >> 3] & (1 << (r2.FlagIds["MetElder"] & 7))) != 0);  // the old save still says MetElder
        Assert.Contains("SetFlag 0x0001", OpcodeTable.Load().Disassemble(r2.Scripts.Single(s => s.Name == "elder.talk").Code));

        // Build 3: the elder's dialogue is gone and another flag is added; the ids of removed flags are not given out
        // again (an old save may have them set).
        var r3 = TestSupport.Compile("flow-flags-3", Context(r2.FlagIds), owl, Dialogue("cave.dlg", "SawBat"));
        Assert.Equal(4, r3.FlagIds["SawBat"]);
        Assert.Equal(1, r3.FlagIds["MetElder"]);

        // The save format version follows the save fields and the store, not the flags: old saves still load.
        var save = new SaveSettings { Fields = [new SaveField { Name = "score", Type = "int" }] };
        var v1 = GameBuilder.SaveVersion(save, scriptStore: true);
        save.FlagIds = r3.FlagIds;
        Assert.Equal(v1, GameBuilder.SaveVersion(save, scriptStore: true));
        save.Fields.Add(new SaveField { Name = "gems", Type = "int" });
        Assert.NotEqual(v1, GameBuilder.SaveVersion(save, scriptStore: true));  // a new save field is a new format
    }
}
