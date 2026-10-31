# New Speaker

Figma: T-D21 · Dialog

> เพิ่มตัวละครที่พูดในบทสนทนา: ชื่อ (ไทย/อังกฤษ) รูปหน้า สีป้ายชื่อ และเสียงตอนข้อความขึ้น

Adds a character who speaks in the dialogue: the name in Thai and English, a portrait, a name tag colour, and the
sound played while the text appears.

## Opens from

- Dialogue Editor › Speakers +

## What is on it

- **Name · EN** and **Name · TH**: the speaker's name in English and Thai. The English name is required and must not
  already be a speaker of this dialogue. When it matches a sprite name, that sprite is picked as the portrait.
- **Portrait**: the project's sprites as tiles, with the chosen one first. Click a tile to use it. The dashed **+**
  tile asks for a PNG file, copies it into `assets/sprites` and uses it.
- **Name tag colour**: four colours (violet, yellow, green, pink). Click one for the speaker's name tag.
- **Blip sound**: the sound played as each character of text appears. A sound starting with `blip_low` is picked
  first when there is one.
- **Add Speaker**: adds the speaker to the open dialogue file, with both names in Localization. Undo removes it.
- **Cancel**: closes without adding anything.

## Related

- [Dialogue Editor](T-P16-dialogue-editor.md)
- [Sprite Editor](T-P08-sprite-editor.md)
- [Localization](T-P14-localization.md)
- [Audio](T-P15-audio.md)
