# Scene Editor · Empty project

Figma: T-P22 · Page

> สถานะโปรเจกต์ว่าง (เพิ่งสร้างแบบไม่มี template): ทุกแผงบอกว่าทำอะไรต่อได้ เช่น ลากรูปมาวาง เพิ่มผู้เล่น ระบาย tile หรือเลือก template

The empty-project state (a project just made without a template): every panel tells you what you can do next, such as
dropping in images, adding a player, painting tiles or choosing a template.

## Opens from

- Opening any project that has no assets, scripts, actors or map yet, for example a Puzzle project made in New Project
  with every include option off (there is no separate File › New Project (Empty) item: checklist 1.4 item 3)

## What is on it

The page looks like the Scene Editor, with the same title bar, toolbar and bottom panel tabs. Each empty panel says
what to do. In the toolbar, Play (F5) and the video mode chip work; Pause and Step act on a running game (Pause
pauses and resumes it, Step moves it one frame, F10) and are greyed out while no game runs.

- **Hierarchy**: the scene and Main Camera only, with the hint "Nothing else here yet. Press + or drag a sprite from
  Assets into the scene." The + opens Add to Scene.
- **Scene view**: the GBA screen as a dashed box with the card "This scene is empty".
  - **Add player**: adds a Player actor to the scene and switches to the Scene Editor.
  - **Paint tiles**: chooses the Paint tiles tool and switches to the Scene Editor.
  - **Import images**: opens Import Asset.
  - "or drop PNG / Aseprite files anywhere": drop `.png` or `.aseprite` files on the page to open Import Asset with
    them.
- **Inspector**: "Nothing selected" and "Select an actor or a layer to edit it here.", then **Useful keys** with the
  current shortcuts for Import assets (Ctrl+I), Paint tiles (B), Play (F5) and Command palette (Ctrl+K).
- **GBA budget**: VRAM BG, VRAM OBJ and OAM meters, all at zero for a new project.
- **Assets tab**: "No assets yet" with an **Import…** button that opens Import Asset.
- **Status bar**: Ready, "New project · 0 assets", and the ROM size after a build.
- **Leaving this page**: as soon as the project has content (an actor is added, files are imported or dropped, or you
  start painting), AGB Studio switches to the Scene Editor by itself.

Differs from Figma: the caption mentions choosing a template here; neither the Figma frame nor AGB Studio has a
template button on this page. Use File › New Project for templates.

## Related

- [Scene Editor](T-P01-scene-editor.md)
- [New Project](T-D01-new-project.md)
- [Import Asset](T-D04-import-asset.md)
- [Launcher · Templates](T-P20-launcher-templates.md)
- [Command Palette](T-O07-command-palette.md)
- [GBA hardware guide: where the editor checks these limits](../gba-hardware-guide.md#where-the-editor-checks-these-limits)
