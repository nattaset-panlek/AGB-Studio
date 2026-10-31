# Tilemap Editor

Figma: T-P03 · Page

> ระบาย tile ลงฉากทีละเลเยอร์ (BG0–BG3) ตั้งค่าการชนที่ตัว tile ได้เลย auto-tile ต่อขอบให้อัตโนมัติ และเห็นการใช้ VRAM ของแต่ละเลเยอร์

Paint tiles into a scene one background layer at a time (BG0 to BG3), set collision on each tile, let auto-tile join
edges for you, and see how much video memory (VRAM) each layer uses.

## Opens from

- The map's document tab, for example `Forest_01.map` (#16)
- Scene › Background Layers…
- Create › Tileset / Tilemap (makes a new 30 x 20 map, `Map_01.map` or the next free number, on the project's first
  tileset, and opens it; a project with no tileset also gets a blank one, `tiles.png`)
- Also: the layer row in the Scene Editor Hierarchy and the Inspector's open-layer button

## What is on it

- **Tools**: Select, Brush, Rectangle, Fill, Eraser and Pick (pick a tile from the map). Left mouse button uses the
  tool.
- **Layer toggles BG0-BG3 and OBJ**: show or hide each background layer and the actors. A BG with no layer stays off.
- **Collision view**: shows the collision of every tile on top of the map.
- **Play, video mode chip, undo / redo**: Play runs the game; the chip ("GBA · Mode 0 · 240×160") opens Video Mode.
- **Tileset panel (left)**: the **Set** dropdown picks the tileset through the Asset Picker. Click a tile to make it
  the brush; drag over tiles to pick a block as a stamp. The "..." button opens the asset menu for the tileset.
- **Tile section**: for the selected tile, pick its collision (Solid, One-way, Ladder, Hazard, Water, None), give it a
  **Tag**, an **Animation** (none, 2, 3 or 4 frames) and the **Auto-tile** set it belongs to.
- **Map view**: the real GBA screen (240 x 160) drawn from the data the ROM uses, with a 16 px grid. Right or middle
  drag moves the camera, the mouse wheel scrolls (Shift: sideways, Ctrl: zoom), arrow keys move one tile.
- **View chips**: the tile under the pointer, the current brush (click to pick the Brush tool), a legend of the
  collision types used (click one to hide or show it) and the zoom (click to step 100% to 400%).
- **Bottom panel**: tabs **Layers** (each layer's type, priority, parallax and where it sits in VRAM, plus eye and
  lock buttons; right-click to add a layer), **Auto-tile rules** and **Assets**.
- **Map panel (right)**: **Size** in tiles (with the size in pixels), **BG size**, **Colours** (4bpp, 16 per tile, or
  8bpp, 256 colours) and **Wrap** for the current layer. The "..." button opens Add Layer.
- **Brush section**: **Stamp** size in tiles, **Random** (the chance a painted cell takes a random tile of the stamp)
  and **Flip H**.
- **Collision section**: where collision comes from (tile properties or a collision-only layer) and how many Solid and
  One-way tiles the map has. Actors with a Platformer Body collide with these with no code.
- **GBA Budget**: meters for unique tiles (of 1024), VRAM for backgrounds (of 64 KB), screen blocks (of 32) and BG
  palettes (of 16). "all good" shows when every meter is under 80%.
- **Status bar**: the layer and tile under the pointer, an **Auto-tile on / off** switch, and "Saved" when the map
  has no unsaved changes.

Differs from Figma: in the Figma prototype, clicking Scene in the title bar menus goes back to the Scene Editor; in
AGB Studio it opens the Scene menu, and the Scene document tab goes back to the Scene Editor.

## Related

- [Auto-tile Rules panel](T-P26-auto-tile-rules-panel.md)
- [Scene Editor](T-P01-scene-editor.md)
- [Asset Picker](T-O04-asset-picker.md)
- [Video Mode](T-O02-video-mode.md)
- [Palette Manager](T-P10-palette-manager.md)
- [Import Asset](T-D04-import-asset.md)
- [GBA hardware guide: Tiles and maps](../gba-hardware-guide.md#tiles-and-maps)
- [GBA hardware guide: Memory](../gba-hardware-guide.md#memory)
- [GBA hardware guide: Video modes](../gba-hardware-guide.md#video-modes)
