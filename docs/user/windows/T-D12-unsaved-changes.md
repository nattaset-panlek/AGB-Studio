# Unsaved Changes

Figma: T-D12 · Dialog

> ขึ้นเมื่อปิดโปรแกรมหรือเปิดโปรเจกต์อื่นขณะที่ยังมีไฟล์ไม่ได้บันทึก

Appears when you close AGB Studio or open another project while some files still have changes you have not saved.

## Opens from

- File › Exit (Alt+F4)
- Closing the app window
- Opening another project with unsaved files (from the Launcher, File › Open Recent, or Create Project in New
  Project)

## What is on it

- **Title**: names what would lose the changes: "Save changes before exiting?", "Save changes before opening another
  project?" (from the Launcher or File › Open Recent), or "Save changes before closing the project?" (Create Project in
  New Project).
- **Message**: how many files have changes, for example "2 files have changes that will be lost if you don't save
  them."
- **File list**: one row per changed file, with its icon, its name (such as `Forest_01.scene` or
  `PlayerController.cs`) and the word "modified".
- **Save All**: saves every changed file, then exits or opens the other project.
- **Don't Save**: drops the changes, then exits or opens the other project.
- **Close button or Esc**: cancels. AGB Studio stays open with your changes, and the other project is not opened.

## Related

- [Opening Project](T-D22-opening-project.md)
- [New Project](T-D01-new-project.md)
- [Launcher](T-P12-launcher.md)
- [Preferences · Editor](T-S07-preferences-editor.md)
