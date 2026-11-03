using Avalonia.Input;
using static AgbStudio.Editor.Commands.ShortcutContext;

namespace AgbStudio.Editor.Commands;

/// <summary>
/// Every command of the 7 menus (T-M01..T-M07) and their submenus (T-M08..T-M10), with the default shortcuts written in
/// the Figma menus (Editor Map board 2) and the Keyboard Shortcuts dialog (T-D10), plus the Scene tool keys and
/// "Go to line" used by Preferences · Shortcuts / Record Shortcut (T-S09, T-O12). Create and Add Actor entries are only
/// meaningful inside their submenu, so they are not listed in the Command Palette.
/// </summary>
public static class Catalog
{
    static KeyGesture K(string s) => Theme.Shortcuts.Parse(s);

    static EditorCommand C(string id, string title, string category, string? glyph = null, string? keys = null, string? window = null, ShortcutContext ctx = Global, bool palette = true) =>
        new() { Id = id, Title = title, Category = category, Glyph = glyph, DefaultGesture = keys is null ? null : K(keys), Window = window, Context = ctx, InPalette = palette };

    public static void RegisterAll(CommandRegistry r)
    {
        foreach (var c in Definitions()) r.Add(c);
    }

    public static IEnumerable<EditorCommand> Definitions() =>
    [
        // T-M01 File
        C("file.newProject", "New Project…", "File", "folderplus", "Ctrl+N", "T-D01"),
        C("file.openProject", "Open Project…", "File", "folder", "Ctrl+O", "T-P12"),
        C("file.save", "Save", "File", "save", "Ctrl+S"),
        C("file.saveAll", "Save All", "File", "save", "Ctrl+Shift+S"),
        C("file.importAsset", "Import Asset…", "File", "upload", "Ctrl+I", "T-D04"),
        C("file.exportRom", "Export ROM (.gba)…", "File", "chip", "Ctrl+E", "T-D05"),
        C("file.projectSettings", "Project Settings…", "File", "sliders", window: "T-D08"),
        C("file.preferences", "Preferences…", "File", "sliders", "Ctrl+,", "T-D09"),
        C("file.exit", "Exit", "File", "x", "Alt+F4"),
        C("file.clearRecent", "Clear Recent", "File", palette: false),
        // T-M02 Edit
        C("edit.undo", "Undo", "Edit", "undo", "Ctrl+Z"),
        C("edit.redo", "Redo", "Edit", "redo", "Ctrl+Y"),
        C("edit.cut", "Cut", "Edit", keys: "Ctrl+X"),
        C("edit.copy", "Copy", "Edit", keys: "Ctrl+C"),
        C("edit.paste", "Paste", "Edit", keys: "Ctrl+V"),
        C("edit.duplicate", "Duplicate", "Edit", keys: "Ctrl+D"),
        C("edit.delete", "Delete", "Edit", keys: "Del"),
        C("edit.selectAll", "Select All", "Edit", keys: "Ctrl+A"),
        C("edit.findInProject", "Find in Project…", "Edit", "search", "Ctrl+Shift+F", "T-O07"),
        C("edit.commandPalette", "Command Palette…", "Edit", "search", "Ctrl+K", "T-O07"),
        // T-M03 Assets (+ T-M09 Create)
        C("assets.reimport", "Reimport", "Assets", "refresh"),
        C("assets.showInExplorer", "Show in Explorer", "Assets", "folder"),
        C("assets.findUnused", "Find Unused Assets", "Assets", "search"),
        C("assets.optimizePalettes", "Optimize Palettes…", "Assets", "sliders", window: "T-P10"),
        C("assets.create.sprite", "Sprite", "Create", "image", window: "T-P08", palette: false),
        C("assets.create.tileset", "Tileset / Tilemap", "Create", "tiles", window: "T-P03", palette: false),
        C("assets.create.font", "Font", "Create", "type", window: "T-P09", palette: false),
        C("assets.create.palette", "Palette", "Create", "sliders", window: "T-P10", palette: false),
        C("assets.create.soundEffect", "Sound Effect", "Create", "sound", window: "T-P15", palette: false),
        C("assets.create.music", "Music", "Create", "music", window: "T-P15", palette: false),
        C("assets.create.dataTable", "Data Table", "Create", "table", window: "T-P07", palette: false),
        C("assets.create.script", "C# Script", "Create", "code", window: "T-P04", palette: false),
        C("assets.create.logicGraph", "Logic Graph", "Create", "nodes", window: "T-P17", palette: false),
        C("assets.create.dialogue", "Dialogue", "Create", "book", window: "T-P16", palette: false),
        // T-M04 Scene (+ T-M10 Add Actor)
        C("scene.new", "New Scene…", "Scene", "map", "Ctrl+Shift+N", "T-D02"),
        C("scene.settings", "Scene Settings…", "Scene", "sliders", window: "T-D03"),
        C("scene.setStart", "Set as Start Scene", "Scene", "flag"),
        C("scene.addActor.empty", "Empty Actor", "Add Actor", "cube", palette: false),
        C("scene.addActor.player", "Player", "Add Actor", "user", palette: false),
        C("scene.addActor.enemy", "Enemy", "Add Actor", "bug", palette: false),
        C("scene.addActor.pickup", "Pickup", "Add Actor", "coin", palette: false),
        C("scene.addActor.trigger", "Trigger Zone", "Add Actor", "link", palette: false),
        C("scene.addActor.camera", "Camera Zone", "Add Actor", "camera", palette: false),
        C("scene.addActor.text", "Text Label", "Add Actor", "type", palette: false),
        C("scene.addActor.canvas", "UI Canvas", "Add Actor", "layers", palette: false),
        C("scene.addActor.panel", "UI Panel", "Add Actor", "square", palette: false),
        C("scene.addActor.button", "UI Button", "Add Actor", "play", palette: false),
        C("scene.addActor.image", "UI Image", "Add Actor", "image", palette: false),
        C("scene.backgroundLayers", "Background Layers…", "Scene", "layers", window: "T-P03"),
        C("scene.worldMap", "World Map", "Scene", "nodes", "Ctrl+M", "T-P13"),
        // T-M05 Build
        C("build.play", "Play", "Build", "play", "F5", "T-P02"),
        C("build.playFromHere", "Play from Here", "Build", "play", "Shift+F5", "T-P02"),
        C("build.buildRom", "Build ROM", "Build", "zap", "Ctrl+B", "T-D06"),
        C("build.buildAndRun", "Build & Run in Emulator", "Build", "play", "Ctrl+R", "T-P02"),
        C("build.flash", "Flash to Cartridge…", "Build", "chip", window: "T-D07"),
        C("build.report", "Build Report", "Build", "chip", window: "T-P11"),
        C("build.clean", "Clean Build", "Build", "refresh"),
        // T-M06 Tools
        C("tools.paletteManager", "Palette Manager", "Tools", "sliders", window: "T-P10"),
        C("tools.fontManager", "Font Manager", "Tools", "type", window: "T-P09"),
        C("tools.database", "Database", "Tools", "db", window: "T-P07"),
        C("tools.localization", "Localization", "Tools", "globe", window: "T-P14"),
        C("tools.audio", "Audio", "Tools", "music", window: "T-P15"),
        C("tools.dialogueEditor", "Dialogue Editor", "Tools", "book", window: "T-P16"),
        C("tools.logicGraph", "Logic Graph", "Tools", "nodes", window: "T-P17"),
        C("tools.debugger", "Debugger", "Tools", "bug", "Ctrl+Shift+D", "T-P19"),
        C("tools.screenshot", "Screenshot / GIF", "Tools", "camera", "F12"),
        C("tools.layout.reset", "Default Layout", "Tools", "layers"),
        C("tools.layout.save", "Save Layout…", "Tools", "plus"),
        // T-M07 Help
        C("help.gettingStarted", "Getting Started", "Help", "star", window: "T-P12"),
        C("help.scriptingApi", "Scripting API", "Help", "book", "F1", "T-P18"),
        C("help.keyboardShortcuts", "Keyboard Shortcuts", "Help", "type", "Ctrl+/", "T-D10"),
        C("help.reportBug", "Report a Bug…", "Help", "bug"),
        C("help.checkUpdates", "Check for Updates", "Help", "refresh"),
        C("help.about", "About AGB Studio", "Help", "star", window: "T-D11"),
        // Keyboard Shortcuts dialog, Scene group (T-D10) and the Script Editor binding shown in T-S09 / T-O12
        C("scene.tool.select", "Select", "Scene", "cursor", "V", ctx: Scene),
        C("scene.tool.move", "Move", "Scene", "move", "M", ctx: Scene),
        C("scene.tool.paint", "Paint tiles", "Scene", "pencil", "B", ctx: Scene),
        C("scene.toggleGrid", "Toggle grid", "Scene", "grid", "G", ctx: Scene),
        C("scene.focusSelection", "Focus selection", "Scene", "expand", "F", ctx: Scene),
        C("script.goToLine", "Go to line", "Script", "code", "Ctrl+G", ctx: Script),
    ];
}
