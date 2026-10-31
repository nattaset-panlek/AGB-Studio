# Review: SHOT "matches Figma" for windows (2026-10-31)

Independent read-only review of the `- [ ] matches Figma` boxes in `docs/ENGINE-CHECKLIST.md` sections 4.1, 4.2,
4.3, 4.4, 4.6, 4.7, 4.8 and 4.10 (75 windows), at commit `c407d74`. Sections 4.9 (states) and 4.11 (system windows)
have no "matches Figma" box; see the note at the end. Menus, buttons, components, tokens and brand are reviewed
separately. Nothing was built, run or exported for this review: the images are the ones in the repo
(`docs/evidence/figma/<id>.png`, `docs/evidence/app/<id>.png`, `docs/evidence/diff/<id>.png`, numbers in
`docs/evidence/shot-report.md`).

Method (SHOT procedure, section 2.5): every window was looked at side by side (Figma above or left, app below or
right), full frame and zoomed. Positions were measured on the PNGs: element edges and text extents, plus a per-element
alignment search (each element of the Figma frame matched against the app within 7 px) to find anything off by more
than 2 px. Colours were sampled and compared with `Theme/Tokens.Dark.axaml`. Text weight was compared by ink amount;
the app (Skia) draws every text about 20 to 50 % heavier than the Figma export, so a weight difference is reported only
where it is clearly larger than that.

Treated as noise (not a difference): anti-aliasing and the heavier Skia text rendering; a text caret (Figma draws none);
the real game picture in a game view (T-P02 Game View, T-P09 / T-P14 dialogue preview background, T-P17 Preview, T-P19
VRAM tiles); live values that a window shows by design (frame counter, fps / CPU, positions, console log, memory and
palette RAM, budget numbers, build progress, loading step, the About version).

Decisions:

- **MATCH**: layout within 2 px, same colours, fonts, icons, texts and states. Tick; the line is in
  `docs/evidence/evidence-shot-windows.tsv`.
- **USER**: the only remaining differences are already described by a pending scope-log row or an editor-gaps.md row.
- **FIX**: a difference nobody recorded. "Fix" can mean a code change or, where the Figma frame contradicts itself or
  the app is arguably right, a new scope-log row for the user to accept; each item says which.
- **MISSING**: no app image or no Figma image.

Every window that has a recorded (USER) difference also has at least one unrecorded one, so all of them are FIX; the
recorded part is listed in the "Also waits on" column and in the USER list below.

## Counts

| Decision | Windows |
|---|---|
| MATCH | 9 (T-D09, T-D11, T-S01, T-S05, T-D13, T-D20, T-D22, T-D23, T-O13) |
| USER | 0 (12 FIX windows also carry a recorded difference, listed below) |
| FIX | 66 |
| MISSING | 0 (all 75 windows have a Figma, an app and a diff image) |

## All windows

| Id | Decision | Reason | Also waits on (recorded) |
|---|---|---|---|
| T-P01 Scene Editor | FIX | Viewport, inspector sprite and Assets come from a fixture that does not look like the Figma scene; speed is a slider; text fields inset 6 px; inspector lower part 3-4 px low | - |
| T-P02 Play Mode | FIX | Toolbar centre group 6 px right; inspector text fields inset 6 px (game view, numbers and console are live: noise) | - |
| T-P03 Tilemap Editor | FIX | Collision view draws no red tile outlines; fixture tileset / map / counts differ; Figma eye icons black | - |
| T-P04 Script Editor | FIX | Breadcrumb, script order, return type text differ; outline rows 5 px right; popup buttons 4 px low; undo/redo greyed; fields inset | - |
| T-P05 Console panel | FIX | "Errors · 1" danger chip vs Figma "Errors · 0" neutral chip (Figma lists an error but counts 0) | - |
| T-P06 Animation panel | FIX | Fixture animation (2 red-blob frames, 20 ticks) vs 8 hero frames; Play / Step button styles differ; list 3 px low | - |
| T-P07 Database | FIX | No dirty dot on items.db; icons 8 px not 16 px; extra description field; text and data differences; toolbar +4 px | ROM 528 B vs 1.1 KB: scope log line 1454 (T-P07, P9.07) |
| T-P08 Sprite Editor | FIX | No dirty dot; G 10 / 0x355F vs 9 / 0x35FF; frame strip sprites 1x not 2x; Play button and Speed field differ; Layers toggle on | - |
| T-P09 Font Manager | FIX | Fixture has no missing glyphs (Figma shows 2 missing and "3 missing"); glyph grid 5 px low; lower-vowel mark not drawn; canvas scale | - |
| T-P10 Palette Manager | FIX | G 10 / 0x355F vs 9 / 0x35FF; "6-9" vs "6-8"; inspector 2-6 px low; fixture palette data | - |
| T-P11 Build Report | FIX | Share bars scaled differently; header text fields inset 6 px; ROM BREAKDOWN label 3 px low | - |
| T-P12 Launcher | FIX | "Cave Quest" vs "Cave Quest RPG"; logo says v1.0 while the top bar says v0.1; Templates nav icon differs | - |
| T-P13 World Map | FIX | Canvas dot grid 20 px vs 24 px; link routing differs; zoom 100 % vs 86 %; fields inset; undo/redo greyed | - |
| T-P14 Localization | FIX | GBA preview box white and unwrapped; long / missing rows not visible; counts in mono; toolbar +4 px; inspector 3-4 px low | - |
| T-P15 Audio | FIX | Tracker row pitch 15.5 px vs 14 px; lists sorted differently; Audio ROM meter empty; fixture song data | Mixer CPU 14 % vs 6 %: scope log line 1455 (G-PERF.05) |
| T-P16 Dialogue Editor | FIX | Line preview white and unwrapped with overlapping choice; "Flags.MetOwl" vs "flags.metOwl"; fixture; toolbar +4 px | - |
| T-P17 Logic Graph | FIX | Set Velocity Y node runs into Play Sound; On Touch pins 26 px low; node palette differs; inspector 6 px low; undo/redo greyed | Generated "!=" guard: scope log line 1447 (T-P17 On Touch Tag) |
| T-P18 Scripting API | FIX | Four member descriptions reworded; "// walk" 8 px right; "Docs v1.0"; undo/redo greyed | See also row: scope log line 1448 (G-SCOPE.02) and editor-gaps.md "Other differences" |
| T-P19 Debugger | FIX | Toolbar centre group 5 px left; undo/redo greyed (VRAM, OAM, profiler data are live: noise) | - |
| T-D01 New Project | FIX | Project name shown selected (highlight) on open | 8 template cards in a scrolling grid (scrollbar, column 3 narrower): editor-gaps.md "Other differences" |
| T-D02 New Scene | FIX | "Forest_03" / width 30 vs "Forest_02" / 32 (Figma conflicts with its own T-O01 / T-D03): record | - |
| T-D03 Scene Settings | FIX | Name, OnEnter, OnExit text 6 px right; BG2 slider knob at 1.0 is 10 px right, fill shorter | - |
| T-D04 Import Asset | FIX | Frames found text 7 px right; fixture preview, sizes and palette slot differ; Figma upload icon black | - |
| T-D05 Export ROM | FIX | "skyhopper.gba" vs "sky_hopper.gba"; field not focused; Estimated size row 4 px low; meter dot | - |
| T-D06 Build ROM | FIX | Title "Building skyhopper.gba..." vs "sky_hopper.gba" | - |
| T-D07 Flash to Cartridge | FIX | "128 / 412 KB" clipped to "128 / 412 KE" | Flash greyed for GBxCart RW: editor-gaps.md "Accepted by decision" (D-12, not yet ticked) |
| T-D08 Project Settings | FIX | Action name, Keyboard and Gamepad columns 9-10 px right | - |
| T-D09 Preferences | MATCH | Same layout, colours, texts and states (row 1443 still names T-D09 for a gap editor-gaps.md lists as fixed) | - |
| T-D10 Keyboard Shortcuts | FIX | A scrollbar thumb overlaps the V keycap | Footer link "Edit in Preferences › Shortcuts": scope log line 1443, editor-gaps.md Fixed T-D10 |
| T-D11 About | MATCH | Same; the version is the running build's (1.0.0 vs 0.1.0), title block within 2 px | - |
| T-D12 Unsaved Changes | FIX | File row icons black in the Figma frame, grey in the app: record | - |
| T-S01 Project Settings · General | MATCH | Same (focus ring 2 px vs 1.5 px is anti-aliasing) | - |
| T-S02 Project Settings · Save data | FIX | "+ Add field" uses text/secondary, Figma text/muted | byte / short vs int: scope log line 1456 (T-S02), editor-gaps.md Still open |
| T-S03 Project Settings · Build | FIX | Configuration segment 7 px right; toolchain title 3 px right | - |
| T-S04 Project Settings · Scripting | FIX | GAE005 / GAE006 descriptions longer than Figma; severity chevrons 3 px right | - |
| T-S05 Project Settings · Localization | MATCH | Same; the Thai bar fills 96 % (326 / 340) where Figma drew it full: data | - |
| T-S06 Preferences · Appearance | FIX | Theme preview blue #6AA0FF vs #58B4FF; System preview split differs | - |
| T-S07 Preferences · Editor | FIX | Font dropdown clips "JetBrains Mono" to "JetBrains Mo" (Figma overflows the box): decide | - |
| T-S08 Preferences · Paths | FIX | Build cache title / path 6 px right; Figma icon black | Per-project build cache path: editor-gaps.md "Other differences" |
| T-S09 Preferences · Shortcuts | FIX | Edit (pencil) buttons 6 px right | - |
| T-O01 Scene Switcher | FIX | Selected row name #CFE0FF vs text/primary #E7E8EB and bolder size; thumbnails are real renders, not the Figma art | - |
| T-O02 Video Mode | FIX | Mode descriptions text/secondary #A3A6AE vs text/muted #6F727B | - |
| T-O03 Grid & Snap | FIX | "Camera" vs "Camera bounds"; blue / red swatches 18 px without ring vs 22 px with black ring | - |
| T-O04 Asset Picker | FIX | Fixture sprites and order differ; third row and scrollbar visible | - |
| T-O05 Component Picker | FIX | Category headers (icon and label) 3 px right, 1 px down | - |
| T-O06 Budget Details | FIX | Captured before a build ("not built"); "5 / 16 colour" clipped; tip box 4 px taller, button 8 px low | - |
| T-O07 Command Palette | FIX | player.png and Platform_tiles.png icons differ; Figma search icon black | - |
| T-P20 Launcher · Templates | FIX | Logo v1.0 vs v0.1; Templates nav icon; grid scrollbar visible; 1 px seams in the preview picture | - |
| T-P21 Launcher · Learn | FIX | Logo v1.0; Templates nav icon; sample names cut to one line; Community rows 3 px right; Figma icons black | - |
| T-P22 Scene Editor · Empty project | FIX | Empty-state card 3 px high; undo/redo greyed; Figma map icon black | (Caption row 1457 has no visual part) |
| T-P23 Debugger · Memory | FIX | Breakpoints list in the left panel (not in Figma); toolbar 5 px left; undo/redo greyed | - |
| T-P24 Debugger · Palettes | FIX | Breakpoints list in the left panel; inspector 4-6 px low; captured state shows no change / black colour; toolbar 5 px left | - |
| T-P25 SFX Designer | FIX | No dirty dot; Play / Loop buttons moved and resized; list order; inspector 4-5 px low; waveform width | - |
| T-P26 Auto-tile Rules panel | FIX | Fixture rule set (4 rules, no Corner) and "any" cells; tabs 2-5 px left | - |
| T-D13 New Sprite | MATCH | Same | - |
| T-D14 New Script | FIX | Figma icons black (Static helper, Scene script); Behaviour check mark 3 px left | - |
| T-D15 New Font | FIX | Symbols checkbox 4 px right; preview box colour, text size and caption position differ | - |
| T-D16 Add Characters | FIX | "NotoSansThai.ttf" vs "NotoSansThai-Regular.ttf" | 2 vs 7 missing characters: scope log line 1453 (T-D16, P8.08) |
| T-D17 New Table | FIX | Hint "EnemyId.Enemy1" vs "EnemyId.Bat" | - |
| T-D18 Add Field | FIX | Enum value chips 2 px further apart each (Epic 4 px right) | - |
| T-D19 Import CSV | FIX | Warning text longer (2 lines); "Choose another..." 9 px right; Figma file icon black | - |
| T-D20 Add Language | MATCH | Same | - |
| T-D21 New Speaker | FIX | Name · EN focused; 7 fixture portraits with the 7th clipped vs 4 | - |
| T-D22 Opening Project | MATCH | Same; "29 / 47 · blip.wav" is the live loading step | - |
| T-D23 Build ROM · Failed | MATCH | Same | - |
| T-D24 Import Asset · Errors | FIX | Cards grow 1 px each, list overflows and shows a scrollbar; card text 3-4 px low | "Split into 4 palettes": scope log line 1452 (T-D24, P7.03, D-25) |
| T-O08 Add Node | FIX | Different result list (no MATH section), mono hints, footer pushed out; Esc keycap moved | - |
| T-O09 Link Settings | FIX | "≥" character instead of the ">=" ligature Figma shows (text 6 px shorter) | - |
| T-O10 Palette Effect | FIX | Preview strip shows the 4 range blues; Figma shows 4 different frames | - |
| T-O11 Find & Replace | FIX | Find field 13 px narrower; icons and buttons 2-5 px left; replace field 15 px narrower | - |
| T-O12 Record Shortcut | FIX | Warning text 12 px instead of 11 px | - |
| T-O13 Tutorial Steps | MATCH | Same | - |
| T-L01 Scene Editor · Light | FIX | As T-P01 | - |
| T-L02 New Project · Light | FIX | As T-D01 (project name selected) | As T-D01 (8 cards) |
| T-L03 Preferences · Appearance · Light | FIX | As T-S06 (preview blue, System preview split) | - |

## Differences that repeat across windows

Fixing one of these clears it in every window listed.

| # | Difference | Windows | Kind |
|---|---|---|---|
| R1 | Inspector text fields (`InspectorField` Kind=Text) draw their text 6-7 px right of Figma. Likely cause: `FieldBox` in `Theme/Panels.axaml` puts `HorizontalSpacing="6"` between `InnerLeftContent` and the text even when there is no inner content (padding 8 + 6 instead of 8). XY fields are correct. | T-D03, T-D04, T-P01, T-P02, T-P04, T-P07, T-P08, T-P11, T-P13, T-P15, T-P16, T-L01 | code |
| R2 | Icons the Figma frame draws black (`#000000`, invisible on the dark background); the app draws them with a text token. Same artefact as scope log line 1458, which today names only two components. | T-D04 (upload), T-D12 (file rows), T-D14 (Static helper, Scene script), T-D19 (file), T-S08 (Build cache), T-O07 (search), T-P03 (layer eye icons), T-P21 (Community), T-P22 (empty-state map), and single icons in T-P02, T-P08, T-P17 | record (extend line 1458) or re-export |
| R3 | ROM file name `skyhopper.gba`; Figma `sky_hopper.gba` (the Console panel T-P05 and Build Report status bar already say `sky_hopper.gba`) | T-D05, T-D06 (also the live console line in T-P02) | code or fixture |
| R4 | Undo / redo greyed (nothing to undo in the capture); Figma shows them enabled | T-P04, T-P13, T-P17, T-P18, T-P19, T-P22, T-P23, T-P24 | capture state or record |
| R5 | Toolbar Play / Pause / Step group not where Figma puts it | T-P02 (+6 px), T-P07 (+4), T-P14 (+4), T-P16 (+4), T-P19, T-P23, T-P24 (-5) | code |
| R6 | Lower inspector sections drift 3-6 px down | T-P01 / T-L01, T-P10, T-P14, T-P15, T-P17, T-P24, T-P25 | code |
| R7 | The Sky Hopper fixture does not show what Figma draws: hero.png is a red blob in T-P01, T-P06, T-O04, T-D04 but the yellow hero in T-P08 and in the T-P02 inspector; Forest_01 has no HUD, ? block, coins or bricks at the top; other sprites, counts and rule sets differ | T-P01, T-L01, T-P03, T-P06, T-P09, T-P15, T-P16, T-P26, T-D04, T-D21, T-O04 | fixture, or record |
| R8 | Doc tab dirty dot missing | T-P07, T-P08, T-P25 | capture state |
| R9 | Launcher logo subtitle `v1.0` while the top bar still says `v0.1` (Figma: v0.1 in both); Scripting API status `Docs v1.0` | T-P12, T-P20, T-P21, T-P18 | code (one source for the version) or record |
| R10 | Templates nav icon: Figma a filled 2x2 tile glyph, app an outlined 2x2 grid; same pair in the T-O07 file icons | T-P12, T-P20, T-P21, T-O07 | code or record |
| R11 | Scrollbars shown where Figma has none | T-D10 (overlaps a keycap), T-D24, T-O04, T-P01 Assets, T-P04 Scripts, T-P07 table, T-P20 grid (T-D01 / T-L02: recorded) | code |
| R12 | text/secondary `#A3A6AE` where Figma uses text/muted `#6F727B` | T-S02 ("+ Add field"), T-O02 (mode descriptions) | code |
| R13 | Colour 4 shows G 10 and `0x355F`; Figma G 9 and `0x35FF`. `#FF4F6D` gives 31, 9.6, 13; Figma's `0x35FF` does not match its own 31 / 9 / 13 (that would be `0x353F`) | T-P08, T-P10 | record (Figma error) and choose rounding |

## FIX list

Coordinates are pixels in the frame (x from the left, y from the top); "Figma" is the Figma export, "app" the app
capture.

**T-P01 Scene Editor** (same for **T-L01**)
- Viewport (330-1050, 128-608): the fixture's Forest_01 shows sky, two clouds, a bridge and a red-blob player in the
  air with selection handles; Figma shows the HUD (three hearts, "SCORE 0120"), ? block, three coins, brick platform,
  hills, ground, flag, slime and the yellow hero on the ground with a "Player" name chip (R7).
- Inspector Sprite: thumbnail, Frames (2 vs 4) and Palette (3 vs 13 colours) follow the red blob (R7).
- PlayerController "speed" is a slider showing 1.50; Figma a text field showing 1.5 (y ≈ 636).
- jumpPower / lives text 6 px right (R1); from Collider down the inspector is 3-4 px low (R6).
- Assets panel: 13 sprites in two rows with a scrollbar (bat, slime_king, gem...) vs 12 in one row (R7, R11).

**T-P02 Play Mode**
- Stop / Pause / Step / Simulating group at x 710-966; Figma 704-960 (R5).
- speed / jumpPower / lives text 6 px right (R1).

**T-P03 Tilemap Editor**
- Collision view is on in both, but the app draws no red outline on solid tiles (Figma outlines every solid tile in
  red); one-way tiles get a blue fill.
- Fixture: tileset art, "Tile #1" vs "Tile #0", "Grass edges (4)" vs "(16)", Solid tiles 30 / One-way 20 vs 412 / 9,
  VRAM column values, status "tile 12, 14 · id 0 empty" (R7).
- Layers table eye icons black in Figma (R2); BG3 eye dimmed in the app only.

**T-P04 Script Editor**
- Breadcrumb ends "› coyoteTime"; Figma "› Update()" (both status bars say Ln 8, so Figma conflicts: record or move the
  caret).
- Scripts list order HUD.cs, CameraFollow.cs, TitleMenu.cs vs HUD.cs, TitleMenu.cs, CameraFollow.cs; scrollbar shown (R11).
- API quick reference: `Text.Show(string key)` returns `void`; Figma `Dialogue`.
- Outline children (Start(), Update(), OnTouch(Actor)) 5 px right; diagnostic popup buttons 4 px low.
- Attached to / Compiles to / jumpPower / lives text 7 px right (R1); undo / redo greyed (R4).

**T-P05 Console panel**
- Filter chip "Errors · 1" in danger colours; Figma "Errors · 0" neutral although its list has one error line. Record
  (Figma conflict) or match the frame.

**T-P06 Animation panel**
- Fixture: hero.png idle has 2 red-blob frames at 20 ticks (≈ 3 fps), list 2f / 4f / 1f / 1f; Figma 8 hero frames at
  6 ticks (≈ 10 fps), 8f / 6f / 3f / 2f; Console badge 1 vs 6; frame 1 selected vs 3 (R7).
- Play and Step are both outlined boxes; Figma: Play filled (no border), Step bare icon.
- Animation list rows 3 px low.

**T-P07 Database**
- Doc tab "items.db" has no dirty dot (R8). Icon column draws sprites at about 8 px; Figma 16 px.
- Heart price 30 vs 0; schema list has an extra "description" field; "Shop_01 · slot2" vs "Shop_01 · slot 2".
- Record fields 6 px right (R1); table scrollbar (R11); toolbar group +4 px (R5); power slider knob 8 px left.

**T-P08 Sprite Editor**
- Doc tab "hero.png" has no dirty dot (R8); Layers toggle (toolbar, x ≈ 279) shown on; Figma off.
- Colour 4: G 10, Hex Y `0x355F`; Figma G 9, `0x35FF` (R13).
- Animation strip: sprites drawn at about 1x (Figma 2x); Play button has no background (Figma filled); Speed field
  655-785 vs 655-760 (Figma text overflows its box).
- Palette slots and Origin / VRAM rows: text 6 px right (R1); canvas bottom rows show other pixels; status VRAM OBJ
  2 / 32 KB vs 16 / 32 KB.

**T-P09 Font Manager**
- Fixture has no missing glyph: Figma shows 0E03 and 0E05 dashed (missing) and the status "128 glyphs · 3 missing"
  with a warning icon; the app shows "194 glyphs · 0 missing" (the 4.9 "missing glyphs" state is not shown) (R7).
- Glyph grid 5 px low; glyph editor canvas smaller (276-420 vs 276-456); "ADVANCE 7" vs 9; title_big "VWF · 16px" vs
  "16×16"; Draw to "BG 0" vs "BG 2".
- "Place lower vowels (ฺ)": the app draws "()" without the lower vowel mark.

**T-P10 Palette Manager**
- Colour 4 G / Hex as T-P08 (R13). Cycle "BG 1 · 6-9 · water" vs "6-8 · water".
- Inspector Colour, Tools and Palette effects sections 2-6 px low (R6); BG 1 · Ground 12 / 16 vs 9 / 16 and
  background palette RAM contents (R7).

**T-P11 Build Report**
- Largest assets share bars are scaled to the largest asset (bgm_forest full); Figma uses another scale
  (bgm_forest ends at x 781 of 590-830); items.db shows a dot where Figma shows none.
- Cartridge header fields 6 px right (R1); "ROM BREAKDOWN" label 3 px low.

**T-P12 Launcher**
- Recent project "Cave Quest" vs "Cave Quest RPG"; logo subtitle "v1.0" (R9); Templates nav icon (R10); "All
  templates" chevron 3 px right.

**T-P13 World Map**
- Canvas dot grid every 20 px; Figma every 24 px.
- Link routing: the "Exit trigger" curve leaves Forest_01 at its top right and passes under its chip; Figma runs from
  the card side with the chip beside the line; "Start" chip label 3 px left.
- Status bar zoom "100%" vs "86%"; Size / OnEnter / OnExit text 7 px right (R1); undo / redo greyed (R4).

**T-P14 Localization**
- GBA preview: the app draws a white text box with one unwrapped line touching its left edge; Figma a dark box with
  two wrapped lines and ▼; caption "พอดี 1 บรรทัด" vs "2 บรรทัด".
- Rows: the app lists dlg.forest.owl_01, hud.score, item.extra.000... all "ok"; Figma shows ui.menu.*, item.potion.desc
  "long" and shop.welcome / boss.summit.intro "missing" (the 4.9 warning states are not visible) (R7).
- Language counts "340 / 340" in mono; Figma in the sans caption style.
- Toolbar +4 px (R5); inspector 3-4 px low (R6); Box width 184 / 154 vs 198 / 176.

**T-P15 Audio**
- Tracker rows every 15.5 px; Figma every 14 px.
- Music and sound lists sorted by name (bgm_cave, blip first); Figma order bgm_forest, jump first.
- Audio ROM meter shows no fill at 90 KB (Figma 96 KB with fill); Preview SFX 5 buttons on two rows vs 3.
- Fixture song: length 2:38, 21 patterns, loop 4-20, 5 mixer channels, pattern data (R7). Loop start text 6 px right
  (R1); inspector 3 px low (R6).

**T-P16 Dialogue Editor**
- Line preview: white box, text unwrapped, choice "ไปที่ถ้ำ" overlapping the box bottom; Figma dark box with portrait,
  two wrapped lines and ▼.
- Conditions "!Flags.MetOwl" and "Flags.MetOwl = true" vs "!flags.metOwl" and "flags.metOwl = true".
- forest_intro 3 lines vs 6, Hopper portrait red blob (R7); toolbar +4 px (R5); condition fields 6 px right (R1).

**T-P17 Logic Graph**
- Set Velocity Y node has extra Mode and Target rows and grows down into the Play Sound node (x 1000: one block from
  y 137 to 319; Figma ends at 233 with a gap to Play Sound at 276); its exec wire to Play Sound runs differently.
- On Touch node: Tag / Other rows start 26 px lower (y 572 vs 546).
- Nodes palette: different items (On Late Update, On Destroy, Velocity X / Y, Jump, an "Other" group), items 6 px left,
  On Touch not highlighted.
- Inspector without the Target row, 6 px low (R6); minimap layout differs; undo / redo greyed (R4).

**T-P18 Scripting API**
- Member descriptions: "true on the frame the button goes down" / "true while the button is held" / "true on the frame
  the button goes up" / "-1, 0 or 1 from the D-pad" / "true when any of the ten buttons was pressed this frame";
  Figma "...the key goes down" / "true while the key is down" / "...the key goes up" / "D-pad as −1, 0 or 1" / "any
  of the 10 keys pressed this frame".
- Example comment "// walk" 8 px right; status "Docs v1.0" (R9); undo / redo greyed (R4).

**T-P19 Debugger**
- Toolbar group 5 px left (R5); undo / redo greyed (R4).

**T-D01 New Project** (same for **T-L02**)
- Project name text is selected (blue highlight) when the dialog opens; Figma shows the focused field without a
  selection.

**T-D02 New Scene** (record)
- Scene name "Forest_03" and Width 30; Figma "Forest_02" and 32. Figma's own T-O01 lists Forest_02 (32×20) as an
  existing scene and T-D03 gives Forest_01 30×20, so the app's values follow the project; needs a scope-log row (or
  a new Figma sample).

**T-D03 Scene Settings**
- Name, OnEnter, OnExit text starts at x 123; Figma 117 (R1).
- BG2 Ground slider at 1.0: knob centre x 527, blue fill to 520; Figma knob 517, fill to 532.

**T-D04 Import Asset**
- Frames found text x 671 vs 664 (R1); upload icon black in Figma (R2).
- Fixture: preview shows four red blobs (Figma the yellow hero), sizes 0.2 KB and 3.6 KB vs 2.1 KB and 14 KB, Palette
  "OBJ 7 · share with hero" vs "OBJ 0" (R7).

**T-D05 Export ROM**
- File name "skyhopper.gba" vs "sky_hopper.gba" (R3); the field is not focused (Figma blue focus ring).
- Estimated size row 4 px low (y 388 vs 384); the meter draws a 6 px blue dot at 398 KB / 32 MB, Figma an empty track.

**T-D06 Build ROM**
- Title "Building skyhopper.gba..." vs "Building sky_hopper.gba..." (R3).

**T-D07 Flash to Cartridge**
- "128 / 412 KB" (x 470-538) is clipped at the right edge and reads "128 / 412 KE".

**T-D08 Project Settings (Input)**
- Column starts ACTION NAME 381, KEYBOARD 582, GAMEPAD 761; Figma 372, 572, 752 (first column 150 px wide instead of
  140).

**T-D10 Keyboard Shortcuts**
- A 2 px wide scrollbar thumb (`#858585`) at x 735-736, y 131-158 overlaps the right edge of the "V" keycap.

**T-D12 Unsaved Changes** (record)
- File row icons black in Figma, grey in the app (R2).

**T-D14 New Script**
- Static helper and Scene script icons black in Figma (R2); Behaviour check mark at x 507 vs 510.

**T-D15 New Font**
- "Symbols · 12" checkbox at x 203 vs 199.
- Preview: box `#1E1A28` vs `#1B1630`; the sample is drawn as 2x pixel glyphs spanning x 33-280 (Figma 34-203) and the
  caption sits 6 px higher; "about 4 KB of ROM" vs "about 6 KB".

**T-D16 Add Characters**
- "Render the new glyphs from NotoSansThai.ttf"; Figma "NotoSansThai-Regular.ttf" (T-D15 in the app also says
  NotoSansThai-Regular.ttf).

**T-D17 New Table**
- Hint "Scripts use it as Enemies.Get(EnemyId.Enemy1)" vs "Enemies.Get(EnemyId.Bat)".

**T-D18 Add Field**
- Enum chips at x 21, 94, 144 vs 21, 92, 140 (gap 9 px vs 7 px); "Add value" follows.

**T-D19 Import CSV**
- Warning: "Rows 7 and 15: Cost is "N/A", not a whole number — will use the default 0. Fix the CSV and choose it
  again to keep the value." on two lines; Figma "Rows 7 and 15: Cost is "N/A" — will use the default 0." on one line
  (box taller).
- "Choose another..." x 658-756 vs 649-747; file icon black in Figma (R2).

**T-D21 New Speaker**
- Name · EN field has the focus ring; Figma none.
- Portraits: 7 fixture sprites plus the add tile, the 7th clipped at the dialog edge; Figma 4 portraits plus add (R7).

**T-D24 Import Asset · Errors**
- Each card is 1 px taller with a 1 px larger gap: card 3 ends at y 359 vs 354, the list overflows and shows a
  scrollbar at x 716 (y 69-354); card 2 / 3 text 3-4 px low.

**T-S02 Project Settings · Save data**
- "+ Add field" `#A3A6AE` (text/secondary); Figma `#6F727B` (text/muted) (R12).

**T-S03 Project Settings · Build**
- Configuration segment x 315-439 vs 308-432; toolchain title "devkitARM 16.1 · D:\devkitPro" 3 px right.

**T-S04 Project Settings · Scripting**
- GAE005 "string concatenation in a loop (a new string every pass)" and GAE006 "lambda that captures variables (hidden
  allocation)"; Figma without the parentheses. Severity chevrons 3 px right.

**T-S06 Preferences · Appearance** (same for **T-L03**)
- Theme previews: viewport block `#6AA0FF`; Figma `#58B4FF`.
- System preview: the app splits the whole thumbnail at x ≈ 649 (title bar half light, bottom block half dark); Figma
  keeps the title bar dark across and the bottom-centre block white.

**T-S07 Preferences · Editor** (decide)
- Code font dropdown shows "JetBrains Mo" (clipped); Figma shows "JetBrains Mono" with the chevron pushed outside the
  box. Widen the field, or record.

**T-S08 Preferences · Paths**
- Build cache title and path start at x 265 vs 259, icon 2 px right; icon black in Figma (R2).

**T-S09 Preferences · Shortcuts**
- Pencil buttons at x ≈ 818-824 vs 812-817 (6 px right).

**T-O01 Scene Switcher**
- Selected row: name `#CFE0FF` vs text/primary `#E7E8EB`; "30×20" visibly bolder than the other sizes.
- Thumbnails are renders of the fixture scenes (plain sky); Figma shows designed art, and the app's own World Map
  (T-P13) uses that designed art. Confirm which is meant (game picture or designed art).

**T-O02 Video Mode**
- Mode descriptions `#A3A6AE` vs `#6F727B` (R12).

**T-O03 Grid & Snap**
- Label "Camera"; Figma "Camera bounds" (two lines).
- Grid colour swatches: blue and red 18 px at x 147 / 177 without a ring; Figma 22 px at 145 / 175 with a 1 px black
  ring.

**T-O04 Asset Picker**
- Fixture sprites hero (red blob), slime (green), block, flag, coin, owl vs hero, slime, coin, block, heart, cloud; a
  third row and a scrollbar (x ≈ 415, y 140-259) are visible (R7, R11).

**T-O05 Component Picker**
- PHYSICS / COMBAT / AI / AUDIO headers (icon and label) 3 px right and 1 px down (icon x 21 vs 18, label 39 vs 36).

**T-O06 Budget Details**
- Captured before a build: EWRAM, IWRAM and ROM say "not built"; Figma shows values (capture after a build).
- "5 / 16 colour" is clipped (no "s"); Figma wraps "15 / 16 colours" on two lines.
- Tip box 297-348 vs 295-342, so Open Build Report sits at 359-384 vs 351-376.

**T-O07 Command Palette**
- player.png icon is an outlined image glyph and Platform_tiles.png a 2x2 grid; Figma a filled square and a filled
  tile glyph (R10). Search icon black in Figma (R2).

**T-O08 Add Node**
- Results: Set Velocity X, Set Velocity Y, Set Velocity, Velocity X, Velocity Y, Get Velocity, all under PHYSICS; Figma
  Set Velocity X, Set Velocity Y, Get Velocity and a MATH section with Vector from Velocity.
- Hints in the mono font; Figma in the sans caption style. The footer (Enter "add here", Shift+Enter "add and
  connect") is pushed out of the 269 px frame. Esc keycap 4 px right, 3 px up.

**T-O09 Link Settings**
- "Game.coins ≥ 10" uses the ≥ character (x 127-224); Figma shows the JetBrains Mono ">=" ligature, two cells wide
  (x 125-228).

**T-O10 Palette Effect**
- Preview strip: the app shows the four range colours (blues) as one rounded strip; Figma four separate frames (green,
  yellow, light blue with a stripe, red with a white top).

**T-O11 Find & Replace**
- Find field x ≈ 12-418 vs 9-428; history / next / close icons 3 px left; Aa / ab / .* toggles 3-5 px left; replace
  field x ≈ 13-357 vs 9-368; Replace / Replace all 2-3 px left; scope segment 3 px right. ("2 of 2" vs "2 of 5" is
  the live match count.)

**T-O12 Record Shortcut**
- Warning "Ctrl+G is used by "Go to line"." spans x 53-219 (12 px text); Figma 53-204 (11 px).

**T-P20 Launcher · Templates**
- Logo "v1.0" (R9); Templates nav icon (R10); grid scrollbar at x ≈ 1116 (R11).
- Detail preview picture has 1 px dark seams at colour-band edges (for example `#5B97C8` at x 1160, y 158) from
  scaling.

**T-P21 Launcher · Learn**
- Logo "v1.0" (R9); Templates nav icon (R10).
- Sample project names cut to one line: "Sky Hopper", "Cave"; Figma wraps "Sky Hopper demo", "Cave Quest".
- Community rows 3 px right; their icons black in Figma (R2).

**T-P22 Scene Editor · Empty project**
- Empty-state card top at y 262 vs 265 (3 px high); its map icon black in Figma (R2); undo / redo greyed (R4).

**T-P23 Debugger · Memory**
- Left panel shows a BREAKPOINTS section under VIEWS; the Figma Memory view has none.
- Toolbar 5 px left (R5); EWRAM legend items 3 px shifted; undo / redo greyed (R4).

**T-P24 Debugger · Palettes**
- BREAKPOINTS section in the left panel, as T-P23.
- Inspector Used by / Actions 4-6 px low (Actions buttons 386 / 418 vs 380 / 412) (R6).
- Captured state: BG 1 index 7 is `0x0000` (black), "Not changed while playing", subtitle without "(water cycle)";
  Figma shows a changed colour with the white outline (capture while the water cycle runs).
- Toolbar 5 px left (R5); undo / redo greyed (R4).

**T-P25 SFX Designer**
- Doc tab "jump.sfx" without the dirty dot (R8); sound list sorted by name (blip first) vs jump first.
- Play button x 964-1007 vs 974-1017; Loop 1020-1099 and 31 px tall vs 1030-1099 and 25 px; Duty segment ends at
  574 vs 566; waveform ends at 625 vs 672.
- Pitch / volume graph and registers follow other parameters (Sweep up 5 vs 2); inspector 4-5 px low (R6).

**T-P26 Auto-tile Rules panel**
- Fixture: "Grass edges (4)" with Top edge, Left edge, Right edge, Inner; Figma "(16)" with a Corner rule too;
  neighbour cells drawn grey for "any" (Figma green / red only) (R7).
- "Auto-tile rules" tab 2 px left, "Assets" tab 5 px left.

## USER: recorded differences waiting for the user

These are real differences that a pending row already describes. They do not change the decisions above (each of
these windows also has an unrecorded difference), but they must be accepted before the box can be ticked.

| Window | Difference | Row |
|---|---|---|
| T-D01, T-L02 | Eight template cards in a scrolling grid (scrollbar, third column narrower) | editor-gaps.md "Other differences noted on the window pages", T-D01 |
| T-D07 | Flash button greyed for GBxCart RW | editor-gaps.md "Accepted by decision", T-D07 (D-12; D-12 itself is not ticked in section 3) |
| T-D10 | Footer link "Edit in Preferences › Shortcuts" not in the frame | scope log line 1443 (pending) and editor-gaps.md Fixed, T-D10 |
| T-D16 | 2 missing characters / "Add 2 glyphs" vs 7 | scope log line 1453 (T-D16, P8.08) |
| T-D24 | "Split into 4 palettes" vs 3 | scope log line 1452 (T-D24, P7.03, D-25) |
| T-S02 | Types byte / short vs int | scope log line 1456 (T-S02); editor-gaps.md Still open, T-S02 |
| T-S08 | Build cache path is per project, not `%LOCALAPPDATA%\AGBStudio\cache` | editor-gaps.md "Other differences", T-S08 |
| T-P07 | Status bar "ROM 528 B" vs "ROM 1.1 KB" | scope log line 1454 (T-P07, P9.07) |
| T-P15 | Mixer CPU 14 % vs 6 % | scope log line 1455 (G-PERF.05) |
| T-P17 | Generated C# guard `if (other.tag != "Coin") return;` | scope log line 1447 (T-P17 On Touch Tag) |
| T-P18 | SEE ALSO row of buttons | scope log line 1448 (G-SCOPE.02); editor-gaps.md "Other differences", T-P18 |

Recorded rows with no visible part in these images: line 1457 (T-P22 caption, no template control in either image),
line 1446 (T-P20 Racing "Opponent cars", not on screen with Platformer selected), line 1460 (T-P02 / T-P03 / T-P04 /
T-P08 Scene menu title, behaviour only), line 1461 (T-D08 options persistence).

Scope log line 1443 still lists T-P01, T-P05, T-P06, T-P08, T-P19, T-P22, T-P24, T-D04, T-D07, T-D09, T-D10, T-S02,
T-S04, T-S06, T-S07, T-S08, T-O01, T-O04, T-O07 and T-O08 as pending, while editor-gaps.md lists all of those gaps
except T-P22, T-S02 and T-S08 as fixed. T-D09 is ticked here on its picture; the row should be updated.

## Notes

- Section 4.9 (states) and 4.11 (system windows) have no "matches Figma" box. For 4.9, the state pictures that differ
  are: T-P09 shows 0 missing glyphs (Figma "3 missing"), T-P14 shows no "long" / "missing" row in view, T-D07 clips
  "Writing x / y KB", T-D24 overflows, and the doc-tab dirty dot is missing in T-P07, T-P08 and T-P25 (T-P04 has it).
  T-P22, T-D22, T-D06, T-D23, T-S09, T-O12, T-D19, T-D01, T-S08, T-D09 and T-D12 show their states as Figma does
  (apart from the items above). 4.11 is "no visual check".
- The note in shot-report.md that T-P01 / T-L01 panel contents "stay empty" (Phase 10) is out of date: the panels are
  filled now, and the large T-P01 diff comes from the fixture scene (R7).
- MATCH lines in the TSV cite the diff image, the shot-report numbers (all nine have offset 0 px) and this review at
  `c407d74`; the date is left to the person who ticks.
