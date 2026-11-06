# maxmod (GBA part)

From <https://github.com/devkitPro/maxmod>, tag `v1.0.15` (commit `ce8adb2a6bf1f29ec203b6c3410c1ddf8e270811`), the version devkitPro shipped and the runtime was measured with, unchanged. ISC licence: `maxmod_license.txt`.
Only what a GBA build needs: the common and `source_gba` assembly, its `.inc` files and the two public headers.
AGB Studio assembles these with the game (`NativeBuild`, P24.03) instead of linking devkitPro's `libmm.a`.
