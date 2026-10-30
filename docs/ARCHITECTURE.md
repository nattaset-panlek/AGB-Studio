# AGB Studio · Architecture

How the blueprint (`D:\GBA-Research\agb-studio-blueprint.html`) maps to code. Decisions are in `DECISIONS.md`; the build plan and evidence are in `ENGINE-CHECKLIST.md`.

## Repository

| Folder | What it is |
|---|---|
| `runtime/include/agb`, `runtime/src` | The C++20 engine that runs on the GBA (no exceptions, no RTTI, no heap) |
| `runtime/tests/<name>/` | On-ROM test programs; each folder becomes one test ROM |
| `tools/AgbStudio.Core` | Project file formats (D-03) and the PNG codec |
| `tools/AgbStudio.Build` | Toolchain detection, Ninja build driver, asset converters, fonts and Thai text, database codegen, game codegen, build report, lint |
| `tools/AgbStudio.Emu` | mGBA libretro host (D-06): ROM tests, debug channel reader |
| `tools/AgbStudio.Scripting`, `tools/AgbStudio.Api`, `tools/AgbStudio.Analyzers` | C# scripting compiler, API reference assembly, GAE analyzer |
| `tools/agb` | Command line: `build`, `sample`, `romtest`, `runtime-test`, `lint`, `text-preview` |
| `editor/AgbStudio.Editor` | Avalonia editor (D-01) |
| `tests/` | xunit suites; `ci.cmd` runs everything |

## Runtime layers (blueprint 03)

| Layer | Files |
|---|---|
| Platform | libtonc, maxmod (`libmm`), BIOS wrappers `bios.hpp`, IWRAM placement (`*.iwram.cpp`, `AGB_IWRAM_CODE`) |
| Core services | `system.hpp` (VBlank frame loop, profiler), `commit.hpp` (OAM / palette / register shadows and DMA queue), `input.hpp`, `task.hpp`, `memory.hpp` (arenas, pools), `rng.hpp`, `save.hpp`, `debug.hpp`, `assert.hpp`, `format.hpp`, `fixed.hpp` |
| Rendering and media | `render/bg.hpp` (VRAM blocks), `render/map.hpp` (metatile streaming), `render/sprite.hpp`, `render/palette.hpp`, `render/text.hpp`, `render/effects.hpp`, `text.hpp` (string tables, dialogue box, labels), `audio.hpp` |
| Game framework | `game.hpp` (screens, staged scene loading, links, camera), `actor.hpp`, `behaviours.hpp`, `script.hpp` + `script_ops.def` (Event Script VM), `bus.hpp` (command bus), `api.hpp` (C# API facade) |

Frame order (blueprint 04): `system::waitVBlank()` commits the previous frame's shadows in VBlank; the scene then runs input, behaviours, scripts, tasks, physics (`lateUpdate`), touches, links, sprite animation, camera, map streaming, OAM build and palette effects.

## Data contract

`runtime/include/agb/data.hpp` defines what the build writes: tilesets with 16x16 metatiles, map layers, sprite sheets with animations, fonts and pre-laid-out text runs. Scenes, actors and links are `SceneDef`, `ActorDef` and `LinkDef` in `game.hpp` / `actor.hpp`. Generated symbols use prefixes (`agbasset_`, `agbfont_`, `agbtext_`, `agbdata_`) so the build report can attribute ROM bytes per asset.

## Build pipeline (blueprint 11)

`GameBuilder` runs: Convert assets (hash cache in `build/cache/manifest.json`) -> C# to C++ (`IScriptBackend`) -> Generate game -> Compile with Ninja and devkitARM -> gbafix -> `build/report.json` and `build/report-history.json`. Generated files are written only when their content changes, so Ninja rebuilds only what changed.

## Debug channel

`agb::debug::Channel` lives in EWRAM with the magic `AGBDBG01`. It carries the log ring, test status, frame number, the live actor table, profiler stats and debugger state. The host reads it through emulator memory (`tools/AgbStudio.Emu/DebugChannel.cs`); field offsets are mirrored there.

## Text (blueprint 13)

Text is laid out at build time: Thai marks are stacked from font metrics, lines break at dictionary word boundaries (ICU word list), and each string becomes a `TextRun` per language. The C# `TextRenderer` draws runs exactly like the runtime `TextBox`, which tests check pixel for pixel against ROM captures.
