using AgbStudio.Build;
using AgbStudio.Build.Content.Templates;
using AgbStudio.Build.Pipeline;
using AgbStudio.Scripting;
using Xunit.Abstractions;

namespace AgbStudio.Tests;

/// <summary>G-HW.04: IWRAM (code, data and an 8 KB stack) and EWRAM use read from the linker map of every runtime test ROM
/// and of a template game, split into the rows of blueprint 10 (RamBudget; report.json "ramSplit"), with the EWRAM
/// defaults decided on 2026-10-31 (scratch 36 KB, map buffer 8 KB, text 4 KB: 192 KB, 64 KB free).</summary>
public sealed class RamBudgetTests(ITestOutputHelper log)
{
    // The ten rows: the nine of RamBudget and the IWRAM stack (StackSpace, at least 8 KB: RamBudget.WithinLimits).
    static readonly string[] Rows = ["Hot ARM code", "Pools", "VM + input", "Other data", "Screen arena", "Map buffer", "Save blocks", "Text", "Scratch"];

    [Fact]
    public void BlueprintDefaultsLeave64KbOfEwramFree()
    {
        var ram = RamBudget.FromMap(LinkerSections.Parse(["Linker script and memory map", ""]));
        Assert.Equal(Rows, ram.Rows.Select(r => r.Name));
        Assert.Equal([112 * 1024, 8 * 1024, 32 * 1024, 4 * 1024, 36 * 1024], ram.Rows.Where(r => r.Region == "EWRAM").Select(r => r.Budget));
        Assert.Equal(192 * 1024, ram.Rows.Where(r => r.Region == "EWRAM").Sum(r => r.Budget));
        Assert.Equal(64 * 1024, RamBudget.EwramSize - ram.Rows.Where(r => r.Region == "EWRAM").Sum(r => r.Budget));
        Assert.Equal(8 * 1024, RamBudget.StackReserve);
    }

    // Every row the runtime controls is held in every runtime test ROM: the runtime's own objects and libraries
    // (RamBudget.FromMap runtimeOnly) stay within each row's default. The test's own code and data (runtime/tests) are
    // left out of the rows but count for the stack and the hard limits, which every ROM keeps.
    [Fact]
    public void EveryRuntimeTestRomKeepsTheRuntimesRowsWithinTheirDefaults()
    {
        var b = RuntimeTests.BuildAll();
        Assert.True(b.Success, b.Log);
        foreach (var name in RuntimeTests.Discover())
        {
            var map = LinkerSections.Read(Path.Combine(RuntimeTests.BuildDir, name + ".elf.map"));
            var ram = RamBudget.FromMap(map);
            Assert.True(ram.WithinLimits, $"{name}: {string.Join("; ", ram.Warnings())}");
            var runtime = RamBudget.FromMap(map, runtimeOnly: true);
            Assert.Equal(ram.StackSpace, runtime.StackSpace);
            foreach (var row in Rows)
                Assert.False(runtime.Rows.Single(r => r.Name == row).OverBudget, $"{name} (runtime only): {string.Join("; ", runtime.Describe())}");
        }
    }

    [Fact]
    public void GameObjectsAreTheGeneratedCodeAndTheTestRomsOwn()
    {
        Assert.True(RamBudget.IsGameObject("projects_ram_platformer_build_gen_game.o"));
        Assert.True(RamBudget.IsGameObject("tests_gen_render_stress_soundbank.o"));
        Assert.True(RamBudget.IsGameObject("wt_rt4_runtime_tests_render_stress_main.o"));
        Assert.False(RamBudget.IsGameObject("wt_rt4_runtime_src_framework_actor.o"));
        Assert.False(RamBudget.IsGameObject("mm_mixer_gba.o"));
    }

    [Fact]
    public void CodeOfIwramCppFilesCountsAsHotArmCodeNotData()
    {
        // *.iwram.cpp files keep their .text section names in IWRAM; they are code like Maxmod's .iwram sections.
        var map = LinkerSections.Parse([
            "Linker script and memory map",
            "",
            " .iwram         0x03000000      0x838 libmm.a(mm_mixer_gba.o)",
            " .text._ZN3agb6render12MapLayerView7drawRowEiii",
            "                0x03000838      0x37c obj/x_runtime_src_render_map_draw.iwram.o",
            " .bss._ZN3agb5audio12_GLOBAL__N_1L14g_mixingMemoryE",
            "                0x03001500      0x580 obj/x_runtime_src_audio_audio.o",
        ]);
        var ram = RamBudget.FromMap(map);
        Assert.Equal(0x838 + 0x37c, ram.Rows.Single(r => r.Name == "Hot ARM code").Bytes);
        Assert.Equal(0x580, ram.Rows.Single(r => r.Name == "Other data").Bytes);
    }

    [Fact]
    public void TemplateGameReportsTheBlueprintSplit()
    {
        var dir = Path.Combine(NativeBuild.RepoRoot, "build", "test-projects", "ram_platformer");
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        var t = TemplateCatalog.Find("platformer")!;
        var p = t.Create(dir, t.Name);
        var r = new GameBuilder(p, new Toolchain()) { Scripts = new ScriptBackend() }.Build();
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        var ram = RamBudget.FromMap(LinkerSections.Read(r.ElfPath + ".map"));
        foreach (var line in ram.Describe()) log.WriteLine(line);
        Assert.True(ram.WithinLimits, string.Join("; ", ram.Warnings()));
        Assert.True(ram.StackSpace >= RamBudget.StackReserve);
        // A whole game, its own data included, keeps all ten rows within the defaults.
        foreach (var row in Rows) Assert.False(ram.Rows.Single(x => x.Name == row).OverBudget, string.Join("\n", ram.Describe()));
        // report.json carries the same rows.
        Assert.Equal(ram.Rows.Select(x => (x.Region, x.Name, x.Bytes)), r.Report!.RamSplit.Select(x => (x.Region, x.Name, x.Bytes)));
        Assert.Equal(9, r.Report.RamSplit.Count);
    }
}
