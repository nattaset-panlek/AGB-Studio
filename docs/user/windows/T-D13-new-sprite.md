# New Sprite

Figma: T-D13 · Dialog

> สร้าง sprite ใหม่: ตั้งชื่อ ขนาดตามที่ GBA รองรับ จำนวนเฟรม และเลือกชุดสี (palette) ที่จะใช้ร่วม

Make a new sprite: give it a name, a size the GBA supports, a number of frames, and the palette it shares.

## Opens from

- Sprite Editor › Sprites +
- Also: Create › Sprite (opens the Sprite Editor first, then this dialog)

## What is on it

- **Name**: the sprite's file name. It starts as a free name such as "new_sprite"; if the name is taken, a number is
  added.
- **Size**: one of the 12 sprite sizes the GBA hardware supports, square, wide or tall, from 8 to 64 px. It starts at
  the size of the sprite open in the Sprite Editor.
- **Frames**: how many frames the sheet has, from 1 to 64.
- **Palette**: an existing OBJ palette to share, or "New palette". The hint below says which sprite already uses those
  16 colours, or that the sprite gets its own 16 colours.
- **Start from**: **Blank**, **Copy of** the sprite open in the Sprite Editor (greyed out when there is none), or
  **Import PNG…**, which asks for a PNG file. An imported image is cut into frames and its colours are reduced to fit
  the palette.
- **Create animation "idle"**: adds an animation named "idle" (on by default).
- **Add a collider**: adds a collider 4 px smaller than the sprite, for example "Add a 12 × 12 collider" for 16 x 16.
- **Create Sprite**: writes the new sprite file and opens it in the Sprite Editor. **Cancel** closes without changes.

## Related

- [Sprite Editor](T-P08-sprite-editor.md)
- [Palette Manager](T-P10-palette-manager.md)
- [GBA hardware guide: Sprites](../gba-hardware-guide.md#sprites)
- [GBA hardware guide: Palettes](../gba-hardware-guide.md#palettes)
