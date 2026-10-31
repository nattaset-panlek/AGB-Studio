# Review: Phases 10-14, and a re-check of 18 earlier OPEN items (2026-10-31)

Independent read-only review of `docs/ENGINE-CHECKLIST.md` in the snapshot worktree `D:\devkitPro\AGB-Studio-wt\review`
(detached at `c407d74`). Nothing was built or run. Test results are the green CI run on `ddf96c0`
(`build\ci\runs\20261031-130823-ddf96c0`: build 0 warnings, lint clean, commit checks, `AgbStudio.Tests` 170/170,
`AgbStudio.Scripting.Tests` 96/96, `AgbStudio.Editor.Tests` 512/512); `c407d74` adds the music-volume change with its
own tests (`MusicMetaTests.SceneMusicVolumeGoesIntoTheSceneDefinition`, ROM test
`options_music_volume_survives_scene_changes_and_the_scene_volume_applies`) and two scope-log rows.

- Part A: every unticked line of Phase 10 to Phase 14 (entry gates, items, exit gates): 124 lines.
- Part B: 18 items an earlier review (`docs/evidence/review-p0-9-15-16-gates.md`) found OPEN and that have been worked
  on since: P0.01, P1.04, P1.06, P2.04, P3.05, P3.11, P3.12, P7.12, P7.15, P8.01, P16.04, P16.05, G-HW.01, G-ERR.01,
  G-DOCS.05, G-SCOPE.01, P4.X1, P5.X1.
- Part C: the REV of G-DOCS.04, written as `docs/evidence/rev-docs-c407d74.md`.

Decisions:

- **TICK**: done, and the evidence its Verify asks for exists. The evidence line is in
  `docs/evidence/evidence-p10-14-recheck.tsv`.
- **HW**: everything but the hardware check is evidenced; the item allows `n/a until hardware` (P0.09 waits for the user).
- **USER**: only the user can close it: an entry or exit gate, a `USER` verify, a decision, or a pending scope-log row
  (section 8) whose answer can change what the item delivers. "(blocked: ...)" means it should be shown only after the
  named items close.
- **OPEN**: something the item text or its Verify asks for is missing.

Evidence conventions: `test:Class.Method` for C# tests and `test:<name>` for runtime ROM tests (`AGB_TEST(name)` in
`runtime/tests/*/main.cpp`). All 316 test names in the TSV were checked against the test sources at `c407d74` (a name
that does not exist would have been caught), and every `doc:` and `shot:` path exists. `commit:c407d74` everywhere;
P1.06 also cites the CI run's `summary.txt` as `report:` (git-ignored, in the main working copy). The tick date is left
to the person who ticks.

SHOT rows were judged as the non-author reviewer of section 2.2: offset within 2 px in `docs/evidence/shot-report.md`,
then the `docs/evidence/diff/<id>.png` picture (Figma | app | red difference) was looked at for the regions the item
names. A ticked item does not tick the "matches Figma" box of section 4; those boxes are not part of this review.

Scope-log rows: section 8 has one general row ("Code differs from Figma captions (list in docs/evidence/editor-gaps.md)
... Fix in the editor before ticking those windows"). Its action is a fix, not a user choice, so a window whose gap
`editor-gaps.md` lists as fixed (with the test that shows it) is not held by that row. The gaps it still lists are
T-P22, T-S02, T-S08 and the prototype links; their effect is under P11.09, P11.61, P11.62 below.

## Counts

| Group | TICK | HW | USER | OPEN | Total |
|---|---|---|---|---|---|
| Phase 10 | 16 | 0 | 3 | 3 | 22 |
| Phase 11 | 63 | 1 | 4 | 1 | 69 |
| Phase 12 | 10 | 0 | 2 | 1 | 13 |
| Phase 13 | 9 | 0 | 2 | 0 | 11 |
| Phase 14 | 5 | 0 | 2 | 2 | 9 |
| **Part A** | **103** | **1** | **13** | **7** | **124** |
| **Part B** | **10** | **0** | **4** | **4** | **18** |
| **A + B** | **113** | **1** | **17** | **11** | **142** |
| Part C (G-DOCS.04) | 0 | 0 | 0 | 1 | 1 |

## Part A: every item

### Phase 10

| Item | Decision | Reason |
|---|---|---|
| P10.E1 | USER | (blocked: P1.X1, because P1.04 is OPEN below) Show D-01 (Avalonia 11 / .NET 10) and D-10 (Windows only), both "decided (default)" in `docs/DECISIONS.md`. |
| P10.01 | TICK | Title bar without brand text, 7 menus, status bar (`ShellE2ETests`, `Menu_MatchesFigma...`); SHOT chrome regions of T-P01 0.9-3.6 %, T-M01..T-M07 1.4-3.2 %, `C-title-bar` 2.58 %, all offset 0. The whole-frame 14 % is scene content (Phase 11). |
| P10.02 | TICK | 29 tokens, both themes, same names, generated from the Figma export (`TokenTests`). |
| P10.03 | TICK | 20 text styles match Figma (`TokenTests.TextStyles_MatchFigma`), fonts bundled with OFL licences; SHOT type specimen: 17 styles with a Figma sample, offset 0-1 px; the diff is glyph rasterisation (Figma alpha-only export vs Skia), same text, face, size and position side by side. Header 1, Header 2 and Body have no Figma sample and no use. |
| P10.04 | TICK | 70 icons exported from Figma, every name used exists (`DesignSystemTests`). |
| P10.05 | USER | 213 of 215 component variants within 2 px; `C-icon-button-default` and `C-play-control-step` are 3 px off and wait on scope-log row "C-icon-button-default, C-play-control-step" (accept as export artefact). |
| P10.06 | TICK | `ShellE2ETests.Docking_PanelsTabsAndDirtyDot`. |
| P10.07 | TICK | Dark / Light / System, accent, scale (`Theme_DarkLightAccentScaleAndReduceMotion`); reduce motion and pixel grid at 400 % now act (`PreferenceEffectsTests.Appearance_...`); code font size (`ScriptEditorSettingsTests.CodeFontSize...`); no colour literals (T-L04 scan); SHOT T-S06 0.87, T-L03 0.84, T-L02 2.28 %, T-L01 chrome regions 1.0-3.8 %. |
| P10.08 | OPEN | Exit and "opening another project" ask first (tested); the "close" case has no E2E: New Project with a dirty document asks "Save changes before closing the project?" (`Windows/Launcher/NewProjectDialog.cs:242`, `Shell/Dialogs.cs:169`), never run by a test. |
| P10.09 | TICK | `ServiceTests.Undo_RespectsHistoryLimitFromPreferences` (200 steps, then the Preferences limit). |
| P10.10 | TICK | `ServiceTests.Autosave_SavesDirtyDocumentsEveryNMinutes`. |
| P10.11 | OPEN | E2E exists per target but not per command: actors have no Cut or Select All (`HierarchyTests.EditMenu_...` runs duplicate, delete, copy, paste); assets no Select All; table rows only Duplicate and Delete; dialogue lines only Copy and Paste. Logic Graph nodes cover all six. |
| P10.12 | TICK | Ctrl+K, arrows, Enter, ">" commands only; file results open their editor and `.scene` switches the scene (`CommandPalette_FileResults_...`, fixed); Find in Project opens it (`MenuActionTests`). |
| P10.13 | TICK | Defaults as in the Figma menus, rebinding, "Ctrl+G is used by Go to line", reset (`CommandAndShortcutTests`, `PreferencesTests.Shortcuts_...`), T-D10 lists them (`AboutTests.KeyboardShortcuts_GroupsAndSearch`). |
| P10.14 | TICK | Preferences in `%APPDATA%\AGBStudio`, Project Settings in the `.agbproj` (`ServiceTests`). |
| P10.15 | TICK | `RobustnessTests.Startup_OpensTheLastProjectOrTheLauncher` (no last project, preference on / off, project gone); `App.axaml.cs:29` calls `Startup.Choose`. |
| P10.16 | OPEN | The fake-folder UT (`ServiceTests.Toolchain_ReportsFoundAndNotFoundWithFakeFolders`) covers devkitPro, libtonc, maxmod, gbafix and devkitARM missing; no UT shows devkitARM found with its version (`gcc -dumpversion`) or the image editor's found / not found status (script editor: `ExternalEditorDetectionTests`; Tiled: `PreferenceEffectsTests.Paths_...`). |
| P10.17 | TICK | T-Y01 folder picker, T-Y02 save dialog, T-Y03 external editor at the line, T-Y04 Browse… (now `PreferenceEffectsTests.Paths_..._AndBrowse`), Show in Explorer (`SystemShellTests`), T-M17 and T-M13 menus. The extra OS dialogs wait on scope-log row G-SCOPE.02, which this item does not list. |
| P10.18 | TICK | SHOT `C-toast-success` 4.41 % (offset 1), `C-toast-error` 4.41 %, `C-tooltip` 3.53 %; `ShellE2ETests.Toasts_...`. |
| P10.19 | TICK | `ServiceTests.Recent_OpenRecentClearAndTimes`, Launcher list with times; SHOT T-M08 2.16 %. |
| P10.20 | TICK | Thai typed in the shell, Localization, Dialogue, Database and Font Manager fields. |
| P10.X1 | USER | (blocked: P10.08, P10.11, P10.16 and the P10.05 scope row) Show `docs/evidence/shot-report.md` and the `diff/` pictures of the shell, tokens, type specimen and components. |

### Phase 11

| Item | Decision | Reason |
|---|---|---|
| P11.E1 | USER | (blocked: P10.X1, and the runtime phase exits each window needs) Confirm per window. |
| P11.01 | TICK | Recent list, search, thumbnails, times, Open…, New Project (`LauncherE2ETests.P11_01_*`). |
| P11.02 | TICK | Filters, details "Includes", Use this template. Scope-log row "T-P20 Racing card" is about the template, not the card. |
| P11.03 | TICK | Tutorials with progress, documentation links, samples made and opened. |
| P11.04 | TICK | Sub-steps tick themselves, Back / Skip / Next (`TutorialTests`). |
| P11.05 | TICK | Context menu actions; Delete asks first and names what is removed. |
| P11.06 | TICK | Cancel during load leaves the open project as it was; full open. |
| P11.07 | TICK | What's new from the release notes; the package writes `bin/release-notes.md` (`PackagingTests`). |
| P11.08 | TICK | Each option on and off changes the created project, ROM title / game code rules, devkitPro warning, save type. The dialog shows 8 template cards where the frame shows 6 (D-09, checklist 1.4 item 4, waiting with P1.02). |
| P11.09 | USER | Empty project opens T-P22 with every empty state (`EmptyProjectTests`); waits on scope-log row "T-P22" (caption names a template choice the frame does not have: change the caption, or add the control). |
| P11.10 | TICK | Viewport pixels equal the mGBA core's frame of the same scene (`ViewportRomTests.Viewport_DrawsTheSceneLikeTheRom`); 240 x 160 frame, zoom, pan, cursor. |
| P11.11 | TICK | Layer toggles, chips, grid. |
| P11.12 | TICK | Tree, search, add, context menu, selection sync, rename. |
| P11.13 | TICK | Header, Transform, Sprite, behaviours, Collider, script fields, Add Component, GBA Budget. |
| P11.14 | TICK | Categories with counts, double-click opens the right editor, context menu, drag into the scene. |
| P11.15 | TICK | Scene Switcher with search, New Scene and the "boss" tag (fixed). |
| P11.16 | TICK | Filters with counts, click opens the code. |
| P11.17 | TICK | Speed and frame order now editable with undo (`Animation_SpeedAndFrameOrder_...`). |
| P11.18 | TICK | New Scene and Scene Settings, every section. |
| P11.19 | TICK | Video Mode, Grid & Snap, Asset Picker, Component Picker, Budget Details. |
| P11.20 | TICK | Every item of the seven Scene Editor menus. |
| P11.21 | TICK | Set as Start Scene, Background Layers…. |
| P11.22 | TICK | Canvas, tile grid, zoom, tools, frame index, save. |
| P11.23 | TICK | Palette panel. |
| P11.24 | TICK | Drop zone imports PNG and Aseprite, goes through Import Asset (fixed). |
| P11.25 | TICK | OAM size menu, origin, collider, mirror, VRAM per frame. |
| P11.26 | TICK | Timeline. |
| P11.27 | TICK | New Sprite. |
| P11.28 | TICK | The Tileset dropdown opens the Tilesets tab (fixed); brush, stamp, random. |
| P11.29 | TICK | Tile properties and collision view. |
| P11.30 | TICK | Layers table and Add Layer. |
| P11.31 | TICK | Map size, BG size, colours, wrap, stats. |
| P11.32 | TICK | Auto-tile rules. |
| P11.33 | TICK | Banks with Used by, colour editor. |
| P11.34 | TICK | UT merge result, sort, lock index 0, Optimize. |
| P11.35 | TICK | Import .pal / .gpl through Import Asset (fixed), Export. |
| P11.36 | TICK | Fade / Cycle / Flash with preview and the popover. |
| P11.37 | TICK | Font Manager lists, settings, glyphs, preview equal to the ROM renderer. |
| P11.38 | TICK | New Font and Add Characters. Scope-log row "T-D16, P8.08" concerns Figma's sample count, not the dialog. |
| P11.39 | TICK | Tables, grid, filter, Add row, record, schema, "Use it in a script", "Used by". Scope-log row "T-P07, P9.07" concerns the status-bar byte figure, which the item does not list. |
| P11.40 | TICK | New Table, Add Field, Import CSV. |
| P11.41 | TICK | Languages, table, fit, search, groups, notes, speaker, preview, missing strings. |
| P11.42 | TICK | Add Language, Export .csv. |
| P11.43 | TICK | Music list, track info including loop start, volume and fade, tracker view, playback. |
| P11.44 | TICK | Channel usage, mixer, Preview SFX, audio ROM and mixer CPU from the runtime measurements. Scope-log row G-PERF.05 may change the default rate and channels, not this page. |
| P11.45 | TICK | SFX Designer fields, presets, Bake to .wav, Wave channel; preview compared with the mGBA core on all four channels. |
| P11.46 | TICK | Conversations, lines, choices, speakers, line inspector, preview. |
| P11.47 | TICK | New Speaker, Add to Dialogue and Command menus. |
| P11.48 | TICK | Canvas, wires, zoom, palette, inspector, Generated C# in sync, Link tool and live Preview (fixed). Scope-log row "T-P17 On Touch Tag" concerns the generated code's shape. |
| P11.49 | TICK | Add Node popover from the canvas and from Nodes › Add node… (fixed), Node Context Menu. |
| P11.50 | TICK | Scene cards, links, start marker, inspector, New Scene, zoom. |
| P11.51 | TICK | Link Settings, Scene Context Menu. |
| P11.52 | TICK | Code editor features; code font, size and theme now applied (fixed). |
| P11.53 | TICK | Side panel. |
| P11.54 | TICK | Problems / Terminal / Output, status line. |
| P11.55 | TICK | Find & Replace, New Script, Open externally. |
| P11.56 | TICK | API tree, search, members, Copy example, see also, works offline. The extra "See also" row waits on scope-log row G-SCOPE.02 (keep or remove), which does not affect the item. |
| P11.57 | TICK | Build ROM streaming from the real build, Failed, Go to first error; buttons come back when the build ends. |
| P11.58 | TICK | Build Report fields and actions, Run in mGBA. |
| P11.59 | TICK | Export ROM fields; the padded ROM boots to the same frames. |
| P11.60 | HW | Flow with fake devices (`FlashTests`, cartridge line now shows flash and save sizes); hardware n/a; GBxCart RW writing refused until verified (D-12). |
| P11.61 | USER | Every field persists and changes the build (`ProjectSettingsTests.EveryField_PersistsToTheAgbproj`, `BuildTab_FieldsChangeTheBuild`, `Build_ReadsTheSettings_...`, inline hints fixed); the Save data tab waits on scope-log row "T-S02" (byte / short / int vs Figma's int with 1 B / 2 B sizes). |
| P11.62 | OPEN | Every field persists and the Emulator, Appearance, Editor and Paths fields now act; two Paths differences have no fix and no scope-log row: the Image editor hint says it opens `.png` / `.aseprite`, but sprite and tileset PNGs still open in the Sprite / Tilemap Editor (`Windows/Settings/ExternalTools.cs:19-33`, hint `PreferencesDialog.cs:290`); the build cache is per project, not the one `%LOCALAPPDATA%` folder of the frame (`PreferencesDialog.cs:353-369`). |
| P11.63 | TICK | Keyboard Shortcuts lists every bound command by group and links to Preferences › Shortcuts (fixed); About. |
| P11.64 | TICK | Detection incl. `.aseprite`, options, count, errors dialog with fixes; Sprite drop zone and Palette Import use it. Scope-log row "T-D24, P7.03" (4 palettes, not 3) changes only the Figma text. |
| P11.65 | TICK | Every title-bar command acts or opens its window, project commands disabled without a project, Create › Tileset / Tilemap / Font fixed, every context menu tested; all 120 prototype links pass. Scope-log row "T-X03, T-M04" concerns the menu title, not the items. |
| P11.66 | TICK | F12 PNG, GIF recording, 30 s stop, core capture. |
| P11.67 | TICK | Report a Bug, Check for Updates per D-13. |
| P11.X1 | USER | (blocked: none of the 410 section 4 boxes is ticked; P11.62; scope-log rows T-P22, T-S02 and the others in section 8) Then the user confirms the windows. |

### Phase 12

| Item | Decision | Reason |
|---|---|---|
| P12.E1 | USER | (blocked: P7.X1 and P10.X1) D-06 decided by the user; D-16 "decided (default)". |
| P12.01 | TICK | 3x, pixel-perfect, audio, pause when unfocused, Tab 4x, 59.7275 fps; Preferences · Emulator now drives Play Mode (`PlayAcrossEditorTests.EmulatorPreferences_TakeEffectInPlayMode`). |
| P12.02 | TICK | Play (incremental), Play from Here, Build & Run, Run in mGBA; a refused ROM is reported. |
| P12.03 | TICK | Play / Pause / Step / Stop; Pause and Step act from every page. |
| P12.04 | TICK | 1/4x to 4x, frame step; SHOT T-M18 0.78 %. |
| P12.05 | OPEN | The overlays draw (pixel E2E), but the SHOT of the game view differs by 32.91 % (`T-P02-game-view`): Figma shows a drawn moment (player jumping, hitbox, a yellow velocity arrow, "SCORE 0120" and hearts), the app the running fixture (player idle, HUD label only); the app draws no velocity arrow. No scope-log row accepts the difference. |
| P12.06 | TICK | Live Inspector values change while running. |
| P12.07 | TICK | Engine lines and a real `Debug.Log` from a saved script reach the Console panel with times (fixed). |
| P12.08 | TICK | Frame, fps, VBlank free, CPU, Running / Paused, Hot reload. |
| P12.09 | TICK | Keyboard and XInput pad from Project Settings · Input. |
| P12.10 | TICK | Save file kept per project across Stop and Play. |
| P12.11 | TICK | Script save continues the same scene with the new code (D-15). |
| P12.X1 | USER | (blocked: P12.05) Show the fixture playing at full speed in Play Mode with the live Inspector and Console. |

### Phase 13

| Item | Decision | Reason |
|---|---|---|
| P13.E1 | USER | (blocked: P12.X1) D-21 "decided (default)". |
| P13.01 | TICK | RT `frame_loop_counts_and_profiles`; Profiler 120 frames, Logic / Render / Audio / Idle; the Profiler view shows its own data (fixed). |
| P13.02 | TICK | VRAM tiles equal the core's memory. |
| P13.03 | TICK | OAM table and in-use count. |
| P13.04 | TICK | Palettes view; "Used by" names the palette effect (fixed). |
| P13.05 | TICK | Memory map, watch list, hex, Add watch. |
| P13.06 | TICK | Breakpoints set from the gutter (F9) and the Breakpoints view stop the game (fixed); VM and "On scene load"; C# call stack at `PlayerController.cs:19`. |
| P13.07 | TICK | UT palette-sharing suggestion for the fixture; live budget. |
| P13.08 | TICK | History across builds, failures included. |
| P13.09 | TICK | Debug button and Tools › Debugger. |
| P13.X1 | USER | Show every Debugger view (Profiler, VRAM, OAM, Palettes, Memory, Breakpoints) on the running fixture. |

### Phase 14

| Item | Decision | Reason |
|---|---|---|
| P14.E1 | USER | (blocked: P10.X1; P6.X1 is USER) |
| P14.01 | TICK | Built-in / External, VS Code / Rider / Visual Studio / Custom detection incl. `%LOCALAPPDATA%\Programs\Microsoft VS Code\Code.exe`. |
| P14.02 | TICK | `dotnet build` of the generated project reports the same GAE diagnostics; the script build rewrites it when it exists (`ScriptBackend.cs:43-44`). |
| P14.03 | OPEN | `docs/evidence/external-ide.md`: Visual Studio 18 is installed and was not checked; VS Code and Rider are recorded n/a. Only the command lines are tested with fake executables. |
| P14.04 | TICK | An external save while the game runs hot-reloads it and its `Debug.Log` reaches the Console (fixed); the folder watcher path in `ScriptToolsTests`. |
| P14.05 | TICK | New Script "Open in the external editor after creating". |
| P14.06 | TICK | Reference assembly with XML docs; autocomplete lists the P6.09 members. |
| P14.07 | OPEN | No check inside any IDE: Visual Studio 18 not checked (`external-ide.md`); in-process and command-line evidence only. |
| P14.X1 | USER | (blocked: P14.03, P14.07; VS Code is not installed on this machine) The user installs VS Code (or names another IDE) and watches a script edited there reload in the running game. |

## Part B: re-checked items

| Item | Was | Decision | Reason |
|---|---|---|---|
| P0.01 | OPEN | TICK | `docs/TOOLCHAIN.md` "Compiler version (P0.01)" holds the saved `arm-none-eabi-gcc --version` output (16.1.0). |
| P1.04 | OPEN | OPEN | The palette library now has `palettes.schema.json` with a schema-checked round trip. New gap: the editor writes fields that are in no schema, and the schema check refuses unknown fields (`tests/AgbStudio.Tests/SchemaTests.cs:259`): `.map` `bgSize` / `bpp` (`MapDocument.cs:112-123`), `.font` `drawTo` (`FontLibrary.cs:76-77`), `.dlg` speaker `names` / `color` (`ProjectData.cs:66-75`), `.graph` `comments` / `collapsed` / `disabled` (`GraphDocument.cs:64-110`). The fixture test passes only because the fixture has none of them. |
| P1.06 | OPEN | TICK | `ci.cmd` builds, lints, checks commits, runs all three suites and keeps artifacts per run (`tools/ci-artifacts.ps1`; the repository is local, D-04); green run `20261031-130823-ddf96c0` on `ddf96c0` (`summary.txt`: passed). Runtime ROMs run in the libretro core in place of mgba-rom-test (D-06, user). |
| P2.04 | OPEN | TICK | Text panels, the text layer and the Mode 3 fill now go through queued fills and sequences (`97c7c3c`); `copyNow` / `fillNow` remain only for staged loads and Debug builds count and log a write while the picture shows; the deferral log line is asserted; lint clean. |
| P3.05 | OPEN | TICK | `parallax_scroll_registers` asserts x0.2, x0.5 and now x1.2 (scroll 599 / 239 and the drawn window); x1.0 in `big_camera_jump_fits_in_a_frame` and the viewport test. |
| P3.11 | OPEN | USER | MusicMeta volume, loop start and fade are built and applied (`music_meta_comes_from_the_build`, `song_end_continues_at_the_loop_start`, `fade_on_scene_change_follows_the_track`, `options_music_volume_survives_...`, `MusicMetaTests`). Left: BUD "near 6 %" waits on scope-log row G-PERF.05 (14.5 % measured), and the EMU listen check is a person listening. |
| P3.12 | OPEN | TICK | `DmgSoundTests.Preview_SoundsLikeTheEmulator` now compares a Wave triangle and a drawn Wave pulse too (pitch, duty share, length) besides Square 1 (sweep), Square 2 and Noise; 0 bytes of samples (`BakeToWav_..._HardwareKeepsZeroBytes`). |
| P7.12 | OPEN | TICK | report.json gives VRAM, palette and RAM per asset; `report.schema.json` and `report-history.schema.json`; `ReportNumbersMatchTheLinkerMap` validates a real report against them and checks the per-asset numbers against the map. |
| P7.15 | OPEN | TICK | `ExportRomBootTests.PaddedReleaseRom_BootsToTheSameFramesAsTheBuiltRom`: a Release ROM padded by the Export code shows the same frames as the built ROM at frames 120, 240 and 360 in the mGBA core. |
| P8.01 | OPEN | TICK | OTF with CFF outlines builds the same game font as the TrueType font; CFF charstrings; CFF2 / TTC / WOFF refused with the reason; BDF and image sheets. |
| P16.04 | OPEN | TICK | Icon (exe `ApplicationIcon`, window icon test), wordmark and brand block (T-P12 1.47 %), About icon (T-D11 2.73 %), app icons 0-0.24 %; the four banners are the Figma export, used in `README.md`, `docs/RELEASE-NOTES.md` and the package (`docs/brand/README.md`). There is no installer (D-19 zip). |
| P16.05 | OPEN | OPEN | What's new reads the 1.0.0 notes in the package. But `docs/RELEASE-NOTES.md:32` says "Video Mode 3 bitmap scenes: full-screen pictures and filled rectangles": a Mode 3 scene only sets the display mode (`runtime/src/framework/game.cpp:189`), the build draws no bitmap and the C# API has no bitmap call (`tools/AgbStudio.Api`), so a user cannot make one; the old Known limit was right. Line 41 also leaves out that sprite VRAM over the limit fails the build. |
| G-HW.01 | OPEN | TICK | `render_limits` ROM fills BG and OBJ VRAM past the limits and checks the refusal and Console warnings, also in bitmap mode (16 KB OBJ); the build fails with the scene named; char and screen block plan tests. |
| G-ERR.01 | OPEN | OPEN | `error-strings-review.md` reports 396 / 396 OK, but it predates strings added since: `tools/AgbStudio.Build/Graphics/Aseprite.cs:51`, `:56`, `:69`, `:147`, `:298` ("is not an Aseprite file.", "a cel in frame N is damaged.", "The Aseprite file ends too early." ...) say what but not how to fix, and reach the user as is (`GameBuilder.cs:195`); `runtime/src/audio/audio.cpp:90` ("%d mixing channels asked, this build has buffers for %d") has no fix. Its row for `TrueType.cs:22` names a message that no longer exists. The new OTF / CFF, commit-queue and mmutil strings follow the pattern. |
| G-DOCS.05 | OPEN | OPEN | Same as P16.05: the 1.0.0 notes overstate Mode 3 (`RELEASE-NOTES.md:32`). |
| G-SCOPE.01 | OPEN | USER | Every commit from `8d7fe23` to `c407d74` names a T-id or `infra:` except `24d3c1d`, listed in `tools/commit-refs-known.txt`; `ci.cmd` runs `check-commit-refs.ps1 -Strict`. The older commits wait on scope-log row G-SCOPE.01. |
| P4.X1 | OPEN | USER | EMU now exists: `docs/evidence/emu/skyhopper-forest01-slice-30-mgba.png`, `-60-mgba.png`, `skyhopper-start-forest01-vbam.png`; 60 fps in `SkyHopperPlayTests.ForestSliceRunsAt60FpsWithPlayerSlimesCoinsAndHud`. Verify includes `USER`. |
| P5.X1 | OPEN | USER | Each scene through its link with its transition in the mGBA core (`skyhopper-<n>-*-mgba.png`, `SkyHopperPlayTests.AllFiveScenesLoadThroughTheirLinks`) and each scene loading in VBA-M (`skyhopper-start-*-vbam.png`). Verify includes `USER`. |

## Part C: G-DOCS.04

OPEN. `docs/evidence/rev-docs-c407d74.md` lists 60 out-of-date statements: ARCHITECTURE.md 13, BUILD.md 9,
FILE-FORMATS.md 21, DECISIONS.md 6, TOOLCHAIN.md 3, THIRD_PARTY.md 2, and 6 in EDITOR-ARCHITECTURE.md. Examples: the
render order (`ARCHITECTURE.md:100-101`), Logic Graph and map scripts said to compile to bytecode (`:143-144`, `:213`),
the link paragraph that says every kind acts like On touch (`FILE-FORMATS.md:79-82`), the commit-reference exemption
list missing from `BUILD.md:20-22` and `:85-88`, the D-13 message and Community / Release notes URLs
(`DECISIONS.md:94`).

## OPEN list (11 in Parts A and B, plus G-DOCS.04)

Small (a test, a string or a doc line):

1. P10.08: E2E: New Project while a document is dirty asks "Save changes before closing the project?" (Save All, Don't
   Save, Cancel).
2. P10.11: E2E of Cut and Select All on actors, Select All on asset cards, Cut / Copy / Paste / Select All on table
   rows, Cut / Duplicate / Delete / Select All on dialogue lines.
3. P10.16: fake-folder UT with a stub `arm-none-eabi-gcc` that reports a version (devkitARM found, version shown) and
   the image editor found / not found.
4. P16.05, G-DOCS.05: fix `docs/RELEASE-NOTES.md:32` (Mode 3: the display mode only, no bitmap drawing for games) and
   name sprite VRAM in line 41.
5. G-ERR.01: give the five `Aseprite.cs` messages and `audio.cpp:90` a "what to do", and bring
   `error-strings-review.md` up to `c407d74` (new rows for the strings added since; drop the `TrueType.cs:22` row).
6. P1.04: put `bgSize` / `bpp`, `drawTo`, speaker `names` / `color` and the graph `comments` / `collapsed` / `disabled`
   into the file model or the schemas, and validate a file the editor wrote with them.

Medium:

7. P11.62: make the image editor open sprite and tileset PNGs as its hint says (or change the hint), and decide the
   build cache location; either way add scope-log rows for what the user must accept.
8. P12.05: draw the velocity arrow (or drop it) and get the user's acceptance of the game-view difference in a
   scope-log row (Figma is a drawing of another moment).
9. P14.03, P14.07: check Visual Studio 18 (Open externally lands on the caret line; GAE001-GAE006 and "Change to fixed"
   inside the IDE) and record it in `docs/evidence/external-ide.md`; this needs someone allowed to start the IDE.
10. G-DOCS.04: fix the 60 rows of `docs/evidence/rev-docs-c407d74.md`.

## USER list

| Item | What to show the user |
|---|---|
| P10.E1 | (after P1.X1) D-01 and D-10 in `docs/DECISIONS.md`. |
| P10.05 | Scope-log row "C-icon-button-default, C-play-control-step": `docs/evidence/diff/C-icon-button-default.png` and `C-play-control-step.png` (3 px, alpha-only Figma export); accept or fix. |
| P10.X1 | (after P10.08, P10.11, P10.16, P10.05) `docs/evidence/shot-report.md` with the shell, type specimen and component diffs. |
| P11.E1 | (after P10.X1) The phase entry. |
| P11.09 | Scope-log row "T-P22": the empty-project page and its caption; change the caption or add a template control to Figma. |
| P11.61 | Scope-log row "T-S02": the Save data tab with byte / short / int sizes against Figma's int at 1 B / 2 B. |
| P11.X1 | (after all section 4 boxes and the rows above) Every window against its frame. |
| P12.E1 | (after P7.X1, P10.X1) D-16. |
| P12.X1 | (after P12.05) The fixture playing in Play Mode at full speed with live Inspector and Console. |
| P13.E1 | (after P12.X1) D-21. |
| P13.X1 | Every Debugger view on the running fixture. |
| P14.E1 | (after P6.X1, P10.X1) The phase entry. |
| P14.X1 | (after P14.03, P14.07; VS Code must be installed) A script edited in VS Code reloading in the running game. |
| P3.11 | Scope-log row G-PERF.05 (14.5 % mixer CPU vs 6 %), and a listen check of the fixture music in mGBA (loop start, volume, fade on scene change). |
| G-SCOPE.01 | Scope-log row G-SCOPE.01: the 18 older commits and `24d3c1d` without a reference; accept them as they are. |
| P4.X1 | `docs/evidence/emu/skyhopper-forest01-slice-30-mgba.png`, `-60-mgba.png`, `skyhopper-start-forest01-vbam.png` and the 60 fps test. The HUD label shows the Thai "คะแนน" (score) where Figma shows "SCORE 0120" with hearts. |
| P5.X1 | The twelve `skyhopper-<n>-*-mgba.png` pictures (each link: transition and arrival) and the `skyhopper-start-*-vbam.png` pictures (the five scenes and the title). |

## Scope-log rows touching ticked items

These rows are pending but their answer does not change what the ticked item delivers; the user sees them under
G-SCOPE.03: "T-P20 Racing card" (P11.02), "1.4 items 1-9" item 4 (P11.08), "T-D16, P8.08" (P11.38), "T-P07, P9.07"
(P11.39), G-PERF.05 (P11.44), "T-P17 On Touch Tag" (P11.48), G-SCOPE.02 (P10.17, P11.05, P11.56), "T-D24, P7.03,
D-25" (P11.64), "T-X03, T-M04" (P11.65), G-HW.04 (P13.05 shows the measured RAM split).
