# New Project

Figma: T-D01 · Dialog

> เลือก template ตามแนวเกม ระบบจะใส่ตัวละคร ศัตรู ฉากตัวอย่าง HUD และสคริปต์พื้นฐานของแนวนั้นมาให้ แล้วตั้งชื่อเกมและข้อมูลบนหัวตลับ

Pick a genre template and AGB Studio fills the new project with that genre's characters, enemies, sample scene, HUD
and basic scripts; then name the game and fill in the cartridge header.

## Opens from

- File › New Project… (Ctrl+N)
- Launcher › New Project
- Launcher · Templates › Use this template
- A template card on the Launcher home, or a double-click on a card in Launcher · Templates

## What is on it

- **Choose a genre template**: the eight template cards (Platformer, Top-down Adventure, Turn-based RPG, Shoot 'em
  up, Puzzle, Visual Novel, Racing (Mode 7), Beat 'em up) in a grid that scrolls. Click a card to choose it. The
  footer line says what the template adds, for example "Template adds: player controller, enemies, coins, HUD and a
  title screen."
- **Project name**: starts as "My Game" and has the focus. As you type, the location, ROM title and game code follow
  the name until you change them yourself.
- **Location**: the folder the project is made in. Click it to pick a different parent folder. The help line reads
  "A folder outside D:\devkitPro is safer for updates" (with your devkitPro folder) and turns amber when the location
  is inside devkitPro. The folder must be new or empty; otherwise the line turns red and says "... is not empty".
- **ROM title**: the name in the cartridge header, up to 12 characters: capital letters, digits, space, `_` or `-`.
  Letters are made capital as you type.
- **Game code**: exactly 4 capital letters or digits. It is made from the ROM title, for example SKYHOPPER gives
  SKYH. The help line turns red when a value is not allowed.
- **Save type**: the save memory on the cartridge: None, SRAM · 32 KB (the default), Flash · 64 KB, Flash · 128 KB or
  EEPROM · 8 KB.
- **Include**: Sample level (Forest_01), Thai variable-width font, Logic Graph examples and Initialize git
  repository. All are on except Logic Graph examples.
- **Create Project**: available only when every field is valid. If the open project has unsaved files, Unsaved
  Changes asks first. The project is made and opened in the Scene Editor (or the empty-project view). If something
  fails, a message says why and no half-made project is left behind.
- **Cancel**: closes without making anything.

Differs from Figma: the Figma frame shows six template cards; the code shows all eight (it adds Racing (Mode 7) and
Beat 'em up) in a scrolling grid.

## Related

- [Launcher · Templates](T-P20-launcher-templates.md)
- [Launcher](T-P12-launcher.md)
- [Unsaved Changes](T-D12-unsaved-changes.md)
- [Scene Editor](T-P01-scene-editor.md)
- [Scene Editor · Empty project](T-P22-scene-editor-empty-project.md)
- [Project Settings · Save data](T-S02-project-settings-save-data.md)
- [GBA hardware guide: save data](../gba-hardware-guide.md#save-data)
