# Play Mode

Figma: T-P02 · Page

> กด Play แล้วเกมรันใน emulator (mGBA core) ในแท็บ Game: เห็น hitbox, ความเร็ว, FPS และค่าตัวแปรสดใน Inspector · Console แสดง log จากสคริปต์

Press Play and your game runs in the built-in emulator (the mGBA core) in the Game tab. You can see hitboxes, speed,
FPS and live values in the Inspector, and the Console shows the log from your scripts.

## Opens from

- Play button in the title bar (#11)
- Game tab in the document tabs (#15)
- Build › Play (F5)
- Build › Play from Here (Shift+F5): starts the game in the scene the Scene Editor shows
- Build › Build & Run in Emulator (Ctrl+R)
- Build Report › Run in mGBA: runs the last built ROM without building again

## What is on it

- **Building first**: Play, Play from Here and Build & Run first make a Debug build of the project. If the build fails,
  a message says "Build failed · N errors. The Console shows what to fix; then press Play again." and the errors are
  listed in the Console. If the emulator cannot run the ROM, a message says "Could not run <file>. The Console says
  why and what to do."
- **Game view**: the GBA screen (240 x 160) scaled up pixel for pixel, at the size set in Preferences › Emulator.
  Top left shows the frame rate and how much VBlank time is free; bottom right shows the selected actor and its state.
  Click an actor in the game to show it in the Inspector.
- **Playing the game**: the GBA buttons use the keys set in Project Settings › Input, or a gamepad. Hold the
  fast-forward key (Tab by default, 4x) to speed up.
- **Toolbar, left**: only the Select tool works while the game runs. **Hitboxes** draws each actor's collision box;
  **Tile collision** shows the solid tiles.
- **Toolbar, centre**: **Stop** ends the game, keeps its save file and goes back to the Scene Editor. **Pause**
  pauses and resumes. **Step** (F10) pauses and runs exactly one frame. "Simulating · mGBA" shows while the game runs.
- **Toolbar, right**: the frame number, and the speed chip, which opens the Play Speed menu (1/4x, 1/2x, 1x, 2x, 4x,
  Frame step).
- **Hierarchy**: the actors of the scene, with search and the add (+) menu.
- **Inspector · Live**: the selected actor read from the running game every frame: Transform (position, velocity),
  Sprite, Platformer Body (gravity, jump force, max speed, on ground) and your script's fields. Changing a value writes
  it into the running game at once, with no rebuild.
- **GBA Budget**: meters for VRAM BG, VRAM OBJ, OAM, palettes and EWRAM. Click the heading to open Budget Details.
- **Bottom tabs**: Assets, Console (build results, engine messages and your Debug.Log lines, with a line count),
  Animation, and Database (opens the Database page).
- **Document tabs**: Scene, Game and your open files. Clicking a map or script tab opens its editor.
- **Status bar**: Running / Paused / Building / Build failed, "Hot reload on" (when hot reload is on in Project
  Settings › Scripting), frames per second and CPU use, and "mGBA core", which opens Preferences.
- **Hot reload**: while the game runs, saving a file rebuilds the game and it carries on in the new ROM in the same
  scene.
- **Save file**: the game's cartridge save is kept between runs, so saves survive Stop and the next Play.

Differs from Figma: in the Figma prototype, clicking Scene in the title bar menus goes back to the Scene Editor; in
AGB Studio it opens the Scene menu, and the Scene document tab goes back to the Scene Editor (Stop goes back too).

## Related

- [Scene Editor](T-P01-scene-editor.md)
- [Debugger](T-P19-debugger.md)
- [Console panel](T-P05-console-panel.md)
- [Budget Details](T-O06-budget-details.md)
- [Build Report](T-P11-build-report.md)
- [Preferences](T-D09-preferences.md) (Emulator tab)
- [Project Settings · Scripting](T-S04-project-settings-scripting.md)
- [GBA hardware guide: the frame](../gba-hardware-guide.md#the-frame), [input](../gba-hardware-guide.md#input),
  [memory](../gba-hardware-guide.md#memory)
