# Scene Switcher

Figma: T-O01 · Popover

> กดชื่อฉากบน breadcrumb เพื่อสลับไปฉากอื่นได้เร็ว

Click the scene name in the title bar breadcrumb to jump quickly to another scene.

## Opens from

- The breadcrumb (project / scene name) in the title bar (#8)

## What is on it

- **Title bar**: "Switch scene", with a close button.
- **Find scene…**: type part of a name to filter the list. The search box has the keyboard when the popover opens.
- **Scene list**: every scene of the project in story order. It starts at the start scene, then follows the links
  between scenes and the "load scene" items of menu scenes; scenes that nothing leads to come last, by name.
- **Scene row**: a small preview drawn the way the game draws it, the scene name, and its size in tiles (for example
  "30×20"), or "menu" for a menu scene. The scene that is open now is highlighted.
- **Click a row**: opens that scene in the Scene Editor and closes the popover.
- **New Scene…**: closes the popover and opens the New Scene dialog.

Differs from Figma: Figma shows a "boss" tag on one row; AGB Studio only shows the size in tiles or "menu".

## Related

- [Scene Editor](T-P01-scene-editor.md)
- [New Scene](T-D02-new-scene.md)
- [Scene Settings](T-D03-scene-settings.md)
- [World Map](T-P13-world-map.md)
- [Command Palette](T-O07-command-palette.md)
