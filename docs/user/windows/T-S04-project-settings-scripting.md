# Project Settings · Scripting

Figma: T-S04 · Dialog

> แท็บ Scripting: ภาษาสคริปต์ (C# ชุดย่อยสำหรับ GBA), hot reload และกฎของตัวตรวจโค้ด (Analyzer) ว่าข้อไหนเป็น Error หรือ Warning

The Scripting tab: the script language (a C# subset made for the GBA), hot reload, and which code checker (Analyzer)
rules count as an Error or a Warning.

## Opens from

- File › Project Settings…, then the Scripting tab in the side bar

## What is on it

- **Language**: `C# · GBA subset`, the only script language.
- **Generated**: the folder where the C++ made from your scripts is written, for example `build/gen`. It cannot be
  empty.
- **Namespace**: the C# namespace for your scripts, for example `SkyHopper`. Leave it empty or use a valid C# name.
- **Hot reload in Play mode**: script changes are applied while the game runs in Play mode.
- **Treat warnings as errors**: any Analyzer warning stops the build.
- **Show inline hints in the editor** (on by default): clicking an underlined Analyzer message in the Script Editor,
  or pressing Ctrl+. on it, opens its hint next to the code. When it is off, the underline stays and the message is
  still in the Problems list, but no hint opens.
- **Analyzer rules table**: each rule with what it catches and its severity:
  - **GAE001**: a class allocated in Update / OnTouch (there is no garbage collector).
  - **GAE002**: `float` or `double` (the GBA has no FPU; use `fixed`).
  - **GAE003**: `new` inside Update (allocate in Start or use a pool).
  - **GAE004**: `try` / `throw` (exceptions are not supported).
  - **GAE005**: joining strings inside a loop.
  - **GAE006**: a lambda that captures variables.
- **Severity**: click the chip to choose Error, Warning, Info or None. New projects start with GAE001, GAE003 and
  GAE004 as Error and the others as Warning.
- **Footer**: Cancel (or Esc) drops the changes; Save writes them to the project.

## Related

- [Project Settings](T-D08-project-settings.md) (the dialog and its other tabs)
- [Script Editor](T-P04-script-editor.md)
- [Scripting API](T-P18-scripting-api.md)
- [Preferences · Editor](T-S07-preferences-editor.md)
- [Play Mode](T-P02-play-mode.md)
- [GBA hardware guide: Memory](../gba-hardware-guide.md#memory)
