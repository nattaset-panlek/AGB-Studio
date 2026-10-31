using AgbStudio.Editor.Commands;
using AgbStudio.Editor.Services;
using Avalonia.Input;
using Fmt = AgbStudio.Editor.Theme.Shortcuts;

namespace AgbStudio.Editor.Windows.Settings;

/// <summary>
/// The shortcut bindings being edited in Preferences · Shortcuts (T-S09): the same rules as <see cref="ShortcutService"/>
/// (one namespace, overrides of the Figma defaults, "" = unbound) on a copy that Save writes back and Cancel drops.
/// </summary>
public sealed class ShortcutDraft(CommandRegistry commands, Dictionary<string, string> overrides)
{
    public Dictionary<string, string> Overrides { get; } = overrides;
    public CommandRegistry Commands { get; } = commands;
    public event Action? Changed;

    static bool Same(KeyGesture a, KeyGesture b) => a.Key == b.Key && a.KeyModifiers == b.KeyModifiers;

    public KeyGesture? GestureOf(string id)
    {
        if (Overrides.TryGetValue(id, out var text)) return text.Length == 0 ? null : Fmt.Parse(text);
        return Commands.Get(id).DefaultGesture;
    }

    public IReadOnlyList<ShortcutConflict> FindConflicts(string id, KeyGesture g)
    {
        var context = Commands.Get(id).Context;
        return Commands.All.Where(c => c.Id != id && ShortcutService.CanConflict(c.Context, context) && GestureOf(c.Id) is { } x && Same(x, g))
            .Select(c => new ShortcutConflict(c, g)).ToList();
    }

    /// <summary>The commands that share the current gesture of <paramref name="id"/>.</summary>
    public IReadOnlyList<ShortcutConflict> ConflictsOf(string id) => GestureOf(id) is { } g ? FindConflicts(id, g) : [];

    /// <summary>Binds a gesture; with <paramref name="replace"/> the commands that used it lose it (Record Shortcut "Replace").</summary>
    public IReadOnlyList<ShortcutConflict> Rebind(string id, KeyGesture? g, bool replace = true)
    {
        var conflicts = g is null ? [] : FindConflicts(id, g);
        if (conflicts.Count > 0 && !replace) return conflicts;
        foreach (var c in conflicts) Set(c.Command.Id, null);
        Set(id, g);
        Changed?.Invoke();
        return conflicts;
    }

    void Set(string id, KeyGesture? g)
    {
        var def = Commands.Get(id).DefaultGesture;
        var same = g is null ? def is null : def != null && Same(def, g);
        if (same) Overrides.Remove(id);
        else Overrides[id] = g is null ? "" : Fmt.Format(g);
    }

    public void ResetToDefaults()
    {
        Overrides.Clear();
        Changed?.Invoke();
    }

    /// <summary>Writes the draft into the preferences and tells menus and dialogs that bindings changed.</summary>
    public static void Commit(EditorServicesView s, Dictionary<string, string> overrides)
    {
        var current = s.Preferences.Current.Shortcuts;
        current.Clear();
        foreach (var (k, v) in overrides) current[k] = v;
        s.Preferences.Save();
        // ShortcutService raises Changed on a rebind; rebinding a command to the gesture it already has changes nothing else
        var cmd = s.Commands.All.FirstOrDefault(c => s.Shortcuts.GestureOf(c.Id) is not { } g || s.Shortcuts.FindConflicts(c.Id, g).Count == 0);
        if (cmd != null) s.Shortcuts.Rebind(cmd.Id, s.Shortcuts.GestureOf(cmd.Id));
    }
}

/// <summary>The services <see cref="ShortcutDraft.Commit"/> needs (kept small for tests).</summary>
public sealed record EditorServicesView(PreferencesStore Preferences, CommandRegistry Commands, ShortcutService Shortcuts);
