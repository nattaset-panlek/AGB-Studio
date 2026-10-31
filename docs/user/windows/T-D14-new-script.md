# New Script

Figma: T-D14 · Dialog

> สร้างสคริปต์ C# ใหม่จากแม่แบบ ระบบใส่โครงที่ถูกต้องสำหรับ GBA มาให้ และติดเข้ากับ actor ได้ทันที

Creates a new C# script from a template. AGB Studio fills in the right starting code for the GBA and can attach the
script to an actor straight away.

## Opens from

- Script Editor › Scripts +
- Component Picker › New C# script…
- Create › C# Script (opens the Script Editor with this dialog on top)

## What is on it

- **Class name**: the name of the script class. It starts as the actor's name plus "Controller", for example
  BatController. The line under it shows the file it will create, for example `scripts/BatController.cs`, or what is
  wrong: an empty name, a name that is not a valid C# class name, or a script that already exists.
- **Template**: pick one card.
  - **Behaviour**: attaches to an actor, with Start(), Update() and OnTouch().
  - **Static helper**: shared functions with no actor, for example MathUtil.
  - **Scene script**: runs when a scene starts and ends and keeps the scene's state.
- **Attach to**: the actor that gets the new behaviour, shown as "Actor · Scene" (for example "Bat · Cave_01"). It
  picks an actor that has no controller yet. Only used with the Behaviour template; it is greyed out for the others.
- **Open in the external editor after creating**: when ticked, the new script also opens in your external editor at the
  class line.
- **Create Script**: creates the file, adds the behaviour to the actor and opens the script in the Script Editor. It is
  greyed out while the class name has a problem.
- **Cancel**: closes without creating anything.

## Related

- [Script Editor](T-P04-script-editor.md)
- [Component Picker](T-O05-component-picker.md)
- [Scripting API](T-P18-scripting-api.md)
- [Preferences · Paths](T-S08-preferences-paths.md)
