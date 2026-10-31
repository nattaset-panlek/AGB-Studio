# Opening Project

Figma: T-D22 · Dialog

> สถานะกำลังโหลด: เห็นทุกขั้นตอนตอนเปิดโปรเจกต์ (อ่านไฟล์ โหลด asset คอมไพล์สคริปต์) และยกเลิกได้

The loading window shown while a project opens: you can follow every step (reading the files, loading assets,
compiling scripts) and cancel at any time.

## Opens from

- Launcher › Project card (double-click, or select it and press Enter)
- File › Open Recent
- Launcher › Open… (after you pick a project folder)
- The project menu on the Launcher (right-click a card › Open)
- A sample project card in Launcher · Learn

## What is on it

- **Title**: "Opening <project name>…", with a close button that works like Cancel.
- If the project that is open now has unsaved files, Unsaved Changes asks first. Choosing to stay stops the open.
- **Steps**: four rows, each with a clock (waiting), turning arrows (running) or a green check (done):
  - **Read project**: reads the project file and checks every scene. Shows the file name and the scene count, for
    example "SkyHopper.agbproj · 5 scenes".
  - **Load assets**: loads sprites, tilesets, maps, fonts, music, sounds, tables, dialogue and text. Shows a count
    and the current file, such as "29 / 48 · forest_tiles.png", with a progress bar.
  - **Compile scripts**: compiles the C# scripts as a check. Shows the script count and, if any, the number of
    errors.
  - **Open last scene**: names the scene that will open (the last one you used, else the start scene).
- **Cancel** (or Esc): stops at once. Nothing changes until the last step, so the project you had open stays open.
- When all steps are done the dialog closes and the Scene Editor opens (or the empty-project view for an empty
  project). Assets that could not be read are counted in a message. If the project cannot be opened at all, a
  message says why.

## Related

- [Launcher](T-P12-launcher.md)
- [Unsaved Changes](T-D12-unsaved-changes.md)
- [Scene Editor](T-P01-scene-editor.md)
- [Scene Editor · Empty project](T-P22-scene-editor-empty-project.md)
- [Launcher · Learn](T-P21-launcher-learn.md)
