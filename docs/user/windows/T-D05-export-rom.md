# Export ROM

Figma: T-D05 · Dialog

> ส่งออกไฟล์ .gba สำหรับเล่นบน emulator หรือเครื่องจริง พร้อมข้อมูลหัวตลับ

Exports a `.gba` file to play in an emulator or on real hardware, together with the cartridge header information.

## Opens from

- File › Export ROM (.gba)… (Ctrl+E)

## What is on it

- **File name**: the name of the ROM file. ".gba" is added if you leave it out.
- **Folder**: where the file goes; starts at the project's build output folder. Click it to pick another folder.
- **ROM title**: up to 12 letters, digits or spaces (made upper case).
- **Game code**: exactly 4 letters or digits (made upper case).
- **Version**: major.minor.patch, for example 1.0.3.
- **Options**:
  - **Compress graphics (LZ77)**: makes graphics smaller in the ROM.
  - **Strip debug info and logs**: leaves debug information and log messages out of the ROM.
  - **Pad ROM to a power of two**: fills the file up to the next power-of-two size, which flash carts and some
    emulators expect.
- **Estimated size**: the size of the last built ROM (padded when that option is on) against the 32 MB cartridge.
- **Cancel**: closes without exporting.
- **Export .gba**: checks the fields first and shows a hint under any field that is wrong. It then saves the title,
  game code, version, Compress and Strip choices to Project Settings, makes a Release build in the Build ROM
  window, and copies the finished ROM to the folder. The third-party notices file (`THIRD_PARTY.txt`) is copied next
  to it.

## Related

- [Build ROM](T-D06-build-rom.md)
- [Build ROM · Failed](T-D23-build-rom-failed.md)
- [Flash to Cartridge](T-D07-flash-to-cartridge.md)
- [Build Report](T-P11-build-report.md)
- [Project Settings · General](T-S01-project-settings-general.md)
- [Project Settings · Build](T-S03-project-settings-build.md)
