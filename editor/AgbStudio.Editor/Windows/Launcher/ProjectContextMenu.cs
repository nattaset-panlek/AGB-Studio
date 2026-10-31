using AgbStudio.Editor.Commands;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Graphics;
using AgbStudio.Editor.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace AgbStudio.Editor.Windows.Launcher;

/// <summary>
/// Project Context Menu (T-M17, Launcher › right-click a project): Open, Show in Explorer, Duplicate, Rename,
/// Remove from list, Delete project…. The commands act on the selected project card (the right-click selects it).
/// </summary>
public static class ProjectContextMenu
{
    [EditorMenu("T-M17")]
    public static IReadOnlyList<MenuEntry> Entries() =>
    [
        new CommandEntry("launcher.open", ShowIcon: true), new CommandEntry("launcher.showInExplorer"), new CommandEntry("launcher.duplicate"),
        new CommandEntry("launcher.rename"), SeparatorEntry.Instance,
        new CommandEntry("launcher.removeFromList"), new CommandEntry("launcher.deleteProject", Danger: true),
    ];

    // The handlers below run until the Launcher service exists; it then replaces them with ones that are enabled only
    // while a project card is selected on a Launcher page.
    [EditorCommand("launcher.open", "Open", Category = "Launcher", Glyph = "folder", Keys = "Enter", Context = ShortcutContext.Launcher, InPalette = false)]
    public static void Open(EditorServices s) => _ = LauncherService.Of(s).OpenSelectedAsync("ProjectContextMenu");

    [EditorCommand("launcher.showInExplorer", "Show in Explorer", Category = "Launcher", Glyph = "folder", Context = ShortcutContext.Launcher, InPalette = false)]
    public static void ShowInExplorer(EditorServices s) => LauncherService.Of(s).ShowSelectedInExplorer();

    [EditorCommand("launcher.duplicate", "Duplicate", Category = "Launcher", Context = ShortcutContext.Launcher, InPalette = false)]
    public static void Duplicate(EditorServices s) => LauncherService.Of(s).DuplicateSelected();

    [EditorCommand("launcher.rename", "Rename", Category = "Launcher", Glyph = "pencil", Keys = "F2", Context = ShortcutContext.Launcher, InPalette = false)]
    public static void Rename(EditorServices s) => LauncherService.Of(s).RenameSelected();

    [EditorCommand("launcher.removeFromList", "Remove from list", Category = "Launcher", Context = ShortcutContext.Launcher, InPalette = false)]
    public static void RemoveFromList(EditorServices s) => LauncherService.Of(s).RemoveSelected();

    [EditorCommand("launcher.deleteProject", "Delete project…", Category = "Launcher", Context = ShortcutContext.Launcher, InPalette = false)]
    public static void Delete(EditorServices s) => _ = LauncherService.Of(s).DeleteSelectedAsync();
}

/// <summary>
/// The question Delete project… asks before anything is removed: which folder goes to the Recycle Bin and what is in it.
/// Figma has no frame for it; it is built from the Dialog Header / Dialog Footer components like Unsaved Changes (T-D12).
/// Result "delete" or null.
/// </summary>
public sealed class DeleteProjectDialog : FigmaDialog
{
    public DeleteProjectDialog(string name, string folder, ProjectContents? contents)
    {
        Name = "DeleteProjectDialog";
        var message = UI.Text("The project folder and everything in it is moved to the Recycle Bin and removed from the recent list:", "Body/12", "text/secondary");
        message.TextWrapping = TextWrapping.Wrap;
        var path = UI.Text(folder, "Mono/11", "text/primary");
        path.TextWrapping = TextWrapping.Wrap;
        var what = contents is { } c
            ? $"{c.Scenes} {Plural(c.Scenes, "scene")} · {c.Assets} {Plural(c.Assets, "asset")} · {c.Scripts} {Plural(c.Scripts, "script")}"
            : "The project file could not be read, so its scenes, assets and scripts are not counted. The whole folder still goes to the Recycle Bin.";
        var row = new DockPanel { HorizontalSpacing = 8 };
        var icon = new Icon { Glyph = "folder", Size = 14, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 1, 0, 0) }.Token(Icon.ForegroundProperty, "text/secondary");
        DockPanel.SetDock(icon, Dock.Left);
        var whatText = UI.Text(what, "Label/11", "text/muted");
        whatText.TextWrapping = TextWrapping.Wrap;
        row.Children.AddRange([icon, UI.Column(4, path, whatText)]);
        var content = UI.Column(14, message, UI.Frame(row, "bg/base", pad: new Thickness(10, 8), radius: 6));
        content.Margin = new Thickness(20);
        var footer = new DialogFooter { CancelText = "Cancel", ConfirmText = "Delete project", ConfirmKind = ButtonKind.Danger, Name = "DeleteFooter" };
        footer.Cancel += (_, _) => Close(null);
        footer.Confirm += (_, _) => Close("delete");
        Build($"Delete {name}?", "warn", content, footer, 460);
    }

    static string Plural(int n, string word) => n == 1 ? word : word + "s";
}
