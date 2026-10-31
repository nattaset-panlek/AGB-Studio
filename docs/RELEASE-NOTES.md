# AGB Studio release notes

## 1.0.0 (2026-10-31)

The first release: a desktop editor for making Game Boy Advance games, with a C# scripting language that compiles to
native code, Thai and English text, and a runtime engine written for the hardware.

### Editor

- Launcher with recent projects, eight genre templates, tutorials, sample projects and a documentation hub.
- Scene Editor with layers, actors, the Hierarchy and Inspector, budgets, Play and Simulate.
- Tilemap Editor, Sprite Editor, Palette Manager, Font Manager (Thai stacking marks), Database, Localization, Dialogue
  Editor, Audio with a tracker view, SFX Designer, Logic Graph, World Map, Script Editor and the Scripting API.
- Build ROM, Build Report with history, Export ROM and Flash to Cartridge (SD-card flash carts; GBxCart RW reads only
  in this release).
- Play mode on the built-in mGBA core with a live Inspector, Console, profiler, debugger (C# breakpoints) and hot
  reload.

### Engine

- Tile, image and rotate / scale (Mode 7) layers, streaming maps, slopes, one-way platforms, ladders and hazards.
- Up to 64 actors per scene with behaviours, spawning and touches; up to 128 sprites with animation and affine
  transforms.
- Text in Thai and English with dictionary word wrap, dialogue boxes, choices and HUD labels.
- Menu / UI kit: boot splash, title menu, pause overlay and options (volume, language, button remap).
- Save data on SRAM, Flash or EEPROM with two copies per slot, checksums and autosave.
- Maxmod music (MOD / XM) and sound effects; DMG sound effects from the SFX Designer.

### Templates and samples

- Templates: Platformer, Top-down Adventure, Visual Novel, Puzzle, Shoot 'em up, Turn-based RPG, Racing (Mode 7) and
  Beat 'em up. Each one builds, plays to its end in an automated test and holds 60 fps.
- Samples: Sky Hopper demo, Cave Quest, Snow Run and Dusk Story.

### Known limits

- Windows 10 and 11 (x64) only; devkitPro must be installed separately (the first run finds it or explains how).
- Builds are not code-signed (D-19): Windows SmartScreen may ask before the first start.
- Check for Updates and Report a Bug use the addresses in `editor.json`; with none set they say so (D-13).
- Writing to a GBxCart RW cartridge is turned off until it has been verified on hardware.
