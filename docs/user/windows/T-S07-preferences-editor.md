# Preferences · Editor

Figma: T-S07 · Dialog

> แท็บ Editor: การบันทึกอัตโนมัติ ประวัติ undo ค่า snap และเลือกได้ว่าจะแก้สคริปต์ใน Script Editor ในตัว หรือเปิดด้วย editor ภายนอก (VS Code, Rider, Visual Studio) ที่ตั้ง path ไว้ในแท็บ Paths

The Editor tab: autosave, undo history, snap, and whether scripts are edited in the built-in Script Editor or opened in
an external editor (VS Code, Rider, Visual Studio) whose path is set on the Paths tab.

## Opens from

- File › Preferences… (Ctrl+,), then the Editor tab in the side bar

## What is on it

- **Autosave**: off, or save every 1, 2, 5 or 10 minutes.
- **Undo history**: how many steps Undo keeps, 1 to 10000.
- **Default snap**: 8, 16 or 32 px. Saved, but not used by the Scene Editor yet.
- **Open last project on start**: opens the project you had open last time instead of the Launcher.
- **Show collision overlay in Scene** and **Show GBA budget in Inspector**: saved, but not used by any window yet.
- **Script editor (Built-in / External)**: where scripts are edited.
- **Editor**: Visual Studio Code, JetBrains Rider, Visual Studio or Custom….
- **Executable**: read only. Shows the program file and where it came from: "set in Paths", "detected" (found where
  that editor is usually installed) or "not set".
- **Open .cs with**: External editor or Built-in editor. A script opens in the external editor only when Script editor
  is External, this is set to External editor and the program is found; otherwise it opens in the Script Editor.
- **Generate .csproj for IntelliSense**: writes a project file so the external editor can offer completions.
- **Hot reload when an external save is detected**: when you save a script in the external editor, the change is
  loaded again (if hot reload is on in Project Settings · Scripting).
- **Built-in code editor**: Font (JetBrains Mono), Tab size (1 to 16) and Theme (Studio Dark or Studio Light). Font and
  Theme are saved but the Script Editor does not change with them yet.
- **Format on save**, **Autocomplete as you type**, **Underline analyzer warnings**: options for the built-in Script
  Editor.
- **Footer**: Cancel (or Esc) drops the changes; Save keeps them and applies autosave and undo history at once.

Differs from Figma: Default snap, Show collision overlay in Scene, Show GBA budget in Inspector, and the built-in
editor Font and Theme are stored but have no effect yet.

## Related

- [Preferences](T-D09-preferences.md) (the dialog and its other tabs)
- [Preferences · Paths](T-S08-preferences-paths.md)
- [Script Editor](T-P04-script-editor.md)
- [Project Settings · Scripting](T-S04-project-settings-scripting.md)
- [Grid & Snap](T-O03-grid-and-snap.md)
