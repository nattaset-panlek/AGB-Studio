# Database

Figma: T-P07 · Page

> เก็บข้อมูลเกมเป็นตาราง (ไอเทม ศัตรู ร้านค้า เควสต์ ข้อความ) แก้แบบ spreadsheet แล้วเรียกใช้ในสคริปต์แบบมี type เช่น Items.Get(ItemId.Potion) ข้อมูลถูกเก็บเป็น const ใน ROM ไม่กิน RAM

Keeps your game data in tables (items, enemies, shops, quests, text) that you edit like a spreadsheet and read from
scripts by name, for example `Items.Get(ItemId.Potion)`. The data is stored in the ROM as constants, so it uses no RAM.

## Opens from

- Database tab in the bottom panel of the Scene Editor (#23)
- Tools › Database
- Create › Data Table (opens the Database, then the New Table dialog)
- Double-clicking a `.db` file in the Assets panel

## What is on it

- **Toolbar**: Add row, Table view, Filter rows (jumps to the filter box) and Import CSV. The chip "Stored in ROM as
  const data" shows when the table is kept in ROM. Play, the video mode chip and Undo / Redo are on the right.
- **Tables** (left): every table of the project (`data/*.db`) with its row count. Click one to show it. The **+**
  button opens New Table.
- **Schema** (left, below Tables): the fields of the selected table with their type (`loc` marks translated text).
  The **+** button opens Add Field.
- **Filter rows**: type to show only the rows that match.
- **Import CSV** and **Add row** buttons: Import CSV asks for a `.csv` file and opens the Import CSV dialog. Add row
  adds a row at the end with a new id (for example `Item25`) and the field defaults, and selects it.
- **Grid**: one row per record, with the sprite icon, id and every field. Click selects a row, the Up and Down arrow
  keys move the selection, double-click edits a cell (a list of choices for enum, yes/no, sprite, sound and reference
  fields). Enter keeps the edit, Esc cancels it. Wrong numbers and duplicate ids are refused with a message.
- **Edit menu**: Cut, Copy, Paste, Duplicate and Delete work on the selected row. A pasted or duplicated row gets an
  id ending in `Copy`.
- **Use it in a script**: C# lines for the selected row, using the names AGB Studio makes for the table.
- **Record** (right): the selected row's icon, name and script id. **Fields** shows one box per field (and one per
  language for translated text): a list for enums, a slider for fixed-point numbers, a switch for yes/no.
  **Description** edits the description text. **Used by** lists other tables, scenes, scripts and dialogue that
  point at this row; click one to open it. The **...** menu has Duplicate and Delete.
- **Status bar**: table name with row and field counts, the table's ROM size, and "Saved" when there is nothing to
  save. Every edit can be undone. Save writes the `.db` file.

The Pause and Step buttons in the toolbar act on a running game from this page too: Pause pauses and resumes it, Step
moves it one frame (F10). They are greyed out while no game runs.

## Related

- [New Table](T-D17-new-table.md)
- [Add Field](T-D18-add-field.md)
- [Import CSV](T-D19-import-csv.md)
- [Localization](T-P14-localization.md)
- [Script Editor](T-P04-script-editor.md)
- [Scripting API](T-P18-scripting-api.md)
- [GBA hardware guide: Memory](../gba-hardware-guide.md#memory)
