# Logic Graph

Figma: T-P17 · Page

> เขียน logic แบบต่อกล่องไม่ต้องพิมพ์โค้ด กล่องแยกสีตามหมวด และแปลงเป็น C# ให้ดูข้าง ๆ ได้ตลอด (Graph ⇄ C#) เหมาะกับมือใหม่หรือ logic ง่าย ๆ

Build game logic by connecting boxes (nodes) instead of typing code. Nodes are coloured by category, and the matching
C# is always shown beside the graph (Graph ⇄ C#). Good for beginners and for simple logic.

## Opens from

- Tools › Logic Graph
- Create › Logic Graph (makes a new `.graph` file in the scripts folder and opens it)

## What is on it

- **Toolbar**: Select, Hand (drag to move the view) and Link tools. With any tool you draw wires by dragging from a
  pin; with Link you can also drag from anywhere on one node to another node: the wire leaves the first node's flow
  output (or its first output) and lands on the other node's first free input of the same kind. Dropping it on empty
  space opens Add Node to connect from that node. Play (F5) runs the game; Pause pauses and resumes a running game and
  Step moves it one frame (F10); both are greyed out while no game runs. The "GBA · Mode 0 · 240×160" chip opens
  Video Mode. Undo and Redo are on the right.
- **Graph ⇄ C# in sync**: shows whether the graph and its C# match. It turns to "not in sync" when typed C# cannot be
  read back into nodes; hover it to see why. Click it to save and open the generated script in the Script Editor.
- **Nodes**: the node list by category: Events, Input, Physics, Actor, Flow, Math, Sound, Data & Save, Text & UI.
  Click a category to open or close it. Double-click a node to add it in the middle of the view. Besides the nodes in
  the Figma frame, there are Random Range, Load Game, Save Exists, Camera Follow and Set Tile, and the nodes that wait
(see below). Random Range, Set Tile and
  the save nodes take whole numbers.
- **Search nodes…**: type to narrow the list to the nodes that match; Enter adds the best match in the middle of the
  view.
- **Canvas**: drag nodes to move them; Ctrl-click or Shift-click selects more than one. Drag from a pin to another pin
  to wire them; wires are coloured by the kind of value. Drop a wire on empty space, double-click empty space or
  right-click empty space to open [Add Node](T-O08-add-node.md) at the mouse. Drag empty space to move the view; the mouse wheel zooms.
- **Node menu**: right-click a node for Duplicate, Disable (kept in the file but not compiled), Group into comment
  (Ctrl+G), Collapse, Show generated C# and Delete. Delete also works with the Delete key.
- **Comments**: dashed frames that group nodes. Double-click a comment title to rename it; Enter keeps the name, Esc
  cancels.
- **Zoom bar**: −, the zoom level, +, Fit (shows every node) and ? (shows or hides the tip).
- **Minimap**: the whole graph in small; click it to move the view there.
- **Tip**: a note that every node compiles to plain C#, then to C++ for the GBA. Hidden until ? on the zoom bar shows it.
- **Preview**: the project's scene (the one last open in the Scene Editor, else the start scene) drawn the way the game
  draws it, with its sprite animations playing at game speed. It does not run the graph. It stays on the first frame
  while Preferences › Appearance › Reduce motion is on, and shows a sample picture when the project has no scene.
- **Node inspector**: the selected node's name, category and a short description, then its settings. A setting that
  is wired from another node shows "(wired)" and cannot be typed in. A setting that names something in the project is
  a dropdown: scenes, sounds, music, number fields of Save data, project tags and text keys. On Touch also has "(any)".
  A name the project no longer has stays in the graph, shows a red line under the field and a red box on the node, and
  can be replaced from the dropdown. The field and tag dropdowns end with "+ New field" and "+ New tag": type a name
  and press Enter (Esc cancels) to add it to the project. A new field is an `int` in Save data; undo takes back the
  node's value, and the new name stays in the project.
- **Generated C#**: the C# made from the graph, updated as you edit. You can type in it too; after a short pause the
  graph is rebuilt from your C#.
- **Status line**: Ready, the number of nodes and wires, and "Compiled to C#" (or "C# generation failed · see
  Generated C#", where the code pane then says why and what to fix).
- **Saving**: saving writes the `.graph` file and its `.cs` script. That script opens read-only in the Script Editor.

## Nodes that wait

Wait, Say, Choice, Move, Fade In and Fade Out take time: the rest of the chain goes on after them. Use them to build a
talk without writing C#: On Touch (Tag Player) → Say → Choice, then a chain from each option.

- **Choice** has up to four options, each a text key. Each option has its own output on the node; an empty option is
  not shown in the game, and an option with nothing wired just ends the talk.
- **Move** moves this actor (Self) or the player along a path. Paths can't be drawn yet, so Move can't be used in a
  game in this version.
- **Wait** counts frames: 60 is one second.
- From the first node that waits, the chain runs as a cutscene. A chain with Say, Choice or Move stops the player and
  the pause menu until it ends, and Save during cutscenes keeps it. A chain with only Wait and Fade runs beside the
  game: the player can still move.
- The chain does not start again while it is still running for the same actor, so standing next to someone does not
  start the same talk again and again.
- After a wait, a chain can play sounds and music, set and read save fields, save and load, show text, load a scene,
  shake the camera, branch and do math. It can't move bodies, read buttons or physics, destroy or follow actors, or
  set tiles: put those before the first wait. A node in the wrong place gets a red border, the inspector says why,
  and Build stops with the same words.
- On Update, On Late Update and On Destroy can't wait: they run every frame, or the actor is gone.

## Related

- [Add Node](T-O08-add-node.md)
- [Script Editor](T-P04-script-editor.md)
- [Scripting API](T-P18-scripting-api.md)
- [Video Mode](T-O02-video-mode.md)
- [Play Mode](T-P02-play-mode.md)
