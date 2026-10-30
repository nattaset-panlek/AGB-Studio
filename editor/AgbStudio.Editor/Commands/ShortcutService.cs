using Avalonia.Input;
using AgbStudio.Editor.Services;

namespace AgbStudio.Editor.Commands;

/// <summary>A command that already uses a gesture.</summary>
public sealed record ShortcutConflict(EditorCommand Command, KeyGesture Gesture);

/// <summary>
/// Shortcut system (P10.13): defaults from the Figma menus and T-D10, rebinding stored in Preferences, conflict
/// detection ("Ctrl+G is used by Go to line") and reset to defaults. Also resolves key presses to commands.
/// </summary>
public sealed class ShortcutService
{
    readonly CommandRegistry _commands;
    readonly PreferencesStore _prefs;

    public ShortcutService(CommandRegistry commands, PreferencesStore prefs)
    {
        _commands = commands;
        _prefs = prefs;
    }

    public event Action? Changed;

    /// <summary>The gesture now bound to a command (override, or the Figma default), or null.</summary>
    public KeyGesture? GestureOf(string commandId)
    {
        if (_prefs.Current.Shortcuts.TryGetValue(commandId, out var text))
            return string.IsNullOrEmpty(text) ? null : Theme.Shortcuts.Parse(text);
        return _commands.Get(commandId).DefaultGesture;
    }

    public bool IsCustomized(string commandId) => _prefs.Current.Shortcuts.ContainsKey(commandId);

    static bool Same(KeyGesture a, KeyGesture b) => a.Key == b.Key && a.KeyModifiers == b.KeyModifiers;

    /// <summary>Commands other than <paramref name="commandId"/> that use <paramref name="gesture"/>. Shortcuts share one namespace, as in
    /// Preferences · Shortcuts where Toggle grid (Scene) conflicts with Go to line (Script); contexts only decide which command a key runs.</summary>
    public IReadOnlyList<ShortcutConflict> FindConflicts(string commandId, KeyGesture gesture)
    {
        _ = _commands.Get(commandId);
        return _commands.All
            .Where(c => c.Id != commandId && GestureOf(c.Id) is { } g && Same(g, gesture))
            .Select(c => new ShortcutConflict(c, gesture)).ToList();
    }

    /// <summary>All pairs of commands that currently share a gesture.</summary>
    public IReadOnlyList<(EditorCommand A, EditorCommand B)> AllConflicts()
    {
        var list = new List<(EditorCommand, EditorCommand)>();
        var bound = _commands.All.Select(c => (c, g: GestureOf(c.Id))).Where(x => x.g != null).ToList();
        for (var i = 0; i < bound.Count; i++)
            for (var j = i + 1; j < bound.Count; j++)
                if (Same(bound[i].g!, bound[j].g!)) list.Add((bound[i].c, bound[j].c));
        return list;
    }

    /// <summary>The user-facing conflict message, as in Record Shortcut (T-O12).</summary>
    public static string ConflictMessage(ShortcutConflict c) => $"{Theme.Shortcuts.Format(c.Gesture)} is used by “{c.Command.Title}”.";

    /// <summary>
    /// Binds <paramref name="gesture"/> (null clears the binding). With <paramref name="replace"/> the conflicting commands
    /// lose the gesture (the Record Shortcut "Replace" button); without it a conflicting rebind is refused.
    /// </summary>
    public IReadOnlyList<ShortcutConflict> Rebind(string commandId, KeyGesture? gesture, bool replace = false)
    {
        var conflicts = gesture is null ? [] : FindConflicts(commandId, gesture);
        if (conflicts.Count > 0 && !replace) return conflicts;
        foreach (var c in conflicts) Set(c.Command.Id, null);
        Set(commandId, gesture);
        _prefs.Save();
        Changed?.Invoke();
        return conflicts;
    }

    void Set(string id, KeyGesture? g)
    {
        var def = _commands.Get(id).DefaultGesture;
        var same = g is null ? def is null : def != null && Same(def, g);
        if (same) _prefs.Current.Shortcuts.Remove(id);
        else _prefs.Current.Shortcuts[id] = g is null ? "" : Theme.Shortcuts.Format(g);
    }

    public void ResetToDefaults()
    {
        _prefs.Current.Shortcuts.Clear();
        _prefs.Save();
        Changed?.Invoke();
    }

    /// <summary>The command a key press triggers in <paramref name="active"/> (Global commands always qualify).</summary>
    public EditorCommand? Resolve(Key key, KeyModifiers mods, ShortcutContext active)
    {
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt) return null;
        mods &= KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Alt;
        EditorCommand? global = null;
        foreach (var c in _commands.All)
        {
            if (GestureOf(c.Id) is not { } g || g.Key != key || g.KeyModifiers != mods) continue;
            if (c.Context == active && active != ShortcutContext.Global) return c;
            if (c.Context == ShortcutContext.Global) global ??= c;
        }
        return global;
    }
}
