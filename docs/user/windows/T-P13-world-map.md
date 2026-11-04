# World Map

Figma: T-P13 · Page

> เห็นทุกฉากของเกมเป็นการ์ด ลากเส้นเชื่อมทางไปฉากถัดไปได้ (ประตู / trigger / จบด่าน) ใช้วางโครงด่านและลำดับเรื่อง

See every scene of your game as a card and draw links to the next scene (a door, a trigger, the end of a level). Use
it to plan the levels and the order of the story.

## Opens from

- Scene › World Map (Ctrl+M)

## What is on it

- **Toolbar**: Select, Hand (drag to move the view) and Link (drag from one card to another to link them) tools, and
  New Scene. Play (F5) runs the game; Pause pauses and resumes a running game and Step moves it one frame (F10); both
  are greyed out while no game runs. The "5 scenes · 6 links" chip fits every card into view. Undo and Redo are on the
  right.
- **Scenes**: every scene with its size in tiles, or "menu" / "boss". Click to select, double-click to open it in the
  Scene Editor. The + button opens New Scene.
- **Links from (scene)**: the links of the selected scene, such as "Goal Flag → Forest_02", with their kind: on touch
  (the player walks into the trigger), door (the player stands in it and presses Up) or respawn (the player falls into
  it, or loses the last heart in this scene). Click one to open Link Settings.
- **Map**: each scene is a card with a picture, its name and size. Drag a card to move it; its place is saved. Lines
  join linked scenes, and each line has a chip (the trigger, or a condition such as "3 stars"); click the chip for Link
  Settings. Title menu starts are dashed. The mouse wheel zooms; the middle button or the Hand tool moves the view.
- **New scene card**: the dashed card opens New Scene, and the new scene is placed there.
- **Card shortcuts**: double-click or Enter opens the scene, F2 renames it (Enter keeps, Esc cancels). Edit › Duplicate
  and Delete work on the selected scene. Right-click a card for the Scene Context Menu (Open, Rename, Duplicate, Set as
  start scene, Delete).
- **Scene panel**: for the selected scene:
  - **Size**: width × height in tiles; type a new size and press Enter.
  - **Video mode**: the GBA video mode of the scene: Mode 0 (4 backgrounds), Mode 1 (3), Mode 2 (2) or Mode 3
    (bitmap).
  - **Start scene**: turn on to make this the scene the game starts with.
  - **Backgrounds**: a parallax slider for each background layer (how fast it scrolls with the camera).
  - **Music**: the track (or None) and its volume.
  - **Events**: the OnEnter and OnExit code run when the scene starts and ends.
  - **Connections**: scenes joined to this one edge to edge. **Connect** picks a side and a scene; **Offset** is where
    the other scene starts along that edge, in 16 px tiles; pick **Remove** on a row to take the join away. The next
    scene shows past the edge and the player walks across with no load and no fade. Naming the join in one of the two
    scenes is enough. Both scenes need the same tileset, the same layers and the same Hud, tile layers with parallax 1,
    and no Text Label or Panel actors, Menu or Page; the build says what to change. Other actors start anew at the
    crossing, as after any scene change, so keep them more than a screen from the edge or they appear on screen. The
    editor's Scene and Tilemap views show one scene at a time; to see a join, press Play.
- **Status line**: Ready, the start scene (click to select it) and the zoom level (click to go back to 100%).

Every change is saved to the scene files at once and can be undone.

## Related

- [Link Settings](T-O09-link-settings.md)
- [New Scene](T-D02-new-scene.md)
- [Scene Editor](T-P01-scene-editor.md)
- [Scene Settings](T-D03-scene-settings.md)
- [Video Mode](T-O02-video-mode.md)
- [Audio](T-P15-audio.md)
- [GBA hardware guide: video modes](../gba-hardware-guide.md#video-modes)
- [GBA hardware guide: the frame](../gba-hardware-guide.md#the-frame)
