using AgbStudio.Build;
using AgbStudio.Build.Content;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;
using AgbStudio.Emu;

namespace AgbStudio.Tests;

/// <summary>Recording a session and playing it back: same game, same screens; a changed game is caught at the frame it shows.</summary>
[Collection("emulator")]
public sealed class ReplayTests(EmulatorFixture emu)
{
    static readonly string Root = Path.Combine(NativeBuild.RepoRoot, "build", "test-projects");

    static GameProject Project(string name)
    {
        var p = SampleProjects.CreatePlatformerMini(Path.Combine(Root, name));
        var scenePath = Path.Combine(p.ScenesDir, "Forest_01.scene");
        var scene = GameProject.Load<SceneFile>(scenePath);
        scene.Music = new MusicSettings();
        GameProject.Save(scenePath, scene);
        return p;
    }

    static string Build(GameProject p)
    {
        var r = new GameBuilder(p, new Toolchain()).Build(new BuildOptions { Configuration = BuildConfiguration.Debug });
        Assert.True(r.Success, string.Join('\n', r.Diagnostics));
        return r.RomPath;
    }

    // Plays through a PlaySession as the editor does: walking right, then jumping.
    ReplayFile Record(string rom, string name)
    {
        var session = new PlaySession(emu.Core);
        session.Start(rom);
        session.Recorder = new ReplayRecorder(emu.Core.SaveRam.ToArray());
        for (var f = 0; f < 400; f++)
        {
            session.Keys = f < 300 ? GbaKeys.None : f < 380 ? GbaKeys.Right : GbaKeys.Right | GbaKeys.A;
            session.Step();
        }
        var replay = session.Recorder.Finish(name, emu.Core.Frame);
        session.Recorder = null;
        session.Stop();
        return replay;
    }

    [Fact]
    public void AReplayPlaysBackTheSameFromTheFileAndTwice()
    {
        var p = Project("replay_same");
        var rom = Build(p);
        var replay = Record(rom, "walk");
        Assert.Equal(400, replay.Frames);
        Assert.Equal(3, replay.Keys.Count);
        Assert.Equal(400, replay.Checks[^1].Frame);

        Directory.CreateDirectory(p.ReplaysDir);
        GameProject.Save(Path.Combine(p.ReplaysDir, "walk.replay"), replay);
        var first = Assert.Single(ReplayRunner.RunAll(emu.Core, p, rom));
        Assert.True(first.Passed, first.Message);
        Assert.Equal(("walk", 400), (first.Name, first.Frames));
        Assert.True(Assert.Single(ReplayRunner.RunAll(emu.Core, p, rom)).Passed);
    }

    [Fact]
    public void AChangedGameIsCaughtAndItsScreenSaved()
    {
        var p = Project("replay_changed");
        var replay = Record(Build(p), "walk");
        Directory.CreateDirectory(p.ReplaysDir);
        GameProject.Save(Path.Combine(p.ReplaysDir, "walk.replay"), replay);

        var scenePath = Path.Combine(p.ScenesDir, "Forest_01.scene");
        var scene = GameProject.Load<SceneFile>(scenePath);
        scene.Backdrop = "#FF00FF";
        GameProject.Save(scenePath, scene);
        var result = Assert.Single(ReplayRunner.RunAll(emu.Core, p, Build(p)));
        Assert.False(result.Passed);
        Assert.Contains("differs from the recording", result.Message);
        Assert.True(File.Exists(result.PicturePath), result.Message);
    }

    [Fact]
    public void WhatWasOnTheSaveChipComesBackAndAnEmptyChipTakesNoRoom()
    {
        var rom = Build(Project("replay_save"));
        var core = emu.Core;
        core.LoadRom(rom);
        var marked = new byte[core.SaveRam.Length];
        Assert.True(marked.Length > 16);
        marked[3] = 0x5A;
        ReplayRunner.RestoreSave(core, ReplayRunner.PackSave(marked));
        Assert.Equal(0x5A, core.SaveRam[3]);
        ReplayRunner.RestoreSave(core, null);
        Assert.Equal(0, core.SaveRam[3]);
        core.UnloadRom();
        Assert.Null(ReplayRunner.PackSave(new byte[64]));
    }

    [Fact]
    public void ABadReplayFileIsSaidInWords()
    {
        var p = Project("replay_bad");
        var rom = Build(p);
        Directory.CreateDirectory(p.ReplaysDir);
        File.WriteAllText(Path.Combine(p.ReplaysDir, "broken.replay"), "not json");
        GameProject.Save(Path.Combine(p.ReplaysDir, "empty.replay"), new ReplayFile());
        var results = ReplayRunner.RunAll(emu.Core, p, rom);
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.False(r.Passed));
        Assert.Contains(results, r => r.Message.Contains("Record it again") && r.Name == "broken");
    }
}
