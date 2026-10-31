# Emulator evidence (EMU)

Pictures from the two emulators: the mGBA libretro core that the editor and the tests embed (`*-mgba.png`) and
VBA-M 2.2.0 (`*-vbam.png`, the whole emulator window).

| Files | What they show | How they were made |
|---|---|---|
| `template_*`, `sample_*` | Every template and sample boots to its title | `agb template` / `agb sample`, then `agb romtest --shot` (mGBA) and VBA-M |
| `skyhopper-forest01-slice-30-mgba.png`, `-60-` | P4.X1: the Forest_01 slice 30 and 60 frames into play: player, slimes, coins, HUD | `SkyHopperPlayTests.ForestSliceRunsAt60FpsWithPlayerSlimesCoinsAndHud` (writes `build/emu-evidence`) |
| `skyhopper-<n>-<scene>-transition-mgba.png`, `skyhopper-<n>-<scene>-mgba.png` | P5.X1: each scene reached through its World Map link, 4 frames in (transition running) and 10 frames in: Forest_02 (on touch, Only if, fade), Summit (door, slide), Forest_02 back (door, fade), Forest_01 (respawn, cut), Cave_01 (door, fade), Forest_01 from the cave | `SkyHopperPlayTests.AllFiveScenesLoadThroughTheirLinks` |
| `skyhopper-start-<scene>-vbam.png` | P4.X1, P5.X1: each of the 5 scenes of the fixture loads in VBA-M (one build per start scene, boot splash off) | `agb sample skyhopper`, Project Settings start scene, `agb build`, VBA-M |

The Sky Hopper pictures (`skyhopper-*` and `sample_Sky_Hopper_demo-*`) were made again on 2026-10-31 at `07000d0`, after the fixture art was redrawn
from the Figma art (`67446e6`).
