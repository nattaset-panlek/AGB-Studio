# Link Settings

Figma: T-O09 · Popover

> ตั้งค่าเส้นทางระหว่างฉาก: ไปฉากไหน เกิดที่จุดไหน ใช้ transition แบบไหน และมีเงื่อนไขหรือไม่

Sets up a link between two scenes: which scene it goes to, where the player appears, which transition it uses and
whether it needs a condition.

## Opens from

- World Map › Link chip (click a link)
- Also: a link in the World Map's "Links from" list, and right after you draw a new link with the Link tool

## What is on it

- **Title**: "Link · " and the link's name, for example "Link · Goal Flag". The close button closes the popover.
- **From**: the actor in this scene that starts the link, shown as "Actor · Scene" (for example
  "Goal Flag · Forest_01"), or "Condition only" for a link with no trigger actor.
- **To scene**: the scene the link goes to.
- **Spawn at**: where the player appears in the new scene: one of its edges, such as "Start (left edge)", or one of its
  actors.
- **Transition**: Cut, Fade or Slide.
- **Duration**: how long the transition takes, from 0 to 1.25 seconds in steps of 0.05; the time is shown beside the
  slider.
- **Only if**: a condition the game must meet before the link works, for example `Game.coins >= 10` (the code font
  draws `>=` as one sign; typing `≥` works too). Press Enter or leave the box to keep it. Leave it empty to always
  allow the link.
- **Choose**: builds a simple condition from a number field of Save data, a comparison and a number, and writes it
  to **Only if**. A condition it cannot show, like `Game.coins > Game.best`, stays as typed, and this row shows "Custom".

Each change is saved to the scene at once and can be undone.

## Related

- [World Map](T-P13-world-map.md)
- [New Scene](T-D02-new-scene.md)
- [Scene Editor](T-P01-scene-editor.md)
- [Project Settings · Save data](T-S02-project-settings-save-data.md)
- [GBA hardware guide: the frame](../gba-hardware-guide.md#the-frame)
