using AgbStudio.Build;
using AgbStudio.Build.Content;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Support;
using AgbStudio.Editor.Windows.Play;
using AgbStudio.Emu;
using AgbStudio.Scripting;

namespace AgbStudio.Editor.Tests.Windows.Play;

/// <summary>
/// The Sky Hopper fixture built once into the worktree's build folder (build/test-projects/editor-play). The mGBA core
/// is the one process core the editors share through their EmulatorHost (libretro keeps global state); the classes that
/// run it are in the <see cref="EmulatorCollection"/> and their Fixture gives it back when disposed.
/// </summary>
public static class PlayTestSupport
{
    static readonly Lazy<(string ProjectPath, BuildResult Result)> s_built = new(() =>
    {
        var dir = Path.Combine(NativeBuild.RepoRoot, "build", "test-projects", "editor-play");
        var p = SkyHopper.Create(dir);
        var r = new GameBuilder(p, new Toolchain()) { Scripts = new ScriptBackend() }.Build(new BuildOptions { Configuration = BuildConfiguration.Debug });
        if (!r.Success) throw new InvalidOperationException("Sky Hopper build failed: " + string.Join("\n", r.Errors));
        return (p.ProjectPath, r);
    });

    public static string ProjectPath => s_built.Value.ProjectPath;
    public static BuildResult Built => s_built.Value.Result;

    /// <summary>A fixture with the Figma documents (Fixture.OpenSkyHopper), switched to the built Sky Hopper project, holding the process core.</summary>
    public static PlayController Open(Fixture fx)
    {
        fx.OpenSkyHopper();
        fx.Services.Project.Open(ProjectPath);
        fx.Services.Emulator.Acquire();
        var play = PlayController.Of(fx.Services);
        play.UseTimer = false;
        play.AudioFactory = () => new CountingSink();
        play.Gamepad = () => null;
        play.Console.Clock = () => new DateTime(2026, 10, 31, 12, 4, 31);
        return play;
    }

    /// <summary>Starts the built ROM in Forest_01 (Play from Here without the build) and runs until the scene settled.</summary>
    public static PlayController RunForest(PlayController play, int frames = 150)
    {
        // the hand-over block loads the scene, as Play from Here does (without the build)
        Assert.True(play.Run(Built.RomPath, "Forest_01", PlayStart.FromHere));
        Assert.True(play.IsRunning);
        return RunUntilScene(play, "Forest_01", frames);
    }

    /// <summary>Waits for an async editor operation (a build) while running the UI dispatcher, as the app would.</summary>
    public static T Wait<T>(Task<T> task, int seconds = 180)
    {
        var until = DateTime.UtcNow.AddSeconds(seconds);
        while (!task.IsCompleted)
        {
            if (DateTime.UtcNow > until) throw new TimeoutException("The operation did not finish.");
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }
        return task.GetAwaiter().GetResult();
    }

    /// <summary>Runs the UI dispatcher until <paramref name="done"/> holds (a build started by a command).</summary>
    public static void WaitUntil(Func<bool> done, int seconds = 120, Func<string>? describe = null)
    {
        var until = DateTime.UtcNow.AddSeconds(seconds);
        while (!done())
        {
            if (DateTime.UtcNow > until) throw new TimeoutException("The condition did not become true. " + describe?.Invoke());
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }
    }

    public static void Wait(Task task, int seconds = 180)
    {
        Wait(task.ContinueWith(_ => true, TaskScheduler.Default), seconds);
        task.GetAwaiter().GetResult();
    }

    public static PlayController RunUntilScene(PlayController play, string scene, int maxFrames)
    {
        for (var i = 0; i < maxFrames && play.World.SceneName != scene; i += 10) play.RunFrames(10);
        Assert.Equal(scene, play.World.SceneName);
        return play;
    }
}

/// <summary>Audio output that only counts samples (tests make no sound).</summary>
public sealed class CountingSink : IAudioSink
{
    public int SampleRate { get; private set; }
    public long SamplesWritten { get; private set; }
    public void Open(int sampleRate) => SampleRate = sampleRate;
    public void Write(short[] stereo) => SamplesWritten += stereo.Length;
    public void Dispose() { }
}
