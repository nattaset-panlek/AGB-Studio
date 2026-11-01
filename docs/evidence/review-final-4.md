# Final review 4 (HEAD 7b966cc, CI 3b5ed1e)

Method: diff PNGs in docs/evidence/diff viewed for T-P01, T-P03, T-P06, T-P13, T-D04, T-O04, T-P15, T-P17, T-P25; the rest rest on accepted scope-log rows (2026-11-01 "final review 3") and the shot-report percentages (all under 13 %).
Counts: PASS 50 (19 matches, 31 gates/T-K05), FAIL 6, HW 1, OWNER 3.

| item | verdict | evidence |
|---|---|---|
| T-P13/matches | PASS | chips Start, Goal Flag, Exit trigger, 3 stars within ~4 px of Figma (diff/T-P13.png); camera 100 vs 85 % is runtime state |
| T-P06/matches | PASS | eight identical idle frames (diff/T-P06.png); Console badge 1 vs 6 is runtime data |
| T-D04/matches | PASS | dialog says "OBJ 0 share with hero", frames match (diff/T-D04.png) |
| T-O04/matches | PASS | order hero, slime, coin, block, heart, cloud (diff/T-O04.png) |
| T-P15, T-P17, T-P25 /matches | PASS | list order, highlight on On Touch, smooth graph; the rest are accepted rows |
| T-P03, T-P10, T-P24 /matches | PASS | remaining diffs (Primary field, palette contents, tile ids) covered by the palettes row; camera box and brush outline close enough |
| T-P09, T-P14, T-P16, T-P23, T-P26, T-O01, T-O07, T-O10, T-D15 /matches | PASS | only differences are the accepted "keep the real data" rows |
| T-P01/matches | FAIL | viewport shows large "SCORE" without "0120" where Figma has small "SCORE 0120"; Hierarchy has a "HUD" group (4) where Figma has "HUD - Score" text; Assets grid shows two rows (12 items) vs Figma one row; Player Y 288 vs 112. No accepted row covers these |
| T-L01/matches | FAIL | same as T-P01 in Light |
| P0.E1, P0.X1 | PASS | all P0 items ticked; owner said building may start |
| P1..P10 E1/X1, P11.E1, P12..P14 E1/X1, P15.X1 | PASS | every non-gate item of the phase is ticked (P2.10 is HW only, allowed "n/a until hardware"); owner answer 2026-11-01 |
| T-K05 | PASS | other T-K items ticked; owner answer 2026-11-01; thumbnails decision T-O01 recorded |
| P2.10 | HW | RT/EMU pass for all save types; cart test needs hardware |
| P11.X1 | FAIL | T-P01 and T-L01 matches boxes will remain unticked |
| P15.E1 | FAIL | requires the Phase 11 exit (P11.X1) |
| P16.E1 | FAIL | requires P11.X1, P15.E1 and G-DOCS.04 |
| G-DOCS.04 | FAIL | see below |
| P16.01 | OWNER | fresh-account install |
| P16.06 | OWNER | release candidate (needs P11.X1 and all gates) |
| P16.X1 | OWNER | release approval, blocked by P16.01 and P16.06 |

## G-DOCS.04 REV at 7b966cc
- docs/user/windows/T-S02-project-settings-save-data.md covers the 8 B options block (current).
- docs/user/windows/T-P03-tilemap-editor.md still says the Tileset panel has a "Set" dropdown (line 25); the app (TilemapEditorPage.cs line 170) has a "Primary" field. The words "Primary tileset" appear nowhere in docs/user. Stale.
- Dev docs ARCHITECTURE, FILE-FORMATS, BUILD, DECISIONS, TOOLCHAIN, THIRD_PARTY last changed 2026-10-31; neither FILE-FORMATS.md nor ARCHITECTURE.md mentions the options block (committed 2026-11-01, 743dc03). Stale.
- Fixed gestures: not found documented in docs/user (grep "gesture" empty); not verified further.
Fix: update T-P03 page and FILE-FORMATS/ARCHITECTURE, then re-run this REV.
