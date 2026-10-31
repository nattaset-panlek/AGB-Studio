# Preferences · Paths

Figma: T-S08 · Dialog

> แท็บ Paths: ที่อยู่ของ devkitPro, โฟลเดอร์โปรเจกต์ และโปรแกรมภายนอก (script editor, Aseprite, Tiled) พร้อมสถานะว่าพบหรือไม่ และล้าง build cache

The Paths tab: where devkitPro, the projects folder and outside programs (script editor, Aseprite, Tiled) are, with a
found / not found mark for each, and a button to clear the build cache.

## Opens from

- File › Preferences… (Ctrl+,), then the Paths tab in the side bar
- Project Settings · Build › Change in Preferences…
- Preferences · Emulator › Change… (after you pick a folder)
- The "devkitPro not found" or "A build tool is missing" message › Open Preferences · Paths

## What is on it

Each path row has a text box, a mark (green check when the file or folder exists, yellow warning when it does not or
is empty) and **Browse…** to pick it. Paths can use Windows variables such as `%LOCALAPPDATA%`.

- **devkitPro**: the folder with the GBA compiler and libraries. When it is not set, AGB Studio looks at the
  `DEVKITPRO` variable, then `C:\devkitPro` and `D:\devkitPro`. Below it: the devkitARM version and whether libtonc and
  maxmod were found, or what is missing.
- **Projects folder**: a folder for your projects. Saved, but new projects do not use it yet.
- **Script editor**: the program file of your external code editor, for example `Code.exe`. The line below says if it
  was found; it is used when Script editor is External on the Editor tab.
- **Image editor**: meant to open `.png` / `.aseprite` files on double-click. Saved, but not used yet.
- **Tiled**: optional, for `.tmx` maps. Saved, but not used yet.
- **Build cache card**: the open project's cache folder and its size. **Clear cache** empties it (and the compiled
  files), so the next build converts every asset again. Greyed out when no project is open.
- **Footer**: Cancel (or Esc) drops the changes; Save keeps them. A changed devkitPro folder is checked again at once.

Differs from Figma: the build cache is per project (the project's `build/cache` and `build/obj` folders), not the one
shared cache folder under `%LOCALAPPDATA%` that the Figma frame shows.

Differs from Figma: Projects folder, Image editor and Tiled are stored but have no effect yet.

## Related

- [Preferences](T-D09-preferences.md) (the dialog and its other tabs)
- [Preferences · Editor](T-S07-preferences-editor.md)
- [Project Settings · Build](T-S03-project-settings-build.md)
- [Build ROM](T-D06-build-rom.md)
- [Import Asset](T-D04-import-asset.md)
