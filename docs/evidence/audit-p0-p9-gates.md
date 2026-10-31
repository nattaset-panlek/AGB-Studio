# Audit: Phase 0-9 and cross-cutting gates against the code (2026-10-31)

Scope: every entry gate, item and exit gate of "Phase 0" to "Phase 9" and every item of "6. Cross-cutting gates" in
`docs/ENGINE-CHECKLIST.md`, checked read-only against the repository.

Snapshot: branch `main` at `aa0a44d` plus an uncommitted working tree (DMG sound-effect work in progress:
`tools/AgbStudio.Build/Audio/DmgRegisters.cs`, `runtime/src/audio/audio.cpp`, `runtime/src/framework/game.cpp`,
`tools/AgbStudio.Build/Pipeline/GameBuilder.cs`, `docs/evidence/editor-gaps.md`). Branch `p11-integrate` (`d125b57`)
holds the remaining Phase 11 windows (T-P04, T-P11, T-P13, T-P15, T-P17, T-P18, T-P25, T-D04-T-D07, T-D14, T-D23,
T-D24, T-O08, T-O09, T-O11) and is **not merged into main**; where it closes a gap the row says so.

Status: **DONE** = feature exists and the evidence its Verify code asks for exists. **GAP** = something is missing or
only partly done. **USER** = only the user can confirm. **HW** = needs real hardware. Runtime test names are
`AGB_TEST` names in `runtime/tests/<rom>/main.cpp` (run by `RuntimeRomTests.RomPasses`); C# tests are `Class.Method`.
Note: the checklist itself is still entirely unticked and still says "ยังไม่เริ่ม build"; no evidence has been written
into it yet.

## Phase 0 · Environment and toolchain check

| Item | Status | Evidence or gap |
|---|---|---|
| P0.E1 | USER | `docs/DECISIONS.md` line 3 records the goal set on 2026-10-31. Show it and record `user:2026-10-31`. |
| P0.01 | DONE | `docs/TOOLCHAIN.md` devkitARM row "arm-none-eabi-gcc 16.1.0" (version recorded; not the raw command output). |
| P0.02 | DONE | `build/runtime-tests/p0_hello.elf.map` lists `libtonc.a` members; `tools/AgbStudio.Build/NativeBuild.cs:97` links `-ltonc`; `RuntimeRomTests.RomPasses(p0_hello)`. The map is a build artifact (`build/` is git-ignored). |
| P0.03 | DONE | 0 references to `libgba.a` in all 19 maps under `build/runtime-tests/` and `build/p0_hello/p0_hello.map`; `libmm.a` and `libtonc.a` linked (`NativeBuild.cs:97`); TOOLCHAIN.md "libgba / libfat present, not linked". |
| P0.04 | GAP | TOOLCHAIN.md gives paths for gbafix and mmutil and only "present" for grit, gbalzss, padbin, bin2s. No versions and no record that each tool runs. |
| P0.05 | GAP | Builds through `build.ninja` + gbafix (`NativeBuild.cs:64-71`), passes headless, VBA-M shot shows colour bars (`docs/evidence/app/P0.07-vbam-hello.png`, `82dab06`). The ROM draws no text: `runtime/tests/p0_hello/main.cpp` only logs "hello from AGB Studio runtime" to the debug channel. |
| P0.06 | DONE | D-06 (decided by user 2026-10-31): mGBA libretro core + `agb romtest` replace mgba-rom-test; `RuntimeRomTests.RomPasses(p0_hello)` passes headless (testPass at frame 30); core sha256 in TOOLCHAIN.md. |
| P0.07 | DONE | `docs/evidence/app/P0.07-vbam-hello.png`, commit `82dab06` ("hello ROM in VBA-M 2.2.0"). |
| P0.08 | DONE | TOOLCHAIN.md "Host tools" with versions; CMake, Node, Python marked "not needed by the chosen stack". (BUILD.md still says Ninja is required; see G-DOCS.04.) |
| P0.09 | USER | TOOLCHAIN.md "Hardware": no flash cart or GBxCart RW; every HW check is n/a until hardware. User confirms. |
| P0.10 | GAP | No before/after listing recorded. Today's mtimes (`GBAStudio.spec` and `build\` 2026-04-23, `devkitARM`, `libtonc`, `tools` 2025-05-01, `libgba` 2025-10-08) predate the build, but the REV record does not exist. |
| P0.X1 | GAP | Blocked by P0.04, P0.05, P0.10; then USER approves the install list (mGBA core per D-06). |

## Phase 1 · Open decisions and foundations

| Item | Status | Evidence or gap |
|---|---|---|
| P1.E1 | USER | Tick after P0.X1. |
| P1.01 | GAP | `docs/DECISIONS.md` has D-01..D-23, but most entries hold only an Answer (question/options only on D-01, D-22, D-23; a date only in the header and D-06), and all except D-06 are "decided (default)" awaiting user confirmation. |
| P1.02 | GAP | Resolved: 2 (D-07), 4 (D-09), 5 (D-03), 6 (D-23), 7 (D-13), 9 (D-08). No recorded resolution for 1 (".gs" tab), 3 ("New Project (Empty)"), 8 (Build ROM step order); Scope log row is still "pending". |
| P1.03 | GAP | Layout matches D-04 (`runtime/`, `tools/`, `editor/`, `tests/`, `templates/`, `samples/`, `third_party/`, `docs/`), but there is no README at the repo root. |
| P1.04 | GAP | `docs/FILE-FORMATS.md`, `"format"` field, `ProjectModelTests.EveryFileFormatRoundTrips` (project, scene, map, sprite and tileset meta, sfx, font, db, strings), `NewerFormatIsRefusedWithAClearMessage`, `GraphicsTests.PaletteFilesRoundTrip`, `DialogueTests.FileRoundTripsAndListsTexts`, `GraphTests.RoundTripKeepsTheGraph`. Missing: JSON schema files (none exist), a layout format (P5.03), round trips of MusicMeta, SoundMeta, StringMeta and `.prefab`. |
| P1.05 | DONE | `FixtureTests.CountsMatchTheFigmaFrames` (5 scenes, 6 links, Forest_01 30x20, 12 sprites, 12 scripts, 3 fonts, 9 audio, 340 keys en / 326 th / 0 ja, Items 24 x 9), `FixtureTests.GeneratedMusicIsReadByMaxmod`; `tools/AgbStudio.Build/Content/SkyHopper.cs`. |
| P1.06 | GAP | `ci.cmd` builds the solution (editor included), runs `agb lint` and the three suites (runtime ROMs in the embedded core). No artifact upload step and no recorded green run. |
| P1.07 | DONE | `tools/AgbStudio.Build/Lint.cs` (no-emoji, hardware-through-commit, no-float); `LintTests.PlantedViolationsAreReported`, `LintTests.RepositoryIsClean`; `agb lint` exits 1 on issues (`tools/agb/Program.cs:151-157`) and `ci.cmd` stops on it. |
| P1.08 | DONE | `docs/evidence/figma/` (321 files), `docs/evidence/app/` (299), `docs/evidence/diff/`, `docs/evidence/shot-report.md`; names `<T-ID>.png` and `C-*.png`. |
| P1.09 | DONE | `docs/THIRD_PARTY.md` (D-05); reviewed against the G-LEGAL.04 list: every component is present. |
| P1.10 | GAP | `docs/ARCHITECTURE.md` and `docs/BUILD.md` exist; ARCHITECTURE maps blueprint 03, 04, 11, 13 only (05-10, 12, 14 are not mapped to folders). |
| P1.11 | GAP | No PR or commit template (no `.gitmessage`, no `.github/pull_request_template.md`) with a "Figma ref: T-... or infra: ..." field. |
| P1.X1 | GAP | Blocked by P1.01, P1.06; then USER (decisions, fixture spec, CI green). |

## Phase 2 · Runtime: Platform + Core services

| Item | Status | Evidence or gap |
|---|---|---|
| P2.E1 | USER | Tick after P1.X1. |
| P2.01 | DONE | `render_effects` `iwram_code_runs_from_iwram` (ARM and Thumb code in 0x03000000-0x03008000); `AGB_IWRAM_CODE` / `AGB_EWRAM_BSS`; `-Wl,-Map` (`NativeBuild.cs:70`). |
| P2.02 | GAP | `core_basics` `frame_loop_counts_and_profiles` counts 10 frames of 270k-290k cycles with Idle > 200k (CPU halted). The Verify asks for 600 frames. |
| P2.03 | GAP | The order exists in `SceneScreen::update` (`runtime/src/framework/game.cpp:204`), but no RT records a trace log and checks the order. |
| P2.04 | DONE | `core_basics` `commit_queue_budget_defers` (over budget is deferred and flushed next VBlank); `LintTests.RepositoryIsClean`. Notes: the lint allow-list exempts core/system, save, bios, assert, input, render/effects, audio (sound, timer, DMA registers); staged loads use `commit::copyNow` while faded; the overflow log line is not asserted. |
| P2.05 | DONE | `core_basics` `input_edges_axes_repeat_and_actions` (injected key sequences: pressed, held, released, repeat, axes, AnyKey, action map, remap); `PlayModeTests.KeyboardMapFollowsProjectSettings`; `ProjectSettingsTests.Build_ReadsTheSettings_RomHeaderSaveTypeStartSceneSplashRemap`. RT stands in for the host UT. |
| P2.06 | DONE | `core_basics` `tasks_run_by_priority_and_step` (priority order, state steps, waits, full pool returns kNone). |
| P2.07 | GAP | Arenas and generation-checked pools exist (`runtime/include/agb/memory.hpp:79,87` assert on stale handle and double free); `core_basics` `pool_handles_and_arena`; `LimitTests.AFullActorPoolIsReportedInTheConsole`. No test triggers the stale-handle or double-free assert in Debug. |
| P2.08 | GAP | `core_basics` `rng_is_deterministic_per_stream`; `RuntimeRomTests.RngMatchesHostReference` compares only the first value with the host copy, not a sequence. |
| P2.09 | GAP | `core_basics` `fixed_point_math` checks values (not a reference table); no BUD measurement of cycles per multiply / divide. |
| P2.10 | GAP | RT: `core_save_sram`, `core_save_flash64`, `core_save_flash128`, `core_save_eeprom` (`newest_copy_wins_and_corruption_falls_back`, `older_format_version_migrates`, `slots_do_not_alias`, `capacity_and_crc`); autosave: `PipelineTests.MiniPlatformerBuildsAndPlays`. Missing: VBA-M run per save type (EMU is mGBA only), checksum-off case; HW n/a until hardware. |
| P2.11 | GAP | `RuntimeRomTests.ReleaseBuildStripsLogText`; logs captured headless by `RomTestRunner`; `PipelineTests.FailedAssertsLogFileAndLineAndStopTheGame` (Keep asserts). Logs go to the AGB debug channel (`runtime/include/agb/debug.hpp`), not mGBA's debug-print interface as the item says; the deviation is not recorded. |
| P2.12 | GAP | Wrappers exist (`runtime/include/agb/bios.hpp`). Only `lz77ToVram` is exercised (indirectly, compressed tilesets in `ViewportRomTests`). No RT for `lz77ToWram`, `div` / `mod`, `halt`, `fastCopy`. |
| P2.13 | GAP | The channel exports actor table, profiler, debugger and GameInfo (`debug.hpp`) and tests read it; no RT compares exported addresses with the `.map` file. |
| P2.X1 | GAP | `core_*` ROMs pass; no BUD check against the blueprint 10 IWRAM / EWRAM split (`PipelineTests.ReportAttributesRomToAssets` checks hardware caps only); blocked by Phase 2 gaps; then USER sees the ROM boot. |

## Phase 3 · Runtime: Rendering and media

| Item | Status | Evidence or gap |
|---|---|---|
| P3.E1 | USER | Tick after P2.X1. |
| P3.01 | GAP | `render_map` `bg_allocation_and_config`; editor plan `TilemapModelTests.VramPlan_*`. No RT with 4 tile layers in Mode 0 at once. |
| P3.02 | GAP | `render_map` sets DISPCNT modes 0-3; `render_effects` `blend_mosaic_windows_registers`. No per-mode screenshot vs reference image; Mode 3 has no bitmap path (`render/bg.cpp:81` only sets the mode bits; FILE-FORMATS says videoMode 0-2). |
| P3.03 | GAP | `render_affine` `affine_layer_rotates_and_scales` checks one transform (45 degrees, x2); `mode7_table_has_perspective`; `TemplateTests.RacingDrivesTheMode7CircuitAndCountsLaps`. No sweep over angles and scales. |
| P3.04 | DONE | `render_map` `map_streams_rows_and_columns` (64x32 = 512 x 256 BG; every visible cell checked in VRAM along a camera path; map bytes per frame <= 2 KB asserted and logged). |
| P3.05 | DONE | `render_map` `parallax_scroll_registers` (x0.2 / x0.5); `ViewportRomTests.Viewport_DrawsTheSceneLikeTheRom` part B (0.2 / 0.5 / 1). |
| P3.06 | GAP | `render_sprites` `hundred_twenty_eight_sprites_share_frames` (128 + refused 129th, priority sort, frame cache), `oam_shapes_flips_and_affine` (12 shapes), `overloaded_screen_line_is_reported`. The 32-matrix limit (`render/sprite.cpp:246`) is never exercised. |
| P3.07 | DONE | `render_sprites` `animation_timing_loop_once_pingpong`; `SpriteEditorE2ETests.P11_26_Timeline_AnimationsTicksLoopPingPongOnionSkin`. |
| P3.08 | GAP | `render_effects` `fade_tint_flash`, `colour_cycle_directions` (Left, Right). PingPong (`render/palette.hpp:14`) is not tested. |
| P3.09 | DONE | `render_text` `glyph_pixels_2bpp_and_shadow`, `typewriter_reveals_by_step` (blips, continue marker); `TextPipelineTests.ThaiLabelInRomMatchesThePreviewRendererPixelForPixel`; `ThaiCorpusTests.CorpusLinesInTheRomMatchThePreview` + `CorpusMatchesTheReviewedGoldenSheet` (`tests/AgbStudio.Tests/Golden/thai-corpus.png`). |
| P3.10 | DONE | `render_effects` `scanline_dma_writes_each_line`; `render_affine` `mode7_table_has_perspective`; `TemplateTests.RacingDrivesTheMode7CircuitAndCountsLaps`. |
| P3.11 | GAP | `audio_basic` `music_plays_and_fades` (play, volume, fade to stop, audio < 10% of a frame); `FixtureTests.GeneratedMusicIsReadByMaxmod`. Missing: a recorded EMU listen check, a recorded mixer CPU figure against the Figma 6% (test only asserts < 10%), loop start. |
| P3.12 | GAP | `audio::playDmg` (`runtime/src/audio/audio.cpp:124`); `audio_basic` `dmg_square_effect_owns_channel_for_its_frames` covers Square 1 and Wave only. On main `.sfx` files are not compiled into ROM register tables (templates ship SFX as `.wav`), so "0 bytes of sample ROM" is not shown; compiling `.sfx` is uncommitted work in progress. `SfxTests.Preview_SoundsLikeTheEmulator` exists only on `p11-integrate`. |
| P3.13 | GAP | `audio::usage()` reports DSound A / B and DMG (`runtime/include/agb/audio.hpp:50`); RT checks `usage().dmg[0]` only. On `p11-integrate`, `AudioTests.ChannelsMixerBudget_AndPreviewSfx` covers the report (unmerged). |
| P3.14 | DONE | `render_effects` `transitions_fade_and_slide` (Fade 8 frames, Slide, Cut). |
| P3.X1 | GAP | No combined ROM with 4 scrolling BGs + 128 sprites + music and a profiler capture; then EMU, USER. |

## Phase 4 · Runtime: Game framework

| Item | Status | Evidence or gap |
|---|---|---|
| P4.E1 | USER | Tick after P3.X1. |
| P4.01 | GAP | Screen stack and 7 staged steps (`game.cpp:66` step names, overrun log naming the step at `game.cpp:432`); `framework_platformer` checks load <= 8 frames and logs per-step cycles. No assertion that each step stays under 280,896 cycles, no test of the step-overrun log, push / pop / return continuation not tested directly. |
| P4.02 | DONE | `framework_platformer` `platformer_scene_end_to_end` (Start, Update, OnTouch, Get, Is, Destroy); scripts ROM `translated_behaviours_play_like_the_figma_code`; `TranslatorTests.ScriptingApiMembersTranslate` (tag). |
| P4.03 | GAP | Scenarios exist for Platformer Body, Health, Damage on Touch, Patrol (`framework_platformer`), slopes (`framework_slopes`), Top-down Mover and Projectile (Top-down and Shoot 'em up template tests). No scenario for Chase Player and Sound on Event; one-way platforms not asserted. |
| P4.04 | GAP | Solid, slopes and collider size / offset tested. One-way, Ladder, Hazard and Water (`runtime/src/framework/behaviours.cpp:84-168`) have no UT or RT. |
| P4.05 | GAP | Follow and clamp tested (`framework_platformer`). Camera Zone actors have no camera effect (only made non-solid, `runtime/src/framework/actor.cpp:85`); dead zone and shake have no RT assertions. |
| P4.06 | GAP | Player, Enemy, Pickup, Trigger Zone (`framework_platformer`), Text Label (`TextPipelineTests`, `ViewportRomTests`). No RT for Empty Actor; Camera Zone does nothing at runtime. |
| P4.07 | GAP | Make Prefab writes `prefabs/<name>.prefab` (`PopoverAndMenuTests`, around line 104), but the build ignores prefabs (no reference in `GameBuilder`), there is no ROM prefab template and no UT; `.prefab` is not in FILE-FORMATS.md. |
| P4.08 | GAP | Id spaces exist (0x0000 saved, 0x4000 per-map, 0x8000 session; `framework_bus_vm` `vm_runs_cutscene_with_waits_calls_and_stores` checks map and session vars); new dialogue flags take the first free id (`tools/AgbStudio.Scripting/Dialogue/DialogueCompiler.cs:193-200`). No UT that adding a flag keeps existing ids and save data. |
| P4.09 | DONE | `GraphTests.FigmaGraphGeneratesTheFigmaCode` (`Game.score += 10`), `GraphTests.GeneratedCodeCompilesAndTranslates`; `TranslatorTests.ScriptingApiMembersTranslate` (`agb_state.score`, `coins`). |
| P4.10 | DONE | `framework_bus_vm` `bus_rules_are_independent_of_presentation` (headless vs presented, AI sources, replay); `TemplateTests.RpgShopsAndWinsABattle`. |
| P4.11 | GAP | Scene.Load (scripts ROM dialogue "Go to scene"; Top-down door), Save.Checkpoint (`framework_platformer`), start scene (`TemplateTests.SampleProjectsBuildAndBootToTheTitle`). Scene.Pause is only translated, not run in a ROM. |
| P4.12 | DONE | Unblocked by D-23 (options screen designed). `framework_ui` `options_remap_swaps_buttons_and_sets_volume`; `UiKitTests.SplashTitleOptionsAndPauseInABuiltGame`. The checklist line still says "Blocked". |
| P4.X1 | GAP | No test runs the fixture's Forest_01 with a frame-overrun check (ViewportRomTests compares pixels only; the sample test stops at the title); no EMU capture; then USER. |

## Phase 5 · World model: map / layout / tileset / metatile

| Item | Status | Evidence or gap |
|---|---|---|
| P5.E1 | USER | Tick after P4.X1. |
| P5.01 | GAP | Dedupe with flips (`GraphicsTests.TilesDedupeWithFlips`, `TilesetBuildsMetatilesWithCollision`); unique-tile stat (`TilemapE2ETests.MapPanel_SizeBgSizeColoursWrap_AndBudget`). No primary / secondary tileset split (a map has one `tileset`); no BUD tile count from `report.json`. |
| P5.02 | GAP | Runtime `Metatile` is 2x2 screen entries of one layer (`runtime/include/agb/data.hpp:35`) with separate collision and tag arrays: no second layer, no layer type, no round-trip UT. |
| P5.03 | GAP | No layout file: layers and cells live in `maps/<Name>.map`, nothing is shareable between maps, no UT. |
| P5.04 | DONE | `SceneFile` holds size, videoMode, map, camera, music, links; `ProjectModelTests.EveryFileFormatRoundTrips`; `SceneDialogTests.SceneSettings_AppliesEverySection_RenamesWithLinks_AndUndo`. |
| P5.05 | GAP | Portal to a spawn point (`framework_platformer` link), interactables and door (`TemplateTests.TopDownAdventureTalksPushesUnlocksAndEnters`). The "Only if" condition is checked only as generated C++ (`TranslatorTests.SceneEventsLinkConditionsAndDatabaseScriptCells`), never in a ROM. |
| P5.06 | GAP | OnEnter runs in a ROM (`TemplateTests.VisualNovelTellsTheStoryAndRemembersTheChoice`: chapter = 1 is set by `Story.OnEnter`); scene event codegen UT. OnExit and OnLoad are not asserted in any ROM test. |
| P5.07 | GAP | The runtime ignores the link kind (`checkLinks`, `game.cpp:257`, fires on any player overlap), so Door and Respawn behave like On touch; no RT warps Sky Hopper Forest_01 to Forest_02 and Cave_01. |
| P5.08 | DONE | `LayerFeatureTests.ImageLayerAndAnimatedTilesShowInTheRom` (animated tile changes on screen); random brush data: `TilemapModelTests.Brush_StampRandomFlip_UndoRedo_SaveRoundTrip`. |
| P5.09 | DONE | `AutoTileTests.GrassEdgesResolveFromNeighbours`; `TilemapModelTests.AutoTile_PaintingAMemberResolvesTheGrassEdgesRules`. |
| P5.10 | DONE | Tile: `render_map`; image: `LayerFeatureTests.ImageLayerAndAnimatedTilesShowInTheRom`; rotate / scale: `render_affine` and `TemplateTests.RacingDrivesTheMode7CircuitAndCountsLaps`; collision-only: the Racing circuit "Ground" layer (`RacingTemplate.cs:203`) driven in that test. |
| P5.X1 | GAP | No test loads all 5 fixture scenes through their 6 links with transitions; then EMU, USER. |

## Phase 6 · Scripting: 3 tiers + Event Script VM

| Item | Status | Evidence or gap |
|---|---|---|
| P6.E1 | USER | D-02 is "decided (default)"; user confirms. |
| P6.01 | USER | DOC exists: generated `subset.html` (`tools/AgbStudio.Scripting/ApiDocs.cs` SubsetHtml: types, classes, generics, attributes, "Not available, and why"), checked by `IdeTests.ApiDocsCoverEveryPublicMember`; plus D-02. Show the page to the user. |
| P6.02 | DONE | `AnalyzerTests` Gae001-Gae006 positive and negative cases, `SeverityPerRuleIsConfigurable`, `ProjectFileSeveritiesAreRead`, `FigmaRuleTitlesAndDescription`; `ProjectSettingsTests.TreatWarningsAsErrors_FailsTheScriptStepOnAWarning`. |
| P6.03 | DONE | `CodeFixTests.PopupOffersBothFixes`, `ChangeToFixedRewritesTypeAndLiteral`, `ChangeToFixedOnLocalsAndReturnTypes`, `IgnoreAddsPragmaAroundTheLine`, `IgnoreWorksForErrorRules`. |
| P6.04 | DONE | `TranslatorTests.TranslationMatchesGolden`, `NoExceptionsNoRttiNoHeap`, `FilesAreWrittenOnlyWhenContentChanges`, `GeneratedCppCompilesWithRuntimeFlags`; `RomTests.TranslatedScriptsRunInTheEmulator` (`translated_behaviours_play_like_the_figma_code`). |
| P6.05 | DONE | `TranslatorTests.PlantedSyntaxErrorReportsFileLineColumn` ("PlayerController.cs(22,9): ';' expected"), `GxxErrorsMapBackToCsLines`; `PipelineTests.GccErrorsBecomeClickableDiagnostics`; `CSharpDebuggerTests.BreakpointPausesWithCallStackAndResumes`. |
| P6.06 | GAP | Public fields reach the Inspector (`InspectorTests.BehaviourFields_SliderTextColliderAndUndo`), but script fields are built with slider off (`editor/.../SceneEditor/Behaviours.cs:78,89`), so `[Range(0.5, 3)] speed` shows a text box although the subset doc says [Range] adds a slider. T-P04 "Exposed fields" with a slider is tested only on `p11-integrate` (`ScriptEditorTests.SidePanelAttachedToCompilesToExposedFieldsOutlineAndApi`, unmerged). |
| P6.07 | DONE | `TranslatorTests.ProjectNamespaceSetting`. |
| P6.08 | DONE | `TranslatorTests.TemplatesCompileClean` (Behaviour, StaticHelper, SceneScript). |
| P6.09 | DONE | `TranslatorTests.ScriptingApiMembersTranslate`; `IdeTests.ApiDocsCoverEveryPublicMember` (offline pages, one per type, every member), `IdeTests.AutocompleteListsTheApiMembers`. |
| P6.10 | DONE | `BytecodeTests.RescueIntroGoldenBytecode`, `ChoiceWaitAndReturnGoldenBytecode`. |
| P6.11 | DONE | `BytecodeTests.OpcodeTableIsTheSingleSourceForCompilerAndVm` (`runtime/include/agb/script_ops.def`), `BuiltinNativesMatchTheRuntime`. |
| P6.12 | GAP | `framework_bus_vm` `vm_runs_cutscene_with_waits_calls_and_stores` (priorities, call / return, locals, WaitFrames, native waits); scripts ROM Shopkeeper `natives::resume` (Continue). No `WaitUntil`: no opcode in `script_ops.def`, no API member. |
| P6.13 | DONE | `BytecodeTests.ScriptNativeTableFromAttributes`. |
| P6.14 | GAP | `framework_bus_vm` `vm_breakpoint_pauses_and_resumes`. Single step (`DebugState.resume = 2`, `runtime/src/script/vm.cpp:180`) and the variable view are not RT-tested. |
| P6.15 | DONE | `DialogueTests` (`ForestIntroGoldenCSharpAndBytecode`, `LinesChoicesAndCommandsBecomeTheRightCalls`, ...); scripts ROM `cutscenes_and_dialogue_run_in_the_vm` (both choices). |
| P6.16 | DONE | `GraphTests.FigmaGraphGeneratesTheFigmaCode`, `CatalogCoversEveryFigmaCategory`, `EveryActionAndValueNodeGeneratesCompilableCode`. |
| P6.17 | DONE | `GraphTests.RoundTripKeepsTheGraph`, `EditedGeneratedCodeUpdatesTheGraph`, `OnlyGraphGeneratedFilesConvertBack` (D-11). |
| P6.18 | DONE | `PlayModeE2ETests.P12_11_HotReload_SaveAScript_GameContinuesInTheSameSceneWithTheNewCode`, `P12_06_LiveInspector_StatePositionVelocityOnGroundScriptFields_ChangeWhileRunning`; `PlayModeTests.HotReloadContinuesInTheSameSceneWithTheSameState`. |
| P6.19 | GAP | `DatabaseTests.GeneratesEnumsStructsAndRows` (one unit per table), `DatabaseBuildTests.TablesBuildIntoTheRomAsData`. No test that changing one row rebuilds only that table's unit. |
| P6.20 | DONE | `DatabaseTests.GeneratesEnumsStructsAndRows` (`static const Item& Get(ItemId id)`); `TranslatorTests.ScriptingApiMembersTranslate` (`agb_db::Items::Get(agb_db::ItemId::Potion)`). |
| P6.X1 | USER | Fixture builds with its scripts and boots (`TemplateTests.SampleProjectsBuildAndBootToTheTitle`, "Sky Hopper demo"); its PlayerController has `float coyoteTime` (`SkyHopper.cs:448`), flagged by `AnalyzerTests.Gae002FloatFlaggedAsInFigma`. Show the build output with GAE002 and the title screen. |

## Phase 7 · Asset pipeline + build pipeline (hash cache)

| Item | Status | Evidence or gap |
|---|---|---|
| P7.E1 | USER | Tick after P2.X1 (D-03 decided by default). |
| P7.01 | GAP | Sprite and tileset meta edited in the editor (`SpriteEditorE2ETests.P11_25_SpriteSettings_OamSizeOriginColliderMirrorVram`, `ArtLibraryTests.AddBank_AndAssign_WriteTheOwnerIntoSpriteMeta`) and round-tripped. MusicMeta / SoundMeta have no editor on main (Audio page on `p11-integrate`: `AudioTests.Inspector_EditsTheMetaUndoAndSave`, unmerged) and no round trip; no schema (P1.04). |
| P7.02 | GAP | PNG paths: `GraphicsTests.SpriteSheetSplitsPadsAndReportsTooManyColors`, `QuantizerReducesTo15`, `PalettesPackIntoBanks`. No Aseprite decoder though the UI says "Drop PNG / Aseprite here" (`SpriteEditorPage.cs:223`); TrimEmptyFrames (`SpriteConverter.cs:35`) untested on main; no golden output files. |
| P7.03 | GAP | `GraphicsTests.SpriteSheetSplitsPadsAndReportsTooManyColors`: 48 colours give "Reduce to 16" and "Split into 4 palettes"; Figma and the item say "Split into 3 palettes". T-D24 dialog only on `p11-integrate` (`ImportTests.Problems_OpenTheErrorsDialog_FixesAndSkipsErrors`). |
| P7.04 | GAP | No Tiled importer on main. Done on `p11-integrate` (`ImportTests.Import_ConvertsIntoTheProjectFolders`, `editor/.../Import/AssetImporter.cs` ReadTmx), unmerged. |
| P7.05 | DONE | `GraphicsTests.PaletteFilesRoundTrip`; `ArtLibraryTests.ExportAndImport_PalAndGpl`; `PaletteManagerE2ETests.P11_35_ImportPalGplAndExport`. |
| P7.06 | GAP | MOD / XM to soundbank via mmutil works (`FixtureTests.GeneratedMusicIsReadByMaxmod`). WAV resampling to 11 kHz mono 8-bit and the MP3 message exist only on `p11-integrate` (`ImportTests.Detects_EachTypeWithItsOptionsAndProblems`, `Import_ConvertsIntoTheProjectFolders`), unmerged. |
| P7.07 | GAP | On main GameBuilder does not compile `.sfx` into register tables (work in progress, uncommitted `tools/AgbStudio.Build/Audio/DmgRegisters.cs`); Bake to `.wav` exists only on `p11-integrate` (`SfxTests.BakeToWav_SavesTheSampleAndHardwareKeepsZeroBytes`). No pipeline UT. |
| P7.08 | DONE | `GraphicsTests.Lz77RoundTripsAndIsVramSafe`; tilesets are LZ77 by default (`TilesetMeta.Compress = true`, `Build.CompressGraphics = true`, `GameBuilder.cs:271`) and `ViewportRomTests.Viewport_DrawsTheSceneLikeTheRom` matches the BIOS-decompressed tiles pixel for pixel (indirect RT). |
| P7.09 | DONE | `PipelineTests.SecondBuildWithoutChangesConvertsNothingAndIsFast` (0 converted; one option change reconverts 1); `AssetCache.cs` (`build/cache/manifest.json`, converter version in the key). |
| P7.10 | GAP | Flags and steps in `NativeBuild.cs:56-71` (thumb, arm7tdmi, `*.iwram.cpp` as ARM, gc-sections, objcopy, gbafix); `PipelineTests.MiniPlatformerBuildsAndPlays`, `ReportAttributesRomToAssets` (title "MINIPLAT", code "AMPE"), `BuildsWithoutNinjaAndSkipsUpToDateObjects`. Maker code and version bytes are not checked. |
| P7.11 | GAP | Assert / log matrix: `PipelineTests.FailedAssertsLogFileAndLineAndStopTheGame`, `RuntimeRomTests.ReleaseBuildStripsLogText`; Optimise is passed to the compiler. On main the output folder and "Run in mGBA after build" are not used by the build (wired only on `p11-integrate`); the ROM-size warning (`BuildReport.cs:114`) is untested; no config-matrix UT. |
| P7.12 | GAP | `PipelineTests.ReportAttributesRomToAssets` (ROM size, breakdown, largest assets, IWRAM / EWRAM, scenes, history), `ErrorsNameTheFileAndTheProblem` (failed builds in history). Sizes come from `nm` (`BuildReport.cs:159`); no test compares them with the `.map` file. |
| P7.13 | GAP | On main T-D06 / T-D23 are not built; build results reach the Console (`BottomPanelTests.Console_TakesBuildResultsFromTheBuildService`). E2E exists only on `p11-integrate` (`BuildTests.Stream_FromTheBuildThread_UpdatesStepsProgressAndLog`, `BuildRom_RealBuild_StreamsStepsAndUpdatesStatusAndReport`, `Failure_ShowsTheFailedStepItsPredecessorSkippedStepsAndErrors`, `GoToFirstError_OpensTheScriptAtItsLine`), unmerged. |
| P7.14 | DONE | `MenuActionTests.CleanBuild_DeletesTheBuildFolder`; `PreferencesTests.Paths_ClearCacheEmptiesTheProjectsBuildCache`. |
| P7.15 | GAP | No export on main (T-D05 unbuilt; TOOLCHAIN.md wrongly says "export pads itself"). On `p11-integrate`, `BuildTests.RunInEmulator_AfterSuccess_OpensPlayModeWithTheRom_AndExportCopiesPadded` checks the 512 KB padded copy of a fake ROM; no EMU boot of a padded real ROM. |
| P7.16 | DONE | `PopoverAndMenuTests.AssetContextMenu_EveryItem` (Find Usages), `MenuActionTests.FindUnusedAssets_ListsAssetsNothingMentions`, `ArtLibraryTests.Banks_LoadedWithUsedByLists`, `DatabaseWindowTests` (Used by rows). |
| P7.17 | DONE | `PopoverAndMenuTests.AssetContextMenu_EveryItem` ("Reimported slime.png", `AssetActions.cs:360`). |
| P7.18 | GAP | `PipelineTests.FixtureRebuildsAfterAOneLineScriptChangeInUnderASecond` committed in `aa0a44d` (best of two warm edits); the working tree is still changing this file and no CI timing record exists yet. |
| P7.X1 | GAP | The fixture builds and boots (`TemplateTests.SampleProjectsBuildAndBootToTheTitle`); the no-change rebuild is proven on the mini project only; report vs map not compared (P7.12); then USER. |

## Phase 8 · Font manager + Thai text

| Item | Status | Evidence or gap |
|---|---|---|
| P8.E1 | USER | Tick after P3.X1 and P7.X1 (D-17 decided by default). |
| P8.01 | GAP | TTF works (all fixture fonts). BDF and image-sheet readers exist (`tools/AgbStudio.Build/Fonts/GameFont.cs:104-110, 205, 255`) but no UT builds a font from either; OTF / CFF is rejected (`TrueType.cs:22`). |
| P8.02 | DONE | `FontTests.FontManager_ListSetsGlyphsDetailPreviewStats` (advance per glyph), `NewFont_FromFontsPlus_CreatesAndShowsTheFont` (Fixed 8x8), `FontManager_EditSettingsSetsAndPixels_SaveUndo` (height, base, spacing, line). |
| P8.03 | DONE | `FontTests` (95 + 87 + 12 glyphs; "Basic Latin · 95", "Thai · 87", "Symbols · 12", "Japanese kana · 0"; `Groups_ThaiConsonantsAreThe44Letters`); `LocalizationWindowTests.P11_42_AddLanguageJapaneseCreatesAKanaFont_FrenchUsesTheCurrentFont`. |
| P8.04 | DONE | `FontTests.GlyphEditor_PaintWritesAnOverrideFontBuilderApplies_Undo`, `MissingIn_ListsCharactersWithoutGlyphs`, `AddCharacters_PasteTextAddMissingGlyphs` (Missing cells). |
| P8.05 | DONE | `ThaiCorpusTests.EveryClusterStacksItsMarksWithoutCollisions` (44 consonants x upper / lower vowels x tone marks + sara am; ascender and descender rules), `CorpusMatchesTheReviewedGoldenSheet` (commit `dec626c`). |
| P8.06 | GAP | `TextPipelineTests.DictionaryWrapNeverSplitsWords` (ICU `third_party/icu/thaidict.txt`) only checks line widths and one first-line case; it does not assert that every break is in `ThaiText.WordStarts`; no RT of a multi-line wrapped Thai string. |
| P8.07 | GAP | `render_text` `glyph_pixels_2bpp_and_shadow` (4 shades, shadow, BG text layer). The "4 / 16 KB" text VRAM meter (`FontTests`, line 219) is computed from runtime defaults, not read from `report.json` or the profiler (BUD). |
| P8.08 | GAP | Add Characters works (`FontTests.AddCharacters_RenderFromSourceOrEmptyGlyphs` incl. project scan, `AddCharacters_PasteTextAddMissingGlyphs`), but the Figma sample finds 2 missing ("2 characters are not in thai_vwf yet"), not the 7 the item expects. |
| P8.09 | DONE | `ViewportRomTests.Viewport_DrawsTheSceneLikeTheRom` (full-frame BGR555 compare including the Thai HUD label; fixture default language th); `TextPipelineTests.ThaiLabelInRomMatchesThePreviewRendererPixelForPixel`; `FontTests.DialoguePreview_TextPixelsAreTheRomRenderer`; `LocalizationWindowTests.P11_41_StringPanelAndGbaPreviewDrawLikeTheRom`. |
| P8.10 | DONE | `ProjectSettingsTests.EveryField_PersistsToTheAgbproj` (default font and language); `LocalizationWindowTests.P11_42_AddLanguageJapaneseCreatesAKanaFont_FrenchUsesTheCurrentFont`; `FontTests.ProjectFonts_DefaultFirst_BuiltByFontBuilder`. |
| P8.11 | GAP | New Font caption "about N KB of ROM" (`FontTests`, line 146); no check that the estimate is within 10% of the font's bytes in `report.json`. |
| P8.X1 | GAP | Waits on G-THAI.02 (G-THAI.06 may be n/a until hardware); then USER reviews the Thai screenshots (`docs/evidence/app/P10-thai-*.png`, corpus sheet). |

## Phase 9 · Database + Localization

| Item | Status | Evidence or gap |
|---|---|---|
| P9.E1 | USER | Tick after P6 tier 3 and P7.X1. |
| P9.01 | DONE | `DatabaseWindowTests.P11_39_TablesWithCountsSchemaStatusAndRomSize`, `P11_39_FilterAndAddRowWithUndo`, `P11_39_EditCellsInPlaceAndSaveWritesTheDbFile`, `P11_39_RecordPanelShowsTheRowAndEditsIt`. |
| P9.02 | DONE | `DatabaseTests.FieldBytesMatchAddField` (enum = 1 B); `DatabaseWindowTests.P11_40_AddFieldRarityEnumIsOneBytePerRow`. |
| P9.03 | DONE | `DatabaseWindowTests.P11_40_NewTableFromEmptyCsvAndCopy_UndoRemovesTheFile`; `DatabaseBuildTests.TablesBuildIntoTheRomAsData`. |
| P9.04 | DONE | `DatabaseTests.ImportCsvMapsColumnsAndReportsBadRows` ("Rows 7 and 15: Cost is ..."); `DatabaseWindowTests.P11_40_ImportCsvWarnsOnRows7And15ThenImports`. |
| P9.05 | DONE | `DatabaseWindowTests` Used by rows "Shop_01 · slot2", "Chest · Cave_01", "Slime.cs · drop table" (line 149); `editor/AgbStudio.Editor/Windows/Database/UsedBy.cs`. |
| P9.06 | GAP | Script cells compile (`TranslatorTests.SceneEventsLinkConditionsAndDatabaseScriptCells`: `agb_dbscript_Items_Potion_use`); no ROM test calls one. |
| P9.07 | GAP | "ROM 528 B" in the status bar (`DatabaseWindowTests.P11_39_TablesWithCountsSchemaStatusAndRomSize`) is computed by the editor and never compared with the table's bytes in `report.json`. |
| P9.08 | DONE | `LocalizationWindowTests.P11_41_LanguagesWithProgressGroupsAndStatus`; `FixtureTests.CountsMatchTheFigmaFrames`. |
| P9.09 | DONE | `LocalizationWindowTests.P11_41_LanguagesWithProgressGroupsAndStatus`, `P11_42_AddLanguageJapaneseCreatesAKanaFont_FrenchUsesTheCurrentFont` (translate from, font, mark all missing, fallback); `PipelineTests.MissingStringsUseTheLanguageFallbackOrShowTheKeyInDebug`. |
| P9.10 | DONE | `LocalizationWindowTests.P11_41_FitCheckSaysHowManyLinesTheBoxNeeds`, `P11_41_TypingThaiUpdatesTableFitAndPreview_UndoAndSave` (same TextLayout as the build). |
| P9.11 | DONE | `LocalizationWindowTests.P11_41_SearchGroupsAndMissingOnly`; `PipelineTests.MissingStringsUseTheLanguageFallbackOrShowTheKeyInDebug` ([key] in Debug); `LimitTests.MissingStringsWarnOrFailAsLocalizationSays`. |
| P9.12 | DONE | `LocalizationWindowTests` (groups, speaker "Hopper", notes; lines 93-116). |
| P9.13 | DONE | `LocalizationWindowTests.P11_42_ExportCsvWritesEveryKeyAndLanguage`. |
| P9.14 | GAP | `text::setLanguage` exists (`runtime/src/render/textdb.cpp:60`, options row `runtime/src/framework/ui.cpp:452`) and `UiKitTests` switches th / en, but no RT asserts that Text.Show or dialogue text changes language in the same scene. |
| P9.X1 | GAP | No RT builds the fixture in English and Thai and shows the Japanese fallback (fallback proven on the mini project only); then USER. |

## G-HW · GBA hardware limits

| Item | Status | Evidence or gap |
|---|---|---|
| G-HW.01 | GAP | BG VRAM: `LimitTests.TooMuchBackgroundVramFailsTheBuildWithTheScene` (`b1d8de2`, `GameBuilder.CheckHardwareLimits`). No build check for OBJ VRAM (32 KB, 16 KB in bitmap modes; the runtime only warns, `render/sprite.cpp:99`); no RT limits ROM. |
| G-HW.02 | GAP | `render_sprites` (129th sprite refused, `overloaded_screen_line_is_reported`), `DebuggerE2ETests.P13_03_OamTable_ActorXYSizeTilePalPrioFlags_InUse`. The 32-affine-matrix limit is not exercised. |
| G-HW.03 | GAP | BG banks: `LimitTests.TooManyBackgroundPalettesFailTheBuild`. No OBJ bank limit check in the build. |
| G-HW.04 | GAP | IWRAM / EWRAM from `nm` with an 8 KB stack warning (`BuildReport.cs:116`); `PipelineTests.ReportAttributesRomToAssets` checks hardware caps only. The blueprint 10 default split is neither encoded nor checked. |
| G-HW.05 | GAP | Warning above the Project Settings threshold (`BuildReport.cs:114`) is untested; nothing fails a ROM above 32 MB. |
| G-HW.06 | GAP | `ProjectSettingsTests` meters "189 B / 32 KB" and "189 B / 8 KB"; `core_save_*` `capacity_and_crc`. The "save slots do not fit in the save chip" validation (`ProjectSettingsModel.cs:182`) is untested and the build does not re-check it. |
| G-HW.07 | GAP | All 8 `TemplateTests` fail on gameplay frame overruns (`GameplayOverrun`). Load-step overruns are excluded on purpose and VBlank commit time is not measured, so two of the three conditions are unverified. |
| G-HW.08 | GAP | No UT of channel allocation; `audio::usage()` is only spot-checked in `audio_basic`. |

## G-THAI · Thai text rendering

| Item | Status | Evidence or gap |
|---|---|---|
| G-THAI.01 | DONE | `ThaiCorpusTests.EveryClusterStacksItsMarksWithoutCollisions`, `DigitsMaiYamokAndMixedLinesLayOutWithoutMissingGlyphs`, `CorpusMatchesTheReviewedGoldenSheet`, `CorpusLinesInTheRomMatchThePreview` (3 ROM lines). |
| G-THAI.02 | GAP | As P8.06: the never-splits assertion is weak, and no test ties wrap breaks to the Localization fit check result. |
| G-THAI.03 | DONE | See P8.09. |
| G-THAI.04 | DONE | `ShellE2ETests.Thai_TypingIntoTextFieldsAndSearch`; `LocalizationWindowTests.P11_41_TypingThaiUpdatesTableFitAndPreview_UndoAndSave`; `DatabaseWindowTests` (Thai localized name typed, line 155); `DialogueWindowTests.P11_47_NewSpeakerWithThaiNamePortraitColourAndBlip`, `P11_46_EditTextInACardSavesTheDialogueAndTheStringTable`; `FontTests`; dialogue box in ROM: `ThaiCorpusTests.CorpusLinesInTheRomMatchThePreview`. |
| G-THAI.05 | DONE | `docs/THIRD_PARTY.md` rows Noto Sans Thai (OFL, `third_party/fonts/OFL-NotoSansThai.txt`) and ICU thaidict (`third_party/icu/LICENSE-ICU.txt`); D-17. |
| G-THAI.06 | HW | Allowed as n/a until hardware (TOOLCHAIN.md: no flash cart). |

## G-ERR · Error handling

| Item | Status | Evidence or gap |
|---|---|---|
| G-ERR.01 | GAP | Several errors follow what / why / how (`TranslatorTests.ErrorsSayWhatWhyAndHow`, `PipelineTests.ErrorsNameTheFileAndTheProblem`, `BytecodeTests.CutsceneErrorsExplainTheLimits`, `LimitTests` messages), but there is no recorded review of all error strings and some lack a fix (for example "devkitARM build failed" in `BottomPanelTests.Console_TakesBuildResultsFromTheBuildService`). |
| G-ERR.02 | GAP | Diagnostics carry `file(line,col)` (`PipelineTests.GccErrorsBecomeClickableDiagnostics`). On main the Console shows "File.cs(line)" and clicking opens T-P04, which is not built. Done on `p11-integrate` (`BuildTests.GoToFirstError_OpensTheScriptAtItsLine` opens line 22, column 9), unmerged. |
| G-ERR.03 | GAP | `LimitTests` (pool full in the Console, missing strings warn / fail, missing glyphs, BG VRAM and palette budgets fail the build; `b1d8de2`). A failed import reaching the Console has no test. |
| G-ERR.04 | GAP | Open project: `OpeningProjectTests.P11_06_CancelDuringLoadAssets_LeavesTheOpenProjectAsItWas`. No cancel E2E for build, flash or import (none on `p11-integrate` either). |
| G-ERR.05 | GAP | `ServiceTests.Toolchain_ReportsFoundAndNotFoundWithFakeFolders` (UT). The E2E exists only on `p11-integrate` (`ToolchainHelpTests.Build_WithAToolMissing_NamesItsPathAndOffersPaths`), unmerged. |
| G-ERR.06 | GAP | Autosave exists (`ServiceTests.Autosave_SavesDirtyDocumentsEveryNMinutes`); there is no crash handler or crash log anywhere in `editor/` (also not on `p11-integrate`) and no forced-crash E2E. |
| G-ERR.07 | DONE | `PipelineTests.FailedAssertsLogFileAndLineAndStopTheGame` (Debug stops with "Checker.cs(9)"; Release runs on; Release + Keep asserts stops), commit `63f1125`. |

## G-PERF · Performance budgets

| Item | Status | Evidence or gap |
|---|---|---|
| G-PERF.01 | DONE | `TemplateTests` plays all 8 templates with bots and fails on any gameplay overrun (`GameplayOverrun`); hardware n/a. |
| G-PERF.02 | GAP | `PipelineTests.FixtureRebuildsAfterAOneLineScriptChangeInUnderASecond` committed in `aa0a44d`; still being edited in the working tree and not yet recorded as passing in a CI run. |
| G-PERF.03 | GAP | No targets for clean build, project open and Play start are recorded (not in DECISIONS.md), so nothing is measured against them. |
| G-PERF.04 | GAP | Builds run off the UI thread (`PlayTestSupport` pumps the dispatcher while waiting) but no E2E measures UI latency during a build or Play. |
| G-PERF.05 | GAP | `audio_basic` logs the audio share and asserts < 10%; no recorded figure against the Figma 6%. |

## G-DOCS · Documentation

| Item | Status | Evidence or gap |
|---|---|---|
| G-DOCS.01 | DONE | `IdeTests.ApiDocsCoverEveryPublicMember` (every public member has a summary; offline HTML from `ApiDocs.Write`), `IdeTests.ReferenceAssemblyShipsWithXmlDocs`. |
| G-DOCS.02 | DONE | `docs/user/windows/README.md` + 72 window pages quoting the Figma captions (commit `aa0a44d`; captions in `docs/evidence/figma/captions.json`, `b1d8de2`). Pages for windows not merged into main describe `p11-integrate` behaviour. |
| G-DOCS.03 | DONE | `docs/user/gba-hardware-guide.md` listed in Launcher · Learn (`LauncherPage.cs:475`); tutorials (`TutorialTests`, `LauncherE2ETests.P11_03_Learn_TutorialProgressDocumentationAndSamples`). |
| G-DOCS.04 | GAP | Stale: BUILD.md says Ninja on PATH is required (optional since `6067cd8`); TOOLCHAIN.md says "export pads itself" (no export on main); FILE-FORMATS.md says videoMode 0-2 and lacks `prefabs/*.prefab` and the `.graph` / `.dlg` fields; ARCHITECTURE.md lacks blueprint 05-10, 12, 14. No per-phase REV record. |
| G-DOCS.05 | GAP | `docs/RELEASE-NOTES.md` 1.0.0 lists Audio with tracker, SFX Designer, Logic Graph, World Map, Script Editor, Scripting API, Build ROM, Build Report, Export ROM and Flash to Cartridge, which are not built on main (they are on unmerged `p11-integrate`). |

## G-LEGAL · Legal and IP

| Item | Status | Evidence or gap |
|---|---|---|
| G-LEGAL.01 | DONE | Lint no-pokeemerald (`gMain`, `gSaveBlock1/2Ptr`, `VAR_RESULT`, `MUS_`, `SPECIES_`, ...): `LintTests.PokeemeraldNamesAndWrongProductNamesAreReported`, `RepositoryIsClean`; VM opcodes are generic stack operations (`script_ops.def`), checked in this audit. |
| G-LEGAL.02 | DONE | `git ls-files` (1547 files): no `.gba`, `.bin`, `.sav` or BIOS images tracked (only the `bios.hpp` / `bios.cpp` wrappers); the only binary is the MPL-2.0 `third_party/mgba/mgba_libretro.dll`. |
| G-LEGAL.03 | DONE | Lint product-name rule; `Directory.Build.props` Product "AGB Studio"; MainWindow Title "AGB Studio"; AssemblyName `AGBStudio`; package `AGB-Studio-<version>-win-x64.zip` (`Packager.cs:23`). |
| G-LEGAL.04 | DONE | `docs/THIRD_PARTY.md` lists every named component (libgba not linked per TOOLCHAIN.md); About › Licenses opens it (`AboutTests.HelpAbout_ShowsVersionDateCreditsLicensesAndWebsite`). |
| G-LEGAL.05 | DONE | THIRD_PARTY.md "Reference only, no code copied: pokeemerald (pret), Butano (zlib), Poryscript"; `git grep` finds no Butano identifiers (`bn::`, `BN_`). |
| G-LEGAL.06 | DONE | D-20; template and sample content is generated in code (`tools/AgbStudio.Build/Content`: `PixelArt.cs`, `SplashArt.cs`, `Audio/XmWriter.cs`, `Audio/Chiptune.cs`, `Templates/*`); `templates/` and `samples/` hold no copied files. |
| G-LEGAL.07 | GAP | D-05 (MIT runtime) is stated only in `docs/DECISIONS.md`; no LICENSE file at the repo root and no user-facing doc or release note gives the licence of the runtime shipped in games. |

## G-EMOJI · No emoji

| Item | Status | Evidence or gap |
|---|---|---|
| G-EMOJI.01 | GAP | Files: Lint no-emoji (`LintTests.PlantedViolationsAreReported`, `RepositoryIsClean`), `DesignSystemTests.Sources_NoEmoji`. Commit messages are clean today (0 emoji in 45 commits, checked in this audit) but `ci.cmd` does not scan commit messages. |

## G-SCOPE · Scope kept to Figma

| Item | Status | Evidence or gap |
|---|---|---|
| G-SCOPE.01 | GAP | 36 of 45 commits on main name neither a `T-...` nor `infra: ...` (for example `aa0a44d`, `63f1125`, `6067cd8`, `a889542`); no PR / commit template (P1.11). |
| G-SCOPE.02 | GAP | `WindowServiceTests.Catalog_KnowsEveryFigmaWindow` and `MenuActionTests.EveryTitleBarMenuCommand_ActsOrOpensItsWindow` cover the window and menu inventory, but no REV against the Figma inventory is recorded at any phase exit. |
| G-SCOPE.03 | USER | Scope log (checklist section 8) has one row, still "pending"; review it with the user at each phase exit. |

## Counts

| Group | DONE | GAP | USER | HW | Total |
|---|---|---|---|---|---|
| Phase 0 | 6 | 4 | 2 | 0 | 12 |
| Phase 1 | 4 | 8 | 1 | 0 | 13 |
| Phase 2 | 4 | 10 | 1 | 0 | 15 |
| Phase 3 | 6 | 9 | 1 | 0 | 16 |
| Phase 4 | 4 | 9 | 1 | 0 | 14 |
| Phase 5 | 4 | 7 | 1 | 0 | 12 |
| Phase 6 | 15 | 4 | 3 | 0 | 22 |
| Phase 7 | 6 | 13 | 1 | 0 | 20 |
| Phase 8 | 6 | 6 | 1 | 0 | 13 |
| Phase 9 | 11 | 4 | 1 | 0 | 16 |
| Cross-cutting gates | 15 | 25 | 1 | 1 | 42 |
| **All** | **81** | **99** | **14** | **1** | **195** |

## Gaps to fix

Ordered by size, small first. "Merge" means: the work exists on branch `p11-integrate` and only needs merging into
main (with its tests run there).

### Small (extend a test, add a record or a doc line)

1. P0.04: record `--version` (or usage banner) output of gbafix, grit, mmutil, gbalzss, padbin, bin2s in TOOLCHAIN.md.
2. P0.10: write the directory listing with timestamps of `GBAStudio.spec`, `build\` and the toolchain folders into TOOLCHAIN.md.
3. P1.03: add a root README.
4. P1.11 / G-SCOPE.01: add `.gitmessage` and a PR template with "Figma ref: T-... or infra: ..."; use it from now on.
5. P1.10, G-DOCS.04: map blueprint 05-10, 12, 14 in ARCHITECTURE.md; fix BUILD.md (Ninja optional), TOOLCHAIN.md (export padding), FILE-FORMATS.md (videoMode, `.prefab`, `.graph`, `.dlg`).
6. G-LEGAL.07: add LICENSE (MIT) at the root and state the runtime licence for games in a user doc and the release notes.
7. G-DOCS.05: make RELEASE-NOTES.md match main (or merge `p11-integrate` first).
8. G-EMOJI.01: add a commit-message codepoint scan to `ci.cmd` (`git log --format=%B` through the lint emoji check).
9. P1.01: give each D-xx entry question, options, answer and date.
10. P1.02: record decisions for design issues 1, 3 and 8 and close the Scope log row.
11. P2.02: count 600 frames in `frame_loop_counts_and_profiles`.
12. P2.08: compare a 16-value sequence per stream with the host reference.
13. P2.11: record the debug-channel deviation from "mGBA debug-print" in DECISIONS (or also write to mGBA's debug-print registers).
14. P3.03: sweep angles and scales in `affine_layer_rotates_and_scales`.
15. P3.06, G-HW.02: test the 33rd affine matrix warning.
16. P3.08: add PingPong to `colour_cycle_directions`.
17. P6.14: RT for VM single step (`resume = 2`) and reading locals.
18. P6.19: test that editing one row rewrites only that table's generated unit.
19. P7.03: make the 48-colour fix "Split into 3 palettes" as in Figma, or log the difference in the Scope log.
20. P7.10: check maker code and version bytes in the header test.
21. P7.12: compare report IWRAM / EWRAM / ROM figures with the `.map` file.
22. P8.06, G-THAI.02: assert every line break is a dictionary word start for each width; tie it to the fit check.
23. P8.08: make the Figma Add Characters sample report 7 missing (fixture font) or log the difference.
24. P8.11, P9.07, P8.07: compare the font ROM estimate, the table ROM size and the text VRAM meter with `report.json`.
25. G-HW.05: test the ROM-size warning; fail the build above 32 MB.
26. G-HW.06: test the "save slots do not fit" validation and re-check it in the build.
27. G-PERF.05, P3.11: record the measured mixer CPU next to the Figma 6% (tighten the assertion).
28. P0.05: draw a line of text in the hello ROM and re-shoot VBA-M.

### Medium (a new test or a contained feature)

29. P1.06: add artifact upload (ROMs, `report.json`, test results) to CI and keep a green-run record.
30. P1.04: JSON schemas for every file format; round trips for MusicMeta, SoundMeta, StringMeta, `.prefab`.
31. P2.03: RT that logs the frame phases and checks the blueprint 04 order.
32. P2.07: Debug test that triggers the stale-handle and double-free asserts.
33. P2.09: reference-table test and BUD cycle counts for fixed multiply / divide.
34. P2.10: run each save-type ROM in VBA-M (shot), test checksum off.
35. P2.12: RT for `lz77ToWram`, `div` / `mod`, `halt`, `fastCopy`.
36. P2.13: RT comparing debug-channel addresses with the `.map` file.
37. P3.01: RT with four tile layers in Mode 0.
38. P4.01: assert every staged-load step under one frame and the step-overrun log; test push / pop / resume.
39. P4.03, P4.04, P4.06: ROM scenarios for Chase Player, Sound on Event, Empty Actor, one-way, ladder, hazard, water.
40. P4.08: UT that adding a flag keeps existing ids and save data.
41. P4.11, P5.05, P5.06, P9.06: ROM tests for Scene.Pause, a link "Only if" condition, OnExit / OnLoad, a database script cell.
42. P6.06: slider for `[Range]` script fields in the Inspector (T-P01) and an E2E; merge the T-P04 Exposed fields test.
43. P6.12: `WaitUntil` (opcode + API + RT).
44. P7.11: config-matrix UT (Debug / Release, optimise, strip log, keep asserts, output folder, run after build, ROM warning); merge the run-after-build wiring.
45. P7.02: Aseprite import, a TrimEmptyFrames test and golden converter outputs.
46. P8.01: UT fonts from BDF and an image sheet (and decide on OTF).
47. P9.14: RT switching language in one scene and checking the shown text.
48. P4.X1, P5.X1, P7.X1, P9.X1, P3.X1: fixture-level ROM tests (Forest_01 at 60 fps; all 5 scenes through their links; no-change rebuild of the fixture; fixture in en / th with ja fallback; 4 BGs + 128 sprites + music stress ROM) with captures for the user.
49. G-HW.01, G-HW.03, G-HW.04, G-HW.08: build checks for OBJ VRAM and OBJ palette banks, the blueprint 10 RAM split, and an audio channel UT; a limits test ROM.
50. G-HW.07: assert load-step budgets and measure commit time in VBlank in the template tests.
51. G-ERR.01: review and fix every user-facing error string (what, why, how).
52. G-ERR.03: failed-import-to-Console test.
53. G-PERF.03: agree targets for clean build, project open and Play start; measure them.
54. G-PERF.04: E2E measuring UI dispatcher latency during build and Play.
55. G-SCOPE.02: record a REV of the app against the Figma inventory at each phase exit.

### Large (feature work or a branch merge)

56. Merge `p11-integrate` into main: closes or advances P3.12 (preview), P3.13, P7.01 (audio meta), P7.04, P7.06, P7.13, P7.15 (still needs an EMU boot of a padded ROM), G-ERR.02, G-ERR.05, G-DOCS.05.
57. P3.12, P7.07: compile `.sfx` into DMG register tables in the build (in progress in the working tree), bake to WAV, test Square 2 and Noise, show 0 bytes of sample ROM.
58. P3.02: per-mode screenshot tests and a Mode 3 bitmap path (or drop Mode 3 from the popover through the Scope log).
59. P4.05, P4.06: Camera Zone behaviour (limit the camera), dead zone and shake RT.
60. P4.07: prefabs as ROM templates in the build, documented format, UT.
61. P5.01, P5.02, P5.03: primary / secondary tilesets, two-layer metatiles with layer type, a shareable layout file (or record a decision that the current single-layer map model replaces them).
62. P5.07: Door and Respawn link kinds at runtime; RT warping through the fixture's links.
63. G-ERR.04: cancellable build, flash and import with consistent state, and E2E.
64. G-ERR.06: crash handler that saves work and writes a crash log, with a forced-crash E2E.
