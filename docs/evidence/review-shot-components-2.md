# Review 2: SHOT for menus and components still unticked (2026-10-31)

Second independent read-only review of the "matches Figma" boxes that were still unticked in
`docs/ENGINE-CHECKLIST.md` 4.5 (menus) and 4.16 (components), and of the SHOT part of T-R01, at `7487e78` (detached
snapshot; the full CI passed on this code). The reviewer wrote none of the UI. Nothing was built, run or exported from
Figma, and no file was changed except this review and `evidence-shot-components-2.tsv`.

Inputs: `docs/evidence/figma/<id>.png`, `docs/evidence/app/<id>.png`, `docs/evidence/diff/<id>.png`,
`docs/evidence/shot-report.md` (the numbers below are from it), `docs/evidence/figma/tokens.json`, the first review
`docs/evidence/review-shot-components.md` (at `c407d74`, FIX list F1..F9), checklist sections 2.2, 2.3, 2.5 and 8.

Method (SHOT procedure, 2.5): every Figma / app pair was composited on the same background the report uses (#151619
for components, black for menus and frames), put side by side with a mask of the pixels that differ by more than 48
per channel, upscaled 2x to 12x and looked at. On top of the report numbers, each image was cut into its elements
(icon, label, shortcut, chevron, count, knob, border) by column gaps, and the bounding box and peak colour of every
element were compared between Figma and app, because the report's single shift for the whole image hides local
offsets. Tolerances: layout within 2 px, colours from tokens, same font, icons, texts and states. The measuring
scripts are not part of the repo.

Decisions:

- **MATCH**: same within the SHOT tolerances; the box can be ticked. Evidence line in `evidence-shot-components-2.tsv`.
- **USER**: every remaining difference is described by a pending Scope-log row (section 8), named below; the user's
  decision on that row closes the box.
- **FIX**: a difference that no Scope-log row records.
- **PARTIAL**: the SHOT part matches, but something else the item asks for cannot be evidenced yet.

Scope-log rows used (section 8, all "pending"):

| Ref | Checklist line | Row (Date · Who · Item) | Subject |
|---|---|---|---|
| U1 | 1459 | 2026-10-31 · build session · "C-icon-button-default, C-play-control-step; also (SHOT review F9) Button Secondary / Ghost Default, Play Control Pause, Layer Toggle On, Chip Default, Tree Item Hover, Doc Tab, Panel Tab, List Item, Nav Item, Status Item Default, Section Header Open / Closed, Console Line Info, Panel Header, Dialog Header" | Figma component masters draw a glyph (or one label) black; the app draws it grey as the Screens frames do |
| U2 | 1463 | 2026-10-31 · SHOT review · "T-M01, T-M02, T-M04..T-M07, T-M09..T-M11, T-M13, T-M16, T-M17, T-M20, T-M22..T-M24" | Editor Map boards fill closed-outline menu icons; icon set and Screens use outlines (first review F1) |
| U3 | 1464 | 2026-10-31 · SHOT review · "C: Slider, Field (Slider)" | Slider knob ring (first review F4) |
| U4 | 1480 | 2026-10-31 · page fixes · "T-P12, T-P20, T-P21, T-O07, T-M11 Templates icon" | Filled 2x2 grid glyph with a hollow quarter |

## Fixes since the first review, checked

| First-review FIX | Now | Evidence |
|---|---|---|
| F2 T-M11 Enemy row without icon | fixed | Enemy row: bug icon x 14-25, label x 36-72 (app 36-73), chevron x 243-246 in both |
| F3 Button Primary / Danger Disabled label colour | fixed | label and icon peak #727375 in the app against #737375 in Figma, fills #2a4575 / #72323a against #2b4575 / #73323a; diff 0.28 to 2.32 % (was 5.22 to 7.74 %) |
| F5 Field Text value 5 px right | fixed | "value" at x 103-134 (Figma) and 103-135 (app); C-field-text 1.06 % (was 2.48 %) |
| F6 Template Card line break | fixed | second line "way platforms." at x 13-88 in both; first line ends after "one-" at x 237 / 238 |
| F7 Glyph Cell glyph 5 px low | fixed | glyph at y 11-21 (Figma) and 12-21 (app), x 16-25 in both |
| F8 Scene Card meta and art offset | fixed | Default: name x 9-62, meta x 125-157, art x 9-158 in both; Selected: 10-63, 126-158, 10-159 in both |

## Observations that apply to every item (not counted as differences)

1. **Text rasterisation** (as in the first review, observation 1). The app draws text with more ink than the Figma
   export (Inter labels look one step brighter and heavier when upscaled, mono text has up to about 1.5x the ink per
   column). Run starts and ends agree within 1 to 2 px, peak colours reach the same token (#e7e8eb, #a3a6ae, #6f727b,
   accent, status), and the same rendering is already accepted on the ticked components and menus (T-M03, Menu Item,
   Tab and others). It is not counted here; the user may want to look at it once at the P10.X1 exit gate.
2. **SM buttons are 1 px narrower on the right.** All 16 SM variants: the fill ends at x 76 (Primary, Ghost, Danger)
   or 78 (Secondary) where Figma ends at 77 / 79; the capture size is the same. Within 2 px, not counted.
3. **Corner anti-aliasing** of 2 px selection rings (Glyph Cell, Scene Card and Template Card Selected) differs by
   about 1 px on the arc; the straight edges are the same. Not counted.
4. **Glyph Cell Missing dash phase.** Both draw a 1 px border/strong (#4a4d55) dashed box with about 3 px dashes and
   2 px gaps; Figma spaces the dashes about 5.3 px apart on the top and bottom edges, the app exactly 5 px (`3,2`), so
   single dashes sit 0 to 2 px apart. Within 2 px, not counted.

## Decisions

### 4.5 Menus (16 unticked)

All 16 captures have the Figma board size, report offset 0 px. In every menu each row was compared element by element:
icon, label, shortcut, chevron and check mark positions agree within 2 px and their peak colours agree (labels #e7e8eb,
shortcuts #6f727b, icons #a3a6ae, Delete status/danger, check marks accent/default), separators are at the same y.
The only differences are the icons that the Editor Map board draws filled (U2).

| Item | Decision | Reason |
|---|---|---|
| T-M01 File menu | USER (U2) | 2.23 %, 0 px; New Project, Open Project, Save, Export ROM icons filled in Figma |
| T-M02 Edit menu | USER (U2) | 1.40 %, 0 px; Find in Project search icon filled in Figma |
| T-M04 Scene menu | USER (U2) | 3.11 %, 0 px; New Scene, Background Layers, World Map icons filled in Figma; check on Set as Start Scene the same |
| T-M05 Build menu | USER (U2) | 2.69 %, 0 px; Build ROM, Flash to Cartridge, Build Report icons filled in Figma; Play the same |
| T-M06 Tools menu | USER (U2) | 2.63 %, 0 px; Database, Localization, Dialogue Editor, Logic Graph, Debugger, Screenshot / GIF icons filled in Figma |
| T-M07 Help menu | USER (U2) | 3.22 %, 0 px; Getting Started, Scripting API, Report a Bug icons filled in Figma |
| T-M09 Create | USER (U2) | 2.07 %, 0 px; Sprite, Tileset / Tilemap, Sound Effect, Data Table, Logic Graph, Dialogue icons filled in Figma |
| T-M10 Add Actor | USER (U2) | 2.07 %, 0 px; Empty Actor, Player, Enemy, Pickup, Camera Zone icons filled in Figma |
| T-M11 Add to Scene | USER (U2, U4) | 2.14 %, 0 px; F2 fixed (table above); icons filled in Figma as T-M10 plus Prefab; Tilemap Layer is the filled grid with a hollow quarter (U4) |
| T-M13 Asset Context Menu | USER (U2) | 1.88 %, 0 px; Find Usages and Create Animation icons filled in Figma |
| T-M16 Animation List | USER (U2) | 1.15 %, 0 px; Edit Animations icon filled in Figma; check on idle the same |
| T-M17 Project Context Menu | USER (U2) | 2.18 %, 0 px; Open (folder) icon filled in Figma |
| T-M20 Add Layer | USER (U2) | 4.02 %, 0 px; Tile layer, Image layer, Collision-only layer icons filled in Figma; Rotate / scale the same |
| T-M22 Scene Context Menu | USER (U2) | 1.94 %, 0 px; Open (map) icon filled in Figma; check and Delete the same |
| T-M23 Add to Dialogue | USER (U2) | 1.16 %, 0 px; the Choice icon's three nodes filled in Figma |
| T-M24 Command | USER (U2) | 2.44 %, 0 px; Go to scene, Give item, Play sound, Wait, Shake camera icons filled in Figma |

### 4.16 Components (20 unticked)

Every variant of each component set was compared (104 variants). Numbers are the report's diff % and offset.

| Component | Decision | Reason |
|---|---|---|
| Button | USER (U1) | 32 variants, 0 px. F3 fixed (Disabled 0.18 to 2.32 %). Icon within 0.5 px and label within 1 px in all 32; Secondary and Ghost Default (SM, LG) glyph black in Figma (U1). Observation 2 (SM 1 px narrower) |
| Icon Button | USER (U1) | Default 6.04 %, glyph black in Figma (U1); the row's "3 px offset" is an alignment artefact: the glyph is at x 8-18 / 9-18, centroid 12.4 against 13.0. Active and Hover 5.91 %, Pressed 6.73 % (report 1 px), glyph #e7e8eb within 1 px; Disabled 0.00 % |
| Play Control | USER (U1) | Play 1.36 % (triangle within 1 px), Stop 0.00 %; Pause 4.71 % and Step 6.67 % only differ by the glyph colour (black in Figma, U1): same box x 13-20 and 12-21 in both |
| Layer Toggle | USER (U1) | On 6.39 %: eye glyph black in Figma (U1), "BG0" #e7e8eb at x 26-43 in both; Off 0.07 %, Active 2.98 % |
| Chip | USER (U1) | Default 4.23 %: chip glyph black in Figma (U1); Accent, Success, Warning, Danger, Script 0.28 to 1.46 %, glyph x 9-17 and label x 27-50 in both, status colours exact |
| Slider | USER (U3) | 11 values, 2.29 %, 0 px (value 0: 1 px); knob and fill positions identical (for example 50: knob x 75-84, fill to x 73). Only the knob ring differs. Note for U3: the Figma export is not a plain knob; it has a 1 px #c7ccd6 ring (the Light theme border/strong value), the app a 1 px dark-theme border/strong ring (#4e5158 after anti-aliasing) |
| Tree Item | USER (U1) | Default 1.26 %, Selected 1.13 %: chevron, icon and label within 1 px, label #e7e8eb, icon #6f727b / #cfe0ff; Hover 2.09 %: label "Actor" black in Figma (U1) |
| Doc Tab | USER (U1) | Default 4.01 %: map glyph black in Figma (U1); Active 4.27 %, Hover 3.88 %; icon x 15-25, label x 34-68 / 69 in all three |
| Panel Tab | USER (U1) | Default 3.85 %: folder glyph black in Figma (U1); Active and Hover 5.64 %; icon x 13-23, label x 31-68, underline the same |
| List Item | USER (U1) | Default 3.38 %: image glyph black in Figma (U1); Selected 3.35 %, Hover 4.72 %; icon x 9-19, label x 28-67, count x 139-148 in all three |
| Nav Item | USER (U1) | Default 1.70 %: sliders glyph black in Figma (U1); Active 2.50 %, Hover 2.70 %; icon x 11 / 12-22, label x 34-77 |
| Section Header | USER (U1) | Open and Closed 2.83 %: move glyph black in Figma (U1); chevron x 2-9 / 4-7 and label x 39-97 / 98 the same |
| Field | USER (U3) | Text 1.06 % (F5 fixed), Dropdown 1.06 %, XY 1.42 % (X / Y letters axis colours at the same x), Toggle 0.23 %; Slider 1.70 %: knob at x 162-173 in both, ring as Slider (U3) |
| Status Item | USER (U1) | Default 1.73 %: check glyph black in Figma (U1); Dot 1.42 %, dot and label within 1 px |
| Console Line | USER (U1) | Info 1.75 %: dots glyph black in Figma (U1); Success 0.53 %, Warning 1.00 %, Error 0.58 %; time, icon and every word at the same x |
| Scene Card | FIX (F10) | 1.31 / 1.66 %, 0 px; F8 fixed. New: the preview art is drawn with 1 px dark seams and greyer edges (F10) |
| Template Card | MATCH | 1.08 / 1.23 %, 0 px; F6 fixed; the art is pixel-identical inside its rounded corners (max channel difference 0); title, both description lines and the selection ring within 1 px; differences are text rasterisation and ring corner anti-aliasing |
| Glyph Cell | MATCH | Default 1.52 %, Selected 3.79 %, Missing 3.46 %, 0 px; F7 fixed (glyph y 12-21 against 11-21, IBM Plex Sans Thai Medium 18 as `Theme/Panels.axaml`); code "0E01" at x 10-33, y 31-38 in both; text/primary and text/muted; remaining pixels are glyph rasterisation, the Selected ring corners (observation 3) and the Missing dash phase (observation 4) |
| Panel Header | USER (U1) | 1.83 %: layers glyph black in Figma (U1); label x 33-99 / 100, plus at x 236-245, bottom border at y 33 in both |
| Dialog Header | USER (U1) | 1.21 % (report 1 px): folder-plus glyph black in Figma (U1); title 1 px higher in the app (y 18-32 against 19-33), close at x 605-614 / 613, border at y 51 |

### 4.17 T-R01

| Item | Decision | Reason |
|---|---|---|
| T-R01 App icon | PARTIAL (not ticked) | SHOT part matches: `C-brand-app-icon-*` 0.24 / 0.00 / 0.00 % (already ticked under brand/app-icon); the 128 px frame of `editor/AgbStudio.Editor/Assets/agb-studio.ico` (the exe `ApplicationIcon`, also the window and taskbar icon; byte-identical to `docs/brand/agb-studio.ico`) is pixel-identical to the Figma export `C-brand-app-icon-light.png`; the About icon in T-D11 (64 px) has 0 pixels over the threshold in a 72 x 72 box around it, at the same place (indigo centre x 48-57, y 100-109 in both). The item also names the installer, which does not exist in this snapshot (D-19 open, P16.01 open), so the box stays unticked, for example with `(blocked: installer per D-19)` as 2.1 allows |

## FIX list

**F10. Scene Card (Default, Selected): preview art drawn with seams and greyer edges.** The card draws `art/scene/day`
(240 x 160, the same file as the Template Card's `art/genre/platformer`) at 150 x 100, a 0.625 scale. Figma's export
of the card shows smooth band edges; the app does not:

- Sky band edge: expected #6abcff at y 41 (Selected y 42), between #58b4ff above and #7cc5ff below; actual a dark 1 px
  line #5d9aca across the whole art width (x 9-158; Selected x 10-159). This row alone is 150 of the differing
  pixels.
- Second band edge: expected #9bd6ff at y 60; actual #8bb8d8 (below the report threshold, still a visible darker
  line).
- Question block and coins: expected warm edges (block outline #825a13, bottom #7f4f0b, "?" #fce097, coin outline
  #887230 / #987318); actual greyer edges and a paler mark (block outline #76653b, bottom #826f49, "?" #e6e7c5, coin
  outline #838159 / #89793e).

Positions are right (sky x 9-158, y 9-40 in both). The Template Card shows the same art at scale 1 and matches pixel
for pixel, so the cause is drawing the vector art at a fractional scale (each shape anti-aliased on its own, so two
shapes meeting in the middle of a pixel let the darker colour through). The windows review saw the same seams in the
T-P20 template preview (`review-shot-windows.md`, T-P20). No Scope-log row records it. Expected vs actual images:
`docs/evidence/figma/C-scene-card-default.png`, `docs/evidence/app/C-scene-card-default.png` (and `-selected`). Ways
out: draw the art at its own size into a bitmap and scale that down, or accept it as a rendering note with a
Scope-log row. The same `SceneCard` control is used on the World Map (`WorldMapPage.cs`) and for the Launcher
samples (`LauncherPage.cs`), so a fix there reaches those windows too.

## USER list (by Scope-log row)

| Row | Items |
|---|---|
| U1, line 1459 (black glyphs in component masters) | Button, Icon Button, Play Control, Layer Toggle, Chip, Tree Item, Doc Tab, Panel Tab, List Item, Nav Item, Section Header, Status Item, Console Line, Panel Header, Dialog Header (15) |
| U2, line 1463 (menu icon fill) | T-M01, T-M02, T-M04, T-M05, T-M06, T-M07, T-M09, T-M10, T-M11, T-M13, T-M16, T-M17, T-M20, T-M22, T-M23, T-M24 (16) |
| U3, line 1464 (slider knob ring) | Slider, Field (2) |
| U4, line 1480 (Templates icon) | T-M11 (also under U2) |

Two corrections to the pending rows' wording, so the user decides on what the images show:

- U1: the "3 px offset" on `C-icon-button-default` and `C-play-control-step` is the alignment step having nothing to
  match against a black glyph; the glyphs themselves are within 1 px (centroid 12.4 / 13.0 and 15.7 / 15.6).
- U3: "the Figma export shows a plain 12 px white knob" is not quite right: the export has a 1 px #c7ccd6 ring, which
  is the Light theme's border/strong value, on the dark theme. The choice is therefore between that light ring as
  exported and the theme's border/strong ring (what the app draws and the checklist heading says).

## Counts

| Group | MATCH | USER | FIX | PARTIAL | Total |
|---|---|---|---|---|---|
| 4.5 Menus | 0 | 16 | 0 | 0 | 16 |
| 4.16 Components | 2 | 17 | 1 | 0 | 20 |
| 4.17 T-R01 | 0 | 0 | 0 | 1 | 1 |
| Total | 2 | 33 | 1 | 1 | 37 |

Evidence lines for the two MATCH items are in `docs/evidence/evidence-shot-components-2.tsv`.
