# Scene Editor

Figma: T-P01 · Page

> หน้าหลักของ editor: จัดฉาก วาง actor ดู/แก้ค่าใน Inspector และเปิด asset จากแถบล่าง (สร้างใหม่จาก component ทั้งหมด)

The main page of AGB Studio: lay out a scene, place actors, view and edit their values in the Inspector, and open
assets from the bottom panel.

## Opens from

- Default workspace: it opens when you open a project that has content (an empty project opens
  [Scene Editor · Empty project](T-P22-scene-editor-empty-project.md) instead)

## What is on it

- **Title bar**: the menus, the breadcrumb (project / scene; click it for the Scene Switcher), **Debug** (opens the
  Debugger, Ctrl+Shift+D) and **Build** (Build ROM, Ctrl+B).
- **Tools**: Select (V), Move (M), Paint tiles (B), Fill, Erase, and the grid button (G) that shows or hides the grid.
  Select and Move pick and drag actors; Paint tiles, Fill and Erase edit the scene's map in the scene view.
- **Layer toggles**: BG0, BG1, BG2, BG3 and OBJ show or hide each layer in the scene view. BG3 starts hidden.
- **Play, Pause, Step**: Play (F5) runs the game in Play Mode.
- **Video mode chip** ("GBA · Mode 0 · 240×160"): opens Video Mode. **Undo / Redo** buttons on the right.
- **Hierarchy** (left): the scene, Main Camera, the background layers and the actors. Actors of a group with two or
  more members sit in a folder. "Search scene…" filters the list. Click selects (the scene view and Inspector follow),
  Ctrl + click adds an actor to the selection or takes it out, Shift + click selects the actors from the first selected
  one to the clicked one, and Edit › Select All (Ctrl+A) selects every actor. Cut, Copy, Paste, Duplicate and Delete
  act on all selected actors as one undo step; after Cut, Paste puts them back where they were. Double click an actor
  to focus it in the view, F2 renames, right click opens the Actor Context Menu, + opens Add to Scene.
- **Document tabs**: Scene, Game, and open files. Game goes to Play Mode, a `.map` to the Tilemap Editor and a `.cs` to
  the Script Editor.
- **Scene view**: the GBA screen, 240×160, drawn the way the game draws it.
  - Click an actor to select it, drag to move it (it snaps to the grid when snapping is on). Arrow keys nudge it (or
    every selected actor). Each selected actor has an outline; the first one selected also has corner handles and its
    name.
  - With Paint tiles, click or drag to paint map cells; with Erase, to empty them; with Fill, click to fill the
    connected cells that match the clicked one. The brush is the tile (or stamp) chosen in the Tilemap Editor when it
    has this map open, otherwise the first named tile of the tileset. The painted layer is the layer selected in the
    Hierarchy, otherwise the ground (collision) layer. Each stroke or fill is one undo step, and the selection stays
    as it was.
  - Press A to place actors or T to place trigger zones (the Command Palette also has Player, Enemy, Pickup and Camera
    Zone): each click puts one on the grid. Drag to give a zone its size. Esc, a right click or another tool stops.
    Add Actor in the menus still puts the actor in the middle of the screen.
  - In the start scene, a START tag sits under the Player: the game starts there. Drag the Player or the tag to move it.
  - To paint collision, select the scene or its collision layer and choose a **Brush** in the Inspector's Collision
    section. This needs a Collision layer (add one in the Tilemap Editor). Without one, collision comes from the tiles
    of the map, and the section says so, because painting those would change the art. The list holds the types the
    tileset's tiles have.
  - Right click an actor for the Actor Context Menu.
  - Pan with the middle mouse button, Space + drag, or the mouse wheel. Ctrl + wheel zooms from 100% to 800%.
  - Chips at the bottom: scene name and size (opens Scene Settings), cursor position, Grid (opens Grid & Snap), zoom
    (opens the Zoom menu).
- **Inspector** (right): what is selected. With several actors selected it says "N actors selected" and shows the
  first one's values read-only.
  - Actor: name, layer and priority; Transform (position); Sprite (Sheet opens the Asset Picker, Animation opens the
    Animation List, frames, palette opens the Palette Manager); each behaviour with its fields; Collider; script
    fields (double click a script heading to open it); Add Component (opens the Component Picker). The "…" menu at
    the top is the Component Menu.
  - Scene: number of actors and a Scene Settings… button. Camera: dead zone and clamp to map. Layer: parallax and
    Open in Tilemap Editor.
  - GBA Budget: VRAM BG, VRAM OBJ and OAM meters with "all good" or "over budget". Click it for Budget Details.
    It is hidden when Preferences › Editor › Show GBA budget in Inspector is off.
  - Every change can be undone.
- **Bottom panel**: Assets, Console (with a message count), Animation and Database tabs. Drag the edge to resize.
  - Assets: categories with counts (Sprites, Tilesets, Maps, Fonts, Audio, Scripts, Data) and cards with the real
    pixels. Double click opens the asset's editor, right click opens the Asset Context Menu, drag a sprite into the
    scene view to add it as an actor.
  - Database opens the Database page.
- **Status bar**: Ready, last build (click for the Build Report), ROM size, speed, and emulator (click for
  Preferences).

## Related

- [Scene Editor · Empty project](T-P22-scene-editor-empty-project.md)
- [Console panel](T-P05-console-panel.md), [Animation panel](T-P06-animation-panel.md)
- [Scene Switcher](T-O01-scene-switcher.md), [Video Mode](T-O02-video-mode.md), [Grid & Snap](T-O03-grid-and-snap.md)
- [Asset Picker](T-O04-asset-picker.md), [Component Picker](T-O05-component-picker.md),
  [Budget Details](T-O06-budget-details.md), [Command Palette](T-O07-command-palette.md)
- [New Scene](T-D02-new-scene.md), [Scene Settings](T-D03-scene-settings.md)
- [Play Mode](T-P02-play-mode.md), [Tilemap Editor](T-P03-tilemap-editor.md), [Sprite Editor](T-P08-sprite-editor.md)
- [GBA hardware guide: where the editor checks these limits](../gba-hardware-guide.md#where-the-editor-checks-these-limits)
