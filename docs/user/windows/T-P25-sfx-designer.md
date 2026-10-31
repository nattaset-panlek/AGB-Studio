# SFX Designer

Figma: T-P25 · Page

> สร้างเสียงเอฟเฟกต์จาก channel ของเครื่อง GBA โดยตรง (Square / Wave / Noise) ปรับ duty, envelope, sweep แล้วฟังทันที ไม่กิน ROM เพราะเล่นจากค่ารีจิสเตอร์ หรือ bake เป็น .wav ก็ได้

Makes sound effects straight from the GBA's own sound channels (Square, Wave, Noise). Adjust duty, envelope and sweep
and listen at once. Such effects take no ROM because they play from register values, or you can bake them to `.wav`.

## Opens from

- Audio › Sound effects +
- Double-clicking a sound effect in Audio that was made in the SFX Designer

## What is on it

- **Toolbar**: Select and Pencil tools (Pencil draws the Wave channel's samples in the waveform box). The chip shows
  the channel, for example "GBA DMG sound · Square 1".
- **Sound effects** (left): the project's `.sfx` files with their channel. Click one to open it. The **+** button
  makes a new effect (`sfx_1.sfx`, `sfx_2.sfx` and so on).
- **Presets** (left): Jump, Coin, Hurt, Laser, Explosion, Power-up, Select, Step. Click one to load its settings; the
  matching preset is highlighted.
- **Channel**: Square 1 (sweep), Square 2, Wave or Noise.
- **Play** and **Loop**: play the effect once, or keep playing it while you change settings.
- **Waveform**: the square wave at the chosen duty, the 32 samples of the Wave channel, or the noise pattern.
- **Pitch and volume over time**: a graph of how the pitch and the volume change while the effect plays.
- **Tone**: Duty (12.5%, 25%, 50%, 75%), Start note and Length in seconds.
- **Envelope and sweep**: Volume (0-15), Fade down / Fade up (click the label to switch direction; not for Wave),
  Sweep time and Sweep up / Sweep down (only for Square 1).
- **How it plays** (right): Hardware (DMG) uses no ROM and one sound channel while playing. Bake to `.wav` also
  writes a sample that mixes with the music; the note shows its size.
- **Use in a script**: the C# line that plays the effect, for example `Sound.Play("jump");`.
- **Registers**: the exact sound register values the effect writes.
- **Status bar**: file name, length and ROM use, and "Saved" when there is nothing to save. Every change can be
  undone; dragging a slider counts as one step.

The Pause and Step buttons in the toolbar act on a running game from this page too: Pause pauses and resumes it, Step
moves it one frame (F10). They are greyed out while no game runs.

## Related

- [Audio](T-P15-audio.md)
- [Script Editor](T-P04-script-editor.md)
- [Scripting API](T-P18-scripting-api.md)
- [GBA hardware guide: Sound](../gba-hardware-guide.md#sound)
