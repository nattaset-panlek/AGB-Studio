# Asset Picker

Figma: T-O04 · Popover

> เลือก asset ให้ช่องใน Inspector (เช่น Sheet ของ sprite) มีค้นหาและกรองตามชนิด

Choose an asset for a field in the Inspector (for example the Sheet of a sprite), with search and a filter by type.

## Opens from

- The Sheet dropdown in the Inspector's Sprite section (#28)
- Add to Scene › Prefab… (the + of the Hierarchy)
- Tilemap Editor › Tileset dropdown

## What is on it

- **Title bar**: "Choose sprite sheet", "Choose tileset" or "Choose font", following the tab; "Choose prefab" when
  opened from Add to Scene › Prefab…. It has a close button.
- **Search assets…**: type part of a file name to filter the cards. The search box has the keyboard when the popover
  opens.
- **Type tabs**: Sprites, Tilesets and Fonts. The picker opens on the tab of the field that opened it (Sprites from
  the Inspector's Sheet, Tilesets from the Tilemap Editor's Tileset dropdown). Click a tab to list that type. The tabs
  are hidden when choosing a prefab.
- **Asset cards**: each card shows the real pixels, the file name and a short note such as "16×16 · 8f" (size and
  frame count). The sprite the selected actor uses now is highlighted.
- **Click a card**: picks that asset for the field that opened the picker and closes the popover.
- **Prefab mode**: lists the project's saved prefabs. Picking one adds a copy of it to the scene at the spawn point
  (this can be undone).

## Related

- [Scene Editor](T-P01-scene-editor.md)
- [Tilemap Editor](T-P03-tilemap-editor.md)
- [Sprite Editor](T-P08-sprite-editor.md)
- [Font Manager](T-P09-font-manager.md)
- [Import Asset](T-D04-import-asset.md)
