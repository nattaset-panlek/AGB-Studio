namespace AgbStudio.Editor.Commands;

public abstract record MenuEntry;

/// <summary>
/// A command in a menu. Label overrides the command title (e.g. "Import…" in Assets). ShowIcon follows the Figma item.
/// A Checkable item is checked when <paramref name="IsChecked"/> says so (else the builder's own check). Keys is a
/// shortcut only shown on the item, for keys a canvas handles itself (World Map Enter / F2, Logic Graph Ctrl+G): it is
/// not a command shortcut, so it never conflicts with one.
/// </summary>
public sealed record CommandEntry(string CommandId, bool ShowIcon = false, string? Label = null, bool Danger = false, bool Checkable = false,
    Func<bool>? IsChecked = null, string? Keys = null) : MenuEntry;

public sealed record SeparatorEntry : MenuEntry
{
    public static readonly SeparatorEntry Instance = new();
}

/// <summary>A submenu; its entries may be produced when it opens (Open Recent). Glyph: the icon Figma shows on the item (T-M23 Command).</summary>
public sealed record SubmenuEntry(string Id, string Title, Func<IReadOnlyList<MenuEntry>> Entries, string? Glyph = null) : MenuEntry;

/// <summary>One menu of the title bar with its Figma id (T-M01..T-M07).</summary>
public sealed record MenuDef(string Id, string Title, IReadOnlyList<MenuEntry> Entries);

/// <summary>The menus exactly as drawn in Editor Map board 2 (order, separators, icons, danger and checked items).</summary>
public static class MenuModel
{
    static CommandEntry E(string id, bool icon = false, string? label = null) => new(id, icon, label);
    static readonly SeparatorEntry S = SeparatorEntry.Instance;

    public static IReadOnlyList<MenuEntry> Create() =>
    [
        E("assets.create.sprite", true), E("assets.create.tileset", true), E("assets.create.font", true), E("assets.create.palette", true),
        E("assets.create.soundEffect", true), E("assets.create.music", true), E("assets.create.dataTable", true), E("assets.create.script", true),
        E("assets.create.logicGraph", true), E("assets.create.dialogue", true),
    ];

    public static IReadOnlyList<MenuEntry> AddActor() =>
    [
        E("scene.addActor.empty", true), E("scene.addActor.player", true), E("scene.addActor.enemy", true), E("scene.addActor.pickup", true),
        E("scene.addActor.trigger", true), E("scene.addActor.camera", true), E("scene.addActor.text", true),
    ];

    /// <param name="recent">Entries of File › Open Recent (T-M08): one per recent project, a separator and Clear Recent.</param>
    public static IReadOnlyList<MenuDef> TitleBarMenus(Func<IReadOnlyList<MenuEntry>>? recent = null) =>
    [
        new("T-M01", "File",
        [
            E("file.newProject", true), E("file.openProject", true), new SubmenuEntry("T-M08", "Open Recent", recent ?? (() => [E("file.clearRecent")])), S,
            E("file.save", true), E("file.saveAll"), S,
            E("file.importAsset", true), E("file.exportRom", true), S,
            E("file.projectSettings", true), E("file.preferences"), S,
            E("file.exit"),
        ]),
        new("T-M02", "Edit",
        [
            E("edit.undo", true), E("edit.redo", true), S,
            E("edit.cut"), E("edit.copy"), E("edit.paste"), E("edit.duplicate"), new CommandEntry("edit.delete", Danger: true), S,
            E("edit.selectAll"), S,
            E("edit.findInProject", true), E("edit.commandPalette"),
        ]),
        new("T-M03", "Assets",
        [
            new SubmenuEntry("T-M09", "Create", Create), E("file.importAsset", true, "Import…"), E("assets.reimport"), E("assets.showInExplorer"), S,
            E("assets.findUnused"), E("assets.optimizePalettes", true),
        ]),
        new("T-M04", "Scene",
        [
            E("scene.new", true), E("scene.settings"), new CommandEntry("scene.setStart", Checkable: true), S,
            new SubmenuEntry("T-M10", "Add Actor", AddActor), E("scene.backgroundLayers", true), S,
            E("scene.worldMap", true),
        ]),
        new("T-M05", "Build",
        [
            E("build.play", true), E("build.playFromHere"), S,
            E("build.buildRom", true), E("build.buildAndRun"), E("build.flash", true), S,
            E("build.report", true), E("build.clean"),
        ]),
        new("T-M06", "Tools",
        [
            E("tools.paletteManager", true), E("tools.fontManager", true), E("tools.database", true), E("tools.localization", true),
            E("tools.audio", true), E("tools.dialogueEditor", true), E("tools.logicGraph", true), S,
            E("tools.debugger", true), E("tools.screenshot", true),
        ]),
        new("T-M07", "Help",
        [
            E("help.gettingStarted", true), E("help.scriptingApi", true), E("help.keyboardShortcuts"), S,
            E("help.reportBug", true), E("help.checkUpdates"), S,
            E("help.about"),
        ]),
    ];

    /// <summary>All command ids referenced by the menus (for tests).</summary>
    public static IEnumerable<string> CommandIds()
    {
        IEnumerable<string> Walk(IEnumerable<MenuEntry> es) => es.SelectMany(e => e switch
        {
            CommandEntry c => [c.CommandId],
            SubmenuEntry s => Walk(s.Entries()),
            _ => [],
        });
        return TitleBarMenus().SelectMany(m => Walk(m.Entries)).Distinct();
    }
}
