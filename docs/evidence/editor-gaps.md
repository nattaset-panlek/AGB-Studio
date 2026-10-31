# Editor gaps found while writing the window pages (2026-10-31)

Found by reading the code against the Figma captions and the checklist "from" lists; read against main at 47b2b7e.
Each open gap blocks the "matches Figma" or "implemented" box of its window until it is fixed or accepted as a
scope-log entry. Tests are in `tests/AgbStudio.Editor.Tests` and are named Class.Method; code paths are relative to
`editor/AgbStudio.Editor`.

## Still open

| Window | What still differs | Where to see it |
|---|---|---|
| T-P22 Scene Editor · Empty project | the caption mentions choosing a template; there is no template button (the Figma frame has none either) | `Windows/Launcher/EmptyProjectPage.cs` has no template action |
| T-S02 Save data | Figma gives the `int` fields `lives` and `coins` sizes of 1 B and 2 B; in AGB Studio the size always follows the type, so an `int` is 4 B and the sample project uses `byte` and `short` for these two fields | `Windows/Settings/ProjectSettingsDialog.cs:243` (`Size`); `ProjectSettingsTests.Tabs_ShowTheProjectsData` checks the sizes by type |
| T-S08 Paths | the hint under Image editor says it opens `.png` / `.aseprite` on double-click; it opens only `.aseprite` files and the background images in `assets/images`, while sprite and tileset PNG files keep opening in the Sprite Editor and the Tilemap Editor | `Windows/Settings/ExternalTools.cs:19-33` (`ProgramFor`), hint text at `Windows/Settings/PreferencesDialog.cs:290` |
| T-P02, T-P03, T-P04, T-P08 (prototype links #48, #73, #75, #76) | Figma's prototype links the Scene menu title of the title bar back to the Scene Editor; in AGB Studio that title opens the Scene menu (T-M04, as Editor Map board 2 draws it) and the Scene document tab goes back. Figma contradicts itself here (prototype vs menu board): a scope-log entry is needed | `PrototypeLinkTests.T_X03_Link_LeadsToTheSameWindowInTheApp` (#48, #73, #75, #76) checks both: the menu opens, the tab leads back; `docs/evidence/prototype-links.md` |

## Prototype links (T-X01..T-X03)

All 120 click links of the Figma prototype pass as E2E cases (`PrototypeLinkTests`, read from
`docs/evidence/figma/prototype.json` at test time; the app control of each link is listed in
`docs/evidence/prototype-links.md`), and both flows are followed end to end (`PrototypeFlowTests`: "AGB Studio ·
start" 15 links from the Launcher, "AGB Studio · editor" 46 links from the Scene Editor). 116 links use the node Figma
links (the 3 asset cards with a double-click, as Editor Map #24 says; the footer Cancel of About and Keyboard
Shortcuts, hidden in both Figma frames, is the footer's Close button); 4 reach the same window through another
control (the row above). Writing them found and fixed:

| Found by | Problem | Fix and test |
|---|---|---|
| #80 Build Report › Run in mGBA | a file the emulator core refuses left a half-made play session; Stop or closing the editor then crashed the process (access violation reading save RAM) | `PlayController.Run`; `PlayRomErrorTests.Run_AFileTheCoreRefuses_ReportsIt_AndStopAndCloseStillWork` |
| T-X02 flow (Debug, then Play) | Play, Build ROM, Build & Run, Flash and Clean stayed greyed out after a build ended until some window opened (the build announced its end before it was over) | `BuildService.BuildAsync`; `BuildEndsTests.PlayAndBuildRom_AreEnabledAgainTheMomentTheBuildEnds` |

## Fixed

| Window | Gap as found | Shown by |
|---|---|---|
| T-P01 Scene Editor | Paint tiles, Fill and Erase only highlighted the tool; clicks in the scene view always selected and moved actors | `ViewportTests.PaintFillAndErase_EditTheMapWithUndo` |
| T-P05 Console | the game's own log lines (Debug.Log) went to Play Mode's console only | `PlayAcrossEditorTests.GameLogLines_ReachTheConsolePanel`, `PlayAcrossEditorTests.ExternalSave_WhileTheGameRuns_HotReloads_AndItsDebugLogReachesTheConsolePanel` |
| T-P06 Animation panel | speed and frame order could only be previewed, not changed | `BottomPanelTests.Animation_SpeedAndFrameOrder_SavedToTheSpriteMetaWithUndo` |
| T-P08 Sprite Editor | the drop zone said PNG / Aseprite but imported only PNG | `SpriteEditorE2ETests.P11_24_Import_DropZoneAutoReducesAndSplitsSheet` |
| T-P19 Debugger | breakpoints could not be added, so the list was always empty | `BreakpointUiTests.Breakpoints_FromTheScriptEditorGutterF9AndTheBreakpointsView_StopTheGame` |
| T-P19 Debugger | the OAM sprites, Profiler and Breakpoints views showed the VRAM tiles screen | `BreakpointUiTests.OamSpritesAndProfilerViews_ShowTheirOwnData` |
| T-P24 Debugger · Palettes | "Used by" did not name the palette effect | `DebuggerE2ETests.P13_04_Palettes_LiveRam_ChangedOutline_AddressBgr555Rgb_UsedBy_OpenAndBreak` |
| T-D04 Import Asset | `.aseprite` files were not detected | `AsepriteImportTests.Aseprite_IsASpriteWithItsFrames_AndImportsForTheBuild` |
| T-D04 Import Asset | the Sprite Editor drop zone did not open Import Asset | `SpriteEditorE2ETests.P11_24_Import_DropZoneAutoReducesAndSplitsSheet` |
| T-D04 Import Asset | Palette Manager › Import .pal / .gpl did not open Import Asset | `PaletteManagerE2ETests.P11_35_ImportPalGplAndExport` |
| T-D07 Flash to Cartridge | the cartridge line showed the game code, not the flash and save sizes | `FlashTests.FlashSize_FromAddressMirrors`, `FlashTests.Detects_BacksUp_Writes_Verifies_WithProgress` |
| T-D09 Preferences · Emulator | the settings were saved but Play Mode ignored them | `PlayAcrossEditorTests.EmulatorPreferences_TakeEffectInPlayMode` |
| T-D10 Keyboard Shortcuts | listed ten shortcuts, not all; no link to Preferences › Shortcuts | `AboutTests.KeyboardShortcuts_GroupsAndSearch` |
| T-S04 Scripting | "Show inline hints" had no effect | `ScriptEditorSettingsTests.InlineHints_OffKeepsTheHintClosed_OnOpensIt` |
| T-S06 Appearance | "Reduce motion" and "Show pixel grid" had no effect | `PreferenceEffectsTests.Appearance_PixelGridAt400Percent_AndReduceMotion`, `LogicGraphTests.Preview_PlaysTheSceneAnimations` |
| T-S07 Editor | default snap, collision overlay and GBA budget in Inspector had no effect | `PreferenceEffectsTests.Editor_DefaultSnapCollisionOverlayAndBudgetInInspector` |
| T-S07 Editor | the code editor font and theme had no effect | `ScriptEditorSettingsTests.CodeFontSizeFontAndTheme_ApplyToTheCodeEditor` |
| T-S08 Paths | projects folder, image editor and Tiled had no effect (the image editor's file types are under Still open) | `PreferenceEffectsTests.Paths_ProjectsFolderImageEditorTiled_AndBrowse` |
| T-O01 Scene Switcher | no "boss" tag on scene rows | `SceneDialogTests.SceneSwitcher_StoryOrderSearchSwitchAndNewScene` |
| T-O04 Asset Picker | the Tilemap Editor's Tileset dropdown opened the Sprites tab | `TilemapE2ETests.TilesetPanel_DropdownBrushStampRandom_PaintWithTheMouseSaveUndo` |
| T-O07 Command Palette | picking a `.scene` file did not switch to that scene | `ShellE2ETests.CommandPalette_FileResults_OpenTheirEditorAndSceneSwitches` |
| T-O08 Add Node | Logic Graph › Nodes › Add node… was a search box in the Nodes panel, not this popover | `LogicGraphTests.NodesAddNode_OpensTheAddNodePopover` |
| 15 pages: T-P03, P04, P07-P11, P13-P18, P22, P25 | Pause and Step in the toolbar did nothing (they worked only in the Scene Editor and Play Mode) | `PlayAcrossEditorTests.PauseAndStep_OnEveryPage_ActOnTheRunningGame` |
| T-P04 Script Editor | the "Analyzer: GBA rules on" chip opened Project Settings on Input instead of Scripting | `ScriptEditorSettingsTests.AnalyzerChip_OpensProjectSettingsOnScripting` |
| T-D12 Unsaved Changes | always said "Save changes before exiting?", also when opening another project | `OpeningProjectTests.P11_06_UnsavedChangesAreAskedFirstAndBrokenProjectsFailCleanly` (opening another project); the New Project title "Save changes before closing the project?" has no test (`Windows/Launcher/NewProjectDialog.cs:242`, `Shell/Dialogs.cs:166` `UnsavedChangesDialog.TitleFor`) |
| Create menu | Create › Tileset / Tilemap opened the current map instead of making a new one; Create › Font opened Font Manager, not New Font | `MenuActionTests.Create_TilesetTilemapMakesANewMap_FontOpensNewFont` |
| T-P17 Logic Graph | the Link tool had no action | `LogicGraphTests.LinkTool_DragsAWireFromNodeToNode` |
| T-P17 Logic Graph | the Preview was a still picture | `LogicGraphTests.Preview_PlaysTheSceneAnimations` |

## Accepted by decision

- T-D19 Import CSV: Google Sheets is CSV export only (D-13). Still so at 47b2b7e: Database › Import CSV picks a
  `.csv` file only (`Windows/Database/DatabasePage.cs:415`).
- T-D07 GBxCart RW: writing is refused until verified on hardware (D-12, `HW`). Still so at 47b2b7e
  (`Windows/Build/CartridgeDevices.cs:215-219`, `FlashTests.VerifyFailure_NoCartridge_AndGbxCartWriteAreReported`).

## Other differences noted on the window pages

These "Differs from Figma" lines are kept on the window pages; they are not listed as gaps here.

- T-D01 New Project: eight template cards in a scrolling grid (adds Racing (Mode 7) and Beat 'em up); the Figma frame
  shows six (`Windows/Launcher/TemplateCatalog.cs`).
- T-P18 Scripting API: the page also ends with a "See also" row of buttons; the Figma frame lists See also only under
  On this page (`Windows/Scripts/ScriptingApiPage.cs:161-169`).
- T-S08 Paths: the build cache is per project (`cache` and `obj` in the project's build output folder), not one shared
  folder under `%LOCALAPPDATA%` (`Windows/Settings/PreferencesDialog.cs:353-369`).
