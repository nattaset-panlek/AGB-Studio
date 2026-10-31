# Review: section 4 "implemented" boxes and plain items (2026-10-31)

Independent evidence review of `docs/ENGINE-CHECKLIST.md` section 4, read-only on the snapshot `c407d74` (detached
worktree `AGB-Studio-wt/review`). In scope: every "implemented" box under 4.1-4.8, 4.10 and 4.16, and every plain item
in 4.9 (states), 4.10 (T-L04), 4.11 (T-Y), 4.12 (T-B), 4.13 (T-C), 4.14 (T-X), 4.15 (T-K, non-SHOT parts) and 4.17
(T-R, non-SHOT parts). Not judged here: the "matches Figma" boxes and the SHOT parts (other reviewers).

Evidence rule (section 2.3): a box is ticked only with an exact test name that exists and passes; for a window, E2E
that it opens from every place Figma names and that its controls do what the Editor Map says, plus the Phase 10-13
items of that window. No build or test was run for this review: the CI run on this code passed (AgbStudio.Tests
170/170, Scripting 96/96, Editor 512/512 at `ddf96c0`; `c407d74` adds a runtime music-volume change with its own
tests and two scope-log rows). Every cited test name was checked against the `[Fact]` / `[Theory]` /
`[AvaloniaFact]` / `[AvaloniaTheory]` methods of `tests/AgbStudio.Editor.Tests`, `tests/AgbStudio.Tests` and
`tests/AgbStudio.Scripting.Tests` (no test carries a Skip), and its body was read. The earlier audit
(`audit-p10-p16.md`, at `24a26b6`) was a starting point only; `editor-gaps.md` and the scope log (section 8) were read
at this snapshot.

Decisions: **TICK** = the evidence is there (listed in `evidence-section4-implemented.tsv`). **USER** = the code works
but a pending scope-log row, checklist 1.4 item or "decided (default)" decision must be confirmed by the user (named).
**OPEN** = evidence or behaviour is missing (said exactly). **n/a** = the item has no non-SHOT part.

How some evidence was read:
- `MenuActionTests.EveryTitleBarMenuCommand_ActsOrOpensItsWindow` runs every title bar menu command and walks the
  menu bar to prove each command is an item of it; that is taken as "opens from <Menu> › <Item>". Opening a title bar
  menu by clicking its title is the T-B item and the "from" of T-M01..T-M07, and needs its own click.
- A `Windows.Requests` assertion counts as "the window opened": every T-P, T-D, T-S, T-O and T-M11..T-M24 id is
  registered (`[EditorWindow]` / `[EditorMenu]`), so a request never lands on the "not built" notice.
- Executing the command a button is bound to (title bar Build, panel header "+") is accepted when the same template
  binding is clicked in another test; the reason says so.
- System dialogs (4.11) are reached through their test seams (`SystemShell`, `FileChooser`, `LauncherService.PickFolder`).
- Prototype-vs-Editor-Map conflicts on the shared title bar (#48, #73, #75, #76 and #55) are judged at T-B05, T-M04,
  T-M05, T-X02 and T-X03, not at the content windows whose page the link starts from.
- State items (4.9) have no id; their key is `4.9:` plus the text before the arrow.

## Counts

| Section | TICK | USER | OPEN | n/a | Total |
|---|---|---|---|---|---|
| 4.1 Pages (Editor Map board 3 · 19) | 17 | 2 | 0 | 0 | 19 |
| 4.2 Dialogs (Editor Map board 3 · 12) | 9 | 2 | 1 | 0 | 12 |
| 4.3 Settings tabs (9 frames; with T-D08 and T-D09 this makes 21 dialogs) | 7 | 2 | 0 | 0 | 9 |
| 4.4 Popovers (Editor Map board 3 · 7) | 7 | 0 | 0 | 0 | 7 |
| 4.5 Menus (Editor Map board 2 · 16 and board 4 · 8) | 16 | 2 | 6 | 0 | 24 |
| 4.6 Design round 2: extra pages (7) | 6 | 1 | 0 | 0 | 7 |
| 4.7 Design round 2: extra dialogs (12, includes loading and error states) | 8 | 3 | 1 | 0 | 12 |
| 4.8 Design round 2: extra popovers (6) | 5 | 0 | 1 | 0 | 6 |
| 4.9 States (error / empty / loading) and where they are designed | 12 | 0 | 2 | 0 | 14 |
| 4.10 Light theme | 2 | 1 | 1 | 0 | 4 |
| 4.11 System windows from Editor Map board 4 (operating-system dialogs, no visual check) | 4 | 0 | 0 | 0 | 4 |
| 4.12 Editor Map board 1: Scene Editor buttons (35) | 30 | 1 | 4 | 0 | 35 |
| 4.13 Editor Map board 4: buttons on other pages (52) | 50 | 1 | 1 | 0 | 52 |
| 4.14 Prototype links | 1 | 2 | 0 | 0 | 3 |
| 4.15 Design tokens, type, icons, art | 5 | 0 | 0 | 0 | 5 |
| 4.16 Components (47 buildable) | 46 | 0 | 1 | 0 | 47 |
| 4.17 Brand (section "AGB Studio · Brand") | 0 | 3 | 0 | 1 | 4 |
| **All** | **225** | **20** | **18** | **1** | **264** |

"implemented" boxes: 146 (TICK 123, USER 13, OPEN 10). Plain items: 118 (TICK 102, USER 7, OPEN 8, n/a 1).
Two TICKs cover only the non-SHOT part of their item: T-K02 (UT part) and T-K05 (REV part); the SHOT reviewer decides
those boxes together with these lines.

## OPEN: what is missing

| Key | Missing |
|---|---|
| T-D12/implemented | "Opening another project" is E2E (`OpeningProjectTests.P11_06_UnsavedChanges...`), but File › Exit and closing the window are not: `ShellE2ETests.Exit_WithUnsavedChanges...` calls `MainWindow.ConfirmExitAsync` directly, `MenuActionTests` only checks that `file.exit` has a handler, and `MainWindow.OnClosing` (cancel, ask, close again) runs in no test. |
| T-M01/implemented | #1 File is clicked and opens (`Menu_ClickFile...`); Save, New Project, Open Project, Import, Export, Project Settings, Preferences act. Missing: no E2E runs File › Save All (`file.saveAll` has only a handler check), File › Exit (see T-D12) or the Open Recent submenu (see T-M08). |
| T-M02/implemented | Items act (Undo / Redo, Cut / Copy / Paste / Duplicate / Delete / Select All on actors, assets, nodes, rows, lines; Find in Project / Command Palette), but no test clicks the #2 Edit title and sees the menu open (T-B02). |
| T-M03/implemented | Items act (Import…, Reimport, Show in Explorer, Find Unused Assets, Optimize Palettes…, Create submenu), but no test clicks the #3 Assets title (T-B03). |
| T-M06/implemented | Items act (every Tools window, Screenshot / GIF), but no test clicks the #6 Tools title (T-B06). |
| T-M07/implemented | Items act (Getting Started, Scripting API, Keyboard Shortcuts, Report a Bug…, Check for Updates, About), but no test clicks the #7 Help title (T-B07). |
| T-M08/implemented | No E2E opens File › Open Recent: the recent entries are built in `MainWindow.RecentEntries` and no test runs one or Clear Recent through the menu (`ServiceTests.Recent_OpenRecentClearAndTimes` tests the store only; `OpeningProjectTests` calls `LauncherService.OpenAsync` with source "OpenRecent" directly). |
| T-D22/implemented | Launcher › Project card opens it (double-click, `P11_01_Projects...`) and Cancel leaves no half-open project, but File › Open Recent is not E2E: `OpeningProjectTests` calls `LauncherService.OpenAsync(path, "OpenRecent")` directly and no test runs a recent entry of the File menu. |
| T-O11/implemented | Ctrl+F opens it (`ScriptToolsTests.CtrlF_...`), but no test presses Ctrl+H (`script.replace`, Windows/Scripts/ScriptEditorPage.cs:714) and checks that it opens in replace mode (T-C21). |
| 4.9:Loading: flashing a cartridge | The dialog's "Writing x / y KB" text and its "Keep the cartridge connected" hint exist (Windows/Build/FlashDialog.cs:80-85) but no test asserts them during a write: `FlashTests.Detects_BacksUp_Writes_Verifies_WithProgress` checks "0 / 412 KB" before and "Verifying" after, with a fake device. An SD-card flash cart over a temp folder (real file copy, `SdCardFlashCart`) driven through the dialog would show the state from a real condition; real hardware is n/a until P0.09. |
| 4.9:Warning: missing glyphs | T-D16 shows it from real text (2 missing; the Figma number is the T-D16 row), but no test shows T-P09 flagging missing glyphs: the Font Manager status is only asserted as "194 glyphs · 0 missing" and no Missing glyph cell is checked in its grid (Windows/Font/FontManagerPage.cs:83, 268). |
| T-L04 | The scan (`DesignSystemTests.Sources_NoColourLiteralsOutsideTheTheme`) covers `.cs` and `.axaml` outside `Theme/` and `Gallery/` (plus `ThemeService.cs`, `SvgImage.cs`), but its regex only finds `#RRGGBB`, `#AARRGGBB`, `Color.Parse`, `FromRgb`, `FromArgb`. REV found a UI colour it misses: `Controls/Inspector.cs:66` draws the Slider knob with `Brushes.White` instead of the `lit/white` resource the Switch knob uses (`Theme/Buttons.axaml:202`). Other hits are allowed: transparent hit-test backgrounds, game pixels (font preview paper / ink defaults in `Windows/Font`, palette and debugger swatches from game data), code colours in `Theme/Literals.axaml`, fallbacks behind a resource lookup. Light switching itself is shown (`ShellE2ETests.Theme_DarkLightAccentScaleAndReduceMotion`, `NewProjectTests.LightTheme_UsesTheLightTokens`, `PreferencesTests.Appearance_PreviewsTheThemesInTheirOwnTokens`; C# code binds tokens with `GetResourceObservable` or resolves them at render time). Needs: the Slider knob from `lit/white`, and the scan regex extended to named colours (`Brushes.X` / `Colors.X` other than Transparent) and `Color.FromUInt32` with a literal, so it would have caught this. |
| T-B02 | No test clicks the Edit title (#2) and checks that the Edit menu opens; the menu's items are only reached through their commands. |
| T-B03 | No test clicks the Assets title (#3) and checks that the Assets menu opens. |
| T-B06 | No test clicks the Tools title (#6) and checks that the Tools menu opens. |
| T-B07 | No test clicks the Help title (#7) and checks that the Help menu opens (`Menu_ClickFile...AndHelpKeyboardShortcuts...` runs the Keyboard Shortcuts command, not the Help title). |
| T-C21 | Ctrl+F is pressed in `ScriptToolsTests.CtrlF_OpensFindWithTheWordAtTheCaret_NextAndReplace` (T-O11 opens); no test presses Ctrl+H (`script.replace`, `ScriptEditorPage.cs:714`) and checks Find & Replace opens in replace mode. |
| C:Dialog Footer/implemented | No test shows that a long hint never pushes the buttons out, and the template cannot promise it: `Theme/Chrome.axaml:266-272` docks the hint TextBlock Left before the Right-docked buttons with no width limit, trimming or wrapping, so a long hint takes the width first and squeezes the buttons. Needs: hint laid out after the buttons (or trimmed / wrapped) and a test with a long hint asserting the Cancel and Confirm buttons keep their full size inside the footer. |

Small fixes that close most of them: one E2E that clicks each of the seven title bar menus and checks it opens
(T-B02, T-B03, T-B06, T-B07, and the "from" of T-M02, T-M03, T-M06, T-M07); one that runs File › Open Recent › a
project and Clear Recent (T-M08, T-D22, part of T-M01); one that runs File › Save All and File › Exit / closes the
window with a dirty file (T-D12, rest of T-M01); one that presses Ctrl+H in the Script Editor (T-O11, T-C21).

## USER: decisions to ask for

| Key | Row or decision |
|---|---|
| T-P17/implemented | Openers (Tools / Create › Logic Graph) and P11.48 / P11.49 pass (`LogicGraphTests`, Link tool, Preview and Add node gaps fixed), but the Generated C# differs from Figma's code panel: scope-log row "T-P17 On Touch Tag" (pending). |
| T-P18/implemented | Help › Scripting API and Script Editor › Open Scripting API (link #49) open it; P11.56 passes (`ScriptingApiTests`). The extra "See also" button row is outside the frame: scope-log row "G-SCOPE.02" (pending). |
| T-D01/implemented | Openers pass (File › New Project…, link #51, link #112) and P11.08 passes (`NewProjectTests`), but the dialog shows eight template cards where the frame shows six: checklist 1.4 item 4, scope-log row "1.4 items 1-9" (pending). |
| T-D07/implemented | Openers pass (Build › Flash to Cartridge…, link #81) and the flow passes with fake devices (`FlashTests`), but GBxCart RW writing, which Figma shows, is refused until hardware verification: D-12 is "decided (default)" only, and P11.60 is HW (n/a until hardware). No scope-log row. |
| T-S02/implemented | Opens and every field persists, but the type / size of `lives` and `coins` differs from the frame (app: byte / short; Figma: int with 1 B / 2 B): scope-log row "T-S02" (pending). |
| T-S08/implemented | Opens (nav, Change in Preferences…, toolchain notice); projects folder, image editor, Tiled and Browse take effect (`PreferenceEffectsTests.Paths_...`). Still open in editor-gaps: the Image editor hint says `.png` opens there on double-click, while Editor Map #24 sends a sprite PNG to the Sprite Editor; Figma contradicts itself. Only the generic row "T-P01, T-P05, ... T-S08 ... Code differs from Figma captions" (pending) covers it; a row of its own is needed for the user to choose. |
| T-M04/implemented | The Scene title opens the Scene menu (clicked in links #48 / #73 / #75 / #76) and every item acts, but the prototype sends that title back to the Scene Editor on four pages: scope-log row "T-X03, T-M04, prototype links #48, #73, #75, #76" (pending). |
| T-M05/implemented | The Build title opens the Build menu (clicked in link #55) and every item acts (incl. Clean Build, disabled states), but Figma's prototype link #55 sends the Build menu title on the Scene Editor straight to Build ROM; same conflict as the T-M04 row, and #55 is not in it yet: add it to row "T-X03, T-M04, prototype links #48, #73, #75, #76". |
| T-P22/implemented | Reached by creating a project with every include off (`EmptyProjectTests`) with every empty state, but there is no "File › New Project (Empty)" item (checklist 1.4 item 3, row "1.4 items 1-9", pending) and the caption's template choice is not built (row "T-P22", pending). The T-P22 window page still lists "File › New Project (Empty)" as an opener. |
| T-D16/implemented | Font Manager › Add characters (link #77) opens it and it adds the missing glyphs, but the result differs from the Figma sample (2 missing vs 7): scope-log row "T-D16, P8.08" (pending). |
| T-D19/implemented | Database › Import CSV (link #78) opens it and row warnings work, but the caption's Google Sheets source is not built (CSV only): checklist 1.4 item 7 / D-13, row "1.4 items 1-9" (pending). |
| T-D24/implemented | Import with problem files opens it with fixes and skips errors, but the split fix offers 4 palettes where Figma says 3: scope-log row "T-D24, P7.03, D-25" (pending). |
| T-L02/implemented | New Project uses the Light tokens (`NewProjectTests.LightTheme_UsesTheLightTokens`, links #116 / #117 in Light), but it shares T-D01's eight-card difference: row "1.4 items 1-9" (item 4, pending). |
| T-B05 | The Build title is clicked in link #55 and the Build menu opens, as Editor Map board 1 says, but Figma's prototype link #55 on this page sends that title to Build ROM: add #55 to row "T-X03, T-M04, prototype links #48, #73, #75, #76". |
| T-C52 | The app has no "File › New Project (Empty)" item: File has New Project… only, and an empty project (template with every include off) opens T-P22 (`EmptyProjectTests.P11_09_EmptyProject_OpensWithEveryEmptyState`, which opens T-D01 through the window service, not the File menu). This is checklist 1.4 item 3, scope-log row "1.4 items 1-9" (pending). The T-P22 window page still lists "File › New Project (Empty)" as an opener, which the menu does not have. |
| T-X02 | "AGB Studio · editor" is walked end to end (46 links), but it includes #48, #73, #75, #76 (row "T-X03, T-M04, prototype links #48, #73, #75, #76", pending) and #55, which has the same kind of difference and is in no row yet. |
| T-X03 | All 120 links pass as E2E cases, read from the read-only export `docs/evidence/figma/prototype.json` at test time; four go through another control (row "T-X03, T-M04, prototype links #48, #73, #75, #76", pending). Link #55 (Build menu title -> Build ROM) also needs two clicks in the app (menu, then Build ROM) but is not flagged in `prototype-links.md` nor in the row. |
| T-R01 | Exe icon (`ApplicationIcon` Assets/agb-studio.ico) and window / taskbar icon (`ShellE2ETests.Window_HasTheAppIcon`); About places the app icon at 64 px (Windows/Menus/AboutDialog.cs:31). The item also names the installer, and there is none: D-19 (portable zip) is "decided (default)" only, so the user must confirm D-19. The look against Plus Pad is SHOT (other reviewer). |
| T-R03 | REV: four banners in docs/brand (read-only Figma export, b3c3b27), used at the top of README.md and docs/RELEASE-NOTES.md and copied into the package (tools/AgbStudio.Build/Packager.cs:43-44; `PackagingTests.PackageHoldsTheRuntimeFontsCoreAndDocsAndFindsItsRoot`; 2914d22). The item says "release page and installer"; there is no installer because D-19 is "decided (default)" only: the user must confirm D-19. |
| T-R04 | RT part done: `RuntimeRomTests.RomPasses` (framework_ui: `boot_splash_then_title_menu` 60 frames for 1 s, `debug_builds_can_skip_the_splash`; runtime gates the skip with `AGB_DEBUG`, `game.cpp:511-515`), `UiKitTests.SplashTitleOptionsAndPauseInABuiltGame` (Plus Pad mark on screen in a built game), `ProjectSettingsTests.Build_ReadsTheSettings_RomHeaderSaveTypeStartSceneSplashRemap` ("AGB Studio logo · 2 s" reaches the build; seconds x 60 frames in `GameBuilder.cs:845`). The user must keep the GBA splash frame the build session added to Figma: scope-log row "1.4 item 6, D-20, D-23" (pending). |

## Other findings

- Prototype link #55 ("Title Bar / Menus / Build" on the Scene Editor, OVERLAY to Build ROM) has the same kind of
  difference as #48 / #73 / #75 / #76: in the app the Build title opens the Build menu (Editor Map board 2) and the test
  then clicks Build ROM. `docs/evidence/prototype-links.md` does not list it under "Links whose app control is not the
  Figma node", and the scope-log row "T-X03, T-M04, prototype links #48, #73, #75, #76" does not name it or T-M05.
- `docs/user/windows/T-P22-scene-editor-empty-project.md` lists "File › New Project (Empty)" as an opener; the File
  menu has no such item (1.4 item 3).
- Scope-log row "T-P01, T-P05, ... T-O08 / Code differs from Figma captions" (pending) is stale for the windows whose
  gaps moved to "Fixed" in `editor-gaps.md`; only T-P22, T-S02 and T-S08 (and the prototype links) are still open there.
- T-C06: a single click on a Launcher project card selects it; Opening Project needs a double-click or Enter (the
  Figma frame shows a selected card). Accepted, noted.
- Dialog Footer (component) and T-L04 are OPEN on code findings, not only missing tests: `Theme/Chrome.axaml:266-272`
  docks the hint before the buttons with no trimming, and `Controls/Inspector.cs:66` draws the Slider knob with
  `Brushes.White`, which the colour scan does not look for.
- Most decisions in `docs/DECISIONS.md` are "decided (default)"; where a box deviates from Figma because of one
  (D-12 for T-D07, D-19 for T-R01 / T-R03), it is USER.

## Every box

### 4.1 Pages (Editor Map board 3 · 19)

| Key | Decision | Reason |
|---|---|---|
| T-P01/implemented | TICK | Opens as the default workspace (start-up with the last project; opening a project with content); P11.10-P11.21 evidence; the Paint / Fill / Erase gap is fixed (`ViewportTests.PaintFillAndErase_EditTheMapWithUndo`). Note: prototype link #55 (Build menu title) is judged under T-B05 / T-M05 / T-X03. |
| T-P02/implemented | TICK | All six openers: #11 Play (link #57), #15 Game tab (link #59), Build › Play / Play from Here / Build & Run (`MenuActionTests`, `P12_02`), Build Report › Run in mGBA (link #80). P12.01 and P12.07 gaps fixed (`PlayAcrossEditorTests`); P12.05 is SHOT only. The Scene-title link #73 is decided under T-M04 / T-X03. |
| T-P03/implemented | TICK | #16 map tab (link #60, `OpensFromTheMapTab...`), Scene › Background Layers…, Create › Tileset / Tilemap makes a new map; P11.28-P11.32 (Tileset dropdown gap fixed). Link #76 is decided under T-M04 / T-X03. |
| T-P04/implemented | TICK | #17 tab (link #61), #31 Script component, Create › C# Script, Actor Context Menu and Component Menu › Edit Script; P11.52-P11.55 (code font / theme and analyzer-chip gaps fixed). Ctrl+H is judged under T-O11; link #48 under T-M04 / T-X03. |
| T-P05/implemented | TICK | #21 Console tab clicked; filters, counts, click to code; game Debug.Log lines reach the panel (gap fixed). |
| T-P06/implemented | TICK | #22 Animation tab clicked; list, frames, play / step, onion skin; speed and frame order edits with undo (gap fixed). |
| T-P07/implemented | TICK | #23 Database tab (link #64), Tools › Database, Create › Data Table; P11.39. Scope-log row "T-P07, P9.07" is pending, but the app shows the Figma value (field sum), so it does not block. |
| T-P08/implemented | TICK | #24 double-click (link #65, `Assets_DoubleClick...`), Create › Sprite, Asset Context Menu › Open, Animation List › Edit Animations…; P11.22-P11.27 (drop zone / Aseprite gap fixed). Link #75 is decided under T-M04 / T-X03. |
| T-P09/implemented | TICK | #26 font asset (link #67), Tools › Font Manager, Create › Font (New Font on top); P11.37. |
| T-P10/implemented | TICK | #30 Palette button, Assets › Optimize Palettes…, Tools › Palette Manager, Create › Palette, Sprite Editor › Palette slots; P11.33-P11.36 (import through Import Asset fixed). |
| T-P11/implemented | TICK | #34 Build status (link #71), Build › Build Report, Budget Details › Open Build Report; P11.58, P13.08. |
| T-P12/implemented | TICK | App start (`Startup.Choose` with and without a last project; flow T-X01 starts here), File › Open Project…, Help › Getting Started; P11.01-P11.07. |
| T-P13/implemented | TICK | Scene › World Map (menu command and Ctrl+M); P11.50, P11.51. |
| T-P14/implemented | TICK | Tools › Localization; P11.41, P11.42. |
| T-P15/implemented | TICK | Tools › Audio, Create › Sound Effect, Create › Music; P11.43, P11.44. The G-PERF.05 row (mixer CPU 14.5 % vs Figma 6 %) is about defaults and the shown number, not about the window's function. |
| T-P16/implemented | TICK | Tools › Dialogue Editor, Create › Dialogue (new conversation, undo); P11.46. |
| T-P17/implemented | USER | Openers (Tools / Create › Logic Graph) and P11.48 / P11.49 pass (`LogicGraphTests`, Link tool, Preview and Add node gaps fixed), but the Generated C# differs from Figma's code panel: scope-log row "T-P17 On Touch Tag" (pending). |
| T-P18/implemented | USER | Help › Scripting API and Script Editor › Open Scripting API (link #49) open it; P11.56 passes (`ScriptingApiTests`). The extra "See also" button row is outside the frame: scope-log row "G-SCOPE.02" (pending). |
| T-P19/implemented | TICK | #9 Debug (link #56, `P13_09`), Tools › Debugger (Ctrl+Shift+D); P13.01-P13.06 (user breakpoints and view gaps fixed by `BreakpointUiTests`). |

### 4.2 Dialogs (Editor Map board 3 · 12)

| Key | Decision | Reason |
|---|---|---|
| T-D01/implemented | USER | Openers pass (File › New Project…, link #51, link #112) and P11.08 passes (`NewProjectTests`), but the dialog shows eight template cards where the frame shows six: checklist 1.4 item 4, scope-log row "1.4 items 1-9" (pending). |
| T-D02/implemented | TICK | Scene › New Scene…, Scene Switcher › New Scene…, World Map › New Scene (link #50); P11.18. |
| T-D03/implemented | TICK | #18 scene size chip (link #62, `ChipsOpenTheirWindows`), Scene › Scene Settings…; P11.18. |
| T-D04/implemented | TICK | File › Import Asset… and Assets › Import… (same command), Sprite Editor drop zone, Palette Manager › Import, Audio › Music +; P11.64 (Aseprite, drop zone and palette import gaps fixed). |
| T-D05/implemented | TICK | File › Export ROM (.gba)…; P11.59; the padded release ROM boots to the same frames. |
| T-D06/implemented | TICK | Build › Build ROM (link #55 clicks the Build menu then Build ROM), Build Report › Build again (link #79); #10 title-bar Build button is bound to `build.buildRom` (Theme/Chrome.axaml:237, Shell/MainWindow.cs:55) and that command is executed, but no test clicks the button itself; P11.57. |
| T-D07/implemented | USER | Openers pass (Build › Flash to Cartridge…, link #81) and the flow passes with fake devices (`FlashTests`), but GBxCart RW writing, which Figma shows, is refused until hardware verification: D-12 is "decided (default)" only, and P11.60 is HW (n/a until hardware). No scope-log row. |
| T-D08/implemented | TICK | File › Project Settings… opens on Input; fields persist and reach the build; keys and pad drive Play (`P12_09`). The pending row "T-D08, P4.12, P2.10" is about the in-game Options values, not this dialog. |
| T-D09/implemented | TICK | #35 Emulator status (clicked), File › Preferences… (and Ctrl+,), Launcher › Settings (link #54); every field persists; Play Mode takes the emulator settings (gap fixed). |
| T-D10/implemented | TICK | Help › Keyboard Shortcuts; lists every shortcut by group, search, link to Preferences › Shortcuts (gap fixed). |
| T-D11/implemented | TICK | Help › About AGB Studio; version, date, credits, Licenses, Website (disabled without a configured URL, D-13). |
| T-D12/implemented | OPEN | "Opening another project" is E2E (`OpeningProjectTests.P11_06_UnsavedChanges...`), but File › Exit and closing the window are not: `ShellE2ETests.Exit_WithUnsavedChanges...` calls `MainWindow.ConfirmExitAsync` directly, `MenuActionTests` only checks that `file.exit` has a handler, and `MainWindow.OnClosing` (cancel, ask, close again) runs in no test. |

### 4.3 Settings tabs (9 frames; with T-D08 and T-D09 this makes 21 dialogs)

| Key | Decision | Reason |
|---|---|---|
| T-S01/implemented | TICK | File › Project Settings… then the General tab (nav clicked in `EveryField`); fields persist and reach the ROM header / start scene / splash. |
| T-S02/implemented | USER | Opens and every field persists, but the type / size of `lives` and `coins` differs from the frame (app: byte / short; Figma: int with 1 B / 2 B): scope-log row "T-S02" (pending). |
| T-S03/implemented | TICK | Build tab clicked from File › Project Settings…; fields persist and change the build (`BuildTab_FieldsChangeTheBuild`); toolchain card; Change in Preferences… opens Paths. |
| T-S04/implemented | TICK | Scripting tab; rule levels, warnings as errors, inline hints take effect (gap fixed); the Script Editor analyzer chip opens this tab. |
| T-S05/implemented | TICK | Localization tab; fields persist; Add language opens T-D20; missing strings warn or fail the build as set (`LimitTests.MissingStringsWarnOrFailAsLocalizationSays`). |
| T-S06/implemented | TICK | Appearance tab; theme, accent, UI scale apply on Save; reduce motion and pixel grid take effect (gap fixed). |
| T-S07/implemented | TICK | Editor tab; autosave, undo history, open last project, default snap, collision overlay, budget in Inspector, code font / size / theme, external editor settings all take effect (gaps fixed). |
| T-S08/implemented | USER | Opens (nav, Change in Preferences…, toolchain notice); projects folder, image editor, Tiled and Browse take effect (`PreferenceEffectsTests.Paths_...`). Still open in editor-gaps: the Image editor hint says `.png` opens there on double-click, while Editor Map #24 sends a sprite PNG to the Sprite Editor; Figma contradicts itself. Only the generic row "T-P01, T-P05, ... T-S08 ... Code differs from Figma captions" (pending) covers it; a row of its own is needed for the user to choose. |
| T-S09/implemented | TICK | Shortcuts tab; search, record with conflict, replace, save, reset (`PreferencesTests.Shortcuts_*`). |

### 4.4 Popovers (Editor Map board 3 · 7)

| Key | Decision | Reason |
|---|---|---|
| T-O01/implemented | TICK | #8 breadcrumb clicked; story order, search, switch, New Scene…, boss tag (gap fixed). |
| T-O02/implemented | TICK | #12 chip (link #58, clicked); modes, alpha, mosaic with undo. |
| T-O03/implemented | TICK | #19 Grid chip (link #63, clicked); size, switches, colour, overlays. |
| T-O04/implemented | TICK | #28 Sheet dropdown (link #68), Add to Scene › Prefab…, Tilemap Editor › Tileset dropdown (opens on Tilesets, gap fixed); search, tabs, pick. |
| T-O05/implemented | TICK | #32 Add Component (link #69), Actor Context Menu › Add Behaviour…; categories, search, keyboard, New C# script…. |
| T-O06/implemented | TICK | #33 GBA Budget (link #70); live numbers, palette-sharing hint, Open Build Report. |
| T-O07/implemented | TICK | Edit › Command Palette…, Edit › Find in Project…, Ctrl+K; file results open their editor and switch scenes (gap fixed). |

### 4.5 Menus (Editor Map board 2 · 16 and board 4 · 8)

| Key | Decision | Reason |
|---|---|---|
| T-M01/implemented | OPEN | #1 File is clicked and opens (`Menu_ClickFile...`); Save, New Project, Open Project, Import, Export, Project Settings, Preferences act. Missing: no E2E runs File › Save All (`file.saveAll` has only a handler check), File › Exit (see T-D12) or the Open Recent submenu (see T-M08). |
| T-M02/implemented | OPEN | Items act (Undo / Redo, Cut / Copy / Paste / Duplicate / Delete / Select All on actors, assets, nodes, rows, lines; Find in Project / Command Palette), but no test clicks the #2 Edit title and sees the menu open (T-B02). |
| T-M03/implemented | OPEN | Items act (Import…, Reimport, Show in Explorer, Find Unused Assets, Optimize Palettes…, Create submenu), but no test clicks the #3 Assets title (T-B03). |
| T-M04/implemented | USER | The Scene title opens the Scene menu (clicked in links #48 / #73 / #75 / #76) and every item acts, but the prototype sends that title back to the Scene Editor on four pages: scope-log row "T-X03, T-M04, prototype links #48, #73, #75, #76" (pending). |
| T-M05/implemented | USER | The Build title opens the Build menu (clicked in link #55) and every item acts (incl. Clean Build, disabled states), but Figma's prototype link #55 sends the Build menu title on the Scene Editor straight to Build ROM; same conflict as the T-M04 row, and #55 is not in it yet: add it to row "T-X03, T-M04, prototype links #48, #73, #75, #76". |
| T-M06/implemented | OPEN | Items act (every Tools window, Screenshot / GIF), but no test clicks the #6 Tools title (T-B06). |
| T-M07/implemented | OPEN | Items act (Getting Started, Scripting API, Keyboard Shortcuts, Report a Bug…, Check for Updates, About), but no test clicks the #7 Help title (T-B07). |
| T-M08/implemented | OPEN | No E2E opens File › Open Recent: the recent entries are built in `MainWindow.RecentEntries` and no test runs one or Clear Recent through the menu (`ServiceTests.Recent_OpenRecentClearAndTimes` tests the store only; `OpeningProjectTests` calls `LauncherService.OpenAsync` with source "OpenRecent" directly). |
| T-M09/implemented | TICK | Assets › Create is a submenu of the title bar (menu walk and Figma item check); every Create item opens its window, Tileset / Tilemap makes a new map, Font opens New Font, Sprite / C# Script / Data Table put their New dialog on top. |
| T-M10/implemented | TICK | Scene › Add Actor items are in the title bar Scene menu (menu walk, Figma item check) and each adds its actor kind. |
| T-M11/implemented | TICK | #13 Hierarchy + clicked; every item incl. the Enemy submenu, Tilemap Layer and Prefab…. |
| T-M12/implemented | TICK | #14 right-click on an actor row; Rename, Duplicate, Delete, Add Behaviour…, Edit Script, Make Prefab, Focus in View. |
| T-M13/implemented | TICK | #25 right-click on an asset card; every item incl. Reimport and Show in Explorer. |
| T-M14/implemented | TICK | #27 Inspector "…" clicked; every item. |
| T-M15/implemented | TICK | #20 Zoom chip clicked; 100-400 %, Fit, Pixel-perfect. |
| T-M16/implemented | TICK | #29 Animation dropdown clicked; animations of the sprite, Edit Animations…. |
| T-M17/implemented | TICK | Right-click on a Launcher project card; every item; Delete asks first and names what is removed (T-C07). |
| T-M18/implemented | TICK | Play Mode speed chip clicked; 1/4x to 4x and Frame step (T-C10). |
| T-M19/implemented | TICK | Sprite Editor OAM size field clicked; all 12 sizes (T-C14). |
| T-M20/implemented | TICK | Tilemap Editor Map "…" and right-click on the layers table; four layer types (T-C17). |
| T-M21/implemented | TICK | Right-click on a Logic Graph node; every item incl. Show generated C# (T-C24). |
| T-M22/implemented | TICK | Right-click on a World Map scene card; every item (T-C26). |
| T-M23/implemented | TICK | Dialogue Editor Add box clicked; items as Figma; Line, Choice, Command, Condition, Paste act (T-C40). |
| T-M24/implemented | TICK | Command submenu of Add to Dialogue (registered, items as Figma); all six commands act. |

### 4.6 Design round 2: extra pages (7)

| Key | Decision | Reason |
|---|---|---|
| T-P20/implemented | TICK | Launcher › Templates tab (link #52, nav clicked); filters, search, details, Use this template. The pending row "T-P20 Racing card" is about the Racing template's karts, not the window. |
| T-P21/implemented | TICK | Launcher › Learn tab (link #53); tutorials with progress, docs links, samples made and opened. |
| T-P22/implemented | USER | Reached by creating a project with every include off (`EmptyProjectTests`) with every empty state, but there is no "File › New Project (Empty)" item (checklist 1.4 item 3, row "1.4 items 1-9", pending) and the caption's template choice is not built (row "T-P22", pending). The T-P22 window page still lists "File › New Project (Empty)" as an opener. |
| T-P23/implemented | TICK | Debugger › Views › Memory clicked; map by category, watch list, hex view, Add watch. |
| T-P24/implemented | TICK | Debugger › Views › Palettes clicked; live RAM, changed outline, Used by with the palette effect (gap fixed). |
| T-P25/implemented | TICK | Audio › Sound effects + clicked; every SFX Designer part; preview compared with the emulator. |
| T-P26/implemented | TICK | Tilemap Editor › Auto-tile rules tab clicked; rule sets, 3x3 rules, Add rule; painting resolves the rules. |

### 4.7 Design round 2: extra dialogs (12, includes loading and error states)

| Key | Decision | Reason |
|---|---|---|
| T-D13/implemented | TICK | Sprite Editor › Sprites + clicked; Create › Sprite also puts it on top; P11.27. |
| T-D14/implemented | TICK | Script Editor › Scripts +, Component Picker › New C# script…; creates, attaches, opens; open in the external editor after creating. |
| T-D15/implemented | TICK | Font Manager › Fonts + clicked; New Font creates and shows the font. |
| T-D16/implemented | USER | Font Manager › Add characters (link #77) opens it and it adds the missing glyphs, but the result differs from the Figma sample (2 missing vs 7): scope-log row "T-D16, P8.08" (pending). |
| T-D17/implemented | TICK | Database › Tables + (bound header command); empty, from CSV, copy; undo. |
| T-D18/implemented | TICK | Database › Schema + (bound header command); enum field, one byte per row. |
| T-D19/implemented | USER | Database › Import CSV (link #78) opens it and row warnings work, but the caption's Google Sheets source is not built (CSV only): checklist 1.4 item 7 / D-13, row "1.4 items 1-9" (pending). |
| T-D20/implemented | TICK | Project Settings › Localization › Add language and Localization › Languages + (bound header command); Japanese gets a kana font. |
| T-D21/implemented | TICK | Dialogue Editor › Speakers + (bound header command); Thai name, portrait, colour, blip. |
| T-D22/implemented | OPEN | Launcher › Project card opens it (double-click, `P11_01_Projects...`) and Cancel leaves no half-open project, but File › Open Recent is not E2E: `OpeningProjectTests` calls `LauncherService.OpenAsync(path, "OpenRecent")` directly and no test runs a recent entry of the File menu. |
| T-D23/implemented | TICK | A real build with a broken script opens it; failed step, skipped steps, errors, Go to first error. |
| T-D24/implemented | USER | Import with problem files opens it with fixes and skips errors, but the split fix offers 4 palettes where Figma says 3: scope-log row "T-D24, P7.03, D-25" (pending). |

### 4.8 Design round 2: extra popovers (6)

| Key | Decision | Reason |
|---|---|---|
| T-O08/implemented | TICK | Right-click on empty canvas and Nodes › Add node… open it (gap fixed); search, Enter, Shift+Enter. |
| T-O09/implemented | TICK | World Map link chip clicked; changes are saved. |
| T-O10/implemented | TICK | Palette Manager Fade and Cycle fields clicked; preview; effects match the runtime. |
| T-O11/implemented | OPEN | Ctrl+F opens it (`ScriptToolsTests.CtrlF_...`), but no test presses Ctrl+H (`script.replace`, Windows/Scripts/ScriptEditorPage.cs:714) and checks that it opens in replace mode (T-C21). |
| T-O12/implemented | TICK | Preferences › Shortcuts › pencil clicked; records with conflict and replace. |
| T-O13/implemented | TICK | Launcher › Learn › Start / Review clicked (Continue is the same button); sub-steps tick themselves. |

### 4.9 States (error / empty / loading) and where they are designed

| Key | Decision | Reason |
|---|---|---|
| 4.9:Empty: whole project empty | TICK | A project with nothing in it opens T-P22 with every empty text, Add player / Import images; adding a file ends the state (real conditions). |
| 4.9:Loading: opening a project | TICK | Read project, Load assets with count, Compile scripts, Open last scene, Cancel during Load assets leaves the project as it was. |
| 4.9:Loading: build progress | TICK | Real build streams per-step state and time, per-file progress (9 / 12) and the log. |
| 4.9:Loading: flashing a cartridge | OPEN | The dialog's "Writing x / y KB" text and its "Keep the cartridge connected" hint exist (Windows/Build/FlashDialog.cs:80-85) but no test asserts them during a write: `FlashTests.Detects_BacksUp_Writes_Verifies_WithProgress` checks "0 / 412 KB" before and "Verifying" after, with a fake device. An SD-card flash cart over a temp folder (real file copy, `SdCardFlashCart`) driven through the dialog would show the state from a real condition; real hardware is n/a until P0.09. |
| 4.9:Error: build failed | TICK | A broken script fails a real build: failed step, skipped steps, clickable errors, Go to first error. |
| 4.9:Error: import failed | TICK | Problem files open the errors dialog with plain-language problems, fix buttons, and error files skipped. The "4 palettes" number is the T-D24 row. |
| 4.9:Error / warning inline: analyzer diagnostic popup with quick fixes | TICK | Typing code that breaks a GBA rule underlines it; the popup offers the quick fixes. |
| 4.9:Warning: shortcut conflict | TICK | Recording a key that is taken shows the conflict in Record Shortcut and the conflict chip in Shortcuts. |
| 4.9:Warning: localization missing / too long | TICK | Missing counts, Missing only filter and the "long" fit chip come from the real string table. |
| 4.9:Warning: missing glyphs | OPEN | T-D16 shows it from real text (2 missing; the Figma number is the T-D16 row), but no test shows T-P09 flagging missing glyphs: the Font Manager status is only asserted as "194 glyphs · 0 missing" and no Missing glyph cell is checked in its grid (Windows/Font/FontManagerPage.cs:83, 268). |
| 4.9:Warning: CSV row problems | TICK | A CSV with bad rows warns on rows 7 and 15, then imports. |
| 4.9:Warning: location inside devkitPro | TICK | A location inside devkitPro shows the warning in New Project. |
| 4.9:Status found / not set for tools and paths | TICK | Paths show check / warn from the real files (devkitPro found, Tiled missing then found after Browse); the Emulator tab uses the same toolchain card (Windows/Settings/PreferencesDialog.cs:419) whose found / not found lines are asserted on the Build tab. |
| 4.9:Unsaved changes | TICK | A dirty document shows the dirty dot; opening another project with it asks first (real condition); the Exit prompt names the file count. |

### 4.10 Light theme

| Key | Decision | Reason |
|---|---|---|
| T-L01/implemented | TICK | Preferences › Appearance › Light then Save turns the app to the Light variant; Light tokens apply; the Scene Editor renders in Light (SHOT render). |
| T-L02/implemented | USER | New Project uses the Light tokens (`NewProjectTests.LightTheme_UsesTheLightTokens`, links #116 / #117 in Light), but it shares T-D01's eight-card difference: row "1.4 items 1-9" (item 4, pending). |
| T-L03/implemented | TICK | Preferences opened in Light (links #118 / #119 in a Light session) and its theme previews use their own tokens; Light applies on Save. |
| T-L04 | OPEN | The scan (`DesignSystemTests.Sources_NoColourLiteralsOutsideTheTheme`) covers `.cs` and `.axaml` outside `Theme/` and `Gallery/` (plus `ThemeService.cs`, `SvgImage.cs`), but its regex only finds `#RRGGBB`, `#AARRGGBB`, `Color.Parse`, `FromRgb`, `FromArgb`. REV found a UI colour it misses: `Controls/Inspector.cs:66` draws the Slider knob with `Brushes.White` instead of the `lit/white` resource the Switch knob uses (`Theme/Buttons.axaml:202`). Other hits are allowed: transparent hit-test backgrounds, game pixels (font preview paper / ink defaults in `Windows/Font`, palette and debugger swatches from game data), code colours in `Theme/Literals.axaml`, fallbacks behind a resource lookup. Light switching itself is shown (`ShellE2ETests.Theme_DarkLightAccentScaleAndReduceMotion`, `NewProjectTests.LightTheme_UsesTheLightTokens`, `PreferencesTests.Appearance_PreviewsTheThemesInTheirOwnTokens`; C# code binds tokens with `GetResourceObservable` or resolves them at render time). Needs: the Slider knob from `lit/white`, and the scan regex extended to named colours (`Brushes.X` / `Colors.X` other than Transparent) and `Color.FromUInt32` with a literal, so it would have caught this. |

### 4.11 System windows from Editor Map board 4 (operating-system dialogs, no visual check)

| Key | Decision | Reason |
|---|---|---|
| T-Y01 | TICK | Launcher › Open… asks the folder picker (seam `LauncherService.PickFolder`, default `SystemShell.PickFolderAsync`) and opens the picked project. |
| T-Y02 | TICK | Localization › Export .csv asks the save dialog (seam `FileChooser.SaveOverride`) with "Export .csv" and the suggested name, and writes the file. |
| T-Y03 | TICK | Open externally starts VS Code, Rider, Visual Studio and Custom at the file and caret line (fake executables). Per-IDE runs are P14.03. |
| T-Y04 | TICK | Preferences › Paths › Browse… asks the file picker (seam `SystemShell.FilePicker`) and the path and status update. |

### 4.12 Editor Map board 1: Scene Editor buttons (35)

| Key | Decision | Reason |
|---|---|---|
| T-B01 | TICK | The File title is clicked and its menu opens. |
| T-B02 | OPEN | No test clicks the Edit title (#2) and checks that the Edit menu opens; the menu's items are only reached through their commands. |
| T-B03 | OPEN | No test clicks the Assets title (#3) and checks that the Assets menu opens. |
| T-B04 | TICK | The Scene title is clicked in links #48 / #73 / #75 / #76 and the Scene menu opens with the page unchanged; the title bar is the same control on every page. The Scene Editor page itself has no conflicting prototype link. |
| T-B05 | USER | The Build title is clicked in link #55 and the Build menu opens, as Editor Map board 1 says, but Figma's prototype link #55 on this page sends that title to Build ROM: add #55 to row "T-X03, T-M04, prototype links #48, #73, #75, #76". |
| T-B06 | OPEN | No test clicks the Tools title (#6) and checks that the Tools menu opens. |
| T-B07 | OPEN | No test clicks the Help title (#7) and checks that the Help menu opens (`Menu_ClickFile...AndHelpKeyboardShortcuts...` runs the Keyboard Shortcuts command, not the Help title). |
| T-B08 | TICK | The breadcrumb is clicked; Scene Switcher opens. |
| T-B09 | TICK | Debug is clicked (link #56, `P13_09`); the Debugger page shows. |
| T-B10 | TICK | The title-bar Build button's Command is `build.buildRom` (Theme/Chrome.axaml:237, Shell/MainWindow.cs:55); the tests execute that command and get Build ROM, and check the button is enabled again after a build. No pointer click on PART_Build (the same template binding is clicked for Debug). |
| T-B11 | TICK | Play is clicked (link #57); Play Mode shows. |
| T-B12 | TICK | Video mode chip clicked (link #58, `ChipsOpenTheirWindows`). |
| T-B13 | TICK | Hierarchy + clicked; Add to Scene opens. |
| T-B14 | TICK | Right-click on an actor row; Actor Context Menu opens and the actor is selected. |
| T-B15 | TICK | Game tab clicked (link #59; also from the Debugger in `P13_09`). |
| T-B16 | TICK | Forest_01.map tab clicked (link #60). |
| T-B17 | TICK | PlayerController.cs tab clicked (link #61). |
| T-B18 | TICK | Scene size chip clicked (link #62). |
| T-B19 | TICK | Grid chip clicked (link #63). |
| T-B20 | TICK | Zoom chip clicked; Zoom menu opens. |
| T-B21 | TICK | Console tab clicked; Console panel shows. |
| T-B22 | TICK | Animation tab clicked; Animation panel shows. |
| T-B23 | TICK | Database tab clicked (link #64). |
| T-B24 | TICK | Double-click on sprite, tileset, map, font, audio, script and data cards opens the right editor (links #65-#67). |
| T-B25 | TICK | Right-click on an asset card; Asset Context Menu opens. |
| T-B26 | TICK | Double-click on the font card (link #67). |
| T-B27 | TICK | Inspector "…" clicked; Component Menu opens. |
| T-B28 | TICK | Sheet dropdown clicked (link #68). |
| T-B29 | TICK | Animation dropdown clicked; Animation List opens. |
| T-B30 | TICK | Palette button clicked; Palette Manager opens. |
| T-B31 | TICK | Double-click on the script component header opens the Script Editor. |
| T-B32 | TICK | Add Component clicked (link #69). |
| T-B33 | TICK | GBA Budget clicked (link #70). |
| T-B34 | TICK | Build status clicked (link #71). |
| T-B35 | TICK | Emulator status clicked (link #72); Preferences opens on Emulator. |

### 4.13 Editor Map board 4: buttons on other pages (52)

| Key | Decision | Reason |
|---|---|---|
| T-C01 | TICK | Link #51 clicks LauncherNewProject, New Project overlay asserted; also `LauncherE2ETests.P11_01_Projects_...` clicks it and gets `NewProjectDialog`. |
| T-C02 | TICK | `LauncherE2ETests.P11_01_Projects_...` clicks LauncherOpen; the folder picker seam (`LauncherService.PickFolder`, default `SystemShell.PickFolderAsync`) answers and Opening Project shows the picked project; a folder with no .agbproj gives the error toast. |
| T-C03 | TICK | Link #52 (Nav_Templates, page T-P20 asserted) and `P11_01_Navigation_...` click Templates. |
| T-C04 | TICK | Link #53 (Nav_Learn, page T-P21) and `P11_01_Navigation_...`. |
| T-C05 | TICK | Link #54 (Nav_Settings, Preferences overlay asserted) and `P11_01_Navigation_...` (request T-D09 "LauncherNav_Settings"). |
| T-C06 | TICK | `P11_01_Projects_...` double-clicks a project card: `OpeningProjectDialog` for that project, request (T-D22, ProjectCard). A single click selects (Figma shows a selected card); the T-D22 page documents double-click or Enter. |
| T-C07 | TICK | `P11_05_ProjectContextMenu_...` right-clicks a card: card selected, request (T-M17, ProjectCard), menu items as Figma. |
| T-C08 | TICK | `P11_03_Learn_...` clicks Tutorial_csharp (Start) and Tutorial_palettes (Review): `TutorialStepsPopover`, request (T-O13, Learn_Start). Continue is the same button and handler (`LauncherPage.cs:456-458`). |
| T-C09 | TICK | Link #112 (UseTemplate, New Project overlay) and `P11_02_Templates_...` (dialog opens on the chosen template, request T-D01 UseThisTemplate). |
| T-C10 | TICK | `PlayModeE2ETests.P12_04_...` clicks the speed chip: request (T-M18, SpeedChip); the menu entries and speeds are then checked. |
| T-C11 | TICK | Link #74 (StopButton, page T-P01) and `P12_03_...` (Stop returns to `SceneWorkspace`, request T-P01 PlayStop). |
| T-C12 | TICK | `SpriteEditorE2ETests.P11_27_...` clicks the Sprites header + (PART_Action): `NewSpriteDialog`. |
| T-C13 | TICK | `P11_24_Import_...` raises a real Drop event on DropZone: request (T-D04, SpriteDropZone) and `ImportAssetDialog`; editor-gaps "Fixed" row. |
| T-C14 | TICK | `P11_25_...` clicks the OAM size field's button: request (T-M19, OamField), entries as Figma. |
| T-C15 | TICK | `P11_23_...` clicks Slot_OBJ_1: `PaletteManagerPage` on "OBJ 1 · Enemies", request (T-P10, PaletteSlots). |
| T-C16 | TICK | `TilemapE2ETests.TilesetPanel_...` clicks the Tileset field's dropdown: request (T-O04, TilesetDropdown), `AssetPicker` on the Tilesets tab (editor-gaps fixed row). |
| T-C17 | TICK | `LayersTable_..._AddLayerMenu` clicks the Map header "..." (request T-M20 MapMenu) and right-clicks the layers table (T-M20 LayersTable); the T-P03 page names the "..." as Add Layer. |
| T-C18 | TICK | `AutoTileRules_...` clicks Tab_AutoTile: request (T-P26, Tab_AutoTile), rule sets and rules shown. |
| T-C19 | TICK | `ScriptToolsTests.NewScript_CreatesFromTemplateAttachesAndOpens` clicks the Scripts header +: `NewScriptDialog`, request T-D14. |
| T-C20 | TICK | `OpenExternally_StartsTheConfiguredEditorAtTheCaretLine` clicks OpenExternally with fake VS Code / Rider / Visual Studio executables: launch command has the file and caret line (and column); Custom template; missing exe gives a toast. |
| T-C21 | OPEN | Ctrl+F is pressed in `ScriptToolsTests.CtrlF_OpensFindWithTheWordAtTheCaret_NextAndReplace` (T-O11 opens); no test presses Ctrl+H (`script.replace`, `ScriptEditorPage.cs:714`) and checks Find & Replace opens in replace mode. |
| T-C22 | TICK | Link #49 clicks OpenScriptingApi: page T-P18. |
| T-C23 | TICK | `LogicGraphTests.RightClickCanvas_...` right-clicks empty canvas: request T-O08, `AddNodePopover`. |
| T-C24 | TICK | `RightClickNode_...` right-clicks a node: request T-M21, context menu with the Figma items. |
| T-C25 | TICK | Link #50 (NewScene, New Scene overlay) and `WorldMapTests.DragCardEditInspectorStartSceneZoomAndUndo` (click NewScene, request T-D02). |
| T-C26 | TICK | `WorldMapTests.SceneContextMenu_...` right-clicks the Title card: request T-M22, menu opened. |
| T-C27 | TICK | `WorldMapTests.LinkChipOpensLinkSettings_ChangesAreSaved` clicks Chip_Goal_Flag: request T-O09, `LinkSettingsPopover`. |
| T-C28 | TICK | `PaletteManagerE2ETests.P11_36_...` clicks the Cycle field (request T-O10 CycleField, popover on Cycle) and the Fade field (popover on Fade). |
| T-C29 | TICK | `P11_35_ImportPalGplAndExport` clicks ImportPal (file picker seam `PickImportFile`): request (T-D04, PaletteImport), `ImportAssetDialog`; editor-gaps fixed row. |
| T-C30 | TICK | `FontE2ETests.NewFont_FromFontsPlus_...` clicks the Fonts header +: request (T-D15, FontsPlus), `NewFontDialog`. |
| T-C31 | TICK | Link #77 and `FontE2ETests.AddCharacters_PasteTextAddMissingGlyphs` click AddCharacters: request (T-D16), `AddCharactersDialog`. |
| T-C32 | TICK | `DatabaseWindowTests.P11_40_NewTable...` executes the Tables header's ActionCommand, the command the + button (PART_Action) is bound to (`Theme/Navigation.axaml:169`): `NewTableDialog`. No pointer click; the same header binding is clicked in other tests. |
| T-C33 | TICK | `P11_40_AddField...` executes the Schema header's bound ActionCommand: `AddFieldDialog` (same note as T-C32). |
| T-C34 | TICK | Link #78 and `P11_40_ImportCsv...` click the Import CSV button (system file dialog seam `FileChooser.OpenOverride`): `ImportCsvDialog`. |
| T-C35 | TICK | `LocalizationWindowTests.P11_42_AddLanguage...` executes the Languages header's bound ActionCommand: `AddLanguageDialog` (same note as T-C32). |
| T-C36 | TICK | `P11_42_ExportCsv...` clicks ExportButton; the save dialog seam (`FileChooser.SaveOverride`, default `SystemShell.PickSaveFileAsync`) is asked with "Export .csv" / "SkyHopper_strings.csv", and the file is written. |
| T-C37 | TICK | `AudioPageTests.PlusButtons_OpenImportAndSfxDesigner` clicks the Sound effects +: request (T-P25, AudioSoundAdd), `SfxDesignerPage` on a new effect. |
| T-C38 | TICK | Same test clicks the Music +: request (T-D04, AudioMusicAdd). |
| T-C39 | TICK | `DialogueWindowTests.P11_47_NewSpeaker...` executes the Speakers header's bound ActionCommand: `NewSpeakerDialog` (same note as T-C32). |
| T-C40 | TICK | `P11_47_AddLineChoiceCommandsConditionAndPaste` clicks AddBox: request (T-M23, Dialogue.AddBox); `P11_47_AddToDialogueAndCommandMenusMatchFigma` checks the items. |
| T-C41 | TICK | `DebuggerE2ETests.P13_09_...` clicks Views › Memory: Memory view shown. |
| T-C42 | TICK | Same test clicks Views › Palettes: Palettes view shown. |
| T-C43 | TICK | Link #79 clicks BuildAgain: Build ROM overlay asserted. (`BuildReportTests.HeaderFields_...` calls `page.BuildAgain()` directly, not a click, so it is not cited.) |
| T-C44 | TICK | `BuildRomTests.BuildRom_ScriptError_OpensBuildFailedAndRecordsHistory`: a real build with a broken script opens `BuildFailedDialog` with the failed step and the error. |
| T-C45 | TICK | `ImportDialogTests.Problems_OpenTheErrorsDialog_FixesAndSkipsErrors`: Import with problem files opens `ImportErrorsDialog`, request (T-D24, ImportAssetErrors). |
| T-C46 | TICK | Link #80 clicks RunInMgba: page T-P02 asserted. (`BuildReportTests.HeaderFields_...` calls `page.RunInMgba()` directly, not cited.) |
| T-C47 | TICK | Link #81 (FlashToCartridge, overlay) and `BuildReportTests.HeaderFields_...` (Click event raised, request T-D07). |
| T-C48 | TICK | `PopoverAndMenuTests.ComponentPicker_...` clicks NewScriptButton in the Component Picker: request (T-D14, ComponentPickerNewScript). |
| T-C49 | TICK | `PreferencesTests.Shortcuts_SearchPressAKeyRecord...` clicks the pencil (Edit_scene.toggleGrid): `RecordShortcutPopover`, request (T-O12). |
| T-C50 | TICK | `PreferenceEffectsTests.Paths_ProjectsFolderImageEditorTiled_AndBrowse` clicks TiledPath_Browse with the `SystemShell.FilePicker` seam: the picked path fills the field and the status turns to found. |
| T-C51 | TICK | `PopoverAndMenuTests.BudgetDetails_LiveNumbersHintAndBuildReport` raises Click on OpenBuildReport: request (T-P11, BudgetDetails), popover closes. |
| T-C52 | USER | The app has no "File › New Project (Empty)" item: File has New Project… only, and an empty project (template with every include off) opens T-P22 (`EmptyProjectTests.P11_09_EmptyProject_OpensWithEveryEmptyState`, which opens T-D01 through the window service, not the File menu). This is checklist 1.4 item 3, scope-log row "1.4 items 1-9" (pending). The T-P22 window page still lists "File › New Project (Empty)" as an opener, which the menu does not have. |

### 4.14 Prototype links

| Key | Decision | Reason |
|---|---|---|
| T-X01 | TICK | "AGB Studio · start" walked end to end from the Launcher (15 links in 5 windows); none of its links differs from the Figma node. |
| T-X02 | USER | "AGB Studio · editor" is walked end to end (46 links), but it includes #48, #73, #75, #76 (row "T-X03, T-M04, prototype links #48, #73, #75, #76", pending) and #55, which has the same kind of difference and is in no row yet. |
| T-X03 | USER | All 120 links pass as E2E cases, read from the read-only export `docs/evidence/figma/prototype.json` at test time; four go through another control (row "T-X03, T-M04, prototype links #48, #73, #75, #76", pending). Link #55 (Build menu title -> Build ROM) also needs two clicks in the app (menu, then Build ROM) but is not flagged in `prototype-links.md` nor in the row. |

### 4.15 Design tokens, type, icons, art

| Key | Decision | Reason |
|---|---|---|
| T-K01 | TICK | All 29 names of the checklist in both collections of `docs/evidence/figma/tokens.json`; `TokenTests.TokenFile_NamesAndValuesMatchFigma` (names and ARGB values, Dark and Light), `BothThemes_HaveIdenticalKeys`, `TokenFile_IsGeneratedFromFigmaExport`. |
| T-K02 | TICK (UT part) | `TokenTests.TextStyles_MatchFigma`: the 20 styles of the checklist, size, case, family, weight, line height, letter spacing vs the Figma export; `DesignSystemTests.Fonts_EveryTextStyleResolvesToItsBundledFace`. The SHOT type specimen is left to the SHOT reviewer. |
| T-K03 | TICK | REV: six families in `editor/AgbStudio.Editor/Assets/Fonts`, each with an `OFL.txt` naming its own copyright holder (Inter, JetBrains Mono, IBM Plex Sans Thai and Looped, Press Start 2P, Nunito with `Nunito-ExtraBold.ttf`); rows in `docs/THIRD_PARTY.md:22-26`; G-LEGAL.04 ticked by the other reviewer; `DesignSystemTests.Fonts_BundledWithOflLicences`. |
| T-K04 | TICK | `DesignSystemTests.Icons_All70ExportedFromFigma` (70 SVG files, 16 px vector), `Icons_EveryNameUsedInTheUiExists` (names in sources, command catalog and on screen in the shell and gallery). |
| T-K05 | TICK (REV part) | REV: `Assets/Art` holds art/scene x4, art/sprite x6, art/tileset x1, art/genre x8 and 19 thumb/*, exported read-only from the project's Figma file by `Shot/figma-export.mjs` (commit 7820b78; SVG clip ids carry Figma node ids); simple pixel art drawn for this design (looked at in the T-P20 Figma frame), no imported third-party files; used by name (`NewProjectTests.Catalog_EightTemplatesInFigmaOrder`, `LauncherE2ETests.P11_01_Projects_RecentListSearchThumbnailsTimesOpenAndNewProject`). SHOT on the windows that show them is left to the SHOT reviewer. |

### 4.16 Components (47 buildable)

| Key | Decision | Reason |
|---|---|---|
| C:Menu Item/implemented | TICK | 7 Figma variants in the gallery (Avalonia `MenuItem`, kinds and states); kinds, shortcuts and icons of the real menus match Figma in `CommandAndShortcutTests.Menu_MatchesFigmaItemsShortcutsIconsAndKinds`. |
| C:Menu Bar Item/implemented | TICK | Default / Open variants (`MenuBarItem` theme, `:open` set by Avalonia when the submenu opens); `ShellE2ETests.Menu_ClickFileOpensItAndHelpKeyboardShortcutsOpensTheDialog` opens File. |
| C:Button/implemented | TICK | 32 variants (4 kinds x SM / LG x Default / Hover / Pressed / Disabled), icon via `Glyph`; `AgbButton` is used across the windows. |
| C:Icon Button/implemented | TICK | 5 variants incl. Active (`IsActive`); used across the toolbars. The 3 px SHOT offset (scope-log row) is a SHOT matter. |
| C:Play Control/implemented | TICK | Play / Pause / Step / Stop (`PlayKind`); the toolbar Play button and its tooltip in `ShellE2ETests.Toasts_ShowAndCloseAndTooltipsUseTheTooltipComponent`. |
| C:Layer Toggle/implemented | TICK | On / Off / Active; used in the Scene Editor toolbar. |
| C:Chip/implemented | TICK | 6 styles (`ChipStyle`) with icon; used across the windows. |
| C:Switch/implemented | TICK | On / Off (`AgbSwitch`). |
| C:Checkbox/implemented | TICK | On / Off with label (themed `CheckBox`). |
| C:Segment/implemented | TICK | Default / Active / Hover. |
| C:Progress/implemented | TICK | OK / Warn / Danger / Accent x 0-100 in steps of 10 (44 ids). |
| C:Slider/implemented | TICK | 0-100 (11 ids); knob ring is `border/strong` (`Theme/Buttons.axaml:284`). The knob fill is hard-coded white: see T-L04. |
| C:Tree Item/implemented | TICK | Default / Selected / Hover x None / Open / Closed; `Meta` and `Glyph` properties. |
| C:Doc Tab/implemented | TICK | Default / Active / Hover; dirty dot (`IsDirty`) in `ShellE2ETests.Docking_PanelsTabsAndDirtyDot`. |
| C:Panel Tab/implemented | TICK | Default / Active / Hover; `Count` badge; the four bottom tabs in `ShellE2ETests.Docking_PanelsTabsAndDirtyDot`. |
| C:List Item/implemented | TICK | Default / Selected / Hover (`ListRow`). |
| C:Nav Item/implemented | TICK | Default / Active / Hover; used by Launcher and the settings dialogs. |
| C:Section Header/implemented | TICK | Open / Closed with `Meta`. |
| C:Field/implemented | TICK | Text / Dropdown / XY / Slider / Toggle (`InspectorField`, `FieldKind`). |
| C:Swatch/implemented | TICK | Default / Selected / Empty / Transparent. |
| C:Status Item/implemented | TICK | Default / Dot; status bar items in `ShellE2ETests.StatusBar_ShowsFixtureValuesAndRoutesClicks`. |
| C:Console Line/implemented | TICK | Info / Success / Warning / Error. |
| C:Toast/implemented | TICK | Success / Error; shown and closed in `ShellE2ETests.Toasts_ShowAndCloseAndTooltipsUseTheTooltipComponent`. |
| C:Asset Card/implemented | TICK | Default / Selected; used by the Assets panel and Asset Picker. |
| C:Frame Thumb/implemented | TICK | Default / Active; used by the Animation panel and Sprite Editor. |
| C:Scene Card/implemented | TICK | Default / Selected; used by Launcher and World Map. |
| C:Template Card/implemented | TICK | Default / Selected; used by Launcher · Templates and New Project. |
| C:Glyph Cell/implemented | TICK | Default / Selected / Missing; used by Font Manager and Add Characters. |
| C:Table Cell/implemented | TICK | Header / Default / Mono / Selected. |
| C:Text Input/implemented | TICK | Default / Focus (`:focus` on `PART_Box`, set by real focus) with `Help`; typing in `ShellE2ETests.Thai_TypingIntoTextFieldsAndSearch`. |
| C:Select/implemented | TICK | Default / Focus (`:focus-within`) with `Help` (`SelectField`). |
| C:Tab/implemented | TICK | Default / Active. |
| C:Node Header/implemented | TICK | 8 categories (`NodeCategory`); drawn by the Logic Graph canvas. |
| C:Node Pin/implemented | TICK | Exec / Bool / Fixed / Actor / Text x In / Out (10 ids; the Vector pin has no Figma variant and is left out of the gallery). |
| C:brand/app-icon/implemented | TICK | Light / Dark / Indigo (`AppIcon`); used in Launcher and About; window and exe icon in `ShellE2ETests.Window_HasTheAppIcon`. |
| C:Menu Separator/implemented | TICK | Built for every `SeparatorEntry` (`Shell/MenuBuilder.cs:42`); separator positions match Figma in `CommandAndShortcutTests.Menu_MatchesFigmaItemsShortcutsIconsAndKinds`. |
| C:Badge/implemented | TICK | Used in the Panel Tab template for counts. |
| C:Keycap/implemented | TICK | Used in shortcut lists and tooltips. |
| C:Panel Header/implemented | TICK | Used by the panels of the windows. |
| C:Search Field/implemented | TICK | Hierarchy search typed into in `ShellE2ETests.Thai_TypingIntoTextFieldsAndSearch`. |
| C:Meter/implemented | TICK | Used by the Inspector GBA Budget and other panels. |
| C:Tooltip/implemented | TICK | `TipContent` on the Play control in `ShellE2ETests.Toasts_ShowAndCloseAndTooltipsUseTheTooltipComponent`. |
| C:Title Bar/implemented | TICK | No brand text, starts at File: `ShellE2ETests.TitleBar_HasSevenMenusAndNoBrandText` (menus File..Help, no "AGB Studio" text in the bar; the template puts the menus in its first column, `Theme/Chrome.axaml:226-227`). |
| C:Dialog Header/implemented | TICK | Used by the dialogs; the title trims with an ellipsis. |
| C:Dialog Footer/implemented | OPEN | No test shows that a long hint never pushes the buttons out, and the template cannot promise it: `Theme/Chrome.axaml:266-272` docks the hint TextBlock Left before the Right-docked buttons with no width limit, trimming or wrapping, so a long hint takes the width first and squeezes the buttons. Needs: hint laid out after the buttons (or trimmed / wrapped) and a test with a long hint asserting the Cancel and Confirm buttons keep their full size inside the footer. |
| C:Popover Header/implemented | TICK | Used by the popovers. |
| C:brand/mark/implemented | TICK | `BrandMark` control (Plus Pad art, `Theme/Panels.axaml:442-446`) in the gallery and SHOT. No window places it: the Launcher brand block and About use brand/app-icon, as their SHOT frames (T-P12, T-D11) do. |

### 4.17 Brand (section "AGB Studio · Brand")

| Key | Decision | Reason |
|---|---|---|
| T-R01 | USER | Exe icon (`ApplicationIcon` Assets/agb-studio.ico) and window / taskbar icon (`ShellE2ETests.Window_HasTheAppIcon`); About places the app icon at 64 px (Windows/Menus/AboutDialog.cs:31). The item also names the installer, and there is none: D-19 (portable zip) is "decided (default)" only, so the user must confirm D-19. The look against Plus Pad is SHOT (other reviewer). |
| T-R02 | n/a | Verify is SHOT vs T-P12 only, so there is no non-SHOT part to judge (other reviewer). Facts for that review: Nunito ExtraBold is bundled with its OFL (`DesignSystemTests.Fonts_BundledWithOflLicences`) and the Launcher brand block draws the wordmark with it (Windows/Launcher/LauncherPage.cs:139-140). |
| T-R03 | USER | REV: four banners in docs/brand (read-only Figma export, b3c3b27), used at the top of README.md and docs/RELEASE-NOTES.md and copied into the package (tools/AgbStudio.Build/Packager.cs:43-44; `PackagingTests.PackageHoldsTheRuntimeFontsCoreAndDocsAndFindsItsRoot`; 2914d22). The item says "release page and installer"; there is no installer because D-19 is "decided (default)" only: the user must confirm D-19. |
| T-R04 | USER | RT part done: `RuntimeRomTests.RomPasses` (framework_ui: `boot_splash_then_title_menu` 60 frames for 1 s, `debug_builds_can_skip_the_splash`; runtime gates the skip with `AGB_DEBUG`, `game.cpp:511-515`), `UiKitTests.SplashTitleOptionsAndPauseInABuiltGame` (Plus Pad mark on screen in a built game), `ProjectSettingsTests.Build_ReadsTheSettings_RomHeaderSaveTypeStartSceneSplashRemap` ("AGB Studio logo · 2 s" reaches the build; seconds x 60 frames in `GameBuilder.cs:845`). The user must keep the GBA splash frame the build session added to Figma: scope-log row "1.4 item 6, D-20, D-23" (pending). |
