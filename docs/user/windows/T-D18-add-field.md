# Add Field

Figma: T-D18 · Dialog

> เพิ่มคอลัมน์ให้ตาราง เลือกชนิดข้อมูล ค่าเริ่มต้น และดูว่าใช้หน่วยความจำต่อแถวเท่าไร

Adds a column to a table: choose its data type and default value, and see how much memory each row uses.

## Opens from

- Database › Schema +

## What is on it

- **Field name**: the column name. It must be a C# name (letters, digits and `_`) and not already used in the table.
- **Type**: one of int (whole number), fixed (number with a fraction), bool (yes/no), text, enum (a fixed list of
  values), sprite, sound, script or reference (a row of another table).
- **Enum values** (enum only): the values as chips. Click a chip to remove it. **Add value** opens a small box; Enter
  adds the value, Esc cancels. An enum needs at least one value.
- **Table** (reference only): the table this field points at.
- **Default**: the value new rows get. It is a list for enum, bool, sprite, sound and reference fields, and a text
  box for the others. Script fields have no default.
- **Show in the record inspector**: shows the field in the Record panel of the Database.
- **Localized**: makes a text field translatable (only for text).
- **Per row**: the ROM bytes this field adds to each row, with the number of rows in the table, for example
  "1 B · 24 rows".
- **Add Field**: checks the entries and adds the field to the table. Undo removes it.
- **Cancel**: closes without changes.

## Related

- [Database](T-P07-database.md)
- [New Table](T-D17-new-table.md)
- [Localization](T-P14-localization.md)
- [GBA hardware guide: Memory](../gba-hardware-guide.md#memory)
