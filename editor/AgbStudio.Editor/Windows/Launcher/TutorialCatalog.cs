namespace AgbStudio.Editor.Windows.Launcher;

/// <summary>What the editor must observe for a tutorial sub-step to tick itself.</summary>
public enum TriggerKind
{
    /// <summary>A command ran (menu, shortcut, palette or button): Target = command id.</summary>
    Command,
    /// <summary>A window was opened through the window service: Target = T-ID.</summary>
    Window,
    /// <summary>A Tree Item with this label became selected (Hierarchy).</summary>
    TreeItemSelected,
    /// <summary>A Panel Tab with this label became selected (Console, Animation ...).</summary>
    PanelTab,
    /// <summary>An Icon Button with this name became active (toolbar tools, e.g. "Tool_pencil").</summary>
    ToolActive,
    /// <summary>An Inspector field with this label shows a value equal to <see cref="Trigger.Value"/> (e.g. Jump force 3.5).</summary>
    InspectorValue,
    /// <summary>A project was created with New Project.</summary>
    ProjectCreated,
    /// <summary>Play Mode is running and the key mapped to the GBA button Target (project Input settings) was pressed.</summary>
    PlayButton,
}

public sealed record Trigger(TriggerKind Kind, string Target, double Value = 0);

/// <summary>A sub-step with the editor actions that complete it (any of them).</summary>
public sealed record SubStep(string Text, IReadOnlyList<Trigger> Triggers)
{
    public SubStep(string text, Trigger trigger) : this(text, [trigger]) { }
}

public sealed record TutorialStep(string Title, string Text, string? Image, IReadOnlyList<SubStep> SubSteps);

/// <summary>A tutorial of Launcher · Learn (T-P21) and the Launcher home, played in Tutorial Steps (T-O13).</summary>
public sealed record TutorialInfo(string Id, string Title, string Subtitle, string HomeSubtitle, string Glyph, string Duration, IReadOnlyList<TutorialStep> Steps)
{
    public int SubStepCount => Steps.Sum(s => s.SubSteps.Count);
}

/// <summary>
/// The tutorials of Launcher · Learn in Figma order. Titles, subtitles and step 3 of "Your first platformer" are the
/// Figma texts; the other steps are written for the editor actions they teach (P15.10 owns the final wording).
/// </summary>
public static class TutorialCatalog
{
    static Trigger Cmd(string id) => new(TriggerKind.Command, id);
    static Trigger Win(string id) => new(TriggerKind.Window, id);

    public static readonly IReadOnlyList<TutorialInfo> All =
    [
        new("first-platformer", "Your first platformer", "Build Sky Hopper step by step", "15 min · build Sky Hopper step by step", "rocket", "15 min",
        [
            new("Create your project", "Open New Project, keep the Platformer template and name the game. Create Project makes the folder and opens it.", "art/genre/platformer",
            [
                new("Open New Project", Win("T-D01")),
                new("Create the project", new Trigger(TriggerKind.ProjectCreated, "")),
                new("Look at the Scene Editor", Win("T-P01")),
            ]),
            new("Find your way around", "The Hierarchy lists everything in the scene, the Inspector edits the selection and the bottom panel holds Assets and the Console.", null,
            [
                new("Pick the Paint tiles tool (B)", [Cmd("scene.tool.paint"), new Trigger(TriggerKind.ToolActive, "Tool_pencil")]),
                new("Toggle the grid (G)", Cmd("scene.toggleGrid")),
                new("Open the Console tab", new Trigger(TriggerKind.PanelTab, "Console")),
            ]),
            new("Make the player jump", "Select Player, then in the Inspector raise Jump force to about 3.5. Press Play and try the A button (Z on your keyboard).", "art/genre/platformer",
            [
                new("Select Player in the Hierarchy", new Trigger(TriggerKind.TreeItemSelected, "Player")),
                new("Set Jump force to 3.5", new Trigger(TriggerKind.InspectorValue, "Jump force", 3.5)),
                new("Press Play and jump", new Trigger(TriggerKind.PlayButton, "A")),
            ]),
            new("Add a coin", "Coins are Pickup actors. Add one from Scene › Add Actor and save the scene.", null,
            [
                new("Add a Pickup from Scene › Add Actor", Cmd("scene.addActor.pickup")),
                new("Save the scene (Ctrl+S)", Cmd("file.save")),
            ]),
            new("Write your first script", "Scripts are C# behaviours. Open PlayerController.cs, change a value and save.", null,
            [
                new("Open PlayerController.cs", Win("T-P04")),
                new("Save the script (Ctrl+S)", Cmd("file.save")),
            ]),
            new("Paint the level", "Open the Forest_01 map and paint ground tiles with the Paint tiles tool.", null,
            [
                new("Open Forest_01.map", Win("T-P03")),
                new("Paint with the B tool", [Cmd("scene.tool.paint"), new Trigger(TriggerKind.ToolActive, "Tool_pencil")]),
            ]),
            new("Check the GBA budget", "VRAM, OAM and ROM space are limited. Look at the budget, build the ROM and read the report.", null,
            [
                new("Open Budget Details", Win("T-O06")),
                new("Build the ROM (Ctrl+B)", Cmd("build.buildRom")),
                new("Open the Build Report", Win("T-P11")),
            ]),
            new("Play it on hardware", "Export the .gba file for an emulator, or flash it to a cartridge.", null,
            [
                new("Export the ROM", Win("T-D05")),
                new("Flash to Cartridge", Win("T-D07")),
            ]),
        ]),
        new("csharp", "Scripting in C# for GBA", "fixed-point, Update(), OnTouch()", "fixed-point, Update(), OnTouch()", "code", "20 min",
        [
            new("Open a script", "Behaviours are C# classes with Update() and OnTouch(). Open PlayerController.cs from Assets.", null,
                [new("Open PlayerController.cs", Win("T-P04"))]),
            new("Read the API", "Numbers on the GBA are fixed-point: fixed is 24.8. The Scripting API lists every class and method.", null,
                [new("Open the Scripting API (F1)", [Cmd("help.scriptingApi"), Win("T-P18")])]),
            new("Compile", "Building translates the scripts to C++ and reports problems in the Console.", null,
                [new("Build the ROM (Ctrl+B)", Cmd("build.buildRom")), new("Open the Console tab", new Trigger(TriggerKind.PanelTab, "Console"))]),
        ]),
        new("thai-fonts", "Making fonts with Thai", "variable width, vowels and tone marks", "variable width, vowels and tone marks", "type", "10 min",
        [
            new("Open the Font Manager", "Fonts are converted to variable-width tiles. Thai vowels and tone marks stack above and below.", null,
                [new("Open the Font Manager", [Cmd("tools.fontManager"), Win("T-P09")])]),
            new("Add characters", "Paste the text of your game to add exactly the characters it uses.", null,
                [new("Add characters from text", Win("T-D16"))]),
            new("Check the text", "Localization shows missing and too long strings.", null,
                [new("Open Localization", [Cmd("tools.localization"), Win("T-P14")])]),
        ]),
        new("palettes", "Palettes & VRAM", "why 16 colours, and how to fit more", "why 16 colours, how to fit more", "chip", "10 min",
        [
            new("Open the Palette Manager", "Each sprite and tile uses one 16-colour palette; the GBA has 16 of them for each layer type.", null,
                [new("Open the Palette Manager", [Cmd("tools.paletteManager"), Win("T-P10")])]),
            new("Check VRAM", "The budget shows how much video memory the scene uses.", null,
                [new("Open Budget Details", Win("T-O06")), new("Open the Build Report", Win("T-P11"))]),
        ]),
        new("sound", "Sound & music", "Maxmod, DMG channels, SFX Designer", "Maxmod, DMG channels, SFX Designer", "music", "15 min",
        [
            new("Open Audio", "Music is MOD or XM, mixed by Maxmod; sound effects can use the DMG channels.", null,
                [new("Open Audio", [Cmd("tools.audio"), Win("T-P15")])]),
            new("Design a sound", "The SFX Designer makes retro sound effects without samples.", null,
                [new("Open the SFX Designer", Win("T-P25"))]),
            new("Listen", "Play the game and listen to the music and effects.", null,
                [new("Press Play", [Cmd("build.play"), Win("T-P02")])]),
        ]),
    ];

    public static TutorialInfo? Find(string id) => All.FirstOrDefault(t => t.Id == id);
}
