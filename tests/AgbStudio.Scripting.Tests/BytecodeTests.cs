using System.Text.RegularExpressions;
using AgbStudio.Build;
using AgbStudio.Scripting.Bytecode;

namespace AgbStudio.Scripting.Tests;

/// <summary>P6.10, P6.11, P6.13: [Cutscene] async methods to Event Script bytecode, the opcode table, natives.</summary>
public sealed class BytecodeTests
{
    static readonly Lazy<ScriptCompileResult> s_figma = new(() =>
        TestSupport.Compile("bytecode-figma", TestSupport.Context(), TestSupport.FigmaScripts()));

    static string Listing(ScriptInfo s)
    {
        var ops = OpcodeTable.Load();
        return $"; {s.Name} args {s.Args} locals {s.Locals}\n" + ops.Disassemble(s.Code);
    }

    [Fact]
    public void RescueIntroGoldenBytecode()
    {
        // The blueprint cutscene: LockAll, Music.Play(Songs.Help), await Say, await Move, Story.Forest = Rescued.
        var r = s_figma.Value;
        Assert.True(r.Success, TestSupport.Errors(r));
        var s = r.Scripts.Single(x => x.Name == "Forest.RescueIntro");
        TestSupport.AssertGolden("Forest.RescueIntro.txt", Listing(s));
        Assert.Equal("agb_cutscene::Forest_RescueIntro", s.Symbol);
        Assert.Equal((0, 0), (s.Args, s.Locals));
        Assert.Equal([9, 10, 11, 12, 13], s.Lines.Select(l => l.Line));
    }

    [Fact]
    public void ChoiceWaitAndReturnGoldenBytecode()
    {
        var r = s_figma.Value;
        var s = r.Scripts.Single(x => x.Name == "Forest.AskHelp");
        TestSupport.AssertGolden("Forest.AskHelp.txt", Listing(s));
        Assert.Equal((1, 2), (s.Args, s.Locals));
    }

    [Fact]
    public void ReleaseBuildHasNoLineMarkers()
    {
        var r = TestSupport.Compile("bytecode-release", TestSupport.Context(debug: false), TestSupport.FigmaScripts());
        var ops = OpcodeTable.Load();
        foreach (var s in r.Scripts) Assert.DoesNotContain("Line", ops.Disassemble(s.Code));
    }

    [Fact]
    public void OpcodeTableIsTheSingleSourceForCompilerAndVm()
    {
        // P6.11: the compiler reads runtime/include/agb/script_ops.def (built in, and from disk when asked); every opcode
        // has a VM handler, and the built-in copy equals the runtime file.
        var include = Path.Combine(NativeBuild.RuntimeDir, "include");
        var fromDisk = OpcodeTable.Load(include);
        var builtIn = OpcodeTable.Load();
        Assert.Equal(fromDisk.All, builtIn.All);
        var vm = File.ReadAllText(Path.Combine(NativeBuild.RuntimeDir, "src", "script", "vm.cpp"));
        foreach (var op in fromDisk.All)
        {
            Assert.Contains($"case Op::{op.Name}:", vm);
            Assert.Equal(fromDisk.All.ToList().IndexOf(op), op.Code);
        }
        Assert.Contains("#include \"agb/script_ops.def\"", vm);
        Assert.Contains("#include \"agb/script_ops.def\"", File.ReadAllText(Path.Combine(include, "agb", "script.hpp")));
    }

    [Fact]
    public void BuiltinNativesMatchTheRuntime()
    {
        var natives = NativeCatalog.Builtins(Path.Combine(NativeBuild.RuntimeDir, "include"));
        Assert.Equal(NativeCatalog.Builtins().Select(n => n.Name), natives.Select(n => n.Name));
        string[] needed = ["Say", "Choice", "Move", "LockAll", "UnlockAll", "PlaySound", "ShakeCamera", "GoToScene", "GiveItem", "FadeIn", "FadeOut"];
        foreach (var n in needed) Assert.Contains(natives, x => x.Name == n);
        var cpp = File.ReadAllText(Path.Combine(NativeBuild.RuntimeDir, "src", "script", "natives.cpp"));
        foreach (var n in natives) Assert.Matches(new Regex($@"\bs32 {n.Name}\(Context& ctx, const s32\* args, int argc\)"), cpp);
        Assert.True(natives.Single(n => n.Name == "Say").Waits);
        Assert.False(natives.Single(n => n.Name == "PlaySound").Waits);
    }

    [Fact]
    public void BitmapCompilesToCppAndToNatives()
    {
        const string src = """
using System.Threading.Tasks;
using GBA;

public class Painter : SceneScript
{
    void OnEnter()
    {
        Bitmap.Clear(Bitmap.Rgb(1, 2, 3));
        Bitmap.SetPixel(4, 5, 6);
        Bitmap.FillRect(1, 2, 3, 4, 5);
        Bitmap.Show("title");
        Bitmap.Flip();
        Bitmap.SetPaletteColor(7, 8);
    }

    [Cutscene]
    static async Task Paint()
    {
        Bitmap.Clear(Bitmap.Rgb(1, 2, 3));
        Bitmap.SetPixel(4, 5, 6);
        Bitmap.FillRect(1, 2, 3, 4, 5);
        Bitmap.Show("backdrop");
        Bitmap.Flip();
        Bitmap.SetPaletteColor(7, 8);
        await Wait(1);
    }
}
""";
        var ctx = TestSupport.Context();
        ctx.Pictures = ScriptCompileContext.Indexed(["backdrop", "other", "title"]);
        var r = TestSupport.Compile("bytecode-bitmap", ctx, TestSupport.Inline("Painter.cs", src));
        Assert.True(r.Success, TestSupport.Errors(r));
        var cpp = string.Concat(Directory.EnumerateFiles(r.IncludeDir, "*.cpp").Select(File.ReadAllText));
        foreach (var call in new[] { "agb::api::Bitmap::Clear(agb::api::Bitmap::Rgb(1, 2, 3))", "agb::api::Bitmap::SetPixel(4, 5, 6)", "agb::api::Bitmap::FillRect(1, 2, 3, 4, 5)", "agb::api::Bitmap::Show(2)", "agb::api::Bitmap::Flip()", "agb::api::Bitmap::SetPaletteColor(7, 8)" })
            Assert.Contains(call, cpp);
        var listing = OpcodeTable.Load().Disassemble(r.Scripts.Single().Code);
        int Native(string name) => r.Natives.FindIndex(n => n.Name == name);
        foreach (var (name, argc) in new[] { ("BitmapRgb", 3), ("BitmapClear", 1), ("BitmapSetPixel", 3), ("BitmapFillRect", 5), ("BitmapShow", 1), ("BitmapFlip", 0), ("BitmapSetPaletteColor", 2) })
            Assert.Contains($"Native #{Native(name)} argc {argc}", listing);
        // The names both forms used are what the build puts in ROM.
        Assert.Equal(["backdrop", "title"], ctx.UsedPictures.OrderBy(n => n, StringComparer.Ordinal));
        Assert.False(natives_wait(r, "BitmapFlip"));

        // A name that is no image says what is wrong, why, and the closest name.
        var bad = TestSupport.Compile("bytecode-bitmap-bad", ctx, TestSupport.Inline("Painter.cs", "using GBA;\n\npublic class Painter : SceneScript\n{\n    void OnEnter() { Bitmap.Show(\"titles\"); }\n}\n"));
        var d = Assert.Single(bad.Diagnostics, x => x.IsError);
        Assert.Contains("Picture \"titles\" is not in the project. Picture names are the images in assets/images without the extension. Did you mean \"title\"?", d.Message);
    }

    static bool natives_wait(ScriptCompileResult r, string name) => r.Natives.Single(n => n.Name == name).Waits;

    [Fact]
    public void ScriptNativeTableFromAttributes()
    {
        // P6.13: [ScriptNative(waits: ...)] methods are appended to the native table with their waits flag.
        var r = s_figma.Value;
        var builtins = NativeCatalog.Builtins().Count;
        Assert.Equal(["GameGet", "GameSet", "User.ShopNatives.OpenShop", "User.ShopNatives.Twice"], r.Natives.Skip(builtins).Select(n => n.Name));
        Assert.True(r.Natives.Single(n => n.Name == "User.ShopNatives.OpenShop").Waits);
        Assert.False(r.Natives.Single(n => n.Name == "User.ShopNatives.Twice").Waits);
        var cpp = File.ReadAllText(Path.Combine(r.IncludeDir, "agb_scripts.cpp"));
        Assert.Contains("    AGB_BUILTIN_NATIVES(AGB_NATIVE_ENTRY)\n", cpp);
        Assert.Contains("{\"ShopNatives.OpenShop\", agbgen_native_ShopNatives_OpenShop, true},", cpp);
        Assert.Contains("agb::script::natives::waitForResume(ctx);", cpp);
        // A cutscene calls natives by index (Native op with the argument count).
        var src = "using System.Threading.Tasks;\nusing GBA;\n\npublic class Shopkeeper : SceneScript\n{\n    [Cutscene]\n    static async Task Visit()\n    {\n        ShopNatives.OpenShop(3);\n        Game.coins = ShopNatives.Twice(21);\n    }\n}\n";
        var r2 = TestSupport.Compile("bytecode-natives", TestSupport.Context(), TestSupport.Script("ShopNatives.cs"), TestSupport.Inline("Shopkeeper.cs", src));
        Assert.True(r2.Success, TestSupport.Errors(r2));
        var listing = OpcodeTable.Load().Disassemble(r2.Scripts.Single().Code);
        var open = r2.Natives.FindIndex(n => n.Name == "User.ShopNatives.OpenShop");
        Assert.Contains($"Native #{open} argc 1", listing);
        Assert.Contains($"Native #{open + 1} argc 1", listing);
    }

    [Fact]
    public void CutsceneErrorsExplainTheLimits()
    {
        const string head = "using System.Threading.Tasks;\nusing GBA;\n\npublic class Bad : SceneScript\n{\n";
        var r = TestSupport.Compile("bytecode-err", TestSupport.Context(), TestSupport.Inline("Bad.cs",
            head + "    int Helper() => 3;\n    [Cutscene]\n    async Task Run()\n    {\n        var x = Helper();\n        await Wait(x);\n    }\n}\n"));
        var d = Assert.Single(r.Diagnostics, x => x.IsError);
        Assert.Equal("AGB0300", d.Id);
        Assert.Contains("[ScriptNative]", d.Message);
        Assert.Equal(10, d.Line);

        var r2 = TestSupport.Compile("bytecode-err2", TestSupport.Context(), TestSupport.Inline("Bad.cs",
            head + "    [Cutscene]\n    async Task Run()\n    {\n        await Say(\"no.such.key\");\n    }\n}\n"));
        Assert.Contains(r2.Diagnostics, x => x.Id == "AGB0010" && x.Message.StartsWith("String key \"no.such.key\" is not in the project."));
    }

    [Fact]
    public void BehaviourCutsceneGetsItsActorAndStartsFromCpp()
    {
        var src = "using System.Threading.Tasks;\nusing GBA;\n\npublic class Npc : Behaviour\n{\n    void OnTouch(Actor other) { Talk(2); }\n\n" +
                  "    [Cutscene]\n    async Task Talk(int times)\n    {\n        await Move(actor, Path.EnterScene);\n        await Wait(times);\n    }\n}\n";
        var r = TestSupport.Compile("bytecode-npc", TestSupport.Context(), TestSupport.Inline("Npc.cs", src));
        Assert.True(r.Success, TestSupport.Errors(r));
        var s = r.Scripts.Single();
        Assert.Equal((2, 2), (s.Args, s.Locals));  // slot 0 = the actor slot, slot 1 = times
        Assert.Contains("LoadLocal 0", OpcodeTable.Load().Disassemble(s.Code));
        Assert.Contains("agb_start_script(agb_cutscene::Npc_Talk, actor->index, 2)", File.ReadAllText(Path.Combine(r.IncludeDir, "Npc.cpp")));
        Assert.Equal("", TestSupport.CompileCpp(r));
    }
}
