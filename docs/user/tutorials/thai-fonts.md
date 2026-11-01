# Making fonts with Thai

variable width, vowels and tone marks · 10 min · 3 steps

Game fonts are converted to variable-width tiles at build time. Thai upper and lower vowels and tone marks are stacked
from the font's measurements, and Thai lines break at word boundaries from a dictionary.

## 1. Open the Font Manager

Fonts are converted to variable-width tiles. Thai vowels and tone marks stack above and below.

- [ ] Open the Font Manager (Tools › Font Manager)

## 2. Add characters

Paste the text of your game to add exactly the characters it uses.

- [ ] Add characters from text (the Add Characters dialog opens)

## 3. Check the text

Localization shows missing and too long strings.

- [ ] Open Localization (Tools › Localization)

## The default font: AGB Pixel

New projects and templates start with `thai_vwf`, made from AGB Pixel (`fonts/agb_pixel.bdf`): AGB Studio's own 1bpp
pixel font with printable ASCII, the Thai block (consonants, vowels, tone marks, signs, digits) and a few symbols.

- Proportional widths: Thai letters are 4 to 7 px wide and Latin 1 to 5 px, so a line holds more text than a fixed
  8 px cell and narrow letters such as ร, i and l do not leave gaps.
- Latin capitals are 7 px tall (lower case 5, descenders 2), Thai letters 6 px. The line is 16 px: room for a tone mark
  over an upper vowel above the letter and a lower vowel under it, with one empty row between marks so they stay
  apart at one bit per pixel.
- Tone marks over an upper vowel are lifted above it; marks over ป ฝ ฟ ฬ move left of the tall stem with one empty
  column; ญ and ฐ lose their tails over a lower vowel; ฎ and ฏ keep short tails that leave the vowel in place.
- It is AGB Studio's own original font (see `docs/THIRD_PARTY.md`), free to ship in your games like the rest of the
  engine. It can also be picked under BDF in New Font.

## See also

- [Font Manager](../windows/T-P09-font-manager.md), [Add Characters](../windows/T-D16-add-characters.md),
  [Localization](../windows/T-P14-localization.md)
