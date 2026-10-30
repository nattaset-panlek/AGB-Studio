using AgbStudio.Editor.Commands;
using AgbStudio.Editor.Shell;

namespace AgbStudio.Editor.Windows.Dialogue;

/// <summary>
/// Add to Dialogue (T-M23; Dialogue Editor › "Add line · choice · command", toolbar +): Line, Choice, Command ›,
/// Condition, Paste; and its Command submenu (T-M24): Go to scene…, Set flag…, Give item…, Play sound…, Wait…,
/// Shake camera… The commands act on the open Dialogue Editor.
/// </summary>
public static class DialogueMenus
{
    static DialogueEditorPage? Page(EditorServices s) => s.Windows.Host?.CurrentPage as DialogueEditorPage ?? s.GetService(typeof(DialogueEditorPage)) as DialogueEditorPage;

    [EditorMenu("T-M23")]
    public static IReadOnlyList<MenuEntry> AddToDialogue() =>
    [
        new CommandEntry("dialogue.add.line", ShowIcon: true), new CommandEntry("dialogue.add.choice", ShowIcon: true),
        new SubmenuEntry("T-M24", "Command", CommandMenu, "code"), new CommandEntry("dialogue.add.condition", ShowIcon: true),
        SeparatorEntry.Instance, new CommandEntry("edit.paste"),
    ];

    [EditorMenu("T-M24")]
    public static IReadOnlyList<MenuEntry> CommandMenu() =>
    [
        new CommandEntry("dialogue.command.goToScene", ShowIcon: true), new CommandEntry("dialogue.command.setFlag", ShowIcon: true),
        new CommandEntry("dialogue.command.giveItem", ShowIcon: true), new CommandEntry("dialogue.command.playSound", ShowIcon: true),
        new CommandEntry("dialogue.command.wait", ShowIcon: true), new CommandEntry("dialogue.command.shakeCamera", ShowIcon: true),
    ];

    [EditorCommand("dialogue.add.line", "Line", Category = "Dialogue", Glyph = "type", InPalette = false)]
    public static void Line(EditorServices s) => Page(s)?.AddLine();

    [EditorCommand("dialogue.add.choice", "Choice", Category = "Dialogue", Glyph = "nodes", InPalette = false)]
    public static void Choice(EditorServices s) => Page(s)?.AddChoice();

    [EditorCommand("dialogue.add.condition", "Condition", Category = "Dialogue", Glyph = "link", InPalette = false)]
    public static void Condition(EditorServices s) => Page(s)?.AddCondition();

    [EditorCommand("dialogue.command.goToScene", "Go to scene…", Category = "Dialogue", Glyph = "map", InPalette = false)]
    public static void GoToScene(EditorServices s) => Page(s)?.AddCommand("goToScene");

    [EditorCommand("dialogue.command.setFlag", "Set flag…", Category = "Dialogue", Glyph = "flag", InPalette = false)]
    public static void SetFlag(EditorServices s) => Page(s)?.AddCommand("setFlag");

    [EditorCommand("dialogue.command.giveItem", "Give item…", Category = "Dialogue", Glyph = "gift", InPalette = false)]
    public static void GiveItem(EditorServices s) => Page(s)?.AddCommand("giveItem");

    [EditorCommand("dialogue.command.playSound", "Play sound…", Category = "Dialogue", Glyph = "sound", InPalette = false)]
    public static void PlaySound(EditorServices s) => Page(s)?.AddCommand("playSound");

    [EditorCommand("dialogue.command.wait", "Wait…", Category = "Dialogue", Glyph = "clock", InPalette = false)]
    public static void Wait(EditorServices s) => Page(s)?.AddCommand("wait");

    [EditorCommand("dialogue.command.shakeCamera", "Shake camera…", Category = "Dialogue", Glyph = "camera", InPalette = false)]
    public static void ShakeCamera(EditorServices s) => Page(s)?.AddCommand("shakeCamera");
}
