# Third-party components

| Component | Licence | How it is used | Ships in |
|---|---|---|---|
| mGBA libretro core | MPL-2.0 (`third_party/mgba/LICENSE-mgba.txt`) | loaded unmodified as a DLL by the editor and test runner | editor |
| libtonc (Tonc library) | MIT | linked into every game ROM | games |
| maxmod (libmm) | ISC-style (`D:\devkitPro\libgba\maxmod_license.txt`) | linked into every game ROM | games |
| devkitARM / newlib | GPL toolchain with runtime exception; newlib BSD-style | compiler and C library; not redistributed by AGB Studio | user install |
| Roslyn (Microsoft.CodeAnalysis) | MIT | parses, analyses and translates C# scripts | editor |
| Avalonia | MIT | editor UI | editor |
| SkiaSharp | MIT | font rasterising and image I/O in the editor and toolchain | editor |
| Inter | SIL OFL 1.1 | editor UI font | editor |
| JetBrains Mono | SIL OFL 1.1 | editor code font | editor |
| IBM Plex Sans Thai (+ Looped) | SIL OFL 1.1 | editor Thai UI text | editor |
| Press Start 2P | SIL OFL 1.1 | pixel labels in the editor | editor |
| Nunito | SIL OFL 1.1 | wordmark | editor |
| Noto Sans Thai | SIL OFL 1.1 | default Thai game font source | templates |
| ICU Thai dictionary (thaidict.txt) | Unicode licence (ICU) | Thai word-break list at build time | toolchain |

Reference only, no code copied: pokeemerald (pret), Butano (zlib), Poryscript.

The build writes `THIRD_PARTY.txt` next to every exported ROM with the libtonc and maxmod notices.
