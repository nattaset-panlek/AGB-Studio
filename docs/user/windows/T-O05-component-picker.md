# Component Picker

Figma: T-O05 · Popover

> เพิ่มความสามารถให้ actor (Add Component) แบ่งหมวด มีคำอธิบายสั้น ๆ และสร้างสคริปต์ C# ใหม่ได้จากตรงนี้

Add a behaviour to an actor (Add Component). Behaviours are grouped by category with a short description, and you can
start a new C# script from here.

## Opens from

- Add Component at the bottom of the Inspector (#32)
- Actor Context Menu › Add Behaviour…

## What is on it

- **Title bar**: "Add behaviour", with a close button.
- **Search behaviours…**: filters by name or description. The search box has the keyboard when the popover opens.
- **Behaviour list**: grouped under category headings, each row with a short description on the right.
  - **Physics**: Platformer Body, Top-down Mover, Projectile.
  - **Combat**: Health, Damage on Touch.
  - **AI**: Patrol, Chase Player.
  - **Audio**: Sound on Event.
  - **Scripts**: the C# behaviours of your own project, after they have been compiled once.
- **Pick a behaviour**: click a row, or move with Up / Down and press Enter. The behaviour is added to the selected
  actor (this can be undone) and appears in the Inspector with its fields.
- **New C# script…**: closes the popover and opens the New Script dialog.

## Related

- [Scene Editor](T-P01-scene-editor.md)
- [New Script](T-D14-new-script.md)
- [Script Editor](T-P04-script-editor.md)
- [Logic Graph](T-P17-logic-graph.md)
- [Scripting API](T-P18-scripting-api.md)
