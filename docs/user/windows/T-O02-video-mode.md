# Video Mode

Figma: T-O02 · Popover

> เลือกโหมดภาพของ GBA สำหรับฉากนี้ แต่ละโหมดมีจำนวนเลเยอร์และความสามารถต่างกัน

Choose the GBA video mode for this scene. Each mode has a different number of layers and different abilities.

## Opens from

- The video mode chip "GBA · Mode 0 · 240×160" in the toolbar (#12)

## What is on it

- **Title bar**: "Video Mode", with a close button.
- **Mode cards**: one card per mode. The mode the open scene uses has a tick and is highlighted. Click a card to switch
  the scene to that mode. The change can be undone with Undo.
  - **Mode 0**: 4 tile layers that scroll. Best for platformers and RPGs.
  - **Mode 1**: 2 tile layers and 1 rotate / scale layer.
  - **Mode 2**: 2 rotate / scale layers, for Mode-7 style racing.
  - **Mode 3**: 1 bitmap of 240×160 pixels in 16-bit colour.
- **Alpha blending (fog, shadows)**: turns see-through blending on or off for this scene.
- **Mosaic effect**: turns the mosaic (big-pixel) effect on or off for this scene.
- The chip in the toolbar shows the mode of the open scene.

## Related

- [Scene Editor](T-P01-scene-editor.md)
- [New Scene](T-D02-new-scene.md)
- [Tilemap Editor](T-P03-tilemap-editor.md)
- [GBA hardware guide: video modes](../gba-hardware-guide.md#video-modes)
- [GBA hardware guide: effects](../gba-hardware-guide.md#effects)
