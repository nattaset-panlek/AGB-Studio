# Localization

Figma: T-P14 · Page

> แปลข้อความทั้งเกม (ไทย / อังกฤษ / ภาษาอื่น) เห็นข้อความที่ยังไม่แปล และพรีวิวว่าข้อความยาวเกินกล่องบน GBA หรือไม่

Translates all the text of the game (Thai, English and other languages), shows which texts are not translated yet,
and previews whether a text is too long for its box on the GBA.

## Opens from

- Tools › Localization

## What is on it

- **Toolbar**: Search (jumps to the search box), Missing only (switches the missing filter on and off) and a progress
  chip for the language you are translating, for example "ไทย 326 / 340". Click the chip to pick another language.
- **Languages** (left): every language of the project with how many texts are translated. Click one to make it the
  language you translate into. The **+** button opens Add Language.
- **Groups** (left): All, then the groups of texts (Dialogue, UI & menus, Items and so on) with their counts, and
  Missing only. Click one to show only those texts.
- **Search keys or text…**: shows only the texts whose key, source text or translation contains what you type.
- **Export .csv**: saves every key with its group, speaker, notes and one column per language, so a translator can
  work outside AGB Studio.
- **Auto-check fit**: when on, the FIT column is checked again while you type. When off, it keeps the last results.
- **String table**: one row per text: key, source text, translation and FIT. FIT says "ok", "long" (does not fit the
  text box) or "missing" (not translated). Click a row to select it, double-click to start typing the translation.
- **String panel** (right): the selected key with its **Speaker** and **Font** lists, the source text and the
  translation in edit boxes, each with a **Box width** meter in pixels, and **Notes** for the translator. Typing
  updates the table and the preview at once. Enter leaves the box. Each change can be undone.
- **Preview on GBA · 240×160**: the text drawn in a game text box as the ROM will show it, with a line saying how many
  lines it takes and how Thai words were split.
- **Status bar**: how many texts are missing in the selected language and the total number of keys.

The Pause and Step buttons in the toolbar act on a running game from this page too: Pause pauses and resumes it, Step
moves it one frame (F10). They are greyed out while no game runs.

## Related

- [Add Language](T-D20-add-language.md)
- [Project Settings · Localization](T-S05-project-settings-localization.md)
- [Font Manager](T-P09-font-manager.md)
- [Dialogue Editor](T-P16-dialogue-editor.md)
- [Database](T-P07-database.md)
