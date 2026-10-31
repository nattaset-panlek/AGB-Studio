# G-SCOPE.02 review 2: app inventory against checklist section 4 (2026-10-31)

Scope. Snapshot: worktree `D:\devkitPro\AGB-Studio-wt\review3`, detached at `d1dc04a`. The first review,
`docs/evidence/scope-inventory-review.md`, was made at `24a26b6`. Between the two commits there are 188 commits; 61 of
them touch `editor/` or `tools/agb/` (122 files, +3142 / -642 lines). This record repeats that review on `d1dc04a`, lists
every app element that is not in section 4 of `docs/ENGINE-CHECKLIST.md`, marks what is new since `24a26b6`, and
re-checks every item of the first review. It is a presence check only. Nothing was built, run or tested, no Figma tool
was used, and no checklist box is ticked.

## Method

1. The same searches as the first review, run with `git grep` at `24a26b6` and at `d1dc04a` and compared by content
   (line numbers removed), so only additions and removals show: `\[EditorWindow\(`, `\[EditorMenu\(`,
   `\[EditorCommand\(`, `\[CommandHandler\(`, `new EditorCommand`; `Overlays\.Show`, `IOverlayContent`, `: FigmaDialog`,
   `new ContextMenu`, `new MenuFlyout`, `new Popup`, `new Window`, `StorageProvider`, `Open(File|Folder)PickerAsync`,
   `SaveFilePickerAsync`, `PickFolderAsync`, `PickFileAsync`, `PickSaveFileAsync`, `FileChooser`, `ShowInExplorer`,
   `links\.Open`, `Process\.Start`, `\.Launch\(`; `Args\?\.`, `GetCommandLineArgs`, `"--[a-z]`, `GetEnvironmentVariable`,
   `#if DEBUG`; every `"T-[A-Z][0-9]{2}` string; `PanelTab` / `NavItem` / `ListRow` labels; `[EditorStartup]`;
   `Toasts.Show`, `ToolTip.SetTip`; `Windows.Open(` routes; comments matching
   `figma has no|not in figma|no frame|infra`.
2. The diff of every changed file under `editor/AgbStudio.Editor/` and of `tools/agb/Program.cs`
   (`git diff 24a26b6 d1dc04a`) read for added controls (`AgbButton`, `IconButton`, `CheckBox`, `TextField`,
   `PanelHeader`, sections, tooltips), keyboard and mouse handling (`Key.`, `KeyModifiers`, `OnKeyDown`, wheel, drag),
   routes, commands, settings, toasts and outside programs. The messages of the feature commits were read for the reason
   each change gives.
3. Compared with: section 4 of `docs/ENGINE-CHECKLIST.md` (4.1 to 4.17) and the Scope log (section 8, 48 rows) at
   `d1dc04a`; `docs/DECISIONS.md` (D-01 to D-26); `docs/evidence/figma/menus.json` (all 48 menu items that carry a
   shortcut), `captions.json` (the T-D10 caption), and the frame exports `T-D10.png`, `T-S09.png`, `T-O12.png`
   (shortcuts), `T-P19.png` (Debugger), `T-P09.png`, `T-P10.png`, `T-P11.png`, `T-O01.png`, `T-S06.png`; also
   `docs/evidence/editor-gaps.md`, `docs/evidence/audit-p10-p16.md` and `docs/evidence/review-final-sweep.md`.
4. "Approved". All 48 Scope log rows have the user decision "pending", and `docs/DECISIONS.md` has one user decision
   (D-06); the rest are "decided (default)". So this record says "logged" when an item has a Scope log row or a
   DECISIONS entry, and "not logged" when it has neither. Neither means the user approved it.

Search results at the two commits:

| Search | `24a26b6` | `d1dc04a` | Difference |
|---|---|---|---|
| `[EditorWindow]` / `[EditorMenu]` lines (72 windows, 4 T-P01 slots, 14 menus, 1 doc comment) | 91 | 91 | none |
| `[EditorCommand]` / `[CommandHandler]` lines | 72 | 75 | `script.toggleBreakpoint` (F9) added; handlers for `assets.create.font` and `assets.create.tileset` added |
| `Commands/Catalog.cs`, `Commands/MenuModel.cs` (title bar menus, 77 catalog commands) | | | unchanged |
| Menu entries of `[EditorMenu]` methods | | | T-M21 second separator removed; T-M11 Enemy submenu row got its bug icon |
| Dialog, overlay, popup, picker, Explorer, `Process.Start`, link and switch lines | 114 | 114 | no new call site; 2 lines changed text |
| Outside program launch through `ExternalEditorService.Launch` | 0 | 1 | `Windows/Settings/ExternalTools.cs:39` (row 11) |
| `Windows.Open(` routes | 153 | 161 | 8 new or changed routes (see "Routes and states") |
| `"T-xx"` strings | 101 | 101 | none |
| `PanelTab` / `NavItem` / `ListRow` labels | 26 | 26 | none |
| `[EditorStartup]` hooks | 2 | 4 | `PlayCommands.RequireRunningGame`, `ExternalTools.Wire` |
| `Toasts.Show(` call sites | 52 | 57 | +5 (wording not compared with Figma, as before) |
| `ToolTip.SetTip(` call sites | 31 | 36 | +5 (Fill, Erase, Pause / Step, Remove breakpoint, Tile collision) |
| Comments saying Figma has no design | | | 1 new: `Windows/SceneEditor/InspectorPanel.cs:122` (row 8) |
| Editor command line switches | `--gallery` | `--gallery` | none |
| `agb` commands and options | 9 | 9 | none; the usage text now lists them (row 26) |

## Elements in the app that are not in section 4 (at `d1dc04a`)

Paths are under `editor/AgbStudio.Editor/` unless they start with `tools/` or `docs/`. Rows 1-12 are new since
`24a26b6`, rows 13-27 are the first review's rows that are still there, row 28 was there at `24a26b6` but the first
review missed it.

| # | Element | Where in the code | How a user reaches it | Why it exists (code or commit) | Status | Proposed |
|---|---|---|---|---|---|---|
| 1 | Command "Toggle breakpoint" (`script.toggleBreakpoint`), shortcut F9, category Script. Listed in the Command Palette, in the Preferences › Shortcuts search and in a Script group of Keyboard Shortcuts | `Windows/Scripts/ScriptEditorPage.cs:725-726` (command), `:717-723` (`ToggleBreakpoint`); `Windows/Debugger/DebuggerModel.cs` (`ToggleBreakpoint`, `HasBreakpoint`) | Script Editor, caret on a line, F9; or Ctrl+K, "Toggle breakpoint" | `ac3f15a` (T-P19 / P13.06): "breakpoints are toggled in the Script Editor's line-number gutter or with F9". Asked for by `docs/evidence/audit-p10-p16.md` item 22. No Figma menu, caption, T-D10, T-S09 or T-O12 frame has F9 or this command | new, not logged (`review-final-sweep.md` item 9 already names it as missing from the Scope log) | Keep; one Scope log row for rows 1-5 ("Debugger breakpoints and views that have no frame") |
| 2 | Script Editor line-number gutter: a click toggles a breakpoint on that line; a red dot (`status/danger`, 4 px radius) marks the line | `Windows/Scripts/CodeEditor.cs:82-84`, `:569-575`, `:700-701`; `ScriptEditorPage.cs:153-154` | Script Editor (T-P04), click a line number | `ac3f15a`, as row 1. The T-P04 frame shows plain line numbers | new, not logged | Keep; Scope log (row 1) |
| 3 | Debugger › Breakpoints view in the centre: title "Breakpoints"; a table Breakpoint / Kind / Stops at with a check box and a remove (x) icon button per row (tooltip "Remove breakpoint"); a text field "A script line, for example PlayerController.cs:19" (Enter adds, a wrong entry shows an error in the help line) with a Secondary SM button "Add breakpoint"; a "Call stack" section with the pause reason | `Windows/Debugger/DebuggerTables.cs:63-148`; shown from `Windows/Debugger/DebuggerPage.cs:191-195` | Tools › Debugger (or the Debug button) › Views › Breakpoints | `ac3f15a`, P13.06 (a way to add breakpoints). T-P19 has "Breakpoints" in Views and the check-box list on the left, but no frame for the view. Before `ac3f15a` this view showed the VRAM view | new, not logged | Keep; Scope log (row 1); ask for a frame and a T-P id, or reduce the view to the T-P19 left list plus rows 1-2 |
| 4 | Debugger › Profiler view in the centre: "Frame time · last 120 frames" with fps, CPU and VBlank free; a 640 x 200 frame graph; a "This frame" table Part / Time / Of the frame budget | `DebuggerTables.cs:13-62`; shown from `DebuggerPage.cs:186-190` | Debugger › Views › Profiler | `ac3f15a`, P13.01. Before it, this view showed the VRAM view. No frame (T-P19 has the Profiler panel on the right, which stays) | new, not logged | Keep; Scope log (row 1) |
| 5 | Debugger › OAM sprites view: the VRAM view without the character blocks (the OAM table alone) | `Windows/Debugger/DebuggerViews.cs:33-41`; `DebuggerPage.cs:196-198` | Debugger › Views › OAM sprites | `ac3f15a`, P13.03. Made of a part of the T-P19 frame; no frame of its own | new, not logged | Keep; Scope log (row 1) |
| 6 | Keyboard Shortcuts (T-D10) footer: Ghost SM button "Edit in Preferences › Shortcuts" (pencil) at the left; it closes the dialog and opens Preferences › Shortcuts (T-S09) | `Shell/Dialogs.cs:58`, `:62`, `:72-76` | Help › Keyboard Shortcuts (Ctrl+/) | `634040f` (T-D10 / P11.63). The T-D10 caption says shortcuts are changed in Preferences › Shortcuts; the frame's footer has only Close, and P11.63 asks for search and groups only | new, not logged | Remove the button (T-S09 stays one click away through File › Preferences…); or log it if the user wants it |
| 7 | Keyboard Shortcuts (T-D10) rows beyond the frame: every command that has a shortcut, 43 rows at the default bindings (the frame's 10 in General and Scene, then 33 more), two more groups (Script, Launcher), and the two columns scroll | `Shell/Dialogs.cs:91-101` (`AllRows`), `:115-145` (`Fill`) | Help › Keyboard Shortcuts, scroll or search | `634040f`. The T-D10 caption says the dialog lists all shortcuts and can be searched. 32 of the 33 extra shortcuts are drawn elsewhere in Figma (menus.json, "Go to line" in T-S09 / T-O12, Ctrl+F / Ctrl+H in T-C21); the 33rd is F9 (row 1) | new, not logged | Keep (the caption asks for all shortcuts); one Scope log line saying the frame shows only the first 10; the F9 row goes with row 1 |
| 8 | Scene Editor multi-selection: Ctrl+click toggles and Shift+click selects a range of actors in the Hierarchy; Edit › Select All selects every actor; the viewport outlines every selected actor; the Inspector shows a header "N actors selected" (layers icon) above the first actor's fields, which are read-only | `Windows/SceneEditor/HierarchyPanel.cs:210-219`; `InspectorPanel.cs:69`, `:121-140`; `ViewportPanel.cs:415-416`; `SceneEditTarget.cs:61`; `SceneEditorModel.cs` (`SelectActors`, `ToggleActor`, `SelectAllActors`) | Scene Editor › Hierarchy, Ctrl+click or Shift+click an actor; or Edit › Select All (Ctrl+A) | `c45fee5` (T-P01, T-M02, P10.11 "select all across actors"). Code comment: "Figma has no Inspector state for it" (`InspectorPanel.cs:122`) | new, not logged | Keep (P10.11 needs it); Scope log; ask for an Inspector multi-selection state in Figma |
| 9 | Animation panel editing without a visible control: mouse wheel over the speed line (ticks of the animation, 1-60) or over a frame (that frame's ticks), drag a frame to move it, keys + / - and Ctrl+Left / Ctrl+Right while the panel has focus; each is one undo step written to the sprite meta. They are not commands, so they are not in Keyboard Shortcuts, Preferences › Shortcuts or the Command Palette and cannot be rebound | `Windows/SceneEditor/AnimationPanel.cs:72-76`, `:243-282` (`OnFrameReleased`, `OnFrameWheel`, `OnKeyDown`) | Scene Editor › Animation tab (T-P06) | `276d7f3` (T-P06 / P11.17); closes the first review's gap "speed and frame order cannot be changed". The T-P06 frame shows the speed as text only | new, not logged | Keep (P11.17); Scope log; ask for a visible control in Figma or accept the gestures |
| 10 | Font Manager: a glyph group "Missing from project text" (glyphs the project's strings need and the font lacks) at the end of the glyph group list, counted in the status item "N glyphs · M missing"; that status item is now clickable and selects the group of the missing glyphs | `Windows/Font/FontLibrary.cs:184-205`; `Windows/Font/FontManagerPage.cs:40`, `:73-74`, `:164-171` | Font Manager (T-P09): click the glyph status item, or open the glyph group list (row 28) | `7c9e857` (T-P09, T-D16). Section 4.9 asks for the missing-glyph warning in T-P09; the frame shows "128 glyphs · 3 missing" as plain status text and has no such group | new, not logged | Keep; Scope log |
| 11 | Outside programs opened from the app: a double-click in the Assets panel and a Command Palette file result open `.aseprite` / `.ase` files and PNGs under `assets/images` in the Image editor, and `.tmx` / `.tsx` files in Tiled, when Preferences › Paths names those programs | `Windows/Settings/ExternalTools.cs:13-41`; `Windows/SceneEditor/AssetActions.cs:212`; `Shell/CommandPalette.cs:125` | Assets panel, double-click such a file; or Ctrl+K and pick it | `adda780` (T-S08: "the image editor opens background images and .aseprite files, Tiled opens .tmx maps"). T-S08 has both path fields and the hint | new; logged (Scope log row "T-S08 ... Figma contradicts itself", pending; `editor-gaps.md` Still open, T-S08) | Keep; add "double-click and the Command Palette open these programs" to that T-S08 row |
| 12 | Crash guard: after an unhandled error the editor saves the changed documents, writes `%APPDATA%\AGBStudio\crash\crash-<time>.log` and, for an error on the UI thread, shows the error toast "AGB Studio hit an unexpected error and saved your changes..." | `Services/CrashGuard.cs:14-103` (toast `:53`); installed in `App.axaml.cs:27` | Only after an unhandled error | `972daed` (G-ERR.06 asks for it) | new; infra | Keep; none |
| 13 | "Delete {name}?" confirmation (folder, counts, Cancel / Delete project). Since `be27d95` it says the folder goes to the Recycle Bin | `Windows/Launcher/ProjectContextMenu.cs:52`, shown from `Windows/Launcher/LauncherService.cs:408` | Launcher › right-click a project › Delete project… | P11.05; code comment: Figma has no frame | logged (Scope log row G-SCOPE.02, pending) | As the first review: accept it as built from Dialog Header / Dialog Footer, or a frame and a T-D id |
| 14 | "devkitPro not found" / "A build tool is missing" dialog | `Windows/Settings/ToolchainHelp.cs:77`, shown from `:41-68` | First start without devkitPro; a build with missing tools | P16.01, G-ERR.05; code comment: Figma has no frame | logged (row G-SCOPE.02, pending) | As the first review |
| 15 | Scripting API "See also" heading and a row of Ghost buttons at the end of a page | `Windows/Scripts/ScriptingApiPage.cs:161-176` | Help › Scripting API, end of a page | none given; noted in `docs/user/windows/T-P18-scripting-api.md` | logged (row G-SCOPE.02, pending) | As the first review: remove the row (keep the On this page entry), or accept it |
| 16 | OS file and folder dialogs outside 4.11, 11 entry points (12 call sites): Export ROM › Folder; New Project › Location; Import Asset › browse; New Table › CSV file…; Database › Import CSV; Import CSV › choose file; New Speaker › portrait; New Sprite › Import PNG…; Sprite Editor › drop zone (click); Palette Manager › Import / Export; Preferences › Emulator › Change… | `Windows/Build/ExportRomDialog.cs:83`; `Windows/Launcher/NewProjectDialog.cs:166` (through `LauncherService.cs:106`); `Windows/Import/ImportAssetDialog.cs:91`; `Windows/Database/NewTableDialog.cs:49`; `Windows/Database/DatabasePage.cs:454`; `Windows/Database/ImportCsvDialog.cs:53`; `Windows/Dialogue/NewSpeakerDialog.cs:143`; `Windows/Sprite/NewSpriteDialog.cs:54`; `Windows/Sprite/SpriteEditorPage.cs:121`, `:243`; `Windows/Palette/PaletteManagerPage.cs:71-72`, `:196`; `Windows/Settings/PreferencesDialog.cs:420` | Each behind a Figma control | Since `5e6964d` the Sprite Editor drop zone and Palette Manager › Import open Import Asset (T-D04) after the picker, so they no longer replace the T-C13 / T-C29 route; the drop zone picker now offers PNG and Aseprite. Database › Import CSV still shows a picker before T-D19 | logged (row G-SCOPE.02, pending) | As the first review: extend 4.11 with T-Y rows for the ones that stay; decide whether T-C13 (click), T-C29 and T-C34 may show the picker first |
| 17 | In-game screens: boot splash, title, pause menu, options (Figma IG-01..IG-04) | runtime `agb::ui`; `ProjectFile.Ui`, `SceneFile.Menu`; no editor window | In the game ROM | D-23 | logged (Scope log row "1.4 item 6, D-20, D-23", pending: keep or remove the frames) | As the first review: T-ids in section 4 when the user decides |
| 18 | Component Gallery window, `AGBStudio --gallery` (since `be27d95` with the app icon) | `App.axaml.cs:19-24`, `Gallery/ComponentGallery.cs:12`; `docs/EDITOR-ARCHITECTURE.md:7`, `:20` | Command line only | SHOT and component review | infra | As the first review: keep, record as infra or leave the switch out of the package |
| 19 | Not-built notice; since `be27d95` the text is "{Name} is not in this version of AGB Studio yet." (no design id) | `Shell/WindowService.cs:154-158`, `:162`, text `:177` | Unreachable: all 72 window ids are registered | development fallback | infra | As the first review: remove before Phase 16, or a test that every `WindowCatalog` id is registered (still none: `WindowServiceTests.cs` is unchanged) |
| 20 | Slot ids `T-P01:Hierarchy`, `:Inspector`, `:Viewport`, `:Assets` | `Windows/SceneEditor/HierarchyPanel.cs`, `InspectorPanel.cs`, `ViewportPanel.cs`, `AssetsPanel.cs` (attributes unchanged) | Parts of T-P01, reused in T-P02, T-P03, T-P08, T-P22 | parts of the T-P01 frame | allowed | none |
| 21 | Open lists of Select and Field Dropdown controls (13 call sites, the same as before) | `Shell/WindowService.cs:149`; `Windows/Settings/SettingsUi.cs:74`; `Windows/Scripts/PageChrome.cs:117`; `Windows/Tilemap/PageChrome.cs:169`; `Windows/Database/Pickers.cs:29`; `Windows/Build/BuildReportPage.cs:325`; `Windows/Build/FlashDialog.cs:58`; `Windows/Launcher/NewProjectDialog.cs:172`; `Windows/SceneEditor/SceneDialogs.cs:110`, `:205`; `Windows/Sprite/NewSpriteDialog.cs:137`; `Windows/Sprite/SpriteEditorPage.cs:856`; `Windows/Palette/PaletteEffectPopover.cs:202` | Click a Select or Field Dropdown | Select / Field behaviour (4.16); Figma draws the closed control | allowed | none |
| 22 | Commands with no Figma menu item (10): `play.pause`, `play.stop`, `script.find`, `script.replace`, `script.openExternally`, `scene.tool.select`, `scene.tool.move`, `scene.tool.paint`, `scene.toggleGrid`, `script.goToLine` | `Windows/Play/PlayCommands.cs:47`, `:53`; `Windows/Scripts/ScriptEditorPage.cs:710`, `:713`, `:728`; `Commands/Catalog.cs:102-105`, `:107` | Preferences › Shortcuts search and Keyboard Shortcuts; all but `script.find` and `script.replace` also in the Command Palette | Each is a Figma button or a shortcut in T-D10, T-S09, T-O12 or T-C21. `script.toggleBreakpoint` is row 1 | allowed | none |
| 23 | Destinations outside the app: About › Licenses and Website, Help › Report a Bug… and Check for Updates, Launcher › Learn documents and Release notes | `Windows/Menus/AboutDialog.cs:44`, `:48`; `Windows/Menus/MenuActions.cs:121`, `:129`; `Windows/Launcher/LauncherPage.cs:515`, `:523` | The Figma buttons | D-13 | allowed | none |
| 24 | `editor.json` keys `bugUrl`, `updateFeed`, `websiteUrl`, `communityUrl`, `releaseNotesUrl`, `devkitProUrl` | `Windows/Menus/EditorLinks.cs:14-20` | File next to the exe, no UI | D-13; `devkitProUrl` serves row 14 | infra | As the first review |
| 25 | Stored preferences without a field: `LastProject`, `DevkitProNoticeShown`, `PixelGridMinZoom` | `Services/PreferencesStore.cs:21`, `:34`; `Windows/Settings/PreferencesFields.cs:35` | No UI | state for T-S07, row 14 and the T-S06 label | infra | none |
| 26 | `agb` command line: `romtest`, `runtime-test`, `make-test-audio`, `build`, `sample`, `template`, `package`, `text-preview`, `lint`. Since `9bcd354` a missing command or argument prints every command with its arguments | `tools/agb/Program.cs:24-32`, usage `:44-60` | `agb.exe` | build, test and package toolchain | infra | As the first review |
| 27 | New Project shows eight template cards (the T-D01 frame shows six) | `Windows/Launcher/NewProjectDialog.cs`, `Windows/Launcher/TemplateCatalog.cs` | File › New Project… | D-09 | allowed | none |
| 28 | Font Manager glyph group list: the caption next to "Glyphs" (the current group, for example "Thai consonants · click a glyph to edit") is a bare button that opens a list of the font's glyph groups | `Windows/Font/FontManagerPage.cs:261-262` (at `24a26b6`: `:246-247`); list from `Windows/Tilemap/PageChrome.cs:167` | Font Manager, click the caption next to "Glyphs" | none given; the T-P09 frame draws the caption as plain muted text with no chevron | missed by the first review; not logged | Keep (it is the only way to other glyph groups, and row 10 depends on it); Scope log, or a chevron in Figma |

## Routes and states of section 4 windows (no new element)

These changed since `24a26b6` but add no window, button, panel, menu item, command, shortcut or setting. They are listed
so the next review can see them.

| What | Where | Note |
|---|---|---|
| Unsaved Changes (T-D12) now has three titles: "Save changes before exiting?", "... before opening another project?", "... before closing the project?"; the third shows when New Project replaces an open project with unsaved files | `Shell/Dialogs.cs:153-176`; `Windows/Launcher/NewProjectDialog.cs` (`ConfirmUnsavedAsync(UnsavedReason.CloseProject)`) | P10.08 asks for "close"; the T-D12 "from" line in 4.2 lists exit, closing the app and opening another project. Suggest adding "creating a new project" to that line; the two new titles are not in Figma |
| Create › Font opens Font Manager, then New Font (T-D15) | `Windows/Font/FontManagerPage.cs:426` | T-D15 "from" lists only Font Manager › Fonts +; a T-M09 item action (`editor-gaps.md` Fixed) |
| Create › Tileset / Tilemap makes `maps/Map_NN.map` (and a blank 64 x 64 tileset when there is none), opens it in T-P03 and shows a toast | `Windows/Tilemap/MapLibrary.cs:169` | T-M09 item action |
| The "Analyzer: GBA rules on" chip opens Project Settings › Scripting (T-S04) instead of Input | `Windows/Scripts/ScriptEditorPage.cs:80` | fix |
| Logic Graph › Nodes search: a click, typing or Down opens Add Node (T-O08) | `Windows/LogicGraph/LogicGraphPage.cs:93-98` | the 4.8 route "Nodes › Add node…"; fix |
| Sprite Editor drop zone: a drop opens T-D04; a click opens the OS picker, then T-D04. Palette Manager › Import: OS picker, then T-D04 | `Windows/Sprite/SpriteEditorPage.cs:243`; `Windows/Palette/PaletteManagerPage.cs:196` | T-C13 / T-C29 routes; the pickers stay in row 16 |
| Pause and Step on the 15 page toolbars act on the running game, are disabled while nothing runs and show their tooltip also when disabled | `Shell/PlayButtons.cs`; `Windows/Play/PlayCommands.cs:40-45` | Figma buttons; fix |
| A `.scene` result of the Command Palette and World Map › Open switch the viewport to that scene | `Windows/SceneEditor/ViewportPanel.cs` (constructor) | T-O07 / T-M22; fix |
| Import Asset and Import Asset · Errors: footer hint "Importing n of m: file", Cancel (or closing) stops the import | `Windows/Import/ImportErrorsDialog.cs` (`ImportRun`) | G-ERR.04 state of T-D04 / T-D24 |
| Flash to Cartridge: stage "Cancelled" with a hint per stage; the cartridge line "Cartridge detected · 32 MB flash · SRAM 32 KB" | `Windows/Build/FlashDialog.cs`; `Windows/Build/CartridgeDevices.cs` | G-ERR.04, P11.60; text of T-D07 |
| Build ROM: closing it while it builds cancels the build ("Build cancelled", toast and Console line) | `Windows/Build/BuildRomDialog.cs`; `Services/ProjectServices.cs` | G-ERR.04 |
| Scene viewport: brush outline for Paint tiles / Fill / Erase; the pixel grid of T-S06; Grid & Snap › Tile collision disabled with the tooltip "Turned off in Preferences › Editor"; Inspector GBA Budget hidden by T-S07; tooltips "Fill" and "Erase" on the tool buttons | `ViewportPanel.cs`; `ScenePopovers.cs`; `InspectorPanel.cs`; `Shell/SceneWorkspace.cs` | T-P01 tools and T-S06 / T-S07 settings taking effect |
| Play Mode: with Hitboxes on, every moving actor gets a velocity arrow (`lit/velocity`) | `Windows/Play/GameView.cs` | as the T-P02 game view |
| Edit › Select All in Assets, Database rows and Dialogue lines | `AssetActions.cs`, `DatabasePage.cs`, `DialogueEditorPage.cs` | T-M02, P10.11 |
| App icon on the window and the exe | `Graphics/WindowIcons.cs` | T-R01 |
| 5 more toasts (52 to 57) and new Console lines (build cancelled, import skipped, game log lines) | various | wording not compared with Figma |

## Counts

- Elements not in section 4 at `d1dc04a`: 28 rows.
  - New since `24a26b6`: 12 (rows 1-12).
  - Carried over from the first review: 15 (rows 13-27; its 16 rows less the T-M21 separator, fixed in `be27d95`).
  - Present at `24a26b6` but missed by the first review: 1 (row 28).
- By status: not logged 11 (rows 1-10, 28); logged in the Scope log, pending 6 (rows 11, 13-17); infra 6 (rows 12, 18,
  19, 24, 25, 26); allowed as component behaviour, Figma buttons or shortcuts, or an earlier decision 5 (rows 20-23, 27).
- Proposed: keep and add to the Scope log 10 (rows 1-5, 7-10, 28; rows 1-5 can share one row); remove 1 (row 6);
  extend an existing Scope log row 1 (row 11); the first review's proposal stands 7 (rows 13-19); no action 9 (rows 12,
  20-27).
- New shortcuts and gestures not in Figma: 1 bound command shortcut (F9), 4 panel keys without a command (+, -,
  Ctrl+Left, Ctrl+Right in the Animation panel), 2 click modifiers (Ctrl+click, Shift+click in the Hierarchy), 2 wheel
  gestures and 1 drag (Animation panel), 1 gutter click (Script Editor).
- Fixed since the first review: the T-M21 separator, and 9 gaps its window table named (T-P01 Paint / Fill / Erase,
  T-P05 game logs, T-P06 speed and frame order, T-P08 drop zone, T-P10 palette import, T-P19 breakpoints, T-D04 routes,
  T-O04 Tileset tab, T-O08 Nodes › Add node…).
- Section 4 windows: 72 of 72 still registered, in the same classes; 96 `WindowCatalog` entries; no window outside it.

## Re-check of the first review's items

| First review item | At `d1dc04a` |
|---|---|
| Delete project dialog (`ProjectContextMenu.cs:52`, `LauncherService.cs:400`) | Still there; shown from `LauncherService.cs:408`; text now says the folder goes to the Recycle Bin (row 13) |
| devkitPro / tool missing dialog (`ToolchainHelp.cs:77`, `:41-68`) | Still there, same lines (row 14) |
| Scripting API "See also" row (`ScriptingApiPage.cs:161-176`) | Still there, same lines (row 15) |
| 11 OS file / folder dialog entry points | Still 11 entry points, 12 call sites; lines moved (see row 16); the Sprite Editor drop zone and Palette Manager import now go on to T-D04 |
| In-game screens | Unchanged (row 17) |
| Extra separator in the Node Context Menu (T-M21) | Fixed in `be27d95`: one separator after Collapse (`Windows/LogicGraph/LogicGraphPage.cs:513-518`); the commit says the test now checks separators |
| Component Gallery `--gallery` | Still there, same lines; has the app icon (row 18) |
| Not-built notice (`WindowService.cs:154-158`, `:162`) | Still there, same lines; text no longer shows the design id (row 19) |
| Slot ids | Unchanged (row 20) |
| Open lists of Select and Field Dropdown | Same 13 call sites, lines moved (row 21) |
| Commands with no Figma menu item (12) | 10, not 12: see below (row 22); line numbers moved to `PlayCommands.cs:47-53` and `ScriptEditorPage.cs:710-728` |
| Destinations outside the app | Same; Launcher lines now `LauncherPage.cs:515`, `:523` (row 23) |
| `editor.json` keys (`EditorLinks.cs:12-20`) | Same keys at `:14-20` (row 24) |
| Stored preferences without a field | Same lines (row 25) |
| `agb` command line (`tools/agb/Program.cs:21-29`) | Same 9 commands at `:24-32`; usage text added (row 26) |
| Eight template cards (D-09) | Unchanged (row 27) |
| Window table notes | Changed: T-P01, T-P05, T-P06, T-P08, T-P10, T-P19, T-D04, T-O04, T-O08 gaps closed (above); T-D10 no longer "lists the ten shortcuts of the frame" (rows 6-7); T-D12 has three titles (Routes and states); T-P07 "Import CSV opens an OS file picker before T-D19" still true; T-P22 and T-D01 notes still true |
| Settings section (11 tabs, every field of the frames, no extra field) | Still true: the settings diffs change layout, colours and help texts only; no field, tab or button was added or removed |
| Menus section (T-M01..T-M24, no extra item) | Still true: `Commands/` is unchanged; the only menu changes are the T-M21 fix and the T-M11 Enemy icon |

## What the first review got wrong

1. Its paragraph "Commands with no Figma menu item" says "None of them is in a menu", but two of its 12 commands are
   Figma menu items: `play.frameStep` is "Frame step" F10 of T-M18 Play Speed (`PlayCommands.cs:60` at `24a26b6`;
   menus.json "Play Speed", "Frame step", F10), and `scene.focusSelection` is "Focus in View" F of T-M12 Actor Context
   Menu (`SceneCommands.cs:155`; menus.json "Actor Context Menu", "Focus in View", F). The list has 10 commands, not 12.
   The classification (allowed) does not change.
2. The same paragraph says the commands "can be found in the Command Palette". `script.find` and `script.replace` are
   `InPalette = false` (`ScriptEditorPage.cs:687`, `:690` at `24a26b6`), and the Command Palette lists only
   `InPalette` commands (`Shell/PaletteSearch.cs:96`); those two are found only in the Preferences › Shortcuts search.
3. It missed row 28, the Font Manager glyph group list opened from the caption text. Its row "Open lists of Select and
   Field Dropdown controls" includes the shared helper (`Windows/Tilemap/PageChrome.cs:170` at `24a26b6`), but this
   caller is anchored on a caption that Figma draws as plain text, not on a Select or Field Dropdown.

Its line references were checked at `24a26b6` (the extras table, the command lines and the T-M21 line) and are right.

## Not checked

- The same limits as the first review: the contents of each window beyond what changed since `24a26b6`; option lists
  inside dropdowns; generated menu rows; button routes, prototype links, tokens, components and brand; the wording of
  toasts and tooltips; canvas keys other than the ones the diff added.
- Whether the new elements work (E2E) or look like Figma (SHOT).
- Files outside `editor/AgbStudio.Editor` and `tools/agb/Program.cs`.
