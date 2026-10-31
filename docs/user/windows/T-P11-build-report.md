# Build Report

Figma: T-P11 · Page

> สรุปผลการ build: ขนาด ROM แยกตามประเภท, asset ที่ใหญ่ที่สุด, การใช้หน่วยความจำ, คำเตือน และประวัติการ build

A summary of the last build: ROM size by kind of content, the largest assets, memory use, warnings and the history of
earlier builds.

## Opens from

- Build status in the status bar (#34)
- Build › Build Report (available once the project has been built)
- Budget Details › Open Build Report

## What is on it

- **Build again**: opens Build ROM and builds the project again.
- **Toolbar, centre and right**: Play starts the game in Play Mode; the video mode chip opens the Video Mode popover;
  Undo and Redo.
- **Cards**: **ROM size** (and its share of the 32 MB cartridge), **Build time** (split into C#, assets and g++),
  **Warnings** (the count and the first warning, or "No warnings"), and **Free VRAM** (out of 96 KB, for the scene that
  uses the most).
- **ROM breakdown**: one bar split into Graphics, Code, Audio and Data, with the size of each.
- **Largest assets**: the five biggest assets with type, size and a share bar: the asset's share of the ROM, all bars
  on one scale where the whole bar is the whole ROM (a 41 KB asset in a 412 KB ROM fills a tenth of it). Point at a bar
  to see the percent, for example "10.0 % of the ROM".
- **History**: up to 12 recent builds, newest first: time, a tick or cross, and the ROM size, build time and warnings,
  or the first error of a failed build.
- **Cartridge header**: Title (up to 12 characters), Game code (4), Maker (2), Save type (None, SRAM 32 KB, Flash 64 KB,
  Flash 128 KB, EEPROM 8 KB) and Version (like 1.0.3). Changes are saved to Project Settings at once and can be undone.
  Letters are made upper case; a value that does not fit is not saved.
- **Export**: **Open build folder** shows the build folder in Explorer. **Run in mGBA** runs the built ROM in Play Mode
  without building (it says "Build the ROM first." if there is none). **Flash to cartridge** opens Flash to Cartridge.
- **Status bar**: Build OK / Build failed · N errors / No build yet, the build time and clock time, and the ROM file
  name.
- **No build yet**: before the first build the page says "No build yet. Build the ROM to see its report."
- The page reloads by itself when a build finishes.

Differs from Figma: the frame draws the share bars on no single scale (41 KB at four fifths of the bar, 6.2 KB at a
fifth); AGB Studio draws each bar as the asset's share of the ROM, so the bars are short and their lengths compare.

## Related

- [Build ROM](T-D06-build-rom.md)
- [Build ROM · Failed](T-D23-build-rom-failed.md)
- [Export ROM](T-D05-export-rom.md)
- [Flash to Cartridge](T-D07-flash-to-cartridge.md)
- [Play Mode](T-P02-play-mode.md)
- [Budget Details](T-O06-budget-details.md)
- [Project Settings · General](T-S01-project-settings-general.md)
- [Project Settings · Save data](T-S02-project-settings-save-data.md)
- [GBA hardware guide: memory](../gba-hardware-guide.md#memory), [save data](../gba-hardware-guide.md#save-data),
  [where the editor checks these limits](../gba-hardware-guide.md#where-the-editor-checks-these-limits)
