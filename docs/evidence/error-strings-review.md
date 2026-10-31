# G-ERR.01 review: user-facing error and warning strings (2026-10-31)

Checklist item: G-ERR.01 in `docs/ENGINE-CHECKLIST.md` ("Every user-facing error says what happened, why, and how to
fix it, in plain words (pattern of T-D24 and T-D23) · Verify: `REV` of all error strings").

Scope: snapshot = branch `fix-docs` at commit `24a26b6` (main, "Merge p11-integrate: Phase 11 audio and scripts
windows, editor integration fixes"). Searched with ripgrep, then read the code around each hit to keep only text a user
can see:

- `tools/AgbStudio.Build`: `new Diagnostic\(|throw new|Problems.Add|warn\(|Warnings.Add|Hints.Add`; `Pipeline/GameBuilder.cs`
  (incl. `CheckHardwareLimits`), `Pipeline/BuildReport.cs`, `Toolchain.cs`, `NativeBuild.cs` and `Packager.cs` read around every hit.
- `tools/AgbStudio.Scripting`: `new ScriptDiagnostic\(|diag\.Error\(|_diag\.Error\(|Error\(|throw new`.
- `tools/AgbStudio.Analyzers/GaeRules.cs`, `tools/agb/Program.cs`: read in full.
- `tools/AgbStudio.Core` and `tools/AgbStudio.Emu`: `throw new`.
- `runtime/src`, `runtime/include`: `debug::log|logf\(|warnOnce\(|AGB_ASSERT`.
- `editor/AgbStudio.Editor`: `Toasts\.|Toast\(` (55 hits), `ConsoleLevel\.(Error|Warning)`, `(Error|Problem|Help|Hint) = `,
  `throw new \w*Exception\(`, `Refuse\(`, `return "` in `Validate()` methods, and the broad
  `-i "failed|can't|cannot|could not|not found|missing|invalid|error|warning|too many|too long|does not|already exists|is not|isn't|must be|not supported|unable"`
  over string literals (194 lines outside `obj/`).

Excluded: `tests/`, `runtime/tests/`, `obj/`, `bin/`, generated files, code comments, the editor component `Gallery`,
theme token names such as `"status/warning"`, and counters that only count ("{n} warnings", "{n} strings missing")
without reporting a problem. No code was changed and nothing was built or run; the suggested wordings are proposals for
the code owners.

How rows are judged. WHAT = the text says what went wrong and where (file, scene, field). WHY = the rule or limit
behind it, when it is not obvious. HOW = what the user does next. Context the user always sees with the text counts:
the file name in a Build ROM log line, the field a help text sits under, the fix buttons of Import Asset · Errors, the
description under an analyzer popup title, the "Go to first error" button of Build ROM · Failed. For texts that end in
`{e.Message}` only the part AGB Studio writes is judged; the .NET part is usually technical. Verdicts: `OK` = all three
in plain words; `PARTIAL` = one part missing (named in the Verdict column); `POOR` = only a bare failure, internal
jargon or ids, or no context. Placeholders are written as `{name}`; runtime strings keep their printf form.

## Summary

| Area | Rows | OK | PARTIAL | POOR |
|---|---|---|---|---|
| Build pipeline | 52 | 52 | 0 | 0 |
| Scripting compiler | 103 | 103 | 0 | 0 |
| Analyzer | 18 | 18 | 0 | 0 |
| Project files | 9 | 9 | 0 | 0 |
| CLI | 16 | 16 | 0 | 0 |
| Runtime Console | 45 | 45 | 0 | 0 |
| Editor | 153 | 153 | 0 | 0 |
| **Total** | **396** | **396** | **0** | **0** |

At the review snapshot the counts were 149 OK, 193 PARTIAL and 53 POOR (Build pipeline 12 / 33 / 7, Scripting compiler
53 / 44 / 6, Analyzer 9 / 8 / 1, Project files 0 / 6 / 3, CLI 2 / 12 / 2, Runtime Console 1 / 25 / 19, Editor 72 / 65 /
15).

Rewording pass (branch `fix-messages`, 2026-10-31): every PARTIAL and POOR row under `tools/` and `runtime/` (169 rows,
including the three `tools/AgbStudio.Emu/LibretroCore.cs` rows of the Editor section) now says what happened, why and
what to do; those rows show the new text and current File:line, with Verdict OK. Two Build pipeline rows
(BuildReport.cs:143/144) were already gone from the code; their checks live in `CheckHardwareLimits` with the T-D23
wording. Runtime lines keep the `warning: ` prefix, use only printf arguments and stay within the 159 characters
`logf` keeps (`runtime/src/core/debug.cpp`); they say what a number is where a name is not known at run time.

Editor pass (branch `fix-editor2`, 2026-10-31): every PARTIAL and POOR row of the Editor section (77 rows) now says
what happened, why and what to do; those rows show the new text and current File:line, with Verdict OK. 12 of them
needed no code change: an earlier editor pass had reworded 11, and `GBxCart RW not available` only ever shows the
reworded serial port texts as its reason. Where a reader already says how to fix the file (PNG, WAV, MOD / XM, Tiled,
project files), the editor keeps that text as the reason and adds its own fix only for file system errors, so the fix
is not given twice. Figma's texts are kept as they are: the success forms `Build OK`, `Compiled to C#`,
`{version} found`, `{editor} found · used when Script editor is External` and the T-D23 title; only the failure forms
next to them, which no Figma frame shows, were reworded. One row was added for Run in mGBA with a ROM the emulator
refuses (`PlayController.Run`). The window pages in `docs/user/windows/` that quote a reworded text were updated.
Project Settings · Scripting now takes its analyzer rule titles from `tools/AgbStudio.Analyzers/GaeRules.cs`
(`ProjectSettingsModel.AnalyzerRules`), so it shows the reworded GAE005 / GAE006 titles and always says the same as the
Problems list.

A row can cover the same text at several places (all are listed in its File:line cell), so rows are fewer than the
call sites.

Main patterns:

- The model texts follow the T-D24 / T-D23 pattern: hardware limits in `CheckHardwareLimits`, the Import Asset colour
  problem with "Reduce to 16" / "Split into N palettes", the "not in the project ... Did you mean" script error
  (AGB0010), the missing-toolchain dialog, Build ROM · Failed, and most field validation texts.
- The most common gap is a missing fix: "X uses Y, which is not in Z." says what and where, but not what to do.
- The worst texts pass raw tool or .NET output through as the whole message (a build step exception, the devkitARM
  log, the mmutil log, `git init` output, `{e.Message}` toasts) or use engine jargon in the runtime Console
  ("screen stack", "script contexts", "bad opcode", "character block").

## Build pipeline

Texts reach the Build ROM log, Build ROM · Failed (T-D23) and the Console as `{file}: {message}` (file name only, the
diagnostics here have no line). Converter problems also show in Import Asset (T-D24).

| File:line | Text | Verdict | Suggested wording |
|---|---|---|---|
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:123 | `Build cancelled.` | OK | - |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:181 (`StepError`) | the exception text with the file being converted as the diagnostic file; AGB Studio's own file errors (Project files rows, fonts, sprite frames) pass through as they are, an IO error adds `Close other programs that use the file and check that it exists, then build again.`, any other exception becomes `The step '{step}' stopped: {reason} If the build keeps failing here, try Build › Clean Build.` | OK | - |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:474 | `Audio conversion failed for {file}: {first mmutil error line}. Open the file in a tracker or audio editor, save it again as MOD, XM or WAV, and build again.` (the whole mmutil output goes to the build log; the file is the diagnostic file) | OK | - |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:472 | `Strings for '{lang}' need a font: add one in Font Manager.` | OK | - |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:553 | `Language '{lang}' has no font set, so {font} is used. Pick a font for it in Localization · Languages.` (and `Language '{lang}' uses font '{name}', which is not in fonts/, so {font} is used. Add the font in Font Manager, or pick another one in Localization · Languages.`) | OK | - |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:561 | `{n} strings missing in {language}. Translate them in Localization ("Missing only"), or set a fallback language in Localization · Languages.` | OK | - |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:618 | `{n} C# scripts were not compiled: this build tool has no script compiler, so the game runs without them. Build from the AGB Studio editor or with agb build.` | OK | - |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:621 | `Scene {scene} needs {n} KB of background VRAM; the GBA has 64 KB. Use fewer layers, a smaller tileset, or image layers with more repeated tiles.` | OK | - |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:624 | `Scene {scene} needs {n} background palettes; {free} are free (of 16, two hold the menus and text). Reduce the colours of its tileset or images (Import Asset "Reduce to 16").` | OK | - |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:662 | `Scene menus need a font: add one in Font Manager.` | OK | - |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:936 | `Scene {scene} uses map '{map}', which does not exist. Pick another map in Scene Settings, or restore maps/{map}.map.` | OK | - |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:950 | `Map {map} uses tileset '{tileset}', which is not in assets/tilesets. Import the tileset again, or pick another one for the map.` | OK | - |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:962 | `Layer {layer} of map {map} has {n} tiles, but a {w}×{h} map needs {w*h}. The .map file is damaged; restore it from a backup or version control.` | OK | - |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:971 | `Layer {layer} uses image '{image}', which is not in assets/images. Import the image again, or pick another one for the layer.` | OK | - |
| tools/AgbStudio.Build/Graphics/AffineConverter.cs:42 (via GameBuilder.cs:984) | `Layer {layer}: a rotate / scale layer holds at most 256 different 8×8 tiles, so the rest show as empty. Use fewer different tiles in it, or make it a normal tile layer.` | OK | - |
| tools/AgbStudio.Build/Graphics/AffineConverter.cs:55 (via GameBuilder.cs:984) | `Layer {layer}: a rotate / scale layer is at most 64 × 64 map tiles (1024 × 1024 pixels), so the rest is cut off. Make the map smaller, or make it a normal tile layer.` | OK | - |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:1031 | `{actor}: {behaviour} has no field '{field}' (renamed or removed), so the value is ignored. Set the field again in the Inspector.` | OK | - |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:1040 | `{actor}: {behaviour}.{field} = '{value}' is not a {number\|whole number}. Enter a {number such as 1.5\|whole number such as 3} in the Inspector.` | OK | - |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:1050 | `{actor}: behaviour '{type}' was not found (not a built-in and no C# script defines it). Remove it from the actor in the Inspector, or create a script with class {type}.` | OK | - |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:1063 | `{actor} uses sprite '{sprite}', which is not in assets/sprites. Import the sprite again, or pick another one in the Inspector.` | OK | - |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:1077 | `{actor}: string key '{key}' is not in the Localization tables, so the label stays empty. Add the key in Localization, or change the text in the Inspector.` | OK | - |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:883 | `Link from '{actor}' in {scene}: no actor has that name (renamed or deleted?). Draw the link again in the World Map.` | OK | - |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:884 | `Link to '{scene}': no scene has that name (renamed or deleted?). Draw the link again in the World Map.` | OK | - |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:890 | `Link {from} → {to}: the 'Only if' condition was not compiled (this build tool has no script compiler), so the link is always allowed. Build from the AGB Studio editor or with agb build.` | OK | - |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:901 | `Scene {scene}: music '{track}' is not in assets/audio/music, so the scene plays no music. Import the track again, or pick another in Scene Settings.` | OK | - |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:1138 | `Menu item '{text}' is not a string key, and only ASCII text can be drawn without one. Add it as a string key in Localization, or use ASCII text.` | OK | - |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:1143 | `Menu item '{text}' loads '{scene}', but no scene has that name (renamed or deleted?). Pick an existing scene for the item.` | OK | - |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:1148 | `The scene menu has {n} items; a scene menu holds 1 to 16. Add or remove items.` (the diagnostic file is the scene) | OK | - |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:913 | `The menu of {id} does not fit on screen; move it up or use fewer items.` | OK | - (optional: `{id}` is the C++ name of the scene; show the scene name) |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:1343 | `Compiling or linking the game failed, and devkitARM gave no error line AGB Studio can point to (last line: {last line}). The full devkitARM output is in the Build ROM log (agb build --verbose prints it). If you changed native C++ code, check it; otherwise try Build › Clean Build.` | OK | - |
| tools/AgbStudio.Build/Pipeline/BuildReport.cs:138 | `ROM is {n} MB, over the {limit} MB warning set in Project Settings · Build. See Build Report · Largest assets for what to shrink, or raise the limit.` | OK | - |
| tools/AgbStudio.Build/Pipeline/BuildReport.cs:123 | `Fast RAM (IWRAM) uses {n} KB of 32 KB; less than 8 KB is left for the stack, so deep calls can crash the game. Use fewer or smaller global arrays in scripts.` | OK | - |
| tools/AgbStudio.Build/Pipeline/BuildReport.cs:143 | Gone since the snapshot: BuildReport no longer writes it. `CheckHardwareLimits` (GameBuilder.cs:741) checks sprite memory per scene with the T-D23 wording (`Scene {scene} needs up to {n} KB of OBJ VRAM for the sprite frames its actors show at once; the GBA has 32 KB. Use fewer or smaller sprites, ...`). | OK | - |
| tools/AgbStudio.Build/Pipeline/BuildReport.cs:144 | Gone since the snapshot: BuildReport no longer writes it. `CheckHardwareLimits` (GameBuilder.cs:745) reports `Scene {scene} needs {n} sprite palettes; the GBA has 16. Let sprites share a palette (Sprite Editor · Palette) or use fewer different sprites in the scene.` | OK | - |
| tools/AgbStudio.Build/Fonts/TextCodegen.cs:62 | `{key}: '{char}' (U+{code}) is not in font {font}, so it is not drawn. Add it in Font Manager · Add characters.` | OK | - |
| tools/AgbStudio.Build/Fonts/TextCodegen.cs:64 | `{key} ({lang}) needs {n} lines, but its text box shows {max}, so the rest does not fit. Shorten the text, or give the key more lines in Localization.` | OK | - |
| tools/AgbStudio.Build/Data/DatabaseCodegen.cs:100+124 | `{table} row {n} ({id}), {field}: '{value}' is not a whole number; using 0.` | OK | - |
| tools/AgbStudio.Build/Data/DatabaseCodegen.cs:100+129 | `{table} row {n} ({id}), {field}: '{value}' is not a number; using 0.` | OK | - |
| tools/AgbStudio.Build/Data/DatabaseCodegen.cs:173 | `{table} row {n} ({id}), {field}: string key '{value}' is not in the Localization tables, so the first string is used. Add the key in Localization, or fix the cell in Database.` | OK | - |
| tools/AgbStudio.Build/Data/DatabaseCodegen.cs:100+142 | `{table} row {n} ({id}), {field}: '{value}' is not one of {values}; using {first}.` | OK | - |
| tools/AgbStudio.Build/Data/DatabaseCodegen.cs:185 | `{table} row {n} ({id}), {field}: sprite '{value}' is not in assets/sprites, so no sprite is used. Import the sprite, or fix the cell in Database.` | OK | - |
| tools/AgbStudio.Build/Data/DatabaseCodegen.cs:195 | `{table} row {n} ({id}), {field}: '{value}' is not a row of {table2}, so its first row is used. Pick an existing row in the cell in Database.` | OK | - |
| tools/AgbStudio.Build/Data/DatabaseCodegen.cs:225 | `The CSV file is empty.` | OK | - |
| tools/AgbStudio.Build/Data/DatabaseCodegen.cs:305 | `Row {n}: {column} is “{value}”, not {a whole number\|a number\|true or false\|one of {values}} — will use the default {default}. Fix the CSV and choose it again to keep the value.` (also "Rows {a}, {b} and {c}: ...") | OK | - |
| tools/AgbStudio.Build/Graphics/SpriteConverter.cs:53 (via GameBuilder.cs:216 and Import Asset) | `Uses {n} colours. A GBA sprite palette holds 16, so it needs reducing or splitting.` + buttons "Reduce to 16", "Split into {n} palettes" | OK | - |
| tools/AgbStudio.Build/Graphics/TilesetConverter.cs:51 (via GameBuilder.cs and Import Asset) | `{n} tiles use more than 15 colours (a GBA tile has 15 plus transparent), so their colours were reduced. Check them, or redraw them with fewer colours.` | OK | - |
| tools/AgbStudio.Build/Graphics/TilesetConverter.cs:56 (via GameBuilder.cs and Import Asset) | `The tiles need more than the {n} free palettes, so all colours were reduced to one palette of 16. Use fewer different colour sets, or split the tileset.` | OK | - |
| tools/AgbStudio.Build/Fonts/TrueType.cs:22 | `{font} is not a TrueType font: it is an OpenType font with CFF outlines, or not a font file, and AGB Studio reads only TrueType outlines. Use the .ttf version of the font.` | OK | - |
| tools/AgbStudio.Build/Fonts/TrueType.cs:31 | `{font} is damaged or incomplete (its '{table}' table is missing), so it can't be read. Download the font again, or use another one.` | OK | - |
| tools/AgbStudio.Build/Fonts/TrueType.cs:64 | `{font} has no Unicode character map, so its letters can't be found. Use a Unicode .ttf font.` | OK | - |
| tools/AgbStudio.Build/Graphics/PaletteFiles.cs:28 | `{file} is not a JASC .pal or GIMP .gpl palette.` | OK | - |
| tools/AgbStudio.Build/Graphics/SpriteConverter.cs:40 (and Gba.cs:45) | `Sprite {sprite}: frames are {w}×{h} pixels, but the biggest GBA sprite is 64×64. Set a frame size of 64×64 or less in the Sprite Editor.` (Gba.FitOam: `Frames of {w}×{h} pixels are larger than the biggest GBA sprite (64×64). Set a frame size of 64×64 or less in the Sprite Editor.`) | OK | - |

Notes:

- `GameBuilder.cs:158` is the path for every exception inside a step, so PNG, font, JSON and script-assembler errors
  arrive without the file that caused them (see Project files and AGB0300 "Script too long"). Since the rewording
  pass the sprite, tileset, image and font being converted is the diagnostic file, project JSON errors name their file
  and line (`GameProject.Load`), and "Script too long" is an AGB0300 error at the cutscene.
- Import Asset marks `.otf` files "Ready · rendered to a GBA font when you build." (`AssetImporter.cs:123`), but most
  `.otf` fonts have CFF outlines and then fail the build with `TrueType.cs:22`.

## Scripting compiler

Texts reach the Script Editor squiggles and Problems, the Console and the build log as `{file}.cs({line},{col}): {message}`.
AGB0100 = C# to C++ translator, AGB0300 = cutscene to Event Script bytecode.

| File:line | Text | Verdict | Suggested wording |
|---|---|---|---|
| tools/AgbStudio.Scripting/ScriptCompiler.cs:196 | AGB0301 `Cutscene {method} must return Task or Task<int> (async void is not supported).` | OK | - |
| tools/AgbStudio.Scripting/ScriptCompiler.cs:211 | AGB0302 `[ScriptNative] method {method} must be static.` | OK | - |
| tools/AgbStudio.Scripting/ScriptCompiler.cs:226 | AGB0002 `Two script types are named {name}. Type names must be unique because scenes refer to behaviours by name.` | OK | - |
| tools/AgbStudio.Scripting/ScriptCompiler.cs:257 | AGB0002 `Too many saved script flags or variables ({flags} flags, {vars} variables); the save keeps at most 256 flags and 32 variables, so the game could not be saved. Remove saved flags or variables you no longer use (dialogue setFlag commands make flags too).` (still shares id AGB0002 with the duplicate type name error) | OK | - |
| tools/AgbStudio.Scripting/Dialogue/DialogueFile.cs:63 (AGB0001) | `The dialogue file is not valid JSON at line {n}: {reason}. Undo the hand edit, or restore the file from a backup or version control.` (`GameProject.BrokenJson` drops the Path / LineNumber tail of the .NET text) | OK | - |
| tools/AgbStudio.Scripting/Dialogue/DialogueFile.cs:65 (AGB0001) | `The dialogue file is empty, so it has no conversations. Delete it, or restore it from a backup or version control.` | OK | - |
| tools/AgbStudio.Scripting/Dialogue/DialogueFile.cs:67 (AGB0001) | `This dialogue file is format {n}; this AGB Studio reads format 1. {Update AGB Studio to open it.\|Restore the file from a backup or version control.}` (the second for formats below 1) | OK | - |
| tools/AgbStudio.Scripting/Graph/LogicGraph.cs:89 (AGB0001) | `The graph file is not valid JSON at line {n}: {reason}. Undo the hand edit, or restore the file from a backup or version control.` | OK | - |
| tools/AgbStudio.Scripting/Graph/LogicGraph.cs:100 (AGB0001) | `This graph file is format {n}; this AGB Studio reads format 1. {Update AGB Studio to open it.\|Restore the file from a backup or version control.}` | OK | - |
| tools/AgbStudio.Scripting/Dialogue/DialogueCompiler.cs:48 | AGB0400 `Two conversations are named '{id}', so scripts can't tell which one to start. Rename one in the Dialogue Editor.` | OK | - |
| tools/AgbStudio.Scripting/Dialogue/DialogueCompiler.cs:67 | AGB0401 `Next goes to line '{label}', which does not exist in conversation '{id}'. Pick an existing line for Next.` | OK | - |
| tools/AgbStudio.Scripting/Dialogue/DialogueCompiler.cs:123 | AGB0401 `Choice {n} goes to line '{label}', which does not exist in conversation '{id}'. Pick an existing line for the choice.` | OK | - |
| tools/AgbStudio.Scripting/Dialogue/DialogueCompiler.cs:167 | AGB0402 `Unknown dialogue command '{command}'. Commands: goToScene, setFlag, giveItem, playSound, wait, shakeCamera, end.` | OK | - |
| tools/AgbStudio.Scripting/Translation/ScriptModel.cs:276 | AGB0010 `{Sound\|Music\|Scene\|...} "{name}" is not in the project. {where these names come from}. Did you mean "{hint}"?` (or `The project has none yet.`) | OK | - |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:96 | AGB0300 `Cutscene parameter '{name}' must be int, bool, fixed or an enum.` | OK | - |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:102 | AGB0300 `This cutscene has no body, so there is nothing to run. Add a body { ... }, or remove [Cutscene].` | OK | - |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:109 | AGB0300 `Cutscene {method} uses {n} variables; the Event Script VM allows {max}. Split it into smaller cutscenes.` | OK | - |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:111 | AGB0300 `Cutscene {method} needs {n} stack entries; the VM has {max}. Simplify long expressions.` | OK | - |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:159 | AGB0300 `Cutscene variables must be int, bool, fixed or an enum ('{name}').` | OK | - |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:171 | AGB0300 `Only using var _ = LockAll(); is supported in cutscenes.` | OK | - |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:179 | AGB0300 `Only using (LockAll()) { ... } is supported in cutscenes.` | OK | - |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:275 | AGB0300 `break / continue is outside a loop, so there is nothing to leave. Move it into a loop, or use return to end the cutscene.` | OK | - |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:276 | AGB0300 `foreach is not available in cutscenes; use a for loop with a counter.` | OK | - |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:287 | AGB0300 `This kind of statement can't run in a cutscene. Cutscenes run if, while, for, switch, return, assignments and calls; do the rest in a [ScriptNative] method and call that.` | OK | - |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:296 | AGB0300 `Only constant case labels are supported.` | OK | - |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:333 | AGB0300 `This expression gives no value a cutscene can use (for example a call that returns nothing). Use it as a statement of its own.` | OK | - |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:349 | AGB0300 `Strings are only allowed as names here (string keys, sounds, scenes).` | OK | - |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:372 | AGB0300 `The operator {symbol} is not available in cutscenes. Do this step in a [ScriptNative] method and call that.` (`ScriptModel.OperatorText` gives the C# symbol) | OK | - |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:394 | AGB0300 `Use x++ as a statement of its own in cutscenes.` | OK | - |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:421 | AGB0300 `This kind of expression can't run in a cutscene. Compute it in a [ScriptNative] method and call that.` | OK | - |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:434 | AGB0300 `Only decimal constants convert to fixed in cutscenes (for example 1.5). Keep the value in a fixed variable instead.` | OK | - |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:497 | AGB0300 `Strings cannot be compared in cutscenes. Compare enums, ids or flags instead.` | OK | - |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:525 | AGB0300 `The operator {symbol} is not available in cutscenes. Do this step in a [ScriptNative] method and call that.` | OK | - |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:567 | AGB0300 `Cutscenes can't read '{code}'. They can read their own variables, parameters, Story / Flags variables, Game fields and constants; read anything else in a [ScriptNative] method.` (`{code}`: the first 40 characters of the expression) | OK | - |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:574 | AGB0300 `actor is only available in cutscenes declared in a Behaviour.` | OK | - |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:593 | AGB0300 `Cutscenes can't assign '{code}'. They can assign their own variables, Story / Flags variables and Game fields; change anything else in a [ScriptNative] method.` | OK | - |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:625 | AGB0300 `Cutscene {method} could not be compiled (see its own error), so it can't be called here. Fix that error first.` | OK | - |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:621 | AGB0300 `{method} belongs to a behaviour; call it from a cutscene of the same behaviour.` | OK | - |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:642 | AGB0300 `Debug.Log in a cutscene takes a constant string.` | OK | - |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:649 | AGB0300 `'{type}.{method}' cannot be called from a cutscene. Cutscenes can call the cutscene API (Say, Choice, Move, Wait, ...), other cutscenes and [ScriptNative] methods.` | OK | - |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:682 | AGB0300 `{type}.{method} is not available in this version of the engine, so a cutscene can't call it. Update AGB Studio, or report this as a bug.` | OK | - |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:673 | AGB0300 `{method} takes at most {max} values in a cutscene.` | OK | - |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:690 | AGB0300 `'{type}.{method}' cannot be called from a cutscene. Cutscenes can call the cutscene API (Say, Choice, Move, Wait, ...), other cutscenes and [ScriptNative] methods.` (same text as the other call site) | OK | - |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:724 | AGB0011 `This name must be a constant string so it can be turned into an id at build time.` | OK | - |
| tools/AgbStudio.Scripting/Bytecode/ScriptAssembler.cs:17 (caught at BytecodeCompiler.cs:111) | AGB0300 at the cutscene: `Cutscene {method} is too long to compile: a jump in it spans more than 32 KB of code. Split it into smaller cutscenes.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.cs:116 | AGB0100 `The type '{type}' cannot be used in GBA scripts. Use int, bool, fixed, fixed2, an enum, a struct or a class of the GBA API or of your scripts.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.cs:133 | AGB0100 `Nested type '{type}' is not supported. Move it out of '{outer}'.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.cs:138 | AGB0100 `Generic type '{type}' is not supported; generics are only allowed on API calls such as Get<T>(). Make one non-generic type per use instead.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.cs:199 | AGB0100 `[Cutscene] method '{method}' must be declared 'async Task'.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.cs:201 | AGB0100 `async method '{method}' needs [Cutscene]: only cutscenes can wait, and they compile to Event Script bytecode.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.cs:204 | AGB0100 `Nested type '{type}' is not supported. Move it out of '{outer}'.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.cs:217 | AGB0100 `'{type}' derives from '{base}', which scripts cannot extend. Derive from Behaviour or SceneScript, or from one of your own classes.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.cs:267 | AGB0100 `Indexers (this[...]) are not supported in GBA scripts. Use Get(i) and Set(i, value) methods instead.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.cs:278 | AGB0100 `Generic method '{method}' is not supported; generics are only allowed on API calls such as Get<T>(). Write one method per type instead.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.cs:281 | AGB0100 `Operators declared in scripts are not supported yet. Use a method instead (for example Add(a, b)).` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.cs:284 | AGB0100 `Static constructors are not supported; initialize static fields where they are declared.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.cs:480 | AGB0100 `Only one-dimensional arrays are supported in GBA scripts. Use one array of width × height and index it as y * width + x.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.cs:496 | AGB0100 `Array sizes must be constants (the GBA has no heap): new int[8].` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.cs:507 | AGB0100 `Arrays need a fixed size where they are declared: int[] counts = new int[8]; (the GBA has no heap).` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.cs:544 | AGB0100 `params arrays are not supported in GBA scripts. Pass a normal array, or write one overload per argument count.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.cs:616 | AGB0100 `This method body could not be turned into C++ for the GBA (it has C# errors, or a form scripts don't support). Fix the other errors in it, or write the body as a block { ... }.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:67 | AGB0100 `foreach works over arrays only in GBA scripts. Use a for loop with an index for other collections.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:81 | AGB0100 `yield is not supported in GBA scripts. Fill an array instead, or use a [Cutscene] to wait across frames.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:99 | AGB0100 `using blocks are only allowed in [Cutscene] methods (using var _ = LockAll();).` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:102 | AGB0100 `Exceptions are not available on GBA (GAE004): remove try / throw.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:105 | AGB0100 `Local functions are not supported; make it a method of the class.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:108 | AGB0100 `This kind of statement can't be used in GBA scripts. Rewrite it with if, loops, switch, assignments and method calls.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:157 | AGB0100 `Only constant case labels are supported in switch.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:194 | AGB0100 `ref locals are not supported in GBA scripts. Copy the value into a normal variable and write it back.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:256 | AGB0100 `Strings cannot be joined at runtime on GBA. Use Debug.Log($"...") to show values, or string keys from Localization for text.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:276 | AGB0100 `The operator {symbol} is not supported in GBA scripts. Write the step as a method call instead.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:286 | AGB0100 `This property can only be assigned in a statement of its own.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:297 | AGB0100 `This property can only be changed in a statement of its own.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:299 | AGB0100 `Strings cannot be joined at runtime on GBA. Use Debug.Log($"...") to show values, or string keys from Localization for text.` (same text as the other call site) | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:312 | AGB0100 `fixed ++ / -- can only be used as a statement of its own.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:333 | AGB0100 `Only one-dimensional arrays are supported in GBA scripts. Use one array of width × height and index it as y * width + x.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:344 | AGB0100 `await is only allowed in [Cutscene] async Task methods.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:347 | AGB0100 `Interpolated strings can only be passed to Debug.Log on GBA.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:350 | AGB0100 `Lambdas are not supported in translated scripts (GAE006). Use a method instead.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:353 | AGB0100 `Type checks need RTTI, which GBA builds do not have. Use actor.Is<T>() or Get<T>() != null.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:356 | AGB0100 `?. and ?? are not supported; check for null with an if statement.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:359 | AGB0100 `Exceptions are not available on GBA (GAE004): remove try / throw.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:362 | AGB0100 `Arrays can only be created where a field or local variable is declared (the GBA has no heap).` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:365 | AGB0100 `This kind of expression can't be used in GBA scripts. Split it into simpler steps.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:413 | AGB0201 `Only decimal constants convert to fixed (for example 1.5). The GBA has no FPU: keep the value in a fixed variable instead of a float or double (GAE002).` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:418 | AGB0202 `{value} is outside the fixed range (-8388608 to 8388607.99).` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:425 | AGB0100 `The conversion to '{type}' is not supported on GBA. Convert between int, fixed and enums only.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:448 | AGB0100 `A value can't be stored as object or as an interface on GBA (that would need the heap). Use the value's own type.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:529 | AGB0100 `Strings cannot be compared at runtime on GBA. Compare tags with actor.tag == "Coin", or use enums and ids.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:563 | AGB0100 `This name must be a constant string so it can be turned into an id at build time.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:598 | AGB0100 `'{type}.{field}' is not available on GBA. Scripts can use the GBA API and their own classes.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:609 | AGB0100 `'{property}' can only be set, not read. Keep its value in a field of your own.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:616 | AGB0100 `'{type}.{property}' is not available on GBA. Scripts can use the GBA API and their own classes.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:624 | AGB0100 `'{code}' can't be created on GBA. Create objects of your own classes or of the GBA API.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:626 | AGB0100 `Object initializers { ... } are not supported; set the fields after creating the object.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:636 | AGB0120 `Behaviours are created by the scene, not with new. Add {type} to an actor in the Inspector, or use Get<{type}>().` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:639 | AGB0100 `'{type}' can't be created on GBA. Create objects of your own classes or of the GBA API.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:669 | AGB0011 `The {parameter} argument must be a constant string (for example "jump") so it can be turned into an id at build time.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:693 | AGB0100 `Local functions are not supported; make it a method of the class.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:705 | AGB0100 `{method} can only be used inside a [Cutscene] async Task method (it waits across frames).` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:707 | AGB0100 `'{type}.{method}' is not available on GBA. Scripts can use the GBA API (Input, Sound, Scene, ...) and their own classes.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:725 | AGB0100 `Cutscene {method} could not be compiled (see its own error), so it can't be started here. Fix that error first.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:750 | AGB0100 `Debug.Log can't show '{code}' ({type}). It shows int, bool, fixed, enum and string values.` | OK | - |

## Analyzer

Rules in `tools/AgbStudio.Analyzers/GaeRules.cs`. The Script Editor popup shows `{id} · {message}` with the
description under it and fix buttons; the Problems line shows `{message} — {text after the arrow}`; the Build ROM log
and Console show `{id} {short text}`. Titles are the rule names in Project Settings · Scripting.

| File:line | Text | Verdict | Suggested wording |
|---|---|---|---|
| tools/AgbStudio.Analyzers/GaeRules.cs:15 | GAE001 title `class allocated in Update / OnTouch (no garbage collector)` | OK | - |
| tools/AgbStudio.Analyzers/GaeRules.cs:16+18 | GAE001 `'new {type}' allocates a class in {method}; the GBA has no garbage collector, so this memory is never given back` + `Classes live until the scene ends. Create the object once in Start (it goes into the scene's memory arena) and reuse it.` | OK | - |
| tools/AgbStudio.Analyzers/GaeRules.cs:67 | GAE001 log `class allocated in {method} is never freed → allocate in Start` | OK | - |
| tools/AgbStudio.Analyzers/GaeRules.cs:23 | GAE002 title `float or double — the GBA has no FPU, use fixed` | OK | - |
| tools/AgbStudio.Analyzers/GaeRules.cs:24 (+26) | GAE002 `{type} is very slow on GBA (no floating-point hardware)` + description (Problems line: `... — use fixed`) | OK | - |
| tools/AgbStudio.Analyzers/GaeRules.cs:68 | GAE002 log `{type} is slow on GBA → use fixed` | OK | - |
| tools/AgbStudio.Analyzers/GaeRules.cs:31 | GAE003 title `new inside Update — allocate in Start or use a pool` | OK | - |
| tools/AgbStudio.Analyzers/GaeRules.cs:32+34 | GAE003 `'new {type}' inside {method} allocates every frame; allocate it in Start or use a pool` + description | OK | - |
| tools/AgbStudio.Analyzers/GaeRules.cs:69 | GAE003 log `new inside {method} fills memory every frame → allocate in Start or use a pool` | OK | - |
| tools/AgbStudio.Analyzers/GaeRules.cs:39 | GAE004 title `try / throw — exceptions are not supported` | OK | - |
| tools/AgbStudio.Analyzers/GaeRules.cs:40+42 | GAE004 `'{keyword}' is not supported: exceptions are not available on GBA` + `The GBA runtime is built without exceptions. Return a bool or an error value instead of throwing.` | OK | - |
| tools/AgbStudio.Analyzers/GaeRules.cs:70 | GAE004 log `{keyword} (no exceptions on GBA) → return a bool or an error value` | OK | - |
| tools/AgbStudio.Analyzers/GaeRules.cs:47 | GAE005 title `string concatenation in a loop (a new string every pass)` (the copy in editor ProjectSettingsModel.AnalyzerRules still has the old title) | OK | - |
| tools/AgbStudio.Analyzers/GaeRules.cs:48+50 | GAE005 `String concatenation in a loop builds a new string on every pass` + description | OK | - |
| tools/AgbStudio.Analyzers/GaeRules.cs:71 | GAE005 log `string concatenation in a loop makes a new string every pass → use string keys, or log outside the loop` | OK | - |
| tools/AgbStudio.Analyzers/GaeRules.cs:55 | GAE006 title `lambda that captures variables (hidden allocation)` (the editor copy still has the old title) | OK | - |
| tools/AgbStudio.Analyzers/GaeRules.cs:56+58 | GAE006 `This lambda captures '{name}', which needs a hidden allocation` + description | OK | - |
| tools/AgbStudio.Analyzers/GaeRules.cs:72 | GAE006 log `lambda captures {name} (hidden allocation) → pass it as a parameter and make the lambda static` | OK | - |

## Project files

Thrown by `tools/AgbStudio.Core` when a project, scene, map or image is read. They reach the user inside
`Could not open {project}: {e.Message}` (OpeningProjectDialog.cs:250), `{file} can't be read: {e.Message}`
(AssetImporter.cs:146), or as a bare build error with no file (GameBuilder.cs:158).

| File:line | Text | Verdict | Suggested wording |
|---|---|---|---|
| tools/AgbStudio.Core/Project/GameProject.cs:48 | `No .agbproj file in {folder}, so it is not an AGB Studio project folder. Pick the folder that holds the project file.` | OK | - |
| tools/AgbStudio.Core/Project/GameProject.cs:50 | `{file} was made by a newer AGB Studio (format {n}); this one reads format 1. Update AGB Studio to open it.` | OK | - |
| tools/AgbStudio.Core/Project/GameProject.cs:85 | `{file} is empty, so it can't be read. Restore it from a backup or version control.` | OK | - |
| tools/AgbStudio.Core/Project/GameProject.cs:89 | `{file} is not valid JSON at line {line}: {reason}. Undo the hand edit, or restore the file from a backup or version control.` (`GameProject.Load` rethrows a JsonException with this text, so callers that skip broken files still catch it) | OK | - |
| tools/AgbStudio.Core/Png.cs:57 | `This is not a PNG file (maybe a renamed JPG or BMP), so it can't be read. Save it again as PNG from your paint program.` (the build names the file: it is the diagnostic file, see GameBuilder.cs `_reading`) | OK | - |
| tools/AgbStudio.Core/Png.cs:75 | `This PNG is interlaced, which AGB Studio can't read. Save it again with interlacing turned off.` | OK | - |
| tools/AgbStudio.Core/Png.cs:95 | `This PNG uses a colour format AGB Studio can't read. Save it again as an RGBA or indexed (palette) PNG.` | OK | - |
| tools/AgbStudio.Core/Png.cs:96 | `This PNG uses {n}-bit colour channels, which AGB Studio can't read. Save it again as an 8-bit RGBA or indexed PNG.` | OK | - |
| tools/AgbStudio.Core/Png.cs:153 | `This PNG is damaged: its pixel data can't be decoded. Save it again from your paint program.` | OK | - |

## CLI

`tools/agb/Program.cs` and the helpers it calls. Most commands are developer tools; `agb build` is the user-facing one.

| File:line | Text | Verdict | Suggested wording |
|---|---|---|---|
| tools/agb/Program.cs:12 | `agb needs a command. Run one of these commands:` + one `agb {command} {arguments}` line per command (build, romtest, runtime-test, sample, template, package, lint, text-preview, make-test-audio) | OK | - |
| tools/agb/Program.cs:33 | `Unknown command '{name}'. Run one of these commands:` + the command list | OK | - |
| tools/agb/Program.cs:16 | a command without its arguments: `error: agb {command} needs more arguments.` + `usage: agb {command} {arguments}`; other exceptions stay `error: {message}` | OK | - |
| tools/agb/Program.cs:71 | `error: agb romtest needs the ROM to run. usage: agb romtest <rom.gba> [--frames N] [--shot out.png] [--memmap] [--dumpsave]` | OK | - |
| tools/AgbStudio.Emu/RomTestRunner.cs:48 | `The ROM never started the AGB Studio debug channel, so no test result can be read. Check that it is a Debug build made by AGB Studio.` | OK | - |
| tools/AgbStudio.Emu/RomTestRunner.cs:49 | `Timed out after {n} frames without a pass or fail. Give it more frames (agb romtest --frames N), or check that the test ends.` | OK | - |
| tools/agb/Program.cs:104 | `The runtime test ROMs could not be built (the devkitARM output is above). Fix its first error, then run agb runtime-test again.` | OK | - |
| tools/agb/Program.cs:98 | `{n} of {total} runtime tests failed` | OK | - |
| tools/agb/Program.cs:139 | `Build failed at {step}: {n} errors above. Fix them, then run agb build again.` | OK | - |
| tools/agb/Program.cs:151 | `Unknown sample '{name}'. Samples: mini, skyhopper.` | OK | - |
| tools/agb/Program.cs:161 | `Unknown template '{id}'. Templates: {ids}.` | OK | - |
| tools/AgbStudio.Build/Content/Templates/TemplateKit.cs:14 | `The folder {dir} is not empty, and a new project could overwrite its files. Pick an empty or new folder.` | OK | - |
| tools/AgbStudio.Build/NativeBuild.cs:39 | `Cannot find the AGB Studio folder (AGB-Studio.sln or agb-studio.root) above {exe folder}, so the engine sources can't be found. Run the agb or AGB Studio that sits inside its AGB Studio folder, or reinstall AGB Studio.` | OK | - |
| tools/AgbStudio.Build/Packager.cs:34 | `dotnet publish could not build the editor (its output is above), so no package was made. Build the solution with dotnet build to see the first error, fix it, then package again.` | OK | - |
| tools/AgbStudio.Build/RuntimeTests.cs:41 | `mmutil could not make the soundbank of runtime test '{name}' from {folder}: {last mmutil line}. Check that the files there are valid MOD, XM or WAV and that devkitPro's tools folder has mmutil.` | OK | - |
| tools/agb/Program.cs:155 | `lint: {n} issues` (after one `{file}({line}): {rule}: {text}` line per issue) | OK | - |

## Runtime Console

Logged by the game through the debug channel (`runtime/src/core/debug.cpp`). The editor Play Console
(`PlayConsole.cs:40`) shows lines that start with `warning: ` as warnings without the prefix, and `assert failed` lines as
errors. `warnOnce` texts are logged once per run with a `warning: ` prefix.

| File:line | Text | Verdict | Suggested wording |
|---|---|---|---|
| runtime/src/audio/audio.cpp:190 | `warning: SFX Designer sound number %u is not in this ROM, so nothing played. Rebuild the game, or check the sound name.` | OK | - |
| runtime/src/core/assert.cpp:13 | `assert failed: %s(%d): %s. The game stopped here. (%s)` (the game stops) | OK | - |
| runtime/include/agb/memory.hpp:79 | assert `an object was used after it was destroyed; check scripts that keep a reference after Destroy()` | OK | - |
| runtime/include/agb/memory.hpp:87 | assert `an object was destroyed that was already gone; check scripts that call Destroy() twice` | OK | - |
| runtime/include/agb/memory.hpp:19 | `warning: scene memory is full (arena '%s', %lu of %lu bytes), so %lu bytes were not given. Allocate in Start, not every frame (GAE003).` | OK | - |
| runtime/include/agb/memory.hpp:72 | `warning: pool '%s' is full (%d objects), so the new object was not created. Destroy objects you no longer need.` | OK | - |
| runtime/src/core/save.cpp:69 | `warning: the save chip did not finish erasing, so this save may be lost. Check that the emulator's or cartridge's save type matches Project Settings (Save data).` | OK | - |
| runtime/src/core/save.cpp:77 | `warning: the save chip did not finish writing, so this save may be lost. Check that the emulator's or cartridge's save type matches Project Settings (Save data).` | OK | - |
| runtime/src/core/save.cpp:122 | `warning: the EEPROM save chip did not finish writing, so this save may be lost. Check that the emulator's or cartridge's save type matches Project Settings (Save data).` | OK | - |
| runtime/src/core/save.cpp:165 | `warning: save data needs %u bytes per slot but the save type has room for %lu, so saves may be lost. Use fewer save fields or slots.` (the build already refuses this, G-HW.06) | OK | - |
| runtime/src/core/save.cpp:253 | `warning: save data over 4 KB per slot is not supported, so the game was not saved. Use fewer save fields in Project Settings (Save data).` | OK | - |
| runtime/src/core/system.cpp:96 | `warning: all 4 VBlank hook slots are taken, so a new per-frame callback does not run. Remove one from native code (system::addVBlankHook).` | OK | - |
| runtime/src/core/commit.cpp:131 | `too much graphics data changed this frame, so the rest is copied next frame. Change fewer tiles or palettes per frame.` (warnOnce) | OK | - |
| runtime/src/core/task.cpp:21 | `16 tasks are already running, so the new task was not started. Let tasks finish before starting more.` (warnOnce) | OK | - |
| runtime/src/framework/actor.cpp:105 | `all 64 actors are in use, so an actor was not spawned. Destroy actors you no longer need.` (warnOnce) | OK | - |
| runtime/src/framework/bus.cpp:39 | `32 commands for the screen (damage, animations, Say ...) are already waiting, so a new one was dropped. Wait at a sync point first.` (warnOnce) | OK | - |
| runtime/src/framework/game.cpp:202 | `warning: layer %d of %s could not be loaded (see the warning above), so it is not shown.` | OK | - |
| runtime/src/framework/game.cpp:404 | `warning: the boot splash could not be shown (no image, or no free background memory). Turn it off in Project Settings (General) if this repeats.` | OK | - |
| runtime/src/framework/game.cpp:433 | `warning: load step '%s' of %s took %lu cycles; split it into smaller steps` | OK | - |
| runtime/src/framework/game.cpp:574 | `warning: frame %lu took longer than one frame (%s: %s), so the game slowed down. The Profiler shows why.` | OK | - |
| runtime/src/framework/game.cpp:585 | `warning: too many screens are open on top of each other (%d), so a new menu or screen was not opened. Close one first.` | OK | - |
| runtime/src/framework/game.cpp:641 | `warning: Scene.Load("%s"): no scene has that name, so nothing loaded. Use a scene name from the World Map.` | OK | - |
| runtime/src/framework/game.cpp:775 | `warning: Spawn %s: all %d actor slots are in use, so it was not spawned. Destroy actors you no longer need.` | OK | - |
| runtime/src/framework/game.cpp:770 | `warning: Spawn: %s has no actor or prefab named %s, so nothing spawned. Add a prefab or hidden actor of that name.` (prefabs count since the snapshot) | OK | - |
| runtime/src/framework/ui.cpp:562 | `warning: this scene's menu can't be shown because the game has no font for menus. Add a font in Font Manager and build again.` | OK | - |
| runtime/src/render/affine.cpp:43 | `warning: no free 16 KB block of background memory for a rotate / scale layer, so it is not shown. Use fewer or smaller layers in this scene.` | OK | - |
| runtime/src/render/bg.cpp:150 | `warning: background memory is full; %u tiles could not be placed, so a layer or text is missing. Use fewer layers or a smaller tileset.` | OK | - |
| runtime/src/render/bg.cpp:207 | `warning: no free background memory for a layer's map, so the layer is not shown. Use fewer layers in this scene.` | OK | - |
| runtime/src/render/bg.cpp:219 | `warning: no free background memory for a rotate / scale layer's map, so the layer is not shown. Use fewer layers in this scene.` | OK | - |
| runtime/src/render/palette.cpp:102 | `warning: all 16 {sprite\|background} palettes are in use, so some colours may look wrong. {Let sprites share a palette (Sprite Editor, Palette).\|Use fewer colours in this scene's tilesets and images.}` | OK | - |
| runtime/src/render/palette.cpp:163 | `warning: 4 palette cycles are already running, so the new one does not start. Stop one first.` | OK | - |
| runtime/src/render/sprite.cpp:102 | `sprite memory is full, so a sprite frame could not be shown. Use fewer or smaller sprites in this scene.` (warnOnce) | OK | - |
| runtime/src/render/sprite.cpp:163 | `all 128 sprites are in use, so a new sprite was not created. Destroy sprites or actors you no longer need.` (warnOnce) | OK | - |
| runtime/src/render/sprite.cpp:251 | `32 sprites are already rotated or scaled, so this one is drawn without it. Rotate or scale fewer sprites at once.` (warnOnce) | OK | - |
| runtime/src/render/sprite.cpp:404 | `warning: screen line %d needs %d sprite cycles of the %d the hardware has, so some sprites on it are hidden. Use fewer on that row.` | OK | - |
| runtime/src/render/textdb.cpp:80 | `warning: string key number %u has no text in any language, so nothing is shown. Add its text in Localization and rebuild.` | OK | - |
| runtime/src/render/textdb.cpp:131 | `warning: no free background layer for text, so it shares BG%d with the scene and may cover it. Leave one layer free in this scene.` | OK | - |
| runtime/src/script/natives.cpp:175 | `warning: Move: no actor number %ld or path number %ld in this scene, so nothing moved. Check the actor and path given to Move.` | OK | - |
| runtime/src/script/vm.cpp:36 | `warning: cutscene %s ran out of stack space (%d values), so a value was lost. Simplify its long expressions or nested calls.` | OK | - |
| runtime/src/script/vm.cpp:46 | `warning: cutscene number %u is not in this ROM, so it did not run. Rebuild the game.` and `warning: cutscenes are nested more than %d deep, so %s did not run. Call fewer cutscenes inside each other.` | OK | - |
| runtime/src/script/vm.cpp:55 | `warning: cutscene %s needs more variables than are free (%d for all nested calls), so it did not run. Use fewer variables.` | OK | - |
| runtime/src/script/vm.cpp:155 | `warning: cutscene %s calls engine function %u, which this ROM does not have, so it got 0. Rebuild the game.` | OK | - |
| runtime/src/script/vm.cpp:163 | `warning: %s waits, but this script must finish in one frame (OnLoad), so it did not wait. Use a [Cutscene] to wait.` (and the same for `WaitUntil in %s`, added since the snapshot) | OK | - |
| runtime/src/script/vm.cpp:212 | `warning: cutscene %s is damaged or from another build (unknown code %u), so it was stopped. Rebuild the game.` | OK | - |
| runtime/src/script/vm.cpp:302 | `8 cutscenes and triggers are already running, so a new one did not start. Let one finish first.` (warnOnce) | OK | - |

## Editor

`editor/AgbStudio.Editor`. Paths below are relative to that folder unless they start with `tools/`.

| File:line | Text | Verdict | Suggested wording |
|---|---|---|---|
| Services/ProjectServices.cs:81 | `{tool} is not at {path}. Set the devkitPro folder in Preferences › Paths.` | OK | - |
| Windows/Settings/ToolchainHelp.cs:88, 101, 115 | title `devkitPro not found` + `AGB Studio builds games with devkitPro (devkitARM, libtonc and maxmod). It was not found in these folders:` + folder list + `Install devkitPro with its installer, or set the folder it is in under Preferences › Paths.` + buttons | OK | - |
| Windows/Settings/ToolchainHelp.cs:89, 115 | title `A build tool is missing` + `The build could not start because these tools are not where the devkitPro folder says:` + tool list + "Open Preferences · Paths" | OK | - |
| Windows/Build/BuildRomDialog.cs:75 | `No project is open. Open or create a project, then build.` (Build ROM log) | OK | - |
| Windows/Build/BuildRomDialog.cs:76, Services/ProjectServices.cs:88 | `A build is already running. Wait for it to finish, then try again.` | OK | - |
| Windows/Build/BuildRomDialog.cs:217-218 | `Build failed · {n} errors` + failed and skipped steps + clickable error lines + `Fix them, then build again.` + "Go to first error" (T-D23) | OK | - |
| Windows/Build/BuildRomDialog.cs:105 | status bar `Build failed · 1 error` / `Build failed · {n} errors` (`Build failed` if no error was reported); Build ROM · Failed opens with it and carries the errors and the fix | OK | - |
| Windows/Build/BuildRomDialog.cs:143 | `Could not write {file}: {reason}. Pick another folder, or close programs that use the file, then export again.` | OK | - |
| Windows/Play/PlayController.cs:206-207 | toast `Build failed · {n} errors. The Console shows what to fix; then press Play again.` (`Build failed. The Console ...` when the build did not start; not a Figma text, so `· see Console` was replaced) | OK | - |
| Windows/Play/PlayController.cs:221-223, Windows/SceneEditor/ConsolePanel.cs:89-91 | `Build failed at {step}: {n} errors above. Fix them, then press Play again.` (Play; Build ROM builds end `... then build again.`; with no error reported: `Build failed at {step}. Build › Build ROM shows which step stopped and why.`) | OK | - |
| Windows/Play/PlayController.cs:173 | `No project is open. Open a project, then press Play.` (Console) | OK | - |
| Windows/Play/PlayController.cs:250-253 | Run in mGBA / Play with a ROM the emulator refuses: the Console line is the core's own text (`The emulator could not start {rom}: it is not a GBA ROM it can run. ...`, LibretroCore.cs:114), so the file is named once; a ROM that can't be read: `The emulator could not read {rom}: {reason}. Build the ROM again, or close programs that use it.`; toast `Could not run {rom}. The Console says why and what to do.` (added after the review as `{rom}: {e.Message}` and `Could not run {rom} · see Console`) | OK | - |
| Windows/Play/PlayController.cs:581 | `Hot reload: {name} rebuilt; the game restarted` | OK | - |
| Windows/Build/BuildReportPage.cs:93 | `Build the ROM first.` | OK | - |
| Windows/Build/BuildReportPage.cs:111 | status `Build failed · 1 error` / `Build failed · {n} errors` (Figma's `Build OK` is the success form; the history line under it shows the first error and Build again is in the toolbar) | OK | - |
| tools/AgbStudio.Emu/LibretroCore.cs:114 | `The emulator could not start {rom}: it is not a GBA ROM it can run. Build the ROM again; if it still fails, the file is damaged.` | OK | - |
| tools/AgbStudio.Emu/LibretroCore.cs:180 | `The emulator could not make a save state of the running game. Try again; if it keeps failing, restart Play Mode.` | OK | - |
| tools/AgbStudio.Emu/LibretroCore.cs:187 | `The emulator could not load this save state; it may come from another build of the game. Make a new save state.` | OK | - |
| Windows/Build/FlashDialog.cs:87 | `No device found` / `Connect a GBxCart RW or insert a flash cart's SD card` | OK | - |
| Windows/Build/FlashDialog.cs:115 | `Can't talk to {device}` / `{reason} Check the cable, then choose the device again.` (earlier editor pass) | OK | - |
| Windows/Build/CartridgeDevices.cs:93 | `No SD card` / `{reason}. Insert the flash cart's SD card, then choose the device again.` | OK | - |
| Windows/Build/CartridgeDevices.cs:160 | `GBxCart RW not responding` / `Check the USB cable and the firmware` | OK | - |
| Windows/Build/CartridgeDevices.cs:169, 175 | `No cartridge` / `Insert a cartridge into the GBxCart RW` | OK | - |
| Windows/Build/CartridgeDevices.cs:211 | `GBxCart RW not available` / `{reason}`, where the reason is always one of the serial port texts of the next three rows, which say why and what to do | OK | - |
| Windows/Build/CartridgeDevices.cs:257 | `{port} can't be opened (Windows error {code}); another program may be using it. Close that program and plug the GBxCart RW in again.` (earlier editor pass) | OK | - |
| Windows/Build/CartridgeDevices.cs:265 | `The device on {port} does not work at the GBxCart RW speed ({baud} baud). Check that it is a GBxCart RW and update its firmware.` (earlier editor pass) | OK | - |
| Windows/Build/CartridgeDevices.cs:272 | `Lost the connection to the GBxCart RW while sending data. Check the USB cable and try again.` (earlier editor pass) | OK | - |
| Windows/Build/FlashDialog.cs:185 | `Verify failed: the cartridge does not match the ROM. Flash again.` | OK | - |
| Windows/Build/FlashDialog.cs:196 | `Flashing cancelled. Flash again before playing the cartridge.` | OK | - |
| Windows/Build/FlashDialog.cs:204 | toast `Flashing failed: {reason} The cartridge may now hold a broken game; flash again.` (earlier editor pass) | OK | - |
| Windows/Scripts/ScriptEditorPage.cs:407, Windows/Launcher/LauncherService.cs:159 | status `devkitARM not found · set it in Preferences › Paths` (Script Editor) / `devkitARM not found · set it in Settings › Paths` (Launcher, whose Settings opens Preferences); the found form `{version} found` is Figma's | OK | - |
| Windows/Settings/PreferencesDialog.cs:315 | `{tools} not found here · Browse to the devkitPro folder or install devkitPro` (under the devkitPro folder field) | OK | - |
| Windows/Settings/ProjectSettingsDialog.cs:324 | `{tools} found · {tools} not found · set the devkitPro folder, or install devkitPro` (next to "Change in Preferences…" / "Change…") | OK | - |
| Windows/Settings/PreferencesDialog.cs:275 | `{editor} not found · Browse to its .exe or install it` (Paths › Script editor; the found form keeps Figma's `{editor} found · used when Script editor is External`) | OK | - |
| Windows/Import/AssetImporter.cs:120 | `Has {n} colours; a GBA palette holds at most 256. Remove colours in your paint program until 256 or fewer are left, then import it again.` | OK | - |
| Windows/Import/AssetImporter.cs:133 | `{EXT} is not supported on GBA. Use .wav for sound effects or .mod / .xm for music.` + button "How to convert" | OK | - |
| Windows/Import/ImportErrorsDialog.cs:91 | `Open {file} in an audio editor and export it as .wav (sound effect, 11 kHz mono), or rebuild the song in a tracker and save it as .xm or .mod (music).` | OK | - (it is help, but shows as an error toast) |
| Windows/Import/AssetImporter.cs:140 | `AGB Studio can't import {ext} files. Save it as one of the kinds it imports: .png, .aseprite, .tmx, .wav, .mod, .xm, .pal, .gpl, .ttf, .otf, .bdf or .cs.` | OK | - |
| Windows/Import/AssetImporter.cs:148, 152, 156 | `{file} can't be read: {reason}`, where the reader's own text (PNG, WAV, MOD / XM, Tiled, palettes) says what to do; a damaged file: `{file} can't be read: it is damaged or not the kind of file its name says ({reason}). Save it again from the program that made it and import the new file.`; a file system error: `{file} can't be read: {reason}. Check that the file is still there and no other program has it open, then import it again.` | OK | - |
| Windows/Import/AssetImporter.cs:186, 215 | `Frames are {w} × {h}; the biggest GBA sprite is 64 × 64. Make the sheet a grid of frames of 8, 16, 32 or 64 pixels a side in your paint program, then import it again.` (an Aseprite file, 215: `... Make the canvas 64 × 64 or smaller in Aseprite, then import it again.`) | OK | - |
| Windows/Import/AssetImporter.cs:238 | `The map file is empty. Save the map again in Tiled.` (after `{file} can't be read: `, so the name is not repeated) | OK | - |
| Windows/Import/AssetImporter.cs:259 | `Layer "{layer}" has no tiles. Save the map again in Tiled.` (after `{file} can't be read: `) | OK | - |
| Windows/Import/AssetImporter.cs:243 | `Tiled {compression} compression is not supported; save the map as CSV.` | OK | - |
| Windows/Import/AssetImporter.cs:288 | `Tiles are {n} px; AGB Studio maps use 16 × 16 tiles. Use a 16 × 16 tileset in Tiled and save the map again.` | OK | - |
| Windows/Import/AssetImporter.cs:289 | `The map has no tile layers. Add one in Tiled and save the map again.` | OK | - |
| Windows/Import/AssetImporter.cs:290 | `The map has {n} layers; the GBA shows at most 4 backgrounds. Merge or delete layers in Tiled until 4 are left, then save the map again.` | OK | - |
| Windows/Audio/WavFile.cs:16 | `It is not a WAV file inside, only named like one. Export it again as .wav from your audio editor.` (the callers put `{file} can't be read: ` or `{file} could not be played: ` in front) | OK | - |
| Windows/Audio/WavFile.cs:38 | `The WAV file has no sound in it. Export it again from your audio editor.` | OK | - |
| Windows/Audio/WavFile.cs:39 | `The WAV file is compressed, and AGB Studio reads only uncompressed (PCM or float) WAV. Export it again as 16-bit PCM WAV.` | OK | - |
| Windows/Audio/WavFile.cs:56 | `{bits}-bit WAV files are not supported; AGB Studio reads 8-, 16-, 24- and 32-bit PCM and 32-bit float. Export it again as 16-bit PCM WAV.` | OK | - |
| Windows/Audio/TrackerModule.cs:86 | `It is not a MOD or XM song inside (too short for one). Save it again from your tracker as .mod or .xm.` | OK | - |
| Windows/Audio/TrackerModule.cs:154 | `This is a MOD variant AGB Studio can't read (signature '{sig}'). Save it again as a ProTracker .mod or as .xm.` (earlier editor pass) | OK | - |
| Windows/Import/ImportErrorsDialog.cs:109 | `Open a project before importing.` | OK | - |
| Windows/Import/ImportErrorsDialog.cs:127-128 | `{file} could not be imported: {reason}. Close programs that use the file or the project folder, then import it again.` (a file the reader refuses keeps the reader's own text as the fix; the Console line adds `Import: `) | OK | - |
| Windows/Import/ImportErrorsDialog.cs:37 | hint `Files with errors are skipped.` | OK | - |
| Windows/Sprite/SpriteModel.cs:184 | `{file}: only PNG sheets can be imported.` | OK | - |
| Windows/Launcher/LauncherPage.cs:192 | `No .agbproj file in {folder}, so it is not an AGB Studio project folder. Pick the folder that holds the project file.` (the words of GameProject.cs:48) | OK | - |
| Windows/Launcher/LauncherPage.cs:519 | `The GBA hardware guide is missing from this install. Reinstall AGB Studio to get it back.` | OK | - |
| Windows/Launcher/LauncherService.cs:294 | `The {sample} sample is missing from this install. Reinstall AGB Studio to get the samples back.` | OK | - |
| Windows/Launcher/LauncherService.cs:319 | `{project} was not found in {folder}; it was moved or deleted. Use Open… to pick its new folder, or remove it from the list.` | OK | - |
| Windows/Launcher/LauncherService.cs:356-357 | `Could not duplicate {project}: {reason}. Check that the drive has free space and no program is using the project's files, then try again.` (a damaged project file shows its own text, see Project files) | OK | - |
| Windows/Launcher/LauncherService.cs:389-390 | `Could not rename {project}: {reason}. Close programs that use its project file (.agbproj), then try again.` (a damaged project file shows its own text) | OK | - |
| Windows/Launcher/LauncherService.cs:417 | `Could not move {folder} to the Recycle Bin. Close programs that use files in it, then try again.` (earlier editor pass) | OK | - |
| Windows/Launcher/NewProjectDialog.cs:254 | `Could not create the project: {reason}. Pick another location or project name, then press Create Project again.` | OK | - |
| Windows/Launcher/ProjectCreator.cs:132 | `The project needs a name.` | OK | - |
| Windows/Launcher/ProjectCreator.cs:133 | `ROM title: 12 characters max (A-Z, 0-9).` | OK | - |
| Windows/Launcher/ProjectCreator.cs:134 | `Game code: 4 characters (A-Z, 0-9).` | OK | - |
| Windows/Launcher/NewProjectDialog.cs:191, Windows/Launcher/ProjectCreator.cs:137 | help `{folder} is not empty · change the project name or pick another location`; ProjectCreator (reached only when the folder fills up after that check): `{folder} is not empty, and a new project needs an empty folder.` inside the toast of NewProjectDialog.cs:254, which says what to do | OK | - |
| Windows/Launcher/ProjectCreator.cs:138 | `{folder} is the top of a drive, and a project needs a folder of its own.` (inside the toast of NewProjectDialog.cs:254, which says to pick another location) | OK | - |
| Windows/Launcher/NewProjectDialog.cs:191 | `A folder outside {devkitPro} is safer for updates` | OK | - |
| Windows/Launcher/ProjectCreator.cs:249 (toast via LauncherService.cs:278) | `The project was made without the Thai font: Noto Sans Thai is missing from this install. Add a Thai font later in the Font Manager, or reinstall AGB Studio.` | OK | - |
| Windows/Launcher/ProjectCreator.cs:285 (toast via LauncherService.cs:278) | `The project was made, but without a Git repository (git init failed: {first line}). Run git init in the project folder to add one.` (earlier editor pass) | OK | - |
| Windows/Launcher/ProjectCreator.cs:289 (toast via LauncherService.cs:278) | `The project was made, but without a Git repository: Git was not found. Install Git and run git init in the project folder if you want one.` | OK | - |
| Windows/Launcher/OpeningProjectDialog.cs:245-246 | `{file} of {project} could not be read. Its card in Assets says why; fix the file or import it again.` / `{n} assets of {project} could not be read (first: {file}). Their cards in Assets say why; fix those files or import them again.` | OK | - |
| Windows/Launcher/OpeningProjectDialog.cs:259-260 | `Could not open {project}: {reason}`, where the reason is a Project files text (which now says what to do); a file system error: `Could not open {project}: {reason}. Check that the project folder is still there and no other program has it open, then open it again.` | OK | - |
| Windows/Launcher/ProjectContextMenu.cs:63 | `The project file could not be read, so its scenes, assets and scripts are not counted. The whole folder still goes to the Recycle Bin.` (Delete project dialog; the line now wraps) | OK | - |
| Windows/Launcher/ProjectContextMenu.cs:57, 73 | `Delete {name}?` + `The project folder and everything in it is moved to the Recycle Bin and removed from the recent list:` | OK | - |
| Shell/Dialogs.cs:107, 124 | `Save changes before exiting?` + `{n} files have changes that will be lost if you don't save them.` | OK | - |
| Windows/SceneEditor/SceneCommands.cs:57 | `All four backgrounds (BG0-BG3) are in use, and the GBA has no more. Delete a background layer before adding another.` | OK | - |
| Windows/SceneEditor/SceneCommands.cs:107 | `{actor} has no C# script behaviour to edit. Add one with Add Component in the Inspector first.` | OK | - |
| Windows/SceneEditor/SceneCommands.cs:218 | `{type} is a built-in behaviour; it has no C# script.` | OK | - |
| Windows/SceneEditor/AssetActions.cs:162 | asset card `can't read · {reason}` (`broken file` for a JSON error; earlier editor pass) | OK | - |
| Windows/SceneEditor/AssetActions.cs:365-366 | `{asset}: {problem}` + `Reimported {asset} · {n} warnings` | OK | - (problem texts are judged under Build pipeline) |
| Windows/Scripts/ExternalEditors.cs:244 | `{editor} was not found. Set the Script editor in Preferences › Paths.` | OK | - |
| Windows/Scripts/ExternalEditors.cs:258 | `Could not start {exe}: {reason}. Check the Script editor path in Preferences › Paths.` | OK | - |
| Windows/Scripts/ScriptWorkspace.cs:259 | `Script check failed: {reason}. Close programs that use the project's build folder, then save the script again to check it.` (the check covers all scripts at once, so no one file can be named; the reason is a file system error) | OK | - |
| Windows/Scripts/ScriptWorkspace.cs:392 | `{file} changed on disk; your unsaved edits are kept` | OK | - |
| Windows/LogicGraph/LogicGraphPage.cs:232-235 | status `C# generation failed · see Generated C#`; the Generated C# pane shows `// No C# could be made from this graph:`, `// {reason}`, `// Fix that node setting or wire in the graph; this code updates as you edit.` (Figma's `Compiled to C#` is the success form) | OK | - |
| Windows/Database/DatabasePage.cs:242 | `{field} must be a whole number.` | OK | - |
| Windows/Database/DatabasePage.cs:245 | `{field} must be a number.` | OK | - |
| Windows/Database/DatabasePage.cs:251 | `“{value}” is not a valid id. An id uses only letters A-Z, digits and _, and does not start with a digit.` | OK | - |
| Windows/Database/DatabasePage.cs:252 | `{value} is already a row of {table}.` | OK | - |
| Windows/Database/AddFieldDialog.cs:172 | `Enter a field name.` | OK | - |
| Windows/Database/AddFieldDialog.cs:173, Windows/Database/NewTableDialog.cs:117 | `Use letters, digits and _ (a C# name).` | OK | - |
| Windows/Database/AddFieldDialog.cs:174 | `Select a table first.` | OK | - |
| Windows/Database/AddFieldDialog.cs:175 | `{table} already has a field named {field}.` | OK | - |
| Windows/Database/AddFieldDialog.cs:176 | `Add at least one value.` | OK | - |
| Windows/Database/AddFieldDialog.cs:177 | `The default must be a whole number.` | OK | - |
| Windows/Database/AddFieldDialog.cs:178 | `The default must be a number.` | OK | - |
| Windows/Database/NewTableDialog.cs:116 | `Enter a table name.` | OK | - |
| Windows/Database/NewTableDialog.cs:118, Windows/Localization/AddLanguageDialog.cs:109, Windows/SceneEditor/SceneDialogs.cs:77 | `Open a project first.` | OK | - |
| Windows/Database/NewTableDialog.cs:119 | `A table named {name} already exists.` | OK | - |
| Windows/Dialogue/NewSpeakerDialog.cs:160 | `Enter a name.` | OK | - |
| Windows/Dialogue/NewSpeakerDialog.cs:160 | `Open a dialogue first.` | OK | - |
| Windows/Dialogue/NewSpeakerDialog.cs:161 | `{name} is already a speaker of this dialogue.` | OK | - |
| Windows/Localization/AddLanguageDialog.cs:109 | `Choose a language.` | OK | - |
| Windows/Localization/AddLanguageDialog.cs:109 | `Choose the language to translate from.` | OK | - |
| Windows/Database/ImportCsvDialog.cs:213 | `{n} rows have warnings and will use the default value.` | OK | - |
| Windows/SceneEditor/SceneDialogs.cs:78 | `Use letters, digits and _ (start with a letter).` | OK | - |
| Windows/SceneEditor/SceneDialogs.cs:79 | `A scene named {name} already exists.` | OK | - |
| Windows/SceneEditor/SceneDialogs.cs:80 | `Width is 15 to 256 tiles.` | OK | - |
| Windows/SceneEditor/SceneDialogs.cs:81 | `Height is 10 to 256 tiles.` | OK | - |
| Windows/Scripts/NewScriptDialog.cs:122 | `Enter a class name.` | OK | - |
| Windows/Scripts/NewScriptDialog.cs:124 | `Use letters, digits and _ (a C# class name).` | OK | - |
| Windows/Scripts/NewScriptDialog.cs:125, Windows/Scripts/ScriptWorkspace.cs:303 | `{name}.cs already exists.` | OK | - |
| Windows/Settings/PreferencesDialog.cs:82 | `Undo history must be 1 to 10000 steps.` | OK | - |
| Windows/Settings/PreferencesDialog.cs:83 | `Code font must be 8 to 32 px.` | OK | - |
| Windows/Settings/PreferencesDialog.cs:84 | `Tab size must be 1 to 16.` | OK | - |
| Windows/Settings/PreferencesDialog.cs:85 | `Fast-forward: a key and a speed, like Tab · 4×.` | OK | - |
| Windows/Settings/ProjectSettingsModel.cs:167 | `Game title is empty.` | OK | - |
| Windows/Settings/ProjectSettingsModel.cs:168 | `Version must be major.minor.patch, like 1.0.3.` | OK | - |
| Windows/Settings/ProjectSettingsModel.cs:169 | `ROM title: up to 12 capital letters, digits or _.` | OK | - |
| Windows/Settings/ProjectSettingsModel.cs:170 | `Game code must be 4 capital letters or digits.` | OK | - |
| Windows/Settings/ProjectSettingsModel.cs:171 | `Maker must be 2 capital letters or digits.` | OK | - |
| Windows/Settings/ProjectSettingsModel.cs:173 | `Action names must be one word (letters, digits, _).` | OK | - |
| Windows/Settings/ProjectSettingsModel.cs:173 | `{button} and {button} both use the action name {name}. Rename one of them.` | OK | - |
| Windows/Settings/ProjectSettingsModel.cs:177 | `Save field "{name}" must be one word (letters, digits, _).` | OK | - |
| Windows/Settings/ProjectSettingsModel.cs:178 | `Save field "{name}": "{type}" is not a type. Use int, byte, short, bool, fixed, fixed2, bitset N or Table[N].` (kept short: footer hints do not wrap) | OK | - |
| Windows/Settings/ProjectSettingsModel.cs:180 | `Slots must be 1 to 16.` | OK | - |
| Windows/Settings/ProjectSettingsModel.cs:185 | `{n} slots of {size} need {total}, but {chip} holds {capacity}. Use fewer slots or fields, or a bigger save type.` (for example `3 slots of 12000 B need 36000 B, but SRAM holds 32 KB.`) | OK | - |
| Windows/Settings/ProjectSettingsModel.cs:183 | `Warn ROM must be 1 to 32 MB.` | OK | - |
| Windows/Settings/ProjectSettingsModel.cs:188 | `Build · Output folder is empty. Enter a folder for the ROM, like build/.` (footer) | OK | - |
| Windows/Settings/ProjectSettingsModel.cs:189 | `Scripting · Generated folder is empty. Enter a folder, like build/gen.` (footer) | OK | - |
| Windows/Settings/ProjectSettingsModel.cs:186 | `Namespace must be a C# name, like SkyHopper.` | OK | - |
| Windows/Build/ExportRomDialog.cs:126 | `Enter a file name.` when the name is empty; `Remove \ / : * ? " < > \| from the file name; Windows does not allow them.` when it has those characters | OK | - |
| Windows/Build/ExportRomDialog.cs:126 | `Up to 12 letters, digits or spaces.` | OK | - |
| Windows/Build/ExportRomDialog.cs:127 | `4 letters or digits.` | OK | - |
| Windows/Build/ExportRomDialog.cs:128 | `major.minor.patch, e.g. 1.0.3` | OK | - |
| Windows/Build/ExportRomDialog.cs:129 | `Choose a folder.` | OK | - |
| Windows/Audio/AudioPage.cs:215-217 | `{file} could not be read: {reason}`, where the reason is the MOD / XM reader's own text with its fix; a file system error: `... {reason}. Close programs that use it, then select it again.`; a damaged file: `{file} could not be read: it is damaged ({reason}). Save it again from your tracker and import it again.` | OK | - |
| Windows/Audio/AudioPage.cs:419-420 | `{file} could not be played: {reason}`, where the reason is the WAV reader's own text with its fix; a file system error: `... {reason}. Close programs that use it, then play it again.` (the error comes from reading the file, so the suggested "check the audio output" did not fit) | OK | - |
| Windows/Audio/SfxDesignerPage.cs:284 | `{file} could not be read: {reason}. An empty sound is shown instead; fix the file before you edit here, or the empty sound is saved over it.` | OK | - |
| Windows/Palette/PaletteManagerPage.cs:368 | `Could not import {file}: {reason}. Palettes must be JASC .pal or GIMP .gpl files.` (earlier editor pass) | OK | - |
| Windows/Sprite/SpriteEditorPage.cs:376-377 | `Could not open {file}: {reason}`, where the reason is the PNG / Aseprite reader's own text with its fix; a file system error: `... {reason}. Close programs that use it, then open it again.` | OK | - |
| Windows/Sprite/SpriteEditorPage.cs:912-913 | `Could not import {file}: {reason}`, with the reader's own text; otherwise `... {reason}. Close programs that use it, or save it again as PNG, then import it again.` | OK | - |
| Windows/Menus/AboutDialog.cs:45 | `The third-party notices (THIRD_PARTY.md) are missing from this install. Reinstall AGB Studio to restore them.` | OK | - |
| Windows/Menus/MenuActions.cs:108 | `Start the game to take a screenshot.` | OK | - |
| Windows/Menus/MenuActions.cs:146 | `Could not clean the build folder: {reason}. Stop the game and close programs that use files in the build folder, then try again.` | OK | - |
| Windows/Menus/EditorLinks.cs:70 | `Could not check for updates: no update source is set. Add an updateFeed address to editor.json next to AGBStudio.exe.` | OK | - |
| Windows/Menus/EditorLinks.cs:75 | `Could not check for updates: {reason}. Check the internet connection and the updateFeed address in editor.json, then try again.` | OK | - |
| Windows/Menus/EditorLinks.cs:85 | `Could not check for updates: the update feed has no version number. Check the updateFeed address in editor.json.` | OK | - |
| Windows/Menus/EditorLinks.cs:92 | `Could not check for updates: the update feed is not JSON. Check the updateFeed address in editor.json.` | OK | - |
| Shell/WindowService.cs:177 | `{name} is not in this version of AGB Studio yet.` (the design ids and caller names are no longer shown; earlier editor pass) | OK | - |

## Not checked

- devkitARM g++ / ld messages: parsed by `GameBuilder.ParseGccLine` (`tools/AgbStudio.Build/Pipeline/GameBuilder.cs:1107`)
  and `GccLogMapper` (`tools/AgbStudio.Scripting/ScriptDiagnostic.cs`) and shown as they come; only the file and line
  mapping is ours.
- Roslyn C# compiler messages (CS codes) passed through `ScriptDiagnostic.From`; only the `';' expected` rewrite is ours.
- Third-party and .NET texts appended after `{e.Message}` or a raw log: mmutil, git, dotnet publish, System.IO,
  System.Text.Json, Win32 errors. The rows above judge only the part AGB Studio writes.
- Text built from many parts at runtime with no literal to review: Build ROM log lines (`BuildStream.LogText`), Console
  formatting of diagnostics (`ConsolePanel.cs:63`), step details in Build ROM, Build Report history entries.
- Runtime: `logf` cuts lines at 160 characters (`runtime/src/core/debug.cpp:41`), so long names can cut the end of a
  warning; logs are compiled out of Release builds unless "Strip debug log" is off (`AGB_KEEP_LOGS`). Runtime test
  output (`TEST PASS`, `TEST FAIL: %s`), breakpoint and info lines were not judged.
- Internal invariant messages that appear only when AGB Studio itself is broken: `OpcodeTable.cs:30/37/93`,
  `ScriptAssembler.cs:28/118`, `Lz77.cs:75`, `Shell/EditorServices.cs:83`, `Shell/WindowService.cs:62/65`,
  `Commands/CommandRegistry.cs:21/31`, `Windows/LogicGraph/LogicGraphPage.cs:346`, `Windows/Audio/DmgSound.cs:303`,
  `Graphics/SvgImage.cs:32`, `Windows/Play/ElfSymbols.cs:37`, and the `No project is open.` / `No font is open.` /
  `No map is open.` guards thrown from editor services.
- `GraphCodeGen.Parse` messages (`tools/AgbStudio.Scripting/Graph/GraphCodeGen.cs:243-505`): not called from the
  editor or the build at this snapshot.
- `tools/AgbStudio.Build/Audio/AudioWriters.cs:73` (ModWriter, used only for generated test and sample audio).
- `LibretroCore.cs:114` now reaches the Console through the catch in `PlayController.Run` (Editor row
  PlayController.cs:250-253); where `LibretroCore.cs:180/187` (save states) surface was not traced end to end.
- The analyzer help links point to `https://agbstudio.local/docs/scripting/rules#` (`GaeRules.cs:11`), which is not a
  public address; whether the editor shows them was not checked.
- No crash or unhandled-exception message exists in the editor at this snapshot (G-ERR.06), so there was nothing to review.
- Strings were judged from the code. The editor was not run, so whether each text fits its box, wraps, or is hidden by
  another message was not verified.
- The editor component `Gallery` and the Script Editor terminal panel (process output and `exit code {n}`) were skipped.
