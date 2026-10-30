using System.Text.Json;
using AgbStudio.Editor.Commands;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Support;
using AgbStudio.Editor.Theme;
using Avalonia.Input;

namespace AgbStudio.Editor.Tests.Shell;

/// <summary>T-M01..T-M10 (P10.01), P10.12 commands and P10.13 shortcuts.</summary>
public class CommandAndShortcutTests
{
    sealed record FigItem(bool? separator, string? kind, string? label, string? shortcut, string? icon);
    sealed record FigMenu(string title, List<FigItem> items);

    static List<FigMenu> FigmaMenus() => JsonSerializer.Deserialize<List<FigMenu>>(File.ReadAllText(Path.Combine(Repo.FigmaEvidence, "menus.json")))!;

    static IReadOnlyList<MenuEntry> Entries(string id) => id switch
    {
        "T-M09" => MenuModel.Create(),
        "T-M10" => MenuModel.AddActor(),
        _ => MenuModel.TitleBarMenus().Single(m => m.Id == id).Entries,
    };

    [Theory]
    [InlineData("T-M01", "File menu")]
    [InlineData("T-M02", "Edit menu")]
    [InlineData("T-M03", "Assets menu")]
    [InlineData("T-M04", "Scene menu")]
    [InlineData("T-M05", "Build menu")]
    [InlineData("T-M06", "Tools menu")]
    [InlineData("T-M07", "Help menu")]
    [InlineData("T-M09", "Create")]
    [InlineData("T-M10", "Add Actor")]
    public void Menu_MatchesFigmaItemsShortcutsIconsAndKinds(string id, string figmaTitle)
    {
        using var fx = new Fixture(withApp: false);
        var fig = FigmaMenus().Single(m => m.title == figmaTitle).items;
        var app = Entries(id);
        Assert.Equal(fig.Count, app.Count);
        for (var i = 0; i < fig.Count; i++)
        {
            var f = fig[i];
            if (f.separator == true) { Assert.IsType<SeparatorEntry>(app[i]); continue; }
            switch (app[i])
            {
                case SubmenuEntry s:
                    Assert.Equal("Submenu", f.kind);
                    Assert.Equal(f.label, s.Title);
                    break;
                case CommandEntry c:
                    var cmd = fx.Services.Commands.Get(c.CommandId);
                    Assert.Equal(f.label, c.Label ?? cmd.Title);
                    Assert.Equal(f.shortcut, fx.Services.Shortcuts.GestureOf(cmd.Id) is { } g ? Shortcuts.Format(g) : null);
                    Assert.Equal(f.icon, c.ShowIcon ? cmd.Glyph : null);
                    Assert.Equal(f.kind == "Danger", c.Danger);
                    Assert.Equal(f.kind == "Checked", c.Checkable);
                    break;
                default:
                    Assert.Fail($"{id} item {i}: unexpected {app[i]}");
                    break;
            }
        }
    }

    [Fact]
    public void Menus_EveryEntryIsARegisteredCommandAndOpensItsWindow()
    {
        using var fx = new Fixture(withApp: false);
        foreach (var id in MenuModel.CommandIds()) Assert.True(fx.Services.Commands.TryGet(id, out _), id);
        // a command whose window is not built yet is routed, never throws
        fx.Services.Commands.Execute("tools.paletteManager");
        Assert.Contains(("T-P10", (string?)"tools.paletteManager"), fx.Services.Windows.Requests);
    }

    [Fact]
    public void Shortcuts_DefaultsHaveNoConflicts()
    {
        using var fx = new Fixture(withApp: false);
        Assert.Empty(fx.Services.Shortcuts.AllConflicts());
        Assert.Equal("Ctrl+K", Shortcuts.Format(fx.Services.Shortcuts.GestureOf("edit.commandPalette")!));
        Assert.Equal("G", Shortcuts.Format(fx.Services.Shortcuts.GestureOf("scene.toggleGrid")!));
    }

    [Fact]
    public void Shortcuts_RebindToCtrlG_ReportsGoToLine()
    {
        using var fx = new Fixture(withApp: false);
        var sc = fx.Services.Shortcuts;
        var ctrlG = Shortcuts.Parse("Ctrl+G");
        var conflicts = sc.Rebind("scene.toggleGrid", ctrlG);
        var c = Assert.Single(conflicts);
        Assert.Equal("script.goToLine", c.Command.Id);
        Assert.Equal("Ctrl+G is used by “Go to line”.", ShortcutService.ConflictMessage(c));
        // refused: nothing changed
        Assert.Equal("G", Shortcuts.Format(sc.GestureOf("scene.toggleGrid")!));
        // Replace: Toggle grid takes Ctrl+G, Go to line loses it
        sc.Rebind("scene.toggleGrid", ctrlG, replace: true);
        Assert.Equal("Ctrl+G", Shortcuts.Format(sc.GestureOf("scene.toggleGrid")!));
        Assert.Null(sc.GestureOf("script.goToLine"));
        Assert.Empty(sc.AllConflicts());
    }

    [Fact]
    public void Shortcuts_PersistInPreferencesAndResetToDefaults()
    {
        using var fx = new Fixture(withApp: false);
        fx.Services.Shortcuts.Rebind("build.play", Shortcuts.Parse("Ctrl+F5"));
        var reloaded = new AgbStudio.Editor.Services.PreferencesStore(fx.Services.Preferences.FilePath);
        Assert.Equal("Ctrl+F5", reloaded.Current.Shortcuts["build.play"]);
        fx.Services.Shortcuts.ResetToDefaults();
        Assert.Equal("F5", Shortcuts.Format(fx.Services.Shortcuts.GestureOf("build.play")!));
        Assert.Empty(new AgbStudio.Editor.Services.PreferencesStore(fx.Services.Preferences.FilePath).Current.Shortcuts);
    }

    [Fact]
    public void Shortcuts_ResolveByContext()
    {
        using var fx = new Fixture(withApp: false);
        var sc = fx.Services.Shortcuts;
        Assert.Equal("script.goToLine", sc.Resolve(Key.G, KeyModifiers.Control, ShortcutContext.Script)!.Id);
        Assert.Null(sc.Resolve(Key.G, KeyModifiers.Control, ShortcutContext.Scene));
        Assert.Equal("scene.toggleGrid", sc.Resolve(Key.G, KeyModifiers.None, ShortcutContext.Scene)!.Id);
        Assert.Equal("edit.commandPalette", sc.Resolve(Key.K, KeyModifiers.Control, ShortcutContext.Scene)!.Id);
        Assert.Equal("file.preferences", sc.Resolve(Key.OemComma, KeyModifiers.Control, ShortcutContext.Global)!.Id);
    }

    [Theory]
    [InlineData("Ctrl+Shift+S")]
    [InlineData("Ctrl+,")]
    [InlineData("Alt+F4")]
    [InlineData("Del")]
    [InlineData("Ctrl+/")]
    [InlineData("Ctrl+0")]
    [InlineData("Shift+F5")]
    public void Shortcuts_FigmaNotationRoundTrips(string text) => Assert.Equal(text, Shortcuts.Format(Shortcuts.Parse(text)));

    [Fact]
    public void Palette_FindsTheFigmaResultsForPla()
    {
        using var fx = new Fixture(withApp: false);
        ProjectFile F(string n, string folder) => new(n, folder, Path.Combine(folder, n), ProjectFileIndex.GlyphOf(n, folder));
        var files = new[] { F("Platform_tiles.png", "Tilesets"), F("PlayerController.cs", "Scripts"), F("player.png", "Sprites"), F("hero.png", "Sprites") };
        var order = new[] { "player.png", "PlayerController.cs", "Platform_tiles.png" };
        var (cmds, fs) = PaletteSearch.Search("pla", fx.Services.Commands.All, files, c => null, f => Array.IndexOf(order, f.Name) is var i and >= 0 ? i : int.MaxValue);
        Assert.Equal(["Play", "Play from Here", "Palette Manager"], cmds.Select(c => c.Label));
        Assert.Equal(order, fs.Select(f => f.Label));
        Assert.Equal(["Sprites", "Scripts", "Tilesets"], fs.Select(f => f.Right));
        Assert.Equal(["image", "code", "tiles"], fs.Select(f => f.Glyph));
        var (only, none) = PaletteSearch.Search("> pla", fx.Services.Commands.All, files, c => null);
        Assert.Empty(none);
        Assert.Equal("Play", only[0].Label);
    }
}
