# AGB Studio · File formats

Every project file is UTF-8 JSON with camelCase property names (D-03), written by `GameProject.Save` with indentation
so it diffs well in version control. Each top-level document carries `"format": 1`; a file with a higher format was
written by a newer AGB Studio and is refused with a message instead of being misread. The C# classes in
`tools/AgbStudio.Core/Project` are the reference; this page lists what lives where.

## Project folder

```
MyGame/
  MyGame.agbproj          project settings (ProjectFile)
  .gitignore              "build/"
  scenes/*.scene          scenes (SceneFile)
  maps/*.map              tile maps and layers (MapFile)
  assets/
    sprites/*.png         sprite sheets + <name>.png.meta.json (SpriteMeta)
    tilesets/*.png        16 x 16 editor tiles + .meta.json (TilesetMeta)
    images/*.png          full-screen backgrounds for image layers
    palettes/*.pal|*.gpl  imported palettes (JASC / GIMP)
    audio/music/*.mod|*.xm  music + .meta.json (MusicMeta)
    audio/sfx/*.wav       sampled sound effects + .meta.json (SoundMeta)
  audio/sfx/*.sfx         SFX Designer effects (SfxFile)
  fonts/*.font            game fonts (FontFile) next to their .ttf / .bdf / image source
  scripts/**/*.cs         C# behaviours
  scripts/**/*.graph      Logic Graphs (the matching .cs is generated from them)
  data/*.db               Database tables (TableFile)
  text/strings.<lang>.json  Localization tables (StringTable)
  text/strings.meta.json  per-key group, speaker, notes, box width and lines (StringMeta)
  dialogue/*.dlg          Dialogue Editor trees
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
| `audio` | Maxmod channels and mixing rate |
| `ui` | Menu / UI kit colours and layout of the title, pause and options screens |

## `scenes/<Name>.scene` (SceneFile)

`name`, `width` and `height` in 16 px tiles, `videoMode` (0-2), `map` (a map name), `camera` (follow actor, dead zone,
clamp to map), `music`, `events` (OnLoad / OnEnter / OnExit scripts), `actors`, `links` (scene changes: on touch,
door, respawn, with a transition), `alphaBlending`, `mosaic`, `backdrop` (#RRGGBB), an optional title `menu`, and the
World Map card position (`mapX`, `mapY`).

A link (`LinkData`) has `from` (an actor of the scene, or empty for "Condition only"), `kind`, `to`, the spawn point
(`spawnAt`, `spawnX`, `spawnY`), `transition`, `duration` in seconds and `onlyIf` (a C# condition). At run time:
**OnTouch** fires when the player overlaps the From actor; **Door** when the player stands in it and presses Up;
**Respawn** when the player overlaps it (a pit, spikes) or is lost in this scene (out of hearts, destroyed, or fallen
below the map). A link without a From actor fires once its `onlyIf` holds (a Respawn link without one: when the player
is lost). `onlyIf` is checked every time the link would fire.

An actor (`ActorData`) has a `name`, `kind` (`Empty`, `Player`, `Enemy`, `Pickup`, `TriggerZone`, `CameraZone`,
`TextLabel`), position, sprite and animation, tag, priority, hidden flag, Hierarchy `group`, collider, a label `text`
key for Text Labels, and `components`: each a behaviour `type` with its Inspector `fields` as strings.

## `maps/<Name>.map` (MapFile)

`tileset`, `width`, `height` in 16 px tiles, `collisionLayer` (-1 = the first tile layer), and `layers`. A layer
(`MapLayerData`) has a `name`, hardware `bg` (0-3), `type` (`Tile`, `Image`, `Affine`, `CollisionOnly`), `priority`,
`parallaxX` / `parallaxY` (1 = moves with the camera), `wrap`, `cells` (one tile index per cell, row-major) or, for
image layers, the `image` name. Affine layers are converted at build time to 8bpp tiles with flips and palettes baked
in. A map is the layout of a scene (blueprint "Layout"): several scenes can show the same map, and the ROM holds one
copy of it (`build/gen/maps/map_<Name>.cpp`).

## `prefabs/<Name>.prefab` (ActorData)

Written by Make Prefab: the actor as it was, with its components and their Inspector values. Add to Scene › Prefab…
places a copy of it in a scene (the copy remembers the prefab in its `prefab` field). The build turns every prefab into
a const actor template in ROM (`build/gen/prefabs.cpp`), and `Scene.Spawn("Name", x, y)` copies it in any scene when
the scene has no actor of that name.

## Asset metadata (`<file>.meta.json`)

- `SpriteMeta`: frame size, origin, collider, `animations` (name, frames, ticks per frame or per-frame ticks, loop,
  ping-pong), palette options.
- `TilesetMeta`: per tile `name`, `collision` (`None`, `Solid`, `OneWay`, `Ladder`, `Hazard`, `Water`, `SlopeUp`,
  `SlopeDown`), `tag`, animation frames; auto-tile sets; whether to compress.
- `MusicMeta` / `SoundMeta`: volume, loop start, fade, sample rate options.

## Other files

- `fonts/<name>.font` (`FontFile`): source kind (`Ttf`, `Bdf`, `Image`) and file, pixel size, variable or fixed width,
  character sets, extra characters, line height, baseline, spacing, bits per pixel, drop shadow, Thai mark stacking
  and lower-vowel placement, glyph overrides.
- `data/<Table>.db` (`TableFile`): key field, keep in ROM, `fields` (name, type `int` / `fixed` / `bool` / `text` /
  `enum` / `sprite` / `sound` / `script` / `reference`, enum values, default, localized) and `rows`.
- `text/strings.<lang>.json` (`StringTable`): `language` and a sorted `strings` map from key to text.
- `audio/sfx/<name>.sfx` (`SfxFile`): DMG channel, bake to WAV, volume and envelope, duty, start note, sweep, length,
  wave RAM samples, noise settings.

## Build output (`build/`)

| File | Written by | Holds |
|---|---|---|
| `<rom>.gba`, `obj/*.elf`, `obj/*.map` | GameBuilder | the ROM, the linked ELF and its map file |
| `gen/` | GameBuilder | generated C++ (assets, scenes, strings, scripts) |
| `cache/` | AssetCache | hashes of converted assets for incremental builds |
| `report.json` | BuildReport | ROM size by category, largest assets, IWRAM / EWRAM use, per-scene budgets, warnings |
| `report-history.json` | BuildReport | the last 20 builds, failed ones included (first error) |

## Editor files (per user)

`%APPDATA%\AGBStudio\preferences.json` (Preferences), `recent-projects.json` (recent projects), `tutorials.json`
(tutorial progress), `script-editor.json` (Script Editor and external editor settings) and `editor.json` next to the
program (website, bug and update URLs, D-13).
