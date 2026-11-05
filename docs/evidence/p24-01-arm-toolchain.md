# P24.01 · Arm GNU Toolchain trial

2026-11-06, on `D:\devkitPro\AGB-Studio` (branch `handoff/2026-11-05`, not committed when measured). A trial only:
nothing in the repository builds with this toolchain yet.

## What was used

- Arm GNU Toolchain **15.2.rel1** (gcc 15.2.1 20251203), `arm-gnu-toolchain-15.2.rel1-mingw-w64-x86_64-arm-none-eabi.zip`
  from developer.arm.com; SHA-256 `7936cac895611023ffb22a64b8e426098c7104cb689778c1894572ca840b9ece`, the same as Arm's
  `.sha256asc`. Zip 296 MB, unpacked 1.1 GB (all its multilibs, Fortran and docs; a GBA build needs a small part).
- Compared with devkitARM **16.1.0**, which builds the repository today.
- Still borrowed from devkitPro for the trial: `gba_crt0.o`, `gba_cart.ld`, libtonc, libgba and maxmod (built by
  devkitARM), `gbafix`, `mmutil`. These are what P24.02 to P24.05 replace.

## Does it fit the GBA

- `-mthumb -mcpu=arm7tdmi` picks the `thumb/nofp` libraries and `-marm` the default ones; `readelf -A` on newlib and
  libgcc objects of both says `Tag_CPU_arch: v4T`, `Tag_THUMB_ISA_use: Thumb-1`: they run on the ARM7TDMI.
- All runtime sources compile with no change.

## What broke

1. Link: devkitPro's `gba_crt0.o` needs `fake_heap_end` (from devkitPro's libsysbase). The trial adds a one-line
   stub; the runtime has no heap. Our own start code (P24.02) removes the need.
2. devkitARM's `gba.specs` includes `sync-none.specs`, which Arm's toolchain does not have: left out in the trial specs.
3. newlib's `_read`, `_write`, `_lseek`, `_close` come from `libnosys` and only warn ("not implemented"); nothing in the
   runtime calls them.
4. IWRAM hot code grows 32 bytes in the ROMs with audio: **8,128 of 8,192 bytes**. Close to the budget row of
   `RamBudget`: one more small `.iwram` function would cross it.

## Results

| Measure | devkitARM 16.1 | Arm 15.2 |
|---|---|---|
| Runtime test ROMs (`agb runtime-test`, clean build) | 24/24 pass | 24/24 pass |
| ROM bytes, all 27 test ROMs | 1,177,180 | 1,175,336 (−0.16 %) |
| Largest change of one ROM | | render_affine +1.2 %, audio and framework ROMs −0.5 to −0.7 % |
| IWRAM hot code, largest | 8,096 | 8,128 |
| render_stress busy, max / average cycles a frame | 197,802 / 147,558 | 200,057 / 148,653 (+1.1 % / +0.7 %) |
| framework_platformer, longest load step | 76,066 | 76,010 |
| Sky Hopper (`agb sample skyhopper`, `agb build --clean`) | 350,488 bytes, builds | 349,600 bytes, builds |
| Sky Hopper screen after 600 frames | | the same PNG, byte for byte |

## What this does not show

- Only mGBA through the libretro core: no hardware.
- The libraries are still devkitARM's; building libtonc and maxmod with gcc 15.2 (P24.03) may change size and speed.
- No licence was checked here (P24.03, P24.05, P24.07).
- Not run on a branch in CI as the item asks, and not on a PC with Smart App Control on (P24.08).
