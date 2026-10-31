using AgbStudio.Editor.Commands;
using AgbStudio.Editor.Shell;

namespace AgbStudio.Editor.Windows.Play;

/// <summary>
/// Build › Play / Play from Here / Build &amp; Run in Emulator (P12.02), the play controls Pause / Step / Stop (P12.03)
/// and the Play Speed menu (T-M18, P12.04).
/// </summary>
public static class PlayCommands
{
    public static readonly double[] Speeds = [0.25, 0.5, 1, 2, 4];

    /// <summary>Speed as the Play Speed menu and the speed chip write it ("¼×", "1×").</summary>
    public static string SpeedText(double speed) => speed switch
    {
        0.25 => "¼×",
        0.5 => "½×",
        _ => speed.ToString(System.Globalization.CultureInfo.InvariantCulture) + "×",
    };

    static void Start(EditorServices s, PlayStart how, string source)
    {
        var play = PlayController.Of(s);
        _ = s.Windows.Open("T-P02", source: source);
        _ = play.StartAsync(how);
    }

    [CommandHandler("build.play")]
    public static void Play(EditorServices s) => Start(s, PlayStart.Play, "build.play");

    [CommandHandler("build.playFromHere")]
    public static void PlayFromHere(EditorServices s) => Start(s, PlayStart.FromHere, "build.playFromHere");

    [CommandHandler("build.buildAndRun")]
    public static void BuildAndRun(EditorServices s) => Start(s, PlayStart.BuildAndRun, "build.buildAndRun");

    /// <summary>Pause and Step act on a running game only: disabled until Play runs (the toolbars of every page bind them).</summary>
    [EditorStartup]
    public static void RequireRunningGame(EditorServices s)
    {
        bool Running() => s.GetService(typeof(PlayController)) is PlayController c && c.IsRunning;
        s.Commands.Get("play.pause").Require(Running);
        s.Commands.Get("play.frameStep").Require(Running);
    }

    [EditorCommand("play.pause", "Pause", Category = "Build", Glyph = "pause")]
    public static void Pause(EditorServices s) => PlayController.Of(s).TogglePause();

    [EditorCommand("play.frameStep", "Frame step", Category = "Build", Glyph = "step", Keys = "F10")]
    public static void FrameStep(EditorServices s) => PlayController.Of(s).StepFrame();

    [EditorCommand("play.stop", "Stop", Category = "Build", Glyph = "stop")]
    public static void Stop(EditorServices s) => PlayController.Of(s).Stop();

    /// <summary>T-M18 Play Speed: ¼× ½× 1× 2× 4× (the current one checked), then Frame step F10.</summary>
    [EditorMenu("T-M18")]
    public static IReadOnlyList<MenuEntry> PlaySpeedMenu(WindowContext ctx)
    {
        var play = PlayController.Of(ctx.Services);
        var list = new List<MenuEntry>();
        foreach (var speed in Speeds)
        {
            var v = speed;
            // a checked row with its own action (the menu builder shows RecentEntry.IsCurrent as the check)
            list.Add(new RecentEntry(SpeedText(v), "", Math.Abs(play.Speed - v) < 0.001, new Relay(() => play.Speed = v)));
        }
        list.Add(SeparatorEntry.Instance);
        list.Add(new CommandEntry("play.frameStep", ShowIcon: true));
        return list;
    }
}
