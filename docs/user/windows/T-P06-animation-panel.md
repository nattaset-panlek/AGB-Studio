# Animation panel

Figma: T-P06 · Page

> แผงล่างเมื่อกดแท็บ Animation: เลือก animation ของ sprite ที่เลือกอยู่ ปรับความเร็วและลำดับเฟรมได้โดยไม่ต้องเปิด Sprite Editor

The bottom panel when you click the Animation tab: pick an animation of the selected sprite, and adjust its speed and
frame order without opening the Sprite Editor.

## Opens from

- The Animation tab of the bottom panel (#22)

## What is on it

- **Animation list** (left): the animations of the selected actor's sprite, each with its frame count and "loop" when
  it loops, for example "idle 8f · loop". The animation the actor uses is selected at first. Click one to show it.
- **Play / Pause**: plays the animation at the game's speed (59.73 frames per second), and pauses it.
- **Step**: shows the next frame. Animations that do not loop stop at the last frame.
- **Info line**: the sprite file, the animation and its speed, for example "hero.png · idle · 6 ticks ≈ 10 fps". A
  tick is one game frame. Turn the mouse wheel over this line (or press + or - while the panel has the focus) to show
  each frame one tick longer or shorter, from 1 to 60 ticks.
- **Onion skin**: draws the previous frame faintly under the current one.
- **Frames**: one picture per frame with its number and how many ticks it shows, for example "3 · 6t". Click a frame
  to show it. Turn the mouse wheel over a frame to change the ticks of that frame only. Drag a frame to another place
  (or press Ctrl + Left / Right) to change the frame order.
- **Saving**: speed and frame order changes are saved to the sprite at once and show in the Sprite Editor too. Each
  change is one undo step.
- The wheel, drag and keys above are listed in Keyboard Shortcuts (group "Animation panel") and found by the search
  in Preferences › Shortcuts. They are fixed: they cannot be given other keys.
- When no actor with a sprite is selected the panel says "Select an actor with a sprite".

## Related

- [Scene Editor](T-P01-scene-editor.md)
- [Sprite Editor](T-P08-sprite-editor.md)
- [Play Mode](T-P02-play-mode.md)
- [Keyboard Shortcuts](T-D10-keyboard-shortcuts.md)
- [GBA hardware guide: sprites](../gba-hardware-guide.md#sprites)
- [GBA hardware guide: the frame](../gba-hardware-guide.md#the-frame)
