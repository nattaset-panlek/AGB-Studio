namespace AgbStudio.Editor.Commands;

/// <summary>
/// Mouse and key gestures that are not commands: a panel handles them itself, so they have no command id, no menu item
/// and cannot be rebound. Keyboard Shortcuts (T-D10) lists them in their own group and Preferences › Shortcuts (T-S09)
/// finds them by search, both as fixed rows (scope log G-SCOPE.02, T-P06: "Keep and list them in Keyboard Shortcuts").
/// </summary>
public static class FixedGestures
{
    /// <summary>One gesture: its group, what it does, the keycaps it is drawn with, where the pointer is (for the mouse
    /// gestures) and a stable id for the row names.</summary>
    public sealed record Gesture(string Group, string Label, IReadOnlyList<string> Keys, string? Where, string Id)
    {
        /// <summary>The keys as Keyboard Shortcuts and "or press a key" search write them ("Ctrl+Left", "Wheel").</summary>
        public string KeyText => string.Join("+", Keys);

        /// <summary>Preferences › Shortcuts search: the name, group or place has the text, or the keys are the pressed keys.</summary>
        public bool Matches(string query) =>
            Label.Contains(query, StringComparison.OrdinalIgnoreCase) || Group.Contains(query, StringComparison.OrdinalIgnoreCase)
            || (Where?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) || string.Equals(KeyText, query, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The group the Animation panel's gestures are listed under.</summary>
    public const string AnimationGroup = "Animation panel";

    /// <summary>The Animation panel (T-P06, Windows/SceneEditor/AnimationPanel.cs): the speed and the frame order are
    /// edited with the wheel, dragging, and keys while the panel has focus; each edit is one undo step.</summary>
    public static readonly IReadOnlyList<Gesture> All =
    [
        new(AnimationGroup, "Animation speed", ["Wheel"], "on the speed line", "anim.speedWheel"),
        new(AnimationGroup, "Animation frame ticks", ["Wheel"], "on a frame", "anim.frameWheel"),
        new(AnimationGroup, "Move animation frame", ["Drag"], "a frame", "anim.dragFrame"),
        new(AnimationGroup, "Animation slower (one tick more)", ["+"], null, "anim.ticksUp"),
        new(AnimationGroup, "Animation faster (one tick less)", ["-"], null, "anim.ticksDown"),
        new(AnimationGroup, "Move animation frame left", ["Ctrl", "Left"], null, "anim.frameLeft"),
        new(AnimationGroup, "Move animation frame right", ["Ctrl", "Right"], null, "anim.frameRight"),
    ];

    /// <summary>The caption under a fixed group in Keyboard Shortcuts and the tip of a fixed row's lock.</summary>
    public const string FixedNote = "Fixed · keys work while the panel has focus · cannot be changed";
}
