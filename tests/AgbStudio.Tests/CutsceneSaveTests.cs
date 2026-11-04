using AgbStudio.Build;
using AgbStudio.Build.Content;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;
using AgbStudio.Emu;
using AgbStudio.Scripting;

namespace AgbStudio.Tests;

/// <summary>A save made while a cutscene runs goes on from there when it is loaded, in the booted ROM, with the save chip
/// carried over between two runs.</summary>
[Collection("emulator")]
public sealed class CutsceneSaveTests(EmulatorFixture emu)
{
    static readonly string Root = Path.Combine(NativeBuild.RepoRoot, "build", "test-projects");
    const uint StepOffset = 8, RunsOffset = 12;  // GameState: the 8 byte header, then the int fields in order

    // Boot one: the cutscene saves to slot 1 half way. Boot two: the scene finds the save, reads it and loads again.
    static GameProject Project(string name, bool saveCutscenes, string afterSave = "await Wait(120);")
    {
        var p = SampleProjects.CreatePlatformerMini(Path.Combine(Root, name));
        p.Settings.Save.Fields.Clear();
        p.Settings.Save.Fields.Add(new SaveField { Name = "step", Type = "int" });
        p.Settings.Save.Fields.Add(new SaveField { Name = "runs", Type = "int" });
        p.Settings.Save.Cutscenes = saveCutscenes;
        p.Settings.Save.Autosave = true;
        p.SaveSettings();
        File.WriteAllText(Path.Combine(p.ScriptsDir, "Forest_01.cs"), $$"""
using System.Threading.Tasks;
using GBA;

public class Forest_01 : SceneScript
{
    void OnEnter()
    {
        if (Game.step != 0) return;
        if (Save.Exists(1))
        {
            Save.Read(1);
            Scene.Load("Forest_01");
        }
        else Intro();
    }

    [Cutscene]
    static async Task Intro()
    {
        Game.runs += 1;
        int kept = 40;
        Game.step = 1;
        await Wait(30);
        Game.step = 2;
        Save.Write(1);
        {{afterSave}}
        Game.step = kept + 1;
    }
}
""");
        var scenePath = Path.Combine(p.ScenesDir, "Forest_01.scene");
        var scene = GameProject.Load<SceneFile>(scenePath);
        scene.Links.Clear();
        scene.Music = new MusicSettings();
        GameProject.Save(scenePath, scene);
        return p;
    }

    DebugChannel? _channel;

    uint Int(uint offset)
    {
        var core = emu.Core;
        return core.ReadU32(core.ReadU32(_channel!.Game + 8) + offset);
    }

    // Boots until the game has told the debug channel about itself (its first scene is loading or running).
    void Boot(string rom, byte[]? chip = null)
    {
        var core = emu.Core;
        core.Keys = GbaKeys.None;
        core.LoadRom(rom);
        chip?.CopyTo(core.SaveRam);
        _channel = null;
        for (var f = 0; f < 600 && (_channel == null || _channel.Game == 0); f++)
        {
            core.RunFrame();
            _channel ??= DebugChannel.Find(core);
        }
        Assert.NotEqual(0u, _channel?.Game ?? 0);
    }

    // Runs the first boot until the save is on the chip and returns the chip.
    byte[] FirstBoot(string rom)
    {
        var core = emu.Core;
        Boot(rom);
        var f = 0;
        for (; f < 900 && Int(StepOffset) != 2; f++) core.RunFrame();
        Assert.True(f < 900, "the cutscene never reached the save");
        for (var i = 0; i < 5; i++) core.RunFrame();
        var chip = core.SaveRam.ToArray();
        core.UnloadRom();
        return chip;
    }

    [Fact]
    public void LoadingASaveMadeDuringACutsceneGoesOnFromThere()
    {
        var r = new GameBuilder(Project("cutscene_save", true), new Toolchain()) { Scripts = new ScriptBackend() }.Build();
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        var chip = FirstBoot(r.RomPath);

        var core = emu.Core;
        Boot(r.RomPath, chip);
        var f = 0;
        for (; f < 1200 && Int(StepOffset) != 41; f++)
        {
            core.RunFrame();
            Assert.NotEqual(1u, Int(StepOffset));  // it never starts over
        }
        Assert.True(f < 1200, $"the cutscene did not finish: step {Int(StepOffset)}");
        Assert.Equal(1u, Int(RunsOffset));  // from the save, and not started a second time; its local variable came back too
        core.UnloadRom();
    }

    [Fact]
    public void WithoutTheSettingTheCutsceneIsNotKept()
    {
        var r = new GameBuilder(Project("cutscene_save_off", false), new Toolchain()) { Scripts = new ScriptBackend() }.Build();
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        var chip = FirstBoot(r.RomPath);
        var core = emu.Core;
        Boot(r.RomPath, chip);
        for (var f = 0; f < 600; f++) core.RunFrame();
        Assert.Equal(2u, Int(StepOffset));  // as before: the values are loaded and nothing goes on
        core.UnloadRom();
    }

    [Fact]
    public void ACutsceneChangedSinceTheSaveDoesNotResume()
    {
        var p = Project("cutscene_save_changed", true);
        var old = new GameBuilder(p, new Toolchain()) { Scripts = new ScriptBackend() }.Build();
        Assert.True(old.Success, string.Join('\n', old.Diagnostics));
        var chip = FirstBoot(old.RomPath);
        var edited = Path.Combine(p.ScriptsDir, "Forest_01.cs");
        File.WriteAllText(edited, File.ReadAllText(edited).Replace("await Wait(120);", "await Wait(121);"));
        var changed = new GameBuilder(p, new Toolchain()) { Scripts = new ScriptBackend() }.Build();
        Assert.True(changed.Success, string.Join('\n', changed.Diagnostics));
        var core = emu.Core;
        Boot(changed.RomPath, chip);
        for (var f = 0; f < 600; f++) core.RunFrame();
        Assert.Equal(2u, Int(StepOffset));
        core.UnloadRom();
    }

    [Fact]
    public void TheSettingCountsInTheSlotSizeAndTheFormatVersion()
    {
        var off = new SaveSettings { Fields = { new SaveField { Name = "a", Type = "int" } } };
        var on = new SaveSettings { Cutscenes = true, Fields = { new SaveField { Name = "a", Type = "int" } } };
        Assert.Equal(SaveLayout.SlotBytes(off.Fields, true) + SaveLayout.CutsceneBytes, SaveLayout.SlotBytes(on.Fields, true, true));
        Assert.NotEqual(GameBuilder.SaveVersion(off, true), GameBuilder.SaveVersion(on, true));
    }
}
