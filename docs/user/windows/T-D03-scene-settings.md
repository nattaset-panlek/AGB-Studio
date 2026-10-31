# Scene Settings

Figma: T-D03 · Dialog

> ตั้งค่าของฉาก: ขนาด กล้อง ฉากหลัง เพลง และ event ตอนเข้า-ออกฉาก

Settings of one scene: its size, camera, backgrounds, music, and the events that run when the player enters or leaves.

## Opens from

- The scene size chip at the bottom left of the scene view, for example "Forest_01 · 30×20 tiles" (#18)
- Scene › Scene Settings…
- Also: Scene Settings… in the Inspector when the scene itself is selected

## What is on it

The settings are in four sections. Click a section heading to fold or unfold it.

- **General**
  - **Name**: renames the scene. Links from other scenes and the start scene follow the new name. If the name is not
    valid or is already taken, the old name is kept.
  - **Size**: width (X) and height (Y) in tiles, up to 256. The map keeps its tiles from the top-left corner.
  - **Start scene**: makes this the scene the game starts with.
- **Camera**
  - **Follow**: None, or the actor the camera follows.
  - **Dead zone**: the box in the middle of the screen (in pixels, up to 240×160) where the target can move without
    the camera moving.
  - **Clamp to map**: stops the camera at the edges of the map.
- **Backgrounds** (parallax): one slider per background layer, for example "BG0 Sky". 0.0 does not scroll, 1.0 scrolls
  with the camera. Only shown when the scene has a map with background layers.
- **Music & events**
  - **Music**: None, or a music file of the project to play in this scene.
  - **OnEnter** and **OnExit**: what runs when the scene starts and when it ends, for example `FadeIn(0.5s)`.
- **Cancel**: closes without changes.
- **Save**: applies everything as one change, which Undo can take back.

## Related

- [Scene Editor](T-P01-scene-editor.md)
- [New Scene](T-D02-new-scene.md)
- [Project Settings · General](T-S01-project-settings-general.md)
- [Tilemap Editor](T-P03-tilemap-editor.md)
- [Audio](T-P15-audio.md)
- [World Map](T-P13-world-map.md)
- [GBA hardware guide: tiles and maps](../gba-hardware-guide.md#tiles-and-maps)
