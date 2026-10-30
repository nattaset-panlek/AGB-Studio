# AGB Studio editor · architecture and how to add a window

The editor is `editor/AgbStudio.Editor` (Avalonia 11.3 on .NET 10, Windows only, D-01 / D-10; output `AGBStudio.exe`).
Tests are `tests/AgbStudio.Editor.Tests` (xUnit + Avalonia.Headless with real Skia rendering).
Phase 10 built the shell and the design system; Phase 11 adds one window per Figma frame, each in its own files.

## Layout of the code

| Folder | What is there |
|---|---|
| `Theme/` | `Tokens.Dark.axaml` / `Tokens.Light.axaml` (generated from Figma, do not edit), `Literals.axaml` (raw hex Figma uses outside the tokens), `TextStyles.cs` (the 20 Figma text styles, `t:Fig.Style="Body/12"`), `Fonts.cs`, `Shadows.cs`, control themes (`Chrome.axaml`, `Buttons.axaml`, `Navigation.axaml`, `Panels.axaml`) |
| `Controls/` | The 47 Figma components as Avalonia controls (`AgbButton`, `IconButton`, `TreeItem`, `DocTab`, `InspectorField`, `Toast`, `TitleBar`, `DialogHeader`, `DialogFooter`, ...) |
| `Graphics/` | `Icon` (icon/* by name, `Glyph="play"`), `ArtImage` (art/*, thumb/*, brand/*), the SVG files exported from Figma in `Assets/Icons` and `Assets/Art` |
| `Commands/` | `EditorCommand`, `CommandRegistry`, `Catalog` (every menu command with its Figma shortcut), `MenuModel` (T-M01..T-M10), `ShortcutService` |
| `Services/` | Preferences (`%APPDATA%\AGBStudio\preferences.json`), project settings, recent projects, undo, autosave, documents, toasts, theme, toolchain, project / build / emulator hooks, Edit routing |
| `Shell/` | `MainWindow` (Title Bar + page host + overlays), `WindowService`, `WindowCatalog`, `SceneWorkspace` (T-P01 docking layout), `CommandPalette` (T-O07), `KeyboardShortcutsDialog` (T-D10), `UnsavedChangesDialog` (T-D12), `UI` helpers |
| `Gallery/` | `GalleryCatalog` (every component variant, used by SHOT) and `ComponentGallery` (`AGBStudio --gallery`) |

## Rules

- UI text is exactly the Figma text (English). No emoji anywhere. The product name is "AGB Studio".
- Colours only through tokens: `{DynamicResource text/primary}` in XAML, `control.Token(prop, "text/primary")` in code.
  No hex literals outside `Theme/` (a test scans for them). Game pixels and palette data are data, not UI colours.
- Text only through the Figma styles: `t:Fig.Style="Body/12 Medium"` (XAML) or `UI.Text(text, "Body/12", "text/secondary")`.
- Use the components in `Controls/` instead of building look-alikes. If Figma shows something no component covers, stop and ask (scope rule 1.1).
- Build only what the Figma frame shows (checklist section 1).

## Adding a window (page, dialog, popover, panel)

1. Create the view in its own file, a `Control` with a constructor taking `WindowContext` (or none), and mark it:

```csharp
[EditorWindow("T-D02")]                       // id from checklist section 4; name, kind and size come from WindowCatalog
public sealed class NewSceneDialog : FigmaDialog   // FigmaDialog = dialog surface + Dialog Header + Dialog Footer
{
    public NewSceneDialog(WindowContext ctx)
    {
        var footer = new DialogFooter { ConfirmText = "Create Scene" };
        footer.Cancel += (_, _) => Close(null);
        footer.Confirm += (_, _) => Close(/* result */ null);
        Build("New Scene", "map", content, footer, width: 520);
    }
}
```

   - Pages (`WindowKind.Page`) replace the area under the Title Bar; the Scene Editor workspace is `"T-P01"`.
   - Dialogs open centred and modal, popovers open under the top edge and close on an outside click; both close with
     Esc. Implement `IOverlayContent` (or derive from `FigmaDialog`) and raise `Closed(result)`.
   - Panels fill a slot of a page. `SceneWorkspace` asks for `"T-P01:Hierarchy"`, `"T-P01:Viewport"`, `"T-P01:Inspector"`,
     `"T-P01:Assets"`, `"T-P05"` (Console) and `"T-P06"` (Animation); register them with
     `[EditorWindow("T-P01:Inspector", Kind = WindowKind.Panel)]`.
   - `WindowContext` gives `Services`, `Info` (id, name, kind, Figma size), `Args` (from the caller), `Anchor` (the chip or
     button that opened a popover) and `Close(result)`.
2. Open it from anywhere: `await services.Windows.Open("T-D02", args, anchor, source: "WorldMapNewScene")`. Menu commands
   with `Window = "T-D02"` in `Commands/Catalog.cs` already route there, so the menus, the Command Palette and the buttons
   start opening your window as soon as it is registered. An id with nothing registered shows a "not built yet" notice;
   it never throws.
3. Context menus (T-M11..T-M24) are a static method in your file:

```csharp
[EditorMenu("T-M12")]
public static IReadOnlyList<MenuEntry> ActorContextMenu() =>
    [new CommandEntry("actor.rename"), new CommandEntry("edit.duplicate"), new CommandEntry("edit.delete", Danger: true), SeparatorEntry.Instance, ...];
```

   Open it with `Windows.Open("T-M12", anchor: theTreeItem)`.
4. Commands live with the window that owns them:

```csharp
[EditorCommand("actor.rename", "Rename", Keys = "F2", Glyph = "pencil")]   // new command; Keys in Figma notation
public static void Rename(EditorServices s) { ... }

[CommandHandler("build.buildRom")]   // implements a catalog command that already sits in a menu
public static void BuildRom(EditorServices s) { ... }
```

   Shortcuts are rebindable (`ShortcutService.Rebind`) and checked for conflicts; use the shortcut written in Figma.
5. Services: `ctx.Services` exposes `Project` (`ProjectService`, the open `AgbStudio.Core.Project.GameProject`),
   `ProjectSettings` (the project's `ProjectFile`, saved into the .agbproj), `Build` (`BuildService.BuildAsync(options,
   IBuildObserver)` runs `AgbStudio.Build.Pipeline.GameBuilder`; `LoadReport()` reads `BuildReport`), `Toolchain`
   (`Toolchain.Check()` results), `Emulator` (`EmulatorHost.Acquire()` returns the single `AgbStudio.Emu.LibretroCore`
   of the process), `Documents` (dirty tracking, Save / Save All), `Undo`, `Toasts`, `Preferences`, `Recent`, `Status`
   (status bar texts), `System` (folder / file / save pickers, Show in Explorer, open in the external editor; set
   `System.Selection` to what Show in Explorer should reveal), `Commands`, `Shortcuts`, `Windows`. Share your own models with `Services.Set(x)` / `Services.Get<T>()`.
6. Undoable edits go through `Services.Undo.Do(new DelegateAction(name, undo, redo))`. A panel whose selection supports
   Cut / Copy / Paste / Duplicate / Delete / Select All implements `IEditTarget`; the Edit menu reaches it when focus is inside it.
7. Mark the document dirty (`EditorDocument.IsDirty`) when the user changes it, and set `SaveHandler`; Save, Save All,
   autosave and the Unsaved Changes dialog then work for your window.

## Tests and SHOT for a new window

1. Unit tests for the window's logic, and an E2E test with the headless window: build services with
   `new Fixture()` (`tests/AgbStudio.Editor.Tests/Support/Fixture.cs`; `OpenSkyHopper()` gives the Figma fixture state),
   create `new MainWindow(fx.Services)`, `Show()`, then drive it with `KeyPressQwerty`, `KeyTextInput`, `MouseDown/Up`
   and check `fx.Services.Windows.Requests`, `win.Overlays.Open` and the visual tree. See `Shell/ShellE2ETests.cs`.
2. SHOT (checklist 2.5):
   - Export the Figma frame: add its id and frame name to `FRAMES` in `tests/AgbStudio.Editor.Tests/Shot/figma-export.mjs`
     and run `node tests/AgbStudio.Editor.Tests/Shot/figma-export.mjs shots` (needs the Talk To Figma relay on
     ws://localhost:3055; the script only reads). It writes `docs/evidence/figma/<T-ID>.png` and `manifest.json`.
   - Render the app side: add the id to `ShotTests.Windows()` with a render function (`Capture.ControlToPng` for a dialog
     or popover at the Figma size; `RenderShell`-style for a page with the fixture project).
   - Run `dotnet test tests/AgbStudio.Editor.Tests --filter ShotTests`. It writes `docs/evidence/app/<T-ID>.png`,
     `docs/evidence/diff/<T-ID>.png` (Figma | app | differing pixels in red) and `docs/evidence/shot-report.md` / `.json`
     with the diff percent and the layout offset (tolerance 2 px). Fix differences or log them in the Scope log.
3. Light theme frames (T-L01..T-L03) are rendered with `Preferences.Theme = ThemeMode.Light`.

## Regenerating design data from Figma

- `node tests/AgbStudio.Editor.Tests/Shot/figma-export.mjs` (all steps: tokens, icons, art, menus, shots).
- Then `AGB_REGEN=1 dotnet test tests/AgbStudio.Editor.Tests --filter TokenTests` rewrites `Theme/Tokens.*.axaml` from
  `docs/evidence/figma/tokens.json`; without `AGB_REGEN` the test fails when they differ.
