# G-SCOPE.02 review: app inventory against checklist section 4 (2026-10-31)

Scope. Snapshot: worktree `D:\devkitPro\AGB-Studio-wt\docs`, branch `fix-docs`, commit `24a26b6` (the main tip). This
record checks G-SCOPE.02 ("No window, button, panel or setting in the app that is not in section 4") by reading the
editor code and comparing it with the inventory in `docs/ENGINE-CHECKLIST.md` section 4 (T-P01..T-P26, T-D01..T-D24,
T-S01..T-S09, T-O01..T-O13, T-M01..T-M24, T-L01..T-L04, T-Y01..T-Y04), the approved deviations in the Scope log
(section 8) and `docs/DECISIONS.md` (D-09, D-13, D-23). Read: `editor/AgbStudio.Editor/**` (every `.cs` file),
`Shell/Windows.cs` (`WindowCatalog`), `Commands/Catalog.cs`, `Commands/MenuModel.cs`, `Windows/Settings/**`,
`docs/evidence/figma/menus.json` (parsed item by item), `docs/evidence/figma/captions.json`, the Figma frame exports
`docs/evidence/figma/T-D08.png`, `T-D09.png`, `T-S01.png`..`T-S09.png` (field lists; the captions have none) and, as
spot checks, `T-P01`, `T-P03`, `T-P08`, `T-P12`, `T-P18`, `T-P19`, `T-D01`, `T-D05`, `T-D10`, `T-D13`, `T-D14`, `T-D17`,
`T-M21`; `docs/user/windows/README.md` and its 72 pages, `docs/evidence/editor-gaps.md`, `docs/evidence/shot-report.md`,
and the tests in `tests/AgbStudio.Editor.Tests` (read only). Method: grep for `\[EditorWindow\(`, `\[EditorMenu\(`,
`\[EditorCommand\(`, `\[CommandHandler\(`; classes deriving from `FigmaDialog`, `IOverlayContent`, `Window`,
`UserControl`; `Overlays\.Show\(`, `new ContextMenu`, `new MenuFlyout`, `new Popup`, `new Window`, `StorageProvider`,
`Open(File|Folder)PickerAsync`, `SaveFilePickerAsync`, `PickFolderAsync`, `PickFileAsync`, `PickSaveFileAsync`,
`ShowInExplorer`, `links\.Open`, `Process\.Start`; `Args\?\.`, `GetCommandLineArgs`, `"--[a-z]`,
`GetEnvironmentVariable`, `#if DEBUG`; every `"T-[A-Z][0-9]{2}` string in the editor; comments matching
`figma has no|not in figma|infra`; every `PanelTab` / `NavItem` label. This is a presence check only: it says whether a
window, menu item, tab, field or button exists, not whether it looks like Figma (that is SHOT,
`docs/evidence/shot-report.md`) or whether it works (E2E, and the known gaps in `docs/evidence/editor-gaps.md`). No
code was built or run and no test was run for this record. It ticks nothing in the checklist.

## Summary

| Group | Section 4 ids | Present in code | Not present | Extra found |
|---|---|---|---|---|
| Pages, dialogs, settings tabs, popovers (T-P, T-D, T-S, T-O) | 72 | 72 (each has an `[EditorWindow]`) | 0 | 4 slot ids inside T-P01 (allowed) |
| Light theme frames (T-L01..T-L03) | 3 | 3 (the same windows in the Light tokens; no own registration, by design) | 0 | 0 |
| Light theme rule (T-L04) | 1 | not checked here (needs the colour-literal scan) | - | - |
| Menus (T-M01..T-M24) | 24 | 24 (7 title bar menus and 3 submenus in `MenuModel`, 14 `[EditorMenu]`) | 0 menus, 0 items | 0 items; 1 extra separator (T-M21) |
| System windows (T-Y01..T-Y04) | 4 | 4 (`Services/SystemShell.cs`) | 0 | 11 more OS file / folder dialog entry points (see below) |
| Settings tabs and fields (T-D08, T-D09, T-S01..T-S09) | 11 tabs | 11 tabs; every field in the Figma frames | 0 | 0 tabs, 0 fields, 0 buttons |
| `WindowCatalog` (`Shell/Windows.cs:83`) | 96 (T-P, T-D, T-S, T-O, T-M) | 96, the same ids and names | 0 | 0 |

Items in the app that are not in section 4: 16 rows in the table below.
- Need a Scope log row (not in Figma): 4. The Delete project dialog, the "devkitPro not found" / "A build tool is
  missing" dialog, the "See also" button row on Scripting API, and 11 OS file / folder dialog entry points that 4.11
  does not list.
- Already in the Scope log, still pending: 1. The four in-game screens (no section 4 id yet).
- Fix in code: 1. The extra separator in the Node Context Menu (T-M21).
- Allowed as infra, component behaviour or an earlier decision: 10.

## Windows

Paths in "Registered in code" are under `editor/AgbStudio.Editor/`. User doc pages are under `docs/user/windows/`.
"Gap" notes repeat `docs/evidence/editor-gaps.md` only where they touch presence (a route or a part that is missing).

| Id | Name (section 4) | Registered in code (file) | User doc page | Note |
|---|---|---|---|---|
| T-P01 | Scene Editor | `Shell/SceneWorkspace.cs:15` | `T-P01-scene-editor.md` | Slots registered as `T-P01:Hierarchy` (`Windows/SceneEditor/HierarchyPanel.cs:24`), `T-P01:Inspector` (`InspectorPanel.cs:24`), `T-P01:Viewport` (`ViewportPanel.cs:22`), `T-P01:Assets` (`AssetsPanel.cs:19`); parts of the T-P01 frame. Gap: Paint / Fill / Erase only highlight the tool |
| T-P02 | Play Mode | `Windows/Play/PlayModePage.cs:21` | `T-P02-play-mode.md` | |
| T-P03 | Tilemap Editor | `Windows/Tilemap/TilemapEditorPage.cs:18` | `T-P03-tilemap-editor.md` | Bottom tabs Layers, Auto-tile rules, Assets as in the frame |
| T-P04 | Script Editor | `Windows/Scripts/ScriptEditorPage.cs:28` | `T-P04-script-editor.md` | Bottom tabs Problems, Terminal, Output (P11.54) |
| T-P05 | Console panel | `Windows/SceneEditor/ConsolePanel.cs:85` (Panel) | `T-P05-console-panel.md` | Gap: game log lines go to Play Mode's console only |
| T-P06 | Animation panel | `Windows/SceneEditor/AnimationPanel.cs:17` (Panel) | `T-P06-animation-panel.md` | Gap: speed and frame order cannot be changed |
| T-P07 | Database | `Windows/Database/DatabasePage.cs:19` | `T-P07-database.md` | Import CSV opens an OS file picker before T-D19 (see extras) |
| T-P08 | Sprite Editor | `Windows/Sprite/SpriteEditorPage.cs:26` | `T-P08-sprite-editor.md` | Gap: drop zone opens an OS picker, not T-D04; PNG only |
| T-P09 | Font Manager | `Windows/Font/FontManagerPage.cs:22` | `T-P09-font-manager.md` | |
| T-P10 | Palette Manager | `Windows/Palette/PaletteManagerPage.cs:21` | `T-P10-palette-manager.md` | Gap: Import .pal / .gpl opens an OS picker, not T-D04 |
| T-P11 | Build Report | `Windows/Build/BuildReportPage.cs:21` | `T-P11-build-report.md` | |
| T-P12 | Launcher | `Windows/Launcher/LauncherPage.cs:22` | `T-P12-launcher.md` | "What's new" is in the frame (P11.07) |
| T-P13 | World Map | `Windows/WorldMap/WorldMapPage.cs:26` | `T-P13-world-map.md` | |
| T-P14 | Localization | `Windows/Localization/LocalizationPage.cs:23` | `T-P14-localization.md` | |
| T-P15 | Audio | `Windows/Audio/AudioPage.cs:23` | `T-P15-audio.md` | |
| T-P16 | Dialogue Editor | `Windows/Dialogue/DialogueEditorPage.cs:24` | `T-P16-dialogue-editor.md` | |
| T-P17 | Logic Graph | `Windows/LogicGraph/LogicGraphPage.cs:39` | `T-P17-logic-graph.md` | |
| T-P18 | Scripting API | `Windows/Scripts/ScriptingApiPage.cs:17` | `T-P18-scripting-api.md` | Extra "See also" button row in the page body (not in the frame; see extras) |
| T-P19 | Debugger | `Windows/Debugger/DebuggerPage.cs:22` | `T-P19-debugger.md` | Same class registers T-P23 and T-P24. Gap: breakpoints cannot be added |
| T-P20 | Launcher · Templates | `Windows/Launcher/LauncherPage.cs:23` | `T-P20-launcher-templates.md` | Same class as T-P12 |
| T-P21 | Launcher · Learn | `Windows/Launcher/LauncherPage.cs:24` | `T-P21-launcher-learn.md` | Same class as T-P12 |
| T-P22 | Scene Editor · Empty project | `Windows/Launcher/EmptyProjectPage.cs:23` | `T-P22-scene-editor-empty-project.md` | Caption mentions a template choice; neither the frame nor the app has one |
| T-P23 | Debugger · Memory | `Windows/Debugger/DebuggerPage.cs:23` | `T-P23-debugger-memory.md` | |
| T-P24 | Debugger · Palettes | `Windows/Debugger/DebuggerPage.cs:24` | `T-P24-debugger-palettes.md` | |
| T-P25 | SFX Designer | `Windows/Audio/SfxDesignerPage.cs:24` | `T-P25-sfx-designer.md` | |
| T-P26 | Auto-tile Rules panel | `Windows/Tilemap/TilemapBottomPanel.cs:21` (Panel) | `T-P26-auto-tile-rules-panel.md` | |
| T-D01 | New Project | `Windows/Launcher/NewProjectDialog.cs:17` | `T-D01-new-project.md` | Eight template cards, the frame shows six (accepted by D-09). Location opens an OS folder picker |
| T-D02 | New Scene | `Windows/SceneEditor/SceneDialogs.cs:17` | `T-D02-new-scene.md` | |
| T-D03 | Scene Settings | `Windows/SceneEditor/SceneDialogs.cs:129` | `T-D03-scene-settings.md` | |
| T-D04 | Import Asset | `Windows/Import/ImportAssetDialog.cs:24` | `T-D04-import-asset.md` | Browse opens an OS file picker. Gap: not opened from Sprite Editor drop zone or Palette Manager import |
| T-D05 | Export ROM | `Windows/Build/ExportRomDialog.cs:19` | `T-D05-export-rom.md` | Folder field opens an OS folder picker |
| T-D06 | Build ROM | `Windows/Build/BuildRomDialog.cs:20` | `T-D06-build-rom.md` | |
| T-D07 | Flash to Cartridge | `Windows/Build/FlashDialog.cs:18` | `T-D07-flash-to-cartridge.md` | GBxCart RW writing gated by D-12 |
| T-D08 | Project Settings (Input tab) | `Windows/Settings/ProjectSettingsDialog.cs:24` | `T-D08-project-settings.md` | Fields match the frame (see Settings) |
| T-D09 | Preferences (Emulator tab) | `Windows/Settings/PreferencesDialog.cs:29` | `T-D09-preferences.md` | Fields match the frame. Toolchain "Change…" opens an OS folder picker |
| T-D10 | Keyboard Shortcuts | `Shell/Dialogs.cs:33` | `T-D10-keyboard-shortcuts.md` | Lists the ten shortcuts of the frame |
| T-D11 | About | `Windows/Menus/AboutDialog.cs:15` | `T-D11-about.md` | Licenses and Website open outside the app (D-13) |
| T-D12 | Unsaved Changes | `Shell/Dialogs.cs:98` | `T-D12-unsaved-changes.md` | |
| T-D13 | New Sprite | `Windows/Sprite/NewSpriteDialog.cs:20` | `T-D13-new-sprite.md` | "Import PNG…" opens an OS file picker |
| T-D14 | New Script | `Windows/Scripts/NewScriptDialog.cs:19` | `T-D14-new-script.md` | |
| T-D15 | New Font | `Windows/Font/FontDialogs.cs:23` | `T-D15-new-font.md` | |
| T-D16 | Add Characters | `Windows/Font/FontDialogs.cs:181` | `T-D16-add-characters.md` | |
| T-D17 | New Table | `Windows/Database/NewTableDialog.cs:18` | `T-D17-new-table.md` | "CSV file…" opens an OS file picker |
| T-D18 | Add Field | `Windows/Database/AddFieldDialog.cs:19` | `T-D18-add-field.md` | |
| T-D19 | Import CSV | `Windows/Database/ImportCsvDialog.cs:20` | `T-D19-import-csv.md` | Google Sheets only as a CSV export (D-13) |
| T-D20 | Add Language | `Windows/Localization/AddLanguageDialog.cs:16` | `T-D20-add-language.md` | |
| T-D21 | New Speaker | `Windows/Dialogue/NewSpeakerDialog.cs:22` | `T-D21-new-speaker.md` | Portrait opens an OS file picker |
| T-D22 | Opening Project | `Windows/Launcher/OpeningProjectDialog.cs:72` | `T-D22-opening-project.md` | |
| T-D23 | Build ROM · Failed | `Windows/Build/BuildRomDialog.cs:184` | `T-D23-build-rom-failed.md` | |
| T-D24 | Import Asset · Errors | `Windows/Import/ImportErrorsDialog.cs:16` | `T-D24-import-asset-errors.md` | |
| T-S01 | Project Settings · General | `Windows/Settings/ProjectSettingsDialog.cs:25` | `T-S01-project-settings-general.md` | Fields match the frame |
| T-S02 | Project Settings · Save data | `Windows/Settings/ProjectSettingsDialog.cs:26` | `T-S02-project-settings-save-data.md` | Fields match the frame |
| T-S03 | Project Settings · Build | `Windows/Settings/ProjectSettingsDialog.cs:27` | `T-S03-project-settings-build.md` | Fields match the frame |
| T-S04 | Project Settings · Scripting | `Windows/Settings/ProjectSettingsDialog.cs:28` | `T-S04-project-settings-scripting.md` | Fields match the frame |
| T-S05 | Project Settings · Localization | `Windows/Settings/ProjectSettingsDialog.cs:29` | `T-S05-project-settings-localization.md` | Fields match the frame |
| T-S06 | Preferences · Appearance | `Windows/Settings/PreferencesDialog.cs:30` | `T-S06-preferences-appearance.md` | Fields match the frame |
| T-S07 | Preferences · Editor | `Windows/Settings/PreferencesDialog.cs:31` | `T-S07-preferences-editor.md` | Fields match the frame |
| T-S08 | Preferences · Paths | `Windows/Settings/PreferencesDialog.cs:32` | `T-S08-preferences-paths.md` | Fields match the frame |
| T-S09 | Preferences · Shortcuts | `Windows/Settings/PreferencesDialog.cs:33` | `T-S09-preferences-shortcuts.md` | Fields match the frame |
| T-O01 | Scene Switcher | `Windows/SceneEditor/ScenePopovers.cs:21` | `T-O01-scene-switcher.md` | |
| T-O02 | Video Mode | `Windows/SceneEditor/ScenePopovers.cs:121` | `T-O02-video-mode.md` | |
| T-O03 | Grid & Snap | `Windows/SceneEditor/ScenePopovers.cs:204` | `T-O03-grid-and-snap.md` | |
| T-O04 | Asset Picker | `Windows/SceneEditor/ScenePopovers.cs:284` | `T-O04-asset-picker.md` | Gap: from the Tileset dropdown it opens on the Sprites tab |
| T-O05 | Component Picker | `Windows/SceneEditor/ScenePopovers.cs:402` | `T-O05-component-picker.md` | |
| T-O06 | Budget Details | `Windows/SceneEditor/ScenePopovers.cs:502` | `T-O06-budget-details.md` | |
| T-O07 | Command Palette | `Shell/CommandPalette.cs:23` | `T-O07-command-palette.md` | Lists commands and project files (P10.12) |
| T-O08 | Add Node | `Windows/LogicGraph/AddNodePopover.cs:20` | `T-O08-add-node.md` | Opened from the canvas (`Windows/LogicGraph/LogicGraphPage.cs:371`). Gap: Nodes › Add node… is a search box, not this popover |
| T-O09 | Link Settings | `Windows/WorldMap/LinkSettingsPopover.cs:18` | `T-O09-link-settings.md` | |
| T-O10 | Palette Effect | `Windows/Palette/PaletteEffectPopover.cs:20` | `T-O10-palette-effect.md` | |
| T-O11 | Find & Replace | `Windows/Scripts/FindReplacePopover.cs:99` | `T-O11-find-and-replace.md` | |
| T-O12 | Record Shortcut | `Windows/Settings/RecordShortcutPopover.cs:23` | `T-O12-record-shortcut.md` | |
| T-O13 | Tutorial Steps | `Windows/Launcher/Tutorials.cs:306` | `T-O13-tutorial-steps.md` | |
| T-L01 | Scene Editor · Light | none of its own: T-P01 drawn with `Theme/Tokens.Light.axaml` through `Services/ThemeService.cs` | none (T-S06 page describes the Light theme) | SHOT `docs/evidence/app/T-L01.png` exists |
| T-L02 | New Project · Light | none of its own: T-D01 in Light (`Windows/Launcher/NewProjectDialog.cs:13` names it) | none | SHOT `docs/evidence/app/T-L02.png` exists |
| T-L03 | Preferences · Appearance · Light | none of its own: T-S06 in Light (`Windows/Settings/PreferencesDialog.cs:109` names it) | none | SHOT `docs/evidence/app/T-L03.png` exists |
| T-L04 | Every other window in Light tokens | a rule, not a window (`Theme/Literals.axaml:2` names it) | none | Not checked in this record |

## Menus

Figma items are the ones in `docs/evidence/figma/menus.json` (the export of Editor Map boards 2 and 4). Paths are under
`editor/AgbStudio.Editor/`.

| Id | Menu | Where in code | Figma items missing in the app | Items in the app not in Figma | Note |
|---|---|---|---|---|---|
| T-M01 | File menu | `Commands/MenuModel.cs:47` | none | none | Test compares it with menus.json |
| T-M02 | Edit menu | `Commands/MenuModel.cs:55` | none | none | Test compares it with menus.json |
| T-M03 | Assets menu | `Commands/MenuModel.cs:62` | none | none | Test compares it with menus.json |
| T-M04 | Scene menu | `Commands/MenuModel.cs:67` | none | none | Test compares it with menus.json |
| T-M05 | Build menu | `Commands/MenuModel.cs:73` | none | none | Test compares it with menus.json |
| T-M06 | Tools menu | `Commands/MenuModel.cs:79` | none | none | Test compares it with menus.json |
| T-M07 | Help menu | `Commands/MenuModel.cs:85` | none | none | Test compares it with menus.json |
| T-M08 | Open Recent | `Commands/MenuModel.cs:49`, `Shell/MainWindow.cs:79` | none | none | Recent projects, separator, Clear Recent; generated, so not compared with menus.json by a test |
| T-M09 | Create | `Commands/MenuModel.cs:31` | none | none | Test compares it with menus.json |
| T-M10 | Add Actor | `Commands/MenuModel.cs:38` | none | none | Test compares it with menus.json |
| T-M11 | Add to Scene | `Windows/SceneEditor/SceneCommands.cs:69` | none | none | Enemy submenu lists "Enemy" plus each enemy sprite of the project; menus.json has no submenu contents, so that list is not checked |
| T-M12 | Actor Context Menu | `Windows/SceneEditor/SceneCommands.cs:147` | none | none | |
| T-M13 | Asset Context Menu | `Windows/SceneEditor/SceneCommands.cs:182` | none | none | |
| T-M14 | Component Menu | `Windows/SceneEditor/SceneCommands.cs:224` | none | none | |
| T-M15 | Zoom | `Windows/SceneEditor/SceneCommands.cs:236` | none | none | |
| T-M16 | Animation List | `Windows/SceneEditor/SceneCommands.cs:264` | none | none | Rows are the selected sprite's animations |
| T-M17 | Project Context Menu | `Windows/Launcher/ProjectContextMenu.cs:18` | none | none | "Delete project…" opens a dialog that has no frame (see extras) |
| T-M18 | Play Speed | `Windows/Play/PlayCommands.cs:48` | none | none | |
| T-M19 | OAM Size | `Windows/Sprite/OamSizeMenu.cs:19` | none | none | |
| T-M20 | Add Layer | `Windows/Tilemap/MapLibrary.cs:130` | none | none | |
| T-M21 | Node Context Menu | `Windows/LogicGraph/LogicGraphPage.cs:498` | none | an extra separator between "Show generated C#" and "Delete" | Figma has one separator (after Collapse), the app two (line 500); `docs/evidence/app/T-M21.png` shows Delete cut off at the bottom of the 175 px frame. The test checks labels only, so it does not catch this |
| T-M22 | Scene Context Menu | `Windows/WorldMap/WorldMapPage.cs:273`, `:280` | none | none | |
| T-M23 | Add to Dialogue | `Windows/Dialogue/DialogueMenus.cs:15` | none | none | Test compares it with menus.json |
| T-M24 | Command | `Windows/Dialogue/DialogueMenus.cs:23` | none | none | Test compares it with menus.json |

Commands with no Figma menu item: `play.pause`, `play.frameStep`, `play.stop` (`Windows/Play/PlayCommands.cs:38-45`),
`script.find`, `script.replace`, `script.openExternally` (`Windows/Scripts/ScriptEditorPage.cs:687-693`),
`scene.tool.select`, `scene.tool.move`, `scene.tool.paint`, `scene.toggleGrid`, `scene.focusSelection`,
`script.goToLine` (`Commands/Catalog.cs:102-107`). None of them is in a menu. They can be found in the Command Palette
and the Preferences › Shortcuts search. Each one is a Figma button or a shortcut shown in T-D10, T-S09 or T-O12 (the
Play Control buttons, T-C20, T-C21, the Scene group of T-D10, "conflicts with Go to line" in T-S09). So they add no UI
of their own.

## Settings

The Figma frames were compared with the code field by field (presence only). Paths are under
`editor/AgbStudio.Editor/Windows/Settings/`. The side bar order matches the frames: Project Settings has General, Input,
Save data, Build, Scripting, Localization (`ProjectSettingsDialog.cs:32`). Preferences has Appearance, Editor, Paths,
Emulator, Shortcuts (`PreferencesDialog.cs:36`). Both use the Dialog Header with X and a Cancel / Save footer
(`SettingsDialog.cs:26`).

| Tab | Fields and buttons in the Figma frame | Code | Missing | Extra |
|---|---|---|---|---|
| T-S01 General | Game title, Version, ROM title, Game code, Maker, Start scene, Default font, Default language, Boot splash, Skip splash in Debug builds | `ProjectSettingsDialog.cs:117` | none | none |
| T-D08 Input | Table GBA button / Action name / Keyboard (emulator) / Gamepad; Let players remap buttons in the options menu | `ProjectSettingsDialog.cs:139` | none | none |
| T-S02 Save data | Save type None / SRAM 32 KB / Flash 64 KB / Flash 128 KB / EEPROM 8 KB; Field / Type / Size table with Add field; Slots; Autosave and its caption; slots meter; bytes per slot; Checksum each slot; code sample | `ProjectSettingsDialog.cs:200` | none | none |
| T-S03 Build | Configuration Debug / Release; Optimise; Output; Warn ROM >; Save type; Release options (Compress graphics with LZ77, Strip Debug.Log calls, Keep asserts, Run in mGBA after build); Toolchain card with Change in Preferences… | `ProjectSettingsDialog.cs:280` | none | none |
| T-S04 Scripting | Language, Generated, Namespace; Hot reload in Play mode, Treat warnings as errors, Show inline hints in the editor; Analyzer rules GAE001-GAE006 with Severity | `ProjectSettingsDialog.cs:340` | none | none |
| T-S05 Localization | Language meters, Add language; Font per language; Source, Fallback, Thai wrap and caption; Show missing keys as [key] in Debug; Fail the build on missing strings | `ProjectSettingsDialog.cs:380` | none | none |
| T-S06 Appearance | Theme cards Dark / Light / System; five accent swatches; UI scale; Code font; Reduce motion; Show pixel grid at 400% zoom and above | `PreferencesDialog.cs:114` | none | none |
| T-S07 Editor | Autosave, Undo history, Default snap, Open last project on start, Show collision overlay in Scene, Show GBA budget in Inspector; Built-in / External, Editor, editor list caption, Executable, Open .cs with, Generate .csproj for IntelliSense, Hot reload when an external save is detected; Font, Tab size, Theme, Format on save, Autocomplete as you type, Underline analyzer warnings | `PreferencesDialog.cs:206` | none | none |
| T-S08 Paths | devkitPro, Projects folder, Script editor, Image editor, Tiled (each with status mark and Browse…); Build cache card with Clear cache | `PreferencesDialog.cs:281` | none | none |
| T-D09 Emulator | Core, Scale, Filter, Audio, Pause when unfocused, Fast-forward; Toolchain card with Change… | `PreferencesDialog.cs:387` | none | none |
| T-S09 Shortcuts | Reset to defaults; search "Search commands or press a key…"; Command / Keys / pencil table with the seven rows; conflict chip | `PreferencesDialog.cs:425` | none | none |

Settings that are stored but have no field (not visible, so not UI): `LastProject` (`Services/PreferencesStore.cs:34`),
`DevkitProNoticeShown` (`Windows/Settings/PreferencesFields.cs:35`) and `PixelGridMinZoom`
(`Services/PreferencesStore.cs:21`, only shown inside the T-S06 label). Fields that are stored but have no effect are
listed in `docs/evidence/editor-gaps.md` (T-S04, T-S06, T-S07, T-S08, T-D09). They are behaviour gaps, not extra
settings.

## Items in the app that are not in section 4

Paths are under `editor/AgbStudio.Editor/` unless they start with `tools/` or `docs/`.

| What | Where (file:line) | Classification | Proposed action |
|---|---|---|---|
| "Delete {name}?" confirmation dialog (Project Context Menu › Delete project…): folder, counts, Cancel / Delete project | `Windows/Launcher/ProjectContextMenu.cs:52`, shown from `Windows/Launcher/LauncherService.cs:400` | not in Figma (needs a Scope log row). The code comment says Figma has no frame. P11.05 asks for the question, so the item needs it | Add a Scope log row. Ask the user to accept it as built from Dialog Header / Dialog Footer (like T-D12), or have a frame designed and given a T-D id |
| "devkitPro not found" / "A build tool is missing" dialog: folders searched or missing paths, "devkitPro installer page" button, Open Preferences · Paths | `Windows/Settings/ToolchainHelp.cs:77`, shown from `:41-68` (first start without devkitPro, and a build with missing tools) | not in Figma (needs a Scope log row). Comment says Figma has no frame. P16.01 and G-ERR.05 ask for the behaviour | Add a Scope log row; design a frame and a T-D id, or accept it as built from Figma components |
| "See also" heading and a row of Ghost buttons at the end of each Scripting API page | `Windows/Scripts/ScriptingApiPage.cs:161-176` | not in Figma (needs a Scope log row). The T-P18 frame lists "See also" only under On this page. Noted in `docs/user/windows/T-P18-scripting-api.md:34`, but not in `editor-gaps.md` or the Scope log | Remove the row (keep the On this page entry), or log it and ask the user |
| OS file and folder dialogs outside 4.11 (11 entry points): Export ROM › Folder; New Project › Location; Import Asset › browse; New Table › CSV file…; Database › Import CSV (picker before T-D19); Import CSV › choose file; New Speaker › portrait; New Sprite › Import PNG…; Sprite Editor › drop zone; Palette Manager › Import / Export .pal; Preferences › Emulator › Change… | `Windows/Build/ExportRomDialog.cs:82`, `Windows/Launcher/NewProjectDialog.cs:160`, `Windows/Import/ImportAssetDialog.cs:77`, `Windows/Database/NewTableDialog.cs:49`, `Windows/Database/DatabasePage.cs:419`, `Windows/Database/ImportCsvDialog.cs:53`, `Windows/Dialogue/NewSpeakerDialog.cs:138`, `Windows/Sprite/NewSpriteDialog.cs:53`, `Windows/Sprite/SpriteEditorPage.cs:112`, `Windows/Palette/PaletteManagerPage.cs:70-71`, `Windows/Settings/PreferencesDialog.cs:404` | not in section 4 (needs a Scope log row). Each sits behind a Figma control, but 4.11 lists only T-Y01..T-Y04. Three replace a Figma route: the Sprite Editor drop zone (T-C13) and Palette Manager import (T-C29) should open T-D04 (already in `editor-gaps.md`), and Database › Import CSV (T-C34) shows a picker before T-D19 | One Scope log row; extend 4.11 with T-Y rows for the ones that stay; fix T-C13 and T-C29 as `editor-gaps.md` says; decide whether T-C34 may show the picker first |
| In-game screens: boot splash, title screen, pause menu, options (Figma frames IG-01..IG-04, D-23) | runtime `agb::ui`, project data `ProjectFile.Ui` and `SceneFile.Menu`; no editor window. The editor only uses `Settings.Ui` for fonts and the title scene (`Windows/Font/FontDialogs.cs:76`, `Windows/SceneEditor/SceneFiles.cs:214`) | already in the Scope log (row of 2026-10-31, "pending: keep or remove the frames"). Section 4 has no T-id for them | When the user decides, add T-ids for the four frames to section 4 (or remove them) |
| Extra separator in Node Context Menu (between "Show generated C#" and "Delete") | `Windows/LogicGraph/LogicGraphPage.cs:500` | differs from Figma (fix; no new item) | Remove the second `SeparatorEntry.Instance`; make `LogicGraphTests.RightClickNode_ContextMenu…` compare separators too |
| Component Gallery window, started with `AGBStudio --gallery` | `App.axaml.cs:19-24`, `Gallery/ComponentGallery.cs:12`; documented in `docs/EDITOR-ARCHITECTURE.md:17` | infra (allowed: SHOT and component review tool for 4.16; not reachable from the UI) | Keep. Record it as infra in the Scope log, or leave the switch out of the release package (P16) |
| "{id} {name} is not built yet" notice for a window id with no registration | `Shell/WindowService.cs:154-158`, `:162` | infra (allowed: development fallback). Unreachable today: all 72 window ids are registered, and T-M01..T-M10 open through the title bar | Remove before Phase 16, or add a test that every `WindowCatalog` id is registered so the notice can never show |
| Slot ids `T-P01:Hierarchy`, `T-P01:Inspector`, `T-P01:Viewport`, `T-P01:Assets` | `Windows/SceneEditor/HierarchyPanel.cs:24`, `InspectorPanel.cs:24`, `ViewportPanel.cs:22`, `AssetsPanel.cs:19` | allowed (parts of the T-P01 frame, reused in T-P02, T-P03, T-P08, T-P22) | none |
| Open lists of Select and Field Dropdown controls (a context menu of the choices, current one checked) | `Windows/Settings/SettingsUi.cs:74`, `Windows/Scripts/PageChrome.cs:117`, `Windows/Tilemap/PageChrome.cs:170`, `Windows/Database/Pickers.cs:29`, `Windows/Build/BuildReportPage.cs:322`, `Windows/Build/FlashDialog.cs:56`, `Windows/Launcher/NewProjectDialog.cs:166`, `Windows/SceneEditor/SceneDialogs.cs:110`, `:205`, `Windows/Sprite/NewSpriteDialog.cs:136`, `Windows/Sprite/SpriteEditorPage.cs:841`, `Windows/Palette/PaletteEffectPopover.cs:191` | allowed (behaviour of the Select and Field components in 4.16; Figma draws only the closed control) | none needed. Optionally one Scope log row saying the open list reuses Menu Item, or design an open Select variant |
| Commands with no Figma menu item, visible in the Command Palette and the Preferences › Shortcuts search | `Windows/Play/PlayCommands.cs:38-45`, `Windows/Scripts/ScriptEditorPage.cs:687-693`, `Commands/Catalog.cs:102-107` | allowed (each is a Figma button or a T-D10 / T-S09 shortcut; see Menus) | none |
| Destinations outside the app: About › Licenses (opens `docs/THIRD_PARTY.md`), Website, Help › Report a Bug…, Check for Updates, Launcher › Learn documentation links | `Windows/Menus/AboutDialog.cs:44`, `:48`, `Windows/Menus/MenuActions.cs:121`, `:129`, `Windows/Launcher/LauncherPage.cs:512`, `:520` | allowed (Figma buttons; destinations decided in D-13) | none |
| `editor.json` keys `bugUrl`, `updateFeed`, `websiteUrl`, `communityUrl`, `releaseNotesUrl`, `devkitProUrl` | `Windows/Menus/EditorLinks.cs:12-20` | infra (allowed: D-13 configuration file, no UI). `devkitProUrl` only serves the dialog in row 2 | Keep; if row 2 is not accepted, drop `devkitProUrl` with it |
| Stored preferences without a field: `LastProject`, `DevkitProNoticeShown`, `PixelGridMinZoom` | `Services/PreferencesStore.cs:21`, `:34`, `Windows/Settings/PreferencesFields.cs:35` | infra (allowed: state for T-S07 "Open last project on start", the row 2 notice and the T-S06 label) | none |
| `agb` command line: `romtest`, `runtime-test`, `make-test-audio`, `build`, `sample`, `template`, `package`, `text-preview`, `lint` | `tools/agb/Program.cs:21-29` | infra (allowed: build, test and package toolchain; not editor UI) | none for G-SCOPE.02; if `agb.exe` ships in the package, say so in the release notes |
| New Project shows eight template cards (the T-D01 frame shows six) | `Windows/Launcher/NewProjectDialog.cs`, `Windows/Launcher/TemplateCatalog.cs` | allowed (decided in D-09; design issue 1.4 item 4) | none; tick 1.4 item 4 when the user confirms D-09 |

## What the tests already cover

Read only, not run for this record.
- `tests/AgbStudio.Editor.Tests/Shell/WindowServiceTests.cs:42` `Catalog_KnowsEveryFigmaWindow` checks two things: the
  catalog holds 96 entries (26 + 24 + 9 + 13 + 24), and two sample entries are right. It does not check that every
  catalog id has an `[EditorWindow]` or `[EditorMenu]`. It also does not check that no window outside the catalog is
  registered. `Discovery_FindsShellWindowsInTheEditor` (`:50`) checks only T-P01, T-O07, T-D10 and T-D12.
- `tests/AgbStudio.Editor.Tests/Windows/Menus/MenuActionTests.cs:128`
  `EveryTitleBarMenuCommand_ActsOrOpensItsWindow` checks three things. Its table matches the command set of
  `MenuModel` exactly, so an extra command in T-M01..T-M10 fails it. Every such command opens its window or has a
  handler. The title bar shows each one.
- `tests/AgbStudio.Editor.Tests/Shell/CommandAndShortcutTests.cs` `Menu_MatchesFigmaItemsShortcutsIconsAndKinds`
  compares T-M01..T-M07, T-M09 and T-M10 with `menus.json` item by item (labels, separators, shortcuts, icons, kinds).
  `Windows/Dialogue/DialogueWindowTests.cs:148` does the same for T-M23 and T-M24.
- T-M11..T-M22 are checked against hard-coded label lists in `Windows/SceneEditor/PopoverAndMenuTests.cs`,
  `InspectorTests.cs`, `Windows/Launcher/LauncherE2ETests.cs`, `Windows/Play/PlayModeE2ETests.cs`,
  `Windows/Sprite/SpriteEditorE2ETests.cs`, `Windows/Tilemap/TilemapE2ETests.cs`, `Windows/LogicGraph/LogicGraphTests.cs`
  and `Windows/WorldMap/WorldMapTests.cs`, not against `menus.json`. The T-M21 test skips separators.
- `tests/AgbStudio.Editor.Tests/Shot/ShotTests.cs` makes every T- frame in `docs/evidence/figma/manifest.json` have an
  app render (lines 83-94). This proves a render source per id, not a registration.
- Suggested test to make G-SCOPE.02 mechanical for windows and menus: every `WindowCatalog` id is registered, and every
  `[EditorWindow]` / `[EditorMenu]` id is either in `WindowCatalog` or of the form `T-P01:<slot>`.

## Not checked

- The contents of each page, dialog and popover beyond its registration: every button, chip, panel and tab. Only the
  11 settings tabs were compared field by field. T-P01, T-P03, T-P08, T-P12, T-P18, T-P19, T-D01, T-D05, T-D10, T-D13,
  T-D14 and T-D17 were looked at by eye against their frames. Layout is SHOT's job; this record does not claim the
  other windows have no extra control.
- The option lists inside dropdowns, for example Optimise -O0..-Os, Boot splash 1-5 s, UI scale 75%-200%, Autosave
  intervals and code editor themes. Each Figma frame shows only the selected value.
- Generated menu rows: the recent projects in T-M08, the Enemy submenu of T-M11 and the animations in T-M16.
  `menus.json` has no submenu contents.
- Button routes T-B01..T-B35 and T-C01..T-C52 (E2E), prototype flows T-X01..T-X03, tokens, type, icons and art
  T-K01..T-K05, the 47 components of 4.16, brand T-R01..T-R04, and the T-L04 colour-literal scan.
- The text of the 52 toast messages (`Toasts.Show` calls) and the tooltips. They use the Toast and Tooltip components,
  but their wording was not compared with Figma.
- Keyboard handling inside canvases (World Map, Logic Graph, Tilemap, Sprite) beyond the registered commands.
- Whether fields and buttons work. See `docs/evidence/editor-gaps.md`.
- The runtime ROM UI and the in-game screens IG-01..IG-04. They are not editor windows.
- Anything outside `editor/AgbStudio.Editor` except the `agb` entry point, which was only classified.
- Branches other than `fix-docs` at `24a26b6`. Nothing was built, run or tested for this record.
