using AgbStudio.Editor.Commands;
using AgbStudio.Editor.Graphics;
using Avalonia.Controls;

namespace AgbStudio.Editor.Shell;

/// <summary>Builds Avalonia menus from <see cref="MenuModel"/>: commands, current shortcuts, icons, danger and checked items.</summary>
public sealed class MenuBuilder(CommandRegistry commands, ShortcutService shortcuts, Func<string, bool>? isChecked = null)
{
    public Menu BuildMenuBar(IReadOnlyList<MenuDef> menus)
    {
        var bar = new Menu { Name = "MenuBar" };
        foreach (var m in menus)
        {
            var top = new MenuItem { Header = m.Title, Name = "Menu_" + m.Title, Tag = m.Id };
            Fill(top.Items, m.Entries);
            top.SubmenuOpened += (_, e) => { if (e.Source == top) Refresh(top); };
            bar.Items.Add(top);
        }
        shortcuts.Changed += () => { foreach (MenuItem t in bar.Items.OfType<MenuItem>()) Refresh(t); };
        return bar;
    }

    /// <summary>Items of one menu, for a standalone surface (gallery and SHOT capture of T-M01..T-M08).</summary>
    public List<Control> BuildItems(IReadOnlyList<MenuEntry> entries)
    {
        var list = new List<Control>();
        foreach (var e in entries) list.Add(Build(e));
        return list;
    }

    void Fill(ItemCollection items, IReadOnlyList<MenuEntry> entries)
    {
        items.Clear();
        foreach (var e in entries) items.Add(Build(e));
    }

    Control Build(MenuEntry e)
    {
        switch (e)
        {
            case SeparatorEntry: return new Separator();
            case SubmenuEntry s:
                var sub = new MenuItem { Header = s.Title, Tag = s };
                Fill(sub.Items, s.Entries());
                return sub;
            case CommandEntry c:
                var cmd = commands.Get(c.CommandId);
                var mi = new MenuItem
                {
                    Header = c.Label ?? cmd.Title,
                    Command = cmd,
                    InputGesture = shortcuts.GestureOf(cmd.Id),
                    Icon = c.ShowIcon && cmd.Glyph != null ? new Icon { Glyph = cmd.Glyph, Size = 14 } : null,
                    Tag = c,
                };
                if (c.Danger) mi.Classes.Add("danger");
                if (c.Checkable)
                {
                    mi.ToggleType = MenuItemToggleType.CheckBox;
                    mi.IsChecked = isChecked?.Invoke(cmd.Id) ?? false;
                }
                return mi;
            case RecentEntry r:
                var ri = new MenuItem { Header = r.Name, Command = r.Open, Tag = r };
                if (r.IsCurrent) { ri.ToggleType = MenuItemToggleType.CheckBox; ri.IsChecked = true; }
                return ri;
            default:
                throw new ArgumentOutOfRangeException(nameof(e));
        }
    }

    /// <summary>Re-reads shortcuts, checked states and dynamic submenus (Open Recent) when a menu opens.</summary>
    public void Refresh(MenuItem item)
    {
        foreach (var child in item.Items.OfType<MenuItem>())
        {
            switch (child.Tag)
            {
                case CommandEntry c:
                    child.InputGesture = shortcuts.GestureOf(c.CommandId);
                    if (c.Checkable) child.IsChecked = isChecked?.Invoke(c.CommandId) ?? false;
                    break;
                case SubmenuEntry s:
                    Fill(child.Items, s.Entries());
                    break;
            }
        }
    }
}

/// <summary>One recent project in File › Open Recent (T-M08); the current project is shown checked.</summary>
public sealed record RecentEntry(string Name, string Path, bool IsCurrent, System.Windows.Input.ICommand Open) : MenuEntry;
