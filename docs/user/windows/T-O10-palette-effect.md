# Palette Effect

Figma: T-O10 · Popover

> ตั้ง effect ของ palette เช่นหมุนสี (น้ำไหล ลาวา) เลือกช่วงสี ความเร็ว และดูพรีวิวทันที ไม่กินพลังเครื่องเพราะเปลี่ยนแค่ palette

Set up a palette effect, such as cycling colours for flowing water or lava: pick the colour range and speed and see a
live preview. It costs almost nothing at run time because only the palette changes.

## Opens from

- Palette Manager › Fade / Cycle

## What is on it

- **Title**: names the effect, for example "Colour cycle · water", "Fade · to black" or "Flash on hit". The close
  button closes the popover.
- **Effect**: switch between **Fade**, **Cycle** and **Flash**. The popover opens on the one you clicked.
- **Bank**: the palette the effect changes. Fade and Flash can also use "All BG banks" or "All OBJ banks".
- **Range**: the 16 colours of the bank, with the range shown above ("Range · index 6 to 9"). For Cycle, click the
  first colour and then the last colour of the range.
- **Speed**: a slider in frames per step (up to 15 frames for Cycle, 60 for Fade, 30 for Flash).
- **Direction**: **Left**, **Right** or **Ping-pong** for the cycle.
- **Preview strip**: plays the effect frame by frame, the same way the game runs it. Press and drag on it to scrub
  through the effect; let go and it plays on.
- Every change shows in the Palette Manager at once, can be undone, and is saved with the project (File › Save).

## Related

- [Palette Manager](T-P10-palette-manager.md)
- [GBA hardware guide: Effects](../gba-hardware-guide.md#effects)
- [GBA hardware guide: Palettes](../gba-hardware-guide.md#palettes)
