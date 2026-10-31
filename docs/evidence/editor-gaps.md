# Editor gaps found while writing the window pages (2026-10-31)

Found by reading the code of branch `p11-integrate` against Figma captions and the checklist "from" lists. Each one
blocks the "matches Figma" or "implemented" box of its window until fixed or accepted as a scope-log entry.

## Behaviour that Figma shows but the code does not do

| Window | Gap |
|---|---|
| T-P01 Scene Editor | Paint tiles, Fill and Erase only highlight the tool; clicks in the scene view always select and move actors |
| T-P05 Console | the game's own log lines (Debug.Log) go to Play Mode's console only; Figma shows them in the Console panel |
| T-P06 Animation panel | speed and frame order can only be previewed, not changed |
| T-P08 Sprite Editor | the drop zone says PNG / Aseprite but only PNG is imported |
| T-P19 Debugger | breakpoints cannot be added, so the list is always empty; OAM sprites, Profiler and Breakpoints views show the VRAM tiles screen |
| T-P22 | the caption mentions choosing a template; there is no template button |
| T-P24 | "Used by" does not name the palette effect |
| T-D04 Import Asset | `.aseprite` not detected; Sprite Editor drop zone and Palette Manager import do not open Import Asset |
| T-D07 Flash to Cartridge | cartridge line shows the game code, not the flash and save sizes |
| T-D09 Preferences · Emulator | settings are saved but Play Mode ignores them |
| T-D10 Keyboard Shortcuts | lists ten shortcuts, not all; no link to Preferences › Shortcuts |
| T-S02 Save data | every int field shows 4 B (Figma shows 1 B and 2 B fields) |
| T-S04 Scripting | "Show inline hints" has no effect |
| T-S06 Appearance | "Reduce motion" and "Show pixel grid" have no effect |
| T-S07 Editor | default snap, collision overlay, GBA budget in Inspector, code editor font and theme have no effect |
| T-S08 Paths | projects folder, image editor and Tiled have no effect |
| T-O01 Scene Switcher | no "boss" tag on scene rows |
| T-O04 Asset Picker | Tilemap Editor's Tileset dropdown opens the Sprites tab (`TilemapEditorPage.cs` passes "Tileset", the picker knows "Tilesets") |
| T-O07 | picking a `.scene` file does not switch to that scene |
| T-O08 Add Node | Logic Graph › Nodes › Add node… is a search box in the Nodes panel, not this popover |

## Other code findings

- Pause and Step in the title bar do nothing on 15 pages (T-P03, P04, P07-P11, P13-P18, P22, P25); they work in the
  Scene Editor and Play Mode.
- Script Editor "Analyzer: GBA rules on" chip opens Project Settings on Input instead of Scripting (`ScriptEditorPage.cs`).
- Unsaved Changes always says "Save changes before exiting?", also when opening another project.
- Create › Tileset / Tilemap opens the current map instead of making a new one; Create › Font opens Font Manager, not New
  Font.
- Logic Graph: the Link tool has no action; the Preview is a still picture.

## Accepted by decision

- T-D19 Import CSV: Google Sheets is CSV export only (D-13).
- T-D07 GBxCart RW: writing is refused until verified on hardware (D-12, `HW`).
