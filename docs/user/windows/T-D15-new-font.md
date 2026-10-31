# New Font

Figma: T-D15 · Dialog

> สร้างฟอนต์ใหม่จากไฟล์ TTF / BDF หรือภาพ sprite sheet เลือกขนาด ความกว้างแบบตายตัวหรือแปรผัน และชุดตัวอักษร

Make a new font from a TTF or BDF file or a sprite-sheet image, and choose its size, fixed or variable width, and
which characters it holds.

## Opens from

- Font Manager › Fonts +

## What is on it

- **Source**: choose the kind of file: **TTF / OTF**, **BDF** or **Image sheet**.
- **File**: the source file, from the project's fonts folder. For TTF / OTF the bundled Thai font
  `NotoSansThai-Regular.ttf` is offered too, and is picked first.
- **Size**: the height in pixels (4 to 64; 12 px to start).
- **Width**: **Variable (VWF)**, where each letter has its own width, or **Fixed 8×8**.
- **Characters**: tick **Latin**, **Thai** and **Symbols**; each shows how many characters it adds.
- **Preview**: the sample text "สวัสดี AGB Studio 0123" drawn with the font these settings make, with its size, colour
  depth and about how much ROM it takes.
- **Create Font**: writes the new `.font` file (copying the bundled Thai font into the project when it is used) and
  opens it in the Font Manager. Nothing happens when no source file is chosen. **Cancel** closes without changes.

## Related

- [Font Manager](T-P09-font-manager.md)
- [Add Characters](T-D16-add-characters.md)
- [GBA hardware guide: Memory](../gba-hardware-guide.md#memory)
