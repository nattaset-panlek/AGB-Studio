# Project Settings

Figma: T-D08 · Dialog

> ตั้งค่าทั้งโปรเจกต์: ปุ่มควบคุม, ระบบ save, การ build, สคริปต์ และภาษา (ตัวอย่างนี้คือแท็บ Input)

Settings for the whole project: controls, save system, build, scripts and languages. The Figma frame shows the Input
tab, which is described on this page.

## Opens from

- File › Project Settings… (greyed out until a project is open; the dialog opens on the Input tab)

## What is on it

- **Side bar**: one tab per group of settings. Click a tab to show it:
  [General](T-S01-project-settings-general.md), **Input** (this page),
  [Save data](T-S02-project-settings-save-data.md), [Build](T-S03-project-settings-build.md),
  [Scripting](T-S04-project-settings-scripting.md) and [Localization](T-S05-project-settings-localization.md).
- **Input mapping table**: one row per GBA button (A, B, L, R, Start, Select, D-pad) with four columns.
- **GBA button**: the button on the console.
- **Action name**: the name your scripts use for that button, for example `Jump` or `Pause`. Each name must be one
  word (letters, digits, `_`) and two buttons cannot share a name.
- **Keyboard (emulator)**: the key that presses this button when you play in the emulator. Click the key, then press
  the new key; Esc keeps the old one. The D-pad always uses the arrow keys and cannot be changed here.
- **Gamepad**: click to pick the gamepad button (South, East, West, North, LB, RB, LT, RT, Menu, View, D-pad, Stick,
  Stick / D-pad).
- **Let players remap buttons in the options menu**: when ticked, players can change the buttons in the game's
  options screen.
- **Hint line**: when a value is not valid, the footer says what is wrong and Save is greyed out.
- **Cancel** (or Esc, or the close button): closes and drops every change made in any tab.
- **Save**: writes all tabs to the project file. The next build uses the new settings.

## Related

- [Preferences](T-D09-preferences.md) (settings for AGB Studio itself, not the project)
- [Keyboard Shortcuts](T-D10-keyboard-shortcuts.md)
- [Play Mode](T-P02-play-mode.md)
- [GBA hardware guide: Input](../gba-hardware-guide.md#input)
