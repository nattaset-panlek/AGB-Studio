using AgbStudio.Build;
using AgbStudio.Build.Content.Templates;
using AgbStudio.Build.Pipeline;
using AgbStudio.Scripting;
using Xunit.Abstractions;

namespace AgbStudio.Tests;

/// <summary>G-HW.04: IWRAM (code, data and an 8 KB stack) and EWRAM use read from the linker map of every runtime test ROM
/// and of a template game, split into the rows of blueprint 10 (RamBudget; report.json "ramSplit").</summary>
public sealed class RamBudgetTests(ITestOutputHelper log)
{
    // Rows the runtime fully controls must stay within the blueprint default in every ROM.
    static readonly string[] HeldRows = ["Hot ARM code", "Pools", "VM + input", "Screen arena", "Map buffer", "Save blocks", "Text"];

    [Fact]
    public void EveryRuntimeTestRomFitsIwramWithItsStackAndEwram()
    {
        var b = RuntimeTests.BuildAll();
        Assert.True(b.Success, b.Log);
        foreach (var name in RuntimeTests.Discover())
        {
            var ram = RamBudget.FromMap(LinkerSections.Read(Path.Combine(RuntimeTests.BuildDir, name + ".elf.map")));
            Assert.True(ram.WithinLimits, $"{name}: {string.Join("; ", ram.Warnings())}");
            foreach (var row in HeldRows) Assert.False(ram.Rows.Single(r => r.Name == row).OverBudget, $"{name}: {string.Join("; ", ram.Describe())}");
        }
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
        foreach (var row in HeldRows) Assert.False(ram.Rows.Single(x => x.Name == row).OverBudget, string.Join("\n", ram.Describe()));
        // report.json carries the same rows.
        Assert.Equal(ram.Rows.Select(x => (x.Region, x.Name, x.Bytes)), r.Report!.RamSplit.Select(x => (x.Region, x.Name, x.Bytes)));
        Assert.Equal(9, r.Report.RamSplit.Count);
    }
}
