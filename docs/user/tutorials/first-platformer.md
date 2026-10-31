# Your first platformer

Build Sky Hopper step by step · 15 min · 8 steps

You make a platformer from the Platformer template, change how the player jumps, add a coin, edit a script, paint
the level, check the GBA budget and get the ROM onto a cartridge or an emulator.

## 1. Create your project

Open New Project, keep the Platformer template and name the game. Create Project makes the folder and opens it.

- [ ] Open New Project (File › New Project…, or New Project in the Launcher)
- [ ] Create the project
- [ ] Look at the Scene Editor

## 2. Find your way around

The Hierarchy lists everything in the scene, the Inspector edits the selection and the bottom panel holds Assets and
the Console.

- [ ] Pick the Paint tiles tool (B)
- [ ] Toggle the grid (G)
- [ ] Open the Console tab

## 3. Make the player jump

Select Player, then in the Inspector raise Jump force to about 3.5. Press Play and try the A button (Z on your
keyboard).

- [ ] Select Player in the Hierarchy
- [ ] Set Jump force to 3.5 (the task ticks when the Inspector shows 3.5)
- [ ] Press Play and jump (press the key mapped to A in Project Settings · Input while the game runs)

## 4. Add a coin

Coins are Pickup actors. Add one from Scene › Add Actor and save the scene.

- [ ] Add a Pickup from Scene › Add Actor
- [ ] Save the scene (Ctrl+S)

## 5. Write your first script

Scripts are C# behaviours. Open PlayerController.cs, change a value and save.

- [ ] Open PlayerController.cs (the Script Editor opens)
- [ ] Save the script (Ctrl+S)

## 6. Paint the level

Open the Forest_01 map and paint ground tiles with the Paint tiles tool.

- [ ] Open Forest_01.map (the Tilemap Editor opens)
- [ ] Paint with the B tool

## 7. Check the GBA budget

VRAM, OAM and ROM space are limited. Look at the budget, build the ROM and read the report.

- [ ] Open Budget Details (click the GBA Budget block at the bottom of the Inspector)
- [ ] Build the ROM (Ctrl+B)
- [ ] Open the Build Report

## 8. Play it on hardware

Export the .gba file for an emulator, or flash it to a cartridge.

- [ ] Export the ROM (File › Export ROM (.gba)…)
- [ ] Flash to Cartridge (Build › Flash to Cartridge…)

Either task finishes the step. Writing to a GBxCart RW is not available in this release; copying the ROM to a flash
cart's SD card is.

## See also

- [Scene Editor](../windows/T-P01-scene-editor.md), [Play Mode](../windows/T-P02-play-mode.md),
  [Tilemap Editor](../windows/T-P03-tilemap-editor.md), [Script Editor](../windows/T-P04-script-editor.md)
- [Budget Details](../windows/T-O06-budget-details.md), [Build Report](../windows/T-P11-build-report.md),
  [Export ROM](../windows/T-D05-export-rom.md), [Flash to Cartridge](../windows/T-D07-flash-to-cartridge.md)
- [The licence of your games](../licence-for-your-games.md)
