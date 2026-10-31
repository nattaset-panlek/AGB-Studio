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
- **Views**: VRAM tiles, OAM sprites, Palettes, Memory, Profiler, Breakpoints. Each shows its own view in the middle;
  VRAM tiles, OAM sprites, Profiler and Breakpoints keep the Profiler panel on the right:
  - **VRAM tiles**: the character blocks above the OAM table (this page).
  - **OAM sprites**: the OAM table alone.
  - **Palettes** and **Memory**: see [Debugger · Palettes](T-P24-debugger-palettes.md) and
    [Debugger · Memory](T-P23-debugger-memory.md).
  - **Profiler**: the frame time of the last 120 frames, large, against the 16.7 ms budget, and this frame's Logic,
    Render, Audio and Idle in milliseconds and percent of the budget, with frames per second, CPU and VBlank time free.
  - **Breakpoints**: a table of every breakpoint with its kind (C# line, Script line or Scene load) and where it stops,
    a tick box to turn it on or off and a button to remove it; a field to add one by typing the script and line, for
    example `PlayerController.cs:19` (a wrong entry says how to write it); and, while the game is stopped, where it
    stopped and the call stack.
- **Breakpoints**: a list with a tick box per breakpoint to turn each on or off. It lists the breakpoints you set in
  the Script Editor (click a line number, or press F9 on a line) or in the Breakpoints view, and "On scene load", which
  is always listed and starts off. An enabled breakpoint pauses the game when it reaches that line (or loads a scene).
- **Document tabs**: Game (back to Play Mode) and Debugger.
- **VRAM · character blocks**: the first 128 tiles of the background character block, each in the palette it is drawn
  with, and every sprite graphic in OBJ tile memory at its size and palette.
- **OAM table**: "OAM · N / 128 in use", with one row per sprite slot: number, actor, X, Y, size, tile, palette,
  priority and flip flags.
- **Profiler**: frame time of the last 120 frames against the 16.7 ms budget line; this frame split into Logic,
  Render, Audio and Idle; memory use (EWRAM, IWRAM, VRAM, OAM); and the script call stack when stopped.
- **Status bar**: Running / Paused / Stopped, the script line it is stopped at (for example
  "PlayerController.cs:19"), and frames per second with CPU use.

## Related

- [Debugger · Memory](T-P23-debugger-memory.md)
- [Debugger · Palettes](T-P24-debugger-palettes.md)
- [Play Mode](T-P02-play-mode.md)
- [Video Mode](T-O02-video-mode.md)
- [Script Editor](T-P04-script-editor.md)
- [GBA hardware guide: the frame](../gba-hardware-guide.md#the-frame), [memory](../gba-hardware-guide.md#memory),
  [tiles and maps](../gba-hardware-guide.md#tiles-and-maps), [sprites](../gba-hardware-guide.md#sprites)
