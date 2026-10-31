# Add Characters

Figma: T-D16 · Dialog

> วางข้อความจากเกม (บทพูด เมนู) ระบบหาตัวอักษรที่ฟอนต์ยังไม่มี แล้วสร้าง glyph ให้จากฟอนต์ต้นฉบับ

Paste text from your game (dialogue, menus); AGB Studio finds the characters the font does not have yet and makes
glyphs for them from the source font.

## Opens from

- Font Manager › Add characters from text…

## What is on it

- **Paste text from your game**: a text box for dialogue lines, menu text or anything else the game shows.
- **Missing characters**: a warning such as "7 characters are not in thai_vwf yet", followed by each missing
  character with its code point. Hidden when nothing is missing.
- **Render the new glyphs from** the font's source file (on by default): the new glyphs are drawn from that file.
  When it is off, empty glyphs are added for you to draw by hand in the Font Manager.
- **Also scan all dialogue and localization strings**: adds every line from the project's dialogue files and
  localization tables to the check.
- **Add N glyphs**: adds the missing characters to the font in one step (it can be undone) and selects the first new
  glyph in the Font Manager. It does nothing when no characters are missing. **Cancel** closes without changes.

## Related

- [Font Manager](T-P09-font-manager.md)
- [New Font](T-D15-new-font.md)
- [Dialogue Editor](T-P16-dialogue-editor.md)
- [Localization](T-P14-localization.md)
