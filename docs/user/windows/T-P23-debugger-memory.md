# Debugger · Memory

Figma: T-P23 · Page

No caption in Figma.

The Memory view of the Debugger shows where the game's work RAM goes, a hex view of one value, and a list of values to
watch while the game runs.

## Opens from

- Debugger › Views › Memory

## What is on it

- **Toolbar, Views list, Breakpoints and status bar**: the same as the main [Debugger](T-P19-debugger.md) page.
- **Memory map**: "where the 288 KB of work RAM goes". One bar for EWRAM (256 KB) and one for IWRAM (32 KB), each with
  KB used and a coloured part per category, plus the free space.
  - EWRAM categories: Actor pool, Tilemap buffers, Audio mixer, Strings (and Other when used).
  - IWRAM categories: Fast code (ARM), Stack (the deepest the game has reached so far), Interrupts, Globals.
- **Hex view**: five rows of 16 bytes at the selected watch's address, with the address, the bytes and their text
  form. The title shows the address and what owns it (for example "Player").
- **Watch memory**: cards with a name, its address and its live value. It starts with player.position,
  player.velocity, player.lives, Game.score and Save.buffer when the game has them. Click a card to show it in the hex
  view.
- **Add watch**: type a name (for example `player.position` or `Game.score`) or an address (for example
  `0x02001A40`), then press Enter. Esc cancels. A name the game does not know is not added.

## Related

- [Debugger](T-P19-debugger.md)
- [Debugger · Palettes](T-P24-debugger-palettes.md)
- [Play Mode](T-P02-play-mode.md)
- [Project Settings · Save data](T-S02-project-settings-save-data.md)
- [GBA hardware guide: memory](../gba-hardware-guide.md#memory), [save data](../gba-hardware-guide.md#save-data)
