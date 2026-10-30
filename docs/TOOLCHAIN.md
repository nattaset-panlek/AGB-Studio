# Toolchain

Recorded on 2026-10-31 on the development machine. The build reads `DEVKITPRO` (default `D:\devkitPro`).

## devkitPro (GBA)

| Tool | Version / location | Used for |
|---|---|---|
| devkitARM | arm-none-eabi-gcc 16.1.0 (`D:\devkitPro\devkitARM`) | compile and link the runtime and games |
| gba.specs, gba_cart.ld, gba_crt0.o | `devkitARM/arm-none-eabi/lib` | boot code, memory layout; `*.iwram.*` objects go to IWRAM, `.ewram` section to EWRAM |
| libtonc | `D:\devkitPro\libtonc` (lib/libtonc.a) | hardware registers, BIOS calls, IRQ |
| maxmod | `D:\devkitPro\libgba\lib\libmm.a`, header `libgba/include/maxmod.h` | music and sample playback |
| libgba / libfat | `D:\devkitPro\libgba\lib` | present, **not linked** (the runtime uses libtonc + libmm only) |
| gbafix | `tools/bin/gbafix.exe` | ROM header (title 12, code 4, maker 2, version, checksum) |
| mmutil | `tools/bin/mmutil.exe` | MOD / XM / WAV -> soundbank |
| grit, gbalzss, padbin, bin2s | `tools/bin` | present; AGB Studio has its own converters, padbin is not needed (export pads itself) |

## Emulators

| Emulator | Version / location | Used for |
|---|---|---|
| mGBA libretro core | `third_party/mgba/mgba_libretro.dll`, libretro buildbot nightly 2026-10-31, sha256 `3b5e74404a39b3b8b2a285843a4dcdbb3fc975e92fea8f9dd90bd6b98fb452e0` | headless ROM tests (`agb romtest`), Play Mode, Debugger |
| VBA-M | `D:\devkitPro\visualboyadvance-m.exe` (2025-07-09) | second-emulator compatibility check |

Memory regions exposed by the core: IWRAM 0x03000000 32 KB, EWRAM 0x02000000 256 KB, SRAM 0x0E000000, ROM 0x08000000 (mirrors 0x0A/0x0C), BIOS 0x00000000 16 KB, VRAM 0x06000000 96 KB, palette 0x05000000 1 KB, OAM 0x07000000 1 KB, IO 0x04000000.

## Host tools

| Tool | Version | Used for |
|---|---|---|
| .NET SDK | 10.0.400 | editor, toolchain, scripting compiler, tests |
| Ninja | 1.12.1 | build graph for the runtime and games |
| CMake | 4.4.3 | not needed by the chosen stack |
| git | 2.39.1 | repository, "Initialize git repository" in New Project |
| Node | 22.20.0 | not needed by the chosen stack |
| Python | 3.10.11 | not needed by the chosen stack |

## Hardware

No flash cart or GBxCart RW is attached to this machine. Every `HW` check is `n/a until hardware`.

## Folders that AGB Studio never touches

`D:\devkitPro\GBAStudio.spec`, `D:\devkitPro\build\`, and the devkitPro toolchain folders.
