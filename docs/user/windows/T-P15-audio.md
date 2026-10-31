# Audio

Figma: T-P15 · Page

> จัดการเพลง (.mod / .xm ผ่าน Maxmod) และเสียงเอฟเฟกต์ ดู pattern แบบ tracker ฟังพรีวิว และดูว่าใช้ channel ของ GBA ไปเท่าไร

Manages music (`.mod` / `.xm` played through Maxmod) and sound effects: view the patterns like a tracker, listen to
a preview, and see how many of the GBA sound channels are in use.

## Opens from

- Tools › Audio
- Create › Sound Effect
- Create › Music
- Double-clicking a `.wav`, `.mod`, `.xm` or `.sfx` file in the Assets panel

## What is on it

- **Toolbar**: three tracker tools: Select (picks a note cell and moves to its row), Pencil (writes the picked note
  into the cell you click) and Eraser (clears the cell you click). The chip shows the sound engine settings, for
  example "Maxmod · 8 ch · 21 kHz".
- **Music** (left): the project's `.mod` and `.xm` files with their length. Click one to open it. The **+** button
  opens Import Asset to add a music file.
- **Sound effects** (left): the project's `.wav` files with their length. Click one to hear it. Double-click opens it
  in the SFX Designer when it was made there. The **+** button opens the SFX Designer with a new effect.
- **Play and Stop**: play the selected music from the current row, or stop it.
- **Loop chip**: shows the looped patterns, for example "Loop · pattern 4–11". Click it to jump to the loop start.
- **BPM, Speed, Patterns, Length**: read from the music file.
- **Song overview**: level bars for the whole song with the play position. Click to jump there.
- **Tracker view**: one column per channel with note, instrument and volume (for example `C-4 01 v40`). The current
  row is highlighted. The mouse wheel moves row by row. Clicking a cell uses the current tool.
- **Mixer**: the loudness of each channel that has notes, in dB.
- **Track** (right): **Format** (for example "XM → Maxmod bank"), **Volume**, **Loop start** (a pattern number) and
  **Fade on scene change**. These are saved next to the music file. Every change can be undone.
- **GBA channels**: how busy the two Direct Sound channels (music) and the four sound-chip channels (Square 1,
  Square 2, Wave, Noise) are, and which effect uses each one.
- **Preview SFX**: one button per sound effect to hear it.
- **Audio ROM and Mixer CPU** (bottom right, also in the status bar): the ROM space of all audio and the share of the
  CPU the music mixer needs.

The Pause and Step buttons in the toolbar act on a running game from this page too: Pause pauses and resumes it, Step
moves it one frame (F10). They are greyed out while no game runs.

## Related

- [SFX Designer](T-P25-sfx-designer.md)
- [Import Asset](T-D04-import-asset.md)
- [Scene Settings](T-D03-scene-settings.md)
- [Project Settings · Build](T-S03-project-settings-build.md)
- [GBA hardware guide: Sound](../gba-hardware-guide.md#sound)
