# AGB Studio · File formats

Every project file is UTF-8 JSON with camelCase property names (D-03), written by `GameProject.Save` with indentation
so it diffs well in version control. Each top-level document carries `"format": 1`; a file with a higher format was
written by a newer AGB Studio and is refused with a message instead of being misread. The C# classes in
`tools/AgbStudio.Core/Project` are the reference (and `tools/AgbStudio.Scripting` for `.graph` and `.dlg`); this page
lists what lives where. There are no separate JSON schema files.

## Project folder

```
MyGame/
  MyGame.agbproj          project settings (ProjectFile)
  .gitignore              "build/"
  scenes/*.scene          scenes (SceneFile)
  maps/*.map              tile maps and layers (MapFile)
  prefabs/*.prefab        actors saved with Make Prefab (ActorData)
  assets/
    sprites/*.png         sprite sheets + <name>.png.meta.json (SpriteMeta)
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
  build/                  everything the build writes (never edited by hand)
```

## `<name>.agbproj` (ProjectFile)

| Section | Holds |
|---|---|
| `name`, `tags`, `lastScene` | project name, tag list (Project Settings · Tags), the scene opened last |
| `general` | game title, ROM title (12 ASCII), game code (4), maker code (2), version, start scene, default font and language, boot splash and its length, skip splash in Debug |
| `input`, `allowRemap` | the ten buttons with an action name and keyboard key each; whether the Options screen remaps them |
| `save` | save type (`None`, `Sram32K`, `Flash64K`, `Flash128K`, `Eeprom8K`), slots, autosave, checksum, save fields (name and type) |
| `build` | configuration (`Debug` / `Release`), optimise flags, compress graphics (LZ77), output folder, strip debug log, ROM size warning (MB), keep asserts, run after build |
| `scripting` | analyzer severities (`GAE001`..`GAE006`), shop currency field and other script options |
| `localization` | source language, languages (code, name, font, fallback), Thai word wrap, show missing keys, fail on missing |
| `audio` | Maxmod channels and mixing rate |
| `ui` | Menu / UI kit colours and layout of the title, pause and options screens |

## `scenes/<Name>.scene` (SceneFile)

`name`, `width` and `height` in 16 px tiles, `videoMode` (0-3; the runtime draws tile layers in modes 0-2, mode 3
only sets the display mode), `map` (a map name), `camera` (`follow` actor, `deadZoneW` / `deadZoneH`, `clampToMap`),
`music` (`track`, `volume`, `fadeOnSceneChange`), `events` (`onLoad` / `onEnter` / `onExit` C# statements), `actors`,
`links`, `alphaBlending`, `mosaic`, `backdrop` (#RRGGBB), an optional title `menu`, the World Map card position
(`mapX`, `mapY`) and the `template` the scene started from.

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
and palettes baked in (at most 256 tiles).

## `prefabs/<Name>.prefab`

One `ActorData` (as in a scene) written by Actor Context Menu › Make Prefab, with `prefab` cleared; the actor in the
scene gets `prefab` set to the name. Add to Scene › Prefab… places a copy. The build does not read prefab files:
placed actors are stored in full in the scene.

## Asset metadata (`<file>.meta.json`)

- `SpriteMeta`: `frameWidth` / `frameHeight`, `oamSize` (for example "Square 16x16"; empty = from the frame size),
  `originX` / `originY`, `collider`, `palette` (a shared palette name, empty = own), `reduceColors`,
  `trimEmptyFrames`, `compress`, `animations` (each `name`, `frames`, `ticks` per frame or per-frame `frameTicks`,
  `loop`, `pingPong`).
- `TilesetMeta`: `tiles` by metatile index (`name`, `collision` = `None`, `Solid`, `OneWay`, `Ladder`, `Hazard`,
  `Water`, `SlopeUp`, `SlopeDown`, `tag`, `animation` frames and `animationTicks`), `autoTiles` (rule sets: each rule a
  3x3 `mask` and the `tile` it picks), `compress` (LZ77, default on).
- `MusicMeta`: `volume`, `loopStart` (pattern order), `fadeOnSceneChange`.
- `SoundMeta`: `rate` (11025: resampled to 11 kHz mono 8-bit), `volume`.

## Other files

- `fonts/<name>.font` (`FontFile`): source kind (`Ttf`, `Bdf`, `Image`) and file, pixel size, variable or fixed width,
  character sets, extra characters, line height, baseline, spacing, bits per pixel, drop shadow, Thai mark stacking
  and lower-vowel placement, glyph overrides (edited pixels by code point). OpenType CFF fonts are refused by the
  build.
- `data/<Table>.db` (`TableFile`): key field, keep in ROM, `fields` (name, type `int` / `fixed` / `bool` / `text` /
  `enum` / `sprite` / `sound` / `script` / `reference`, enum values, default, localized) and `rows`.
- `text/strings.<lang>.json` (`StringTable`): `language` and a sorted `strings` map from key to text.
- `audio/sfx/<name>.sfx` (`SfxFile`): DMG `channel` (`Square1`, `Square2`, `Wave`, `Noise`), `bake` (to a `.wav`
  instead of playing on the DMG channel), `volume` (0-15), `duty`, `envelopeStep` / `envelopeUp`, `startNote`,
  `sweepTime` / `sweepShift` / `sweepUp` (Square 1), `length` in seconds, `wave` (32 samples, 0-15), `noiseDivider` /
  `noiseShort`. The build turns every effect that is not baked into DMG register values in the ROM.
- `scripts/<Name>.graph` (`LogicGraph`): `class` (the generated C# class), `nodes` (each `id`, `type` from the node
  catalog, `x`, `y`, `props`) and `links` (`from` / `to` as "node.pin", from an output pin to an input pin). The
  generated `<Name>.cs` next to it carries a header and is read-only in the editor (D-11).
- `dialogue/<name>.dlg` (`DialogueFile`): `name`, `textSpeed`, `blip`, `speakers` (`name`, `portrait`, `blip`,
  `textSpeed`) and `conversations` (`id`, `lines`). A line has an optional `id`, `speaker`, a string `key` or inline
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
| `report.json` | BuildReport | ROM size by category, largest assets, IWRAM / EWRAM use, per-scene budgets, warnings |
| `report-history.json` | BuildReport | the last 20 builds, failed ones included (first error) |

## Editor files (per user)

In `%APPDATA%\AGBStudio\`: `preferences.json` (Preferences, including the Script Editor and external editor
settings), `recent-projects.json` (recent projects), `tutorials.json` (tutorial progress) and
`launcher-projects.json` (Launcher project thumbnails). Next to the program: `editor.json` (website, bug, update,
community and release notes URLs, D-13) and, in a packaged copy, `release-notes.md` (the Launcher's What's new).
