# Palette Manager

Figma: T-P10 · Page

> จัดการ palette ของ GBA (BG 16 ชุด + OBJ 16 ชุด ชุดละ 16 สี) ดูว่า sprite ไหนใช้ชุดไหน รวมสีที่ซ้ำ และตั้ง effect เช่น fade / cycle สีน้ำ

Manage the GBA palettes (16 background sets and 16 sprite sets, 16 colours each): see which sprite uses which set,
merge duplicate colours, and set effects such as a fade or a cycling water colour.

## Opens from

- The Palette button of a Sprite component in the Inspector (#30)
- Assets › Optimize Palettes… (first removes unused and near-duplicate colours in every palette, then opens this page)
- Tools › Palette Manager
- Create › Palette (adds a new sprite palette and shows it)
- Sprite Editor › Palette slots

## What is on it

- **Toolbar**: **Select** and **Pick colour** tools, and a chip counting palettes in use (for example
  "BG 3 / 16 · OBJ 3 / 16"). Play, the video mode chip and undo / redo are on the right as on other pages.
- **Palette Banks (left)**: two groups, Background (BG) and Sprites (OBJ), each with its palettes and how many colours
  each uses. Click a group to fold it, click a palette to select it. **+** adds a new palette of the same kind.
- **Selected palette**: its name, the files that use it ("used by hero.png, heart.png"), and its 16 colours. Click a
  colour to edit it. Index 0 is the transparent colour while **Lock index 0 as transparent** is on.
- **Background palette RAM / Sprite palette RAM**: all 256 + 256 colours as the GBA holds them. Click a colour to
  select that palette and colour. With the Pick colour tool, a click copies that colour into the selected slot.
- **Fade preview**: a sample scene at three steps of the fade effect, with the percentage of each step.
- **Colour panel (right)**: R, G and B from 0 to 31, the hex colour and the 15-bit value the ROM stores, with a note
  that GBA colours are 15-bit so a colour is stored as the nearest of 32 levels.
- **Tools**: **Merge near-duplicate colours**, **Sort by hue** and **Lock index 0 as transparent** for the selected
  palette. **Import .pal / .gpl** loads colours from a palette file; **Export** saves the palette as a `.pal` file.
- **Palette effects**: **Fade** and **Cycle** open the Palette Effect popover. **Flash on hit** turns the flash effect
  on or off.
- **Status bar**: the selected palette and how many of its 16 colours are used, and "Palette RAM 1 KB".
- Changes are saved with File › Save, and every change can be undone.

## Related

- [Palette Effect](T-O10-palette-effect.md)
- [Sprite Editor](T-P08-sprite-editor.md)
- [Tilemap Editor](T-P03-tilemap-editor.md)
- [Import Asset](T-D04-import-asset.md)
- [Debugger · Palettes](T-P24-debugger-palettes.md)
- [GBA hardware guide: Palettes](../gba-hardware-guide.md#palettes)
- [GBA hardware guide: Effects](../gba-hardware-guide.md#effects)
