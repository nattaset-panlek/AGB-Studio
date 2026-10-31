# Import Asset · Errors

Figma: T-D24 · Dialog

> สถานะนำเข้าไม่ผ่าน: บอกปัญหาของแต่ละไฟล์เป็นภาษาคน (สีเกิน 16 สี ไฟล์ชนิดที่ไม่รองรับ) พร้อมทางแก้ให้กดได้ทันที

The import-failed state: it explains each file's problem in plain words (too many colours, a file type that is not
supported) and gives fixes you can click straight away.

## Opens from

- Import Asset when a file can't be converted (pressing Import while any file still has a problem)

## What is on it

- **Title**: "Import Asset · N problems", counting the files that still need a fix or have an error.
- **One card per file**: the file name and what it was detected as, with an icon: a warning for a problem you can
  fix, a red cross (and red outline) for an error, a tick for a file that is ready.
- **Message**: the problem in plain words, for example "Uses 48 colours. A GBA sprite palette holds 16, so it needs
  reducing or splitting", "MP3 is not supported on GBA. Use .wav for sound effects or .mod / .xm for music", or for
  ready files how they will be converted ("Ready · resampled to 11 kHz mono 8-bit (9 KB)").
- **Fix buttons**:
  - **Reduce to 16**: reduces the image to 16 colours. The card then shows it as ready.
  - **Split into N palettes**: makes one sprite per group of 15 colours, each with its own palette, drawn on top of
    each other.
  - **How to convert**: explains how to turn the file into `.wav` (sound effect, 11 kHz mono) or `.xm` / `.mod`
    (music) with another program.
- **Footer**: the hint "Files with errors are skipped." and **Import N files**, which converts only the ready files and
  shows "Imported N files". **Cancel** closes without importing; while the files are being converted it stops the
  import and leaves the project as it was, as in [Import Asset](T-D04-import-asset.md).

## Related

- [Import Asset](T-D04-import-asset.md)
- [Sprite Editor](T-P08-sprite-editor.md)
- [Palette Manager](T-P10-palette-manager.md)
- [Audio](T-P15-audio.md)
- [GBA hardware guide: Palettes](../gba-hardware-guide.md#palettes)
- [GBA hardware guide: Sound](../gba-hardware-guide.md#sound)
