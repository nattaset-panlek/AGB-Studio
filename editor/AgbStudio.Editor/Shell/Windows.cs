using AgbStudio.Editor.Commands;
using Avalonia.Controls;

namespace AgbStudio.Editor.Shell;

/// <summary>How a Figma window is shown. Page = replaces the workspace under the Title Bar; Panel = a slot inside a page.</summary>
public enum WindowKind { Page, Panel, Dialog, Popover, Menu }

/// <summary>
/// Registers a window (a <see cref="Control"/>) for a Figma id. Found by reflection at startup, so a Phase 11 window
/// is added by creating its own file, without editing a shared list. The class needs a public constructor taking
/// <see cref="WindowContext"/> or no arguments.
/// <code>[EditorWindow("T-P08")] public sealed partial class SpriteEditorPage : UserControl { ... }</code>
/// Title and Kind default to the entry in <see cref="WindowCatalog"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class EditorWindowAttribute(string id, string? title = null) : Attribute
{
    public string Id { get; } = id;
    public string? Title { get; } = title;
    /// <summary>Overrides the catalog kind (set it for ids that are not in the catalog, e.g. "T-P01:Inspector").</summary>
    public WindowKind Kind { get; set; } = (WindowKind)(-1);
}

/// <summary>
/// Registers a menu (T-M11..T-M24) on a public static method returning its entries; the method may take a
/// <see cref="WindowContext"/>. Opened at <see cref="WindowContext.Anchor"/> as a context menu.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class EditorMenuAttribute(string id) : Attribute
{
    public string Id { get; } = id;
}

/// <summary>
/// Adds a command from a public static method (no parameters, or one <see cref="EditorServices"/>). Keys use the Figma
/// notation ("Ctrl+Shift+S"). Lives in the window's own file.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class EditorCommandAttribute(string id, string title) : Attribute
{
    public string Id { get; } = id;
    public string Title { get; } = title;
    public string Category { get; set; } = "";
    public string? Glyph { get; set; }
    public string? Keys { get; set; }
    public string? Window { get; set; }
    public ShortcutContext Context { get; set; } = ShortcutContext.Global;
    public bool InPalette { get; set; } = true;
}

/// <summary>Implements an existing catalog command (e.g. "build.buildRom") with a public static method (no parameters, or one <see cref="EditorServices"/>).</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class CommandHandlerAttribute(string commandId) : Attribute
{
    public string CommandId { get; } = commandId;
}

/// <summary>
/// Runs a public static method (no parameters, or one <see cref="EditorServices"/>) once when the main window starts,
/// after its own commands are wired: enable conditions of menu commands, event subscriptions of a window group.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class EditorStartupAttribute : Attribute;

/// <summary>What a window receives when it is opened.</summary>
public sealed class WindowContext
{
    public required EditorServices Services { get; init; }
    public required WindowInfo Info { get; init; }
    /// <summary>Arguments from the caller (e.g. the asset to edit).</summary>
    public object? Args { get; init; }
    /// <summary>The control that opened a popover or menu (a chip, a button), or null.</summary>
    public Control? Anchor { get; init; }
    /// <summary>Closes a dialog / popover with a result (no-op for pages and panels).</summary>
    public Action<object?> Close { get; init; } = _ => { };
}

/// <summary>A Figma window: id, name, kind and frame size.</summary>
public sealed record WindowInfo(string Id, string Name, WindowKind Kind, int Width, int Height);

/// <summary>Every window of the traceability list (checklist section 4), so ids resolve to names and kinds even before they are built.</summary>
public static class WindowCatalog
{
    static WindowInfo P(string id, string n, int w = 1440, int h = 900) => new(id, n, WindowKind.Page, w, h);
    static WindowInfo N(string id, string n) => new(id, n, WindowKind.Panel, 860, 250);
    static WindowInfo D(string id, string n, int w, int h) => new(id, n, WindowKind.Dialog, w, h);
    static WindowInfo O(string id, string n, int w, int h) => new(id, n, WindowKind.Popover, w, h);
    static WindowInfo M(string id, string n) => new(id, n, WindowKind.Menu, 264, 0);

    public static readonly IReadOnlyList<WindowInfo> All =
    [
        P("T-P01", "Scene Editor"), P("T-P02", "Play Mode"), P("T-P03", "Tilemap Editor"), P("T-P04", "Script Editor"),
        N("T-P05", "Console panel"), N("T-P06", "Animation panel"), P("T-P07", "Database"), P("T-P08", "Sprite Editor"),
        P("T-P09", "Font Manager"), P("T-P10", "Palette Manager"), P("T-P11", "Build Report"), P("T-P12", "Launcher"),
        P("T-P13", "World Map"), P("T-P14", "Localization"), P("T-P15", "Audio"), P("T-P16", "Dialogue Editor"),
        P("T-P17", "Logic Graph"), P("T-P18", "Scripting API"), P("T-P19", "Debugger"), P("T-P20", "Launcher · Templates"),
        P("T-P21", "Launcher · Learn"), P("T-P22", "Scene Editor · Empty project"), P("T-P23", "Debugger · Memory"),
        P("T-P24", "Debugger · Palettes"), P("T-P25", "SFX Designer"), N("T-P26", "Auto-tile Rules panel"), P("T-P27", "UI Editor"),
        D("T-D01", "New Project", 1180, 668), D("T-D02", "New Scene", 520, 470), D("T-D03", "Scene Settings", 600, 646),
        D("T-D04", "Import Asset", 900, 460), D("T-D05", "Export ROM", 560, 488), D("T-D06", "Build ROM", 600, 440),
        D("T-D07", "Flash to Cartridge", 560, 368), D("T-D08", "Project Settings", 900, 620), D("T-D09", "Preferences", 900, 620),
        D("T-D10", "Keyboard Shortcuts", 760, 390), D("T-D11", "About", 460, 304), D("T-D12", "Unsaved Changes", 460, 262),
        D("T-D13", "New Sprite", 520, 506), D("T-D14", "New Script", 560, 564), D("T-D15", "New Font", 560, 506),
        D("T-D16", "Add Characters", 600, 388), D("T-D17", "New Table", 520, 440), D("T-D18", "Add Field", 520, 516),
        D("T-D19", "Import CSV", 780, 478), D("T-D20", "Add Language", 520, 416), D("T-D21", "New Speaker", 520, 438),
        D("T-D22", "Opening Project", 480, 322), D("T-D23", "Build ROM · Failed", 600, 382), D("T-D24", "Import Asset · Errors", 720, 436),
        D("T-S01", "Project Settings · General", 900, 620), D("T-S02", "Project Settings · Save data", 900, 620),
        D("T-S03", "Project Settings · Build", 900, 620), D("T-S04", "Project Settings · Scripting", 900, 620),
        D("T-S05", "Project Settings · Localization", 900, 620), D("T-S06", "Preferences · Appearance", 900, 620),
        D("T-S07", "Preferences · Editor", 900, 620), D("T-S08", "Preferences · Paths", 900, 620), D("T-S09", "Preferences · Shortcuts", 900, 620),
        O("T-O01", "Scene Switcher", 320, 356), O("T-O02", "Video Mode", 380, 400), O("T-O03", "Grid & Snap", 300, 328),
        O("T-O04", "Asset Picker", 440, 368), O("T-O05", "Component Picker", 400, 464), O("T-O06", "Budget Details", 420, 398),
        O("T-O07", "Command Palette", 640, 325), O("T-O08", "Add Node", 380, 269), O("T-O09", "Link Settings", 360, 330),
        O("T-O10", "Palette Effect", 380, 372), O("T-O11", "Find & Replace", 540, 124), O("T-O12", "Record Shortcut", 340, 264),
        O("T-O13", "Tutorial Steps", 380, 466),
        M("T-M01", "File menu"), M("T-M02", "Edit menu"), M("T-M03", "Assets menu"), M("T-M04", "Scene menu"), M("T-M05", "Build menu"),
        M("T-M06", "Tools menu"), M("T-M07", "Help menu"), M("T-M08", "Open Recent"), M("T-M09", "Create"), M("T-M10", "Add Actor"),
        M("T-M11", "Add to Scene"), M("T-M12", "Actor Context Menu"), M("T-M13", "Asset Context Menu"), M("T-M14", "Component Menu"),
        M("T-M15", "Zoom"), M("T-M16", "Animation List"), M("T-M17", "Project Context Menu"), M("T-M18", "Play Speed"),
        M("T-M19", "OAM Size"), M("T-M20", "Add Layer"), M("T-M21", "Node Context Menu"), M("T-M22", "Scene Context Menu"),
        M("T-M23", "Add to Dialogue"), M("T-M24", "Command"),
    ];

    static readonly Dictionary<string, WindowInfo> ById = All.ToDictionary(w => w.Id, StringComparer.Ordinal);

    public static WindowInfo? Find(string id) => ById.GetValueOrDefault(id);
}
