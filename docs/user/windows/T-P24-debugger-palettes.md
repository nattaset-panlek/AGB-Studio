# Debugger · Palettes

Figma: T-P24 · Page

No caption in Figma.

The Palettes view of the Debugger shows the GBA palette memory live while the game runs, marks colours that changed in
this frame, and tells you what uses a colour.

## Opens from

- Debugger › Views › Palettes

## What is on it

- **Toolbar, Views list, Breakpoints and status bar**: the same as the main [Debugger](T-P19-debugger.md) page.
- **Palette RAM · live**: two grids of 16 rows by 16 colours, "BG palettes · 0x0500 0000" and "OBJ palettes · 0x0500
  0200". A white outline marks a colour that changed this frame (for example a water colour cycle). Click a colour to
  select it.
- **Colour panel**: the selected entry (for example "BG 1 · index 7") with a swatch, its address, its value in the
  GBA's 15-bit BGR555 form, and its RGB values.
- **Used by**: which layers and tiles use that background palette, or which actors (with their OAM slots) use that
  sprite palette; the palette effect of the project that changes this colour, for example "Palette effect · water
  cycle (6–9)" (an effect on every palette, such as a screen fade, is not listed); and how many frames ago the colour
  last changed ("Not changed while playing" if it has not).
- **Open in Palette Manager**: opens the Palette Manager at that palette bank.
- **Break when this colour changes**: pauses the game the next time this colour changes. Click again to turn it off.

## Related

- [Debugger](T-P19-debugger.md)
- [Debugger · Memory](T-P23-debugger-memory.md)
- [Palette Manager](T-P10-palette-manager.md)
- [Palette Effect](T-O10-palette-effect.md)
- [GBA hardware guide: palettes](../gba-hardware-guide.md#palettes), [effects](../gba-hardware-guide.md#effects)
