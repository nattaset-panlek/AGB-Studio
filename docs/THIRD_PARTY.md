# Third-party components

| Component | Licence | How it is used | Ships in |
|---|---|---|---|
| mGBA libretro core | MPL-2.0 (`third_party/mgba/LICENSE-mgba.txt`) | loaded unmodified as a DLL by the editor and test runner | editor |
| libtonc (Tonc library) | MIT | linked into every game ROM | games |
| maxmod (libmm) | ISC-style (`D:\devkitPro\libgba\maxmod_license.txt`) | linked into every game ROM | games |
| devkitARM / newlib | GPL toolchain with runtime exception; newlib BSD-style | compiler and C library; not redistributed by AGB Studio | user install |
| Roslyn (Microsoft.CodeAnalysis 4.14, incl. its localized resource folders `cs` ... `zh-Hant`) | MIT | parses, analyses and translates C# scripts | editor |
| System.Composition 9.0 | MIT | dependency of Roslyn Workspaces | editor |
| Humanizer (Humanizer.Core 2.14.1) | MIT | dependency of Roslyn Workspaces | editor |
| NLayer 3.0.0 | MIT | decodes MP3 files when they are imported (`AudioConverter`) | editor |
| NVorbis 0.10.5 | MIT | decodes OGG files when they are imported (`AudioConverter`) | editor |
| Avalonia 11.3 (incl. Avalonia.DesignerSupport) | MIT | editor UI | editor |
| MicroCom.Runtime 0.11 | MIT | COM interop used by Avalonia on Windows | editor |
| ANGLE (`av_libglesv2.dll`, package Avalonia.Angle.Windows.Natives) | BSD 3-clause (licence file in the package) | OpenGL ES on Direct3D for Avalonia rendering | editor |
| SkiaSharp 3.119 | MIT | Avalonia's 2D renderer in the editor (`Program.cs` `.UseSkia()`); the toolchain does not use it (PNG through its own `Png.cs`, fonts rasterised by its own `Fonts/` code) | editor |
| Skia (`libSkiaSharp.dll`) | BSD 3-clause; bundled parts (etc1, gif and others) listed in the package's THIRD-PARTY-NOTICES.txt | native 2D graphics behind SkiaSharp | editor |
| HarfBuzzSharp 8.3 | MIT | text shaping binding used by Avalonia | editor |
| HarfBuzz (`libHarfBuzzSharp.dll`) | "Old MIT" | native text shaping | editor |
| .NET runtime 10.0 (self-contained: `coreclr.dll`, `hostfxr.dll`, `System.*.dll`, `createdump.exe`, ...) | MIT; notices in the runtime pack's THIRD-PARTY-NOTICES.TXT | runs the editor | editor |
| MsQuic (`msquic.dll`) | MIT | part of the .NET runtime pack (System.Net.Quic); not used by AGB Studio | editor |
| Microsoft.DiaSymReader.Native (`Microsoft.DiaSymReader.Native.amd64.dll`) | ships in the .NET runtime pack, whose package licence is MIT; the DLL's own licence terms were not checked separately | part of the .NET runtime pack (symbol reading); not used by AGB Studio directly | editor |
| Inter | SIL OFL 1.1 | editor UI font | editor |
| JetBrains Mono | SIL OFL 1.1 | editor code font | editor |
| IBM Plex Sans Thai (+ Looped) | SIL OFL 1.1 | editor Thai UI text | editor |
| Press Start 2P | SIL OFL 1.1 | pixel labels in the editor | editor |
| Nunito | SIL OFL 1.1 | wordmark | editor |
| Noto Sans Thai | SIL OFL 1.1 | Thai TrueType font offered by New Font; source of the Sky Hopper sample's `thai_vwf` | editor; Sky Hopper sample |
| Noto Sans (`third_party/fonts/fallback-latin.ttf`) | SIL OFL 1.1 (`OFL-NotoSans.txt`) | Latin glyphs for fonts that lack them; source font of the boot splash text built into every game with Boot splash on (`Content/SplashArt.cs`); copied into Sky Hopper as `NotoSans.ttf` for its `pixel8` and `title_big` fonts | toolchain; its rasterised glyphs in games |
| Ninja | Apache-2.0 | runs the native build when it is on PATH; otherwise AGB Studio runs the same build itself | not shipped |
| Manop Thai 6x14 (`third_party/fonts/thai6x14.bdf`) | public domain (`LICENSE-thai6x14.txt`) | default game font of new projects and templates (`thai_vwf`), offered under BDF in New Font | editor; games that use it |
| ICU Thai dictionary (thaidict.txt) | Unicode licence (ICU) | Thai word-break list at build time | toolchain |

Not third-party: **AGB Pixel** (`third_party/fonts/agb_pixel.bdf`), offered under BDF in New Font, is the project's own font. It is an original 1bpp Thai and Latin pixel font drawn for AGB Studio, not
traced or copied from any other font, and is covered by AGB Studio's licence (`LICENSE`). Its source is the text art in
`tools/AgbStudio.Build/Fonts/AgbPixelFont.Glyphs.cs`; `agb pixel-font` writes the BDF from it.

Reference only, no code copied: pokeemerald (pret), Butano (zlib), Poryscript.

The build writes `THIRD_PARTY.txt` next to every ROM with the libtonc and maxmod notices, and Export ROM copies it
next to the exported ROM. It starts with the MIT notice of the AGB Studio runtime itself (D-05, `GameBuilder`
`WriteNotices`); `docs/user/licence-for-your-games.md` tells users what the file is for.

AGB Studio's own code is under the MIT licence (`LICENSE` at the repository root, D-05).

Package check (P16.02, 2026-10-31): every DLL in `bin/` of `build/package/AGB-Studio-1.0.0-win-x64` (227 DLLs, package
built from commit `dec626c`) maps to a row above or to AGB Studio's own assemblies (`AGBStudio.dll`,
`AgbStudio.*.dll`). Licences were read from the NuGet package metadata (`.nuspec` licence expression) and the licence
and notice files in the local NuGet cache. Fonts are embedded in `AGBStudio.dll`. A package built from a newer commit
was not checked.
