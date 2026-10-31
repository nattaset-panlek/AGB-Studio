# Flash to Cartridge

Figma: T-D07 · Dialog

> เขียนเกมลงตลับจริงผ่านเครื่องเขียนตลับ USB หรือคัดลอกลง SD ของ flash cart

Writes your game to a real cartridge through a USB cartridge writer, or copies it to the SD card of a flash cart.

## Opens from

- Build › Flash to Cartridge…
- Build Report › Flash to cartridge

## What is on it

- **Device**: the devices found on this PC: a GBxCart RW on a USB port, and removable drives shown as flash cart SD
  cards (named EverDrive GBA, EZ-Flash or Flash cart, with the drive letter). Pick one from the list.
- **Cartridge info**: what the chosen device reports, for example "SD card detected · 12.3 GB free · FAT32" with
  "Writes the .gba to the card root", or for a GBxCart RW "Cartridge detected" with the game now on the cartridge. With
  no device it says "No device found · Connect a GBxCart RW or insert a flash cart's SD card".
- **Back up the save file first** (on by default): copies the save from the card to the project's
  `build/save-backups` folder, named with the date and time.
- **Verify after writing** (on by default): reads the game back and checks it. If it does not match, a message asks
  you to flash again.
- **Progress**: the stage (Backing up, Writing, Verifying) with a bar and "x / y KB".
- **Hint** (bottom left): "Keep the cartridge connected until it finishes."
- **Cancel**: closes the window, or stops a flash that is running (a message reminds you to flash again before
  playing the cartridge).
- **Flash**: writes the ROM of the last build. It is only available when the device has a cartridge or card and a
  built ROM exists. When done, a message shows the file and size and the window closes.

Differs from Figma: Figma shows writing through a GBxCart RW over USB; in the code a GBxCart RW can be detected and
its cartridge read, but writing (and save backup) through it is refused with "verified on hardware later". Only flash
cart SD cards can be written.

Differs from Figma: Figma's cartridge line shows flash and save sizes ("32 MB flash · SRAM 32 KB"); the code shows the
game code from the cartridge header.

## Related

- [Build ROM](T-D06-build-rom.md)
- [Export ROM](T-D05-export-rom.md)
- [Build Report](T-P11-build-report.md)
- [Project Settings · Save data](T-S02-project-settings-save-data.md)
- [GBA hardware guide: save data](../gba-hardware-guide.md#save-data)
