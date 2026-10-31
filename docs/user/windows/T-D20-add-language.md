# Add Language

Figma: T-D20 · Dialog

> เพิ่มภาษาใหม่ให้เกม เลือกภาษาต้นทางที่จะใช้แปล และฟอนต์ที่รองรับตัวอักษรของภาษานั้น

Adds a new language to the game: choose the language you translate from and a font that has the characters of the
new language.

## Opens from

- Project Settings › Localization › Add language
- Localization › Languages +

## What is on it

- **Language**: the language to add. Only languages that are not in the project yet are listed.
- **Translate from**: one of the project's languages; its texts are the starting point for the translation.
- **Font**: the font the new language uses. When the font of the "translate from" language has no characters for the
  new language, the first choice is a new font (for example `jp_kana · new font`) and a note says a new font will be
  created. You can also pick any existing font.
- **Mark all N strings as missing**: when on, the new language starts with no texts, so every text shows as missing
  in Localization. When off, the texts of the "translate from" language are copied in.
- **Fall back to &lt;language&gt; until translated**: the game shows the "translate from" text wherever a translation
  is missing.
- **Add Language**: adds the language to the project settings, writes its text file (`text/strings.<code>.json`) and
  the new font if one was chosen. Undo removes them again.
- **Cancel**: closes without adding anything.

## Related

- [Localization](T-P14-localization.md)
- [Project Settings · Localization](T-S05-project-settings-localization.md)
- [Font Manager](T-P09-font-manager.md)
- [New Font](T-D15-new-font.md)
