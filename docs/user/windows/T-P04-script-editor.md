# Script Editor

Figma: T-P04 · Page

> เขียนสคริปต์เป็น C# (ชุดย่อยที่เหมาะกับ GBA) มี autocomplete และตัวตรวจ (Analyzer) ที่เตือนทันทีเมื่อใช้สิ่งที่ GBA ทำไม่ได้ เช่น float หรือการสร้าง object ระหว่างเกม พร้อมปุ่มแก้ให้อัตโนมัติ · แก้ในตัวนี้ หรือกด Open externally เพื่อเปิดใน VS Code / Rider ตามที่ตั้งไว้ใน Preferences ก็ได้

Write your game scripts in C# (the subset that suits the GBA), with autocomplete and an Analyzer that warns at once
when you use something the GBA cannot do, such as `float` or creating objects during play, and offers a fix. Edit here,
or press Open externally to use VS Code or Rider as set in Preferences.

## Opens from

- A script tab in the Scene Editor's document tabs (#17, for example PlayerController.cs)
- The Script component in the Inspector (#31)
- Create › C# Script (also opens the New Script dialog on top)
- Actor Context Menu › Edit Script
- Component Menu › Edit Script

## What is on it

- **Toolbar**: Find (Ctrl+F), the Code view button, and Debug, which opens the Debugger. Play (F5) runs the game;
  Pause pauses and resumes a running game and Step moves it one frame (F10); both are greyed out while no game runs.
  Undo and Redo are on the right.
- **Analyzer: GBA rules on**: a chip that opens Project Settings on its Scripting tab, where the Analyzer rules are.
- **Open externally**: opens the current script in the external editor set in Preferences, at the caret line. If that
  editor is not found, a message tells you to set it in Preferences › Paths.
- **C# → C++ → devkitARM**: a chip that opens the Build Report.
- **Scripts**: every script of the project; an "M" marks unsaved changes. The + button opens New Script.
- **Outline**: the class, its fields and its methods. Click one to jump to it in the code.
- **Document tabs**: every open document. A script tab shows it here; Game, `.map`, `.db`, `.graph` and scene tabs open
  their own windows.
- **Breadcrumb**: the folder, the file and the method the caret is in.
- **Code editor**: line numbers, colours, auto-indent and the tab size from Preferences. Autocomplete appears as you
  type, or with Ctrl+Space; Up, Down and Enter or Tab pick an item. Analyzer messages get a wavy underline.
- **Analyzer message popup**: click an underline (or press Ctrl+. on it) to see the rule, why the GBA needs it, and its
  fixes, for example "Change to fixed". "Ignore" turns the rule off for that line. Esc closes the popup.
- **Script panel**: the class with Attached to (the actors that use it) and Compiles to (the C++ file it becomes).
  Exposed fields lists the public fields; changing a slider, toggle or box writes the new default value into the code.
- **API quick reference**: the calls scripts use most (Input.Pressed, Sound.Play, Scene.Load, Save.Write and more).
  Click one to open it in the Scripting API. Open Scripting API opens the full reference.
- **Problems**: warnings and errors of the open scripts, plus the steps of the last script check. Click a line to go to
  it.
- **Terminal**: type a command and press Enter to run it in the project folder.
- **Output**: the full log of checks, builds and hot reloads.
- **Status line**: problem count (click to show Problems), caret line and column, the script language and the devkitARM
  version (or "devkitARM not found").
- **Saving**: File › Save (Ctrl+S) tidies the code when format on save is on in Preferences, then checks every script.
  When a script is saved in an external editor, AGB Studio reloads it, checks it and, if the game is running, updates
  the running game (hot reload). Preferences and Project Settings can turn hot reload off.
- **Graph scripts**: scripts written by the Logic Graph open read-only; edit the graph instead.

## Related

- [New Script](T-D14-new-script.md)
- [Find & Replace](T-O11-find-and-replace.md)
- [Scripting API](T-P18-scripting-api.md)
- [Logic Graph](T-P17-logic-graph.md)
- [Project Settings · Scripting](T-S04-project-settings-scripting.md)
- [Preferences · Editor](T-S07-preferences-editor.md)
- [Preferences · Paths](T-S08-preferences-paths.md)
- [Build Report](T-P11-build-report.md)
- [Console panel](T-P05-console-panel.md)
- [GBA hardware guide: the frame](../gba-hardware-guide.md#the-frame)
- [GBA hardware guide: memory](../gba-hardware-guide.md#memory)
