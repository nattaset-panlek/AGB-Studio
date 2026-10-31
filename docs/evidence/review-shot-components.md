# Review: SHOT for menus, components, type and brand (2026-10-31)

Independent read-only review of the "matches Figma" boxes in `docs/ENGINE-CHECKLIST.md` 4.5 (menus T-M01..T-M24),
4.16 (47 components), the SHOT parts of 4.15 (T-K02, T-K05) and 4.17 (T-R01, T-R02), and P10.03 (type specimen), at
`c407d74`. The reviewer wrote none of the UI. Nothing was built, run or exported from Figma: the review uses the
images already in the repo (`docs/evidence/figma/<id>.png`, `docs/evidence/app/<id>.png`, `docs/evidence/diff/<id>.png`,
`docs/evidence/figma/type/`, `docs/brand/`) and `docs/evidence/shot-report.md` / `.json`.

Method (SHOT procedure, section 2.5): every diff image was looked at side by side, upscaled 2x to 10x; on top of the
report numbers, pixel colours were compared with `docs/evidence/figma/tokens.json`, and the position of every text
run and glyph was measured in both images, because the report's single best shift for the whole image hides a
local offset (one label moving 5 px changes the whole-image diff by about 1 %). Tolerances: layout within 2 px, colours
from tokens, same font, icons, texts and states. The measuring scripts are not part of the repo.

Decisions:

- **MATCH**: same within the SHOT tolerances; the box can be ticked. The evidence line is in
  `docs/evidence/evidence-shot-components.tsv`.
- **USER**: the only difference is described by a pending Scope-log row (section 8); the user's decision closes it.
- **FIX**: a difference that no Scope-log row records. "FIX (record)" marks the ones caused by the Figma export
  itself (see F9): they need a Scope-log entry, not a code change.
- **PARTIAL**: the SHOT part matches, but something else the item asks for is not evidenced.
- **MISSING**: no image to compare (none in this scope).

## Observations that apply to every item (not counted as differences)

1. **Text rasterisation.** The app draws text with about 25 to 35 % more ink than Figma's export (measured on the 17
   type specimens). Font family, weight, size, line height and letter spacing are the same (`Theme/TextStyles.cs`
   against `tokens.json` › textStyles, all 17 sampled styles equal), glyph advances are the same (text runs start and
   end within 1 to 2 px), and the brightest pixels reach the token colour in both. This is the main source of the
   1 to 10 % component diffs and the 3 to 29 % type-specimen diffs. It is not counted as a font difference here; the
   user may want to look at it once at the P10.X1 exit gate.
2. **Parts that only appear through a property** (Doc Tab dirty dot, Panel Tab count, Tree Item and Section Header
   meta, Text Input and Select help text, Menu Item icon) are not visible in the exported component variants; they
   are compared where the windows and menus show them.
3. **Menus in Figma draw closed-outline icons filled** (F1), while the icon components (`icon/*`) and every Screens
   frame draw them as outlines. This is Figma contradicting itself.
4. **Component masters in Figma draw some glyphs and one label black** (F9). The Screens frames draw the same
   glyphs grey (for example Pause and Step in T-P02, the Hierarchy and Doc Tab icons in T-P01), so the app follows
   the Screens frames.

## Decisions

### 4.5 Menus (Editor Map boards 2 and 4)

All 24 menu captures are 264 px wide and the same height as the Figma board frame; report offset 0 px for all.

| Item | Decision | Reason |
|---|---|---|
| T-M01 File menu | FIX | F1: New Project, Open Project, Save and Export ROM icons are filled in Figma, outline in the app; texts, shortcuts and separators match (2.23 %) |
| T-M02 Edit menu | FIX | F1: Find in Project search icon filled in Figma; the rest matches (1.40 %) |
| T-M03 Assets menu | MATCH | 2.26 %, 0 px; Import and Optimize Palettes icons are the same, Create submenu chevron the same, differences are text anti-aliasing |
| T-M04 Scene menu | FIX | F1: New Scene, Background Layers and World Map icons filled in Figma |
| T-M05 Build menu | FIX | F1: Build ROM, Flash to Cartridge and Build Report icons filled in Figma (Play icon the same) |
| T-M06 Tools menu | FIX | F1: Database, Localization, Dialogue Editor, Logic Graph, Debugger and Screenshot / GIF icons filled in Figma |
| T-M07 Help menu | FIX | F1: Getting Started, Scripting API and Report a Bug icons filled in Figma |
| T-M08 Open Recent | MATCH | 2.16 %, 0 px; check mark, three recent projects, separator, Clear Recent |
| T-M09 Create | FIX | F1: Sprite, Tileset / Tilemap, Data Table, Logic Graph and Dialogue icons (and others) filled in Figma |
| T-M10 Add Actor | FIX | F1: Empty Actor, Player, Enemy, Pickup and Camera Zone icons filled in Figma |
| T-M11 Add to Scene | FIX | F1 as T-M10 plus Tilemap Layer and Prefab; F2: the Enemy row has no icon in the app and its label starts at x 13 instead of 35 |
| T-M12 Actor Context Menu | MATCH | 1.82 %, 0 px; plus and code icons the same, Delete in status/danger |
| T-M13 Asset Context Menu | FIX | F1: Find Usages and Create Animation icons filled in Figma |
| T-M14 Component Menu | MATCH | 2.15 %, 0 px |
| T-M15 Zoom | MATCH | 1.62 %, 0 px; check marks on 300 % and Pixel-perfect in both |
| T-M16 Animation List | FIX | F1: Edit Animations icon filled in Figma |
| T-M17 Project Context Menu | FIX | F1: Open (folder) icon filled in Figma |
| T-M18 Play Speed | MATCH | 0.78 %, 0 px; Frame step icon the same |
| T-M19 OAM Size | MATCH | 3.70 %, 0 px; all six rows (Square x4, Wide, Tall) the same text, check on 16x16 |
| T-M20 Add Layer | FIX | F1: Tile layer, Image layer and Collision-only layer icons filled in Figma |
| T-M21 Node Context Menu | MATCH | 2.05 %, 0 px |
| T-M22 Scene Context Menu | FIX | F1: Open (map) icon filled in Figma |
| T-M23 Add to Dialogue | FIX | F1: the Choice icon's three nodes are filled in Figma (small, but an icon difference) |
| T-M24 Command | FIX | F1: Go to scene, Give item, Wait and Shake camera icons filled in Figma |

### 4.16 Components (47)

Numbers are the report's diff % per variant and its offset; every variant of the Figma component set was compared
(the manifest has 215 variants, all rendered).

| Component | Decision | Reason |
|---|---|---|
| Menu Item | MATCH | 7 variants (the Kind x State pairs Figma has), 0.29 to 2.62 %, 0 px; text/primary, text/on-selected on accent/selected, text/muted, status/danger reach their token values |
| Menu Bar Item | MATCH | Default 1.04 %, Open 3.01 %, 0 px |
| Button | FIX | F3: Primary and Danger Disabled (SM, LG) label and icon colour; F9 (record): Secondary and Ghost Default (SM, LG) glyph black in Figma. The other 24 variants match (3.2 to 10.4 %, all text anti-aliasing, 0 px) |
| Icon Button | USER | Default: Figma glyph black and the 3 px offset, pending row "C-icon-button-default, C-play-control-step" (2026-10-31); Active, Hover, Pressed 1 px, Disabled 0.00 % |
| Play Control | FIX | F9 (record): Pause glyph black in Figma, not in the pending row; Step is USER (same row as Icon Button); Play 1.36 %, Stop 0.00 % |
| Layer Toggle | FIX | F9 (record): On, eye glyph black in Figma; Off 0.07 %, Active 2.98 % match |
| Chip | FIX | F9 (record): Default, chip glyph black in Figma; Accent, Success, Warning, Danger, Script 0.28 to 1.46 % match |
| Switch | MATCH | On and Off 0.00 % |
| Checkbox | MATCH | 5.52 / 5.73 %: label anti-aliasing; 14 px box with 1.5 px border/strong, check on accent/default, label clipped at 60 px in both |
| Segment | MATCH | 1.60 to 4.93 %, 0 px |
| Progress | MATCH | 44 variants (OK, Warn, Danger, Accent x 0..100), all 0.00 % |
| Slider | FIX | F4: the app draws a 1 px border/strong ring inside the knob; the Figma export has a plain 12 px white knob (all 11 values) |
| Tree Item | FIX | F9 (record): Hover (None, Open, Closed) label "Actor" black in the Figma export; Default and Selected (6 variants) 1.13 to 1.26 % match |
| Doc Tab | FIX | F9 (record): Default, map glyph black in Figma; Active and Hover match |
| Panel Tab | FIX | F9 (record): Default, folder glyph black in Figma; Active and Hover match |
| List Item | FIX | F9 (record): Default, image glyph black in Figma; Selected and Hover match |
| Nav Item | FIX | F9 (record): Default, sliders glyph black in Figma; Active and Hover match |
| Section Header | FIX | F9 (record): Open and Closed, move glyph black in Figma; chevron and label match |
| Field | FIX | F5: Text, value text 5 px right of Figma; F4: Slider, knob ring as Slider; Dropdown, XY, Toggle match |
| Swatch | MATCH | 0.00 to 4.78 %, corner anti-aliasing only |
| Status Item | FIX | F9 (record): Default, check glyph black in Figma; Dot 1.42 % matches |
| Console Line | FIX | F9 (record): Info, dots glyph black in Figma; Success, Warning, Error 0.53 to 1.00 % match |
| Toast | MATCH | Success and Error 4.41 %, 0 / 1 px |
| Asset Card | MATCH | 3.10 / 3.86 %; art, name and meta within 1 px |
| Frame Thumb | MATCH | 1.13 / 3.16 %; art and label within 1 px |
| Scene Card | FIX | F8: Selected, meta "30x20" 4 px left of Figma and the art 2 px left; Default meta 2 px left |
| Template Card | FIX | F6: the description breaks after "slopes," in the app and after "one-" in Figma (both variants) |
| Glyph Cell | FIX | F7: the Thai glyph is 5 px lower in the app (all three variants) |
| Table Cell | MATCH | Header, Default, Mono, Selected 1.64 to 2.81 %, 0 px |
| Text Input | MATCH | 1.37 / 6.57 %; the Focus ring is 2 px solid accent/default in the app and 1.5 px in Figma (half a pixel, within 2 px); value text at the same x |
| Select | MATCH | 1.69 / 6.93 %; same Focus ring note as Text Input |
| Tab | MATCH | 1.74 / 7.50 %, text anti-aliasing; accent underline the same |
| Node Header | MATCH | 8 categories 1.57 to 1.92 %, icon colours and header fills the same |
| Node Pin | MATCH | 10 variants 0.00 % |
| brand/app-icon | MATCH | Light 0.24 %, Dark 0.00 %, Indigo 0.00 % |
| Menu Separator | MATCH | 0.00 % |
| Badge | MATCH | 1.84 % |
| Keycap | MATCH | 3.07 % |
| Panel Header | FIX | F9 (record): layers glyph black in Figma; label and plus button match |
| Search Field | MATCH | 0.40 % |
| Meter | MATCH | 2.29 % |
| Tooltip | MATCH | 3.53 % |
| Title Bar | MATCH | 2.58 %; menu titles, breadcrumb, Debug and Build within 1 px, no brand text |
| Dialog Header | FIX | F9 (record): folder-plus glyph black in Figma; title and close match |
| Dialog Footer | MATCH | 2.74 %, buttons 1 px |
| Popover Header | MATCH | 1.61 % |
| brand/mark | MATCH | 0.00 % |

### 4.15, 4.17 and P10.03

| Item | Decision | Reason |
|---|---|---|
| T-K02 text styles | MATCH | SHOT: 17 specimens (`docs/evidence/diff/type/`), offset 0 to 1 px, same text, same token colour, same font (observation 1); UT: `TokenTests.TextStyles_MatchFigma` (all 20 styles; its expected values were checked against `tokens.json` for this review, and it was in the green CI run on `ddf96c0` that commit `2c96c1b` names). Note below |
| T-K05 art and thumbnails | PARTIAL | Art inside the components matches (hero sprite in Asset Card and Frame Thumb, scene art in Template Card; Scene Card is F8 for its meta text, not the art); the `REV` "original design work" is not evidenced (`legal-review.md` leaves it to T-K05), and SHOT on the windows that show the art is the windows review |
| T-R01 app icon | PARTIAL | SHOT matches everywhere the icon exists: `C-brand-app-icon-*` 0.24 / 0.00 / 0.00 %, About icon region of T-D11 0.00 % at 0 px, `docs/brand/agb-studio.ico` 128 px frame identical to the Figma export (exe `ApplicationIcon`, window and taskbar icon). The item also names the installer, which does not exist yet (D-19 open) |
| T-R02 wordmark | MATCH | T-P12 brand block: "AGB Studio" spans x 64-157, y 70/71-83 in both; Nunito ExtraBold (`LauncherPage.cs`); Plus Pad icon at the same place, 0 px |
| P10.03 type specimen | MATCH | Same evidence as the SHOT part of T-K02. Note below |

Note on T-K02 and P10.03: Header 1, Header 2 and Body have no sample in Figma (no text node uses them) and a mixed
font in the Figma style; the app gives them Inter Regular (`Theme/TextStyles.cs`) and no window uses them, so
nothing visible can differ. That choice is not in the Scope log; adding a row for it is recommended, it does not
block the SHOT.

### Not in this scope

- 4.12 and 4.13 (T-B01..T-B35, T-C01..T-C52): Verify is `E2E` only and there is no "matches Figma" box.
- T-K01, T-K04 (`UT`), T-K03 (`REV`), T-R03 (`REV`), T-R04 (`RT`, `USER`): no SHOT.

## FIX list

**F1. Menu icons: filled in Figma, outline in the app** (T-M01, T-M02, T-M04, T-M05, T-M06, T-M07, T-M09, T-M10,
T-M11, T-M13, T-M16, T-M17, T-M20, T-M22, T-M23, T-M24).
Expected (Editor Map board export): icons whose outline is a closed path are drawn as filled silhouettes in
text/secondary grey: folder, folder-plus, save, chip, search, map, layers, bolt, database, globe, book, star, bug,
cube, user, coin, camera, image, tiles, film, square, clock, the graph nodes. Open-path icons (upload, sliders, plus,
code, undo, redo, play, frame step) look the same in both. Actual: the app draws every icon as the outline of its
`icon/*` component (`editor/AgbStudio.Editor/Assets/Icons/*.svg`, stroke only). The icon components and every Screens
frame use outlines too, so this is Figma contradicting itself (section 2.6). Needs a Scope-log row and the user's
choice: keep outlines (the icon set), or fill those icons inside menus.

**F2. T-M11 Add to Scene, Enemy row has no icon.** Expected (`docs/evidence/figma/menus.json`: Submenu, icon bug):
bug icon, label at x 35, chevron. Actual: no icon, label at x 13, chevron. `SubmenuEntry("T-M11.Enemy", "Enemy",
Enemies)` in `editor/AgbStudio.Editor/Windows/SceneEditor/SceneCommands.cs` (line 85) has no glyph.

**F3. Button, Primary and Danger Disabled (SM, LG): label and icon colour.** Ids `C-button-primary-sm-disabled`,
`C-button-primary-lg-disabled`, `C-button-danger-sm-disabled`, `C-button-danger-lg-disabled`. Expected #737375
(Figma dims the whole button to 40 %: white over bg/base). Actual #7f8fac (Primary) and #ab8488 (Danger): the label is
dimmed on top of the already dimmed fill, so it is lighter and tinted (up to 55 per channel). The fill colours match
(#2b4575, #73323a). Theme: `Theme/Buttons.axaml`, `^:disabled` Opacity 0.4.

**F4. Slider knob ring** (Slider, 11 values; Field Slider). Expected: a 12 px white knob with no dark edge in the
export. Actual: a 1 px border/strong (#4a4d55) ring, white area 10 px. The checklist heading says "knob with
border/strong ring", so the checklist text and the Figma export disagree: remove the ring, or record the ring in the
Scope log with the user's decision.

**F5. Field, Text variant: value text 5 px right.** Expected "value" at x 104-134 (the Dropdown variant is at 103-135
in both). Actual x 109-141. Probably the TextBox's inner padding.

**F6. Template Card (Default, Selected): description line break.** Expected "Run, jump and collect. Gravity, slopes,
one-" / "way platforms.". Actual "Run, jump and collect. Gravity, slopes," / "one-way platforms." (the app does not
break after the hyphen).

**F7. Glyph Cell (Default, Selected, Missing): glyph 5 px low.** Expected the Thai glyph at y 12-21. Actual y 17-26.
The code line "0E01" matches (y 31/32-38).

**F8. Scene Card, Selected: meta text 4 px left.** Expected "30x20" ending at x 158 and the art at x 10-159. Actual
the text ends at x 154 and the art is at x 8-157 (the Default variant is 2 px off: 157 against 155). The name
"Forest_01" is within 1 px.

**F9 (record). Figma component masters draw a glyph or label black.** `C-button-secondary-sm-default`,
`C-button-ghost-sm-default`, `C-button-secondary-lg-default`, `C-button-ghost-lg-default`, `C-play-control-pause`,
`C-layer-toggle-on`, `C-chip-default`, `C-tree-item-hover-none`, `C-tree-item-hover-open`, `C-tree-item-hover-closed`
(the label), `C-doc-tab-default`, `C-panel-tab-default`, `C-list-item-default`, `C-nav-item-default`,
`C-section-header-open`, `C-section-header-closed`, `C-status-item-default`, `C-console-line-info`, `C-panel-header`,
`C-dialog-header`. Expected (export): the glyph or label in black. Actual: text/secondary or text/primary, as the same
elements are drawn in the Screens frames. Same cause as the pending row for `C-icon-button-default` and
`C-play-control-step`; extending that row to these ids (user decision) closes them, no code change.

## USER list

| Item | Scope-log row |
|---|---|
| Icon Button (`C-icon-button-default`) | 2026-10-31, build session, "C-icon-button-default, C-play-control-step": 3 px offset, the Figma exports draw the glyph as black with alpha only; proposed "Accept (export artefact)"; pending |
| Play Control, Step variant (`C-play-control-step`) | the same row; the component stays FIX because of Pause (F9) |

## Counts

| Group | MATCH | USER | FIX | PARTIAL | MISSING | Total |
|---|---|---|---|---|---|---|
| 4.5 Menus | 8 | 0 | 16 | 0 | 0 | 24 |
| 4.16 Components | 27 | 1 | 19 (13 of them record only) | 0 | 0 | 47 |
| 4.15 T-K (SHOT) | 1 | 0 | 0 | 1 | 0 | 2 |
| 4.17 T-R (SHOT) | 1 | 0 | 0 | 1 | 0 | 2 |
| P10.03 | 1 | 0 | 0 | 0 | 0 | 1 |
| Total | 38 | 1 | 35 | 2 | 0 | 76 |

Components that need a code change or a design decision (6): Button (F3, also F9), Slider (F4), Field (F4, F5),
Scene Card (F8), Template Card (F6), Glyph Cell (F7). Record only, F9 (13): Play Control, Layer Toggle, Chip, Tree
Item, Doc Tab, Panel Tab, List Item, Nav Item, Section Header, Status Item, Console Line, Panel Header, Dialog Header.
Menus: F1 for all 16, F2 for T-M11 as well.

## Seen in passing (windows review)

- T-P12 Launcher brand block: the app says "v1.0 · for Game Boy Advance", Figma "v0.1"; the app's own status line
  above it says "v0.1".
- T-D11 About: "Version 1.0.0" in the app, "0.1.0" in Figma.
- In this snapshot no "implemented" box of 4.5 or 4.16 is ticked.
