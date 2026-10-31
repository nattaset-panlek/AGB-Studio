# Import CSV

Figma: T-D19 · Dialog

> นำเข้าข้อมูลจาก CSV / Google Sheets: จับคู่คอลัมน์กับ field ตรวจชนิดข้อมูล และแจ้งแถวที่มีปัญหาก่อนนำเข้า

Brings rows in from a CSV file or a Google Sheets export: match each column to a field, check the data types, and see
which rows have problems before importing.

## Opens from

- Database › Import CSV (the button above the grid, or the toolbar button). A file picker asks for the `.csv` file
  first.

## What is on it

- **File line**: the file name, the number of data rows and the text encoding (UTF-8, or Latin-1 when the file is
  not UTF-8). **Choose another…** picks a different file.
- **Map columns**: one row per CSV column, with:
  - **CSV column**: the column name from the first line of the file.
  - **Sample**: the value from the first data row.
  - **Field**: the table field this column fills. AGB Studio matches columns by name when the file opens. A translated
    field has one choice per language (for example `name · EN` and `name · TH`), and a column named like `Name_TH`
    goes to that language. Choose `— skip —` to leave a column out.
  - **Check**: "ok", "skipped", or the number of warnings when values do not fit the field type (for example text in
    a number column, or a value that is not in the enum list).
- **Warning box**: the row-by-row problems, in plain words.
- **Footer note**: how many rows have warnings; those values use the field default.
- **Import N rows**: rows whose id is already in the table are updated, the other rows are added at the end.
  Translated text goes to Localization. The whole import can be undone.
- **Cancel**: closes without importing.

Differs from Figma: the caption mentions Google Sheets; the dialog only reads `.csv` files, so export the sheet as CSV
first.

## Related

- [Database](T-P07-database.md)
- [New Table](T-D17-new-table.md)
- [Add Field](T-D18-add-field.md)
- [Localization](T-P14-localization.md)
