# Project Settings · General

Figma: T-S01 · Dialog

> แท็บ General ของ Project Settings: ชื่อเกม ข้อมูลบนหัวตลับ ฉากเริ่มต้น ฟอนต์และภาษาเริ่มต้น และ splash ตอนเปิดเกม

The General tab of Project Settings: the game title, the cartridge header, the start scene, the default font and
language, and the splash shown when the game boots.

## Opens from

- File › Project Settings…, then the General tab in the side bar

## What is on it

- **Game title**: the name of your game. It cannot be empty.
- **Version**: written as major.minor.patch, for example `1.0.3`.
- **ROM title**: the name stored in the cartridge header. Up to 12 characters: capital letters, digits or `_`.
  Letters you type become capitals.
- **Game code**: exactly 4 capital letters or digits, stored in the cartridge header.
- **Maker**: exactly 2 capital letters or digits, stored in the cartridge header.
- **Start scene**: the scene the game opens with. The list shows the scenes in your project.
- **Default font**: the font text uses unless you choose another. The list shows the fonts in your project.
- **Default language**: the language the game starts in, from the languages set on the Localization tab.
- **Boot splash**: None, or the AGB Studio logo for 1 to 5 seconds when the game starts.
- **Skip splash in Debug builds**: leaves the splash out of Debug builds so testing starts faster.
- **Footer**: if a field is not valid, the footer says what is wrong and Save is greyed out. Cancel (or Esc) drops
  the changes; Save writes them to the project.

## Related

- [Project Settings](T-D08-project-settings.md) (the dialog and its other tabs)
- [Export ROM](T-D05-export-rom.md)
- [Font Manager](T-P09-font-manager.md)
- [Project Settings · Localization](T-S05-project-settings-localization.md)
- [Scene Editor](T-P01-scene-editor.md)
