# Review: final sweep of every unticked checklist line (2026-10-31)

Independent read-only review of `docs/ENGINE-CHECKLIST.md` in the snapshot worktree `D:\devkitPro\AGB-Studio-wt\review2`
(detached at `7487e78`). Scope: every line that starts with `- [ ]` and is not a "matches Figma" box: 160 lines
(262 unticked, 102 of them "matches Figma"). Nothing was built or run. Test results are the green CI run on `1af2af4`
(`D:\devkitPro\AGB-Studio\build\ci\runs\20261031-155702-1af2af4`, `summary.txt`: passed; `.trx`: `AgbStudio.Tests`
172/172, `AgbStudio.Scripting.Tests` 96/96, `AgbStudio.Editor.Tests` 542/542); `7487e78` adds only SHOT images.
Every `test:` name in `evidence-final-sweep.tsv` was matched against the passed results of those `.trx` files, and
every `doc:` path exists at `7487e78`. Runtime ROM tests (`AGB_TEST(name)`) were found in `runtime/tests/*/main.cpp`
and run through `RuntimeRomTests.RomPasses` (all 22 ROMs passed).

Decisions (checklist section 2):

- **TICK**: done, and the evidence its Verify asks for exists; the line is in `evidence-final-sweep.tsv`.
- **USER**: an entry / exit gate, a decision, a 1.3 or 1.4 issue, a `USER` verify, or a pending scope-log row (section 8,
  named by its "Item / window" text) whose answer can change what the item delivers.
- **HW**: everything except the hardware check is evidenced; no hardware (P0.09 is USER).
- **OPEN**: something the item or its Verify asks for is missing (listed after the table).

All 48 scope-log rows are pending. A row that only concerns a "matches Figma" box (a visual difference) does not hold
an implemented box or a phase item.

## Counts

| Group | Lines | TICK | USER | HW | OPEN |
|---|---|---|---|---|---|
| 1.1 scope rule, 1.3 blueprint items, 1.4 design issues | 16 | 0 | 16 | 0 | 0 |
| Section 3 decisions D-01..D-21 | 21 | 0 | 21 | 0 | 0 |
| Section 4 implemented boxes | 23 | 10 | 13 | 0 | 0 |
| Section 4 plain items (4.9 states, T-L04, T-B, T-C, T-X, T-K, T-R) | 16 | 8 | 7 | 0 | 1 |
| Section 5 phases (entry / exit gates and items) | 73 | 4 | 55 | 7 | 7 |
| Section 6 gates | 11 | 3 | 5 | 1 | 2 |
| **Total** | **160** | **25** | **117** | **8** | **10** |

## Every line

### Section 1

| Line | Item | Decision | Reason |
|---|---|---|---|
| 45 | 1.1 Scope change rule accepted by every session | USER | Ticked when the user confirms at the build start. |
| 77 | 1.3 Pool sizes per project | USER | Decision: pools are constants in the runtime (no UI, 1.2 "UI to set pool sizes" out of scope); confirm or design a UI. |
| 78 | 1.3 Genre kit "Menu / UI kit" | USER | Built as D-23 (four in-game frames added to Figma); scope-log row "1.4 item 6, D-20, D-23". |
| 79 | 1.3 Grid movement controller | USER | Not built (Component Picker has Top-down Mover only); confirm it stays out. |
| 80 | 1.3 Event Script VM Daily (RTC) store and save mid-script | USER | Not built (1.2 out of scope); confirm. |
| 81 | 1.3 Seamless map streaming | USER | Not built (`ARCHITECTURE.md:132` says out of scope); confirm. |
| 82 | 1.3 Replay / AI / test input sources | USER | Player and AI are built; the runtime bus also has a `Replay` source (`runtime/include/agb/bus.hpp:15`, `bus::record` / `setReplay`), used only by the `framework_bus_vm` test: confirm it as test infra or remove it. |
| 86 | 1.4 item 1 ".gs" tab | USER | App uses `PlayerController.cs` everywhere; accept as a typo in Editor Map board 1 (row "1.4 items 1-9"). |
| 87 | 1.4 item 2 GAE rule ids | USER | Kept GAE001-GAE006 (D-07). |
| 88 | 1.4 item 3 "File menu › New Project (Empty)" | USER | No such menu item; an empty project is New Project with every include off (T-C52, T-P22). |
| 89 | 1.4 item 4 New Project 6 vs 8 templates | USER | New Project shows all 8 cards (D-09). |
| 90 | 1.4 item 5 `.agbproj` vs `project.json` | USER | Figma names kept (D-03). |
| 91 | 1.4 item 6 in-game screens | USER | Four frames added to Figma by the build session and built (D-23); row "1.4 item 6, D-20, D-23": keep or remove. |
| 92 | 1.4 item 7 Import CSV "Google Sheets" | USER | CSV files only (D-13). |
| 93 | 1.4 item 8 Build ROM step order | USER | Dialog lists steps in Figma order, the pipeline converts assets first (`ARCHITECTURE.md:171`, `BuildStream.cs:41`). |
| 94 | 1.4 item 9 `using GBA;` | USER | Kept (D-08). |

### Section 3 decisions

All 21 are recorded in `docs/DECISIONS.md` with date, question, options and answer; D-01..D-05 and D-07..D-21 are
"decided (default)", D-06 is "decided (user, 2026-10-31)".

| Line | Item | Decision | Reason (what to show) |
|---|---|---|---|
| 177 | D-01 | USER | Avalonia 11 on .NET 10. |
| 178 | D-02 | USER | Roslyn parse + GAE analyzer, C# subset to C++20, `[Cutscene]` to bytecode; `fixed` 24.8 (and D-22 for the `fixed` keyword). |
| 179 | D-03 | USER | UTF-8 JSON with `"format"`, Figma file names (`.agbproj`, `.scene`, `.map` ...). |
| 180 | D-04 | USER | `D:\devkitPro\AGB-Studio`, local git, branch `main`; no `templates/` or `samples/` folders (content is generated). |
| 181 | D-05 | USER | MIT for AGB Studio and the runtime; third-party register in `THIRD_PARTY.md`. |
| 182 | D-06 | USER | Recorded as "decided (user, 2026-10-31)" (mGBA libretro core, `agb romtest`, VBA-M second emulator); tick with `user:2026-10-31` once the user confirms the record. |
| 183 | D-07 | USER | Keep GAE001-GAE006. |
| 184 | D-08 | USER | Keep `using GBA;`. |
| 185 | D-09 | USER | All 8 templates; New Project shows 8 cards. |
| 186 | D-10 | USER | Windows 10/11 x64 only. |
| 187 | D-11 | USER | Graph to C# always; C# to graph only for graph-generated files. |
| 188 | D-12 | USER | SD-card copy supported; GBxCart RW detected and read, writing refused until verified on hardware. |
| 189 | D-13 | USER | `editor.json` URLs; Check for Updates text per the Note; Google Sheets not supported (CSV). |
| 190 | D-14 | USER | MOD / XM only, WAV to 11 kHz mono 8-bit, Maxmod 21 kHz 8 ch (see row G-PERF.05). |
| 191 | D-15 | USER | Rebuild, hand-over snapshot in save RAM, reload; Inspector edits live. |
| 192 | D-16 | USER | Simulate = the real ROM in the embedded mGBA core. |
| 193 | D-17 | USER | ICU `thaidict.txt` word list, Noto Sans Thai. |
| 194 | D-18 | USER | Editor-side C# renderer checked pixel for pixel against mGBA. |
| 195 | D-19 | USER | Portable zip, devkitPro not bundled, unsigned, update feed per D-13. |
| 196 | D-20 | USER | Generated content; in-game screens designed in Figma first. Since `67446e6` the Sky Hopper fixture and the "Sky Hopper demo" sample carry pixel grids of the Figma art (`SkyHopperArt.cs`). |
| 197 | D-21 | USER | Cooperative debugging (line probes, shadow call stack) in Debug builds. |

### Section 4 implemented boxes

| Line | Item | Decision | Reason |
|---|---|---|---|
| 258 | T-P17/implemented | USER | Openers and P11.48 / P11.49 pass; generated C# shape differs from Figma's panel: row "T-P17 On Touch Tag". |
| 261 | T-P18/implemented | USER | Opens from Help and the Script Editor, P11.56 passes; the extra "See also" row: row "G-SCOPE.02". |
| 270 | T-D01/implemented | USER | Openers and P11.08 pass (six template cards now make their games and build); 8 cards vs Figma's 6: row "1.4 items 1-9" (item 4, D-09). |
| 288 | T-D07/implemented | USER | Openers, flow and the writing state pass with fake devices; GBxCart RW writing, which Figma shows, is refused until hardware verification: D-12 (default only); P11.60 is HW. |
| 303 | T-D12/implemented | TICK | File › Exit and the window close button go through `OnClosing` to Unsaved Changes (Cancel stays, Save All saves and closes, Don't Save closes without writing, no second dialog); opening another project and New Project with a dirty file ask too. |
| 312 | T-S02/implemented | USER | Opens and every field persists; field types / sizes differ from the frame: rows "T-P22, T-S02" and "T-S02"; row "T-D08, P4.12, P2.10" may add an options block to the Save data meter. |
| 330 | T-S08/implemented | USER | Fields take effect, devkitARM found with its version, image editor found / not found; the image-editor hint vs Editor Map #24 and the per-project build cache: rows "T-S08" and "T-S08, P11.62 build cache". |
| 365 | T-M01/implemented | TICK | `TitleBarMenuTests` opens File by a click, runs Open Recent (through Opening Project) and Clear Recent, Save All writes two edited files, Exit asks first; every other item acts (`MenuActionTests`). |
| 368 | T-M02/implemented | TICK | Edit opens by a click; Undo / Redo, Cut / Copy / Paste / Duplicate / Delete / Select All on actors (also several), assets, nodes, dialogue lines and table rows; Find in Project and Command Palette. |
| 371 | T-M03/implemented | TICK | Assets opens by a click; Create, Import…, Reimport, Show in Explorer, Find Unused Assets, Optimize Palettes… act. |
| 374 | T-M04/implemented | USER | Scene opens by a click and every item acts; prototype links send the title back to the Scene Editor: row "T-X03, T-M04, T-M05, T-B05, prototype links #48, #55, #73, #75, #76". |
| 377 | T-M05/implemented | USER | Build opens by a click and every item acts; link #55 sends the title to Build ROM: same row. |
| 380 | T-M06/implemented | TICK | Tools opens by a click; every Tools window and Screenshot / GIF. |
| 383 | T-M07/implemented | TICK | Help opens by a click; Getting Started, Scripting API, Keyboard Shortcuts, Report a Bug…, Check for Updates, About. |
| 386 | T-M08/implemented | TICK | Open Recent lists the projects with the open one checked, a click opens one through Opening Project, Clear Recent empties it. |
| 446 | T-P22/implemented | USER | Every empty state from a real empty project (`EmptyProjectTests`); caption's template choice and "File › New Project (Empty)": rows "T-P22" (two rows) and "1.4 items 1-9" (item 3). |
| 473 | T-D16/implemented | USER | Opens and adds glyphs; Figma's sample count (7) vs 2: row "T-D16, P8.08". |
| 482 | T-D19/implemented | USER | Opens, maps columns, warns on rows 7 and 15; Google Sheets not built: row "1.4 items 1-9" (item 7, D-13). |
| 491 | T-D22/implemented | TICK | Opens from the Launcher card and from File › Open Recent (real menu clicks), steps, Cancel. |
| 497 | T-D24/implemented | USER | Opens with fix buttons; "Split into 4 palettes" vs Figma's 3: row "T-D24, P7.03, D-25". |
| 512 | T-O11/implemented | TICK | Ctrl+F opens find mode, Ctrl+H opens replace mode with the caret in Replace, Enter replaces; options and scopes. |
| 546 | T-L02/implemented | USER | Light tokens pass; shares T-D01's 8-card difference: row "1.4 items 1-9" (item 4). |
| 808 | C:Dialog Footer/implemented | TICK | `Chrome.axaml:266-275` lays out the buttons first; the hint wraps to two lines and trims; `DialogFooter_ALongHintNeverPushesTheButtonsOut` checks the buttons keep size and place. |

### Section 4 plain items

| Line | Item | Decision | Reason |
|---|---|---|---|
| 528 | Loading: flashing a cartridge | TICK | Build › Flash to Cartridge…, Flash: during the write the dialog shows "Writing", "208 / 412 KB", the bar at 208 KB and "Keep the cartridge connected until it finishes."; Flash greyed out (fake device paused mid-write; real hardware is P11.60). |
| 534 | Warning: missing glyphs | TICK | Font Manager counts glyphs the project's text needs ("196 glyphs · 2 missing", warning icon) and shows them as Missing cells; Add Characters lists them from pasted text. The number differs from Figma's "3 missing" because of the fixture font (row "T-P09 Font Manager", a SHOT matter). |
| 551 | T-L04 | TICK | Scan now also catches named colours and colours built from literals; the Slider knob uses `lit/white`. REV at `7487e78`: the only named colours outside `Theme/` and `Gallery/` are allowed (SVG parser, accent derivation in `ThemeService.cs`, token fallbacks); `Buttons.axaml:72` is an opacity mask. |
| 565 | T-B02 | TICK | `TitleBar_ClickEachMenuTitle_OpensItsMenuWithItsFirstItems`: Edit opens T-M02. |
| 566 | T-B03 | TICK | Same test: Assets opens T-M03. |
| 568 | T-B05 | USER | Build opens T-M05 in the same test; link #55 says Build ROM: row "T-X03, T-M04, T-M05, T-B05, prototype links ...". |
| 569 | T-B06 | TICK | Same test: Tools opens T-M06. |
| 570 | T-B07 | TICK | Same test: Help opens T-M07. |
| 624 | T-C21 | TICK | Ctrl+F and Ctrl+H open Find & Replace (`CtrlH_OpensFindAndReplaceInReplaceMode_TypeAndEnterReplaces`). |
| 655 | T-C52 | USER | No "File › New Project (Empty)" item: row "1.4 items 1-9" (item 3). |
| 660 | T-X02 | USER | `PrototypeFlowTests.T_X02_FlowEditor_FromTheSceneEditor_EveryLinkItReaches` passes; it includes the links of row "T-X03, T-M04, T-M05, T-B05, prototype links ...". |
| 661 | T-X03 | USER | All 120 link cases pass; five go through another control: same row. |
| 669 | T-K05 | OPEN | REV: the art and thumbnails are read-only exports of the project's Figma file (no third-party files). Missing: SHOT on the windows that show them. |
| 821 | T-R01 | USER | Exe, window / taskbar and About icon done; "installer" depends on D-19 (zip, no installer). |
| 823 | T-R03 | USER | Banners in README, release notes and the package; "installer" depends on D-19. |
| 824 | T-R04 | USER | RT done (`boot_splash_then_title_menu`, `debug_builds_can_skip_the_splash`); the user keeps the splash frame: row "1.4 item 6, D-20, D-23". |

### Section 5 phases

| Line | Item | Decision | Reason |
|---|---|---|---|
| 835 | P0.E1 | USER | Confirm building started (goal of 2026-10-31). |
| 846 | P0.09 | USER | `docs/TOOLCHAIN.md` › Hardware: no flash cart, no GBxCart RW; every HW check is n/a. |
| 847 | P0.10 | USER | `docs/evidence/p0.10-untouched-folders.md` has no "before" listing; the user accepts the write-time listing. |
| 850 | P0.X1 | USER | After P0.09, P0.10; approve the install list. |
| 857 | P1.E1 | USER | After P0.X1. |
| 860 | P1.01 | USER | `docs/DECISIONS.md` D-01..D-21 (and D-22..D-26). |
| 861 | P1.02 | USER | 1.4 items 1-9, row "1.4 items 1-9". |
| 863 | P1.04 | OPEN | Schemas, format field and round trips pass (`SchemaTests.*`, `ProjectModelTests.EveryFileFormatRoundTrips`, `EditorFileSchemaTests.MapFontDialogueAndGraphFilesTheEditorWritesValidate`); the spec text contradicts them (see OPEN 1). |
| 873 | P1.X1 | USER | After P1.04; decisions, fixture spec, CI green (`20261031-155702-1af2af4`). |
| 880 | P2.E1 | USER | After P1.X1. |
| 892 | P2.10 | USER | Save types, copies, CRC, migration, checksum toggle, autosave evidenced (`newest_copy_wins_and_corruption_falls_back`, `older_format_version_migrates`, `checksum_off_loads_the_newest_copy_unchecked`, `save_type_none_stores_nothing`, VBA-M shots); row "T-D08, P4.12, P2.10" may add a global options block; HW n/a. |
| 893 | P2.11 | USER | Logs go to the AGB debug channel, not mGBA debug print: row "D-24". Strip and asserts evidenced (`RuntimeRomTests.ReleaseBuildStripsLogText`). |
| 898 | P2.X1 | USER | Show the `core_*` ROMs passing and the RAM split; row "G-HW.04". |
| 905 | P3.E1 | USER | After P2.X1. |
| 918 | P3.11 | USER | Music meta, loop start, fade and volumes built and tested; row "G-PERF.05" (14.5 % vs 6 %) and a listen check in mGBA. |
| 924 | P3.X1 | USER | Show `docs/evidence/app/P3.X1-mgba-stress.png`, `P3.X1-vbam-stress.png` and the profiler line of `four_layers_128_sprites_and_music_at_60_fps`. |
| 931 | P4.E1 | USER | After P3.X1. |
| 945 | P4.12 | USER | Remap works (`options_remap_swaps_buttons_and_sets_volume`); rows "1.4 item 6, D-20, D-23" (options frame) and "T-D08, P4.12, P2.10" (not saved). |
| 948 | P4.X1 | USER | 60 fps test passes; the EMU pictures in `docs/evidence/emu` (`b63db2e`) show the fixture before `67446e6` changed its art and Forest_01 start: make them again, then show. |
| 955 | P5.E1 | USER | After P4.X1. |
| 958 | P5.01 | USER | Row "P5.01". |
| 959 | P5.02 | USER | Row "P5.02". |
| 970 | P5.X1 | USER | `SkyHopperPlayTests.AllFiveScenesLoadThroughTheirLinks` passes; EMU pictures predate `67446e6`: make them again, then show. |
| 977 | P6.E1 | USER | After P4.X1; D-02. |
| 980 | P6.01 | USER | Verify includes USER: the generated C# subset page (`ApiDocs.cs`) and D-02. |
| 1006 | P6.X1 | USER | Fixture build with GAE002 on `float coyoteTime` and the ROM running. |
| 1013 | P7.E1 | USER | After P2.X1; D-03. |
| 1018 | P7.03 | USER | "Split into 4 palettes" (D-25): row "T-D24, P7.03, D-25". |
| 1036 | P7.X1 | USER | `FixtureBuildTests` (clean build, no-change rebuild, report vs map). |
| 1043 | P8.E1 | USER | After P3.X1, P7.X1; D-17. |
| 1053 | P8.08 | USER | Figma sample finds 2, not 7: row "T-D16, P8.08". |
| 1059 | P8.X1 | USER | Thai screenshots and the corpus sheet. |
| 1066 | P9.E1 | USER | After P6.X1, P7.X1. |
| 1075 | P9.07 | USER | 528 B (field sum) vs 576 B ROM: row "T-P07, P9.07". |
| 1087 | P9.X1 | USER | `FixtureBuildTests.FixtureBuildsInEnglishAndThaiAndJapaneseShowsTheFallback`. |
| 1094 | P10.E1 | USER | After P1.X1; D-01, D-10. |
| 1101 | P10.05 | OPEN | See OPEN 2. |
| 1104 | P10.08 | TICK | Open, Save All, dirty dot, Unsaved Changes on Exit, window close, New Project and opening another project, all E2E. |
| 1107 | P10.11 | TICK | All six commands on actors (one and several), assets, nodes, dialogue lines and table rows, E2E through the Edit shortcuts and menu. |
| 1112 | P10.16 | TICK | Fake devkitPro with a stub `arm-none-eabi-gcc -dumpversion` (found, "devkitARM 14.2" in Paths and the Launcher line), libtonc, maxmod, gbafix, not-found cases, image editor found / not found, script editor and Tiled detection. |
| 1119 | P10.X1 | USER | (blocked: P10.05) `docs/evidence/shot-report.md` with the shell, type specimen and component diffs. |
| 1126 | P11.E1 | USER | After P10.X1 and the runtime phase exits. |
| 1139 | P11.09 | USER | Rows "T-P22" and "1.4 items 1-9" (item 3). |
| 1218 | P11.60 | HW | Flow with fake devices and an SD-card folder; hardware n/a (P0.09). |
| 1221 | P11.61 | USER | Every field persists and changes the build; Save data tab: rows "T-P22, T-S02", "T-S02". |
| 1222 | P11.62 | USER | Every field persists and acts; the two Paths differences now have rows "T-S08" and "T-S08, P11.62 build cache". |
| 1234 | P11.X1 | USER | (blocked: section 4 boxes, many "matches Figma" boxes open) Every window against its frame. |
| 1241 | P12.E1 | USER | After P7.X1, P10.X1; D-06, D-16. |
| 1248 | P12.05 | OPEN | See OPEN 3. |
| 1257 | P12.X1 | USER | (blocked: P12.05) The fixture playing at full speed with live Inspector and Console. |
| 1264 | P13.E1 | USER | After P12.X1; D-21. |
| 1278 | P13.X1 | USER | Every Debugger view on the running fixture. |
| 1285 | P14.E1 | USER | After P6.X1, P10.X1. |
| 1290 | P14.03 | OPEN | See OPEN 4. |
| 1294 | P14.07 | OPEN | See OPEN 5. |
| 1297 | P14.X1 | USER | (blocked: P14.03, P14.07; VS Code is not installed) A script edited in VS Code reloads in the running game. |
| 1304 | P15.E1 | USER | After Phases 4-9 and 11; D-09, D-20. |
| 1307 | P15.01 | USER | Play test, New Project E2E, EMU shots; title and pause use the frames of row "1.4 item 6, D-20, D-23". |
| 1308 | P15.02 | HW | `TemplateTests.TopDownAdventureTalksPushesUnlocksAndEnters` (frame budget), `NewProjectTests.Create_TopDownAdventure_WritesTheTemplateGameThatBuilds`, `template_topdown-mgba/-vbam.png`; hardware n/a. |
| 1309 | P15.03 | HW | `RpgShopsAndWinsABattle`, `Create_TurnBasedRpg_WritesTheTemplateGameThatBuilds`, `template_rpg-*`; hardware n/a. |
| 1310 | P15.04 | HW | `ShootEmUpScrollsFiresAndClearsTheStage`, `Create_ShootEmUp_WritesTheTemplateGameThatBuilds`, `template_shmup-*`; hardware n/a. |
| 1311 | P15.05 | HW | `PuzzleDropsPairsClearsLinesAndEnds`, `Create_Puzzle_WithSampleLevel_WritesTheTemplateGameThatBuilds`, `template_puzzle-*`; hardware n/a. |
| 1312 | P15.06 | HW | `VisualNovelTellsTheStoryAndRemembersTheChoice`, `Create_VisualNovel_WritesTheTemplateGame`, `template_visualnovel-*`; hardware n/a. |
| 1313 | P15.07 | HW | `BeatEmUpFightsThroughTheWavesWithCombos`, `Create_BeatEmUp_WritesTheTemplateGameThatBuilds`, `template_brawler-*`; hardware n/a. (Its card in New Project is part of 1.4 item 4.) |
| 1314 | P15.08 | USER | Evidence as above (`RacingDrivesTheMode7CircuitAndCountsLaps`, `Create_RacingMode7_...`, `template_racing-*`); "Opponent cars": row "T-P20 Racing card". |
| 1317 | P15.11 | USER | All four build and boot (`SampleProjectsBuildAndBootToTheTitle`); row "P15.11". `sample_Sky_Hopper_demo-*.png` predates `67446e6` (the demo is the fixture): make it again. |
| 1321 | P15.X1 | USER | The eight template play tests and `template_*` shots. |
| 1328 | P16.E1 | USER | After every exit and gate. |
| 1331 | P16.01 | OPEN | See OPEN 6. |
| 1335 | P16.05 | TICK | 1.0.0 notes, shown in What's new and in the package; the Mode 3 statement is now a known limit (`2ce9f4e`) and matches `game.cpp`. Line "The build fails ... background VRAM or palettes" leaves out sprite VRAM, save data and the 32 MB ROM limit (true, not complete; optional). |
| 1336 | P16.06 | OPEN | See OPEN 7. |
| 1337 | P16.07 | USER | D-19: unsigned, no update feed configured. |
| 1340 | P16.X1 | USER | After P16.E1. |

### Section 6 gates

| Line | Item | Decision | Reason |
|---|---|---|---|
| 1353 | G-HW.04 | USER | EWRAM scratch 33.8 KB vs 26 KB default: row "G-HW.04"; `RamBudgetTests.*` pass. |
| 1366 | G-THAI.06 | HW | n/a until hardware. |
| 1370 | G-ERR.01 | TICK | `error-strings-review.md` 407 / 407 OK, up to date: the Aseprite and mixing-channel texts reworded (`2f56de1`); no other user-facing error text was added up to `7487e78` (diff `2f56de1..7487e78` read). The Import CSV row warning was shortened to Figma T-D19's words in `b1a5d69` (no "not {expected}", no "Fix the CSV"); the review row records it, and the dialog shows the field type and "Choose another…" next to it, so it is accepted here as Figma's own text. |
| 1373 | G-ERR.04 | TICK | Cancel E2E for open project, build, flash (device and SD card) and a 41-file import; each leaves a consistent state and the operation works again. |
| 1382 | G-PERF.03 | USER | D-26 targets (default) met by `FixtureBuildTests.FixtureBuildsCleanWithinTheTarget`, `ProjectOpensWithinTheTarget`, `PlayStartsWithinTheTarget`; "agreed in Phase 1" needs the user. |
| 1384 | G-PERF.05 | USER | Row "G-PERF.05". |
| 1391 | G-DOCS.04 | OPEN | See OPEN 8 and the judgement below. |
| 1392 | G-DOCS.05 | TICK | As P16.05. |
| 1410 | G-SCOPE.01 | USER | 92 non-merge commits `8d7fe23..7487e78` re-checked: all name a T-id or `infra:` except `24d3c1d` (in `tools/commit-refs-known.txt`); `ci.cmd` runs `-Strict`. The 18 older commits and `24d3c1d`: row "G-SCOPE.01". |
| 1411 | G-SCOPE.02 | OPEN | See OPEN 9. |
| 1412 | G-SCOPE.03 | USER | Review the 48 pending scope-log rows with the user. |

## G-DOCS.04: does the docs REV count as the REV at this phase exit?

`docs/evidence/rev-docs-c407d74.md` read every statement of the six docs (plus `EDITOR-ARCHITECTURE.md`) at `c407d74`,
found 60 out-of-date statements, and its Fixes section records a commit per row. Ten rows checked against the code at
`7487e78`, all match:

| REV row | Doc now | Code |
|---|---|---|
| ARCHITECTURE 100-101 render order | `ARCHITECTURE.md:102-103` | `runtime/src/framework/game.cpp:317-334` |
| ARCHITECTURE 130-131 Door | `:133` | `game.cpp:366` (`Input::Pressed(Key::Up)`) |
| ARCHITECTURE 143-144 Logic Graph not bytecode | `:146-147` | `GraphCodeGen` C# Behaviour |
| ARCHITECTURE 213 map scripts | `:222-224`; `runtime/include/agb/script.hpp:1-2` fixed in `0a2ca4e` | `SceneDef::onLoad` |
| BUILD 20-22 commit-refs-known | `BUILD.md:20-23` | `tools/check-commit-refs.ps1:30-41` |
| BUILD 39 `agb build` folder optional | `BUILD.md:43` | `tools/agb/Program.cs:46` |
| FILE-FORMATS 79-82 link kinds | `FILE-FORMATS.md:80-82` | `game.cpp:365-370` |
| DECISIONS 94 D-13 update text | `DECISIONS.md:96` | `EditorLinks.cs:70` |
| THIRD_PARTY 15 SkiaSharp | `THIRD_PARTY.md:15` | no SkiaSharp in `AgbStudio.Build.csproj` / `AgbStudio.Core.csproj` |
| TOOLCHAIN 49 mmutil | `TOOLCHAIN.md:49-51` | `GameBuilder.cs:476-489` |

Judgement: yes, this REV with its fixes is a full REV and would count for this phase exit, but changes merged after it
made three statements wrong again (OPEN 8). The editor-only-field rows of FILE-FORMATS (REV rows 11, 90-93, 129-131,
141-142, 144) were fixed on `fix-docs2` by saying the fields are not in the models or schemas, while `bfceb55`
(`fix-tools4`, merged later) put them into both. After OPEN 8, G-DOCS.04 can be ticked with
`doc:docs/evidence/rev-docs-c407d74.md, doc:docs/evidence/review-final-sweep.md`.

## OPEN list (10)

1. **P1.04**: `docs/FILE-FORMATS.md:16-20` says the schemas do not hold the editor-only fields (`bgSize` / `bpp`,
   `drawTo`, speaker `names` / `color`, `comments` / `collapsed` / `disabled`) and that a file with one of them fails its
   schema; since `bfceb55` they are in `MapFile` (`SceneModel.cs:121-123`), `FontFile`, `DialogueSpeaker` and in
   `map.schema.json:67-73`, `font.schema.json:84`, `dlg.schema.json:44-53`, `graph.schema.json:54-61`, and
   `EditorFileSchemaTests` validates files the editor writes. `:110-112` says `bgSize` and `bpp` "are not in `MapFile`
   or `map.schema.json`" (and repeats `:107-108` with a different value list). Reword both paragraphs.
2. **P10.05**: at `7487e78` the components fixed after the first component SHOT review (Button F3 `7fedeb3`, Field F5
   `abd1988`, Template Card F6 and Scene Card F8 `e9e3cd7`, Glyph Cell F7 `d195abf`) have no non-author re-review, so
   their "matches Figma" boxes are open; the other open component boxes wait on rows "C-icon-button-default,
   C-play-control-step; also (SHOT review F9) ..." and "C: Slider, Field (Slider)". Note: while this sweep ran, another
   session wrote a second component review into this worktree (`review-shot-components-2.md`,
   `evidence-shot-components-2.tsv`, untracked, not part of `7487e78`). It matches Template Card and Glyph Cell, puts
   Button, Icon Button, Field and Slider under those rows, and finds a new difference on Scene Card (F10: 1 px dark
   seams in the preview art). Once its boxes are applied, what P10.05 still misses is Scene Card F10 (fix or a
   scope-log row); after that P10.05 is USER on the rows.
3. **P12.05**: the SHOT `docs/evidence/diff/T-P02-game-view.png` (4.01 %, offset 0, velocity arrow now drawn) compares a
   different state: the capture has Tile collision on while the Figma frame has it off (`figma/T-P02.png` toolbar), and
   no slime is on screen, so the enemy hitbox (`status/danger`) is not compared. The app also outlines every actor with a
   collider in `status/warning` (coins, the ? block, the goal flag and the three HUD hearts, `GameView.cs:84-103`), which
   Figma's game view does not. Capture with Hitboxes only and the slime in view, and either outline only what Figma
   outlines or add a scope-log row for the user to accept the extra outlines.
4. **P14.03**: nothing checked inside an IDE (`docs/evidence/external-ide.md`, unchanged since `3f0ca7e`): Visual Studio
   18 is installed and Open externally / "Open .cs with" landing on the caret line there is not verified (only the
   command lines, with fake executables). Needs a person (or a session allowed) to start Visual Studio; VS Code and
   Rider are n/a until installed.
5. **P14.07**: same record: GAE001-GAE006 with their severities and the "Change to fixed" light bulb inside Visual
   Studio 18 are not verified (only `dotnet build` of the generated project and in-process code fixes).
6. **P16.01**: no install of the package on a clean Windows user account is recorded (first run, devkitPro guidance,
   removing the app keeps projects).
7. **P16.06**: no release candidate: section 4 "matches Figma" boxes and several gates are open; then a CI report of the
   candidate (HW n/a).
8. **G-DOCS.04**: fix `FILE-FORMATS.md:16-20` and `:110-112` (as OPEN 1), and name `SkyHopperArt.cs` in
   `ARCHITECTURE.md:22-25` and the D-20 note `DECISIONS.md:140` (since `67446e6` the Sky Hopper fixture and the "Sky
   Hopper demo" sample use pixel grids of the Figma art, not `PixelArt`). Minor, not wrong: `EDITOR-ARCHITECTURE.md:27`
   describes the colour scan as hex-only; since `4c418cb` it also finds named colours.
9. **G-SCOPE.02**: the inventory REV (`docs/evidence/scope-inventory-review.md`) is from `24a26b6`; repeat it at
   `7487e78`. Found since then and not in row "G-SCOPE.02": the Script Editor command "Toggle breakpoint" (F9) and gutter
   breakpoints (`ac3f15a`; listed in Keyboard Shortcuts and the Command Palette), which no Figma menu, caption or
   T-D10 frame has. Then the row goes to the user.
10. **T-K05**: SHOT on the windows that show the art (T-P01, T-P08, T-P12, T-P20, T-P21, T-D01, T-O04): their "matches
   Figma" boxes are open (windows SHOT review verdict FIX; fixes in `f1e90cc` / `1af2af4` not re-reviewed). The REV part
   holds; the user may confirm the Figma art is the project's own design.

## USER list

### Gates (what to show)

- P0.E1: the goal of 2026-10-31 (header of `docs/DECISIONS.md`).
- P0.X1: after P0.09 and P0.10; the install list (`docs/TOOLCHAIN.md`).
- P1.E1 (after P0.X1), P1.X1 (after P1.04; decisions, fixture spec, CI run `20261031-155702-1af2af4`).
- P2.E1 (after P1.X1), P2.X1 (`core_*` ROMs, RAM split; row "G-HW.04").
- P3.E1, P3.X1 (`P3.X1-mgba-stress.png`, `P3.X1-vbam-stress.png`, profiler line).
- P4.E1, P4.X1 (new EMU pictures of the current fixture, 60 fps test).
- P5.E1, P5.X1 (new EMU pictures of every scene through its link).
- P6.E1 (D-02), P6.X1 (fixture build with GAE002, ROM running).
- P7.E1 (D-03), P7.X1 (`FixtureBuildTests`).
- P8.E1 (D-17), P8.X1 (Thai screenshots, corpus sheet).
- P9.E1, P9.X1 (fixture in English and Thai, Japanese fallback).
- P10.E1 (D-01, D-10), P10.X1 (after P10.05: shell, type and component SHOT).
- P11.E1, P11.X1 (every window against its frame, after the open "matches Figma" boxes).
- P12.E1 (D-06, D-16), P12.X1 (after P12.05: the fixture in Play Mode with live Inspector and Console).
- P13.E1 (D-21), P13.X1 (every Debugger view on the running fixture).
- P14.E1, P14.X1 (after P14.03 / P14.07; VS Code must be installed).
- P15.E1 (D-09, D-20), P15.X1 (eight template play tests and `template_*` shots).
- P16.E1, P16.X1.
- G-SCOPE.03: the 48 pending scope-log rows.

### Decisions

- D-01..D-21 (section 3 table): confirm each "decided (default)" answer in `docs/DECISIONS.md`; D-06 is already
  "decided (user, 2026-10-31)" and only needs the tick. P1.01 also shows D-22..D-26 (D-24 says it needs the user).
- 1.3 blueprint items (6): pool sizes as runtime constants; Menu / UI kit as D-23; no grid mover; no Daily / mid-script
  save; no seamless streaming; Player and AI sources, plus the test-only `Replay` source in `bus.hpp` (keep as infra or
  remove).
- 1.1 scope change rule (line 45): confirm.
- Decision-held items: T-D07 (D-12, GBxCart RW writing), T-R01 and T-R03 (D-19, no installer), P16.07 (D-19, unsigned,
  no feed), G-PERF.03 (D-26 targets), P6.01 (D-02 subset page).

### 1.4 design issues

Items 1-9 (and P1.02), row "1.4 items 1-9": 1 accept `.cs` (board 1 typo); 2 GAE kept (D-07); 3 no "New Project
(Empty)" menu item (T-C52, T-P22, P11.09); 4 New Project shows 8 cards (T-D01, T-L02, D-09); 5 `.agbproj` and Figma
extensions (D-03); 6 in-game frames added to Figma (row "1.4 item 6, D-20, D-23"; T-R04, P4.12, P15.01); 7 CSV only
(T-D19, D-13); 8 Build ROM lists steps in Figma order, the pipeline converts assets first; 9 `using GBA;` kept (D-08).

### Scope-log rows that hold an item

| Row | Holds |
|---|---|
| "1.4 items 1-9" | 1.4 items 1-9, P1.02, T-D01, T-L02, T-D19, T-C52, T-P22, P11.09 |
| "1.4 item 6, D-20, D-23" | 1.3 Menu / UI kit, T-R04, P4.12, P15.01 |
| "T-P22, T-S02" and "T-S02" | T-S02, P11.61 (and T-P22) |
| "T-P22" | T-P22, P11.09 |
| "T-S08" | T-S08, P11.62 |
| "T-S08, P11.62 build cache" | T-S08, P11.62 |
| "T-X03, T-M04, T-M05, T-B05, prototype links #48, #55, #73, #75, #76" | T-M04, T-M05, T-B05, T-X02, T-X03 |
| "T-P17 On Touch Tag" | T-P17 |
| "G-SCOPE.02" | T-P18 (and G-SCOPE.02 after OPEN 9) |
| "T-D16, P8.08" | T-D16, P8.08 |
| "T-D24, P7.03, D-25" | T-D24, P7.03 |
| "T-P07, P9.07" | P9.07 |
| "P5.01" / "P5.02" | P5.01 / P5.02 |
| "D-24" | P2.11 |
| "T-D08, P4.12, P2.10" | P2.10, P4.12 (and the T-S02 meter) |
| "G-HW.04" | G-HW.04, P2.X1 |
| "G-PERF.05" | G-PERF.05, P3.11 |
| "G-SCOPE.01" | G-SCOPE.01 |
| "P15.11" | P15.11 |
| "T-P20 Racing card" | P15.08 |
| "C-icon-button-default, C-play-control-step; also (SHOT review F9) ...", "C: Slider, Field (Slider)" | component "matches Figma" boxes, then P10.05 |

The other 24 rows (menu icon fill, unsampled text styles, black icons in frames, Colour 4, New Scene name, clipped font
name, Console error count, version 0.1, fixture rows, page-fix rows) concern "matches Figma" boxes only.

### Other USER items

- P0.09 (no hardware recorded in `TOOLCHAIN.md`), P0.10 (accept the write-time listing), P1.01, P3.11 (also a listen
  check), P6.01, P6.X1 and the other `USER` verifies listed under Gates.
- Before showing P4.X1, P5.X1 and P15.11: make the Sky Hopper EMU pictures again (`docs/evidence/emu/skyhopper-*`,
  `sample_Sky_Hopper_demo-*` date from before `67446e6`).

### HW

P11.60, P15.02, P15.03, P15.04, P15.05, P15.06, P15.07, G-THAI.06 (8): n/a until hardware (P0.09).
