using AgbStudio.Build;
using AgbStudio.Core;
using AgbStudio.Emu;

namespace AgbStudio.Scripting.Tests;

// The libretro core keeps global state: one core per process, ROM tests run one after another.
[CollectionDefinition("emulator", DisableParallelization = true)]
public sealed class EmulatorCollection : ICollectionFixture<EmulatorFixture>;

public sealed class EmulatorFixture : IDisposable
{
    public LibretroCore Core { get; } = new();
    public void Dispose() => Core.Dispose();
}

/// <summary>
/// RT checks: the Figma scripts, a coin and a slime controller are translated, compiled with devkitARM against the
/// runtime and run in mGBA, with cutscenes and the forest_intro dialogue in the Event Script VM.
/// </summary>
[Collection("emulator")]
public sealed class RomTests(EmulatorFixture emu)
{
    static ScriptSource[] Sources() =>
        [.. TestSupport.FigmaScripts(), TestSupport.Script("Shopkeeper.cs"), TestSupport.Script("forest_intro.dlg")];

    static (NativeBuildResult Build, ScriptCompileResult Scripts) BuildRom(string name, string harness, ScriptCompileContext ctx)
    {
        var scripts = TestSupport.Compile($"rom-{name}-gen", ctx, Sources());
        Assert.True(scripts.Success, TestSupport.Errors(scripts));
        var tests = Path.Combine(NativeBuild.RuntimeDir, "tests");
        var target = new RomTarget
        {
            Name = name,
            Config = ctx.DebugBuild ? BuildConfig.Debug : BuildConfig.Release,
            Header = new RomHeader("AGBSCRIPT", "ASCT", "00", 0),
            LinkMaxmod = true,
        };
        target.Sources.AddRange(NativeBuild.RuntimeSources());
        target.Sources.Add(Path.Combine(tests, "test.cpp"));
        target.Sources.AddRange(scripts.Sources);
        target.Sources.Add(Path.Combine(TestSupport.HarnessDir, harness, "main.cpp"));
        target.IncludeDirs.Add(Path.Combine(NativeBuild.RuntimeDir, "include"));
        target.IncludeDirs.Add(tests);
        target.IncludeDirs.Add(scripts.IncludeDir);
        if (!ctx.DebugBuild) target.Defines.Add("AGB_KEEP_LOGS=1");
        var dir = Path.Combine(NativeBuild.RepoRoot, "build", "scripting-tests", $"rom-{name}");
        var build = new NativeBuild(new Toolchain(), dir).Build([target]);
        // g++ messages from generated code point at the .cs files (#line).
        Assert.True(build.Success, string.Join("\n", GccLogMapper.Map(build.Log).Select(d => d.ToString())) + "\n" + build.Log);
        return (build, scripts);
    }

    [Fact]
    public void TranslatedScriptsRunInTheEmulator()
    {
        var (build, _) = BuildRom("scripts_game", "scripts_game", TestSupport.Context(debug: true));
        var result = RomTestRunner.Run(emu.Core, build.RomPath, new RomTestOptions { MaxFrames = 4000 });
        Png.WriteArgb(Path.ChangeExtension(build.RomPath, ".png"), result.LastFrame, LibretroCore.Width, LibretroCore.Height);
        var log = string.Join("\n", result.Log);
        Assert.True(result.Passed, $"{result.Error}\n{log}");
        Assert.Contains("ok   translated_behaviours_play_like_the_figma_code", log);
        Assert.Contains("ok   cutscenes_and_dialogue_run_in_the_vm", log);
        // SlimeController's Debug.Log($"...") with an int and a fixed value.
        Assert.Contains("slime hit the player, hearts 2, speed 0.75", log);
    }

    [Fact]
    public void ReleaseBuildOfTheSameScriptsRuns()
    {
        var (build, _) = BuildRom("scripts_game_release", "scripts_game", TestSupport.Context(debug: false));
        var result = RomTestRunner.Run(emu.Core, build.RomPath, new RomTestOptions { MaxFrames = 4000 });
        Assert.True(result.Passed, $"{result.Error}\n{string.Join("\n", result.Log)}");
    }
}
