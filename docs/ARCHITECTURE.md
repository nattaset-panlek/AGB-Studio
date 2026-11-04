# AGB Studio · Architecture

How the blueprint (`D:\GBA-Research\agb-studio-blueprint.html`, sections 01-16) maps to the code on `main`. Decisions
are in `DECISIONS.md`, the editor in `EDITOR-ARCHITECTURE.md`, the build plan and evidence in `ENGINE-CHECKLIST.md`.
Only what exists in the code is described; where a blueprint idea is not built, this page says so.

## Repository

| Folder | What it is |
|---|---|
| `runtime/include/agb`, `runtime/src` | The C++20 engine that runs on the GBA (no exceptions, no RTTI, no heap). `src/core`, `src/render`, `src/audio`, `src/framework`, `src/script` |
| `runtime/tests/<name>/` | On-ROM test programs; each folder becomes one test ROM (`agb runtime-test`, `RuntimeRomTests`), except `host_assert` and `host_probe`, which never finish on their own and are driven by their own C# tests |
| `tools/AgbStudio.Core` | Project file formats (D-03, `Project/`) and the PNG codec |
| `tools/AgbStudio.Build` | Toolchain detection, native build (`NativeBuild`), build pipeline (`Pipeline/`), asset converters (`Graphics/`, `Audio/`), fonts and Thai text (`Fonts/`), database codegen (`Data/`), generated template and sample content (`Content/`), packaging (`Packager`), lint (`Lint`) |
| `tools/AgbStudio.Emu` | mGBA libretro host (D-06): ROM tests, debug channel reader, Play sessions, live Inspector, GIF capture |
| `tools/AgbStudio.Scripting` | C# scripting compiler: C# to C++ (`Translation/`), cutscenes to bytecode (`Bytecode/`), Dialogue Editor files (`Dialogue/`), Logic Graph (`Graph/`), offline API docs (`ApiDocs`), external IDE project (`CsprojGenerator`) |
| `tools/AgbStudio.Api`, `tools/AgbStudio.Analyzers` | The `GBA` API reference assembly scripts compile against; the GAE001-GAE006 analyzer and its code fixes |
| `tools/agb` | Command line: `build`, `sample`, `template`, `package`, `romtest`, `runtime-test`, `lint`, `text-preview`, `make-test-audio` |
| `editor/AgbStudio.Editor` | Avalonia editor (D-01), see `EDITOR-ARCHITECTURE.md` |
| `tests/` | xUnit suites `AgbStudio.Tests`, `AgbStudio.Scripting.Tests`, `AgbStudio.Editor.Tests`; `ci.cmd` runs them |
| `third_party/` | mGBA libretro core, fonts, ICU Thai word list, each with its licence |

D-04 lists `templates/` and `samples/` folders; they do not exist. Templates, samples and the Sky Hopper fixture are
written by code in `tools/AgbStudio.Build/Content` (`Templates/*Template.cs`, `Samples.cs`, `SampleProjects.cs` (the
`mini` sample), `SkyHopper.cs`, `SkyHopperArt.cs` (the Sky Hopper fixture and demo art, drawn as the Figma frames
draw it), `PixelArt.cs`, `SplashArt.cs`) with the module and sound writers in
`tools/AgbStudio.Build/Audio` (`XmWriter.cs`, `Chiptune.cs`, `AudioWriters.cs`) when a project is created.

## Blueprint sections and where they live

| Blueprint section | Code |
|---|---|
| 01 Overview (editor, toolchain, runtime, emulator in a loop) | `editor/`, `tools/`, `runtime/`, `tools/AgbStudio.Emu` |
| 02 Lessons from pokeemerald | Reference only (G-LEGAL.01); `agb lint` rejects distinctive pokeemerald identifiers |
| 03 Runtime layers | `runtime/include/agb`, `runtime/src` (table below) |
| 04 One frame | `system.hpp` (`waitVBlank`, profiler), `commit.hpp`, `game.cpp` `frame()` and `SceneScreen::update` |
| 05 Screen flow | `game.hpp` (screen stack of up to 4, staged loading), `ui.hpp` (boot splash, pause, options, scene menus) |
| 06 Three tiers of logic | `tools/AgbStudio.Scripting/Translation` (tier 1, C# to C++), `Bytecode` + `runtime/src/script` (tier 2), `tools/AgbStudio.Build/Data` (tier 3, database tables) |
| 07 Event Script VM | `runtime/include/agb/script.hpp`, `script_ops.def`, `natives.hpp`, `runtime/src/script/vm.cpp` |
| 08 World model | `render/map.hpp`, `data.hpp` (tilesets, metatiles, map layers), `game.hpp` (scenes, links); editor Tilemap Editor and World Map |
| 09 Rules / Presentation / Input | `bus.hpp` (command bus with player, AI and replay sources), `input.hpp` |
| 10 Memory and budgets | `memory.hpp` (arenas, pools), `types.hpp` (IWRAM / EWRAM section macros), `Pipeline/BuildReport.cs`, `GameBuilder.CheckHardwareLimits` |
| 11 Build pipeline | `tools/AgbStudio.Build/Pipeline/GameBuilder.cs`, `NativeBuild.cs`, `Pipeline/AssetCache.cs` |
| 12 User project structure | `tools/AgbStudio.Core/Project` (`GameProject`), `FILE-FORMATS.md` |
| 13 Old problems, modern answers | Text (`Fonts/`), C# debugging (`probe.hpp`), hot reload (D-15), headless ROM tests (`RomTestRunner`) |
| 14 Genre kits | `behaviours.hpp` built-in behaviours, `ui.hpp`, `render/affine.hpp`, `tools/AgbStudio.Build/Content/Templates` |
| 15 Roadmap, 16 References | `ENGINE-CHECKLIST.md` phases; no code |

## Runtime layers (blueprint 03)

An upper layer calls only lower layers. User code (translated C# behaviours and cutscenes) lives in the game framework.

| Layer | Headers (`runtime/include/agb`) |
|---|---|
| Platform | libtonc, maxmod (`libmm`), `bios.hpp` (LZ77 to VRAM / WRAM, Div, Halt, CpuFastSet), `types.hpp` (`AGB_IWRAM_CODE`, `AGB_EWRAM_BSS`; `*.iwram.cpp` files are compiled as ARM into IWRAM) |
| Core services | `system.hpp` (VBlank frame loop, interrupts, profiler), `commit.hpp` (OAM / palette / register shadows and the DMA queue with a per-frame byte budget, flushed in VBlank), `input.hpp`, `task.hpp` (16 tasks), `memory.hpp`, `rng.hpp` (xoshiro128**, one stream per purpose), `save.hpp`, `debug.hpp`, `assert.hpp`, `format.hpp`, `fixed.hpp` (24.8) |
| Rendering and media | `render/bg.hpp` (video modes 0-5, char / screen block allocation), `render/map.hpp` (metatile streaming), `render/affine.hpp` (affine layers, Mode 7), `render/bitmap.hpp` (frame buffers of Modes 3, 4 and 5), `render/sprite.hpp` (128 sprites, 32 affine matrices, shared frame cache), `render/palette.hpp` (fade, tint, cycle, flash), `render/text.hpp` (VWF text windows), `render/effects.hpp` (blending, mosaic, windows, scanline DMA, transitions), `text.hpp` (string tables, dialogue box, labels), `audio.hpp` |
| Game framework | `game.hpp` (screens, scenes, links, camera), `actor.hpp` (64 actors, 6 behaviours each), `behaviours.hpp`, `script.hpp` + `script_ops.def` + `natives.hpp` (Event Script VM), `bus.hpp`, `ui.hpp` (Menu / UI kit, D-23), `api.hpp` (the C++ side of the C# API), `probe.hpp` (C# breakpoints, D-21) |

The lint (`tools/AgbStudio.Build/Lint.cs`) keeps two rules of these layers: runtime code writes video hardware only
through the commit queues (a short allow-list of modules may touch registers), and runtime C++ uses no `float` or
`double`.

### Video modes, affine layers and Mode 7

`render::bg::setVideoMode` takes modes 0-5 (the Video Mode popover). Modes 0-2 draw tile layers. Modes 3, 4 and 5 show a
frame buffer on BG2 at priority 3, through `render/bitmap.hpp`: Mode 3 has one 240 x 160 page of BGR555, Mode 4 two 240 x 160 pages of
palette indices at 0x06000000 and 0x0600A000 (index 0 is the backdrop, so a picture has 255 colours), Mode 5 two 160 x 128 pages of BGR555
shown 1:1 at the top-left corner with the backdrop around them. `bitmap::begin` resets BG2's matrix, takes the whole background memory
in the allocator and drops queued writes left from the last scene; sprites use OBJ tiles 512 and up. A scene in these modes
shows `SceneDef::picture` (`data::Picture`, from `Scene.picture` via `Pipeline/GameBuilder.Pictures.cs` and `Graphics/BitmapConverter.cs`, which the
Scene Editor's `GbaScreenRenderer` also uses) on both pages, loads no map layers (their collision still reads the ROM data) and no
text layer, UI pages or pause menu, and gets a 40 KB DMA budget. Drawing goes through the commit queue, in order: `bitmap::fill`,
`setPixel`, `clear`, `queuePicture`, and in Modes 4 and 5 it lands on the hidden page until `flip`. The C# API is `Bitmap` (`Facades.cs`, `api.hpp`,
built-in natives for cutscenes); it is not a Logic Graph node. A map layer of type `Affine` (Add Layer "Rotate / scale layer
(Mode 1-2)") is converted at build time by `Graphics/AffineConverter.cs` to 8bpp tiles with flips and palette banks
baked in (at most 256 tiles, map side 16 / 32 / 64 / 128). At runtime `render::affine::load` and `setTransform` rotate
and scale it with the BG2 / BG3 matrices; `render::affine::mode7` fills a per-scanline matrix table that
`render/effects` copies with HBlank DMA, which is the perspective floor of the Racing (Mode 7) template.

### Audio and DMG sound effects

Music (MOD / XM) and sampled effects (WAV) go through maxmod on the two Direct Sound channels: the build runs mmutil
over every music and sound file into one soundbank (`Audio/SoundbankBuilder.cs`), maxmod mixes at the rate and with
the channel count of Project Settings · Audio (21 kHz and 8 channels by default, D-14; the build sizes maxmod's buffers
for them with `AGB_AUDIO_MIX_KHZ` and `AGB_AUDIO_CHANNELS`, the mixer CPU per rate is in D-26). SFX Designer effects (`audio/sfx/*.sfx` without "Bake to .wav") play on the four DMG
channels with no sample data: `Audio/DmgRegisters.cs` encodes each effect into register values (sweep, envelope and
duty, frequency, wave RAM, length in frames), the build writes them as an `agb_dmg_sfx` table, and
`audio::playDmgId` writes the registers when `Sound.Play` gets an id with bit 15 set. An effect with "Bake to .wav", or
a `.wav` of the same name in `assets/audio/sfx`, plays as a sample instead. `audio::usage()` reports which channels are
busy (Audio page).

Each module's MusicMeta (Audio page: volume, loop start, fade on scene change) is built in by `SoundbankBuilder`: the
loop start is written into the module as maxmod's restart order (a looping song continues there when it ends), and the
volume and fade flag go into the table `agb_soundbank_music` that `GameDef::music` hands to `audio::init`. The module
plays at four volumes multiplied (`audio::moduleVolume()`): the player's (Options "Music" slider,
`audio::setMusicVolume`; nothing else changes it, so it stays through scene changes and `game::boot`), the game's (the
scene's music Volume, `SceneDef::musicVolume`, set when the scene starts its own track, or `Music.SetVolume`), the fade
(`Music.Fade`, fade on scene change; back to full when a module starts) and the track's MusicMeta volume.
`game::loadScene` fades only when the scene and the track both ask for it (`audio_music` ROM test). The Options values
are saved in an 8 B options block on the save chip, outside the slots (`save::Options`, `kOptionsBytes`: music and
effects volume, language, button remap; Flash reserves its last 4 KB sector, SRAM and EEPROM their last 8 bytes, so
slots never overlap it). `game::boot` applies it (`loadOptions`) and the Options screen writes it when it closes after a
change; a game with no save type keeps the values in RAM only. `SaveLayout.OptionsBytes` and the Project Settings Save
data meter count the block.

## Frame order (blueprint 04)

`game::frame()` reads input, then runs the top screen. While a screen loads, one load step runs per frame (scenes have
seven: fade out, free and reset, tiles to VRAM, map and palette, spawn actors, OnLoad script, fade in); a step that
takes more than three quarters of a frame logs a warning naming it. A running scene (`SceneScreen::update`) does:
transitions, pause check, scene menu, new actors' Start, behaviours (unless a cutscene has `LockAll`), Event Script VM,
tasks, physics and tile collision (`lateUpdate`), text typewriter, touches, links, removal of destroyed actors, sprite
animation, then camera follow and shake, actor sprites to the OAM shadow (`actors::syncSprites`, `sprites::build`),
palette effects (`palettes::update`), map layer streaming, animated tiles and the debug-channel actor table.
`system::waitVBlank()` sleeps in the BIOS until VBlank and flushes the commit queues, so the hardware changes of a
frame land in VBlank. The profiler counts Logic, Render, Audio and Idle cycles per frame, the VBlank cycles left after
the commit, and frame overruns.

**Commit queues (P2.04).** Runtime modules write the OAM, palette and display-register shadows and queue VRAM writes;
`commit::flush` (VBlank only) writes the registers, OAM and palette, then the queued VRAM writes in order up to the
per-frame DMA budget (16 KB by default). Three kinds of VRAM write are queued: `queueCopy` (a copy from the caller's
memory; refused when the budget is used up, so the caller retries next frame: sprite frames, text tiles, map rows),
`queueFill` (a rectangle filled with one value: closing a text panel, the text layer's blank map, `bitmap::fill`) and
`queueSequence` (a rectangle of map entries value, value + 1 ...: a text panel's tiles mapped in
order). The bitmap modes add `queueCopyRows` (a picture from ROM, row by row), `queueBytes` and `queuePixel` (GBA VRAM has no byte
writes, so these read, change and write a halfword in VBlank, in queue order: two Mode 4 pixels in one halfword in one frame both stay; at most 512
`queuePixel` a frame, in memory the bitmap scene lends from its screen arena) and `queuePage` (a page flip that waits for the writes queued before it). Fills and sequences keep everything they need in the queue entry, so rows past the budget wait for the next
VBlank instead of being refused, and `queueCopy` leaves the last 16 of the 64 queue entries to them. A deferral counts in `deferredCount()` and logs "too much graphics data changed this
frame, so the rest is copied next frame" once. `copyNow` / `fillNow` (and LZ77 to VRAM) write at once and are only for
staged loads: the scene loader opens `beginStagedLoad` / `endStagedLoad` around each load step, which runs while the
picture is faded out. Debug builds count a `copyNow` / `fillNow` made while the picture shows (outside VBlank, forced
blank and a staged load) in `directWrites()` and log it once; the pause, options and dialogue panels therefore change
the text layer only through the queue (`framework_ui` test `pause_and_resume_map_text_in_vblank`).

## Data contract

`runtime/include/agb/data.hpp` defines what the build writes: tilesets with 16x16 metatiles (2x2 screen entries of one
layer, with a collision type and a tag per metatile), map layers, sprite sheets with animations, fonts and
pre-laid-out text runs. Scenes, actors and links are `SceneDef`, `ActorDef` and `LinkDef` in `game.hpp` /
`actor.hpp`. Generated symbols use prefixes (`agbasset_`, `agbfont_`, `agbtext_`, `agbdata_`). The build report
attributes ROM bytes per asset by the generated object file each asset is written to (`gen/assets/sprite_*`,
`tileset_*`, `gen/text/font_*`, `gen/data/db_*`, `gen/maps/map_*`, `gen/audio/soundbank`), read from the linker map.

Not built from blueprint 08: primary / secondary tilesets (a map has one tileset), two-layer metatiles, a separate
layout file shared between maps (layers live in `maps/<Name>.map`), and seamless map streaming (out of scope,
checklist 1.2). Links of kind On touch fire when the player overlaps the trigger actor; Door fires on the frame Up
goes down during that overlap (holding Up does not fire it); Respawn fires on the overlap or when the player is lost
(destroyed, out of hearts or fallen below the map) (`game.cpp` `checkLinks`).

## Build pipeline (blueprint 11)

`GameBuilder.Build` runs these steps (`IBuildObserver` reports each one to the Build ROM dialog, the Console and
`agb build`):

1. **Convert assets**: sprites, tilesets (LZ77 by default), image layers, the maxmod soundbank (mmutil), SFX Designer
   DMG tables, fonts and per-language string tables (text laid out at build time), database tables. Each converted
   asset is keyed by a hash of its files, its meta and the converter version in `build/cache/manifest.json`, so an
   unchanged asset is not converted again.
2. **C# → C++**: `IScriptBackend` (the scripting compiler) translates behaviours to C++ (the C# Behaviour class a
   Logic Graph generates among them), compiles `[Cutscene]` methods and dialogue to bytecode, and reports diagnostics
   with `file(line,col)`.
3. **Generate game**: scenes, actors, links, the boot splash, the Menu / UI kit and the game definition as C++; then the
   hardware limit check of every scene (below).
4. **Compile (devkitARM g++ -O2)**: runtime sources plus generated sources, Thumb by default, ARM for `*.iwram.cpp`,
   `-std=c++20 -fno-exceptions -fno-rtti`, `-Wall -Wextra -Werror`, `AGB_DEBUG` or `AGB_RELEASE`, optimise flags from
   Project Settings · Build (`-O2` by default; the step keeps its name whatever the setting).
5. **Link + gbafix**: link with `gba.specs`, `-lmm -ltonc`, `--gc-sections` and a map file, `objcopy` to a binary,
   `gbafix` writes the header (title, game code, maker, version, checksum). The step name switches from Compile to
   Link + gbafix when the first link line appears.

After a successful build: `build/report.json` (ROM by category, per asset ROM bytes with the VRAM, palette and RAM
buffer bytes it takes when loaded, largest assets, IWRAM / EWRAM from the linker map, per-scene budgets, warnings;
schema `docs/schemas/report.schema.json`), `build/report-history.json` (failed builds included;
`report-history.schema.json`), and `THIRD_PARTY.txt` next to the ROM with the AGB Studio runtime notice (MIT), then the
libtonc and maxmod notices. Per asset ROM bytes come from the linker map for graphics, maps, fonts, text and data;
music and sounds share the soundbank's bytes in proportion to their source file sizes, and DMG effects share the DMG
table evenly. Generated files are written only when their content changes.

**Native build with or without Ninja** (`NativeBuild.cs`): the build always writes `build.ninja`. When `ninja.exe` is on
`PATH` it runs Ninja; otherwise it runs the same graph in-process: objects older than their source or any header in
their `.d` file are compiled in parallel, then each ROM is linked and fixed when an object is newer. Progress lines
look like Ninja's either way. `AGB_BUILD_JOBS` limits the parallel compiles.

The editor's Build ROM dialog lists the steps in Figma order (C# → C++ first, design issue 1.4-8); the pipeline
converts assets first.

## Hardware limit checks

| Where | What is checked |
|---|---|
| Build, fails the build | Per scene: background VRAM of its map layers, image layers, affine layers and text layer against 64 KB; background palette banks against the 14 free banks (two hold the menus and text); OBJ VRAM of the sprite frames its actors show at once against 32 KB (16 KB in bitmap modes); sprite palettes against 16 (`GameBuilder.CheckHardwareLimits`). Whole game: save data of one slot larger than the save type with its slot count (`CheckSaveFits`, G-HW.06); a ROM over 32 MB (`BuildReport.RomSizeError`, G-HW.05) |
| Build report, warnings | ROM size over the Project Settings · Build warning; IWRAM leaving less than 8 KB for the stack |
| Runtime, Console warnings | 64 actors, 128 sprites, 32 affine matrices, OBJ VRAM full, sprite cycles per screen line, 16 palette banks per type, background VRAM full, 16 tasks, 8 script contexts, command bus queue, DMA budget per frame (deferred to the next frame), load step and frame overruns, save data larger than the save type. The `render_limits` test ROM fills background VRAM (64 KB in tile modes, none left in Mode 3) and OBJ VRAM (32 KB, 16 KB in Mode 3) past the limit and checks the refusal, the warning and that nothing already placed changed (G-HW.01) |
| Editor | Budget Details popover and the GBA Budget block (T-O06), Build Report (T-P11), Tilemap Editor VRAM plan (T-P03), Project Settings · Save data meter (T-S02), Audio channel use (T-P15) |

The split of IWRAM and EWRAM into the rows of blueprint 10 is measured from the linker map (`RamBudget.cs`, report.json
`ramSplit`, G-HW.04): code in IWRAM (Maxmod's `.iwram` sections and the `*.iwram.cpp` files) is "Hot ARM code"; the
rows the runtime controls are held to their defaults by `RamBudgetTests`, the others are reported when over.

## Debug channel and logs

`agb::debug::Channel` lives in EWRAM with the magic `AGBDBG01`. It carries the log ring (4 KB), test status, frame
number, a host command word, the live actor table, profiler stats, debugger and C# breakpoint state, and the game
state hot reload reads. The host finds it by scanning EWRAM and reads it through the core's memory map
(`tools/AgbStudio.Emu/DebugChannel.cs`, `LibretroCore.Memory`); field offsets are mirrored there.

`Debug.Log` (C#), `debug::log` / `logf` and `warnOnce` (C++) and failed asserts write to this log ring, not to mGBA's
debug-print registers. Reasons: one mechanism serves the Console, the headless test runner (`TEST PASS` / `TEST FAIL`
lines), the live Inspector and the Debugger; the host reads it from emulator memory, and the libretro host does not
take text from the core's log interface, so text written to mGBA's debug-print registers would not reach the editor;
and the ROM does not depend on registers that only mGBA has (on VBA-M and hardware the log is ordinary RAM). In
Release builds `log`, `logf` and `warnOnce` are empty inline functions, so no log text reaches the ROM, unless
Project Settings · Build keeps the log (`AGB_KEEP_LOGS`); asserts stop the game in Debug and in Release with "Keep
asserts" (`AGB_KEEP_ASSERTS`). This differs from the wording of checklist item P2.11 ("mGBA debug-print interface");
see D-24.

## Text (blueprint 13)

Text is laid out at build time (`Fonts/TextLayout.cs`): fonts come from TrueType or OpenType (`.ttf` / `.otf`, TrueType
outlines or CFF outlines with Type 2 charstrings, `Fonts/TrueType.cs` and `Fonts/Cff.cs`), BDF or image sheets
(`Fonts/GameFont.cs`), Thai upper vowels, tone marks and lower vowels are stacked from
the font's metrics so marks clear tall consonants, lines break at dictionary word boundaries (ICU word list,
`Fonts/ThaiText.cs`, never inside a word), and each string becomes a `TextRun` per language. The runtime only places
the glyphs (`render/text.cpp`, 1bpp or 2bpp with drop shadow). The C# `TextRenderer` draws runs exactly like the
runtime text box; tests compare it pixel for pixel with ROM captures, and the Localization fit check uses the same
layout.

## Scripting (blueprint 06, 07)

- Tier 1: Roslyn parses user scripts against `AgbStudio.Api`; the analyzer enforces the GBA subset (GAE001-GAE006,
  D-07); `Translation/CppTranslator` writes C++20 per behaviour with `#line` back to the `.cs` file, so g++ errors and
  `Debug.Assert` name the C# line.
- Tier 2: `[Cutscene] async` methods and Dialogue Editor files compile to bytecode for the Event Script VM. The opcode
  table `script_ops.def` is the single source for the VM and the compiler; natives are declared with `[ScriptNative]`.
  Map scripts (Scene Settings OnLoad / OnEnter / OnExit) and link conditions are not bytecode: they become static
  methods of a C# class `SceneEvents_<Scene>` (`ScriptCompiler.cs`), are translated to C++ like tier 1 and are called
  as functions (`SceneDef::onLoad`, `onEnter`, `onExit`).
- Tier 3: Database tables become generated C++ (one unit per table, enums and `Items.Get(ItemId.X)`); script cells
  compile through `Translation/DbScripts.cs`.
- Logic Graph (`Graph/LogicGraph.cs`, `Graph/GraphCodeGen.cs`): a `.graph` file generates C# (D-11). Node catalog:
  Events (On Start, On Update, On Late Update, On Touch with a tag, On Button, On Destroy), Input (Button Pressed / Held
  / Released, Axis X / Y, Any Key), Physics (Is On Ground, Velocity X / Y, Set Velocity X / Y with Set / Add / Clamp,
  Set Velocity, Add Force, Get Velocity, Jump), Flow (Branch), Math (AND, OR, NOT, Add, Subtract, Multiply, Vector from
  Velocity, Compare), Actor (Compare Tag, Destroy, Self), Sound (Play Sound, Play Music, Stop Music), Data & Save (Get /
  Set / Add to Variable, Save Game, Checkpoint), Text & UI (Show Text), Other (Debug Log, Load Scene, Shake Camera).
  Pins are Exec, Bool, Fixed, Actor, Text and Vector. Generated C# that still matches the graph reads back into it.
- C# debugging (D-21): Debug builds insert `AGB_SCOPE` / `AGB_PROBE` calls; a probe that hits a breakpoint pauses the
  game while the editor reads state through the emulator.

## Emulator host (D-06, D-16)

`LibretroCore` loads `third_party/mgba/mgba_libretro.dll` once per process. `RomTestRunner` runs a ROM headless until
the debug channel reports pass or fail (`agb romtest`, the runtime test ROMs of `agb runtime-test` and
`RuntimeRomTests`; the `host_*` ROMs are run from their own tests). `PlaySession` drives Play Mode at 59.73 fps: speed,
pause, frame step, keyboard input and hot reload (D-15, the hand-over block in the last 4 KB of EWRAM). `Inspector`
reads the live actor table, OAM, palettes, memory and profiler stats for Play Mode and the Debugger.

## Packaging (D-19)

`agb package [out]` (`Packager.cs`) publishes the editor self-contained for win-x64 into `bin/`, copies the runtime
sources and headers, `third_party/fonts`, `third_party/icu`, `third_party/mgba`, `docs/user`, `docs/brand` (the
release banners and icon), `THIRD_PARTY.md` and `RELEASE-NOTES.md` (also as `bin/release-notes.md`, which the
Launcher's What's new reads), writes an empty `bin/editor.json` (D-13), a `README.txt` and the `agb-studio.root` marker
that lets `NativeBuild.RepoRoot` find the runtime in an installed copy, and zips it as
`AGB-Studio-<version>-win-x64.zip`.
devkitPro is not bundled.
