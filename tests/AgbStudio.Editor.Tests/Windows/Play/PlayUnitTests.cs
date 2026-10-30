using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Windows.Debugger;
using AgbStudio.Editor.Windows.Play;
using AgbStudio.Emu;
using Avalonia.Input;

namespace AgbStudio.Editor.Tests.Windows.Play;

/// <summary>Unit tests of the Play Mode and Debugger helpers: input map, audio output, ELF names, behaviour layout, debug map, console.</summary>
public class PlayUnitTests
{
    [Fact]
    public void InputMap_KeyboardAndGamepadColumns()
    {
        var map = new InputMap(InputAction.Defaults());
        Assert.Equal(GbaKeys.A, map.Key(Key.Z));
        Assert.Equal(GbaKeys.Start, map.Key(Key.Enter));
        Assert.Equal(GbaKeys.Select, map.Key(Key.Back));
        Assert.Equal(GbaKeys.Up, map.Key(Key.Up));
        Assert.Equal(GbaKeys.None, map.Key(Key.Q));
        Assert.Equal(GbaKeys.A | GbaKeys.B | GbaKeys.R | GbaKeys.Start, map.Pad(new XInputPad(0x1000 | 0x4000 | 0x0200 | 0x0010, 0, 0)));
        Assert.Equal(GbaKeys.Select | GbaKeys.Down | GbaKeys.Right, map.Pad(new XInputPad(0x0020 | 0x0002, 20000, 0)));
        Assert.Equal(GbaKeys.Up, map.Pad(new XInputPad(0, 0, 20000)));
        Assert.Equal(GbaKeys.None, map.Pad(new XInputPad(0, 5000, -5000)));  // inside the dead zone
        Assert.Equal("Enter", InputMap.KeyName(Key.Return));
        Assert.Equal("Tab", InputMap.KeyName(Key.Tab));
    }

    [Fact]
    public void WaveOut_OpensWritesAndCloses()
    {
        using var sink = new WaveOutSink();
        sink.Open(65536);
        sink.Write(new short[2048]);  // silence
        // with a sound device the samples are queued; without one the sink stays closed and drops them
        Assert.Equal(sink.IsOpen ? 2048 : 0, sink.SamplesWritten);
    }

    [Fact]
    public void Elf_DemanglesNestedAndInternalNames()
    {
        Assert.Equal("agb::(anonymous namespace)::g_scene", ElfSymbols.Demangle("_ZN3agb12_GLOBAL__N_1L7g_sceneE"));
        Assert.Equal("agb::save::write", ElfSymbols.Demangle("_ZN3agb4save5writeEiPKv"));
        Assert.Equal("mmMixerMix", ElfSymbols.Demangle("mmMixerMix"));
    }

    [Fact]
    public void BehaviourLayout_FollowsTheItaniumAbi()
    {
        const string header = """
            class PlatformerBody final : public Behaviour {
            public:
                static constexpr u16 kTypeId = 1;
                u16 typeId() const override { return kTypeId; }
                void lateUpdate() override;
                fixed gravity = fixed::lit(0.25);
                fixed jumpForce = fixed::lit(3.5);
                fixed vx, vy;
                bool onGround = false, onLadder = false;
                u16 framesSinceGround = 0;
                void jump() { vy = -jumpForce; onGround = false; }
            };
            """;
        var m = BehaviourLayout.Members(header, "PlatformerBody");
        Assert.Equal([("gravity", 12), ("jumpForce", 16), ("vx", 20), ("vy", 24), ("onGround", 28), ("onLadder", 29), ("framesSinceGround", 30)], m.Select(x => (x.Name, x.Offset)));
        // a bool first sits in the base's tail padding (offset 10)
        var b = BehaviourLayout.Members("class A final : public agb::Behaviour {\n    bool on = true;\n    agb::s32 n = 0;\n};", "A");
        Assert.Equal([("on", 10), ("n", 12)], b.Select(x => (x.Name, x.Offset)));
    }

    [Fact]
    public void DebugMap_BindsBraceLinesToTheNextStatement_AndVmLines()
    {
        var gen = Directory.CreateTempSubdirectory("agb-debugmap-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(gen, "debug-map.json"), "{\"files\":[\"A.cs\",\"PlayerController.cs\"],\"methods\":[]}");
            File.WriteAllText(Path.Combine(gen, "PlayerController.cpp"), "AGB_PROBE(1, 16); x;\nAGB_PROBE(1, 18); if (a) {\nAGB_PROBE(1, 20); y;\nAGB_PROBE(1, 21); z;");
            File.WriteAllText(Path.Combine(gen, "scripts.json"), "{\"scripts\":[{\"id\":2,\"name\":\"Cut.Play\",\"file\":\"dialogue/intro.dlg\",\"line\":1,\"lines\":[[0,3],[6,4]]}]}");
            var map = DebugMap.Load(gen)!;
            Assert.Equal((1, 18), map.BindCSharp("PlayerController.cs", 18));
            Assert.Equal((1, 20), map.BindCSharp("PlayerController.cs", 19));  // the "{" line
            Assert.Null(map.BindCSharp("PlayerController.cs", 30));
            Assert.Equal((2, 4), map.BindVm("intro.dlg", 4));
            Assert.Null(map.BindVm("intro.dlg", 9));
            Assert.Equal("PlayerController.cs", map.FileName(1));
            Assert.Equal("Cut.Play", map.ScriptName(2));
        }
        finally { Directory.Delete(gen, true); }
    }

    [Fact]
    public void Console_ClassifiesGameLines()
    {
        var c = new PlayConsole { Clock = () => new DateTime(2026, 10, 31, 12, 4, 39) };
        Assert.Equal((ConsoleLevel.Warning, "Palette OBJ 1 uses 15/16 colours"), Pick(c.AddGameLine("warning: Palette OBJ 1 uses 15/16 colours")));
        Assert.Equal(ConsoleLevel.Error, c.AddGameLine("assert failed: a.cpp(3): x (y)").Level);
        Assert.Equal((ConsoleLevel.Info, "Slime.Update: target = Player (dist 92)"), Pick(c.AddGameLine("Slime.Update: target = Player (dist 92)")));
        Assert.Equal("12:04:39", c.Entries[0].TimeText);
        Assert.Equal((1, 1), (c.Warnings, c.Errors));
        static (ConsoleLevel, string) Pick(ConsoleEntry e) => (e.Level, e.Text);
    }

    [Fact]
    public void BudgetLines_StatusFromUse()
    {
        Assert.Equal(ProgressStatus.OK, new BudgetLine("OAM", 12, 128, "12 / 128").Status);
        Assert.Equal(ProgressStatus.Warn, new BudgetLine("Palette OBJ 1", 15, 16, "15 / 16 colours").Status);
        Assert.Equal(ProgressStatus.Danger, new BudgetLine("VRAM OBJ", 40, 32, "40 / 32 KB").Status);
        var pal = new ushort[512];
        pal[1] = 1; pal[16 + 3] = 5;          // BG banks 0 and 1
        for (var i = 1; i < 15; i++) pal[256 + 16 + i] = 7;  // OBJ bank 1: 14 colours + transparent
        pal[256 + 1] = 3;
        Assert.Equal((2, 2, 1, 15), BudgetService.Palettes(pal));
    }
}
