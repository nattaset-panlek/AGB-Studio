# Project Settings · Localization

Figma: T-S05 · Dialog

> แท็บ Localization: ภาษาต้นฉบับ ภาษาที่รองรับและความคืบหน้าการแปล การตัดคำภาษาไทย และฟอนต์ของแต่ละภาษา

The Localization tab: the source language, the languages the game supports and how far each translation is, Thai line
breaking, and the font for each language.

## Opens from

- File › Project Settings…, then the Localization tab in the side bar

## What is on it

- **Languages**: one bar per language showing how many strings have text, for example `326 / 340`. The count comes
  from the project's string tables. A bar is green when the language is complete and yellow when strings are missing.
- **Add language**: opens the Add Language dialog. The new language joins the list when the dialog closes.
- **Font per language**: pick the font each language uses, or `— none —`. The list shows the fonts in your project.
- **Source**: the language you write the game in; other languages are translated from it.
- **Fallback**: the language shown when a string has no translation yet.
- **Thai wrap**: how Thai lines break. `Dictionary · 30k words` breaks between words using a word list (Thai has no
  spaces between words); `Any character` breaks anywhere.
- **Show missing keys as [key] in Debug**: in Debug builds, a missing string shows its key in brackets so you can spot
  it.
- **Fail the build on missing strings**: the build stops if any string is not translated.
- **Footer**: Cancel (or Esc) drops the changes; Save writes them to the project. The strings themselves are edited on
  the Localization page.

## Related

- [Project Settings](T-D08-project-settings.md) (the dialog and its other tabs)
- [Add Language](T-D20-add-language.md)
- [Localization](T-P14-localization.md)
- [Font Manager](T-P09-font-manager.md)
- [Project Settings · General](T-S01-project-settings-general.md) (default language)
