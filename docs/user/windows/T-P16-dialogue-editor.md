# Dialogue Editor

Figma: T-P16 · Page

> เขียนบทสนทนาเป็นลำดับบรรทัด มีผู้พูด รูปหน้า ตัวเลือกแตกเส้นทาง และคำสั่ง (เช่น ไปฉากถัดไป / ตั้ง flag) พรีวิวในกล่องข้อความของ GBA ได้ทันที

Writes conversations as a list of lines, each with a speaker and a portrait, plus choices that branch and commands
(such as going to the next scene or setting a flag), with an instant preview in the GBA text box.

## Opens from

- Tools › Dialogue Editor
- Create › Dialogue (opens the Dialogue Editor with a new conversation)
- Double-clicking a `.dlg` file in the Assets panel
- A dialogue entry under "Used by" in the Database

## What is on it

- **Toolbar**: Select, **+** (opens the Add to Dialogue menu: Line, Choice, Command, Condition, Paste) and Condition
  (opens "Show if" for the selected line). The language chip, for example "ไทย / English", swaps which language is
  shown first on the cards.
- **Conversations** (left): every conversation of the project's `dialogue/*.dlg` files with its line count. Click one
  to show it. The **+** button makes a new dialogue file with one empty conversation.
- **Speakers** (left): the speakers of the open file with their portrait. The **+** button opens New Speaker.
- **Line cards** (centre): one card per line with the portrait, speaker and the text in both languages. Click a card
  to select it. Double-click a text to edit it; Enter keeps it. Texts are stored in Localization under the line's key.
- **Choice cards**: the choices under a line, each with its target (`Scene.Load("Cave_01")`, `goto line 2` or
  `end`). Click the target to pick another line, a scene, or end.
- **Command cards**: commands such as Go to scene, Set flag, Give item, Play sound, Wait and Shake camera.
- **Add line · choice · command**: the box under the cards opens the same Add to Dialogue menu.
- **Edit menu**: Cut, Copy, Paste, Duplicate and Delete work on the selected line.
- **Line panel** (right): a GBA preview of the selected line drawn as the ROM will show it, with the choices under the
  text. **Line N** has Speaker, Portrait, Text speed (characters per frame) and Blip sound. **Conditions** has "Show
  if" (when the line appears) and "Then set" (what changes after it). A selected choice or command shows its own
  fields instead.
- **Status bar**: the conversation with its line and choice counts, and "Saved" when there is nothing to save. Every
  edit can be undone.

The Pause and Step buttons in the toolbar act on a running game from this page too: Pause pauses and resumes it, Step
moves it one frame (F10). They are greyed out while no game runs.

## Related

- [New Speaker](T-D21-new-speaker.md)
- [Localization](T-P14-localization.md)
- [Font Manager](T-P09-font-manager.md)
- [World Map](T-P13-world-map.md)
- [Database](T-P07-database.md)
- [Audio](T-P15-audio.md)
