# New Table

Figma: T-D17 · Dialog

> สร้างตารางข้อมูลใหม่ (เช่น ศัตรู อาวุธ เควสต์) เริ่มจากว่าง จาก CSV หรือ copy โครงจากตารางเดิม

Creates a new data table (for example enemies, weapons or quests), starting empty, from a CSV file, or as a copy of an
existing table.

## Opens from

- Database › Tables +
- Create › Data Table (through the Database page)

## What is on it

- **Table name**: the name of the table. It must be a C# name (letters, digits and `_`) and not used by another
  table. The line under it shows how scripts will use it, for example `Enemies.Get(EnemyId.Bat)`.
- **Start from**:
  - **Empty**: the table starts with three fields: `id`, `name` and `description`.
  - **CSV file…**: asks for a `.csv` file. The first column becomes the id, the other columns become fields with a
    type guessed from the values (whole number, number, yes/no or text). Columns like `Name_EN` and `Name_TH` become
    one translated field. If the name box is empty, it is filled from the file name.
  - **Copy &lt;table&gt;**: copies the fields and rows of the table selected in the Database. Shown only when a table
    is selected.
- **Key field**: `id · enum`. Every row gets a name you can autocomplete in C#.
- **Localize text fields (name, description)**: makes the text fields translatable, so they appear in Localization.
- **Keep in ROM as const data**: stores the table in the ROM so it uses no RAM.
- **Create Table**: checks the name, writes `data/<Name>.db` and shows the new table. Undo removes it again.
- **Cancel**: closes without creating anything.

## Related

- [Database](T-P07-database.md)
- [Add Field](T-D18-add-field.md)
- [Import CSV](T-D19-import-csv.md)
- [Localization](T-P14-localization.md)
- [GBA hardware guide: Memory](../gba-hardware-guide.md#memory)
