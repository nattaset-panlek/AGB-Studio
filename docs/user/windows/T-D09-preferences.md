# Preferences

Figma: T-D09 · Dialog

> ตั้งค่าของตัว editor (ไม่ผูกกับโปรเจกต์): หน้าตา, path ของ devkitPro, emulator และปุ่มลัด (ตัวอย่างนี้คือแท็บ Emulator)

Settings for AGB Studio itself, not tied to a project: appearance, the devkitPro path, the emulator and shortcuts.
The Figma frame shows the Emulator tab, which is described on this page.

## Opens from

- Emulator status in the status bar (#35)
- File › Preferences… (Ctrl+,)
- Launcher › Settings tab

All three open the dialog on the Emulator tab.

## What is on it

- **Side bar**: one tab per group of settings: [Appearance](T-S06-preferences-appearance.md),
  [Editor](T-S07-preferences-editor.md), [Paths](T-S08-preferences-paths.md), **Emulator** (this page) and
  [Shortcuts](T-S09-preferences-shortcuts.md).
- **Core**: the emulator that runs your game. mGBA (built in) is the only choice.
- **Scale**: the size of the game picture, from 1x (240 x 160) to 4x (960 x 640).
- **Filter**: Pixel-perfect keeps sharp pixels; Smooth blurs them slightly.
- **Audio**: turns the game sound on or off.
- **Pause when unfocused**: pauses the game while you are in another program, and goes on when you come back.
- **Fast-forward**: the key to hold and the speed, written as key and speed, for example `Tab · 4×`. The speed must be
  2 to 16.
- **Toolchain card**: the devkitPro folder and whether devkitARM, libtonc, maxmod and gbafix were found. A check mark
  means all are there; a warning mark lists what is missing. It says "Checking the toolchain…" while it looks.
- **Change…**: pick the devkitPro folder. The dialog then switches to the Paths tab so you can see the result.
- **Cancel** (or Esc, or the close button): closes and drops every change made in any tab.
- **Save**: keeps the changes for this user and applies them at once (theme, accent, scale, autosave, undo history,
  devkitPro folder and shortcuts). If the devkitPro folder changed, the toolchain is checked again.

Differs from Figma: Core, Scale, Filter, Audio, Pause when unfocused and Fast-forward are saved, but Play Mode does not
read them yet; it always uses its own defaults (3x, Pixel-perfect, audio on, no pause, Tab at 4x).

## Related

- [Project Settings](T-D08-project-settings.md) (settings stored in the project)
- [Play Mode](T-P02-play-mode.md)
- [Launcher](T-P12-launcher.md)
- [Keyboard Shortcuts](T-D10-keyboard-shortcuts.md)
