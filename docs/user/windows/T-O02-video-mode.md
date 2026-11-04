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
  - **Mode 4**: 2 bitmap pages of 240×160 pixels with a palette of 255 colours.
  - **Mode 5**: 2 bitmap pages of 160×128 pixels in 16-bit colour, in the top-left corner of the screen; the backdrop
    colour shows around it.
- **Picture** (Modes 3, 4 and 5): the image from `assets/images` the scene shows; 240×160 in Modes 3 and 4, 160×128 in
  Mode 5. It says when the image is another size. In Mode 4 an image with more than 255 colours is reduced when the game
  is built, and the build warns about it; transparent pixels show the backdrop there, and are black in Modes 3 and 5. The
  Scene Editor and the World Map show the picture. A scene in these modes shows no map layers, text or menus: scripts draw
  on the picture with `Bitmap`, and the map's collision still works.
- **Alpha blending (fog, shadows)**: turns see-through blending on or off for this scene. In the game, the map layers
  in front of the collision layer (fog, clouds, haze) are drawn half see-through over everything behind them, and
  semi-transparent sprites (shadows) blend over the layers. The collision layer and the layers behind it stay solid, and
  text (HUD labels, dialogue, menus) is never blended. A scene without a layer in front of its collision layer looks the
  same with the box ticked.
- **Mosaic effect**: turns the mosaic (big-pixel) effect on or off for this scene. In the game, the scene's layers and
  sprites (not the text) break into big pixels while the scene fades in and out, up to 9-pixel blocks at black.
- The chip in the toolbar shows the mode of the open scene.

## Related

- [Scene Editor](T-P01-scene-editor.md)
- [New Scene](T-D02-new-scene.md)
- [Tilemap Editor](T-P03-tilemap-editor.md)
- [GBA hardware guide: video modes](../gba-hardware-guide.md#video-modes)
- [GBA hardware guide: effects](../gba-hardware-guide.md#effects)
