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

## The default font: Manop Thai 6x14

New projects and templates start with `thai_vwf`, made from Manop Thai 6x14 (`fonts/thai6x14.bdf`): a public domain 1bpp
font with printable ASCII and the whole Thai block, easy to read at the GBA's resolution.

- Every letter is 6 px wide, so a 224 px dialogue box holds about 37 characters, and the line is 16 px, so three lines fit
  in a 56 px box with 4 px of padding.
- The font places its own marks, so its `.font` file turns off Stack upper marks and Place lower vowels.
- It can be shipped in your games with no notice. It is also listed under BDF in New Font.

## A second font for emphasis

Text between `<b>` and `</b>` in a string is drawn with another font. Set `emphasisFont` in the main `.font` file to the
name of the other `.font` (both need the same Bits per pixel). Only the characters used this way go into the ROM. Lines
keep the height of the main font. `<b>` works with `<c1>`..`<c3>` colours and `{0}`..`{3}` values.

`Hello, <b>this part</b> uses the emphasis font.`

## AGB Pixel

AGB Pixel (`agb_pixel.bdf`) is AGB Studio's own proportional 1bpp font: Thai letters 4 to 7 px wide, Latin 1 to 5 px, so a
line holds more text, at the cost of some Thai letters looking alike. Pick it under BDF in New Font when space matters more.

## See also

- [Font Manager](../windows/T-P09-font-manager.md), [Add Characters](../windows/T-D16-add-characters.md),
  [Localization](../windows/T-P14-localization.md)
