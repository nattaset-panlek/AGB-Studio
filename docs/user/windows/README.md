# Editor windows

One page for each window of AGB Studio. Each page is named after the window's Figma id, quotes the Figma caption,
lists where the window opens from and describes what is on it. A line that starts with "Differs from Figma" marks a
place where AGB Studio does not yet match the design.

Menus (T-M01 to T-M24) have no pages of their own: each menu item is listed under "Opens from" on the window it opens.
For the hardware limits the windows check (VRAM, palettes, sprites, sound, save), see the
[GBA hardware guide](../gba-hardware-guide.md).

## Pages

| Id | Window | Purpose | Page |
|---|---|---|---|
| T-P01 | Scene Editor | Lay out a scene, place actors, edit them in the Inspector, open assets. | [T-P01](T-P01-scene-editor.md) |
| T-P02 | Play Mode | Run the game in the built-in mGBA core with a live Inspector, overlays and Console. | [T-P02](T-P02-play-mode.md) |
| T-P03 | Tilemap Editor | Paint tiles on BG layers, set tile collision, auto-tile, and watch VRAM use. | [T-P03](T-P03-tilemap-editor.md) |
| T-P04 | Script Editor | Write and check C# game scripts, with autocomplete, GBA rule warnings and quick fixes. | [T-P04](T-P04-script-editor.md) |
| T-P05 | Console panel | Bottom panel log of build and editor messages; click a line to open the code. | [T-P05](T-P05-console-panel.md) |
| T-P06 | Animation panel | Bottom panel that previews the selected sprite's animations frame by frame. | [T-P06](T-P06-animation-panel.md) |
| T-P07 | Database | Game data tables edited like a spreadsheet, stored in ROM, used from scripts by name. | [T-P07](T-P07-database.md) |
| T-P08 | Sprite Editor | Draw or import sprites, edit their 16-colour palette, and make animations. | [T-P08](T-P08-sprite-editor.md) |
| T-P09 | Font Manager | Build GBA fonts from TTF, BDF or images, with Thai layout and a text box preview. | [T-P09](T-P09-font-manager.md) |
| T-P10 | Palette Manager | Manage the 16 BG and 16 OBJ palettes, their colours and palette effects. | [T-P10](T-P10-palette-manager.md) |
| T-P11 | Build Report | Last build: ROM size and breakdown, largest assets, history, cartridge header, export. | [T-P11](T-P11-build-report.md) |
| T-P12 | Launcher | Start page: open recent projects, start from a template, see tutorials and news. | [T-P12](T-P12-launcher.md) |
| T-P13 | World Map | See every scene as a card and draw the links between them. | [T-P13](T-P13-world-map.md) |
| T-P14 | Localization | Translate all game text, find missing strings, check text fits the GBA text box. | [T-P14](T-P14-localization.md) |
| T-P15 | Audio | Music and sound effects: tracker patterns, preview, mixer, GBA channel use, audio size. | [T-P15](T-P15-audio.md) |
| T-P16 | Dialogue Editor | Write conversations with speakers, portraits, choices and commands, with a text box preview. | [T-P16](T-P16-dialogue-editor.md) |
| T-P17 | Logic Graph | Build game logic by wiring nodes, kept in sync with generated C#. | [T-P17](T-P17-logic-graph.md) |
| T-P18 | Scripting API | Searchable reference of the scripting API, with examples you can copy. | [T-P18](T-P18-scripting-api.md) |
| T-P19 | Debugger | Look inside the running game: VRAM tiles, OAM sprites, Profiler, breakpoints. | [T-P19](T-P19-debugger.md) |
| T-P20 | Launcher · Templates | Browse the genre templates by filter or search, read the details, start a project. | [T-P20](T-P20-launcher-templates.md) |
| T-P21 | Launcher · Learn | Tutorials with saved progress, sample projects, and links to the docs and community. | [T-P21](T-P21-launcher-learn.md) |
| T-P22 | Scene Editor · Empty project | The Scene Editor of a new empty project; each panel shows how to start. | [T-P22](T-P22-scene-editor-empty-project.md) |
| T-P23 | Debugger · Memory | Where work RAM goes, a hex view, and watched values while the game runs. | [T-P23](T-P23-debugger-memory.md) |
| T-P24 | Debugger · Palettes | Live palette memory, colours changed this frame, and what uses each colour. | [T-P24](T-P24-debugger-palettes.md) |
| T-P25 | SFX Designer | Design sound effects on the GBA sound channels (square, wave, noise), or bake them to `.wav`. | [T-P25](T-P25-sfx-designer.md) |
| T-P26 | Auto-tile Rules panel | Edit 3×3 auto-tile rules so edges and corners join up while you paint. | [T-P26](T-P26-auto-tile-rules-panel.md) |

## Dialogs

| Id | Window | Purpose | Page |
|---|---|---|---|
| T-D01 | New Project | Pick a genre template, name the game, set the cartridge header and save type. | [T-D01](T-D01-new-project.md) |
| T-D02 | New Scene | Create a scene from a template or empty, with its size and video mode. | [T-D02](T-D02-new-scene.md) |
| T-D03 | Scene Settings | One scene's name, size, start scene, camera, parallax, music and events. | [T-D03](T-D03-scene-settings.md) |
| T-D04 | Import Asset | Drop files, see how each is detected, set options per file, and import. | [T-D04](T-D04-import-asset.md) |
| T-D05 | Export ROM | Export a Release `.gba` with its cartridge header and size options. | [T-D05](T-D05-export-rom.md) |
| T-D06 | Build ROM | Build progress step by step with the log, then run in the emulator. | [T-D06](T-D06-build-rom.md) |
| T-D07 | Flash to Cartridge | Write the built ROM to a flash cart's SD card or a cartridge writer. | [T-D07](T-D07-flash-to-cartridge.md) |
| T-D08 | Project Settings | Settings for the whole project; the Input tab maps GBA buttons to actions, keys and gamepad. | [T-D08](T-D08-project-settings.md) |
| T-D09 | Preferences | Settings for AGB Studio itself; the Emulator tab sets core, scale, filter, audio, fast-forward. | [T-D09](T-D09-preferences.md) |
| T-D10 | Keyboard Shortcuts | Searchable list of the main shortcuts with the keys set now. | [T-D10](T-D10-keyboard-shortcuts.md) |
| T-D11 | About | Version, build date and credits, with links to the licenses and the website. | [T-D11](T-D11-about.md) |
| T-D12 | Unsaved Changes | Asks whether to save changed files before exiting or opening another project. | [T-D12](T-D12-unsaved-changes.md) |
| T-D13 | New Sprite | Make a sprite: name, GBA size, frames, shared palette, starting image. | [T-D13](T-D13-new-sprite.md) |
| T-D14 | New Script | Create a C# script from a template and attach it to an actor. | [T-D14](T-D14-new-script.md) |
| T-D15 | New Font | Make a font from a source file, with size, width and character sets. | [T-D15](T-D15-new-font.md) |
| T-D16 | Add Characters | Paste game text and add the characters the font is missing. | [T-D16](T-D16-add-characters.md) |
| T-D17 | New Table | Create a data table: empty, from a CSV file, or copied from another table. | [T-D17](T-D17-new-table.md) |
| T-D18 | Add Field | Add a column to a table: type, enum values, default, ROM bytes per row. | [T-D18](T-D18-add-field.md) |
| T-D19 | Import CSV | Match CSV columns to table fields, check types, then merge the rows. | [T-D19](T-D19-import-csv.md) |
| T-D20 | Add Language | Add a game language with its source language, font and fallback. | [T-D20](T-D20-add-language.md) |
| T-D21 | New Speaker | Add a dialogue speaker: English and Thai names, portrait, name tag colour, blip sound. | [T-D21](T-D21-new-speaker.md) |
| T-D22 | Opening Project | Loading steps while a project opens (read, load assets, compile, open scene); can cancel. | [T-D22](T-D22-opening-project.md) |
| T-D23 | Build ROM · Failed | A failed build: the failed step, clickable errors and Go to first error. | [T-D23](T-D23-build-rom-failed.md) |
| T-D24 | Import Asset · Errors | Each file's import problem in plain words, with a fix you can click. | [T-D24](T-D24-import-asset-errors.md) |

## Settings tabs

| Id | Window | Purpose | Page |
|---|---|---|---|
| T-S01 | Project Settings · General | Game title, version, cartridge header, start scene, default font and language, splash. | [T-S01](T-S01-project-settings-general.md) |
| T-S02 | Project Settings · Save data | Save chip type, save slots and the values each slot stores, with a size meter. | [T-S02](T-S02-project-settings-save-data.md) |
| T-S03 | Project Settings · Build | Debug and Release, optimisation, output folder, ROM size warning, toolchain status. | [T-S03](T-S03-project-settings-build.md) |
| T-S04 | Project Settings · Scripting | Script language, generated folder, namespace, hot reload and Analyzer rule levels. | [T-S04](T-S04-project-settings-scripting.md) |
| T-S05 | Project Settings · Localization | Languages and translation progress, font per language, fallback, Thai line breaking. | [T-S05](T-S05-project-settings-localization.md) |
| T-S06 | Preferences · Appearance | Theme, accent colour, UI scale, code font size and display options. | [T-S06](T-S06-preferences-appearance.md) |
| T-S07 | Preferences · Editor | Autosave, undo history, snap, and the built-in or external script editor. | [T-S07](T-S07-preferences-editor.md) |
| T-S08 | Preferences · Paths | Paths to devkitPro, the projects folder and outside programs; clear the build cache. | [T-S08](T-S08-preferences-paths.md) |
| T-S09 | Preferences · Shortcuts | Search, change and reset keyboard shortcuts, with clash warnings. | [T-S09](T-S09-preferences-shortcuts.md) |

## Popovers

| Id | Window | Purpose | Page |
|---|---|---|---|
| T-O01 | Scene Switcher | The project's scenes in story order; pick one to switch to it. | [T-O01](T-O01-scene-switcher.md) |
| T-O02 | Video Mode | Choose the scene's GBA video mode (0 to 3), alpha blending and mosaic. | [T-O02](T-O02-video-mode.md) |
| T-O03 | Grid & Snap | Grid size, show grid, snap, collision and camera overlays, grid colour. | [T-O03](T-O03-grid-and-snap.md) |
| T-O04 | Asset Picker | Pick a sprite sheet, tileset, font or prefab, with search and type tabs. | [T-O04](T-O04-asset-picker.md) |
| T-O05 | Component Picker | Add a built-in or C# behaviour to the selected actor, or start a new script. | [T-O05](T-O05-component-picker.md) |
| T-O06 | Budget Details | The full GBA budget of the open scene, with a tip on sharing palettes. | [T-O06](T-O06-budget-details.md) |
| T-O07 | Command Palette | Ctrl+K search for commands and project files, then jump to them. | [T-O07](T-O07-command-palette.md) |
| T-O08 | Add Node | Search for a logic node and add it where you clicked. | [T-O08](T-O08-add-node.md) |
| T-O09 | Link Settings | A scene link's target, spawn point, transition, duration and condition. | [T-O09](T-O09-link-settings.md) |
| T-O10 | Palette Effect | Set up a palette fade, colour cycle or flash with a live preview. | [T-O10](T-O10-palette-effect.md) |
| T-O11 | Find & Replace | Find and replace in this file, all scripts or the whole project. | [T-O11](T-O11-find-and-replace.md) |
| T-O12 | Record Shortcut | Press new keys for a command's shortcut and replace a clashing one. | [T-O12](T-O12-record-shortcut.md) |
| T-O13 | Tutorial Steps | Floating tutorial panel: step text, picture, tasks that tick themselves, Back / Skip / Next. | [T-O13](T-O13-tutorial-steps.md) |
