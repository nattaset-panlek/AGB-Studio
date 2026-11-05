# AGB Studio

![AGB Studio](docs/brand/banner-github-1280-320.png)

AGB Studio is a desktop editor and toolchain for making games for the Game Boy Advance. You lay out scenes, draw
sprites and tile maps, write game logic in a C# subset (or with the Logic Graph), write dialogue and translations
(Thai and English included), and build a `.gba` ROM that runs in an emulator or on a flash cart. The editor runs the
ROM in an embedded mGBA core for Play mode and the Debugger.

![Eight games made from the AGB Studio templates, running on the GBA](docs/readme/templates.gif)

*The eight project templates, playing as real ROMs in mGBA. Pick one when you create a project.*

## Why AGB Studio

- **A real editor for real hardware.** Scenes, tile maps, sprites, UI, audio and data are edited in one window, and the
  result is a `.gba` ROM you can play in an emulator or on a cartridge.
- **Write C#, ship native code.** Game logic is a C# subset that compiles through C++ to ARM code, with no virtual machine
  on the console. If you would rather not type, the Logic Graph generates the same C#.
- **Press Play, not Export.** The game runs inside the editor in an embedded mGBA core. Save a file while it runs and hot
  reload carries on in the new ROM.
- **See the hardware limits as you work.** Budget meters show VRAM, OAM, palettes and work RAM. The Debugger shows the
  tiles in VRAM, all 128 sprite slots, palettes, memory, time per frame, and breakpoints in your scripts.
- **Thai as a first-class language.** Variable-width fonts, stacked vowels and tone marks in the right place, automatic
  Thai word wrap, and per-language strings that switch while the game runs.
- **Start from a game that already works.** Eight templates: Platformer, Top-down Adventure, Visual Novel, Puzzle,
  Shoot 'em up, Turn-based RPG, Racing (Mode 7) and Beat 'em up. Each comes with title, pause and options menus, ready to change.
  Or pick Blank to start from one empty scene.
- **Sell what you make.** The runtime linked into your game is MIT licensed.

## See it work

### Build a scene

Select an actor and move it, then paint the tile map straight on the GBA screen.

![Moving an actor and painting tiles in the Scene Editor](docs/readme/scene-editor.gif)

### Play it inside the editor

One click builds the ROM and runs it, with the hitboxes, live actor values and budget beside the game.
The Debugger opens on the same running game.

![Running and playing the game in Play Mode, then opening the Debugger](docs/readme/play-mode.gif)

### Design the UI

Title screens, pause menus, options, choices and HUDs are UI pages. Start from a preset, drag elements on the real
240 x 160 screen, and add controls from the toolbox.

![Adding a preset page and editing it in the UI Editor](docs/readme/ui-editor.gif)

### Change language in the game

Options comes with every template. Switching to Thai redraws the menus at once, and the choice is kept in the save.

![Switching a template's menus from English to Thai in the game](docs/readme/language-switch.gif)

## The editor

<table>
  <tr>
    <td width="50%"><img src="docs/readme/scene-editor.png" alt="Scene Editor"><br><b>Scene Editor</b><br>The scene as the GBA draws it, with hierarchy, components and budget</td>
    <td width="50%"><img src="docs/readme/ui-editor.png" alt="UI Editor"><br><b>UI Editor</b><br>Pages, layout groups and ready-made menus, drawn as the game draws them</td>
  </tr>
  <tr>
    <td><img src="docs/readme/script-editor.png" alt="Script Editor"><br><b>Script Editor</b><br>C# with GBA checks, the API beside you and the C++ file it compiles to</td>
    <td><img src="docs/readme/logic-graph.png" alt="Logic Graph"><br><b>Logic Graph</b><br>Node-based logic that generates C# you can read</td>
  </tr>
  <tr>
    <td><img src="docs/readme/tilemap.png" alt="Tilemap Editor"><br><b>Tilemap Editor</b><br>Layers, tile properties, collision and auto-tile rules</td>
    <td><img src="docs/readme/sprite-editor.png" alt="Sprite Editor"><br><b>Sprite Editor</b><br>Pixel editing, animations and palette slots in the GBA's limits</td>
  </tr>
  <tr>
    <td><img src="docs/readme/debugger.png" alt="Debugger"><br><b>Debugger</b><br>VRAM, OAM, palettes, memory, profiler and breakpoints on the running game</td>
    <td><img src="docs/readme/debugger-memory.png" alt="Debugger memory view"><br><b>Memory view</b><br>A live map of where your RAM and VRAM go</td>
  </tr>
  <tr>
    <td><img src="docs/readme/font-manager.png" alt="Font Manager"><br><b>Font Manager</b><br>Fonts from TTF, BDF or an image, with Thai layout and a preview in a real text box</td>
    <td><img src="docs/readme/localization.png" alt="Localization"><br><b>Localization</b><br>Every string by language, with what is missing and how it fits on the screen</td>
  </tr>
  <tr>
    <td><img src="docs/readme/dialogue.png" alt="Dialogue Editor"><br><b>Dialogue Editor</b><br>Conversations, choices, conditions and commands, previewed in the game's text box</td>
    <td><img src="docs/readme/world-map.png" alt="World Map"><br><b>World Map</b><br>Every scene as a card, with links for doors, triggers and level ends</td>
  </tr>
  <tr>
    <td><img src="docs/readme/audio.png" alt="Audio"><br><b>Audio</b><br>Music through Maxmod, with tracker patterns and channel use</td>
    <td><img src="docs/readme/sfx-designer.png" alt="SFX Designer"><br><b>SFX Designer</b><br>Effects from the GBA's own sound channels, which take no ROM</td>
  </tr>
  <tr>
    <td><img src="docs/readme/database.png" alt="Database"><br><b>Database</b><br>Tables of items, enemies and quests, read from scripts by name and stored in ROM</td>
    <td><img src="docs/readme/palette-manager.png" alt="Palette Manager"><br><b>Palette Manager</b><br>All 32 palette sets, duplicates to merge, and effects such as fades and colour cycling</td>
  </tr>
  <tr>
    <td><img src="docs/readme/build-report.png" alt="Build Report"><br><b>Build Report</b><br>ROM size by kind, the largest assets, memory use and build history</td>
    <td><img src="docs/readme/templates.png" alt="Project templates"><br><b>Templates</b><br>Choose a genre and get a game that already plays</td>
  </tr>
</table>

*The editor pictures are rendered from the editor's own window on the Sky Hopper sample project. In the GIFs the input is
scripted and the mouse pointer is drawn on afterwards. The game clips are recorded from the built ROMs.*

## What is in the repository

| Folder | What it is |
|---|---|
| `runtime/` | The C++20 engine that runs on the GBA and is linked into every game (no heap, no exceptions, no RTTI) |
| `tools/` | The C# toolchain: project formats, asset converters, the build pipeline, the C# to C++ scripting compiler, the emulator host and the `agb` command line |
| `editor/` | The Windows editor (Avalonia on .NET 10), output `AGBStudio.exe` |
| `tests/` | xUnit suites; they also build and run the runtime test ROMs headless |
| `third_party/` | The mGBA libretro core, fonts and the ICU Thai word list, each with its licence |
| `docs/` | Developer docs, the build checklist, evidence, and the user docs in `docs/user/` |

Templates, samples and the fixture project are not stored as files: they are generated by code in
`tools/AgbStudio.Build/Content` when a project is created.

## Requirements

- Windows 10 or 11, x64
- .NET SDK 10
- devkitPro with devkitARM, libtonc, libgba (maxmod) and the GBA tools (gbafix, mmutil), found through `DEVKITPRO`,
  `C:\devkitPro` or `D:\devkitPro`
- Ninja is optional: without it the build runs the same steps itself

The versions in use are in [docs/TOOLCHAIN.md](docs/TOOLCHAIN.md).

## Build and run

```
dotnet build AGB-Studio.sln -c Release
editor\AgbStudio.Editor\bin\Release\net10.0\AGBStudio.exe
```

Build a game project from the command line:

```
tools\agb\bin\Release\net10.0\agb.exe build <project folder> [--release] [--clean]
```

## Test

```
ci.cmd
```

builds the solution, runs `agb lint` (no emoji, product name, runtime hardware and float rules), scans commit
messages for emoji, lists commits that name neither a Figma window (`T-...`) nor `infra:`, and runs the three test
suites. The C# tests build every runtime test ROM and run it in the embedded mGBA core. Details and the other `agb`
commands are in [docs/BUILD.md](docs/BUILD.md).

## Package

```
tools\agb\bin\Release\net10.0\agb.exe package [out folder]
```

publishes the editor self-contained for win-x64 and writes `AGB-Studio-<version>-win-x64.zip` (default folder
`build\package`): a portable folder with `bin\AGBStudio.exe`, the runtime sources games are built from, fonts, the
Thai word list, the emulator core and the user docs. devkitPro is not included; the editor finds it or asks for it.

## Documentation

- [docs/user/](docs/user/): one page per editor window, the GBA hardware guide, tutorials and the licence of your games
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md): how the code is organised, runtime layers, build pipeline
- [docs/EDITOR-ARCHITECTURE.md](docs/EDITOR-ARCHITECTURE.md): editor code, services and how to add a window
- [docs/BUILD.md](docs/BUILD.md): building, testing, the `agb` command line, commit rules
- [docs/FILE-FORMATS.md](docs/FILE-FORMATS.md): project and asset files
- [docs/TOOLCHAIN.md](docs/TOOLCHAIN.md): tool versions
- [docs/DECISIONS.md](docs/DECISIONS.md): the decisions D-01 to D-23
- [docs/ENGINE-CHECKLIST.md](docs/ENGINE-CHECKLIST.md): the build checklist; the Figma design is the scope
- [docs/THIRD_PARTY.md](docs/THIRD_PARTY.md): third-party components and their licences
- [docs/RELEASE-NOTES.md](docs/RELEASE-NOTES.md): release notes

## Contributing

Every change names the Figma window it implements (`T-...`) or says `infra: <reason>`; use the commit template
(`git config commit.template .gitmessage`) and the pull request template in `.github/`. Build only what the Figma
design shows (checklist section 1). No emoji anywhere.

## Licence

AGB Studio is released under the MIT licence ([LICENSE](LICENSE)). The runtime linked into your games is under the same
licence, so games made with AGB Studio can be sold; see [docs/user/licence-for-your-games.md](docs/user/licence-for-your-games.md).
Files in `third_party/` keep their own licences, listed in [docs/THIRD_PARTY.md](docs/THIRD_PARTY.md).
