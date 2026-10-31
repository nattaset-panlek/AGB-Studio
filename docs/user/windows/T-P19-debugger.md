# Debugger

Figma: T-P19 · Page

> ส่องการทำงานของเกมขณะรัน: VRAM (tile ที่โหลดอยู่), OAM (sprite 128 ช่อง), palette, หน่วยความจำ, Profiler เวลาต่อเฟรม และ breakpoint ในสคริปต์

Looks inside the game while it runs: the tiles loaded in VRAM, the 128 OAM sprite slots, palettes, memory, a Profiler
of time per frame, and breakpoints in your scripts. This page opens on the VRAM tiles view with the Profiler.

## Opens from

- Debug button in the title bar (#9)
- Tools › Debugger (Ctrl+Shift+D)

If no game is running, opening the Debugger starts one from the scene the Scene Editor shows (like Play from Here).

## What is on it

- **Toolbar, left**: the bug button turns breakpoints on or off. **Step to the next line** moves one script line when
  the game is stopped at a breakpoint, otherwise one frame. **Restart** runs the game again from the start.
  **Paused at frame N** shows while paused; click it to resume.
- **Toolbar, centre**: **Stop**, **Pause** (pause and resume), **Step** (one frame, F10) and "Simulating · mGBA".
- **Toolbar, right**: the live video mode, for example "GBA · Mode 0 · 240×160", which opens the Video Mode popover;
  Undo and Redo.
- **Views**: VRAM tiles, OAM sprites, Palettes, Memory, Profiler, Breakpoints. Memory and Palettes switch to their own
  views; the others show the VRAM and OAM view with the Profiler panel.
- **Breakpoints**: a list with a tick box per breakpoint (a script line or "On scene load") to turn each on or off.
- **Document tabs**: Game (back to Play Mode) and Debugger.
- **VRAM · character blocks**: the first 128 tiles of the background character block, each in the palette it is drawn
  with, and every sprite graphic in OBJ tile memory at its size and palette.
- **OAM table**: "OAM · N / 128 in use", with one row per sprite slot: number, actor, X, Y, size, tile, palette,
  priority and flip flags.
- **Profiler**: frame time of the last 120 frames against the 16.7 ms budget line; this frame split into Logic,
  Render, Audio and Idle; memory use (EWRAM, IWRAM, VRAM, OAM); and the script call stack when stopped.
- **Status bar**: Running / Paused / Stopped, the script line it is stopped at (for example
  "PlayerController.cs:19"), and frames per second with CPU use.

Differs from Figma: Figma shows breakpoints listed (PlayerController.cs:19, Slime.cs:42, On scene load), but no window
in the code adds a breakpoint, so the Breakpoints list stays empty.

## Related

- [Debugger · Memory](T-P23-debugger-memory.md)
- [Debugger · Palettes](T-P24-debugger-palettes.md)
- [Play Mode](T-P02-play-mode.md)
- [Video Mode](T-O02-video-mode.md)
- [Script Editor](T-P04-script-editor.md)
- [GBA hardware guide: the frame](../gba-hardware-guide.md#the-frame), [memory](../gba-hardware-guide.md#memory),
  [tiles and maps](../gba-hardware-guide.md#tiles-and-maps), [sprites](../gba-hardware-guide.md#sprites)
