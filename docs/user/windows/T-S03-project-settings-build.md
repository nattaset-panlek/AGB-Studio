# Project Settings · Build

Figma: T-S03 · Dialog

> แท็บ Build: ตั้งค่าการ build แยก Debug / Release ระดับ optimise การบีบอัดภาพ และสิ่งที่ทำหลัง build เสร็จ

The Build tab: build settings for Debug or Release, the optimisation level, graphics compression and what happens
after a build finishes.

## Opens from

- File › Project Settings…, then the Build tab in the side bar

## What is on it

- **Configuration**: Debug (for testing) or Release (the version you share).
- **Optimise**: how the compiler tunes the code: `-O0` none, `-O1` balanced, `-O2` speed, `-O3` max speed or `-Os`
  size.
- **Output**: the folder the built ROM goes to, for example `build/`. It cannot be empty.
- **Warn ROM >**: warns when the ROM grows past this size, 1 to 32 MB.
- **Save type**: the save chip on the cartridge (None, SRAM, Flash 64 KB, Flash 128 KB, EEPROM). It is the same
  setting as on the Save data tab.
- **Release options**:
  - **Compress graphics with LZ77**: packs graphics so the ROM is smaller.
  - **Strip Debug.Log calls**: removes log lines from the game.
  - **Keep asserts**: keeps the `Debug.Assert` checks in the game.
  - **Run in mGBA after build**: starts the game in the emulator when the build finishes.
- **Toolchain card**: the devkitARM version and devkitPro folder, and whether the compiler, libtonc, maxmod and
  gbafix were found. A warning mark lists what is missing.
- **Change in Preferences…**: opens Preferences on the Paths tab, where the devkitPro folder is set.
- **Footer**: Cancel (or Esc) drops the changes; Save writes them to the project.

## Related

- [Project Settings](T-D08-project-settings.md) (the dialog and its other tabs)
- [Preferences · Paths](T-S08-preferences-paths.md)
- [Build ROM](T-D06-build-rom.md)
- [Build Report](T-P11-build-report.md)
- [Project Settings · Save data](T-S02-project-settings-save-data.md)
- [GBA hardware guide: Memory](../gba-hardware-guide.md#memory)
