# Project Settings · Save data

Figma: T-S02 · Dialog

> แท็บ Save data: เลือกชนิดหน่วยความจำ save บนตลับ จำนวนช่อง save และกำหนดว่าจะเก็บตัวแปรอะไร ขนาดเท่าไร

The Save data tab: choose the kind of save memory on the cartridge, the number of save slots, and which values are
saved and how big they are.

## Opens from

- File › Project Settings…, then the Save data tab in the side bar

## What is on it

- **Save type**: None, SRAM 32 KB, Flash 64 KB, Flash 128 KB or EEPROM 8 KB. This is the same setting as Save type on
  the Build tab.
- **Fields table**: what the game remembers, one row per value, with Field (its name), Type and Size.
- **Field**: one word made of letters, digits or `_`. Clear the name to remove the field when you save.
- **Type**: `int`, `byte`, `short`, `bool`, `fixed`, `fixed2`, `bitset <N>` (N on/off flags) or a table list such as
  `Items[16]`. An unknown type blocks Save.
- **Size**: worked out from the type: `bool` and `byte` 1 B, `short` 2 B, `int` and `fixed` 4 B, `fixed2` 8 B,
  `bitset 128` 16 B, `Items[16]` 32 B.
- **Add field**: adds a new `int` field named `field1`, `field2` and so on.
- **Slots**: how many save slots the game has, 1 to 16.
- **Autosave**: saves on every scene change.
- **Slots meter**: the space all slots need against the size of the save chip, for example `189 B / 32 KB`. It turns
  yellow above 90% and red when the slots do not fit; then Save is greyed out. With save type None it says
  "no save chip".
- **Bytes per slot**: the total size of one slot.
- **Checksum each slot**: stores a check value so a damaged save can be found.
- **Code sample**: shows how a script writes and reads a slot.
- **Footer**: Cancel (or Esc) drops the changes; Save writes them to the project.

Differs from Figma: Size is always worked out from the type, so every `int` shows 4 B; Figma shows the `int` fields
`lives` and `coins` as 1 B and 2 B.

## Related

- [Project Settings](T-D08-project-settings.md) (the dialog and its other tabs)
- [Project Settings · Build](T-S03-project-settings-build.md)
- [Database](T-P07-database.md) (tables that `Items[16]` style fields refer to)
- [GBA hardware guide: Save data](../gba-hardware-guide.md#save-data)
