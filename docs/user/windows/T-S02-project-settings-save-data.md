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
- **Slots meter**: the space all slots need against the size of the save chip, for example `197 B / 32 KB` (three slots of 63 B plus the 8 B options block). It turns
  yellow above 90% and red when the slots do not fit; then Save is greyed out. With save type None it says
  "no save chip".
- **Options block**: besides the slots the chip keeps 8 bytes for what the player sets in the game's Options screen:
  music volume, sound effects volume, language and the remapped buttons. The game loads them when it starts and writes
  them when the Options screen closes after a change. They are not part of a slot, so saving or erasing a slot leaves
  them alone; the Slots meter counts them (on Flash they take the chip's last 4 KB sector).
- **Bytes per slot**: the total size of one slot.
- **Checksum each slot**: stores a check value so a damaged save can be found.
- **Save during cutscenes**: a save made while a cutscene runs also keeps where the cutscene is, and loading goes on from
  there: the line on screen shows again, or the move runs again. Actors other than the player stand where the scene puts
  them. It takes 320 bytes a slot, and a cutscene you change after the save does not go on.
- **Code sample**: shows how a script writes and reads a slot.
- **Footer**: Cancel (or Esc) drops the changes; Save writes them to the project.

Differs from Figma: Figma gives `lives` and `coins` the type `int` with sizes 1 B and 2 B. In AGB Studio the size
always follows the type, so an `int` is always 4 B; the sample project gets the Figma sizes by giving `lives` the type
`byte` and `coins` the type `short`.

## Related

- [Project Settings](T-D08-project-settings.md) (the dialog and its other tabs)
- [Project Settings · Build](T-S03-project-settings-build.md)
- [Database](T-P07-database.md) (tables that `Items[16]` style fields refer to)
- [GBA hardware guide: Save data](../gba-hardware-guide.md#save-data)

Differs from Figma: the frame's Slots meter shows `189 B / 32 KB` (the slots only); AGB Studio adds the 8 B options
block, so the same project shows `197 B / 32 KB`.
