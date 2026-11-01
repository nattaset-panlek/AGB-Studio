# 1bpp bitmap fonts with Latin and Thai: candidates

Research only, date 2026-11-01. Nothing was downloaded or installed. Everything below comes from web pages read through a text fetcher; items marked UNVERIFIED must be checked by opening the actual font file before adoption.

## What the importer accepts

`tools/AgbStudio.Build/Fonts/GameFont.cs` switches on `FontSourceKind`:

- `Ttf` : .ttf or .otf (TrueType glyf or CFF 1 outlines), read by `TrueType.cs` / `Cff.cs`, rasterised at the chosen size and thresholded to 1bpp at coverage 128. No .ttc, WOFF, WOFF2 or variable fonts.
- `Bdf` : read by `Bdf.Read(source, baseline)`. Glyphs are taken as drawn, so a hand-drawn pixel font keeps its exact pixels.
- `Image` : PNG sheet, `ImageFont.Read`.

After loading, `GameFont` reads the heights of U+0E34 and U+0E48 as reference tops for the Thai layout step (`ThaiText.cs`), and marks (`ThaiText.IsMark`) get advance 0. So the Thai marks must exist as separate glyphs in the font, and the layout step does the stacking; a font does not need pre-composed clusters. BDF is therefore the best-fit format for pixel fonts.

## Candidates

### 1. GNU Unifont
- Source: https://unifoundry.com/unifont/index.html
- Licence (read on the official page): dual, GPL v2 or later with the GNU Font Embedding Exception, or SIL OFL 1.1. The page says it can be used with non-free software. Choose the OFL option to avoid GPL questions in a shipped ROM.
- Size / height: 16 px tall, 8 or 16 wide (one fixed size only).
- Thai coverage: covers Plane 0 including the Thai block. The page itself says Unifont holds one glyph per code point, that scripts with floating vowel marks "won't render well", and calls it a font of last resort. The marks are not designed to stack. Mark glyph placement UNVERIFIED.
- Formats: OpenType, BDF and PCF (BMP only), HEX, PSF.
- Fit for 240x160: poor to fair. 16 px lines give 10 lines of text, glyphs are large and plain, and stacking quality is doubtful. Useful as a gap-filling fallback only.

### 2. Thai X bitmap fonts: manop and etlthai (tlwg/thaixfonts)
- Source: https://github.com/tlwg/thaixfonts (directories `manop`, `etlthai`)
- Licence: the repo `COPYING` states that etlthai and manop are public domain (author Manop Wongsaisuwan). The collection as a whole is GPL v2 or later, but each family follows its vendor's terms.
- Sizes: manop `thai6x14.bdf`, `thai8x16.bdf`, `thai7x18.bdf`, `thai8x20.bdf`; etlthai `etl14-thai.bdf`, `etl16-thai.bdf`, `etl24-thai.bdf`. The numbers are cell sizes; exact glyph heights UNVERIFIED. The smallest is 14 px, so no 8 px or 12 px choice.
- Thai coverage: TIS-620 era fonts. These were made for terminals, with marks on separate cells or overlaid; the manop README says BDF properties were barely worked on. Whether the Unicode codepoints U+0E31 and so on are mapped, and whether the marks are zero advance, UNVERIFIED (these are likely encoded TIS-620 or ISO10646 variants; check the BDF `ENCODING` lines).
- Latin: only basic ASCII at best; UNVERIFIED.
- Format: BDF (exactly what the importer reads).
- Fit: fair as a licence-clean BDF. 14 px is tall for 240x160 (11 lines), and the style is dated.

### 3. Thai X bitmap fonts: poonlap, nectec, yenbut (same repo)
- Source: https://github.com/tlwg/thaixfonts
- Licence: poonlap permits copying and distribution for commercial and non-commercial use provided the notice is preserved (custom wording, not a standard licence; get legal review). yenbut: free if the copyright notice is kept. nectec: GPL v2 or later with no font exception stated, which is risky for embedding in a commercial ROM.
- Sizes: poonlap has `pvfixed_8r.bdf` and `pvemacs_8r.bdf` (the "8" probably means an 8 px class, UNVERIFIED), plus 18 and 20 px sets (`thai18_emacs`, `pvfixed_20r`).
- Thai: the "emacs" variants give each vowel and tone mark one cell, which suits a separate-mark glyph model. Stacking quality UNVERIFIED.
- Format: BDF.
- Fit: the 8r BDF is the only possible 8 px Thai candidate found; it needs opening to see whether it is actually 8 px tall and whether it carries Latin and Unicode encodings.

### 4. ThaiThai Font (itch.io, GGBotNet)
- Source: https://ggbot.itch.io/thaithai-font
- Licence: SIL OFL 1.1 (stated on the page).
- Size: outline font, 552 glyphs, Regular and Light; it is not described as a pixel font and has no bitmap sizes.
- Thai plus Latin: yes, both.
- Formats: OTF, TTF, WOFF.
- Fit: only usable through the TTF rasteriser at 1bpp, where anti-aliasing is thresholded, so strokes will be uneven at 8 to 12 px. Not a true pixel font. Same situation as the current default (Noto Sans Thai, OFL).

### 5. Fusion Pixel Font (TakWolf)
- Source: https://github.com/TakWolf/fusion-pixel-font
- Licence: SIL OFL 1.1 for the fonts, MIT for the build program; some upstream fonts have their own licences (mostly OFL 1.1, Misaki is described as OFL-compatible).
- Sizes: 8 px, 10 px and 12 px, monospaced and proportional. Releases include BDF, PCF, OTF, TTF, WOFF2.
- Thai: the README lists Latin, Chinese, Japanese and Korean variants only. The searches found no Thai statement. Treat as NOT covering Thai until a glyph check says otherwise.
- Fit: excellent size match and licence, but likely fails the Thai requirement. Possible as a Latin and kana font in a multi-language project.

### 6. ARMBOX MOON Night (itch.io) and Plain Pixel (itch.io), rejected
- ARMBOX MOON Night is a Thai and English pixel font (https://armbox.itch.io/armbox-moon-night-font), but the free demo is non-commercial only and commercial use needs a paid licence from $15 with custom terms. Possible if the user pays, but it is not open licensed and the exact licence terms were not read.
- Plain Pixel (https://burpyfresh.itch.io/plain-pixel) is CC BY 4.0 (commercial use allowed with attribution), but its current Unicode blocks do not include Thai.

## Result of the search

No font found that is simultaneously an open-licensed true pixel font, 8 or 12 px tall, with verified Latin plus correctly positioned Thai marks. The searches (GitHub topics, itch.io, Debian packages) turned up no OFL Thai pixel font at 8 or 12 px. FontPixel (https://github.com/fontpixel/fontpixel, BDF catalogue with coverage reports) may list one; its Thai coverage report was not reachable and should be checked by a person.

## Recommendation

Primary: a public-domain BDF from `tlwg/thaixfonts` (manop `thai8x16.bdf` or etlthai `etl14-thai.bdf` / `etl16-thai.bdf`), paired with Unifont (OFL option) or another OFL Latin font for glyphs it lacks, because:
- the licence is the cleanest found (public domain per the repo COPYING), with no GPL entanglement;
- BDF is read as drawn by the importer, so pixels are exact and no 1bpp threshold artefacts appear;
- the Thai layout step already stacks marks from separate mark glyphs, so the font only has to supply base letters and marks.

Cost: 14 to 16 px is tall for 240x160, so the fonts suit dialogue boxes (about 10 lines) rather than HUD text. For a smaller 8 to 12 px result, test `poonlap/pvfixed_8r.bdf` first (needs a legal read of its notice), then fall back to the current Noto Sans Thai TTF at 12 px with the 1bpp threshold.

Before committing, open each candidate BDF and check: ENCODING values for U+0E31, U+0E34 to U+0E3A, U+0E47 to U+0E4E; zero DWIDTH on marks; presence of ASCII; and run `agb text-preview <font> <size> <width> <out.png> <text>` (docs/BUILD.md line 49) on a Thai corpus (the check in `ThaiCorpusTests` is the golden reference).
