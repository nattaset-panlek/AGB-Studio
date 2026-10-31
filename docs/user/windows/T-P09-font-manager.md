# Font Manager

Figma: T-P09 · Page

> สร้างฟอนต์สำหรับ GBA จาก TTF / BDF / ภาพ sprite sheet รองรับภาษาไทยแบบ variable width วางสระบน-ล่างและวรรณยุกต์ซ้อนให้ถูกตำแหน่ง ตัดคำภาษาไทยอัตโนมัติ และพรีวิวในกล่องข้อความจริง

Make GBA fonts from TTF, BDF or a sprite-sheet image. Thai is supported with variable-width letters, upper and lower
vowels and stacked tone marks in the right place, automatic Thai word wrap, and a preview in a real text box.

## Opens from

- Double-click a font asset (#26)
- Tools › Font Manager
- Create › Font (opens this page with the New Font dialog on top; the new font is shown when it is made)

## What is on it

- **Toolbar**: **Pencil**, **Eraser** and **Pick shade** for editing glyph pixels, and **Pixel grid** to show or hide
  the grid. A chip sums up the font, for example "VWF · 12 px · 2bpp". Play, the video mode chip and undo / redo are on
  the right.
- **Fonts (left)**: every font in the project, the default font first, with its size and whether it is variable width
  (VWF) or fixed. Click one to open it. **+** opens New Font.
- **Character sets**: tick **Basic Latin**, **Thai**, **Symbols** or **Japanese kana** to include them; each shows how
  many characters it adds.
- **Add characters from text…**: opens Add Characters for the open font.
- **Glyphs**: the characters of one group with their code points. Click the group name to switch group. Missing
  glyphs have a dashed outline. Click a glyph to edit it.
- **Glyph detail**: the selected glyph's pixels on a large grid with the baseline marked, its code point and its
  advance. Draw with the toolbar tools; the change is saved in the `.font` file.
- **Preview · Dialogue box on GBA**: a dialogue box drawn the way the game draws it, on the current scene.
- **Source**: **From** (the TTF, BDF or image file in the project's fonts folder), **Size** in pixels (4 to 64) and
  **Width** (**Fixed 8×8** or **Variable**).
- **Metrics**: **Height · base** (glyph height and baseline) and **Spacing · line** (space between letters and line
  height).
- **Thai layout**: **Stack upper vowels & tone marks**, **Place lower vowels** and **Word-wrap Thai by dictionary**.
  Thai has no spaces between words, so line breaks use a word list and never split a word.
- **Rendering**: **Colours** (2bpp, 4 shades, or 1bpp, 2 shades), **Drop shadow**, and **Draw to** (the BG text layer
  the font draws on).
- **Meters**: the font's ROM size and the VRAM the text box uses (of 16 KB).
- **Status bar**: glyph count and how many are missing, the ROM size, and "Saved".
- Every change can be undone and is saved to the `.font` file.

## Related

- [New Font](T-D15-new-font.md)
- [Add Characters](T-D16-add-characters.md)
- [Localization](T-P14-localization.md)
- [Dialogue Editor](T-P16-dialogue-editor.md)
- [Project Settings · General](T-S01-project-settings-general.md)
- [GBA hardware guide: Memory](../gba-hardware-guide.md#memory)
- [GBA hardware guide: Tiles and maps](../gba-hardware-guide.md#tiles-and-maps)
