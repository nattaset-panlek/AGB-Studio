# Review: Phases 0-9, 15, 16 and the cross-cutting gates (2026-10-31)

Independent read-only review of `docs/ENGINE-CHECKLIST.md`: every item line of "Phase 0" to "Phase 9", "Phase 15",
"Phase 16" and every `G-...` line of section 6, checked against the code on `main` at `a0dbbfb` (`a0dbbfb` only edits
the checklist scope log, so the code is that of `d802a71`). Nothing was built or run for this review; test results are
the CI run on `d802a71` (lint clean; 21 runtime ROMs pass; `AgbStudio.Tests` 161/161; `AgbStudio.Scripting.Tests`
96/96; `AgbStudio.Editor.Tests` 384/385, the run-order failure is fixed only on the unmerged branch `fix-editor2`,
commit `24d3c1d`).

Decisions:

- **TICK**: done, and the evidence its Verify asks for exists now. The evidence line is in
  `docs/evidence/evidence-p0-9-15-16-gates.tsv`.
- **HW**: everything but the hardware check is evidenced; the item allows `n/a until hardware` (section 2.5,
  `docs/TOOLCHAIN.md` › Hardware). Also in the TSV.
- **USER**: only the user can close it (gate, decision, `USER` verify, or a deviation the user must accept).
- **OPEN**: something the item text asks for is missing. Where a scope-log row (section 8) covers it, the reason says so.

Gates: an entry gate is USER when the exit gate it waits for is USER, and OPEN when that exit gate is OPEN. An exit
gate is judged by its own text: USER when its technical conditions are evidenced, OPEN when one is not.

Evidence conventions in the TSV: `test:Class.Method` for C# tests (every name was checked against the test sources);
`test:<name>` for runtime ROM tests (`AGB_TEST(name)` in `runtime/tests/*/main.cpp`, `runtime/tests/save_common.hpp`
and `tests/AgbStudio.Scripting.Tests/RomHarness/scripts_game/main.cpp`; the four `core_save_*` ROMs share the
`save_common.hpp` names and run through `RuntimeRomTests.RomPasses`); `commit:d802a71` everywhere, the main commit at
which CI ran green for the cited suites and at which every cited doc, shot and test exists. `rom:` points at a local
build artifact (git-ignored) that was read for this review. The date is left to the person who ticks.

## Counts

| Group | TICK | HW | USER | OPEN | Total |
|---|---|---|---|---|---|
| Phase 0 | 7 | 0 | 3 | 2 | 12 |
| Phase 1 | 7 | 0 | 2 | 4 | 13 |
| Phase 2 | 10 | 1 | 0 | 4 | 15 |
| Phase 3 | 11 | 0 | 1 | 4 | 16 |
| Phase 4 | 11 | 0 | 2 | 1 | 14 |
| Phase 5 | 8 | 0 | 0 | 4 | 12 |
| Phase 6 | 19 | 0 | 2 | 1 | 22 |
| Phase 7 | 15 | 0 | 1 | 4 | 20 |
| Phase 8 | 9 | 0 | 2 | 2 | 13 |
| Phase 9 | 13 | 0 | 2 | 1 | 16 |
| Phase 15 | 3 | 1 | 3 | 7 | 14 |
| Phase 16 | 2 | 0 | 1 | 6 | 9 |
| Section 6 gates | 30 | 1 | 2 | 9 | 42 |
| **All** | **145** | **3** | **21** | **49** | **218** |

## Every item

### Phase 0

| Item | Decision | Reason |
|---|---|---|
| P0.E1 | USER | User confirms the build start (goal set 2026-10-31, header of `docs/DECISIONS.md`). |
| P0.01 | OPEN | `TOOLCHAIN.md` gives the version from the package database; the Verify asks for the `arm-none-eabi-gcc --version` output, which is not saved. |
| P0.02 | TICK | `p0_hello` uses libtonc's text engine and its map lists `libtonc.a` members; `RuntimeRomTests.RomPasses` passes it. |
| P0.03 | TICK | `build/runtime-tests/p0_hello.elf.map` (built after `d802a71`) has only `libtonc.a` and `libmm.a` members, no `libgba.a`; the link line has no `-lgba`. |
| P0.04 | TICK | `TOOLCHAIN.md` "Each tool runs": banner line and exit code of gbafix, grit, mmutil, gbalzss, padbin, bin2s. |
| P0.05 | TICK | Hello ROM with bars and text in VBA-M and mGBA captures; build.ninja plus gbafix; header bytes checked in `RomHeaderHoldsTitleGameCodeMakerAndVersion`. |
| P0.06 | TICK | D-06 (decided by the user): mGBA libretro core plus `agb romtest` replace mgba-rom-test; `p0_hello` passes headless. |
| P0.07 | TICK | `P0.07-vbam-hello.png` and `P0.05-vbam-hello.png`. |
| P0.08 | TICK | `TOOLCHAIN.md` › Host tools with versions and which ones the stack needs. |
| P0.09 | USER | Verify is `USER`: confirm no flash cart or GBxCart RW (`TOOLCHAIN.md` › Hardware). |
| P0.10 | USER | `docs/evidence/p0.10-untouched-folders.md` has no "before" listing (none was taken); the user must accept the write-time listing in its place. |
| P0.X1 | OPEN | P0.01 is open; P0.09 and P0.10 and the install list need the user. |

### Phase 1

| Item | Decision | Reason |
|---|---|---|
| P1.E1 | OPEN | Waits for P0.X1 (OPEN). |
| P1.01 | USER | `DECISIONS.md` D-01..D-21 have date, question, options, answer; all but D-06 are "decided (default)". |
| P1.02 | USER | 1.4 items 2, 4, 5, 6, 7, 9 are resolved through decisions; 1 (".gs" tab), 3 ("New Project (Empty)") and 8 (step order, ARCHITECTURE.md note) need the user; scope-log row pending. |
| P1.03 | TICK | Layout per D-04 and a root `README.md` with the folder table. |
| P1.04 | OPEN | 15 schemas and round trips exist (`SchemaTests`, `ProjectModelTests.EveryFileFormatRoundTrips`), but the palette format named in the item (`palettes/palettes.json`, `PaletteLibraryFile`) has no schema and no schema-checked round trip. |
| P1.05 | TICK | `FixtureTests.CountsMatchTheFigmaFrames` (5 scenes, 6 links, 30x20, 12 sprites, 12 scripts, 340 keys, Items 24 x 9, 3 fonts, 9 audio). |
| P1.06 | OPEN | `ci.cmd` has no artifact upload, and there is no green run: the CI run on `d802a71` failed one editor test (fix only on unmerged `fix-editor2`). |
| P1.07 | TICK | `LintTests.PlantedViolationsAreReported` (emoji, hardware write, float) and `agb lint` stops `ci.cmd`. |
| P1.08 | TICK | `docs/evidence/figma`, `app`, `diff`, `shot-report.md` with `<T-ID>.png` names. |
| P1.09 | TICK | `THIRD_PARTY.md` lists every G-LEGAL.04 component; `legal-review.md`; `PackagingTests.EveryShippedLibraryHasALicenceRow`. |
| P1.10 | TICK | `ARCHITECTURE.md` maps blueprint 01-16 to folders; `BUILD.md` exists (stale lines are under G-DOCS.04). |
| P1.11 | TICK | `.gitmessage` and `.github/pull_request_template.md` with "Figma ref: T-... / infra:". |
| P1.X1 | OPEN | P1.04 open; CI not green (P1.06); decisions and fixture spec need the user. |

### Phase 2

| Item | Decision | Reason |
|---|---|---|
| P2.E1 | OPEN | Waits for P1.X1 (OPEN). |
| P2.01 | TICK | `iwram_code_runs_from_iwram`; map-file sections checked by `RamBudgetTests` and `DebugChannelAddressesMatchTheMapFile`. |
| P2.02 | TICK | `frame_loop_counts_and_profiles`: 600 frames, 59.72-59.735 Hz, idle (halted) > 200k cycles per frame. |
| P2.03 | TICK | `frame_phases_follow_blueprint_order` checks all 11 phases of two frames. |
| P2.04 | OPEN | `commit_queue_budget_defers` checks the defer but not the log line; `commit::copyNow` / `fillNow` write VRAM at once outside staged loads (`TextBox::remap` / `unmap` on pause and resume, Mode 3 `bitmap::fill`), which breaks "flushed only in VBlank". |
| P2.05 | TICK | `input_edges_axes_repeat_and_actions` (recorded key sequences, run on the target in place of a host UT); action map from Project Settings. |
| P2.06 | TICK | `tasks_run_by_priority_and_step`. |
| P2.07 | TICK | `RuntimeRomTests.PoolAssertsCatchStaleHandlesAndDoubleFrees` (Debug asserts, Release safe); `pool_handles_and_arena`; full pool in the Console. |
| P2.08 | TICK | `RuntimeRomTests.RngMatchesHostReference`: 16 values of each of the 3 streams and 16 bounded values equal the host copy. |
| P2.09 | TICK | `FixedPointMatchesHostReference` (32 pairs, sine, sqrt against exact host values); `fixed_point_cycle_budget` logs and bounds the cycles. |
| P2.10 | HW | All five save types, two copies, CRC, migration, checksum off, None (`save_common.hpp` in 4 ROMs); autosave in `MiniPlatformerBuildsAndPlays`; VBA-M shots per type; hardware n/a. |
| P2.11 | OPEN | Logs go to the AGB debug channel, not the mGBA debug-print interface the item names; D-24 and its scope-log row wait for the user. Strip and asserts are evidenced. |
| P2.12 | TICK | `bios_wrappers`: LZ77 to WRAM and VRAM, Div / Mod, CpuFastSet, Halt. |
| P2.13 | TICK | `RuntimeRomTests.DebugChannelAddressesMatchTheMapFile`. |
| P2.X1 | OPEN | `RamBudgetTests` holds only 7 of the blueprint 10 rows to their defaults; the default split itself waits for the user (scope-log row G-HW.04); P2.04, P2.11 open. |

### Phase 3

| Item | Decision | Reason |
|---|---|---|
| P3.E1 | OPEN | Waits for P2.X1 (OPEN). |
| P3.01 | TICK | `bg_allocation_and_config`, `mode0_four_tile_layers_blend_and_mosaic`; layer table plan `TilemapModelTests.VramPlan_*`. |
| P3.02 | TICK | `VideoModeTests.VideoModesMatchReferenceImages` (modes 0-3 vs references), `video_mode_alpha_blending_and_mosaic`, `P3.02-mode0..3.png`. |
| P3.03 | TICK | `affine_rotate_scale_sweep`, `affine_sweep_pictures` (6 sweep pictures checked). |
| P3.04 | TICK | `map_streams_rows_and_columns` (512 x 256, every visible cell, bytes per frame), `big_camera_jump_fits_in_a_frame`. |
| P3.05 | OPEN | x0.2 and x0.5 are asserted (`parallax_scroll_registers`), x1.0 by the viewport test; x1.2 only runs in `render_stress`, which never checks its scroll value. |
| P3.06 | TICK | 128 sprites, 12 shapes, 33rd matrix refused, line overload, frame streaming; OAM counts in the Debugger test and the stress log. |
| P3.07 | TICK | `animation_timing_loop_once_pingpong` (on target); Sprite Editor timeline E2E. |
| P3.08 | TICK | `fade_tint_flash`, `colour_cycle_directions` (Left, Right, Ping-pong). |
| P3.09 | TICK | 1bpp / 2bpp, shadow, typewriter, blips, continue marker; ROM text equals the golden corpus sheet and the preview. |
| P3.10 | TICK | `scanline_dma_writes_each_line`, `mode7_table_has_perspective`, Racing template test. |
| P3.11 | OPEN | Mixer 14.5% vs Figma 6% (scope-log row G-PERF.05); `MusicMeta` (volume, loop start, fade on scene change) is never read by the build, so loop start does not exist at runtime; no recorded EMU listen check. |
| P3.12 | OPEN | `DmgSoundTests.Preview_SoundsLikeTheEmulator` compares Square 1, Square 2 and Noise with the emulator; the Wave channel is not compared. |
| P3.13 | TICK | `AudioPageTests.ChannelsMixerBudget_AndPreviewSfx` (DSound A, Square 1/2, Wave, Noise); runtime `channel_use_...`. |
| P3.14 | TICK | `transitions_fade_and_slide` (Cut, Fade, Slide). |
| P3.X1 | USER | Show `P3.X1-mgba-stress.png`, `P3.X1-vbam-stress.png` and the profiler line of `four_layers_128_sprites_and_music_at_60_fps`. |

### Phase 4

| Item | Decision | Reason |
|---|---|---|
| P4.E1 | USER | Tick after P3.X1. |
| P4.01 | TICK | `screen_stack_push_pop_resume_replace`, `staged_load_steps_fit_in_a_frame` (7 steps < 1 frame, overrun logged with the step name). |
| P4.02 | TICK | `platformer_scene_end_to_end`, `translated_behaviours_play_like_the_figma_code`. |
| P4.03 | TICK | One scenario per behaviour: platformer, slopes, one-way, chase, sound on event, Top-down and Shoot 'em up template play tests. |
| P4.04 | TICK | One-way, ladder, hazard, water ROM tests; solid and colliders in the platformer test; tile collision UTs. |
| P4.05 | TICK | `camera_dead_zone_shake_and_camera_zone`; follow and clamp in the platformer test. |
| P4.06 | TICK | Empty Actor, Player, Enemy, Pickup, Trigger Zone, Camera Zone and Text Label each in a ROM test. |
| P4.07 | TICK | `prefab_templates_spawn_from_rom`; prefabs as const `ActorDef` in `build/gen/prefabs.cpp`. |
| P4.08 | TICK | `FlowTests.AddingAFlagKeepsTheIdsOfTheOthersAndTheSaveFormat`; flag ids kept in the project file. |
| P4.09 | TICK | Graph "Add to Variable" generates `Game.score += 10`; translator maps game state fields. |
| P4.10 | TICK | `bus_rules_are_independent_of_presentation` (headless vs presented, AI, replay). |
| P4.11 | TICK | `scene_pause_freezes_until_start`, Scene.Pause in a built game, Save.Checkpoint, Go to scene, start scene. |
| P4.12 | USER | Remap works (`options_remap_swaps_buttons_and_sets_volume`, `UiKitTests`); the options screen was added to Figma by the build session and the user must keep it (scope-log row, D-23). |
| P4.X1 | OPEN | `SkyHopperPlayTests.ForestSliceRunsAt60Fps...` passes in the mGBA core, but no EMU picture or clip (mGBA and VBA-M) of the slice exists. |

### Phase 5

| Item | Decision | Reason |
|---|---|---|
| P5.E1 | OPEN | Waits for P4.X1 (OPEN). |
| P5.01 | OPEN | No primary / secondary tilesets; scope-log row P5.01 waits for the user. |
| P5.02 | OPEN | Metatiles are one layer; scope-log row P5.02 waits for the user. |
| P5.03 | TICK | Map file = layout, shared by two scenes with one ROM copy (`BuildMakesPrefabTemplatesSharesMapsAndKeepsFlagIds`). |
| P5.04 | TICK | Scene file round trip; Scene Settings applies every section. |
| P5.05 | TICK | Entities, door portal to spawn point, trigger with "Only if", interactables in ROM tests. |
| P5.06 | TICK | `map_scripts_run_on_load_enter_and_exit`; OnLoad / OnEnter / OnExit in a built game. |
| P5.07 | TICK | `SkyHopperPlayTests.AllFiveScenesLoadThroughTheirLinks` (touch with Only if, door, respawn, transitions). |
| P5.08 | TICK | Animated tile in a ROM; random brush data is edit-time and round-trips. |
| P5.09 | TICK | `AutoTileTests.GrassEdgesResolveFromNeighbours`. |
| P5.10 | TICK | Tile, image, rotate / scale and collision-only layers in ROMs and the layer model. |
| P5.X1 | OPEN | All 5 scenes load in the mGBA core test, but no EMU picture or clip (mGBA and VBA-M) exists. |

### Phase 6

| Item | Decision | Reason |
|---|---|---|
| P6.E1 | OPEN | Waits for P4.X1 (OPEN); D-02 needs the user. |
| P6.01 | USER | Verify includes `USER`: show the generated subset page (Help › Scripting API, `subset.html`) and D-02. |
| P6.02 | TICK | GAE001-GAE006 positive and negative, severities, Treat warnings as errors. |
| P6.03 | TICK | `CodeFixTests` (Change to fixed, Ignore); popup in the Script Editor. |
| P6.04 | TICK | Golden files, no exceptions / RTTI, write-on-change, compiled with runtime flags, runs in the emulator. |
| P6.05 | TICK | `PlantedSyntaxErrorReportsFileLineColumn` (`PlayerController.cs(22,9)`), g++ lines mapped back, C# breakpoints. |
| P6.06 | TICK | `[Range]` fields are sliders (`InspectorTests.BehaviourFields_SliderTextColliderAndUndo`); Exposed fields panel. |
| P6.07 | TICK | `TranslatorTests.ProjectNamespaceSetting`. |
| P6.08 | TICK | `TranslatorTests.TemplatesCompileClean`. |
| P6.09 | TICK | `ScriptingApiMembersTranslate` (every member of the API page), `ApiDocsCoverEveryPublicMember`. |
| P6.10 | TICK | Golden bytecode tests. |
| P6.11 | TICK | `OpcodeTableIsTheSingleSourceForCompilerAndVm` (`script_ops.def`). |
| P6.12 | TICK | Contexts, call / return, locals, waits, `WaitUntil` (`vm_wait_until_checks_its_condition_every_frame`), natives resume. |
| P6.13 | TICK | `ScriptNativeTableFromAttributes`. |
| P6.14 | TICK | `vm_breakpoint_pauses_and_resumes`, `vm_single_step_and_locals_view`. |
| P6.15 | TICK | Dialogue golden C# and bytecode; `cutscenes_and_dialogue_run_in_the_vm` plays forest_intro with both choices. |
| P6.16 | TICK | `FigmaGraphGeneratesTheFigmaCode`, every category and node compiles. |
| P6.17 | TICK | Round trip of graph-generated files (D-11). |
| P6.18 | TICK | Hot reload E2E in the same scene, including an external save. |
| P6.19 | TICK | One unit per table; `DatabaseBuildTests.ChangingOneRowRebuildsOnlyThatTable`. |
| P6.20 | TICK | Typed `Items.Get(ItemId.Potion)` generated and translated. |
| P6.X1 | USER | Show the fixture build (GAE002 on `float coyoteTime`, `IdeTests.GeneratedProjectBuildsWithTheSameGaeDiagnostics`) and the ROM running. |

### Phase 7

| Item | Decision | Reason |
|---|---|---|
| P7.E1 | OPEN | Waits for P2.X1 (OPEN). |
| P7.01 | TICK | Meta schemas match the model; sprite, tileset, palette owner and audio meta edited in the editor. |
| P7.02 | TICK | PNG and Aseprite, 16 colours, shared palettes, split, trim, animations; golden outputs. |
| P7.03 | OPEN | The fix reads "Split into 4 palettes", not the item's "3" (D-25, `FortyEightColoursNeedFourPalettesNotThree`); scope-log row T-D24 / P7.03 waits for the user. |
| P7.04 | TICK | Tiled `.tmx` imported to map and tileset (`AssetImporterTests.Import_ConvertsIntoTheProjectFolders`). |
| P7.05 | TICK | `.pal` / `.gpl` round trip and Palette Manager import / export. |
| P7.06 | TICK | MOD / XM soundbank via mmutil; WAV to 11025 Hz 8-bit mono; MP3 refused with the message. |
| P7.07 | TICK | `.sfx` as DMG register images in the ROM; Bake to `.wav`. |
| P7.08 | TICK | VRAM-safe LZ77; compressed and plain tiles give the same screen in the emulator. |
| P7.09 | TICK | No-change rebuild converts 0, one option change reconverts 1. |
| P7.10 | TICK | Header title, game code, maker, version checked; link and gbafix steps; ARM code in IWRAM. |
| P7.11 | TICK | 16-way configuration matrix, strip / keep, ROM warning, output folder, run after build. |
| P7.12 | OPEN | Report has ROM per asset, breakdown, largest, warnings, step times, history, and numbers match the map, but RAM is per blueprint-10 row and VRAM per scene, not per asset as the item says, and report.json has no schema ("UT schema"). |
| P7.13 | TICK | Build ROM streaming, real build, failed step, Go to first error. |
| P7.14 | TICK | Clean Build; cache size shown and cleared. |
| P7.15 | OPEN | Padding is tested on a stand-in file; no EMU boot of a padded real ROM. |
| P7.16 | TICK | Find Usages, Find Unused Assets, Used by lists. |
| P7.17 | TICK | Reimport in the asset context menu. |
| P7.18 | TICK | `FixtureRebuildsAfterAOneLineScriptChangeInUnderASecond` (best of warm edits < 1 s) in CI. |
| P7.X1 | USER | Show `FixtureBuildTests` (clean build, no-change rebuild does no work, report matches the map). |

### Phase 8

| Item | Decision | Reason |
|---|---|---|
| P8.E1 | USER | Tick after P3.X1 and P7.X1; D-17 needs the user. |
| P8.01 | OPEN | TTF, BDF and image sheets are tested; OTF with CFF outlines is refused (`TrueType.cs:22`) and no test reads an `.otf` file, although the item and T-D15 name OTF. |
| P8.02 | TICK | Fixed 8x8 and VWF 12 px fonts, metrics and per-glyph advance in Font Manager tests. |
| P8.03 | TICK | "Basic Latin · 95", "Thai · 87", "Symbols · 12", Japanese kana font. |
| P8.04 | TICK | Pixel editing overrides the glyph; Missing cells. |
| P8.05 | TICK | Full cluster corpus without collisions; reviewed golden sheet. |
| P8.06 | TICK | Every break on a dictionary word start; known sentences; long line wrapped in a ROM. |
| P8.07 | TICK | 2bpp, shadow, BG text layer; text VRAM in report.json. |
| P8.08 | OPEN | The Figma sample finds 2 missing in the fixture font, not 7 (`FigmaAddCharactersSampleMissesTwoCharactersInTheFixtureFont`); scope-log row T-D16 / P8.08 waits for the user. |
| P8.09 | TICK | Editor viewport, labels, dialogue preview and Localization preview equal the ROM pixel for pixel. |
| P8.10 | TICK | Default font and font per language persist and build. |
| P8.11 | TICK | `ReportNumbersMatchTheLinkerMap` holds the font estimate within 10%. |
| P8.X1 | USER | G-THAI.01-05 TICK, G-THAI.06 n/a; user reviews `docs/evidence/app/P10-thai-*.png` and `tests/AgbStudio.Tests/Golden/thai-corpus.png`. |

### Phase 9

| Item | Decision | Reason |
|---|---|---|
| P9.E1 | USER | Tick after P6.X1 and P7.X1. |
| P9.01 | TICK | Tables, rows, filter, record panel. |
| P9.02 | TICK | Field types and bytes; "rarity" enum is 1 B per row. |
| P9.03 | TICK | New Table from empty / CSV / copy; tables built into the ROM. |
| P9.04 | TICK | CSV mapping and warnings on rows 7 and 15. |
| P9.05 | TICK | Used by: "Shop_01 · slot2", "Chest · Cave_01", "Slime.cs · drop table". |
| P9.06 | TICK | A script cell runs in a built game (touching the gem adds the price). |
| P9.07 | OPEN | Status bar shows 528 B (field sum); the ROM table is 576 B; scope-log row T-P07 / P9.07 waits for the user. |
| P9.08 | TICK | Keys, source language, table per language. |
| P9.09 | TICK | Progress, fallback, Add Language with translate-from and font, mark missing. |
| P9.10 | TICK | Fit check from the runtime layout; built runs have the lines the fit check shows. |
| P9.11 | TICK | Missing only filter, `[key]` in Debug, warn / fail per setting. |
| P9.12 | TICK | Groups, notes and speaker per string. |
| P9.13 | TICK | Export .csv with every key and language. |
| P9.14 | TICK | `Text.Show` and labels change language in the same scene after Options. |
| P9.X1 | USER | Show `FixtureBuildTests.FixtureBuildsInEnglishAndThaiAndJapaneseShowsTheFallback`. |

### Phase 15

| Item | Decision | Reason |
|---|---|---|
| P15.E1 | OPEN | Phase 4, 5 exits are OPEN (Phase 11 is outside this review); D-09 and D-20 need the user. |
| P15.01 | USER | Play test, New Project E2E, mGBA / VBA-M shots exist (HW n/a); its title screen and pause menu use the Figma frames the build session added, which the user must keep (scope-log row, D-23). |
| P15.02 | OPEN | No E2E makes the Top-down Adventure template through New Project (only Platformer and Visual Novel are made; other cards are only mapped). |
| P15.03 | OPEN | No E2E makes the Turn-based RPG template through New Project. |
| P15.04 | OPEN | No E2E makes the Shoot 'em up template through New Project. |
| P15.05 | OPEN | `Create_PuzzleWithLogicGraphOnly` turns Sample level off, so no E2E makes the Puzzle template game. |
| P15.06 | HW | Play test, New Project E2E (`Create_VisualNovel_WritesTheTemplateGame`), mGBA and VBA-M shots; hardware n/a. |
| P15.07 | OPEN | No E2E makes the Beat 'em up template through New Project. |
| P15.08 | OPEN | No E2E makes the Racing (Mode 7) template through New Project; scope-log row "Opponent cars" also waits for the user. |
| P15.09 | TICK | `legal-review.md` G-LEGAL.06 (template content generated in code; unchanged since that review). |
| P15.10 | TICK | Five tutorial pages; steps tick themselves (`TutorialTests`). |
| P15.11 | USER | Samples build and boot (`SampleProjectsBuildAndBootToTheTitle`, emu shots); the user must accept samples made from templates (scope-log row P15.11). |
| P15.12 | TICK | Learn page reaches Scripting API, hardware guide, keyboard shortcuts, release notes and Report a bug (`P11_03_Learn_...`). |
| P15.X1 | USER | Show the eight template play tests and their emu shots. |

### Phase 16

| Item | Decision | Reason |
|---|---|---|
| P16.E1 | OPEN | Earlier exits and section 6 gates are not all ticked. |
| P16.01 | OPEN | Package and first-run devkitPro guidance are tested; no install on a clean Windows user account is recorded. |
| P16.02 | TICK | About fields and credits match `THIRD_PARTY.md`; every shipped DLL has a licence row. |
| P16.03 | TICK | Check for Updates and Report a Bug read `editor.json` as D-13 says. |
| P16.04 | OPEN | Icon, wordmark, Launcher brand block and About icon have SHOT captures (`C-brand-*`, `T-P12`, `T-D11`); the banners (T-R03) are in `docs/brand` but used nowhere (README, release notes, package). |
| P16.05 | OPEN | Launcher "What's new" reads the release notes, but `RELEASE-NOTES.md` "Known limits" is out of date (Mode 3 bitmap, Door / Respawn links, Aseprite and the runtime notice in THIRD_PARTY.txt all work on main). |
| P16.06 | OPEN | No release candidate; section 4 and section 6 are not all ticked; CI on main not green. |
| P16.07 | USER | D-19: no certificate, builds unsigned, no update feed configured; the user decides; the signature check cannot pass. |
| P16.X1 | OPEN | Waits for P16.E1 and P16.01, P16.04-P16.06. |

### Section 6 gates

| Item | Decision | Reason |
|---|---|---|
| G-HW.01 | OPEN | Build fails over BG VRAM and OBJ VRAM (32 KB, 16 KB in bitmap modes), but no ROM test fills BG or OBJ VRAM and checks the runtime refusal and warning ("RT limits test ROM"). |
| G-HW.02 | TICK | 129th sprite and 33rd matrix refused, line overload reported; OAM table in the Debugger. |
| G-HW.03 | TICK | BG and OBJ palette bank limits fail the build; index 0 kept transparent. |
| G-HW.04 | OPEN | EWRAM scratch measures 33.8 KB against the 26 KB default; new defaults wait for the user (scope-log row G-HW.04); `RamBudgetTests` holds only 7 rows. |
| G-HW.05 | TICK | ROM-size warning from Project Settings; over 32 MB fails. |
| G-HW.06 | TICK | Save data larger than the save type fails the build; slot size is the compiled state. |
| G-HW.07 | TICK | All 8 template tests assert no frame overrun, no late VBlank commit and every load step < 280,896 cycles. |
| G-HW.08 | TICK | `AudioChannelTests` and the DMG clash ROM test. |
| G-THAI.01 | TICK | Corpus of every cluster, digits, mai yamok, mixed lines; golden sheet; ROM lines. |
| G-THAI.02 | TICK | Breaks on dictionary word starts; built runs match the fit check. |
| G-THAI.03 | TICK | As P8.09. |
| G-THAI.04 | TICK | Thai typed in text fields, Localization, Database record, Dialogue Editor, New Speaker, Font Manager; ROM dialogue box. |
| G-THAI.05 | TICK | Noto Sans Thai (OFL) and ICU thaidict rows in `THIRD_PARTY.md` (D-17). |
| G-THAI.06 | HW | Item allows `n/a until hardware`; no flash cart (`TOOLCHAIN.md`). |
| G-ERR.01 | OPEN | `error-strings-review.md` found 193 PARTIAL and 53 POOR of 395 strings; only the editor POOR rows were fixed on main (`be27d95`); the tools and runtime rewording is on unmerged branch `fix-messages`. |
| G-ERR.02 | TICK | g++ and script errors carry file(line,col); Console click and Go to first error open the line. |
| G-ERR.03 | TICK | Pool full, VRAM budget, missing string, missing glyph and failed import reach the Console. |
| G-ERR.04 | OPEN | Cancel E2E exists for opening a project and for a build; none for flash or import. |
| G-ERR.05 | TICK | Missing tool names its path and offers Preferences · Paths; first run without devkitPro. |
| G-ERR.06 | TICK | `RobustnessTests.ForcedCrash_SavesTheWorkAndWritesACrashLog`. |
| G-ERR.07 | TICK | Debug assert logs file and line and stops; Release per Keep asserts. |
| G-PERF.01 | TICK | All 8 templates played to the end in mGBA with no overrun (hardware n/a). |
| G-PERF.02 | TICK | One-line change rebuild under 1 s in CI. |
| G-PERF.03 | USER | D-26 targets (60 s / 5 s / 3 s) are default, "agreed in Phase 1" needs the user; measured 14.6 s / 0.2 s / 0.4 s by `FixtureBuildTests`. |
| G-PERF.04 | TICK | `LongOperationTests` (UI responsive during build and play). |
| G-PERF.05 | OPEN | Mixer 14.49% for 8 ch at 21 kHz vs 6%; scope-log row G-PERF.05 waits for the user. |
| G-DOCS.01 | TICK | API docs from source, offline, every public member documented. |
| G-DOCS.02 | TICK | 72 window pages in `docs/user/windows`. |
| G-DOCS.03 | TICK | Tutorials and hardware guide listed and opened from Launcher · Learn. |
| G-DOCS.04 | OPEN | Out of date on main: `ARCHITECTURE.md:106` (Door / Respawn "behave like On touch") and its limits table (OBJ VRAM / palettes "warnings", now build errors), `THIRD_PARTY.md:35` and `DECISIONS.md:45` (runtime notice "not yet" in THIRD_PARTY.txt); no REV record at a phase exit. |
| G-DOCS.05 | OPEN | `RELEASE-NOTES.md` 1.0.0 Known limits do not match main (see P16.05). |
| G-LEGAL.01 | TICK | Lint rule for pokeemerald identifiers, repository clean; references cite file names. |
| G-LEGAL.02 | TICK | `legal-review.md`; files added since (`agb-studio.ico`, `slime.aseprite`, copies of the generated `tune.mod`) are generated or brand files. |
| G-LEGAL.03 | TICK | Product "AGB Studio" (props, window title, exe, package); lint blocks wrong names. |
| G-LEGAL.04 | TICK | Every named component and font in `THIRD_PARTY.md` and About › Licenses. |
| G-LEGAL.05 | TICK | `legal-review.md`: no Butano or other engine code; references recorded. |
| G-LEGAL.06 | TICK | `legal-review.md` G-LEGAL.06. |
| G-LEGAL.07 | TICK | MIT runtime (D-05) in `LICENSE`, `docs/user/licence-for-your-games.md`, release notes (that page's "not yet in THIRD_PARTY.txt" paragraph is out of date). |
| G-EMOJI.01 | TICK | Lint emoji scan of files, `Sources_NoEmoji`, and `tools/check-commit-emoji.ps1` in `ci.cmd` for commit messages. |
| G-SCOPE.01 | OPEN | 21 of the 68 commits since `24a26b6` name neither a T-id nor "infra:", 18 of them after the commit template `7606516` (for example `5a9e710`, `012d781`, `94f8d08`); scope-log row G-SCOPE.01 covers only older history; `check-commit-refs.ps1` only reports. |
| G-SCOPE.02 | OPEN | Windows outside section 4 (scope-inventory-review.md) wait for the user (scope-log row G-SCOPE.02). |
| G-SCOPE.03 | USER | Verify is `USER`: review the 19 pending scope-log rows with the user. |

## OPEN list

Small (a record, a doc line or one assertion):

1. P0.01: save the `arm-none-eabi-gcc --version` output in `TOOLCHAIN.md`.
2. P2.04: assert the "DMA budget ... deferred" log line in `commit_queue_budget_defers`; queue the text-box map writes (`TextBox::remap` / `unmap`) and the Mode 3 fill through the VBlank commit, or limit `copyNow` / `fillNow` to blanked loads.
3. P3.05: assert the x1.2 scroll value (parallax 307) in `parallax_scroll_registers`.
4. P3.12: compare a Wave-channel effect with the emulator in `Preview_SoundsLikeTheEmulator`.
5. P1.04: schema (and schema-checked round trip) for the palette file `palettes/palettes.json`.
6. G-DOCS.04: fix `ARCHITECTURE.md:106` and its limits table, `THIRD_PARTY.md:35`, `DECISIONS.md:45`; record a REV at each phase exit.
7. G-DOCS.05, P16.05: update `RELEASE-NOTES.md` Known limits to main (also `docs/user/licence-for-your-games.md` paragraph).
8. P16.04: use the banners (README and release page / package), or record where they are used.
9. P7.12: give RAM / VRAM per asset in report.json and a report schema, or reword the item to what the Figma Build Report shows (ROM per asset, VRAM per scene).

Medium (a test or a contained change):

10. P1.06, P1.X1: artifact upload in `ci.cmd` (ROMs, report.json, test results) and a green run (merge the run-order fix from `fix-editor2`).
11. P3.11: build `MusicMeta` (volume, loop start, fade on scene change) into the ROM and test loop start at runtime; record an EMU listen check; the 6% figure waits on scope-log row G-PERF.05.
12. P4.X1, P5.X1: EMU pictures or clips (mGBA and VBA-M) of the Forest_01 slice and of the five scenes through their links.
13. P7.15: export a real ROM padded and boot it in the emulator.
14. P8.01: read OpenType fonts (CFF outlines) or decide TTF-only with the user (T-D15 says OTF); add an `.otf` test.
15. P15.02, P15.03, P15.04, P15.05, P15.07, P15.08: an E2E per template that makes it through New Project (Sample level on).
16. G-HW.01: a ROM test that fills BG and OBJ VRAM and checks the refusal and the Console warning.
17. G-ERR.04: cancel E2E for flash and import.
18. G-ERR.01: merge the rewording on `fix-messages` and close the remaining PARTIAL / POOR rows.
19. G-SCOPE.01: make `check-commit-refs.ps1` fail CI, and name T-ids or "infra:" in every new commit.
20. P16.01: install on a clean Windows user account and record it.

Waiting for a user decision (scope-log row exists): P2.11 (D-24), P5.01, P5.02, P7.03 (D-25), P8.08, P9.07, G-HW.04
(also P2.X1), G-PERF.05 (also P3.11), G-SCOPE.02.

Blocked gates: P0.X1, P1.E1, P1.X1, P2.E1, P2.X1, P3.E1, P5.E1, P6.E1, P7.E1, P15.E1, P16.E1, P16.06, P16.X1.

## USER list

| Item | What to show the user |
|---|---|
| P0.E1 | The goal set on 2026-10-31 (header of `docs/DECISIONS.md`); confirm building started. |
| P0.09 | `TOOLCHAIN.md` › Hardware: no flash cart or GBxCart RW, so every HW check is `n/a until hardware`. |
| P0.10 | `docs/evidence/p0.10-untouched-folders.md`; accept the write-time listing in place of the missing "before" listing. |
| P1.01 | `docs/DECISIONS.md` D-01..D-21: confirm the 20 "decided (default)" answers. |
| P1.02 | Checklist 1.4 items 1, 3 and 8 (".gs" tab, "New Project (Empty)", Build ROM step order) and the scope-log row. |
| P3.X1 | `docs/evidence/app/P3.X1-mgba-stress.png`, `P3.X1-vbam-stress.png` and the profiler line of `render_stress`. |
| P4.E1 | Tick after P3.X1. |
| P4.12 | Keep the four in-game frames added to Figma (scope-log row, D-23); remap test `options_remap_swaps_buttons_and_sets_volume`; the checklist line still says "Blocked". |
| P6.01 | The generated C# subset page (Help › Scripting API) and D-02. |
| P6.X1 | The fixture build output with GAE002 on `float coyoteTime` and the ROM running. |
| P7.X1 | `FixtureBuildTests` results: clean build, no-change rebuild does no work, report equals the map. |
| P8.E1 | Tick after P3.X1 and P7.X1; confirm D-17. |
| P8.X1 | The Thai screenshots (`docs/evidence/app/P10-thai-*.png`) and the corpus sheet `tests/AgbStudio.Tests/Golden/thai-corpus.png`. |
| P9.E1 | Tick after P6.X1 and P7.X1. |
| P9.X1 | The fixture built in English and Thai with the Japanese fallback (`FixtureBuildTests`). |
| P15.01 | Keep the in-game title / pause frames (scope-log row, D-23); the Platformer evidence is otherwise complete. |
| P15.11 | Accept samples built from templates (scope-log row P15.11) and the four sample shots in `docs/evidence/emu`. |
| P15.X1 | The eight template play-through tests and `docs/evidence/emu/template_*` shots. |
| P16.07 | D-19: unsigned builds and no update feed for 1.0. |
| G-PERF.03 | D-26 targets (clean build 60 s, open 5 s, Play 3 s) against the measured 14.6 s, 0.2 s, 0.4 s. |
| G-SCOPE.03 | The 19 pending rows of checklist section 8. |
