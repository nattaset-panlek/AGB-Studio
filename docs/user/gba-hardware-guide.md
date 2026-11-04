# GBA hardware guide

This guide explains the Game Boy Advance limits that AGB Studio works within, and where the editor shows each one.
Every number here is a hardware fact; the editor checks them for you, but knowing them helps you plan a game.

## The frame

- The CPU is an ARM7TDMI at 16.78 MHz. One frame at 59.73 Hz is **280,896 cycles**.
- A frame is 160 visible lines plus 68 lines of VBlank. Video memory is safest to change during VBlank, so AGB Studio
  queues tile, map and palette changes during the frame and copies them in VBlank (the commit queue).
- The Profiler in Play mode splits each frame into Logic, Render, Audio and Idle. When a frame needs more than 280,896
  cycles the Console shows "frame N took longer than one frame" with the scene and the step that ran.
- Loading a scene is split into steps (fade out, free, tiles, map and palette, actors, labels and OnLoad, fade in) so no
  single frame does too much. A layer that is slow to load gets a frame of its own.

## Memory

| Area | Size | What AGB Studio keeps there |
|---|---|---|
| ROM (cartridge) | up to 32 MB | code, graphics, maps, music, text, database tables |
| EWRAM | 256 KB | scene arena (actors, map copies, text), save buffers, Mode 7 tables |
| IWRAM | 32 KB | stack, hot ARM code (map drawing, glyph blitting, mixer), engine state |
| VRAM | 96 KB | background tiles and maps (64 KB), sprite tiles (32 KB) |
| OAM | 1 KB | 128 sprite entries and 32 affine matrices |
| Palette RAM | 1 KB | 16 background and 16 sprite palettes of 16 colours |

The Build Report shows ROM, IWRAM and EWRAM use after each build, and Project Settings · Build sets the ROM size
warning.

## Video modes

| Mode | Backgrounds | Used for |
|---|---|---|
| 0 | 4 regular tile layers | most games: platformers, top-down, menus |
| 1 | 2 regular + 1 rotate / scale | a Mode 7 floor with a sky and a HUD (Racing template) |
| 2 | 2 rotate / scale | rotating or zooming playfields |
| 3-5 | bitmap modes: a picture and sprites, no tile layers or text (Mode 3: 240×160, 16-bit colour; Mode 4: 240×160, 255 colours, two pages; Mode 5: 160×128, 16-bit colour, two pages) | not used by the templates; pick the scene's picture in Video Mode, draw with `Bitmap` in C# |

Set the mode per scene in the Scene Settings. Add Layer offers "Rotate / scale layer (Mode 1-2)" for affine layers.

## Tiles and maps

- Graphics are made of 8 x 8 tiles. Regular layers use 4 bits per pixel (16 colours, one palette per tile) or 8 bits
  per pixel (256 colours). Rotate / scale layers always use 8 bits per pixel, at most 256 different tiles, and no
  flipped tiles; AGB Studio bakes flips and palettes into those tiles when it builds the layer.
- Background video memory is four character blocks of 16 KB for tiles and 32 screen blocks of 2 KB for maps. The
  Budget panel and Budget Details show how full each part is.
- The editor's 16 x 16 "tiles" are metatiles: four hardware tiles plus collision and a tag.
- Maps larger than the hardware map are streamed: only the columns and rows that scroll into view are drawn.

## Sprites

- Up to 128 sprites on screen, sizes from 8 x 8 to 64 x 64.
- Up to 32 affine matrices for rotated or scaled sprites.
- About 960 sprite pixels per line can be drawn (fewer with rotation); too many sprites on one line make some vanish.
- Sprite tiles share 32 KB of video memory (16 KB in bitmap modes).

## Palettes

- 16 background palettes and 16 sprite palettes, each of 16 colours. Colour 0 of every palette is transparent.
- Colours are 15-bit (5 bits each for red, green and blue), so close RGB colours can become the same GBA colour.
- The Palette Manager shows which palette each sprite and tileset uses. When an image needs more than 15 colours per
  tile, Import Asset offers "Reduce to 16" or splitting it into more palettes.
- AGB Studio reserves palette 14 for menus and palette 15 for text.

## Effects

- Alpha blending between two layers, fades to black or white, mosaic, and two windows.
- HBlank DMA changes a register on every line; AGB Studio uses it for Mode 7 floors (a new rotate / scale matrix per
  line) and per-line effects.

## Sound

- Four DMG channels (two square waves, a programmable wave, noise) and two Direct Sound channels for samples.
- AGB Studio mixes music (MOD and XM) and sound effects through Maxmod, by default 8 channels at 21 kHz; the Audio page
  shows the mixer's CPU share.
- The SFX Designer makes effects for the DMG channels, or bakes them into WAV samples.

## Input

Ten buttons: A, B, Select, Start, the D-pad, L and R. Project Settings · Input names actions for them, and the Options
screen lets players remap them when "Allow remap" is on.

## Save data

| Type | Size | Notes |
|---|---|---|
| SRAM | 32 KB | fast, battery backed |
| Flash | 64 KB or 128 KB | written in sectors; slower writes |
| EEPROM | 8 KB | small, written in 8-byte blocks |

Each save slot is stored twice with a counter and a checksum, so a power cut while saving keeps the previous copy.
Project Settings · Save data shows how many bytes the save fields need against the chosen type.

## Where the editor checks these limits

- Budget panel and Budget Details: VRAM, OAM, palettes per scene.
- Build Report: ROM, IWRAM, EWRAM, the largest assets, the history of builds.
- Console: frame overruns, full pools, missing strings and glyphs, with the file and line to fix.
- Project Settings · Save data: save size against the save type.
