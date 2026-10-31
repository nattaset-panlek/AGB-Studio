# Budget Details

Figma: T-O06 · Popover

> รายละเอียดการใช้ทรัพยากรของ GBA ทั้งหมด พร้อมคำแนะนำว่าลดตรงไหนได้

All of the GBA resources the scene uses, in detail, with a tip on where you can save.

## Opens from

- The GBA Budget block at the bottom of the Inspector (#33)

## What is on it

- **Title bar**: "GBA budget · " and the name of the open scene, with a close button.
- **Meters**: one bar per resource, with the amount used and the limit. A bar turns orange at 90 percent and red when
  it is over the limit.
  - **VRAM BG**: tile and map memory for backgrounds, out of 64 KB.
  - **VRAM OBJ**: tile memory for all frames of the scene's sprites (32 KB, or 16 KB in the bitmap mode).
  - **OAM**: visible actors with a sprite, out of 128.
  - **BG palettes** and **OBJ palettes**: 16-colour palettes used, out of 16 each.
  - **Palette OBJ n**: how many of the 16 colours the fullest sprite palette uses.
  - **EWRAM**, **IWRAM** and **ROM**: from the last build. Before the first build they say "not built".
- **Tip**: when two sprites' colours would fit in one palette, a light-bulb note says so, for example "slime.png and
  coin.png could share OBJ palette 1 — saves one palette slot."
- **Open Build Report**: closes the popover and opens the Build Report.
- The numbers are worked out from the open scene each time the popover opens.

## Related

- [Scene Editor](T-P01-scene-editor.md)
- [Build Report](T-P11-build-report.md)
- [Palette Manager](T-P10-palette-manager.md)
- [Debugger](T-P19-debugger.md)
- [GBA hardware guide: memory](../gba-hardware-guide.md#memory)
- [GBA hardware guide: sprites](../gba-hardware-guide.md#sprites)
- [GBA hardware guide: palettes](../gba-hardware-guide.md#palettes)
- [GBA hardware guide: where the editor checks these limits](../gba-hardware-guide.md#where-the-editor-checks-these-limits)
