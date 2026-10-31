# Final review 2 (2026-11-01, HEAD 60935dc, CI run 20261101-003956-60935dc, 0 failed tests)

## Scope-log rows (accepted option implemented and tested)
- T-S08 Image editor hint: PASS, PreferencesDialog.ImageEditorHint; PreferencesTests (ccb96d2).
- P5.01 primary tilesets: PASS, tests/AgbStudio.Tests/PrimaryTilesetTests.cs (4 tests), runtime/src/render/bg.cpp:177, runtime/tests/render_map.
- T-P07, P9.07 ROM size: PASS, ScriptSample.TableBytes -> DatabaseCodegen.TableBytes (576 B); DatabaseWindowTests.
- G-HW.04: PASS, RamBudget.cs defaults 36/8/4 KB; RamBudgetTests.
- T-D08, P4.12, P2.10 options block: PASS (done), runtime/tests/framework_ui options_persist_on_the_save_chip, LimitTests, ProjectSettingsTests.
- T-S07 font: PASS, Editor_FontAndThemeDropdownsShowTheirWholeNames.
- Version row: PASS, AboutTests:46 "1.0.0", LauncherE2ETests:333 "v1.0" (earlier commit).
- T-S08, P11.62 build cache: PASS, PreferencesDialog.CacheTitle/CacheText; PreferencesTests.
- G-SCOPE.02, T-P06 gestures: PASS, FixedGestures.cs; KeyboardShortcuts_ListsTheAnimationPanelGesturesAsFixed.
- T-D12, P10.08: PASS, NewProjectTests.cs:500 (close-project title).
- T-P11 bars: PASS, BuildReportPage.RomShare; BuildTests.
- T-P14, T-P16 preview: PASS, DialogueWindowTests / LocalizationWindowTests (light 0x7BDE box, one line).
- T-D04, T-P10 palette banks: NOT DONE, fixture writes no palettes.json (SkyHopper*.cs has none). Row stays pending.

## Checklist items
- P5.01 PASS. P9.07 PASS. G-HW.04 PASS (runtime-only rows; whole-ROM limits kept). P4.12 PASS (RT; owner accepted frame).
- P11.62 PASS (Preferences tabs, E2E tests, diff images T-S07/T-S08 viewed).
- P2.10 FAIL: still needs EMU per save type and HW.
- T-S07/matches PASS: docs/evidence/diff/T-S07.png, only icons differ; font now whole.
- T-S08/matches PASS: docs/evidence/diff/T-S08.png, differences are the accepted hint and per-project cache.
- T-P11/matches PASS: docs/evidence/diff/T-P11.png, bars to scale, 1.47 %.
- T-D12/matches PASS: docs/evidence/diff/T-D12.png, 2.22 %, icons only.
- T-P07/matches FAIL: review-shot-windows-2 lists doc tab, Heart price, scrollbar, script box.
- T-P14, T-P16, T-P06 matches FAIL: other FIX differences remain (review-shot-windows-2).
- T-D04/matches, T-P10/matches FAIL: preview frame size, fade seams, palette bank row open.
- T-K05 FAIL (art seams, FIX). P10.05 FAIL (Scene Card F10 FIX). P12.05 FAIL (T-P02 hitboxes on HUD, FIX).
- Other FIX windows in review-shot-windows-2 (T-P01..T-P26, T-D01, T-D10, T-D15, T-D19, T-D21, T-O*, T-L*) FAIL, not re-rendered.
