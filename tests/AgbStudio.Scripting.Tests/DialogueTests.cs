using AgbStudio.Scripting.Bytecode;
using AgbStudio.Scripting.Dialogue;

namespace AgbStudio.Scripting.Tests;

/// <summary>P6.15: Dialogue Editor files: lines, speakers, choices, commands, "Show if" and "Then set".</summary>
public sealed class DialogueTests
{
    static readonly Lazy<ScriptCompileResult> s_compiled = new(() =>
        TestSupport.Compile("dialogue", TestSupport.Context(), TestSupport.Script("forest_intro.dlg")));

    [Fact]
    public void ForestIntroGoldenCSharpAndBytecode()
    {
        var r = s_compiled.Value;
        Assert.True(r.Success, TestSupport.Errors(r));
        var cs = r.GeneratedCSharp.Single(x => x.Key.EndsWith("forest_intro.dlg")).Value;
        TestSupport.AssertGolden("forest_intro.dlg.cs", cs);
        var s = r.Scripts.Single();
        Assert.Equal("forest_intro.intro", s.Name);
        Assert.Equal("agb_cutscene::forest_intro_intro", s.Symbol);
        Assert.Equal("Scripts/forest_intro.dlg", s.File);
        TestSupport.AssertGolden("forest_intro.intro.txt", $"; {s.Name} args {s.Args} locals {s.Locals}\n" + OpcodeTable.Load().Disassemble(s.Code));
    }

    [Fact]
    public void LinesChoicesAndCommandsBecomeTheRightCalls()
    {
        var r = s_compiled.Value;
        var listing = OpcodeTable.Load().Disassemble(r.Scripts.Single().Code);
        int Native(string name) => r.Natives.FindIndex(n => n.Name == name);
        Assert.Equal(4, Count(listing, $"Native #{Native("Say")} argc 5"));       // hello, ask, thanks, bye
        Assert.Equal(1, Count(listing, $"Native #{Native("Choice")} argc 2"));
        Assert.Equal(1, Count(listing, $"Native #{Native("GiveItem")} argc 3"));
        Assert.Equal(1, Count(listing, $"Native #{Native("PlaySound")} argc 1"));
        Assert.Equal(1, Count(listing, $"Native #{Native("ShakeCamera")} argc 2"));
        Assert.Equal(1, Count(listing, $"Native #{Native("GoToScene")} argc 1"));
        Assert.Equal(1, Count(listing, "WaitFrames"));
        // Flags used by the dialogue are created as saved flags; Story.Visits (+1) is a typed variable.
        Assert.Equal(new Dictionary<string, int> { ["Helped"] = 0, ["MetElder"] = 1 }, r.AutoFlags);
        Assert.Contains("SetFlag 0x0000", listing);
        Assert.Contains("SetFlag 0x0001", listing);
        Assert.Contains("GetFlag 0x0000", listing);   // Show if !Flags.Helped on the "No" choice
        Assert.Contains("StoreVar 0x0002", listing);  // Then set Story.Visits = Story.Visits + 1
        Assert.Contains("LoadVar 0x0001", listing);   // Show if Story.Forest != ForestState.Rescued
        Assert.Contains("public static bool Helped", r.ProjectApiSource);
    }

    static int Count(string text, string what) => text.Split('\n').Count(l => l.EndsWith(what, StringComparison.Ordinal));

    [Fact]
    public void SpeakerDefaultsAndPerLineOverrides()
    {
        var cs = s_compiled.Value.GeneratedCSharp.Single().Value;
        Assert.Contains("await Say(\"forest_intro.hello\", \"Elder\", \"elder_happy\", 2, \"blip\");", cs);
        Assert.Contains("await Say(\"forest_intro.ask\", \"Elder\", \"elder_worried\", 1, \"blip\");", cs);
    }

    [Fact]
    public void ErrorsPointAtTheDialogueFile()
    {
        var bad = File.ReadAllText(Path.Combine(TestSupport.ScriptsDir, "forest_intro.dlg")).Replace("\"goto\": \"bye\"", "\"goto\": \"byee\"");
        var r = TestSupport.Compile("dialogue-err", TestSupport.Context(), TestSupport.Inline("forest_intro.dlg", bad));
        var d = Assert.Single(r.Diagnostics, x => x.IsError);
        Assert.Equal("Choice 2 goes to line 'byee', which does not exist in conversation 'intro'. Pick an existing line for the choice.", d.Message);
        Assert.Equal(("Scripts/forest_intro.dlg", 18), (d.File, d.Line));

        var missingKey = File.ReadAllText(Path.Combine(TestSupport.ScriptsDir, "forest_intro.dlg")).Replace("forest_intro.bye", "forest_intro.later");
        var r2 = TestSupport.Compile("dialogue-err2", TestSupport.Context(), TestSupport.Inline("forest_intro.dlg", missingKey));
        var d2 = Assert.Single(r2.Diagnostics, x => x.IsError);
        Assert.StartsWith("String key \"forest_intro.later\" is not in the project.", d2.Message);
        Assert.Equal(("Scripts/forest_intro.dlg", 27), (d2.File, d2.Line));

        var r3 = TestSupport.Compile("dialogue-err3", TestSupport.Context(), TestSupport.Inline("x.dlg", "{ \"format\": 1, \"conversations\": [ { \"id\": \"a\", \"lines\": [ { \"command\": \"dance\" } ] } ] }"));
        Assert.Contains(r3.Diagnostics, x => x.Id == "AGB0402");
        var r4 = TestSupport.Compile("dialogue-err4", TestSupport.Context(), TestSupport.Inline("y.dlg", "{ \"format\": 1, "));
        Assert.Contains(r4.Diagnostics, x => x.Id == "AGB0001" && x.Message.StartsWith("The dialogue file is not valid JSON"));
    }

    [Fact]
    public void FileRoundTripsAndListsTexts()
    {
        var f = DialogueFile.Parse(File.ReadAllText(Path.Combine(TestSupport.ScriptsDir, "forest_intro.dlg")));
        var again = DialogueFile.Parse(f.ToJson());
        Assert.Equal(f.Conversations[0].Lines.Count, again.Conversations[0].Lines.Count);
        var texts = again.Texts();
        Assert.Equal("ช่วยข้าได้ไหม", texts["forest_intro.ask"]["th"]);
        Assert.Equal("Yes", texts["forest.yes"]["en"]);
        Assert.Equal(13, f.LineOf["/conversations/0/lines/0"]);
    }
}
