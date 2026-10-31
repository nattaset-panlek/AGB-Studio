# Import Asset

Figma: T-D04 · Dialog

> ลากไฟล์มาวาง ระบบตรวจชนิดให้ (PNG, Aseprite, Tiled .tmx, WAV, MOD/XM, TTF) แล้วแปลงเป็นรูปแบบของ GBA ปรับตัวเลือกได้ทีละไฟล์

Drag files in; AGB Studio works out what each one is (PNG, Aseprite, Tiled .tmx, WAV, MOD/XM, TTF) and converts it to
the GBA format, with options you can set for each file.

## Opens from

- File › Import Asset… (Ctrl+I)
- Assets › Import…
- Sprite Editor › Drop zone
- Palette Manager › Import .pal / .gpl
- Audio › Music +
- Also: the Import buttons and dropping files on the empty project screen

## What is on it

- **Drop zone**: drop files here, or click it to pick files (several at once). The text reads "Drop files here or
  browse..." and then "Drop more files here or browse...".
- **File list**: each file with what it was detected as (Sprite sheet, Tileset, Background, Tilemap (Tiled), Sound
  effect, Music, Palette, Font, C# script), its size and a state chip: **ready**, **needs fix** or **error**. Click a
  row to see its options. A file already in the list is not added twice.
- **How files are detected**: PNG images become a sprite sheet, a tileset (when the name contains "tile" or the image
  is large and tile-shaped) or a background (240 x 160 or bigger). `.tmx` is a Tiled map, `.wav` a sound effect,
  `.mod` / `.xm` music, `.pal` / `.gpl` a palette, `.ttf` / `.otf` / `.bdf` a font and `.cs` a script.
- **Options of the selected file**: the file name and type, a preview (up to four frames of a sprite sheet, the
  image, or an icon), then:
  - Sprite sheets: **Frame size** (GBA sprite sizes that divide the sheet), **Colours** (reduce to 16, or split into
    several palettes when there are too many colours), **Palette** (its own, or share with another sprite), **Frames
    found**, **Trim empty frames** and **Create animation** (named from the file, for example "run" for
    `hero_run.png`).
  - Other files: what they become, for example "11 kHz mono 8-bit" for sound effects or "XM → Maxmod bank" for
    music, and the ready message or the problem.
- **Import N files**: converts the files into the project folders and shows "Imported N files". If any file still has
  a problem, this dialog closes and Import Asset · Errors opens instead. **Cancel** closes without importing.

Differs from Figma: the caption lists Aseprite, but `.aseprite` files are not detected; they show the error "AGB Studio
can't import .aseprite files".

Differs from Figma: Sprite Editor › Drop zone imports PNG files straight into the Sprite Editor and does not open this
dialog.

Differs from Figma: Palette Manager › Import .pal / .gpl loads the colours into the selected palette with a file
picker and does not open this dialog.

## Related

- [Import Asset · Errors](T-D24-import-asset-errors.md)
- [Sprite Editor](T-P08-sprite-editor.md)
- [Tilemap Editor](T-P03-tilemap-editor.md)
- [Palette Manager](T-P10-palette-manager.md)
- [Audio](T-P15-audio.md)
- [Font Manager](T-P09-font-manager.md)
- [Scene Editor · Empty project](T-P22-scene-editor-empty-project.md)
- [GBA hardware guide: Sprites](../gba-hardware-guide.md#sprites)
- [GBA hardware guide: Palettes](../gba-hardware-guide.md#palettes)
- [GBA hardware guide: Sound](../gba-hardware-guide.md#sound)
