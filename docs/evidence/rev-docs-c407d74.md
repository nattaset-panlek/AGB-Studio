# REV of the developer docs at c407d74 (G-DOCS.04)

Checklist item: G-DOCS.04 "Developer docs: ARCHITECTURE, BUILD, file formats, DECISIONS, TOOLCHAIN, THIRD_PARTY kept
current · Verify: `REV` at each phase exit".

Snapshot: worktree `D:\devkitPro\AGB-Studio-wt\review`, detached at `c407d74` (2026-10-31). Read-only: every statement of
`docs/ARCHITECTURE.md`, `docs/BUILD.md`, `docs/FILE-FORMATS.md`, `docs/DECISIONS.md`, `docs/TOOLCHAIN.md` and
`docs/THIRD_PARTY.md` (plus `docs/EDITOR-ARCHITECTURE.md`, the editor half of the architecture) was read against the
code at that commit with grep, `git log -S` and `git blame`. Nothing was built or run. Doc line numbers are those of
the files at `c407d74`.

**Result: G-DOCS.04 is OPEN.** 60 statements are out of date or wrong (listed below): 54 in the six docs the item
names (ARCHITECTURE 13, BUILD 9, FILE-FORMATS 21, DECISIONS 6, TOOLCHAIN 3, THIRD_PARTY 2) and 6 in
EDITOR-ARCHITECTURE.md. None of them is about
the music-volume change of `c407d74` / `6ab2838`: ARCHITECTURE.md and FILE-FORMATS.md describe the four volume levels
and `SceneDef::musicVolume` correctly. Most of the list is older drift: the link paragraph of FILE-FORMATS.md that
predates the link kinds, fields the editor writes into its own files, the commit-reference exemption list added in
`ddf96c0`, the runtime notice and brand folder added in `2914d22`, and the OTF support added in `381e3ea`.

## docs/ARCHITECTURE.md (13)

| Line | The doc says | The code does instead |
|---|---|---|
| 12 | every `runtime/tests/<name>/` folder becomes a test ROM run by `agb runtime-test` / `RuntimeRomTests` | `host_assert` and `host_probe` are left out of that discovery and driven by their own C# tests (`tools/AgbStudio.Build/RuntimeTests.cs:10`, `:15-17`; `tests/AgbStudio.Tests/RuntimeRomTests.cs:17`). Line 231 repeats "all runtime test ROMs". |
| 24-25 | content code in `tools/AgbStudio.Build/Content` (... `Audio/XmWriter.cs`, `Audio/Chiptune.cs`) | those two files are in `tools/AgbStudio.Build/Audio/`, not under `Content`; `Content/SampleProjects.cs` (the "mini" sample, `tools/agb/Program.cs:149`) is not listed |
| 100-101 | render half order: camera, map layer streaming, animated tiles, sprites to the OAM shadow, palette effects, debug table | code order is camera follow and shake, `actors::syncSprites` + `sprites::build` (OAM), `palettes::update`, then the map layer commit, `bg::animateTiles`, debug table (`runtime/src/framework/game.cpp:310-335`; phase enum order BuildOam, PaletteFade, Tilemaps in `runtime/include/agb/debug.hpp:73`) |
| 103-104 | the profiler counts Logic and Render cycles and overruns | it also counts Audio and Idle and the VBlank cycles left after the commit (`runtime/include/agb/system.hpp:23`, `:32-33`; `runtime/src/core/system.cpp:109`, `:122`) |
| 125-126 | symbol prefixes `agbasset_` / `agbfont_` / `agbtext_` / `agbdata_` exist so the report can attribute ROM bytes per asset | the report attributes linker-map input sections by the generated object file name (`assets/sprite_*`, `tileset_*`, `text/font_*`, `data/db_*`, `maps/map_*`, `audio/soundbank`), not by symbol prefix (`tools/AgbStudio.Build/Pipeline/BuildReport.cs:106-118`, `:211-237`) |
| 130-131 | Door fires on the overlap "while Up is pressed" | it fires on the frame Up goes down during the overlap (`game.cpp:366` uses `api::Input::Pressed(Key::Up)`; `runtime/include/agb/input.hpp:22`); holding Up does not fire it |
| 143-144 | build step 2 compiles `[Cutscene]` methods, dialogue "and Logic Graph code" to bytecode | a `.graph` becomes a C# Behaviour class (`tools/AgbStudio.Scripting/Graph/GraphCodeGen.cs:9`, `:29`) that is translated to C++ (tier 1); only `[Cutscene]` methods and dialogue become bytecode (line 218 of the same doc is right) |
| 147 | step name "Compile (devkitARM g++)" | the step is "Compile (devkitARM g++ -O2)" (`tools/AgbStudio.Build/Pipeline/GameBuilder.cs:72`) |
| 154 | report.json has "per asset ROM bytes from the linker map" | true for graphics, maps, fonts, text and data; music and sounds share the soundbank's bytes split by source file size and DMG effects split evenly (`BuildReport.cs:128-137`) |
| 157 | `THIRD_PARTY.txt` holds "the libtonc and maxmod notices" | it starts with the AGB Studio runtime MIT notice, then libtonc and maxmod (`GameBuilder.cs:1430-1449`, since `2914d22`) |
| 171 | the "Build, fails the build" row lists only the per-scene VRAM and palette checks | save data larger than the save type (`GameBuilder.cs:822-828`) and a ROM over 32 MB (`GameBuilder.cs:128-135`, `BuildReport.cs:192-197`) also fail the build |
| 213 | tier 2: "map scripts compile to bytecode" | Scene Settings OnLoad / OnEnter / OnExit become static C# methods of `SceneEvents_<Scene>` (`tools/AgbStudio.Scripting/ScriptCompiler.cs:312-334`), translated to C++ and called as functions (`Translation/RegistryWriter.cs:107`, `:259`; `SceneDef::onLoad` is a function pointer, `runtime/include/agb/game.hpp:72`); `script::runImmediate` is called only by the `framework_bus_vm` test. The header comment `runtime/include/agb/script.hpp:1-2` has the same stale claim. |
| 238-240 | `agb package` copies runtime, `third_party/fonts|icu|mgba`, `docs/user`, THIRD_PARTY.md, RELEASE-NOTES.md | it also copies `docs/brand` (`tools/AgbStudio.Build/Packager.cs:43-44`, since `2914d22`) |

## docs/BUILD.md (9)

| Line | The doc says | The code does instead |
|---|---|---|
| 20-22 | `check-commit-refs.ps1 -Strict` fails on a commit after the baseline that names neither; earlier commits are in the Scope log | commits listed in `tools/commit-refs-known.txt` are reported as "(known, commit-refs-known.txt)" and do not fail (`tools/check-commit-refs.ps1:32-55`, `:69-71`, since `ddf96c0`); `24d3c1d` is inside the checked range and is exempt only through that file; BUILD.md never names the file |
| 39 | `agb build <project folder> ...` | the folder is optional and defaults to "." (`tools/agb/Program.cs:46`, `:125`) |
| 40 | `agb runtime-test` builds `runtime/tests/*` | it skips the `host_*` folders (`tools/AgbStudio.Build/RuntimeTests.cs:8-10`) |
| 52 | output goes to `<project>/build/` | that is the default; Project Settings · Build output folder moves it (`tools/AgbStudio.Core/Project/GameProject.cs:35`) |
| 56 | `THIRD_PARTY.txt` "(libtonc and maxmod notices)" | runtime MIT notice first, then libtonc and maxmod (`GameBuilder.cs:1431-1449`) |
| 72-74 | the package holds `bin/`, runtime sources, fonts, word list, emulator core, `docs/user`, THIRD_PARTY.md, release notes | it also holds `docs/brand` (`Packager.cs:44`), `README.txt` (`:55-59`), `bin/editor.json` (`:53-54`), `bin/release-notes.md` (`:51`) and the root marker (`:47`), all asserted by `PackagingTests.PackageHoldsTheRuntimeFontsCoreAndDocsAndFindsItsRoot` (`tests/AgbStudio.Tests/PackagingTests.cs:19-23`) |
| 85-88 | `-Strict` makes it exit 1; commits from before the rule are in the Scope log | same as lines 20-22: commits in `commit-refs-known.txt` are exempt from `-Strict` |
| 47 (wording) | "the `loop.mod` of `audio_music` (also its `plain.mod`)" | `make-test-audio` writes `beep.wav`, `tune.mod` and `loop.mod` only (`tools/agb/Program.cs:246`); the committed `plain.mod` is a byte copy of `loop.mod`. Correct only if read as "loop.mod is also used as plain.mod". |
| 27-29 (wording) | every run keeps its artifacts | a run that fails before the test step (build, lint, commit checks) copies the previous run's `.trx` files and ROMs, because `build\ci\results` is deleted only after the commit-refs step (`ci.cmd:20`; `tools/ci-artifacts.ps1:17-26`) |

## docs/FILE-FORMATS.md (21)

| Line | The doc says | The code does instead |
|---|---|---|
| 3 | every file is written by `GameProject.Save` | `.map`, `.font`, `.dlg` and `.graph` have their own writers in the editor (`editor/AgbStudio.Editor/Windows/Tilemap/MapDocument.cs:112-123`, `Windows/Font/FontLibrary.cs:74-84`, `Windows/Database/ProjectData.cs:66-81`, `Windows/LogicGraph/GraphDocument.cs:91-116`) |
| 4 | each top-level document carries `"format": 1` | prefab files are plain `ActorData` with no format field (`tools/AgbStudio.Core/Project/SceneModel.cs:54-72`; written at `Windows/SceneEditor/SceneCommands.cs:129-131`; `SchemaTests` skips them, `tests/AgbStudio.Tests/SchemaTests.cs:92-94`) |
| 4-5 | a file with a higher format is refused | only `.agbproj` (`GameProject.cs:50`), `.dlg` (`tools/AgbStudio.Scripting/Dialogue/DialogueFile.cs:66-68`) and `.graph` (`Graph/LogicGraph.cs:99-101`) check it; every other file goes through `GameProject.Load<T>` (`GameProject.cs:74-86`) with no version check |
| 11 | schemas are generated from the C# classes "with the settings the files are written with" | the editor writes fields that are in no schema (next four rows); the schema validator rejects unknown fields (`SchemaTests.cs:259`), so such a file fails its own schema |
| 17-42 | the project folder tree | leaves out the IDE files written into the project root: `<Name>.csproj`, `.editorconfig`, `.vscode/settings.json`, `.vscode/extensions.json` (`tools/AgbStudio.Scripting/CsprojGenerator.cs:7`, `:17`, `:107`, `:119-120`) |
| 23 and 40 | `prefabs/*.prefab` | listed twice in the same tree |
| 26-27 | `tilesets/*.png`, `images/*.png` | `.aseprite` files are accepted too (`GameProject.cs:118-119`, `:141-142`; loaded by `ColorImage.Load`, `tools/AgbStudio.Build/Graphics/TileConverter.cs:36`) |
| 31 | sfx `.wav` (and baked SFX Designer effects) + `.meta.json` | a baked effect gets the `.wav` only, no `.meta.json` (`Windows/Audio/SfxDesignerPage.cs:320-331`); only Import writes a SoundMeta (`Windows/Import/AssetImporter.cs:352-357`) |
| 33 | fonts next to their `.ttf` / `.bdf` / image source | `.otf` sources too (`AssetImporter.cs:122`; `Windows/Font/FontManagerPage.cs:427`); line 131 was updated in `381e3ea`, line 33 was not |
| 50 | `input`: "the ten buttons" with an action and keyboard key | seven rows: A, B, L, R, Start, Select and one "D-pad" row, each with `button`, `action`, `keyboard` and `gamepad` (`tools/AgbStudio.Core/Project/ProjectModel.cs:61-75`) |
| 56 | `ui`: colours "and layout" of the title, pause and options screens | `UiSettings` holds `pauseMenu`, `titleScene`, `font`, seven colours and `dim`, no layout (`ProjectModel.cs:43-56`); the title menu layout is each scene's `menu` (`SceneModel.cs:139-147`) |
| 79-82 | "The runtime currently treats every kind like `OnTouch`" | wrong, and contradicts lines 67-72: Door fires on `inside && Input::Pressed(Up)`, Respawn on `inside || lost` (`runtime/src/framework/game.cpp:365-370`); the paragraph predates the link kinds (`5b436be`, before `1974ee6`) |
| 90-93 | map fields | the Tilemap Editor also writes top-level `bgSize` (32x32 / 32x64 / 64x64) and `bpp` (8) and keeps unknown fields (`MapDocument.cs:40-42`, `:82-85`, `:112-115`); neither is in `MapFile` or `map.schema.json` |
| 129-131 | font fields | `cellHeight` and `thaiWordWrap` are missing (`AssetModel.cs:125`, `:135`); the Font Manager also writes `drawTo` (`FontLibrary.cs:30-31`, `:52-54`, `:76-77`), which is not in `font.schema.json` |
| 134-135 | db fields | `showInInspector` and `reference` (the referenced table) are missing (`AssetModel.cs:164-165`) |
| 140 | the build turns every effect that is not baked into DMG registers | it also skips an effect that has a `.wav` of the same name in `assets/audio/sfx` (`GameBuilder.cs:428-431`) |
| 141-142 | graph: `class`, `nodes`, `links` | the editor also writes `comments`, `collapsed` and `disabled` (`GraphDocument.cs:64-88`, `:91-110`), none of them in `graph.schema.json` |
| 144 | dlg speakers: `name`, `portrait`, `blip`, `textSpeed` | the Dialogue Editor also writes `names` (per language) and `color` (`ProjectData.cs:19-31`, `:65-75`; `Windows/Dialogue/DialogueEditorPage.cs:422-424`), not in `dlg.schema.json` |
| 150-159, 41 | the `build/` table | leaves out `build/ide/` (`CsprojGenerator.cs:14`, `:43-46`), `build/play/<rom>.sav` (`Windows/Play/PlayController.cs:146`) and `build/save-backups/` (`Windows/Build/FlashDialog.cs:174`) |
| 176-178 | files in `%APPDATA%\AGBStudio\` | leaves out the crash logs `crash\crash-<time>.log` (`editor/AgbStudio.Editor/Services/CrashGuard.cs:9-11`, `:23`) |
| 178 | `editor.json` holds website, bug, update, community and release-notes URLs | it also has `devkitProUrl` (`editor/AgbStudio.Editor/Windows/Menus/EditorLinks.cs:19-20`) |

## docs/DECISIONS.md (6)

| Line | The doc says | The code does instead |
|---|---|---|
| 7 | every entry has Date, Question, Options, Answer | D-25 (164-166) has no Date and no Options; D-26 (167-176) has no Date; no blank line between D-25 and the D-26 heading |
| 27 | D-03: every file is JSON with a `"format"` field; generated files live only under `build/` | prefabs have no format field (see FILE-FORMATS line 4); prefabs and `assets/palettes/` are not in the list; generated files are also written outside `build/`: `<Project>.csproj`, `Directory.Build.props`, `.editorconfig`, `.vscode/*` at the project root (`CsprojGenerator.cs:87-120`) and the Logic Graph's `.cs` next to the `.graph` (`GraphDocument.cs:118`) |
| 94 | D-13: Check for Updates says "No update source configured" | the text is "Could not check for updates: no update source is set. Add an updateFeed address to editor.json next to AGBStudio.exe." (`EditorLinks.cs:70`; asserted in `tests/AgbStudio.Editor.Tests/Windows/Menus/MenuActionTests.cs:369`) |
| 94 | D-13: Website / Community / Release notes open configured URLs | `communityUrl` and `releaseNotesUrl` are parsed (`EditorLinks.cs:17-18`) and used nowhere; Launcher Community has only Release notes and Report a bug (`Windows/Launcher/LauncherPage.cs:496`); Release notes always opens the bundled notes (`LauncherPage.cs:507-513`); Website is only About › Website (`Windows/Menus/AboutDialog.cs:47-48`); the Help menu has no Website, Community or Release notes item (`Commands/Catalog.cs:95-100`) |
| 137 | D-20: chiptune modules generated as XM | the mini sample and Sky Hopper generate MOD modules (`tools/AgbStudio.Build/Audio/Chiptune.cs:7-9`; `Content/SampleProjects.cs:95`; `Content/SkyHopper.cs:292-293`); templates use XM (`Content/Templates/TemplateKit.cs:117`) |
| 138 | D-20 note: generator files include `Audio/XmWriter.cs`, `Audio/Chiptune.cs` under `Content` | they are in `tools/AgbStudio.Build/Audio/`; `Content/SampleProjects.cs` is not listed |

## docs/TOOLCHAIN.md (3)

| Line | The doc says | The code does instead |
|---|---|---|
| 49 | gbafix and mmutil run in every game build | mmutil runs only when the project has MOD / XM / WAV files and the soundbank cache is stale (`GameBuilder.cs:466`, `:476`), and in runtime tests only for tests with an `audio/` folder (`RuntimeTests.cs:34-36`) |
| 70 | git is used for `tools/check-commit-*.ps1` in `ci.cmd` | also by `tools/ci-artifacts.ps1` (commit in the run folder name and `summary.txt`, `:11`, `:32`) |
| 71 | Windows PowerShell runs "the commit-message checks in `ci.cmd`" | it also runs `tools\ci-artifacts.ps1` on every pass and failure (`ci.cmd:24`, `:28`) |

## docs/THIRD_PARTY.md (2)

| Line | The doc says | The code does instead |
|---|---|---|
| 15 | SkiaSharp is for "font rasterising and image I/O in the editor and toolchain" | the toolchain has no SkiaSharp reference (`tools/AgbStudio.Build/AgbStudio.Build.csproj`, `tools/AgbStudio.Core/AgbStudio.Core.csproj`); it reads PNG with its own `tools/AgbStudio.Core/Png.cs` and rasterises fonts with `Fonts/TrueType.cs`, `Cff.cs`, `TextRenderer.cs`; in the editor SkiaSharp is Avalonia's renderer (`editor/AgbStudio.Editor/Program.cs:14` `.UseSkia()`) |
| 28 | Noto Sans (`fallback-latin.ttf`): Latin glyphs for fonts that lack them, ships in "toolchain" | it is also the source font of the boot splash text built into every game with Boot splash on (`tools/AgbStudio.Build/Content/SplashArt.cs:27-29`, `:46`; `GameBuilder.cs:831-835`) and is copied into Sky Hopper as `NotoSans.ttf` for its `pixel8` and `title_big` fonts (`Content/SkyHopper.cs:324`, `:332`, `:336`), so rasterised Noto Sans glyphs ship inside games |

## docs/EDITOR-ARCHITECTURE.md (6, read with ARCHITECTURE)

| Line | The doc says | The code does instead |
|---|---|---|
| 14 | `Theme/` holds Chrome / Buttons / Navigation / Panels axaml | it also holds `Controls.axaml` (component library) and `Conv.cs` |
| 27 | no hex literals outside `Theme/` (a test scans for them) | the scan also exempts `Gallery/`, `Services/ThemeService.cs` and `Graphics/SvgImage.cs` (`tests/AgbStudio.Editor.Tests/Design/DesignSystemTests.cs:141-152`); `ThemeService.cs:14`, `:16` hold hex accent values |
| 83 | `ProjectSettings` is the project's `ProjectFile` | it is an `IProjectSettingsStore`; the `ProjectFile` is its `.Settings`, with `Save()` and `Saved` (`Shell/EditorServices.cs:39`; `Services/ProjectSettingsStore.cs:9-16`) |
| 106-107 | a new window adds its id to `ShotTests.Windows()` | since `6b790b2` a window group adds an `IShotSource` class found by reflection; `ShotTests.Windows()` holds only the Phase 10 shell windows (`tests/AgbStudio.Editor.Tests/Shot/ShotTests.cs:20-25`, `:66-70`, `:109`) |
| 111 | Light frames are rendered with `Preferences.Theme = ThemeMode.Light` | the code sets `fx.Services.Preferences.Current.Theme` (`ShotTests.cs:159`) |
| 122 | `figma-export.mjs` "(all steps: tokens, icons, art, menus, shots)" | with no argument it runs those five; the brand, prototype, type and gameview steps run only when named (`Shot/figma-export.mjs:2-3`, `:20`) |

## Could not be settled from the repository

- `TOOLCHAIN.md` versions and banners (lines 9-23, 27-34, 42-47, 57, 67-73) depend on the machine; all matched the
  build machine on 2026-10-31 except that line 17 "libgba / libfat" gives only the libgba version (libfat is the
  separate package libfat-gba 1.1.5).
- `THIRD_PARTY.md:8`: `gba_crt0.o` (devkitarm-crtls) and libgcc / newlib objects are linked into every ROM
  (`-specs=gba.specs`, `NativeBuild.cs:72`); whether they need a row or a notice in `THIRD_PARTY.txt` is a licence
  question, not a doc mismatch.
- `THIRD_PARTY.md:40-44` (the package DLL check, 227 DLLs at `dec626c`) needs a publish to repeat; no package
  reference changed since.
- `FILE-FORMATS.md:115` "`volume` (0..256)" is the built value; the file stores 0..1 (`AssetModel.cs:76`;
  `SoundbankBuilder.cs:98`). `:106-108` lists SpriteMeta `oamSize` and `compress`, which the build never reads
  (`SpriteConverter.cs:41-42`, `GameBuilder.cs:312`). `:119` SoundMeta `rate` / `volume` are written but nothing reads
  them.
- `ARCHITECTURE.md:230` "loads the core once per process" holds for the editor's `EmulatorHost`; `LibretroCore` can be
  created again after `Dispose` (`tools/AgbStudio.Emu/LibretroCore.cs:61-68`, `:196`).

## Read and found current

- ARCHITECTURE.md: repository and blueprint tables, runtime layers and limits (64 actors x 6 behaviours, 128 sprites,
  32 matrices, xoshiro128**, 24.8 fixed), video modes and Mode 7, the audio paragraph including the four multiplied
  music volume levels and `SceneDef::musicVolume` from `6ab2838`, the commit-queue paragraph (`97c7c3c`, `818c76e`),
  build steps 1, 3-5, report.json schemas, hardware-limit numbers and `render_limits`, RAM budget rows, the debug
  channel, the text pipeline with OTF / CFF, tier 1 and tier 3 scripting, Play Mode and hot reload, packaging outputs.
- BUILD.md: requirements, the `ci.cmd` step list (apart from commit-refs), artifacts contents and keep-10,
  `AGB_BUILD_JOBS`, the CLI table (apart from the two rows above), pipeline steps, cache, incremental build,
  `.gitmessage`, PR template, emoji check.
- FILE-FORMATS.md: the schema list and generator test, agbproj rows other than `input` and `ui`, SceneFile, the link
  paragraph at lines 67-72, MapFile layer fields, prefabs, sprite / tileset / music meta, palettes.json, OTF / TTC /
  WOFF / CFF2 rules, strings, sfx, graph and dialogue core fields, report.json and report-history.json, the per-user
  file list apart from the crash folder.
- DECISIONS.md: D-01, D-02, D-04 to D-12, D-14 to D-19, D-21 to D-26 answers match the code (D-24 debug channel, D-25
  ceil(n/15) palettes, D-26 targets and mixer figures).
- TOOLCHAIN.md: detection order, devkitPro table uses, the saved `arm-none-eabi-gcc --version` output, mGBA core hash.
- THIRD_PARTY.md: every other row matches the csproj references, NuGet versions, `third_party/` licence files, About ›
  Licenses and the `THIRD_PARTY.txt` paragraph (lines 34-36).

## What closes G-DOCS.04

Fix the 60 rows above (or reword them), then repeat this REV at the next phase exit. The FILE-FORMATS rows
about editor-only fields go with checklist P1.04 (the schemas lack the same fields).
