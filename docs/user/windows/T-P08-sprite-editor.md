# Sprite Editor

Figma: T-P08 · Page

> วาด pixel art หรือนำเข้า PNG/Aseprite ระบบลดสีเหลือ 16 สีตาม palette ของ GBA เห็นเส้น tile 8×8 ทำ animation ที่ timeline ด้านล่าง

Draw pixel art or import images; colours are reduced to the 16 of a GBA palette, the 8 x 8 tile lines are shown, and
animations are made on the timeline at the bottom.

## Opens from

- Double-click a sprite in the Assets panel (#24)
- Create › Sprite (opens this page, then the New Sprite dialog)
- Asset Context Menu › Open
- Animation List › Edit Animations…
- Also: Asset Context Menu › Create Animation, and a sprite's document tab

## What is on it

- **Drawing tools**: Pencil, Eraser, Fill, Line, Rectangle and Pick colour. Every stroke is one undo step.
- **Mirror X, 8x8 tile grid, Onion skin**: preview the sprite flipped as the GBA would flip it, show or hide the tile
  grid, and show the previous frame faintly under the current one.
- **Format chip**: shows size, colour depth and palette (for example "16×16 · 4bpp · OBJ pal 0"). Click it to move
  the sprite to another OBJ palette so it shares those colours.
- **Play, video mode chip, undo / redo**: as on the other editor pages.
- **Sprites panel (left)**: search and list every sprite with its size and frame count. Click to open one; **+** opens
  New Sprite.
- **Import**: drop images on the drop zone or click it to pick a PNG. Options: **Auto-reduce to 16 colours**, **Split
  sheet into frames** (the current frame size) and **Share palette with** the current OBJ palette.
- **Canvas**: the current frame on a checkerboard, with chips for the animation and frame, the tile grid, the pixel
  and colour under the pointer, Mirror X and zoom. Ctrl + mouse wheel zooms; click the zoom chip to fit again.
- **Previews**: the frame at 1x, 2x and 4x.
- **Bottom panel**: tabs **Animation**, **Assets** and **Console**.
- **Animation tab**: the sprite's animations (click to pick, double-click to switch loop on or off), Play / Pause and
  Next frame, **Speed** in ticks (for example "6 ticks ≈ 10 fps"; press Enter to apply), **Onion skin**,
  **Ping-pong** and the frame strip (click a frame to edit it).
- **Palette panel (right)**: the 16 colours of the sprite's palette (click to pick a colour), the **Colour** editor
  (R, G and B from 0 to 31, the hex colour and the 15-bit value the ROM stores), and **Palette slots** listing every
  OBJ palette in use (click one to open it in the Palette Manager).
- **Sprite settings**: **OAM size** (opens the OAM Size menu), **Origin**, **Collider**, and **VRAM/frame**
  (read-only: the tiles and bytes one frame uses).
- **Status bar**: the sprite's size, frame count and colour depth; VRAM used by sprites out of 32 KB; "Saved" or
  "Unsaved changes".

Differs from Figma: the drop zone says "Drop PNG / Aseprite here", but only PNG files are imported here.

## Related

- [New Sprite](T-D13-new-sprite.md)
- [Palette Manager](T-P10-palette-manager.md)
- [Animation panel](T-P06-animation-panel.md)
- [Import Asset](T-D04-import-asset.md)
- [Scene Editor](T-P01-scene-editor.md)
- [GBA hardware guide: Sprites](../gba-hardware-guide.md#sprites)
- [GBA hardware guide: Palettes](../gba-hardware-guide.md#palettes)
- [GBA hardware guide: Memory](../gba-hardware-guide.md#memory)
