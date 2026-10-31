using AgbStudio.Editor.Controls;
using Avalonia.Controls;

namespace AgbStudio.Editor.Shell;

/// <summary>
/// Pause and Step of a page toolbar (every page with Play, Pause, Step in Figma). They act on the running game
/// through the commands "play.pause" and "play.frameStep" on every page, and are disabled while nothing runs; the
/// tooltip names the command and its shortcut and also shows while the button is disabled.
/// </summary>
public static class PlayButtons
{
    public static PlayControl Pause(EditorServices s) => Bind(new PlayControl { Kind = PlayKind.Pause, Name = "PauseButton" }, s, "play.pause");

    public static PlayControl Step(EditorServices s) => Bind(new PlayControl { Kind = PlayKind.Step, Name = "StepButton" }, s, "play.frameStep");

    static PlayControl Bind(PlayControl c, EditorServices s, string commandId)
    {
        if (!s.Commands.TryGet(commandId, out var cmd)) return c;
        c.Command = cmd;
        var g = s.Shortcuts.GestureOf(commandId);
        ToolTip.SetTip(c, new TipContent(cmd.Title, g is null ? null : AgbStudio.Editor.Theme.Shortcuts.Format(g)));
        ToolTip.SetShowOnDisabled(c, true);
        return c;
    }
}
