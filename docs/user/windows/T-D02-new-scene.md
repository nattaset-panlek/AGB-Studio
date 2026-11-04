# New Scene

Figma: T-D02 · Dialog

> สร้างฉากใหม่จากแบบสำเร็จหรือฉากว่าง

Create a new scene from a ready-made template or as an empty scene.

## Opens from

- Scene › New Scene… (Ctrl+Shift+N)
- Scene Switcher › New Scene…
- World Map › New Scene

## What is on it

- **Title bar**: "New Scene", with a close button.
- **Scene name**: starts with the next free name in the series, for example Forest_02 after Forest_01. The box has the
  keyboard when the dialog opens. Use letters, digits and _, starting with a letter. The name must not be taken.
- **Start from**: Platformer level, Top-down room or Empty scene.
- **Width (tiles)** and **Height (tiles)**: start at the size of the open scene. Width is 15 to 256 tiles, height is
  10 to 256 tiles (one GBA screen is 30×20 tiles).
- **Video mode**: Mode 0 to Mode 5. Starts at the mode of the open scene.
- **Copy backgrounds from** (the open scene): copies its image background layers. It is only available when the open
  scene has image layers.
- **Add a camera that follows the player**: on by default.
- **Hint line**: when a field is not valid, the footer says why (for example "A scene named Forest_02 already
  exists.") and Create Scene stays greyed out.
- **Cancel**: closes without creating anything.
- **Create Scene**: writes the new scene and its map, then opens it. Undo removes it again.

## Related

- [Scene Editor](T-P01-scene-editor.md)
- [Scene Switcher](T-O01-scene-switcher.md)
- [Scene Settings](T-D03-scene-settings.md)
- [Video Mode](T-O02-video-mode.md)
- [World Map](T-P13-world-map.md)
- [GBA hardware guide: video modes](../gba-hardware-guide.md#video-modes)
- [GBA hardware guide: tiles and maps](../gba-hardware-guide.md#tiles-and-maps)
