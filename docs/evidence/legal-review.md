# G-LEGAL.02, G-LEGAL.05, G-LEGAL.06 review (2026-10-31)

Snapshot: branch `fix-docs` at `24a26b6` (main), 1,714 tracked files (`git ls-files`), plus the package folder
`D:\devkitPro\AGB-Studio\build\package\AGB-Studio-1.0.0-win-x64` (built from `dec626c`). Read-only review: nothing was
changed in the code. Each section says what was checked, how, the result, and what was not checked.

## G-LEGAL.02 No Nintendo assets, BIOS files or commercial ROMs in the repo, tests or installer

Checks:

| Check | How | Result |
|---|---|---|
| ROM, save, BIOS and archive files tracked in git | `git ls-files` filtered for `.gba .gb .gbc .agb .mb .bin .sav .srm .rom .zip .7z .rar .ips .ups .bps` and names containing `bios`, `nintendo`, `pokemon`, `emerald` | none; the only `bios` names are the runtime's BIOS-call wrappers `runtime/include/agb/bios.hpp` and `runtime/src/core/bios.cpp` (code that calls the BIOS, no BIOS data) |
| File types in the repository | `git ls-files` grouped by extension | `.png` 951, `.cs` 394, `.svg` 112, `.md` 88, `.cpp` 57, `.hpp` 41, `.ttf` 19, `.txt` 14, `.csproj` 12, `.axaml` 9, `.json` 5, and one each of `.dll`, `.mjs`, `.wav`, `.dlg`, `.graph`, `.mod`, `.cmd`, `.props`, `.sln`, `.def`, `.manifest`, `.gitignore` |
| The only binary library | `third_party/mgba/mgba_libretro.dll` | the mGBA libretro core, MPL-2.0, unmodified (D-06, `third_party/mgba/LICENSE-mgba.txt`) |
| Nintendo logo bitmap (the 156-byte logo every GBA header carries) | byte search of every tracked file for its first six bytes `24 FF AE 51 69 9A`, and a text search for them as hex | not found. Built ROMs get the logo from `gbafix` at build time; ROMs are written only under `build/`, which git ignores |
| Paths to outside ROMs or BIOS images in code and tests | search for `gba_bios`, `bios.bin`, `GBA-Research`, absolute `*.gba` paths in `tools/`, `editor/`, `runtime/`, `tests/` | none. Every test ROM is built from `runtime/tests/*` or from generated projects |
| BIOS given to the emulator | `LibretroCore` passes `%TEMP%\agb-studio-emu` as the core's system folder | the folder is empty, so no BIOS image is loaded; mGBA uses its own built-in BIOS replacement |
| Test media | `runtime/tests/audio_basic/audio/beep.wav` and `tune.mod` | written by `agb make-test-audio` (a generated square-wave beep and a generated MOD, `tools/agb/Program.cs`) |
| Installer (portable package) | the package folder listed for `.gba .gb .gbc .bin .sav .srm .rom .zip` and `bios` names | none; only `runtime/include/agb/bios.hpp` and `runtime/src/core/bios.cpp` |

Not checked:

- The 951 PNG files were not opened one by one. They are SHOT evidence (`docs/evidence/app`, `docs/evidence/figma`,
  `docs/evidence/diff`: captures of AGB Studio windows, of the Figma frames and of ROMs built from original content),
  editor icons and art exported from Figma (`editor/AgbStudio.Editor/Assets`), and the golden image
  `tests/AgbStudio.Tests/Golden/thai-corpus.png`. Whether the Figma art itself is original design work is item T-K05.
- A package built from a newer commit than `dec626c` was not checked.

Result: no Nintendo asset, BIOS image or commercial ROM was found in the repository, the tests or the package.

## G-LEGAL.05 Butano and other engines used as reference only, no code copied unless its licence is followed and recorded

Checks, over the 522 code-like tracked files (`.cs .cpp .hpp .h .def .axaml .mjs .cmd .ps1 .json .props .csproj`),
case-sensitive:

| Pattern | Looks for | Hits |
|---|---|---|
| `Butano`, `butano`, `bn::`, `BN_[A-Z]` | Butano names, namespace and macros | 0 |
| `gbstudio` and the other engine's product name spelled with a space (any "Studio" name built on "GB" that is not "AGB Studio") | code from that engine (the lint product-name rule also blocks the name) | 0 |
| `gba-sprite-engine`, `Natu`, `agbabi` | other GBA engines and libraries | 0 |
| `Poryscript`, `Porymap` | pret community tools named in the blueprint | 0 |
| `Copyright`, `SPDX`, `adapted from`, `ported from`, `taken from`, `copied from`, `based on` | copied code that kept a header or an attribution | only the libtonc and maxmod notices the build writes into `THIRD_PARTY.txt` (`GameBuilder.WriteNotices`), the lint comment about pokeemerald names, and unrelated phrases ("exported from Figma", "arguments taken from the caller's stack") |
| pokeemerald identifiers (`gMain`, `gSaveBlock1Ptr`, `VAR_RESULT`, `MUS_`, `SPECIES_`, ...) | covered by `agb lint` (G-LEGAL.01) | the lint passes on this tree (see the lint run of this branch) |

Code that implements well-known public algorithms: the random number generator (`runtime/src/core/rng.cpp`) is
xoshiro128** seeded with splitmix, written in the runtime's own style; the LZ77 encoder (`Graphics/Lz77.cs`), the
TrueType reader (`Fonts/TrueType.cs`) and the Thai segmenter (`Fonts/ThaiText.cs`) are AGB Studio code. Third-party
code and data that is used is linked or loaded, not copied, and recorded in `docs/THIRD_PARTY.md`: libtonc and maxmod
(linked), the mGBA core (loaded as a DLL), the ICU Thai word list (data), fonts.

`docs/THIRD_PARTY.md` records "Reference only, no code copied: pokeemerald (pret), Butano (zlib), Poryscript".

Not checked: a text search cannot prove that no code was copied with its names changed; this review found no sign of
it. The pokeemerald clone at `D:\GBA-Research\pokeemerald` and Butano sources were not compared line by line with the
runtime.

Result: no Butano or other engine code found; references are recorded.

## G-LEGAL.06 Template, sample and tutorial content is original

| Content | Where it comes from | Result |
|---|---|---|
| Template projects (8) | `tools/AgbStudio.Build/Content/Templates/*Template.cs`, `TemplateKit.cs`: sprites, tilesets and images drawn from shapes in code (`PixelArt.cs`), music generated as XM / MOD (`Audio/Chiptune.cs`, `Audio/XmWriter.cs`), sound effects generated, texts written in the code (English and Thai) | original (D-20) |
| Sample projects (Sky Hopper demo, Cave Quest, Snow Run, Dusk Story) | `Content/SkyHopper.cs`, `Content/Samples.cs`: built from the templates, recoloured in code (`Samples.Recolor` reads and rewrites the generated PNGs), Dusk Story's story text written in `Samples.cs` | original; the Scope log row of 2026-10-31 (P15.11) asks the user whether samples built from templates are enough |
| Boot splash | `Content/SplashArt.cs`: the Plus Pad mark and "AGB Studio" drawn in code | original (AGB Studio brand) |
| Fonts inside templates and samples | Noto Sans Thai and Noto Sans copied from `third_party/fonts` with `OFL-NotoSansThai.txt` (TemplateKit copies the licence next to the font) | third-party, SIL OFL 1.1, recorded in `docs/THIRD_PARTY.md`; not original, and allowed |
| Tutorials (5) | `editor/AgbStudio.Editor/Windows/Launcher/TutorialCatalog.cs`: titles and step 3 of "Your first platformer" are the Figma texts, the other steps were written for AGB Studio; step pictures are Figma art (`art/genre/*`) | original text; the art is covered by T-K05 |
| `templates/` and `samples/` folders | do not exist (D-04 note) | nothing stored as files |

Files read from disk by the generators: only the fonts above, the project's own generated PNGs (recolouring) and the
`third_party/fonts` fallback font for the splash text. No downloaded or copied art, music or text is used.

Not checked: the generated pictures and music were not compared by eye or ear with existing games; the review relies
on them being produced by code in this repository.

Result: template, sample and tutorial content is original, apart from the openly licensed fonts.
