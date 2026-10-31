# Final review 3 (HEAD d5b8677, CI run 20261101-012419-d5b8677, 826 tests, 0 failed)

Method: fresh app/ renders vs figma/ pixel-checked at the points of review-shot-windows-2.md; many earlier FIX points are fixed (items.db, glyph cells, float colour, 2 ch/ frame, grass tile, Get Velocity). Counts: PASS 21, FAIL 15, OWNER 14 (lines grouped), HW 2.

| item | verdict | evidence / reason |
|---|---|---|
| P1.04 | PASS | test:ProjectModelTests.EveryFileFormatRoundTrips (round trip, "format": 1 and schema validation for every file; docs/schemas has 18 schemas) |
| P10.05 | PASS | test:DesignSystemTests.Art_AtAFractionalScaleHasNoSeamBetweenBands, shot:docs/evidence/diff/C-scene-card-default.png, shot:docs/evidence/diff/C-scene-card-selected.png (0.44 / 0.81 %); every other C: box ticked |
| P12.05 | PASS | test:PlayModeE2ETests.P12_05_Overlays_HitboxesAndTileCollision_DrawnOverTheGame, shot:docs/evidence/diff/T-P02-game-view.png (no outlines on hearts or flag any more) |
| C:Scene Card/matches | PASS | shot:docs/evidence/diff/C-scene-card-default.png, shot:docs/evidence/diff/C-scene-card-selected.png (seams gone, 0.44 / 0.81 %) |
| T-S08/implemented | PASS | test:PreferencesTests.Paths_ClearCacheEmptiesTheProjectsBuildCache, test:PreferenceEffectsTests.Paths_ProjectsFolderImageEditorTiled_AndBrowse (hint and per-project cache done, rows 1444 and 1472) |
| T-P02/matches | PASS | shot:docs/evidence/diff/T-P02.png (hitboxes on hearts and flag gone) |
| T-P04/matches | PASS | shot:docs/evidence/diff/T-P04.png (float colour fixed) |
| T-P07/matches | PASS | shot:docs/evidence/diff/T-P07.png (table, price, header match; rest in rows 1455 1487 1491) |
| T-P08/matches | PASS | shot:docs/evidence/diff/T-P08.png (list order and frame strip match) |
| T-P12/matches | PASS | shot:docs/evidence/diff/T-P12.png (Top-down card and thumbnails match) |
| T-P20/matches | PASS | shot:docs/evidence/diff/T-P20.png (Top-down card and detail preview match) |
| T-P21/matches | PASS | shot:docs/evidence/diff/T-P21.png (thumbnails seam-free) |
| T-P22/matches | PASS | shot:docs/evidence/diff/T-P22.png (folder icon 18x14 fixed in db463f2) |
| T-D01/matches | PASS | shot:docs/evidence/diff/T-D01.png (grass tile gone (0 px diff there); eight cards = row 1479) |
| T-L02/matches | PASS | shot:docs/evidence/diff/T-L02.png (same as T-D01 in Light) |
| T-D10/matches | PASS | shot:docs/evidence/diff/T-D10.png (only the footer link, row 1480) |
| T-D19/matches | PASS | shot:docs/evidence/diff/T-D19.png (warning text 12 px now) |
| T-D21/matches | PASS | shot:docs/evidence/diff/T-D21.png (portraits near Figma sizes; count = row 1475) |
| T-O05/matches | PASS | shot:docs/evidence/diff/T-O05.png (selected name colour now matches) |
| T-O08/matches | PASS | shot:docs/evidence/diff/T-O08.png (Get Velocity third result and colours match) |
| T-O11/matches | PASS | shot:docs/evidence/diff/T-O11.png (toggles aligned; count text is fixture content) |
| T-P01/matches | FAIL | no "SCORE" label in viewport; Inspector frames strip shows 6+ not 4; player.png card shows a picture where Figma has none |
| T-L01/matches | FAIL | same as T-P01 |
| T-P03/matches | FAIL | Tileset panel is a "Primary" dropdown + fixture art, not Figma plain 8-tile grid; camera colour/brush outline/VRAM column |
| T-P06/matches | FAIL | fixture idle frame 5 draws a blink (Figma identical frames) |
| T-P10/matches | FAIL | BG Ground 12/16 vs 9/16 and BG palette RAM contents differ from Figma; fade seams |
| T-P13/matches | FAIL | link chips (Start, Goal Flag, Exit trigger, 3 stars) sit 7-59 px from Figma |
| T-P14/matches | FAIL | string list from fixture (no "long"/"missing" rows), box widths |
| T-P15/matches | FAIL | list order, song data (2:38 vs 1:24), 5 channels, preview buttons |
| T-P17/matches | FAIL | Nodes palette lists On Late Update and On Touch not highlighted; inspector order; preview icon |
| T-P24/matches | FAIL | palette contents differ, no changed colour / "water cycle" state |
| T-P25/matches | FAIL | sound list order, jump.sfx graph drawn as steps, registers, bake size |
| T-D04/matches | FAIL | import dialog still says "OBJ 7" (Figma "OBJ 0 share with hero"); preview frames show different frames/size |
| T-O04/matches | FAIL | sprite order hero, slime, block, flag, coin, heart vs hero, slime, coin, block, heart, cloud |
| T-O07/matches | FAIL | player.png icon outlined, Figma filled square (no scope row) |
| P11.X1 | FAIL | section 4 boxes above still unticked (14 FAIL) and USER sign-off |
| T-P09/matches | OWNER | Font preview: Figma dusk scene with smooth 1x text vs app day scene with 2x pixel text. Accept the runtime-accurate preview (as rows 1495)? Rec: yes, record row |
| T-P16/matches | OWNER | "6 lines" status but 3 cards drawn in Figma. Show real count (3)? Rec: yes, record row |
| T-P23/matches | OWNER | Extra "Other 33 KB" legend item/grey segment (real EWRAM has other use). Keep? Rec: keep, record row |
| T-P26/matches | OWNER | Rule result tiles are the fixture tileset, not plain tiles. Accept? Rec: yes, record row |
| T-O01/matches | OWNER | Thumbnails: Figma shows scene art, app renders fixture scenes. Which is meant? Rec: keep real renders, record row |
| T-O10/matches | OWNER | Four preview frames in Figma are unrelated colours, app shows range colours. Rec: keep app, record row |
| T-D15/matches | OWNER | Smooth 12 px vs 2x pixel preview, caption colour/"about 6 KB". Rec: keep pixel preview (runtime-true), record row |
| T-K05 | OWNER | Art originality REV and T-O01 thumbnails are owner calls. Rec: approve after T-O01 decision |
| G-DOCS.04 | OWNER | REV at each phase exit; last REV was c407d74. Rec: REV again at P16 exit, then tick |
| P0.E1 | OWNER | Has building started? Rec: yes (it has) |
| P0.X1 | OWNER | Install list approved? Rec: yes |
| P1.E1..P16.E1 | OWNER | phase exit lines follow the X1 gates; tick when the previous X1 is ticked |
| P1.X1..P10.X1, P12.X1..P16.X1 | OWNER | USER sign-off gates; evidence is in earlier audits, owner must tick. Rec: sign after the FAIL list is fixed |
| P16.01 | OWNER | Fresh Windows account install, owner only. Rec: run once on a clean user |
| P2.10 | HW | RT and EMU per save type pass (RuntimeRomTests.OptionsBlockAndSlotSurviveAReloadOnEachSaveType x4); HW on one cart still needed |
| P16.06 | HW | needs HW boot and all of section 4 ticked |

## Scope-log rows
Row "T-D04, T-P10 palette banks": palettes.json is now written (f81da8b) and the Palette Manager shows OBJ 0 Hero, but T-D04 says OBJ 7 and T-P10 BG banks differ, so only partly done; listed in final-3-rows.txt for the caller to judge. Row 1442 (boot splash frames) is "pending: keep or remove", owner.
