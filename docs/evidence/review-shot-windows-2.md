# Review 2: SHOT "matches Figma" for windows (2026-10-31)

Second independent read-only review of the `- [ ] matches Figma` boxes still unticked under the window headings of
`docs/ENGINE-CHECKLIST.md` sections 4.1, 4.2, 4.3, 4.4, 4.6, 4.7, 4.8 and 4.10 (66 windows), plus the SHOT part of
T-K05 (art) and P12.05 (game view, row `T-P02-game-view`). Snapshot: worktree `review2`, detached at `7487e78` (full CI
passed on this code). Nothing was built, run or exported: the images are the ones in the repo
(`docs/evidence/figma/<id>.png`, `docs/evidence/app/<id>.png`, `docs/evidence/diff/<id>.png`, numbers in
`docs/evidence/shot-report.md`). The first review (`docs/evidence/review-shot-windows.md`, at `c407d74`) was re-checked
item by item.

Method (SHOT procedure, section 2.5): every window was looked at side by side (Figma left or above, app right or below)
at 1x, and in zoomed crops wherever the diff image showed red. A script grouped the differing pixels into clusters and
searched, per cluster, the shift (up to 8 px) that best aligns Figma and app, to find anything moved by more than 2 px.
Text colours were sampled (peak ink colour) and compared with the token values; text and icon extents were measured
on the PNGs. The art SVGs in `editor/AgbStudio.Editor/Assets/Art` were rendered independently (PyMuPDF) and compared
with both images.

Treated as noise: text anti-aliasing and the heavier Skia text; a text caret (Figma draws none, or draws it at
another place); live data the window shows by design: the running game picture (T-P02 game view, T-P17 Preview), frame
counter, fps / CPU / vblank, positions and velocities, console log lines and their count, VRAM / OAM / palette RAM /
memory contents, profiler bars, GBA budget meters and the budget tip derived from them, build progress, flash
progress. All coordinates below are pixels in the frame (x from the left, y from the top).

Decisions:

- **MATCH**: within the 2 px tolerance; what is left is noise. Tick; line in `docs/evidence/evidence-shot-windows-2.tsv`.
- **USER**: every remaining real difference is covered by a pending Scope-log row (section 8, line numbers at
  `7487e78`) or a pending decision; the user decides.
- **FIX**: at least one real difference that no pending row covers. "Fix" can be a code or fixture change, or (where
  the Figma frame contradicts itself or the app is arguably right) a new scope-log row; each item says which.

## Counts

| Decision | Windows |
|---|---|
| MATCH | 17: T-P19, T-D03, T-D05, T-D06, T-D08, T-S03, T-S04, T-S06, T-S09, T-O02, T-O03, T-O06, T-D17, T-D18, T-O09, T-O12, T-L03 |
| USER | 11: T-P05, T-P18, T-D02, T-D07, T-D12, T-S02, T-S07, T-S08, T-D14, T-D16, T-D24 |
| FIX | 38: T-P01, T-P02, T-P03, T-P04, T-P06, T-P07, T-P08, T-P09, T-P10, T-P11, T-P12, T-P13, T-P14, T-P15, T-P16, T-P17, T-P20, T-P21, T-P22, T-P23, T-P24, T-P25, T-P26, T-D01, T-D04, T-D10, T-D15, T-D19, T-D21, T-O01, T-O04, T-O05, T-O07, T-O08, T-O10, T-O11, T-L01, T-L02 |
| Plain items | T-K05 (SHOT part): FIX; P12.05: FIX |

Per section: 4.1 pages 1 / 2 / 16 (MATCH / USER / FIX), 4.2 dialogs 4 / 3 / 3, 4.3 settings 4 / 3 / 0, 4.4 popovers
3 / 0 / 4, 4.6 extra pages 0 / 0 / 7, 4.7 extra dialogs 2 / 3 / 3, 4.8 extra popovers 2 / 0 / 3, 4.10 light 1 / 0 / 2.

## All windows

| Id | Decision | Reason | Pending rows (USER part) |
|---|---|---|---|
| T-P01 Scene Editor | FIX | Brick platform 24 px left; no "SCORE" label in the viewport; Inspector frames strip and palette from the fixture; Assets counts, kinds and an empty player.png card | 1473, 1474, 1475, 1481 |
| T-P02 Play Mode | FIX | Hitbox outlines on the three HUD hearts and the Goal Flag that Figma does not draw (game picture and live values are noise) | 1474 |
| T-P03 Tilemap Editor | FIX | Tileset panel art, camera frame colour, brush preview outline, brick platform 24 px left with a one-way band, VRAM column values | 1466, 1473, 1476 |
| T-P04 Script Editor | FIX | `float` on line 8 in the keyword colour; scrollbar in the Scripts list | 1482 |
| T-P05 Console panel | USER | Only "Errors · 1" vs "Errors · 0" remains | 1470 |
| T-P06 Animation panel | FIX | Frame 1 selected (Figma frame 3); hero drawn 36 x 36 vs 31 x 33; a blink frame in the fixture animation | - |
| T-P07 Database | FIX | Doc tab and breadcrumb say "Items.db"; Heart price 30 vs 0; table scrollbar; script box 3 px taller | 1455, 1479, 1483 |
| T-P08 Sprite Editor | FIX | Sprites list order; Layers toggle on; frame strip sprites 24 x 26 vs 31 x 33; speed field 20 px wider | 1467, 1477, 1479 |
| T-P09 Font Manager | FIX | Glyph cells cut the lower parts of 0E0D, 0E0E, 0E0F, 0E10; Figma code labels wrong in row 3; "NotoSansThai.ttf"; "Place lower vowels ()"; preview box | 1478 |
| T-P10 Palette Manager | FIX | Only the 1 px seams in the fade previews remain unrecorded | 1467, 1477, 1484 |
| T-P11 Build Report | FIX | Largest assets row says "Items.db" | 1485 |
| T-P12 Launcher | FIX | Top-down Adventure art draws a grass tile at the path crossing; seams in the project thumbnails | 1471, 1480 |
| T-P13 World Map | FIX | Link chips placed 7 to 59 px away from Figma's places; seams in the scene cards | 1486 |
| T-P14 Localization | FIX | String list from the fixture (no "long" / "missing" row in view), table scrollbar, Dialogue 111, box widths 184 / 154 | 1487 |
| T-P15 Audio | FIX | Lists sorted by name; fixture song data, 5 mixer channels, 5 preview buttons | 1456, 1488 |
| T-P16 Dialogue Editor | FIX | forest_intro 3 vs 6; "2 ch/fr" vs wrapped "2 ch/ frame" | 1487 |
| T-P17 Logic Graph | FIX | Set Velocity Y runs into Play Sound; On Touch Tag 26 px low; node palette, inspector, minimap and Preview icon differ | 1448 |
| T-P18 Scripting API | USER | Only the See also row and "Docs v1.0" remain | 1449, 1471 |
| T-P19 Debugger | MATCH | Toolbar and undo / redo fixed; VRAM, OAM, profiler and memory are live | - |
| T-P20 Launcher · Templates | FIX | Top-down grass tile; grid scrollbar; seams in the detail preview | 1471, 1480 |
| T-P21 Launcher · Learn | FIX | Seams in the sample project thumbnails | 1466, 1471, 1480 |
| T-P22 Empty project | FIX | "No assets yet" folder icon 16 x 12 vs 18 x 14 (bottom 3 px higher) | 1466 (1443, 1458 have no visible part) |
| T-P23 Debugger · Memory | FIX | Extra "Other · 33 KB" legend item and grey segment; Add watch 4 px low | - |
| T-P24 Debugger · Palettes | FIX | Captured state: no changed colour (index 7 black, "Not changed while playing", no "(water cycle)") | 1477 (palette layout) |
| T-P25 SFX Designer | FIX | Sound list sorted by name; jump.sfx parameters, graph, registers and bake size from the fixture | 1479 |
| T-P26 Auto-tile Rules panel | FIX | Rule result tiles drawn from the fixture tileset (textured, bricks) instead of Figma's plain tiles | 1476 |
| T-D01 New Project | FIX | Top-down Adventure art: grass tile at the path crossing (x 400-424, y 204-224) | 8 cards: editor-gaps.md "Other differences", D-09 |
| T-D02 New Scene | USER | Only Forest_03 / 30 vs Forest_02 / 32 remains | 1468 |
| T-D03 Scene Settings | MATCH | Text inset and BG2 slider fixed; whole body 1 px higher | - |
| T-D04 Import Asset | FIX | Preview frames 24 x 26 vs 36 x 39; file sizes 0.2 / 3.6 KB vs 2.1 / 14 KB | 1466, 1477 |
| T-D05 Export ROM | MATCH | sky_hopper.gba, focus, size row and meter fixed | - |
| T-D06 Build ROM | MATCH | sky_hopper.gba fixed; bar at 9 / 12 is live progress | - |
| T-D07 Flash to Cartridge | USER | Only the greyed Flash button remains (title line 2 px higher, within tolerance) | D-12; editor-gaps.md "Accepted by decision" |
| T-D08 Project Settings (Input) | MATCH | Columns at Figma's x | 1462 has no visible part |
| T-D10 Keyboard Shortcuts | FIX | Footer link "Edit in Preferences › Shortcuts" not in the frame; no pending row any more | - |
| T-D12 Unsaved Changes | USER | Only the black file icons in Figma remain | 1466 |
| T-S02 Save data | USER | Only byte / short vs int remains | 1443, 1457 |
| T-S03 Build | MATCH | Segment and toolchain fixed; Change in Preferences button 2 px left (within) | - |
| T-S04 Scripting | MATCH | Catches texts and chevrons fixed | - |
| T-S06 Appearance | MATCH | Preview blue and System split fixed | - |
| T-S07 Editor | USER | Only the clipped "JetBrains Mo" remains (lower section 2 px low, within) | 1469 |
| T-S08 Paths | USER | Only the per-project build cache path and the black icon remain | 1472, 1466 (1444: no visible part) |
| T-S09 Shortcuts | MATCH | Pencils fixed | - |
| T-O01 Scene Switcher | FIX | Title, Forest_02, Cave_01 and Summit thumbnails are fixture renders, not Figma's scene art | - |
| T-O02 Video Mode | MATCH | Descriptions in text/muted | - |
| T-O03 Grid & Snap | MATCH | "Camera bounds" and swatches fixed | - |
| T-O04 Asset Picker | FIX | Sprite order hero, slime, block, flag, coin, heart vs hero, slime, coin, block, heart, cloud | 1475 |
| T-O05 Component Picker | FIX | Selected name "Patrol" in text/on-selected; Figma text/primary | - |
| T-O06 Budget Details | MATCH | Captured after a build; numbers, bars and tip are the live budget | - |
| T-O07 Command Palette | FIX | player.png icon drawn outlined; Figma draws it filled (no row names it) | 1466, 1480 |
| T-O08 Add Node | FIX | Third result "Set Velocity" instead of "Get Velocity · read vx, vy"; selected name colour | - |
| T-O09 Link Settings | MATCH | Ligature fixed; dropdown texts 2 px right (within) | - |
| T-O10 Palette Effect | FIX | Preview strip shows four blues; Figma four different frames | - |
| T-O11 Find & Replace | FIX | Aa toggle 9 px right, ab 5 px right, match count 9 px right | - |
| T-O12 Record Shortcut | MATCH | 11 px warning | - |
| T-D14 New Script | USER | Only the black Static helper / Scene script icons remain | 1466 |
| T-D15 New Font | FIX | Preview sample drawn as 2x pixel glyphs; caption in text/muted, "about 4 KB" | - |
| T-D16 Add Characters | USER | Only 2 vs 7 missing remains | 1454 |
| T-D17 New Table | MATCH | EnemyId.Bat | - |
| T-D18 Add Field | MATCH | Chip gaps fixed | - |
| T-D19 Import CSV | FIX | Warning text 13 px instead of 12 px | 1466 |
| T-D21 New Speaker | FIX | Portrait sprites drawn smaller (block 32 x 30 vs 40 x 38, hero 24 x 26 vs 31 x 33, coin 16 vs 28) | 1475 |
| T-D24 Import Asset · Errors | USER | Only "Split into 4 palettes" remains | 1453 |
| T-L01 Scene Editor · Light | FIX | As T-P01 | as T-P01 |
| T-L02 New Project · Light | FIX | As T-D01 | as T-D01 |
| T-L03 Appearance · Light | MATCH | As T-S06 | - |

## USER: pending rows, grouped

| Row (line at 7487e78) | Windows whose only remaining differences these are |
|---|---|
| 1466 black icons in Screens frames | T-D12 (file rows), T-D14 (Static helper, Scene script), T-S08 (Build cache, with 1472) |
| 1468 T-D02 New Scene name / width | T-D02 |
| 1470 T-P05 error count | T-P05 |
| 1469 T-S07 clipped font name | T-S07 |
| 1472 T-S08 per-project build cache | T-S08 (with 1466) |
| 1443 and 1457 T-S02 types | T-S02 |
| 1454 T-D16 2 vs 7 missing | T-D16 |
| 1453 T-D24 split into 4 palettes | T-D24 |
| 1449 G-SCOPE.02 See also row, and 1471 version 0.1 / 1.0 | T-P18 |
| D-12 (section 3, not ticked) and editor-gaps.md "Accepted by decision" T-D07 | T-D07 (Flash greyed for GBxCart RW) |

Pending rows that also cover part of FIX windows (they still wait for the user): 1448 (T-P17), 1453, 1455 (T-P07), 1456
(T-P15), 1466 (T-P03, T-P21, T-P22, T-D04, T-D19, T-O07), 1467 (T-P08, T-P10), 1471 (T-P12, T-P20, T-P21), 1473 to 1477
(fixture rows: T-P01 / T-L01, T-P02, T-P03, T-P08, T-P10, T-P24, T-P26, T-D04, T-O04, T-D21), 1478 (T-P09), 1479
(T-P07, T-P08, T-P25), 1480 (T-P12, T-P20, T-P21, T-O07), 1481 (T-P01), 1482 (T-P04), 1483 (T-P07), 1484 (T-P10), 1485
(T-P11), 1486 (T-P13), 1487 (T-P14, T-P16), 1488 (T-P15); D-09 and editor-gaps.md "Other differences" (8 template
cards in T-D01 / T-L02).

## Differences that repeat across windows

| # | Difference | Windows | Kind |
|---|---|---|---|
| A | `Graphics/SvgImage.cs` builds every path with Avalonia `Geometry.Parse`, which fills even-odd; SVG's default fill rule is nonzero. Where a Figma path has overlapping sub-paths the overlap is left empty. Visible: art-genre-topdown (`M0 92H240V114H0V92ZM108 0H132V160H108V0Z`, the path cross) shows the grass under the hero instead of path. Latent in art-genre-rpg (#E9EDF2, 80 px² hidden in current renders), art-tileset-forest and thumb-tileset (#1B1630, 6-8 px²). No icon is affected. | T-D01, T-L02, T-P12, T-P20 (and T-K05) | code |
| B | Art drawn at a non-integer scale shows 1 px dark seams where two bands of the art meet (anti-aliased edges of adjacent shapes over the dark card), e.g. T-P10 fade preview at y 294 (#77A5C5 between #7CC5FF and #A6DBFF; Figma has no line) | T-P10 (fade previews), T-P12 (recent project thumbnails, Sky Hopper y 168 and 187), T-P13 (scene cards), T-P20 (detail preview y 158 and 193), T-P21 (sample thumbnails) (and T-K05) | code |
| C | Sprite pictures drawn at another scale than the Figma frame (the app uses integer scales, Figma the art at about 1.5x to 2.6x) | T-D04, T-D21, T-P06, T-P08 (strip) | code or record |
| D | Selected list row: the name is drawn in text/on-selected #CFE0FF; Figma uses text/primary #E7E8EB for the name and #CFE0FF for the hint (as T-O01 was fixed) | T-O05, T-O08 | code |
| E | List order not Figma's: sorted by name or by file creation time (f661cca notes the creation time is not stable between fixture runs) | T-P08, T-O04, T-P15, T-P25, T-P14 | code or fixture |
| F | Scrollbar shown where Figma has none | T-P04 (Scripts, x 255-256, y 130-226), T-P07 (table, x 1116-1117, y 206-301), T-P14 (table, x 1042-1043, y 354-375), T-P20 (template grid, x 1116-1117, y 64-840) | code |
| G | Brick platform of Forest_01 one tile (24 px at 300 %) left of Figma: x 570-714 vs 594-738 (y 384-417 same); T-P03 also draws a blue one-way band above it (y 370-381) | T-P01, T-L01, T-P03 (and the T-P02 game picture) | fixture |
| H | Fixture data that differs from the frame with no row: counts, names, sizes, song and sound data, string keys (details per window below) | T-P01, T-P03, T-P07, T-P11, T-P14, T-P15, T-P16, T-P24, T-P25, T-P26, T-D04, T-O01 | fixture or record |

## FIX list

**T-P01 Scene Editor** (same for **T-L01**)
- Viewport: brick platform x 570-714, Figma 594-738 (G).
- Viewport: Figma draws "SCORE 0120" at x 442-550, y 145-160; the app draws nothing there. Row 1474 says the editor
  viewport shows the label "SCORE"; the capture shows no label at all, so the row's premise does not hold.
- Inspector › Sprite › Frames: six and a clipped seventh thumbnail run to the panel edge (x 1232-1440, y 357-389);
  Figma four thumbnails (x 503-651). Palette: 7 colours vs 10.
- Assets: category counts Sprites 13 / Tilesets 5 / Maps 5 / Fonts 3 / Scripts 12 / Data 6 vs 12 / 3 / 6 / 2 / 7 / 4
  (row 1475 says the fixture has 12 sprites; the app counts 13). The grid shows sprites only (hero, slime, block, flag,
  coin, heart, owl, bat, slime_king, cloud, gem, player); Figma's "Sprites" grid shows hero, slime, coin,
  forest_tiles.png, block.png, pixel8.font (Figma mixes kinds: record). The player.png card (x 1002-1104, y 778-840)
  has no picture.
- Recorded and still pending: 1473 (position), 1474 (HUD folder), 1475 (scrolling grid), 1481 (speed slider).

**T-P02 Play Mode**
- Hitboxes overlay: orange outlines around the three HUD hearts (x 343-422, y 145-167) and a blue outline around the
  Goal Flag (x 1002-1050, y 370-460); Figma outlines only the player (green) and the slime (red). Either draw hitboxes
  only for the actors Figma shows, or record (the hearts are actors, row 1474).
- Not counted: game picture (player mid-jump, slime gone, "SCORE 0000" in the ROM's HUD font, bricks as G), Inspector
  live values, console lines, budget.

**T-P03 Tilemap Editor**
- Tileset panel (x 10-249, y 154-272): Figma shows the art/tileset forest tiles (8 plain tiles with a grid); the app
  shows the fixture's forest_tiles.png (empty tile 0, textured grass / dirt, brick, a water tile, no red / white tile).
  The empty first cell is row 1476; the tile pictures are not.
- Camera bounds: app frame 2 px wide in status/warning #FFB020 at x 330-331 and a #FFB020 "Camera 240×160" chip; Figma
  a 1 px #FFD84A line at x 331 / y 130 with the sky outside it and a #FFD84A chip. #FFD84A is not a token: code or
  record.
- Brush preview (x 906-1002, y 322-368): Figma 2 px white outline; app a faint #CFE0FF-like outline (207, 224, 255)
  and a slightly stronger fill.
- Brick platform G, plus a blue one-way band y 370-381 above it.
- Layers table VRAM column: BG0 "char block 0 · screen 30" vs "char block 1 · screen 30", BG1 "char block 0 · screen
  28" vs "char block 1 · screen 29", BG2 "screen 26" vs "screen 28", BG3 "char block 0 · screen 24" vs "empty" (H).
- Recorded and pending: 1466 (eye icons), 1473 (tile 12, 14), 1476 (#1, 58 solid, rule set (5), id 0 empty).

**T-P04 Script Editor**
- Line 8: `float` drawn in the keyword pink; Figma draws it in text/primary under the squiggle (x 352-392, y 296-308);
  the app's squiggle sits 2 px higher.
- Scripts list scrollbar (F).
- Recorded and pending: 1482 (breadcrumb, Text.Show).

**T-P06 Animation panel**
- Frame 1 selected (blue border, bold label); Figma frame 3.
- Hero drawn 36 x 36 (x 276-311 in frame 2) vs 31 x 33 (C); frame 5 of the fixture's idle shows other eyes (H).

**T-P07 Database**
- Doc tab and title-bar breadcrumb say "Items.db"; Figma "items.db" (also T-P11).
- Table row 2 (Heart) price 30; Figma 0 (H).
- Table scrollbar (F).
- "USE IT IN A SCRIPT" box ends at y 570; Figma 567 (3 px taller).
- Recorded and pending: 1455 (ROM 528 B), 1479 (dirty dot), 1483 (knob, 9 fields incl. "description").

**T-P08 Sprite Editor**
- Sprites list order hero, coin, slime, block, cloud, heart vs hero, slime, coin, block, heart, cloud (E).
- Layers toggle (toolbar x ≈ 279, y 57) shown active; Figma off.
- Animation strip: sprites 24 x 26 (x 541-564, y 754-779) vs 31 x 33 (x 538-568, y 750-782) (C).
- Speed field box x 656-780; Figma 655-760 with the text running past the box (record, as row 1469, or match).
- Recorded and pending: 1467 (Colour 4), 1477 (palette slots colours), 1479 (dirty dot).

**T-P09 Font Manager**
- Glyph cells cut off the parts below the baseline: 0E0D ญ, 0E0E ฎ, 0E0F ฏ and 0E10 ฐ (cells x 876-1070, y 160-195)
  show ณ-, ภ-, ภ- and จ-like shapes; Figma draws the whole glyphs. Code.
- Row 3: Figma labels ล ว ศ ษ ส ห as 0E24-0E29 and ฝ อ ฮ as 0E2A-0E2C (wrong code points); the app shows the right ones
  (0E25, 0E27, 0E28, 0E29, 0E2A, 0E2B; 0E2C ฬ, 0E2D, 0E2E). Figma error: new scope-log row.
- Source › From: "NotoSansThai-Regular.ttf"; this frame says "NotoSansThai.ttf" while T-D15 / T-D16 say
  "NotoSansThai-Regular.ttf" (Figma conflict: record).
- "Place lower vowels ()" without the mark; Figma "(ฺ)".
- Preview: Figma a dusk scene with a 1x dialogue box (two smooth lines, ▼, x 485-720); the app a day scene with a 2x
  pixel-text box (three lines, x 1208-1440). No row (row 1487 covers T-P14 / T-P16 only).
- Recorded and pending: 1478 (missing glyphs, advance 7, "16×16 fixed", glyph counts).

**T-P10 Palette Manager**
- Seams in the fade previews (B). Everything else is covered: 1467, 1477, 1484.

**T-P11 Build Report**
- Largest assets row "Items.db" (x 30-79, y 495); Figma "items.db". Rest covered by 1485.

**T-P12 Launcher**
- Top-down Adventure template card: grass tile at the path crossing under the hero (A).
- Recent project thumbnails with seams (B).
- Recorded and pending: 1471 (v1.0), 1480 (Templates icon).

**T-P13 World Map**
- Link chips: "Start" at y 227-249 vs 271-293 (44 px higher); "Goal Flag" at y 359-381 vs 300-323 (59 px lower);
  "Exit trigger" at y 454-476 vs 499-521 (45 px higher); "3 stars" at y 607-629 vs 600-623 (7 px lower); the curves
  are routed so the chips sit elsewhere on them.
- Scene card thumbnails with seams (B).
- Recorded and pending: 1486 (zoom 100 %).

**T-P14 Localization**
- String list: the app lists dlg.forest.owl_01, hud.score, item.extra.000..011 (all "ok"); Figma ui.menu.start /
  continue / options, item.potion.name, item.potion.desc ("long"), shop.welcome ("missing"), boss.summit.intro. The
  "long" and "missing" states are not in view (H or E).
- Table scrollbar (F). Groups: Dialogue 111 vs 112.
- Box width 184 / 224 and 154 / 224 vs 198 and 176 (measured with the real font: record, as row 1478).
- Recorded and pending: 1487 (preview box, one line, caption).

**T-P15 Audio**
- Music list bgm_cave, bgm_forest, bgm_title, jingle_win vs bgm_forest, bgm_cave, bgm_title, jingle_win; sounds
  blip, coin, explode, hurt, jump vs jump, coin, hurt, explode, blip (E).
- Song data: 2:38 vs 1:24, 21 vs 12 patterns, loop 4-20 vs 4-11, flat waveform, other pattern notes, 5 mixer channels
  (Arp) vs 4, other dB values, durations 0:20 / 1:36 / 0:13 vs 1:02 / 0:48 / 0:06, GBA channel labels "SFX jump +1",
  "SFX blip +1" (H).
- Preview SFX: five buttons on two rows (blip, coin, explode, hurt, jump) vs three (jump, coin, hurt).
- Recorded and pending: 1456 (mixer CPU), 1488 (tracker pitch).

**T-P16 Dialogue Editor**
- forest_intro count 3 and status "3 lines · 2 choices"; Figma 6 and "6 lines · 2 choices" while it also draws three
  line cards (record or fixture).
- Line inspector Text speed value "2 ch/fr" on one line at the right end of the row (x ≈ 1383-1427, y ≈ 440);
  Figma wraps "2 ch/" and "frame" on two lines (y 425-449).
- Recorded and pending: 1487 (preview, Flags.MetOwl).

**T-P17 Logic Graph**
- Set Velocity Y node has Mode and Target rows and runs from y 137 down into Play Sound (header y 276); Figma ends at
  233.
- On Touch: Tag row at y 576; Figma 550 (26 px lower).
- Nodes palette: app lists On Late Update, On Destroy, Is On Ground, Velocity X / Y, Set Velocity X / Y, Set Velocity,
  Add Force, Get Velocity, Jump and an "Other" group, On Touch not highlighted; Figma On Start, On Update, On Touch
  (selected), On Button; Physics: Set Velocity, Add Force, Is On Ground.
- Inspector: Value plus a Mode segment and a Target dropdown below; Figma Target dropdown then Value.
- Preview header icon is an eye; Figma a gamepad. Minimap layout differs.
- Recorded and pending: 1448 (generated guard).

**T-P20 Launcher · Templates**
- Top-down card grass tile (A); template grid scrollbar (F); detail preview seams (B).
- Recorded and pending: 1471, 1480 (1447 has no visible part with Platformer selected).

**T-P21 Launcher · Learn**
- Sample project thumbnails with seams (B). Recorded and pending: 1466, 1471, 1480.

**T-P22 Scene Editor · Empty project**
- Bottom panel "No assets yet" folder icon ink 16 x 12 at x 682-697, y 717-728; Figma 18 x 14 at x 681-698, y 718-731
  (a smaller icon; bottom 3 px higher). Recorded and pending: 1466 (map icon).

**T-P23 Debugger · Memory**
- EWRAM bar and legend add an "Other · 33 KB" item and grey segment; Figma's legend has Actor pool, Tilemap buffers,
  Audio mixer, Strings and Free only (code or record).
- "Add watch" at y 378-387; Figma 374-383 (4 px low).

**T-P24 Debugger · Palettes**
- Captured state: BG 1 index 7 is `0x0000` (black), "Not changed while playing", no white outlines, subtitle without
  "(water cycle)"; Figma a changed water colour `0x7289` with white outlines on 6-9 and "Changed 3 frames ago" (capture
  while the cycle runs, or make the fixture's BG 1 cycle run). Palette layout: row 1477.

**T-P25 SFX Designer**
- Sound list blip, coin, hurt, jump, laser vs jump, coin, hurt, blip, laser (E).
- jump.sfx: Sweep up 5 vs 2, graph drawn as steps (pitch flat, volume step at x ≈ 765) vs Figma's curves, registers
  0x0033 / 0xF28A / 0xC706 vs 0x0023 / 0xF280 / 0xC6D6, "Bake: 2 KB" vs "9 KB" (H).
- Recorded and pending: 1479 (dirty dot).

**T-P26 Auto-tile Rules panel**
- Rule result tiles (x 95-138, y 54-100 and the four others) are fixture tiles (grass with dots, dirt, a brick tile for
  Right edge, dotted greens); Figma plain tiles with a dark edge (H). Recorded and pending: 1476 ("(5)").

**T-D01 New Project** (same for **T-L02**)
- Top-down Adventure art: grass tile at x 400-424, y 204-224 (A).
- Recorded: eight cards in a scrolling grid (editor-gaps.md "Other differences", D-09 pending); not a scope-log row.

**T-D04 Import Asset**
- Preview frames drawn 24 x 26 at x 630 / 684 / 738 / 792, y 127-152; Figma 36 x 39 at x 624 / 678 / 732 / 786, y
  121-159 (C).
- Sizes 0.2 KB and 3.6 KB vs 2.1 KB and 14 KB (H).
- Recorded and pending: 1466 (upload icon), 1477 ("OBJ 7").

**T-D10 Keyboard Shortcuts**
- Footer link "Edit in Preferences › Shortcuts" (x 25-218, y 352-364) is not in the frame. The Figma caption says the
  shortcuts are editable in Preferences › Shortcuts; scope-log line 1443 no longer names T-D10, so no pending row
  covers the link. New row or remove the link.

**T-D15 New Font**
- Preview sample drawn as 2x pixel glyphs in #E6E8EE, x 33-280, y 371-390; Figma smooth 12 px white text, x 34-197,
  y 377-390.
- Caption "12 px · 2bpp · about 4 KB of ROM" in text/muted #6F727B; Figma text/secondary #A3A6AE and "about 6 KB".

**T-D19 Import CSV**
- Warning text drawn at 13 px: x 54-351, y 373-383; Figma 12 px: x 54-326, y 375-384. Recorded: 1466 (file icon).

**T-D21 New Speaker**
- Portrait sprites at integer scale: block 32 x 30, hero 24 x 26, coin 16 x 16; Figma block 40 x 38, hero 31 x 33,
  coin 28 x 28 (C). The selected portrait is the fixture's bat sprite; Figma selects the slime picture (row 1475 covers
  the count only).

**T-O01 Scene Switcher**
- Thumbnails (x 34-130, y 174-430): Title, Forest_02, Cave_01 and Summit are renders of the fixture scenes (plain sky,
  red bars, peach); Figma shows the scene art (dusk, day, cave, snow) that T-P12, T-P13 and T-P21 use. Decide which
  is meant and record it, or render the scenes from the art.

**T-O04 Asset Picker**
- Order hero, slime, block, flag, coin, heart vs hero, slime, coin, block, heart, cloud (E). The third row is row 1475.

**T-O05 Component Picker**
- Selected row: "Patrol" (x 19-51, y 316-324) in #CFE0FF; Figma #E7E8EB (D).

**T-O07 Command Palette**
- player.png icon (x 18-30, y 205-217) drawn as the outlined image glyph; Figma a filled square. Rows 1463 / 1480
  describe the same filled-glyph artefact but name only the menus and the Templates glyph: extend a row to this icon.
  Recorded: 1466 (search icon), 1480 (Platform_tiles.png).

**T-O08 Add Node**
- Third PHYSICS result "Set Velocity" with no hint; Figma "Get Velocity · read vx, vy" (the node exists in
  `LogicGraph.cs`; the ranking differs).
- Selected row name "Set Velocity X" in #CFE0FF and 2 px left (x 17); Figma #E7E8EB at x 19 (D).

**T-O10 Palette Effect**
- Preview strip (x 21-356, y 291-351): four blues (the range colours); Figma four different frames (green, yellow,
  light blue with a stripe, red with a white top). Code, or record that Figma's frames are not the range colours.

**T-O11 Find & Replace**
- "Aa" toggle x 316-350 vs 307-341 (9 px right); "ab" 5 px right; ".*" 4 px right; the match count 9 px right (x 280
  vs 271). The toggle group is 9 px narrower than Figma's with the same right edge.

## T-K05 (SHOT part) and P12.05

**T-K05** art and thumbnails: FIX. The art is drawn from the Figma SVGs, but (A) the even-odd fill of `SvgImage` shows
the wrong picture for art/genre/topdown in T-D01, T-L02, T-P12 and T-P20 (an independent render of
`art-genre-topdown.svg` matches the Figma frame to 64 px, the app differs by 461 px), and (B) scaled art has seams in
T-P10, T-P12, T-P13, T-P20 and T-P21. Where the art is drawn 1:1 it matches (Platformer, RPG, Shoot 'em up, Puzzle,
Visual Novel cards in T-D01; T-O13 was ticked earlier). Evidence: `docs/evidence/diff/T-D01.png`, `T-P12.png`,
`T-P20.png`, `T-P10.png`, `T-P21.png`.

**P12.05** overlays over the game view (`docs/evidence/diff/T-P02-game-view.png`, 4.01 %, offset 0 px): FIX. The
velocity arrow is drawn now and the player / slime hitboxes look like Figma's, but (1) the capture turns Tile
collision on (blue outlines on the ground, the brick row and its one-way band) while the Figma frame has Tile
collision off, so the tile overlay is compared with nothing: capture in Figma's state, or record; (2) hitbox outlines
appear on the three HUD hearts (orange) and the Goal Flag (blue), which Figma does not draw. Noise: player mid-jump,
slime off screen, vblank 4.8 ms, and the ROM's own HUD text "SCORE 0000" (a larger dark pixel font where Figma draws
small white "SCORE 0120"; listed for the user, not counted).

## First-review items checked again

Fixed since `c407d74` (seen in the images): R1 text inset, R3 sky_hopper.gba, R4 undo / redo, R5 toolbar groups, R6
inspector drift, R8 is now row 1479, R9 / R10 / R13 are rows 1471 / 1480 / 1467, R12 text/muted; T-D03 slider, T-D05,
T-D06, T-D07 clip, T-D08 columns, T-D10 scrollbar, T-D14 check mark, T-D17 hint, T-D18 chips, T-D19 wording and link,
T-D21 focus, T-D24 cards, T-S02 / T-S03 / T-S04 / T-S06 / T-S08 / T-S09, T-O01 name colour, T-O02, T-O03, T-O05
headers, T-O06, T-O08 footer and MATH, T-O09, T-O12, T-P06 Play / Step, T-P07 icons, T-P09 grid offset, T-P11
spacing, T-P13 dot grid, T-P14 counts style, T-P15 Audio ROM, T-P18 descriptions and "// walk", T-P23 / T-P24
Breakpoints section and inspector, T-P25 Play / Loop, T-P26 tabs.

Still open from the first review (now in the FIX list): T-D04 fixture sizes, T-D15 preview, T-O01 thumbnails, T-O04
order, T-O10 strip, T-O11 positions (changed but still off), T-P03 several, T-P04 scrollbar, T-P07 price / scrollbar,
T-P08 Layers toggle / speed field / strip, T-P09 lower vowel mark, T-P13 link routing, T-P14 rows, T-P15 lists /
preview buttons, T-P17 all, T-P20 scrollbar / seams, T-P24 captured state, T-P25 list order.

## Notes

- Row 1474 says the editor viewport shows the label "SCORE"; the T-P01 / T-L01 captures show none. Row 1475 says the
  fixture has 12 sprites; T-P01 counts 13.
- T-D01 / T-L02 eight template cards are recorded only in editor-gaps.md and under D-09, not as a scope-log row.
- Row 1462 (T-D08 options persistence), 1443 / 1458 (T-P22 caption), 1444 (T-S08 hint), 1447 (T-P20 Racing) and 1461
  (Scene menu title) have no visible part in these images.
- MATCH lines in the TSV cite the diff image, the shot-report numbers and this review at `7487e78`; the date is left
  to the person who ticks.
