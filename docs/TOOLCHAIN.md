# Toolchain

## AGB Studio's own toolchain (Phase 24, 2026-11-06)

The build looks for a compiler in this order (`Toolchain.Detect`): Preferences · Paths, then `toolchain\` in the AGB
Studio folder, then `DEVKITPRO`, `C:\devkitPro` and `D:\devkitPro`.

| Part | Version | Where | Used for |
|---|---|---|---|
| Arm GNU Toolchain | 15.2.rel1 (gcc 15.2.1 20251203), zip SHA-256 `7936cac8...0b9ece` | `toolchain\` from `tools\fetch-toolchain.ps1` (230 MB of 1.1 GB: C and C++ for the ARM and `thumb/nofp` libraries) | compile and link |
| Start code, linker script, specs | AGB Studio's own | `runtime/link` | boot, memory layout (P24.02) |
| Registers, BIOS calls, interrupts | AGB Studio's own | `runtime/include/agb/hw.hpp`, `runtime/src/core/irq*.cpp` | replaces libtonc (P24.03) |
| maxmod | 1.0.15, GBA sources | `third_party/maxmod` | assembled with every ROM that links it (P24.03) |
| ROM header | AGB Studio's own | `tools/AgbStudio.Build/GbaHeader.cs` | replaces gbafix (P24.04) |
| mmutil | devkitPro's 1.10.1 | devkitPro `tools\bin` | soundbank; still needed for music (P24.05 open) |

What the trial measured is in `docs/evidence/p24-01-arm-toolchain.md`.

## Before Phase 24

Recorded on 2026-10-31 on the development machine. The build reads `DEVKITPRO` (the devkitPro installer sets it to
`/opt/devkitpro`, its MSYS2 name for `C:\devkitPro`), then tries `C:\devkitPro` and `D:\devkitPro`
(`tools/AgbStudio.Build/Toolchain.cs`); in the editor, Preferences · Paths › devkitPro overrides it.

## devkitPro (GBA)

Package versions are from the devkitPro package database (`D:\devkitPro\msys2\var\lib\pacman\local`).

| Tool / library | Package | Location | Used for |
|---|---|---|---|
| devkitARM | devkitARM r68: arm-none-eabi-gcc 16.1.0, binutils 2.46.0, newlib 4.6.0 | `D:\devkitPro\devkitARM` | compile and link the runtime and games; `objcopy` to a binary ROM; `nm` for the build report |
| gba.specs, gba_cart.ld, gba_crt0.o | devkitarm-crtls 1.2.6 | `devkitARM/arm-none-eabi/lib` | boot code and memory layout; `*.iwram.*` objects go to IWRAM, the `.ewram` section to EWRAM |
| libtonc | libtonc 1.4.5 | `D:\devkitPro\libtonc` (`lib/libtonc.a`) | hardware registers, BIOS calls, IRQ |
| maxmod | maxmod-gba 1.0.15 | `D:\devkitPro\libgba\lib\libmm.a`, header `libgba/include/maxmod.h` | music and sample playback |
| libgba / libfat | libgba 0.5.4 | `D:\devkitPro\libgba\lib` | present, **not linked** (the runtime uses libtonc + libmm only) |
| gbafix | gba-tools 1.2.0 (banner "GBA ROM fixer v1.05") | `tools/bin/gbafix.exe` | ROM header (title 12, game code 4, maker 2, version, checksum) in the "Link + gbafix" step |
| mmutil | mmutil 1.10.1 (banner "Maxmod Utility 1.10.1") | `tools/bin/mmutil.exe` | MOD / XM / WAV to the maxmod soundbank |
| grit | grit 0.10.0 (the binary's banner says "grit v0.9.2") | `tools/bin/grit.exe` | not used: AGB Studio converts graphics itself (`tools/AgbStudio.Build/Graphics`) |
| gbalzss | gba-tools 1.2.0 | `tools/bin/gbalzss.exe` | not used: AGB Studio has its own LZ77 encoder (`Graphics/Lz77.cs`, VRAM-safe) |
| padbin | general-tools 1.4.4 | `tools/bin/padbin.exe` | not used: Export ROM pads to a power of two itself (`ExportRomDialog.PadRom`, 0xFF fill) |
| bin2s | general-tools 1.4.4 (banner "general-tools 1.4.4") | `tools/bin/bin2s.exe` | not used: the build writes assets as C++ arrays |

### Compiler version (P0.01)

Output of `D:\devkitPro\devkitARM\bin\arm-none-eabi-gcc --version`, saved on 2026-10-31:

```
arm-none-eabi-gcc.exe (devkitARM) 16.1.0
Copyright (C) 2026 Free Software Foundation, Inc.
This is free software; see the source for copying conditions.  There is NO
warranty; not even for MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
```

### Each tool runs (P0.04)

Run on 2026-10-31 from `D:\devkitPro\tools\bin` in an empty folder; first line of output and exit code:

| Command | Exit code | First line of output |
|---|---|---|
| `gbafix` | -1 (no ROM given; prints its usage) | `GBA ROM fixer v1.05 by Dark Fader / BlackThunder / WinterMute / Diegoisawesome` |
| `grit` | 0 | `---grit v0.9.2 ---` |
| `mmutil` | 0 | `* Maxmod Utility 1.10.1 *` |
| `gbalzss --help` | 0 | `Usage: gbalzss.exe [-h\|--help] [--lz11] [--vram] <d\|e> <infile> <outfile>` |
| `padbin` | 1 (no arguments; prints its usage) | `pads a binary file to an integer multiple of a given number of bytes` |
| `bin2s --help` | 0 | `general-tools 1.4.4` |

gbafix also runs whenever a game or runtime test ROM is linked (`NativeBuild.cs`). mmutil runs in a game build only
when the project has MOD / XM / WAV files and its soundbank cache is out of date (`GameBuilder` `ConvertAudio`,
`SoundbankBuilder.cs`), and in the runtime test ROM builds only for tests with an `audio/` folder (`RuntimeTests.cs`).
gbalzss and padbin have no version flag; their version is the package version above.

## Emulators

| Emulator | Version / location | Used for |
|---|---|---|
| mGBA libretro core | `third_party/mgba/mgba_libretro.dll`, libretro buildbot nightly 2026-10-31, sha256 `3b5e74404a39b3b8b2a285843a4dcdbb3fc975e92fea8f9dd90bd6b98fb452e0` | headless ROM tests (`agb romtest`), Play Mode, Debugger |
| VBA-M | `D:\devkitPro\visualboyadvance-m.exe` (2.2.0, file dated 2025-07-09) | second-emulator compatibility check |

Memory regions exposed by the core: IWRAM 0x03000000 32 KB, EWRAM 0x02000000 256 KB, SRAM 0x0E000000, ROM 0x08000000
(mirrors 0x0A/0x0C), BIOS 0x00000000 16 KB, VRAM 0x06000000 96 KB, palette 0x05000000 1 KB, OAM 0x07000000 1 KB, IO
0x04000000.

## Host tools

| Tool | Version | Used for |
|---|---|---|
| .NET SDK | 10.0.400 | editor, toolchain, scripting compiler, tests |
| Ninja | 1.12.1 | optional: runs `build.ninja` when it is on `PATH`; without it the build runs the same graph in-process |
| CMake | 4.4.3 | not needed by the chosen stack |
| git | 2.39.1 | repository, "Initialize git repository" in New Project, `tools/check-commit-*.ps1` in `ci.cmd`, `tools/ci-artifacts.ps1` (the commit in the run folder name and `summary.txt`) |
| Windows PowerShell | 5.1 | the commit-message checks in `ci.cmd` and `tools\ci-artifacts.ps1`, which `ci.cmd` runs after every pass or failure |
| Node | 22.20.0 | only for the read-only Figma export script of the SHOT tests (`figma-export.mjs`); not needed to build |
| Python | 3.10.11 | not needed by the chosen stack |

`AGB_BUILD_JOBS` limits the number of parallel compiles (Ninja `-j` or the in-process build) on machines with little
memory.

## Hardware

No flash cart or GBxCart RW is attached to this machine. Every `HW` check is `n/a until hardware`.

## Folders that AGB Studio never touches

`D:\devkitPro\GBAStudio.spec`, `D:\devkitPro\build\`, and the devkitPro toolchain folders. The listing with timestamps
taken to check this (P0.10) is in [evidence/p0.10-untouched-folders.md](evidence/p0.10-untouched-folders.md).
