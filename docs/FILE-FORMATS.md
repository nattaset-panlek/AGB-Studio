# AGB Studio · File formats

Every project file is UTF-8 JSON with camelCase property names (D-03), written by `GameProject.Save` with indentation
so it diffs well in version control. Each top-level document carries `"format": 1`; a file with a higher format was
written by a newer AGB Studio and is refused with a message instead of being misread. The C# classes in
`tools/AgbStudio.Core/Project` are the reference (and `tools/AgbStudio.Scripting` for `.graph` and `.dlg`); this page
lists what lives where.

JSON schemas (draft 2020-12) for every format are in `docs/schemas/`: `agbproj`, `scene`, `map`, `sprite-meta`,
`tileset-meta`, `music-meta`, `sound-meta`, `sfx`, `font`, `db`, `strings`, `strings-meta`, `prefab`, `dlg`,
`palettes` and `graph`. All but `graph` are generated from the C# classes with the settings the files are written with;
`SchemaTests.SchemasMatchTheFileModel` fails when a class changes and its schema was not regenerated (run the tests with
`AGB_UPDATE_SCHEMAS=1` to rewrite them).

## Project folder

```
MyGame/
  MyGame.agbproj          project settings (ProjectFile)
  .gitignore              "build/"
  scenes/*.scene          scenes (SceneFile)
  maps/*.map              tile maps and layers (MapFile)
  prefabs/*.prefab        actors saved with Make Prefab (ActorData)
  assets/
    sprites/*.png         sprite sheets + <name>.png.meta.json (SpriteMeta); *.aseprite files: frames, layers, tags
    tilesets/*.png        16 x 16 editor tiles + .meta.json (TilesetMeta)
    images/*.png          full-screen backgrounds for image layers
    palettes/*.pal|*.gpl  imported palettes (JASC / GIMP)
    palettes/palettes.json  Palette Manager banks and palette effects (PaletteLibraryFile, editor only)
    audio/music/*.mod|*.xm  music + .meta.json (MusicMeta)
    audio/sfx/*.wav       sampled sound effects (and baked SFX Designer effects) + .meta.json (SoundMeta)
  audio/sfx/*.sfx         SFX Designer effects (SfxFile)
  fonts/*.font            game fonts (FontFile) next to their .ttf / .bdf / image source
  scripts/**/*.cs         C# behaviours
  scripts/**/*.graph      Logic Graphs (the matching .cs is generated from them)
  data/*.db               Database tables (TableFile)
  text/strings.<lang>.json  Localization tables (StringTable)
  text/strings.meta.json  per-key group, speaker, notes, box width and lines (StringMeta)
  dialogue/*.dlg          Dialogue Editor files (DialogueFile)
  prefabs/*.prefab        Make Prefab: one actor (ActorData) each
  build/                  everything the build writes (never edited by hand)
```

## `<name>.agbproj` (ProjectFile)

| Section | Holds |
|---|---|
| `name`, `tags`, `lastScene` | project name, tag list (Project Settings · Tags), the scene opened last |
| `general` | game title, ROM title (12 ASCII), game code (4), maker code (2), version, start scene, default font and language, boot splash and its length, skip splash in Debug |
| `input`, `allowRemap` | the ten buttons with an action name and keyboard key each; whether the Options screen remaps them |
| `save` | save type (`None`, `Sram32K`, `Flash64K`, `Flash128K`, `Eeprom8K`), slots, autosave, checksum, save fields (name and type), `flagIds` (written by the build: the saved flag id of each Dialogue Editor flag; a flag keeps its id and a new flag never reuses one, so adding a flag does not change what old save data means) |
| `build` | configuration (`Debug` / `Release`), optimise flags, compress graphics (LZ77), output folder, strip debug log, ROM size warning (MB), keep asserts, run after build |
| `scripting` | analyzer severities (`GAE001`..`GAE006`), shop currency field and other script options |
| `localization` | source language, languages (code, name, font, fallback), Thai word wrap, show missing keys, fail on missing |
| `audio` | Maxmod channels (`channels`, 1 to 16) and mixing rate (`mixKHz`: 8, 10, 13, 16, 18, 21, 27 or 31; the runtime mixes at it) |
| `ui` | Menu / UI kit colours and layout of the title, pause and options screens |

## `scenes/<Name>.scene` (SceneFile)

`name`, `width` and `height` in 16 px tiles, `videoMode` (0-3; the runtime draws tile layers in modes 0-2, mode 3
only sets the display mode), `map` (a map name), `camera` (`follow` actor, `deadZoneW` / `deadZoneH`, `clampToMap`),
`music` (`track`, `volume`: the game's music volume while this scene's own track plays, built as
`SceneDef::musicVolume` 0..256; `fadeOnSceneChange`), `events` (`onLoad` / `onEnter` / `onExit` C# statements), `actors`,
`links`, `alphaBlending`, `mosaic`, `backdrop` (#RRGGBB), an optional title `menu`, the World Map card position
(`mapX`, `mapY`) and the `template` the scene started from.

A link (`LinkData`) has `from` (an actor of the scene, or empty for "Condition only"), `kind`, `to`, the spawn point
(`spawnAt`, `spawnX`, `spawnY`), `transition`, `duration` in seconds and `onlyIf` (a C# condition). At run time:
**OnTouch** fires when the player overlaps the From actor; **Door** when the player stands in it and presses Up;
**Respawn** when the player overlaps it (a pit, spikes) or is lost in this scene (out of hearts, destroyed, or fallen
below the map). A link without a From actor fires once its `onlyIf` holds (a Respawn link without one: when the player
is lost). `onlyIf` is checked every time the link would fire.

An actor (`ActorData`) has a `name`, `kind` (`Empty`, `Player`, `Enemy`, `Pickup`, `TriggerZone`, `CameraZone`,
`TextLabel`), position (`x`, `y`), `sprite` and `animation`, `tag`, OBJ `priority`, `hidden`, the `prefab` it came
from, Hierarchy `group`, `collider` (null = from the sprite), `zoneW` / `zoneH` for trigger and camera zones, a label
`text` key for Text Labels, and `components`: each a behaviour `type` with its Inspector `fields` as strings.

A link (`LinkData`, Link Settings) has `from` (an actor in this scene), `kind` (`OnTouch`, `Door`, `Respawn`), `to`
(a scene), `spawnAt` (a spawn point name or "x,y") with `spawnX` / `spawnY`, `transition` (`Cut`, `Fade`, `Slide`),
`duration` in seconds and `onlyIf` (a C# condition, empty = always). The runtime currently treats every kind like
`OnTouch`.

A scene `menu` (`SceneMenu`, title screens) has the panel `x`, `y`, `width`, `height`, `spacing` and `items`, each
with a `text` (a string key or ASCII text), a `command` (`LoadScene`, `Continue`, `Options`, `QuitToTitle`) and the
target `scene` for `LoadScene`.

## `maps/<Name>.map` (MapFile)

`name`, `tileset`, `width`, `height` in 16 px tiles, `collisionLayer` (-1 = the first tile layer), and `layers`. A
layer (`MapLayerData`) has a `name`, hardware `bg` (0-3), `type` (`Tile`, `Image`, `Affine`, `CollisionOnly`),
`priority`, `parallaxX` / `parallaxY` (1 = moves with the camera), `wrap`, `cells` (one metatile index per cell,
row-major) or, for image layers, the `image` name. Affine layers are converted at build time to 8bpp tiles with flips
and palettes baked in (at most 256 tiles). A map is the layout of a scene (blueprint "Layout"): several scenes can
show the same map, and the ROM holds one copy of it (`build/gen/maps/map_<Name>.cpp`). The Tilemap Editor also writes
`bgSize` ("32x32", "64x32", "32x64" or "64x64"; left out = 64x32) and `bpp` (4 or 8; left out = 4).

## `prefabs/<Name>.prefab` (ActorData)

Written by Actor Context Menu › Make Prefab: the actor as it was, with its components and their Inspector values
(`prefab` cleared); the actor in the scene gets `prefab` set to the name. Add to Scene › Prefab… places a copy. The
build turns every prefab into a const actor template in ROM (`build/gen/prefabs.cpp`), and `Scene.Spawn("Name", x, y)`
copies it in any scene when the scene has no actor of that name.

## Asset metadata (`<file>.meta.json`)

- `SpriteMeta`: `frameWidth` / `frameHeight`, `oamSize` (for example "Square 16x16"; empty = from the frame size),
  `originX` / `originY`, `collider`, `palette` (a shared palette name, empty = own), `reduceColors`,
  `trimEmptyFrames`, `compress`, `animations` (each `name`, `frames`, `ticks` per frame or per-frame `frameTicks`,
  `loop`, `pingPong`).
- `TilesetMeta`: `tiles` by metatile index (`name`, `collision` = `None`, `Solid`, `OneWay`, `Ladder`, `Hazard`,
  `Water`, `SlopeUp`, `SlopeDown`, `tag`, `animation` frames and `animationTicks`), `autoTiles` (rule sets: each rule a
  3x3 `mask` and the `tile` it picks), `compress` (LZ77, default on).
- `MusicMeta`: `volume`, `loopStart` (pattern order), `fadeOnSceneChange`. The build reads it for every MOD / XM (the
  defaults when the file is missing: 0.8, 0, true): `loopStart`, clamped to the module's last order, becomes the
  module's restart order in the soundbank, so maxmod continues there when a looping song ends; `volume` (0..256) and
  `fadeOnSceneChange` go into `agb_soundbank_music` (`agb::audio::MusicInfo` by module id). At runtime the track volume
  multiplies the music volume, and a scene change fades the music only when the scene's own "Fade on scene change" and
  the track's both ask for it.
- `SoundMeta`: `rate` (11025: resampled to 11 kHz mono 8-bit), `volume`.

## Other files

- `assets/palettes/palettes.json` (`PaletteLibraryFile`, Palette Manager; read and written by the editor only, the
  build takes the colours from the art and the sprite meta): `banks` (each `kind` `Bg` / `Obj`, `slot` 0-15, `name`,
  `colors` as "#RRGGBB" with "none" for the locked transparent index, at most 16, `assets` that use the bank as
  project-relative paths, `lockTransparent`, `mergeNear`, `sortByHue`) and `effects` (`fade`, `cycle`, `flash`: each
  `kind`, `name`, `bankKind`, `bank` with -1 for every bank, colour range `first` / `last`, `frames`, cycle
  `direction` `Left` / `Right` / `PingPong`, 15-bit `color`, `enabled`).
- `fonts/<name>.font` (`FontFile`): source kind (`Ttf`, `Bdf`, `Image`) and file, pixel size, variable or fixed width,
  character sets, extra characters, line height, baseline, spacing, bits per pixel, drop shadow, Thai mark stacking
  and lower-vowel placement, glyph overrides (edited pixels by code point). `Ttf` takes a `.ttf` or an `.otf` file:
  TrueType outlines or CFF (PostScript) outlines, name-keyed or CID-keyed. Font collections (`.ttc`), web fonts
  (`.woff`) and variable fonts with CFF2 outlines are refused with a message. `drawTo` is the BG (0-3) of the text
  layer chosen in Font Manager · Draw to (left out = BG 0).
- `data/<Table>.db` (`TableFile`): key field, keep in ROM, `fields` (name, type `int` / `fixed` / `bool` / `text` /
  `enum` / `sprite` / `sound` / `script` / `reference`, enum values, default, localized) and `rows`.
- `text/strings.<lang>.json` (`StringTable`): `language` and a sorted `strings` map from key to text.
- `audio/sfx/<name>.sfx` (`SfxFile`): DMG `channel` (`Square1`, `Square2`, `Wave`, `Noise`), `bake` (to a `.wav`
  instead of playing on the DMG channel), `volume` (0-15), `duty`, `envelopeStep` / `envelopeUp`, `startNote`,
  `sweepTime` / `sweepShift` / `sweepUp` (Square 1), `length` in seconds, `wave` (32 samples, 0-15), `noiseDivider` /
  `noiseShort`. The build turns every effect that is not baked into DMG register values in the ROM.
- `scripts/<Name>.graph` (`LogicGraph`): `class` (the generated C# class), `nodes` (each `id`, `type` from the node
  catalog, `x`, `y`, `props`) and `links` (`from` / `to` as "node.pin", from an output pin to an input pin). The
  generated `<Name>.cs` next to it carries a header and is read-only in the editor (D-11). The Logic Graph editor
  adds `comments` (`title`, `x`, `y`, `w`, `h`), `collapsed` (node ids) and `disabled` (nodes and links switched off,
  a graph of the same shape that is not compiled).
- `dialogue/<name>.dlg` (`DialogueFile`): `name`, `textSpeed`, `blip`, `speakers` (`name`, `portrait`, `blip`,
  `textSpeed`, and from New Speaker `names` per language and the name tag `color`) and `conversations` (`id`, `lines`). A line has an optional `id`, `speaker`, a string `key` or inline
  `text` per language, `portrait`, `speed`, `blip`, `showIf`, `thenSet`, `next`, `choices` (each `key` or `text`,
  `showIf`, `thenSet`, `goto`, `scene`, `end`), or a `command` with its arguments (`scene`, `flag` / `value`,
  `table` / `item` / `count`, `sound` / `volume`, `frames` / `seconds`, `amplitude`).

## Build output (`build/`)

| File | Written by | Holds |
|---|---|---|
| `<rom>.gba`, `THIRD_PARTY.txt` | GameBuilder | the ROM and the libtonc and maxmod notices that travel with it |
| `obj/build.ninja`, `obj/*.elf`, `obj/*.elf.map`, `obj/obj/` | NativeBuild | the build graph, the linked ELF, its map file, objects |
| `gen/` | GameBuilder, scripting compiler | generated C++ (assets, scenes, strings, scripts, database), the soundbank, debugger maps |
| `cache/manifest.json` | AssetCache | hashes of converted assets for incremental builds |
| `report.json` | BuildReport | `format`, ROM size by category and per asset, IWRAM / EWRAM use (all from the linker map), `ramSplit` (IWRAM / EWRAM by blueprint 10 row, with each row's default budget), save slot bytes, per-scene VRAM and palette budgets (`scenes`), build time per step (`stepSeconds`), warnings. Each of `assets` (largest ROM first; `largestAssets` is the first ten) has `name`, `type`, ROM `bytes` and `share` (percent of the ROM), and what it takes when a scene loads it: `vram` (`BG`, `OBJ` or empty), `vramBytes` = `vramTileBytes` + `vramMapBytes`, `paletteBytes` (32 per 16-colour bank) and `ramBytes` (see below) |
| `report-history.json` | BuildReport | the last 20 builds, failed ones included (first error) |

What an asset takes when loaded (`GameBuilder` `AssetInfo`, the same numbers the scene budgets add up): a sprite one
frame of OBJ tiles for each actor that shows it and its own OBJ palette bank (none when it shares a palette); a tileset
or image all its tiles in BG VRAM and its palette banks (LZ77 tiles are decompressed by the BIOS straight into VRAM, so
no RAM); a map, for each tile or image layer, a 64 x 32 screen map (4 KB) in BG VRAM and its 4 KB shadow in the screen
arena (EWRAM), and for each rotate / scale layer a 16 KB character block for its 8bpp tiles, its map and its palette
banks; the boot splash its tiles, palette, a 2 KB screen map and its 2 KB shadow. Music, sounds, fonts, string tables
and Database tables are read from ROM: no VRAM and no RAM (the text layer's tiles are in the scene budget's
`vramTextBytes`; the Maxmod mixing buffer and other runtime memory are in `ramSplit`). A script that edits map tiles
(`Scene.SetTile`) copies that layer's cells into the screen arena once; that copy is not counted.

`docs/schemas/report.schema.json` and `report-history.schema.json` are generated from `BuildReport` like the project
format schemas; `BuildReportTests.ReportNumbersMatchTheLinkerMap` checks a real build's files against them.

## Editor files (per user)

In `%APPDATA%\AGBStudio\`: `preferences.json` (Preferences, including the Script Editor and external editor
settings), `recent-projects.json` (recent projects), `tutorials.json` (tutorial progress) and
`launcher-projects.json` (Launcher project thumbnails). Next to the program: `editor.json` (website, bug, update,
community and release notes URLs, D-13) and, in a packaged copy, `release-notes.md` (the Launcher's What's new).
