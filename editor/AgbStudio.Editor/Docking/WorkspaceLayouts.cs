using AgbStudio.Editor.Commands;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Shell;
using Avalonia;
using Avalonia.Controls;

namespace AgbStudio.Editor.Docking;

/// <summary>Tools › Workspace Layout: back to the default, save the current layout by name, and the saved layouts.</summary>
public static class WorkspaceLayouts
{
    static EditorServices? _s;
    static DockHost? _dock;

    public static void Attach(EditorServices s, DockHost dock)
    {
        _s = s;
        _dock = dock;
        s.Commands.Get("tools.layout.reset").Handle(() => dock.Apply(DockLayout.Default()));
        s.Commands.Get("tools.layout.save").Handle(() => _ = SaveAsync(s, dock));
    }

    public static IReadOnlyList<MenuEntry> Entries()
    {
        var list = new List<MenuEntry> { new CommandEntry("tools.layout.reset"), new CommandEntry("tools.layout.save") };
        if (_s is not { } s || _dock is not { } dock || s.Preferences.Current.SavedLayouts.Count == 0) return list;
        list.Add(SeparatorEntry.Instance);
        foreach (var (name, json) in s.Preferences.Current.SavedLayouts.OrderBy(kv => kv.Key, StringComparer.CurrentCulture))
            list.Add(new RecentEntry(name, "", false, new Relay(() => Load(dock, json))));
        return list;
    }

    public static void Load(DockHost dock, string json) => dock.Apply(DockLayout.FromJson(json, dock.Layout.AllPanels().ToList()));

    public static void Save(EditorServices s, DockHost dock, string name) =>
        s.Preferences.Update(x => x.SavedLayouts[name] = dock.Layout.ToJson());

    static async Task SaveAsync(EditorServices s, DockHost dock)
    {
        if (s.Windows.Host is not MainWindow win) return;
        if (await win.Overlays.Show(new LayoutNameDialog()) is string name) Save(s, dock, name);
    }
}

/// <summary>Asks the name of a layout to save. Figma has no frame for it; it is built from the Dialog Header and Dialog
/// Footer components.</summary>
public sealed class LayoutNameDialog : FigmaDialog
{
    public TextField LayoutName { get; } = new() { Label = "Layout name", Name = "LayoutName" };

    public LayoutNameDialog()
    {
        Name = "LayoutNameDialog";
        var footer = new DialogFooter { ConfirmText = "Save Layout" };
        footer.Cancel += (_, _) => Close(null);
        footer.Confirm += (_, _) =>
        {
            var name = (LayoutName.Text ?? "").Trim();
            if (name.Length > 0) Close(name);
        };
        Build("Save Layout", "layers", new Border { Padding = new Thickness(16), Child = LayoutName }, footer, 420);
    }
}
