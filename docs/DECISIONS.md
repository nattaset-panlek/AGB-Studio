# AGB Studio · Decisions

The user set the goal on 2026-10-31: build and test every phase until the checklist is fully ticked, without stopping to ask. Each decision below is the recommended default taken under that goal. The user can override any of them; an override is logged in the checklist Scope log and the affected items are re-opened.

Status key: **decided (default)** = taken under the goal; **decided (user)** = answered by the user.

## D-01 Editor UI tech stack · decided (default)
- Options: .NET (Avalonia / WPF / WinUI), web UI in a shell (Electron / Tauri) + .NET sidecar, Qt, Dear ImGui.
- Answer: **Avalonia 11 on .NET 10, C#**. One language for editor, toolchain and scripting; Roslyn runs in-process; the mGBA core is loaded with P/Invoke; Avalonia.Headless renders windows to bitmaps, so E2E tests and SHOT captures run without a desktop.

## D-02 C#-like scripting front end · decided (default)
- Answer: Roslyn parses and binds user scripts against the `GBA` API reference assembly. A Roslyn analyzer enforces the GBA subset (GAE001-GAE006). A translator turns the bound syntax tree into C++20 (no exceptions, no RTTI). Methods marked `[Cutscene]` and `async` compile to Event Script VM bytecode instead.
- Supported subset (1.0): classes deriving from `Behaviour`, fields, properties with bodies, methods, `virtual` / `override`, `static` helpers, structs, enums, `int` / `bool` / `fixed` / `fixed2` / enums / `string` literals, `if` / `while` / `for` / `foreach` over arrays / `switch`, generics only on API calls (`Get<T>()`, `Is<T>()`), attributes `[Range]`, `[Cutscene]`, `[ScriptNative]`.
- `fixed` is 24.8 in a 32-bit int (as the Figma Script Editor text says); `fixed2` is two `fixed`.

## D-03 Project file format · decided (default)
- Answer: every file is UTF-8 JSON with a `"format"` version field. Names follow Figma (Figma is the scope): `<Name>.agbproj` (project), `scenes/<Name>.scene`, `maps/<Name>.map`, `assets/**/<file>.meta.json`, `fonts/<name>.font`, `data/<table>.db`, `dialogue/<name>.dlg`, `audio/sfx/<name>.sfx`, `scripts/**/*.cs`, `scripts/**/*.graph`, `text/strings.<lang>.json`. Generated files live only under `build/`.

## D-04 Repo layout · decided (default)
- Answer: `D:\devkitPro\AGB-Studio`, local git repository, branch `main`.
- `runtime/` C++ engine for the GBA, `tools/` C# toolchain and CLI (`agb`), `editor/` Avalonia app, `tests/` C# tests, `templates/`, `samples/`, `third_party/`, `docs/`.

## D-05 Licences · decided (default)
- AGB Studio (editor and toolchain): MIT.
- Runtime that ships inside user games: MIT, so games can be sold; the only obligation is the third-party notices that the build writes next to the ROM (`THIRD_PARTY.txt`).
- Third-party compliance is tracked in `docs/THIRD_PARTY.md`.

## D-06 Emulator integration · decided (user, 2026-10-31)
- Answer: the mGBA libretro core (`third_party/mgba/mgba_libretro.dll`, MPL-2.0, unmodified) hosted in-process by `tools/AgbStudio.Emu`. The headless runner `agb romtest` replaces mgba-rom-test. VBA-M (`D:\devkitPro\visualboyadvance-m.exe`) is the second emulator for compatibility checks. The user approved running the core.

## D-07 Analyzer rule prefix · decided (default)
- Answer: keep **GAE001-GAE006** exactly as in Figma; no Figma change needed. "GAE" does not contain the words the brand rule forbids.

## D-08 Script API namespace · decided (default)
- Answer: keep `using GBA;` as in Figma. It names the hardware descriptively and is not the product name.

## D-09 Templates and kits for 1.0 · decided (default)
- Answer: all **8** templates in Launcher · Templates, because Figma is the scope. Build order: Platformer, Top-down Adventure, Visual Novel, Puzzle, Shoot 'em up, Turn-based RPG, Beat 'em up, Racing (Mode 7). The New Project dialog shows the same 8 (its card grid scrolls); design issue 1.4-4 is resolved that way.

## D-10 Editor OS target · decided (default)
- Answer: Windows 10/11 x64 only for 1.0.

## D-11 Logic Graph and C# sync · decided (default)
- Answer: Graph -> C# always. C# -> Graph only for files the graph generated (the `.graph` file is the source; the generated `.cs` carries a header and is read-only in the editor).

## D-12 Cartridge flashing hardware · decided (default)
- Answer: (a) copy the `.gba` to a flash cart's SD card folder (EZ-Flash, EverDrive and similar), fully supported; (b) GBxCart RW over USB serial: detection of the device and cartridge, then writing through the device protocol. (b) is verified only with hardware; without hardware it is marked `n/a until hardware`.

## D-13 Online services · decided (default)
- Answer: Help › Report a Bug… opens the issue page set in `editor.json` (`bugUrl`); Check for Updates reads a release feed URL set in `editor.json` (`updateFeed`) and says "No update source configured" when empty; Website / Community / Release notes open configured URLs, with release notes also bundled offline. Google Sheets: export the sheet as CSV, then Import CSV (no live connection).

## D-14 Audio formats and rates · decided (default)
- Answer: music MOD and XM only (as Figma); WAV sound effects resampled to 11 kHz mono 8-bit (as Figma); Maxmod mixing 21 kHz with 8 channels by default.

## D-15 Hot reload · decided (default)
- Answer: on save in Play mode the editor rebuilds incrementally, writes a hand-over snapshot (current scene, actor positions, game variables) into save RAM, reloads the new ROM and the Debug runtime restores the snapshot at boot. Inspector value edits apply live through the debug channel without a rebuild.

## D-16 Meaning of "Simulate" · decided (default)
- Answer: Simulate means running the real ROM in the embedded mGBA core inside the editor. There is no separate PC-side simulation.

## D-17 Thai data sources · decided (default)
- Answer: word list from ICU's Thai break dictionary (`thaidict.txt`, Unicode licence); default Thai font Noto Sans Thai (SIL OFL 1.1). Both are recorded in `docs/THIRD_PARTY.md`.

## D-18 Editor viewport renderer · decided (default)
- Answer: an editor-side software renderer in C# that draws from the same converted tile, palette and map data the ROM uses. Tests compare its output pixel for pixel with mGBA captures of the same scene.

## D-19 Distribution · decided (default)
- Answer: self-contained win-x64 folder packaged as a zip (portable install), first-run detection of an existing devkitPro (not bundled). No code-signing certificate is available, so builds are unsigned and the release notes say so. Update channel per D-13.

## D-20 Template and sample content · decided (default)
- Answer: all template and sample art, music and text are made new for AGB Studio (pixel art drawn programmatically by the asset generator in `templates/_source`, chiptune modules generated as XM files, text written fresh). The missing in-game screens (boot splash, title, pause, options) are designed in Figma section "AGB Studio · Screens" before they are built, then built to match.

## D-21 C# source-level debugging · decided (default)
- Answer: cooperative debugging in Debug builds. The translator inserts line probes and a shadow call stack into the generated C++; the runtime checks a breakpoint table in the debug channel and pauses inside the game loop while the editor reads state through emulator memory. No GDB stub.

## D-22 The `fixed` type name in scripts · decided (default)
- Question: Figma writes `public fixed speed = 1.5;`, but `fixed` is a reserved word in standard C# (unsafe fixed buffers), so that line does not parse as normal C#.
- Options: (a) rewrite the `fixed` keyword to `Fixed` before parsing in the built-in Script Editor and accept `Fixed` / `@fixed` in external IDEs; (b) change every Figma example to `Fixed`; (c) a custom parser.
- Answer: **(a)**. The built-in editor shows and saves `fixed` exactly as in Figma; the compiler rewrites it to `Fixed` (same length, so lines and columns stay right) and code fixes write `fixed` back. Projects opened in an external IDE use `Fixed` (the generated project adds `global using Fixed = GBA.@fixed;`) or `@fixed`.

## D-23 Menu / UI kit · decided (default)
- Question: the blueprint lists a "Menu / UI kit" and Figma refers to a boot splash, title screen, pause menu and an options menu for button remap, but none of them was designed (checklist 1.3 and 1.4 item 6).
- Answer: the four screens were designed in Figma section "AGB Studio · Screens", In-game row (frames "In-game · Boot splash", "In-game · Title screen", "In-game · Pause menu", "In-game · Options"), then built to match: `agb::ui` in the runtime, `ProjectFile.Ui` (pause menu, colours, font) and `SceneFile.Menu` (title screens) in the project files. Labels come from string keys `ui.*` when the Localization tables have them.

## D-24 Performance targets: clean build, project open, Play start · decided (default)
- Question: G-PERF.03 asks for targets for a clean build, opening a project and starting Play, agreed in Phase 1. None were recorded, so nothing was measured against them.
- Options: (a) wall-clock targets for the Sky Hopper fixture on the build machine; (b) targets per asset count; (c) no targets.
- Answer: **(a)**, for Sky Hopper (5 scenes, 12 sprites, 12 scripts, 3 fonts, 9 audio files, 6 tables, 340 strings) on the build machine (16 GB, devkitARM with Ninja, `AGB_BUILD_JOBS=2`, other builds running):
  - clean build (build folder deleted, devkitARM compiles the runtime and every generated file, links, gbafix): **under 60 s**;
  - project open (the Opening Project steps: read the project and its scenes, load every asset, compile the scripts; the UI work of showing the scene is not included): **under 5 s**;
  - Play start (a build with no changes, load the ROM into the embedded mGBA core, run to the first game frame): **under 3 s**.
  The one-value script edit stays at under 1 s (G-PERF.02, T-D06). Tests: `FixtureBuildTests.FixtureBuildsCleanWithinTheTarget`, `ProjectOpensWithinTheTarget`, `PlayStartsWithinTheTarget`; each run appends its times to `build/perf-targets.txt`.
- Measured 2026-10-31: clean build 14.6 s, project open 0.2 s (scripting compiler already loaded in the process), Play start 0.4 s.
- Audio mixer (G-PERF.05), measured with the runtime profiler (Audio share of the 280,896-cycle frame, maxmod at 21 kHz with 8 mixing channels, a Release ROM, `RuntimeTextAudioTests.MixerShareFor8ChannelsAt21kHzIsMeasured`): **14.5%** with 8 channels playing, 5.6% with 1 channel (about 1.3% per channel on a base of about 4.3%). Figma's Audio window shows **6%** for 8 channels at 21 kHz, so the runtime is well above the Figma figure; the loop length of the samples does not change it. Getting near 6% needs fewer channels or a lower mixing rate (the runtime always mixes at 21 kHz and ignores Project Settings · Audio "mixing rate") or a faster mixer: a decision for the user.

## D-25 "Split into N palettes" for a 48-colour image · decided (default)
- Question: Figma's Import Asset · Errors card offers "Split into 3 palettes" for an image with 48 colours; the importer says "Split into 4 palettes".
- Answer: **4**. A 4bpp GBA palette has 16 entries and entry 0 is transparent (for sprites, and for background tiles), so one palette holds 15 colours: 3 palettes hold at most 45, and every colour must be in at least one palette, so 48 colours need ceil(48 / 15) = 4. The message keeps "Split into 4 palettes"; 31 to 45 colours give "Split into 3 palettes". Test: `ImageConverterTests.FortyEightColoursNeedFourPalettesNotThree`. The Figma frame text is the one to change (Scope log).
