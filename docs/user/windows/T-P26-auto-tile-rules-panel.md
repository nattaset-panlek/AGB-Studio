# Auto-tile Rules panel

Figma: T-P26 · Page

> แผงล่างของ Tilemap Editor แท็บ Auto-tile rules: กฎ 3×3 ว่าเมื่อรอบ ๆ เป็น tile แบบไหน ให้ใช้ tile ไหน ระบบจะต่อขอบหญ้า มุม และปลายให้เองตอนระบาย

The Auto-tile rules tab at the bottom of the Tilemap Editor. Each 3 x 3 rule says which tile to use when the
neighbouring tiles look a certain way, so grass edges, corners and ends join up by themselves while you paint.

## Opens from

- Tilemap Editor › Auto-tile rules tab

## What is on it

- **Tabs**: Layers, Auto-tile rules and Assets. This page is the Auto-tile rules tab.
- **Rule set**: a dropdown (shown only on this tab) that picks which auto-tile set you are editing, for example
  "Grass edges (16)".
- **Rule cards**: one card per rule. The left side is a 3 x 3 grid of the neighbours around the painted tile; the
  right side is the tile the rule places. The card's name is shown below it, for example "Top edge".
- **Neighbour cells**: click a cell to cycle it: green (a tile of the same set must be there), red outline (it must
  not be a tile of the set), grey (anything). The centre cell is the tile being painted and does not change.
- **Result tile**: double-click it to replace it with the tile selected in the Tileset panel. Hover to see the
  tile's name.
- **Selecting a rule**: click a card; the selected card has a blue outline. Press Delete or Backspace to remove the
  selected rule.
- **Add rule**: adds a new rule for the selected tile, with every neighbour set to "anything". If no set is chosen,
  a new set named after the tile is made.
- Every change can be undone.
- Auto-tile only works while painting when the status bar of the Tilemap Editor says **Auto-tile on**.

## Related

- [Tilemap Editor](T-P03-tilemap-editor.md)
- [GBA hardware guide: Tiles and maps](../gba-hardware-guide.md#tiles-and-maps)
