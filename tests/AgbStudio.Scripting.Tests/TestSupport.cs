using AgbStudio.Build;
using AgbStudio.Core.Project;

namespace AgbStudio.Scripting.Tests;

/// <summary>Shared fixtures: the test project context, the Figma scripts and scratch folders.</summary>
public static class TestSupport
{
    public static string TestsDir => Path.Combine(NativeBuild.RepoRoot, "tests", "AgbStudio.Scripting.Tests");
    public static string ScriptsDir => Path.Combine(TestsDir, "Scripts");
    public static string GoldenDir => Path.Combine(TestsDir, "Golden");
    public static string HarnessDir => Path.Combine(TestsDir, "RomHarness");

    /// <summary>A fresh folder under build/scripting-tests.</summary>
    public static string Scratch(string name)
    {
        var dir = Path.Combine(NativeBuild.RepoRoot, "build", "scripting-tests", name);
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static ScriptSource Script(string fileName) =>
        new(Path.Combine(ScriptsDir, fileName), File.ReadAllText(Path.Combine(ScriptsDir, fileName)));

    public static ScriptSource Inline(string fileName, string text) => new(Path.Combine(ScriptsDir, fileName), text);

    /// <summary>The project of the tests: Sky Hopper-like names, save fields, an Items table and a Story variable.</summary>
    public static ScriptCompileContext Context(bool debug = true) => new()
    {
        ProjectName = "SkyHopper",
        ProjectRoot = TestsDir,
        DebugBuild = debug,
        EmitProjectHeaders = true,
        Sounds = new() { ["jump"] = 0, ["coin"] = 1, ["blip"] = 2 },
        Music = new() { ["help"] = 0, ["forest"] = 1 },
        Scenes = new() { ["Forest_01"] = 0, ["Forest_02"] = 1, ["Cave_01"] = 2 },
        Tags = new() { ["Player"] = 1, ["Enemy"] = 2, ["Coin"] = 3 },
        Strings = ScriptCompileContext.Indexed(["forest.help_me", "forest.no", "forest.yes", "forest_intro.ask", "forest_intro.bye", "forest_intro.hello", "forest_intro.thanks"]),
        Speakers = new() { ["Elder"] = 0, ["Hopper"] = 1 },
        Portraits = new() { ["elder_happy"] = 0, ["elder_worried"] = 1 },
        Paths = [new ScriptPathDef { Name = "EnterScene", Points = [[16, 0], [16, -8]], Speed = 2 }],
        SaveFields =
        [
            new SaveField { Name = "score", Type = "int" },
            new SaveField { Name = "coins", Type = "int" },
            new SaveField { Name = "lives", Type = "byte" },
            new SaveField { Name = "speed", Type = "fixed" },
            new SaveField { Name = "collected", Type = "bitset 32" },
            new SaveField { Name = "bag", Type = "Items[8]" },
        ],
        Tables =
        [
            new TableFile
            {
                Name = "Items",
                KeyField = "id",
                Fields =
                [
                    new FieldDef { Name = "id", Type = "text" },
                    new FieldDef { Name = "power", Type = "int" },
                    new FieldDef { Name = "price", Type = "int" },
                    new FieldDef { Name = "weight", Type = "fixed" },
                ],
                Rows =
                [
                    new() { ["id"] = "Potion", ["power"] = "20", ["price"] = "10", ["weight"] = "0.5" },
                    new() { ["id"] = "Ether", ["power"] = "40", ["price"] = "25", ["weight"] = "0.25" },
                ],
            },
        ],
        Enums = [new ScriptEnumDef { Name = "ForestState", Values = ["Start", "Help", "Rescued"] }],
        Vars = [new ScriptVarDef { Group = "Story", Name = "Forest", Type = "ForestState", Id = 1 }, new ScriptVarDef { Group = "Story", Name = "Visits", Type = "int", Id = 2 }],
    };

    public static ScriptCompileResult Compile(string outName, ScriptCompileContext ctx, params ScriptSource[] sources)
    {
        var dir = Scratch(outName);
        return new ScriptCompiler().Compile(sources, ctx, dir);
    }

    public static string Errors(ScriptCompileResult r) => string.Join("\n", r.Diagnostics.Select(d => d.ToString()));

    public static ScriptSource[] FigmaScripts() =>
        [Script("PlayerController.cs"), Script("Coin.cs"), Script("SlimeController.cs"), Script("Forest.cs"), Script("ShopNatives.cs")];

    /// <summary>Compiles every generated .cpp with the runtime's flags (-Wall -Wextra -Werror); returns g++ output, empty when clean.</summary>
    public static string CompileCpp(ScriptCompileResult r, bool debug = true, IEnumerable<string>? only = null)
    {
        var tc = new Toolchain();
        var flags = NativeBuild.CommonFlags(tc, debug ? BuildConfig.Debug : BuildConfig.Release) +
            $" -O2 -mthumb -I\"{Path.Combine(NativeBuild.RuntimeDir, "include")}\" -I\"{r.IncludeDir}\"";
        var log = new System.Text.StringBuilder();
        var objDir = Path.Combine(r.IncludeDir, "obj");
        Directory.CreateDirectory(objDir);
        foreach (var src in only ?? r.Sources)
        {
            var p = Proc.Run(tc.Gxx, $"{flags} -c \"{src}\" -o \"{Path.Combine(objDir, Path.GetFileName(src) + ".o")}\"", r.IncludeDir, TimeSpan.FromMinutes(2));
            if (p.ExitCode != 0 || p.Output.Trim().Length > 0) log.AppendLine(p.Output);
        }
        return log.ToString();
    }

    public static string Golden(string name) => File.ReadAllText(Path.Combine(GoldenDir, name)).Replace("\r\n", "\n");

    /// <summary>Compares with Golden/name; set AGB_UPDATE_GOLDEN=1 to rewrite the golden file instead.</summary>
    public static void AssertGolden(string name, string actual)
    {
        var path = Path.Combine(GoldenDir, name);
        actual = actual.Replace("\r\n", "\n");
        if (Environment.GetEnvironmentVariable("AGB_UPDATE_GOLDEN") == "1" || !File.Exists(path))
        {
            File.WriteAllText(path, actual);
            if (Environment.GetEnvironmentVariable("AGB_UPDATE_GOLDEN") != "1") Assert.Fail($"Golden file {name} was missing and has been written; review it and run again.");
            return;
        }
        Assert.Equal(Golden(name), actual);
    }
}
