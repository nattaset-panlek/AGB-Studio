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
| Build pipeline | 52 | 12 | 33 | 7 |
| Scripting compiler | 103 | 53 | 44 | 6 |
| Analyzer | 18 | 9 | 8 | 1 |
| Project files | 9 | 0 | 6 | 3 |
| CLI | 16 | 2 | 12 | 2 |
| Runtime Console | 45 | 1 | 25 | 19 |
| Editor | 152 | 72 | 65 | 15 |
| **Total** | **395** | **149** | **193** | **53** |

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
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:158 | `{e.Message}` (any exception thrown in a build step becomes the error text, with no file) | POOR (no file, no fix; raw .NET or parser text such as the PNG and JSON errors under Project files) | `{step}: {file} could not be read: {reason}. Fix or export the file again, then build again.` |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:404 | `Audio conversion failed: {mmutil output}` | POOR (raw mmutil log, no file, no fix) | `Audio conversion failed for {file}: {first mmutil line}. Open it in a tracker or audio editor, save it again, and build again.` |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:472 | `Strings for '{lang}' need a font: add one in Font Manager.` | OK | - |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:475 | `Language '{lang}' has no font set; using {font}.` | PARTIAL (no fix) | `Language '{lang}' has no font set, so {font} is used. Pick a font for it in Localization · Languages.` |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:483 | `{n} strings missing in {language}.` | PARTIAL (no fix) | `{n} strings missing in {language}. Translate them in Localization ("Missing only"), or set a fallback language.` |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:539 | `{n} C# scripts were not compiled: the scripting compiler is not available in this build.` | PARTIAL (no fix) | `{n} C# scripts were not compiled: this build tool has no script compiler. Build from the AGB Studio editor or with agb build.` |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:621 | `Scene {scene} needs {n} KB of background VRAM; the GBA has 64 KB. Use fewer layers, a smaller tileset, or image layers with more repeated tiles.` | OK | - |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:624 | `Scene {scene} needs {n} background palettes; {free} are free (of 16, two hold the menus and text). Reduce the colours of its tileset or images (Import Asset "Reduce to 16").` | OK | - |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:662 | `Scene menus need a font: add one in Font Manager.` | OK | - |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:706 | `Scene {scene} uses map '{map}', which does not exist.` | PARTIAL (no fix) | `Scene {scene} uses map '{map}', which does not exist. Pick another map in Scene Settings, or restore maps/{map}.map.` |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:715 | `Map {map} uses tileset '{tileset}', which is not in assets/tilesets.` | PARTIAL (no fix) | `Map {map} uses tileset '{tileset}', which is not in assets/tilesets. Import the tileset again, or pick another one for the map.` |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:725 | `Layer {layer} has {n} cells; the map is {w}×{h}.` | POOR (no why or fix; "cells" is an internal word) | `Layer {layer} of map {map} has {n} tiles, but a {w}×{h} map needs {w*h}. The .map file is damaged; restore it from a backup or version control.` |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:733 | `Layer {layer} uses image '{image}', which is not in assets/images.` | PARTIAL (no fix) | `Layer {layer} uses image '{image}', which is not in assets/images. Import the image again, or pick another one for the layer.` |
| tools/AgbStudio.Build/Graphics/AffineConverter.cs:42 (via GameBuilder.cs:747) | `Layer {layer}: An affine layer holds at most 256 different 8x8 tiles; the rest show as empty.` | PARTIAL (no fix; "affine" is jargon) | `Layer {layer}: a rotate / scale layer holds at most 256 different 8×8 tiles; the rest show as empty. Use fewer different tiles, or make it a normal tile layer.` |
| tools/AgbStudio.Build/Graphics/AffineConverter.cs:55 (via GameBuilder.cs:747) | `Layer {layer}: An affine layer is at most 64 x 64 tiles; the rest is cut off.` | PARTIAL (no fix; "tiles" means 16×16 map tiles here, unclear) | `Layer {layer}: a rotate / scale layer is at most 64 × 64 map tiles (1024 × 1024 px); the rest is cut off. Make the map smaller or use a normal layer.` |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:788 | `{actor}: {behaviour} has no field '{field}'.` | PARTIAL (no why, no fix) | `{actor}: {behaviour} has no field '{field}' (renamed or removed), so the value is ignored. Set the field again in the Inspector.` |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:797 | `{actor}: {behaviour}.{field} = '{value}' is not a valid value.` | PARTIAL (no why: the expected type) | `{actor}: {behaviour}.{field} = '{value}' is not a {type}. Enter a {type} in the Inspector.` |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:807 | `{actor}: behaviour '{type}' was not found (not a built-in and no C# script defines it).` | PARTIAL (no fix) | `{actor}: behaviour '{type}' was not found (not a built-in and no C# script defines it). Remove it from the actor, or create a script with class {type}.` |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:825 | `{actor} uses sprite '{sprite}', which is not in assets/sprites.` | PARTIAL (no fix) | `{actor} uses sprite '{sprite}', which is not in assets/sprites. Import the sprite again, or pick another one in the Inspector.` |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:840 | `{actor}: string key '{key}' is not in the Localization tables.` | PARTIAL (no fix) | `{actor}: string key '{key}' is not in the Localization tables. Add the key in Localization, or change the text in the Inspector.` |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:852 | `Link from '{actor}': no actor with that name in {scene}.` | PARTIAL (no fix) | `Link from '{actor}' in {scene}: no actor has that name (renamed or deleted?). Draw the link again in the World Map.` |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:853 | `Link to '{scene}': no scene with that name.` | PARTIAL (no fix) | `Link to '{scene}': no scene has that name (renamed or deleted?). Draw the link again in the World Map.` |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:857 | `Link {from} → {to}: the 'Only if' condition needs the scripting compiler; the link is always allowed.` | PARTIAL (no fix) | `Link {from} → {to}: the 'Only if' condition was not compiled, so the link is always allowed. Build from the editor or with agb build.` |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:868 | `Scene {scene}: music '{track}' is not in assets/audio/music.` | PARTIAL (no fix, no consequence) | `Scene {scene}: music '{track}' is not in assets/audio/music, so the scene plays no music. Import the track again or pick another in Scene Settings.` |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:894 | `Menu item '{text}' is not a string key; only ASCII text can be drawn without one.` | PARTIAL (no fix) | `Menu item '{text}' is not a string key; only ASCII text can be drawn without one. Add it as a string key in Localization, or use ASCII text.` |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:899 | `Menu item '{text}' loads '{scene}': no scene with that name.` | PARTIAL (no fix) | `Menu item '{text}' loads '{scene}', but no scene has that name. Pick an existing scene for the item.` |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:904 | `A scene menu has 1 to 16 items.` | PARTIAL (no what: which scene, how many items) | `The menu of {scene} has {n} items; a scene menu holds 1 to 16. Add or remove items.` |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:913 | `The menu of {id} does not fit on screen; move it up or use fewer items.` | OK | - (optional: `{id}` is the C++ name of the scene; show the scene name) |
| tools/AgbStudio.Build/Pipeline/GameBuilder.cs:1070 | `devkitARM build failed:\n{full build log}` | POOR (the whole raw log is one error; no step, file or fix) | `Compiling or linking failed and no error line could be read from devkitARM. The full log is in the Console. If you changed native code, check it; otherwise try Build › Clean Build.` |
| tools/AgbStudio.Build/Pipeline/BuildReport.cs:115 | `ROM is {n} MB, over the {limit} MB warning set in Project Settings · Build.` | PARTIAL (no fix) | `ROM is {n} MB, over the {limit} MB warning set in Project Settings · Build. See Build Report · Largest assets for what to shrink, or raise the limit.` |
| tools/AgbStudio.Build/Pipeline/BuildReport.cs:117 | `IWRAM uses {n} KB of 32 KB; less than 8 KB is left for the stack.` | PARTIAL (jargon "IWRAM", no consequence, no fix) | `Fast RAM (IWRAM) uses {n} KB of 32 KB; less than 8 KB is left for the stack, so deep calls can crash the game. Use fewer or smaller global arrays.` |
| tools/AgbStudio.Build/Pipeline/BuildReport.cs:143 | `OBJ tiles exceed 32 KB; fewer frames of large sprites can be resident.` | POOR (jargon "OBJ", "resident"; no fix) | `The sprites of this scene need more than 32 KB of sprite memory, so not all frames can be loaded at once. Use fewer or smaller sprite sheets in this scene.` |
| tools/AgbStudio.Build/Pipeline/BuildReport.cs:144 | `More than 16 OBJ palettes: share palettes between sprites.` | PARTIAL (jargon "OBJ", no why) | `This scene uses more than 16 sprite palettes (the GBA has 16). Share palettes between sprites (Import Asset "Share palette").` |
| tools/AgbStudio.Build/Fonts/TextCodegen.cs:62 | `{key}: '{char}' (U+{code}) is not in font {font}.` | PARTIAL (no consequence, no fix) | `{key}: '{char}' (U+{code}) is not in font {font}, so it is not drawn. Add it in Font Manager · Add characters.` |
| tools/AgbStudio.Build/Fonts/TextCodegen.cs:64 | `{key} ({lang}) needs {n} lines; the box shows {max}.` | PARTIAL (no fix) | `{key} ({lang}) needs {n} lines; the box shows {max}. Shorten the text or make the box taller.` |
| tools/AgbStudio.Build/Data/DatabaseCodegen.cs:100+124 | `{table} row {n} ({id}), {field}: '{value}' is not a whole number; using 0.` | OK | - |
| tools/AgbStudio.Build/Data/DatabaseCodegen.cs:100+129 | `{table} row {n} ({id}), {field}: '{value}' is not a number; using 0.` | OK | - |
| tools/AgbStudio.Build/Data/DatabaseCodegen.cs:100+136 | `{table} row {n} ({id}), {field}: string key '{value}' is not in the Localization tables.` | PARTIAL (no consequence, no fix) | `... string key '{value}' is not in the Localization tables, so the first string is used. Add the key in Localization or fix the cell in Database.` |
| tools/AgbStudio.Build/Data/DatabaseCodegen.cs:100+142 | `{table} row {n} ({id}), {field}: '{value}' is not one of {values}; using {first}.` | OK | - |
| tools/AgbStudio.Build/Data/DatabaseCodegen.cs:100+148 | `{table} row {n} ({id}), {field}: sprite '{value}' is not in assets/sprites.` | PARTIAL (no consequence, no fix) | `... sprite '{value}' is not in assets/sprites, so no sprite is used. Import it, or fix the cell in Database.` |
| tools/AgbStudio.Build/Data/DatabaseCodegen.cs:100+158 | `{table} row {n} ({id}), {field}: '{value}' is not a row of {table2}.` | PARTIAL (no consequence, no fix) | `... '{value}' is not a row of {table2}, so its first row is used. Fix the cell in Database.` |
| tools/AgbStudio.Build/Data/DatabaseCodegen.cs:225 | `The CSV file is empty.` | OK | - |
| tools/AgbStudio.Build/Data/DatabaseCodegen.cs:260 | `Row {n}: {column} is “{value}” — will use the default {default}.` (also "Rows {a}, {b} and {c}: ...") | PARTIAL (no why: what the field expects) | `Row {n}: {column} is “{value}”, not a {type} — will use the default {default}. Fix the CSV and choose it again to keep the value.` |
| tools/AgbStudio.Build/Graphics/SpriteConverter.cs:53 (via GameBuilder.cs:216 and Import Asset) | `Uses {n} colours. A GBA sprite palette holds 16, so it needs reducing or splitting.` + buttons "Reduce to 16", "Split into {n} palettes" | OK | - |
| tools/AgbStudio.Build/Graphics/TilesetConverter.cs:51 (via GameBuilder.cs:270 and Import Asset) | `{n} tiles use more than 15 colours; they were reduced.` | PARTIAL (no why) | `{n} tiles use more than 15 colours (a GBA tile has 15 plus transparent), so their colours were reduced. Check them, or redraw them with fewer colours.` |
| tools/AgbStudio.Build/Graphics/TilesetConverter.cs:56 (via GameBuilder.cs:270 and Import Asset) | `The tiles need more than {n} palettes; colours were reduced to fit one palette.` | PARTIAL (no fix) | `The tiles need more than the {n} free palettes, so all colours were reduced to one palette of 16. Use fewer colour sets, or split the tileset.` |
| tools/AgbStudio.Build/Fonts/TrueType.cs:22 | `{font} is not a TrueType font (CFF / OTF outlines are not supported).` | PARTIAL (jargon "CFF outlines", no fix) | `{font} uses OpenType (CFF) outlines, which AGB Studio can't read. Use the .ttf version of the font.` |
| tools/AgbStudio.Build/Fonts/TrueType.cs:31 | `{font} has no '{table}' table.` | POOR (jargon, no fix) | `{font} is damaged or incomplete (no '{table}' table). Download the font again or use another one.` |
| tools/AgbStudio.Build/Fonts/TrueType.cs:64 | `{font} has no Unicode cmap.` | POOR (jargon "cmap", no fix) | `{font} has no Unicode character map, so its letters can't be found. Use a Unicode .ttf font.` |
| tools/AgbStudio.Build/Graphics/PaletteFiles.cs:28 | `{file} is not a JASC .pal or GIMP .gpl palette.` | OK | - |
| tools/AgbStudio.Build/Graphics/Gba.cs:45 | `A {w}x{h} frame is larger than the biggest GBA sprite (64x64).` | PARTIAL (no which sprite, no fix) | `{sprite}: frames are {w}×{h}; the biggest GBA sprite is 64×64. Set a smaller frame size in the Sprite Editor.` |

Notes:

- `GameBuilder.cs:158` is the path for every exception inside a step, so PNG, font, JSON and script-assembler errors
  arrive without the file that caused them (see Project files and AGB0300 "Script too long").
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
| tools/AgbStudio.Scripting/ScriptCompiler.cs:249 | AGB0002 `Too many saved script flags or variables: the save keeps at most 256 flags and 32 variables.` | PARTIAL (no fix, no file; shares id AGB0002 with an unrelated error) | `Too many saved script flags or variables ({flags} flags, {vars} variables); the save keeps at most 256 flags and 32 variables. Mark some as not saved.` |
| tools/AgbStudio.Scripting/Dialogue/DialogueFile.cs:62 (AGB0001) | `The dialogue file is not valid JSON (line {n}): {json message}` | PARTIAL (no fix; the JSON part is technical) | `The dialogue file is not valid JSON at line {n}: {reason}. Undo the hand edit, or restore the file from version control.` |
| tools/AgbStudio.Scripting/Dialogue/DialogueFile.cs:64 (AGB0001) | `The dialogue file is empty.` | PARTIAL (no fix) | `The dialogue file is empty. Delete it, or restore it from version control.` |
| tools/AgbStudio.Scripting/Dialogue/DialogueFile.cs:65 (AGB0001) | `Dialogue format {n} is newer than this AGB Studio understands (1).` | PARTIAL (no fix; also shown for format 0, which is older) | `This dialogue file is format {n}; this AGB Studio reads format 1. Update AGB Studio to open it.` |
| tools/AgbStudio.Scripting/Graph/LogicGraph.cs:89 (AGB0001) | `The graph file is not valid JSON (line {n}): {json message}` | PARTIAL (no fix) | `The graph file is not valid JSON at line {n}: {reason}. Undo the hand edit, or restore the file from version control.` |
| tools/AgbStudio.Scripting/Graph/LogicGraph.cs:99 (AGB0001) | `Graph format {n} is newer than this AGB Studio understands (1).` | PARTIAL (no fix; also shown for format 0) | `This graph file is format {n}; this AGB Studio reads format 1. Update AGB Studio to open it.` |
| tools/AgbStudio.Scripting/Dialogue/DialogueCompiler.cs:48 | AGB0400 `Two conversations are named '{id}'.` | PARTIAL (no fix) | `Two conversations are named '{id}'. Rename one in the Dialogue Editor.` |
| tools/AgbStudio.Scripting/Dialogue/DialogueCompiler.cs:67 | AGB0401 `Line '{label}' does not exist in conversation '{id}' (Next).` | PARTIAL (no fix) | `Next goes to line '{label}', which does not exist in conversation '{id}'. Pick an existing line for Next.` |
| tools/AgbStudio.Scripting/Dialogue/DialogueCompiler.cs:123 | AGB0401 `Choice {n} goes to line '{label}', which does not exist in conversation '{id}'.` | PARTIAL (no fix) | `Choice {n} goes to line '{label}', which does not exist in conversation '{id}'. Pick an existing line for the choice.` |
| tools/AgbStudio.Scripting/Dialogue/DialogueCompiler.cs:167 | AGB0402 `Unknown dialogue command '{command}'. Commands: goToScene, setFlag, giveItem, playSound, wait, shakeCamera, end.` | OK | - |
| tools/AgbStudio.Scripting/Translation/ScriptModel.cs:276 | AGB0010 `{Sound\|Music\|Scene\|...} "{name}" is not in the project. {where these names come from}. Did you mean "{hint}"?` (or `The project has none yet.`) | OK | - |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:96 | AGB0300 `Cutscene parameter '{name}' must be int, bool, fixed or an enum.` | OK | - |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:102 | AGB0300 `This cutscene has no body.` | PARTIAL (no fix) | `This cutscene has no body. Add a body { ... }, or remove [Cutscene].` |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:109 | AGB0300 `Cutscene {method} uses {n} variables; the Event Script VM allows {max}. Split it into smaller cutscenes.` | OK | - |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:111 | AGB0300 `Cutscene {method} needs {n} stack entries; the VM has {max}. Simplify long expressions.` | OK | - |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:159 | AGB0300 `Cutscene variables must be int, bool, fixed or an enum ('{name}').` | OK | - |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:171 | AGB0300 `Only using var _ = LockAll(); is supported in cutscenes.` | OK | - |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:179 | AGB0300 `Only using (LockAll()) { ... } is supported in cutscenes.` | OK | - |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:267 | AGB0300 `break / continue outside a loop.` | PARTIAL (no fix) | `break / continue is outside a loop. Move it into the loop, or use return.` |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:276 | AGB0300 `foreach is not available in cutscenes; use a for loop with a counter.` | OK | - |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:279 | AGB0300 `This statement is not supported in cutscenes ({OperationKind}).` | PARTIAL (Roslyn enum name as the only detail, no fix) | `This kind of statement can't run in a cutscene. Cutscenes run if, while, for, switch, return and calls; move the rest into a normal method.` |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:296 | AGB0300 `Only constant case labels are supported.` | OK | - |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:325 | AGB0300 `This expression has no value in a cutscene.` | POOR (unclear what or why, no fix) | `This expression gives no value a cutscene can use (for example a call that returns void). Use it as a statement of its own.` |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:349 | AGB0300 `Strings are only allowed as names here (string keys, sounds, scenes).` | OK | - |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:364 | AGB0300 `The operator {UnaryOperatorKind} is not available in cutscenes.` | PARTIAL (enum name instead of the operator, no fix) | `The operator {symbol} is not available in cutscenes. Do this step in a [ScriptNative] method.` |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:394 | AGB0300 `Use x++ as a statement of its own in cutscenes.` | OK | - |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:413 | AGB0300 `This expression is not available in cutscenes ({OperationKind}).` | PARTIAL (Roslyn enum name, no fix) | `This kind of expression can't run in a cutscene. Compute it in a [ScriptNative] method and call that.` |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:426 | AGB0300 `Only decimal constants convert to fixed.` | PARTIAL (no example, no fix) | `Only decimal constants convert to fixed (for example 1.5). Keep the value in a fixed variable.` |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:489 | AGB0300 `Strings cannot be compared in cutscenes.` | PARTIAL (no fix) | `Strings cannot be compared in cutscenes. Compare enums, ids or flags instead.` |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:517 | AGB0300 `The operator {BinaryOperatorKind} is not available in cutscenes.` | PARTIAL (enum name, no fix) | `The operator {symbol} is not available in cutscenes. Do this step in a [ScriptNative] method.` |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:559 | AGB0300 `Cutscenes can read their own variables, parameters, Story / Flags variables, Game fields and constants.` | PARTIAL (no what: the name that was read) | `Cutscenes can't read '{name}'. They can read their own variables, parameters, Story / Flags variables, Game fields and constants.` |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:574 | AGB0300 `actor is only available in cutscenes declared in a Behaviour.` | OK | - |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:585 | AGB0300 `Cutscenes can assign their own variables, Story / Flags variables and Game fields.` | PARTIAL (no what: the target) | `Cutscenes can't assign '{name}'. They can assign their own variables, Story / Flags variables and Game fields.` |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:617 | AGB0300 `Cutscene {method} was not compiled.` | POOR (no why, no fix) | `Cutscene {method} has errors (see above), so it can't be called here. Fix those first.` |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:621 | AGB0300 `{method} belongs to a behaviour; call it from a cutscene of the same behaviour.` | OK | - |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:642 | AGB0300 `Debug.Log in a cutscene takes a constant string.` | OK | - |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:649 | AGB0300 `'{type}.{method}' cannot be called from a cutscene. Cutscenes can call the cutscene API (Say, Choice, Move, Wait, ...), other cutscenes and [ScriptNative] methods.` | OK | - |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:669 | AGB0300 `The runtime has no native '{name}' (runtime/include/agb/natives.hpp).` | POOR (internal file path, no fix; it means the API and the engine do not match) | `{method} is not available in this version of the engine. Update AGB Studio, or report this as a bug.` |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:673 | AGB0300 `{method} takes at most {max} values in a cutscene.` | OK | - |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:677 | AGB0300 `'{method}' cannot be called from a cutscene.` | PARTIAL (no why, no fix; line 649 has both) | Same text as line 649. |
| tools/AgbStudio.Scripting/Bytecode/BytecodeCompiler.cs:724 | AGB0011 `This name must be a constant string so it can be turned into an id at build time.` | OK | - |
| tools/AgbStudio.Scripting/Bytecode/ScriptAssembler.cs:120 | `Script too long: a jump exceeds 32 KB.` (thrown, reaches the user without a file through GameBuilder.cs:158 or "Script check failed:") | PARTIAL (jargon "jump", no which cutscene, no fix) | `Cutscene {method} is too long to compile (over 32 KB of code between two points). Split it into smaller cutscenes.` |
| tools/AgbStudio.Scripting/Translation/CppTranslator.cs:116 | AGB0100 `The type '{type}' cannot be used in GBA scripts. Use int, bool, fixed, fixed2, an enum, a struct or a class of the GBA API or of your scripts.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.cs:133 | AGB0100 `Nested type '{type}' is not supported. Move it out of '{outer}'.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.cs:138 | AGB0100 `Generic type '{type}' is not supported; generics are only allowed on API calls such as Get<T>().` | PARTIAL (no fix) | `... Make one non-generic type per use instead.` |
| tools/AgbStudio.Scripting/Translation/CppTranslator.cs:199 | AGB0100 `[Cutscene] method '{method}' must be declared 'async Task'.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.cs:201 | AGB0100 `async method '{method}' needs [Cutscene]: only cutscenes can wait, and they compile to Event Script bytecode.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.cs:204 | AGB0100 `Nested type '{type}' is not supported. Move it out of '{outer}'.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.cs:217 | AGB0100 `'{type}' derives from '{base}', which scripts cannot extend.` | PARTIAL (no fix) | `'{type}' derives from '{base}', which scripts cannot extend. Derive from Behaviour or SceneScript, or from one of your own classes.` |
| tools/AgbStudio.Scripting/Translation/CppTranslator.cs:267 | AGB0100 `Indexers are not supported in scripts.` | PARTIAL (no fix) | `Indexers are not supported in scripts. Use Get(i) and Set(i, value) methods.` |
| tools/AgbStudio.Scripting/Translation/CppTranslator.cs:278 | AGB0100 `Generic method '{method}' is not supported; generics are only allowed on API calls such as Get<T>().` | PARTIAL (no fix) | `... Write one method per type instead.` |
| tools/AgbStudio.Scripting/Translation/CppTranslator.cs:281 | AGB0100 `Operators declared in scripts are not supported yet. Use a method instead (for example Add(a, b)).` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.cs:284 | AGB0100 `Static constructors are not supported; initialize static fields where they are declared.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.cs:480 | AGB0100 `Only one-dimensional arrays are supported.` | PARTIAL (no fix) | `Only one-dimensional arrays are supported. Use one array of width × height and index it as y * width + x.` |
| tools/AgbStudio.Scripting/Translation/CppTranslator.cs:496 | AGB0100 `Array sizes must be constants (the GBA has no heap): new int[8].` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.cs:507 | AGB0100 `Arrays need a fixed size where they are declared: int[] counts = new int[8]; (the GBA has no heap).` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.cs:544 | AGB0100 `params arrays are not supported in translated code.` | PARTIAL (jargon "translated code", no fix) | `params arrays are not supported in GBA scripts. Pass a normal array, or add overloads.` |
| tools/AgbStudio.Scripting/Translation/CppTranslator.cs:616 | AGB0100 `This method body could not be translated.` | POOR (no why, no fix) | `This method could not be turned into C++: {first unsupported part}. Rewrite that part with the GBA script subset.` |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:67 | AGB0100 `foreach works over arrays only.` | PARTIAL (no fix) | `foreach works over arrays only. Use a for loop with an index for other collections.` |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:81 | AGB0100 `yield is not supported.` | PARTIAL (no why, no fix) | `yield is not supported on GBA. Fill an array, or use a [Cutscene] to wait across frames.` |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:99 | AGB0100 `using blocks are only allowed in [Cutscene] methods (using var _ = LockAll();).` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:102 | AGB0100 `Exceptions are not available on GBA (GAE004): remove try / throw.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:105 | AGB0100 `Local functions are not supported; make it a method of the class.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:108 | AGB0100 `This statement is not supported in GBA scripts ({OperationKind}).` | PARTIAL (Roslyn enum name, no fix) | `This kind of statement can't be used in GBA scripts. Rewrite it with if, loops, switch and method calls.` |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:157 | AGB0100 `Only constant case labels are supported in switch.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:194 | AGB0100 `ref locals are not supported.` | PARTIAL (no fix) | `ref locals are not supported in GBA scripts. Copy the value into a normal variable and write it back.` |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:256 | AGB0100 `Strings cannot be joined at runtime on GBA. Use Debug.Log($"...") to show values, or string keys from Localization for text.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:276 | AGB0100 `The operator {UnaryOperatorKind} is not supported.` | PARTIAL (enum name, no fix) | `The operator {symbol} is not supported in GBA scripts. Write it as a method call.` |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:286 | AGB0100 `This property can only be assigned in a statement of its own.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:297 | AGB0100 `This property can only be changed in a statement of its own.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:299 | AGB0100 `Strings cannot be joined at runtime on GBA.` | PARTIAL (no fix; line 256 has it) | Same text as line 256. |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:312 | AGB0100 `fixed ++ / -- can only be used as a statement of its own.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:333 | AGB0100 `Only one-dimensional arrays are supported.` | PARTIAL (no fix) | Same as CppTranslator.cs:480. |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:344 | AGB0100 `await is only allowed in [Cutscene] async Task methods.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:347 | AGB0100 `Interpolated strings can only be passed to Debug.Log on GBA.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:350 | AGB0100 `Lambdas are not supported in translated scripts (GAE006). Use a method instead.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:353 | AGB0100 `Type checks need RTTI, which GBA builds do not have. Use actor.Is<T>() or Get<T>() != null.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:356 | AGB0100 `?. and ?? are not supported; check for null with an if statement.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:359 | AGB0100 `Exceptions are not available on GBA (GAE004).` | PARTIAL (no fix; line 102 has it) | `Exceptions are not available on GBA (GAE004): remove try / throw.` |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:362 | AGB0100 `Arrays can only be created where a field or local variable is declared (the GBA has no heap).` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:365 | AGB0100 `This expression is not supported in GBA scripts ({OperationKind}).` | PARTIAL (Roslyn enum name, no fix) | `This kind of expression can't be used in GBA scripts. Split it into simpler steps.` |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:413 | AGB0201 `Only decimal constants convert to fixed (for example 1.5). The GBA has no FPU: keep the value in a fixed variable instead of a float or double (GAE002).` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:418 | AGB0202 `{value} is outside the fixed range (-8388608 to 8388607.99).` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:425 | AGB0100 `The conversion to '{type}' is not supported.` | PARTIAL (no why, no fix) | `The conversion to '{type}' is not supported on GBA. Use int, fixed or an enum cast.` |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:448 | AGB0100 `Boxing (object, interfaces) is not available on GBA.` | PARTIAL (jargon "boxing", no fix) | `A value can't be stored as object or an interface on GBA. Use the real type.` |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:529 | AGB0100 `Strings cannot be compared at runtime on GBA. Compare tags with actor.tag == "Coin", or use enums and ids.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:563 | AGB0100 `This name must be a constant string so it can be turned into an id at build time.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:598 | AGB0100 `'{type}.{field}' is not available on GBA.` | PARTIAL (no fix) | `'{type}.{field}' is not available on GBA. Scripts can use the GBA API and their own classes.` |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:609 | AGB0100 `'{property}' has no getter.` | PARTIAL (no fix) | `'{property}' can only be set, not read. Keep its value in a field of your own.` |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:616 | AGB0100 `'{type}.{property}' is not available on GBA.` | PARTIAL (no fix) | `'{type}.{property}' is not available on GBA. Scripts can use the GBA API and their own classes.` |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:624 | AGB0100 `This object cannot be created.` | POOR (no what, why or fix) | `{type} can't be created on GBA. Create objects of your own classes or the GBA API.` |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:626 | AGB0100 `Object initializers { ... } are not supported; set the fields after creating the object.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:636 | AGB0120 `Behaviours are created by the scene, not with new. Add {type} to an actor in the Inspector, or use Get<{type}>().` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:639 | AGB0100 `'{type}' cannot be created on GBA.` | PARTIAL (no why, no fix) | `'{type}' can't be created on GBA. Create objects of your own classes or the GBA API.` |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:669 | AGB0011 `The {parameter} argument must be a constant string (for example "jump") so it can be turned into an id at build time.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:693 | AGB0100 `Local functions are not supported.` | PARTIAL (no fix; line 105 has it) | `Local functions are not supported; make it a method of the class.` |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:705 | AGB0100 `{method} can only be used inside a [Cutscene] async Task method (it waits across frames).` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:707 | AGB0100 `'{type}.{method}' is not available on GBA. Scripts can use the GBA API (Input, Sound, Scene, ...) and their own classes.` | OK | - |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:725 | AGB0100 `Cutscene {method} was not compiled.` | POOR (no why, no fix) | `Cutscene {method} has errors (see above), so it can't be called here. Fix those first.` |
| tools/AgbStudio.Scripting/Translation/CppTranslator.Code.cs:750 | AGB0100 `Debug.Log can show int, bool, fixed, enum and string values.` | PARTIAL (no what: the value that was passed) | `Debug.Log can't show '{expr}' ({type}). It shows int, bool, fixed, enum and string values.` |

## Analyzer

Rules in `tools/AgbStudio.Analyzers/GaeRules.cs`. The Script Editor popup shows `{id} · {message}` with the
description under it and fix buttons; the Problems line shows `{message} — {text after the arrow}`; the Build ROM log
and Console show `{id} {short text}`. Titles are the rule names in Project Settings · Scripting.

| File:line | Text | Verdict | Suggested wording |
|---|---|---|---|
| tools/AgbStudio.Analyzers/GaeRules.cs:15 | GAE001 title `class allocated in Update / OnTouch (no garbage collector)` | OK | - |
| tools/AgbStudio.Analyzers/GaeRules.cs:16+18 | GAE001 `'new {type}' allocates a class in {method}; the GBA has no garbage collector, so this memory is never given back` + `Classes live until the scene ends. Create the object once in Start (it goes into the scene's memory arena) and reuse it.` | OK | - |
| tools/AgbStudio.Analyzers/GaeRules.cs:67 | GAE001 log `class allocated in {method} → allocate in Start` | PARTIAL (no why) | `class allocated in {method} (no GC) → allocate in Start` |
| tools/AgbStudio.Analyzers/GaeRules.cs:23 | GAE002 title `float or double — the GBA has no FPU, use fixed` | OK | - |
| tools/AgbStudio.Analyzers/GaeRules.cs:24+26 | GAE002 `{type} is not supported on GBA` + `The GBA has no FPU, so floats are emulated and very slow. Use the 24.8 fixed-point type instead — same syntax, runs at full speed.` | PARTIAL (the message says "not supported" but the rule is a warning and the description says "emulated and very slow") | `{type} is very slow on GBA (no FPU); use fixed` |
| tools/AgbStudio.Analyzers/GaeRules.cs:68 | GAE002 log `{type} → use fixed` | PARTIAL (no why) | `{type} is slow (no FPU) → use fixed` |
| tools/AgbStudio.Analyzers/GaeRules.cs:31 | GAE003 title `new inside Update — allocate in Start or use a pool` | OK | - |
| tools/AgbStudio.Analyzers/GaeRules.cs:32+34 | GAE003 `'new {type}' inside {method} allocates every frame; allocate it in Start or use a pool` + description | OK | - |
| tools/AgbStudio.Analyzers/GaeRules.cs:69 | GAE003 log `new inside {method} → allocate in Start or use a pool` | PARTIAL (no why) | `new inside {method} fills the scene memory every frame → allocate in Start or use a pool` |
| tools/AgbStudio.Analyzers/GaeRules.cs:39 | GAE004 title `try / throw — exceptions are not supported` | OK | - |
| tools/AgbStudio.Analyzers/GaeRules.cs:40+42 | GAE004 `'{keyword}' is not supported: exceptions are not available on GBA` + `The GBA runtime is built without exceptions. Return a bool or an error value instead of throwing.` | OK | - |
| tools/AgbStudio.Analyzers/GaeRules.cs:70 | GAE004 log `{keyword} → exceptions are not supported` | PARTIAL (no fix after the arrow; the Problems line shows "— exceptions are not supported" as the hint) | `{keyword} (no exceptions on GBA) → return a bool or an error value` |
| tools/AgbStudio.Analyzers/GaeRules.cs:47 | GAE005 title `string concatenation in a loop` | PARTIAL (no why) | `string concatenation in a loop (builds a new string every pass)` |
| tools/AgbStudio.Analyzers/GaeRules.cs:48+50 | GAE005 `String concatenation in a loop builds a new string on every pass` + description | OK | - |
| tools/AgbStudio.Analyzers/GaeRules.cs:71 | GAE005 log `string concatenation in a loop` | POOR (no why, no fix; has no arrow, so the Problems line gets no hint either) | `string concatenation in a loop → use string keys or log outside the loop` |
| tools/AgbStudio.Analyzers/GaeRules.cs:55 | GAE006 title `lambda that captures variables` | PARTIAL (no why) | `lambda that captures variables (hidden allocation)` |
| tools/AgbStudio.Analyzers/GaeRules.cs:56+58 | GAE006 `This lambda captures '{name}', which needs a hidden allocation` + description | OK | - |
| tools/AgbStudio.Analyzers/GaeRules.cs:72 | GAE006 log `lambda captures {name} → make it static` | PARTIAL (no why; "make it static" alone does not work while it still captures) | `lambda captures {name} (hidden allocation) → pass it as a parameter and make the lambda static` |

## Project files

Thrown by `tools/AgbStudio.Core` when a project, scene, map or image is read. They reach the user inside
`Could not open {project}: {e.Message}` (OpeningProjectDialog.cs:250), `{file} can't be read: {e.Message}`
(AssetImporter.cs:146), or as a bare build error with no file (GameBuilder.cs:158).

| File:line | Text | Verdict | Suggested wording |
|---|---|---|---|
| tools/AgbStudio.Core/Project/GameProject.cs:46 | `No .agbproj file in {folder}.` | PARTIAL (no fix) | `No .agbproj file in {folder}. Pick the folder that holds the project file.` |
| tools/AgbStudio.Core/Project/GameProject.cs:48 | `{file} was made by a newer AGB Studio (format {n}).` | PARTIAL (no fix) | `{file} was made by a newer AGB Studio (format {n}); this one reads format 1. Update AGB Studio to open it.` |
| tools/AgbStudio.Core/Project/GameProject.cs:71 | `{path} is empty.` | PARTIAL (no fix) | `{file} is empty. Restore it from a backup or version control.` |
| tools/AgbStudio.Core/Project/GameProject.cs:71 | (System.Text.Json exception text passed through for a broken .agbproj, .scene, .map or meta file: position as Path / LineNumber / BytePositionInLine, no file name) | POOR (no file, technical, no fix) | `{file} is not valid JSON at line {line}: {reason}. Undo the hand edit, or restore it from version control.` (DialogueFile.cs:62 already wraps it this way.) |
| tools/AgbStudio.Core/Png.cs:57 | `Not a PNG file.` | PARTIAL (no file in the build, no fix) | `{file} is not really a PNG (maybe a renamed JPG). Save it again as PNG.` |
| tools/AgbStudio.Core/Png.cs:75 | `Interlaced PNG files are not supported.` | PARTIAL (no fix) | `{file} is an interlaced PNG. Save it again with interlacing off.` |
| tools/AgbStudio.Core/Png.cs:95 | `Unknown PNG colour type.` | POOR (jargon, no fix) | `{file} uses a PNG colour format AGB Studio can't read. Save it again as an RGBA or indexed PNG.` |
| tools/AgbStudio.Core/Png.cs:96 | `PNG bit depth {n} is not supported.` | PARTIAL (jargon, no fix) | `{file} uses {n}-bit colour channels. Save it again as an 8-bit RGBA or indexed PNG.` |
| tools/AgbStudio.Core/Png.cs:153 | `Bad PNG filter.` | POOR (jargon, no fix) | `{file} is damaged (its PNG data can't be decoded). Save it again from your paint program.` |

## CLI

`tools/agb/Program.cs` and the helpers it calls. Most commands are developer tools; `agb build` is the user-facing one.

| File:line | Text | Verdict | Suggested wording |
|---|---|---|---|
| tools/agb/Program.cs:14 | `usage: agb romtest <rom.gba> [--frames N] [--shot out.png] [--memmap]` (shown when agb gets no arguments) | POOR (names one of nine commands) | `usage: agb <command> [args]. Commands: build, romtest, runtime-test, sample, template, package, lint, text-preview, make-test-audio.` |
| tools/agb/Program.cs:30 | `Unknown command '{name}'.` | PARTIAL (no fix) | `Unknown command '{name}'. Commands: build, romtest, runtime-test, sample, template, package, lint, text-preview, make-test-audio.` |
| tools/agb/Program.cs:35 | `error: {e.Message}` (catch-all; a missing argument, for example `agb sample` alone, prints the .NET index-out-of-range text) | POOR (no what for missing arguments, no usage) | `error: {command} needs {arguments}. usage: agb {command} {arguments}` |
| tools/agb/Program.cs:47 | `Missing ROM path.` | PARTIAL (no usage) | `Missing ROM path. usage: agb romtest <rom.gba> [--frames N] [--shot out.png] [--memmap]` |
| tools/AgbStudio.Emu/RomTestRunner.cs:48 | `The ROM never initialised the debug channel.` | PARTIAL (jargon, no fix) | `The ROM never started the AGB Studio runtime (no debug channel). Check that it is an AGB Studio Debug build.` |
| tools/AgbStudio.Emu/RomTestRunner.cs:48 | `Timed out after {n} frames.` | PARTIAL (no fix) | `Timed out after {n} frames without a pass or fail. Raise --frames, or check that the test ends.` |
| tools/agb/Program.cs:81 | `build failed` (runtime-test, after the build log) | PARTIAL (no what) | `runtime test ROMs failed to build (see the log above).` |
| tools/agb/Program.cs:98 | `{n} of {total} runtime tests failed` | OK | - |
| tools/agb/Program.cs:116 | `Build failed at {step}` (after the diagnostics) | PARTIAL (no fix) | `Build failed at {step}: {n} errors above. Fix them and run agb build again.` |
| tools/agb/Program.cs:127 | `Unknown sample '{name}'.` | PARTIAL (no list) | `Unknown sample '{name}'. Samples: mini, skyhopper.` |
| tools/agb/Program.cs:136 | `Unknown template '{id}'.` | PARTIAL (no list) | `Unknown template '{id}'. Templates: {ids}.` |
| tools/AgbStudio.Build/Content/Templates/TemplateKit.cs:14 | `The folder {dir} is not empty.` | PARTIAL (no fix) | `The folder {dir} is not empty. Pick an empty or new folder.` |
| tools/AgbStudio.Build/NativeBuild.cs:38 | `Cannot find the AGB Studio repository root.` | PARTIAL (no fix) | `Cannot find the AGB Studio folder (AGB-Studio.sln or the install marker). Run agb from inside it.` |
| tools/AgbStudio.Build/Packager.cs:33 | `dotnet publish failed:\n{output}` | PARTIAL (raw output, no fix) | `dotnet publish failed (output above). Build the solution once in Visual Studio or with dotnet build to see the first error.` |
| tools/AgbStudio.Build/RuntimeTests.cs:40 | `mmutil failed: {log}` | PARTIAL (raw log, no fix) | `mmutil could not make the test soundbank: {first line}. Check the devkitPro tools folder.` |
| tools/agb/Program.cs:155 | `lint: {n} issues` (after one `{file}({line}): {rule}: {text}` line per issue) | OK | - |

## Runtime Console

Logged by the game through the debug channel (`runtime/src/core/debug.cpp`). The editor Play Console
(`PlayConsole.cs:40`) shows lines that start with `warning: ` as warnings without the prefix, and `assert failed` lines as
errors. `warnOnce` texts are logged once per run with a `warning: ` prefix.

| File:line | Text | Verdict | Suggested wording |
|---|---|---|---|
| runtime/src/audio/audio.cpp:136 | `warning: no DMG sound effect %u` | POOR (number instead of a name, jargon "DMG", no fix) | `warning: SFX Designer sound {n} does not exist, so nothing played. Check the sound name in the script and rebuild.` |
| runtime/src/core/assert.cpp:13 | `assert failed: %s(%d): %s (%s)` (the game stops) | PARTIAL (no fix; depends on the assert message) | `assert failed: {file}({line}): {message}. The game stopped here; see the message for the cause.` |
| runtime/include/agb/memory.hpp:79 | assert `stale handle used after the object was destroyed` | PARTIAL (jargon "handle", no fix) | `an object was used after it was destroyed. Check scripts that keep a reference after Destroy().` |
| runtime/include/agb/memory.hpp:87 | assert `double free or stale handle` | POOR (jargon, no fix) | `an object was destroyed twice, or used after it was destroyed. Check scripts that call Destroy() more than once.` |
| runtime/include/agb/memory.hpp:19 | `warning: arena '%s' is full (%lu of %lu bytes); allocation of %lu failed` | PARTIAL (jargon "arena", no fix) | `warning: scene memory '{name}' is full ({used} of {size} bytes); {n} bytes could not be given. Allocate once in Start instead of every frame (GAE003).` |
| runtime/include/agb/memory.hpp:72 | `warning: pool '%s' is full (%d); the new object was not created` | PARTIAL (no fix) | `warning: pool '{name}' is full ({n}); the new object was not created. Destroy objects you no longer need.` |
| runtime/src/core/save.cpp:69 | `warning: flash sector erase timed out` | POOR (jargon, no consequence, no fix) | `warning: the save chip did not answer (erase), so the game was not saved. In an emulator set the save type to Flash; on hardware check the cartridge.` |
| runtime/src/core/save.cpp:77 | `warning: flash program timed out` | POOR (jargon, no consequence, no fix) | `warning: the save chip did not answer (write), so the game was not saved. In an emulator set the save type to Flash; on hardware check the cartridge.` |
| runtime/src/core/save.cpp:122 | `warning: EEPROM write timed out` | POOR (jargon, no consequence, no fix) | `warning: the save chip did not answer (EEPROM write), so the game was not saved. Check the save type in Project Settings · Save.` |
| runtime/src/core/save.cpp:165 | `warning: save data needs %u bytes per slot but the save type has room for %lu` | PARTIAL (no fix) | `warning: save data needs {n} bytes per slot but the save type has room for {cap}. Use fewer save fields or slots, or a bigger save type in Project Settings · Save.` |
| runtime/src/core/save.cpp:253 | `warning: save payload larger than 4 KB is not supported` | PARTIAL (no fix) | `warning: save data over 4 KB per slot is not supported, so it was not saved. Use fewer save fields.` |
| runtime/src/core/system.cpp:93 | `warning: no free VBlank hook slot` | POOR (jargon, no consequence) | `warning: too many per-frame callbacks; the new one does not run.` |
| runtime/src/core/commit.cpp:128 | `DMA budget for this frame is used up; transfer deferred to the next frame` | PARTIAL (jargon "DMA", no fix) | `too much graphics data changed this frame; the rest is copied next frame. Change fewer tiles or palettes per frame.` |
| runtime/src/core/task.cpp:21 | `task pool is full (16 tasks); the new task was not started` | PARTIAL (no fix) | `16 tasks are already running; the new task was not started. Let tasks finish before starting more.` |
| runtime/src/framework/actor.cpp:105 | `all 64 actors are in use; an actor was not spawned` | PARTIAL (no fix) | `all 64 actors are in use; an actor was not spawned. Destroy actors you no longer need.` |
| runtime/src/framework/bus.cpp:39 | `command bus queue is full` | POOR (jargon, no consequence, no fix) | `too many messages between scripts this frame; some were dropped. Send fewer messages per frame.` |
| runtime/src/framework/game.cpp:141 | `warning: layer %d of %s could not be loaded` | PARTIAL (no why; the reason is the warning logged just before) | `warning: layer {n} of {scene} could not be loaded (see the warning above).` |
| runtime/src/framework/game.cpp:297 | `warning: the boot splash image could not be loaded` | PARTIAL (no why, no fix) | `warning: the boot splash could not be shown (no free background memory). Turn it off in Project Settings · General if it keeps failing.` |
| runtime/src/framework/game.cpp:433 | `warning: load step '%s' of %s took %lu cycles; split it into smaller steps` | OK | - |
| runtime/src/framework/game.cpp:451 | `warning: frame %lu took longer than one frame (%s: %s)` | PARTIAL (no consequence, no fix) | `warning: frame {n} took longer than one frame ({scene}: {step}), so the game slowed down. Check the Profiler for the slow part.` |
| runtime/src/framework/game.cpp:462 | `warning: screen stack is full` | POOR (jargon, no consequence, no fix) | `warning: too many screens are open on top of each other; the new one was not opened. Close a menu or screen first.` |
| runtime/src/framework/game.cpp:518 | `warning: Scene.Load("%s"): no scene with that name` | PARTIAL (no fix) | `warning: Scene.Load("{name}"): no scene has that name, so nothing loaded. Check the name in the World Map.` |
| runtime/src/framework/game.cpp:619 | `warning: Spawn %s: all %d actor slots are in use` | PARTIAL (no consequence, no fix) | `warning: Spawn {name}: all {n} actor slots are in use, so it was not spawned. Destroy actors you no longer need.` |
| runtime/src/framework/game.cpp:622 | `warning: Spawn: no actor named %s in %s` | PARTIAL (no fix) | `warning: Spawn: {scene} has no actor named {name} to copy. Add one to the scene (it can be hidden).` |
| runtime/src/framework/ui.cpp:553 | `warning: this scene has a menu but the game has no UI kit` | POOR (jargon "UI kit", no fix) | `warning: the menu of this scene can't be shown because the project has no font. Add one in Font Manager and rebuild.` |
| runtime/src/render/affine.cpp:43 | `warning: no empty character block for an affine layer` | POOR (jargon, no consequence, no fix) | `warning: no free background memory for a rotate / scale layer, so it is not shown. Use fewer layers in this scene.` |
| runtime/src/render/bg.cpp:150 | `warning: background VRAM is full; %u tiles could not be placed` | PARTIAL (no fix) | `warning: background memory is full; {n} tiles could not be placed. Use fewer layers or a smaller tileset in this scene.` |
| runtime/src/render/bg.cpp:207 | `warning: no free screen blocks for a background layer` | POOR (jargon, no consequence, no fix) | `warning: no free background memory for a layer, so it is not shown. Use fewer layers in this scene.` |
| runtime/src/render/bg.cpp:219 | `warning: no free screen blocks for an affine layer` | POOR (jargon, no consequence, no fix) | `warning: no free background memory for a rotate / scale layer, so it is not shown. Use fewer layers in this scene.` |
| runtime/src/render/palette.cpp:102 | `warning: all 16 %s palette banks are in use` | PARTIAL (no consequence, no fix) | `warning: all 16 {BG\|sprite} palettes are in use, so new colours are not loaded. Share palettes between assets.` |
| runtime/src/render/palette.cpp:162 | `warning: all 4 palette cycle slots are in use` | PARTIAL (no consequence, no fix) | `warning: 4 palette cycles are already running; the new one does not start. Stop one first.` |
| runtime/src/render/sprite.cpp:99 | `OBJ VRAM is full; a sprite frame could not be shown` | PARTIAL (jargon "OBJ VRAM", no fix) | `sprite memory is full; a sprite frame could not be shown. Use fewer or smaller sprite sheets in this scene.` |
| runtime/src/render/sprite.cpp:159 | `all 128 sprites are in use; a new sprite was not created` | PARTIAL (no fix) | `all 128 sprites are in use; a new sprite was not created. Hide or destroy sprites you no longer need.` |
| runtime/src/render/sprite.cpp:246 | `all 32 affine matrices are in use` | POOR (jargon, no consequence, no fix) | `32 sprites are already rotated or scaled; this one is drawn without rotation. Rotate fewer sprites at once.` |
| runtime/src/render/sprite.cpp:386 | `warning: screen line %d needs %d sprite cycles and the hardware has %d; some sprites on it will not show` | PARTIAL (no fix) | `... some sprites on it will not show. Put fewer or smaller sprites on the same row.` |
| runtime/src/render/textdb.cpp:71 | `warning: string %u has no text in any language` | PARTIAL (number instead of the key, no fix) | `warning: string key {key} has no text in any language. Add it in Localization.` |
| runtime/src/render/textdb.cpp:114 | `warning: no free background layer for text; it shares BG%d with the scene` | PARTIAL (no fix) | `warning: no free background layer for text, so it shares BG{n} with the scene and may cover it. Use 3 layers or fewer in this scene.` |
| runtime/src/script/natives.cpp:175 | `warning: Move: no actor %ld or path %ld` | POOR (numbers instead of names, no fix) | `warning: Move: actor {name} or path {name} does not exist in this scene, so nothing moved.` |
| runtime/src/script/vm.cpp:35 | `warning: script stack overflow` | POOR (jargon, no where, no fix) | `warning: cutscene {name} called too deep and was stopped. Call fewer cutscenes inside each other.` |
| runtime/src/script/vm.cpp:44 | `warning: script call failed (id %u, depth %u)` | POOR (ids only, no why, no fix) | `warning: cutscene {name} could not start: {reason}.` |
| runtime/src/script/vm.cpp:49 | `warning: script locals exhausted` | POOR (jargon, no where, no fix) | `warning: cutscene {name} needs more variables than the engine has free; it was stopped. Use fewer variables or nested calls.` |
| runtime/src/script/vm.cpp:149 | `warning: unknown script native %u` | POOR (internal id; means the ROM and engine do not match) | `warning: cutscene {name} calls an engine function this ROM does not have. Rebuild the game.` |
| runtime/src/script/vm.cpp:157 | `warning: %s waits, which is not allowed in an immediate script` | PARTIAL (jargon "immediate script", no fix) | `warning: {native} waits, but it was called from a script that must finish in one frame. Call it from a [Cutscene] instead.` |
| runtime/src/script/vm.cpp:192 | `warning: bad opcode %u in %s` | POOR (internal, no fix) | `warning: cutscene {name} is damaged or from another build; it was stopped. Rebuild the game.` |
| runtime/src/script/vm.cpp:282 | `all 8 script contexts are busy; a script did not start` | PARTIAL (jargon "contexts", no fix) | `8 cutscenes are already running; a new one did not start. Let one finish first.` |

## Editor

`editor/AgbStudio.Editor`. Paths below are relative to that folder unless they start with `tools/`.

| File:line | Text | Verdict | Suggested wording |
|---|---|---|---|
| Services/ProjectServices.cs:81 | `{tool} is not at {path}. Set the devkitPro folder in Preferences › Paths.` | OK | - |
| Windows/Settings/ToolchainHelp.cs:88, 101, 115 | title `devkitPro not found` + `AGB Studio builds games with devkitPro (devkitARM, libtonc and maxmod). It was not found in these folders:` + folder list + `Install devkitPro with its installer, or set the folder it is in under Preferences › Paths.` + buttons | OK | - |
| Windows/Settings/ToolchainHelp.cs:89, 115 | title `A build tool is missing` + `The build could not start because these tools are not where the devkitPro folder says:` + tool list + "Open Preferences · Paths" | OK | - |
| Windows/Build/BuildRomDialog.cs:72 | `No project is open.` (Build ROM log) | PARTIAL (no fix) | `No project is open. Open or create a project, then build.` |
| Windows/Build/BuildRomDialog.cs:72, Services/ProjectServices.cs:69 | `A build is already running.` | PARTIAL (no fix) | `A build is already running. Wait for it to finish, or cancel it in Build ROM.` |
| Windows/Build/BuildRomDialog.cs:217-218 | `Build failed · {n} errors` + failed and skipped steps + clickable error lines + `Fix them, then build again.` + "Go to first error" (T-D23) | OK | - |
| Windows/Build/BuildRomDialog.cs:98 | status bar `Build failed` | PARTIAL (no count, no fix; Build ROM · Failed carries the detail) | `Build failed · {n} errors` |
| Windows/Build/BuildRomDialog.cs:128 | `Could not write {file}: {e.Message}` | PARTIAL (no fix) | `Could not write {file}: {reason}. Pick another folder, or close programs that use the file.` |
| Windows/Play/PlayController.cs:188 | toast `Build failed · see Console` | PARTIAL (no count; fix only by pointer) | `Build failed · {n} errors. Open the Console to go to the first one.` |
| Windows/Play/PlayController.cs:196, Windows/SceneEditor/ConsolePanel.cs:73 | `Build failed at {step}` | PARTIAL (no fix) | `Build failed at {step}: {n} errors above. Fix them and press Play again.` |
| Windows/Play/PlayController.cs:156 | `No project is open.` (Console) | PARTIAL (no fix) | `No project is open. Open a project, then press Play.` |
| Windows/Play/PlayController.cs:581 | `Hot reload: {name} rebuilt; the game restarted` | OK | - |
| Windows/Build/BuildReportPage.cs:93 | `Build the ROM first.` | OK | - |
| Windows/Build/BuildReportPage.cs:109 | status `Build failed` | PARTIAL (no what) | `Build failed · {first error}` (the history entry already keeps it) |
| tools/AgbStudio.Emu/LibretroCore.cs:114 | `The emulator core could not load the ROM.` | PARTIAL (no why, no fix) | `The emulator could not start {rom}. Build the ROM again; if it still fails, the file is damaged.` |
| tools/AgbStudio.Emu/LibretroCore.cs:180 | `Save state failed.` | POOR (no why, no fix) | `Could not make a save state of the running game. Try again; if it keeps failing, restart Play Mode.` |
| tools/AgbStudio.Emu/LibretroCore.cs:187 | `Load state failed.` | POOR (no why, no fix) | `Could not load this save state; it may come from another build of the game. Make a new save state.` |
| Windows/Build/FlashDialog.cs:87 | `No device found` / `Connect a GBxCart RW or insert a flash cart's SD card` | OK | - |
| Windows/Build/FlashDialog.cs:113 | `Device error` / `{e.Message}` | POOR (bare title, raw detail) | `Can't talk to {device}: {reason}. Check the cable, then try again.` |
| Windows/Build/CartridgeDevices.cs:71 | `No SD card` / `{e.Message}` | PARTIAL (no fix) | `No SD card in {drive}. Insert the flash cart's SD card.` |
| Windows/Build/CartridgeDevices.cs:160 | `GBxCart RW not responding` / `Check the USB cable and the firmware` | OK | - |
| Windows/Build/CartridgeDevices.cs:169, 175 | `No cartridge` / `Insert a cartridge into the GBxCart RW` | OK | - |
| Windows/Build/CartridgeDevices.cs:180 | `GBxCart RW not available` / `{e.Message}` | PARTIAL (no fix) | `GBxCart RW not available: {reason}. Plug it in again and check the USB cable.` |
| Windows/Build/CartridgeDevices.cs:226 | `{port} cannot be opened (error {code}).` | POOR (Windows error number, no fix) | `{port} can't be opened; another program may be using it. Close that program and plug the GBxCart RW in again.` |
| Windows/Build/CartridgeDevices.cs:234 | `{port} does not accept {baud} baud.` | POOR (jargon, no fix) | `The device on {port} does not answer at the GBxCart RW speed. Check it is a GBxCart RW and update its firmware.` |
| Windows/Build/CartridgeDevices.cs:241 | `Serial write failed.` | POOR (no where, no fix) | `Lost the connection to the GBxCart RW while writing. Check the USB cable and flash again.` |
| Windows/Build/FlashDialog.cs:185 | `Verify failed: the cartridge does not match the ROM. Flash again.` | OK | - |
| Windows/Build/FlashDialog.cs:196 | `Flashing cancelled. Flash again before playing the cartridge.` | OK | - |
| Windows/Build/FlashDialog.cs:201 | `{e.Message}` (toast) | POOR (raw text only) | `Flashing failed: {reason}. The cartridge may hold a broken game; flash again.` |
| Windows/Scripts/ScriptEditorPage.cs:386, Windows/Launcher/LauncherService.cs:159 | status `devkitARM not found` | PARTIAL (no fix) | `devkitARM not found · set it in Preferences › Paths` |
| Windows/Settings/PreferencesDialog.cs:304 | `{tools} missing` (under the devkitPro folder field) | PARTIAL (no fix) | `{tools} not found in this folder. Pick the devkitPro folder, or install devkitPro.` |
| Windows/Settings/ProjectSettingsDialog.cs:321 | `{tools} found · {tools} missing` | PARTIAL (no fix) | `... {tools} missing. Set the devkitPro folder in Preferences › Paths.` |
| Windows/Settings/PreferencesDialog.cs:266 | `{editor} not found` | PARTIAL (no fix) | `{editor} not found · set its path here` |
| Windows/Import/AssetImporter.cs:119 | `Has {n} colours. A GBA palette holds at most 256.` | PARTIAL (no fix) | `Has {n} colours; a GBA palette holds at most 256. Reduce it to 256 colours or fewer in your paint program, then import it again.` |
| Windows/Import/AssetImporter.cs:133 | `{EXT} is not supported on GBA. Use .wav for sound effects or .mod / .xm for music.` + button "How to convert" | OK | - |
| Windows/Import/ImportErrorsDialog.cs:91 | `Open {file} in an audio editor and export it as .wav (sound effect, 11 kHz mono), or rebuild the song in a tracker and save it as .xm or .mod (music).` | OK | - (it is help, but shows as an error toast) |
| Windows/Import/AssetImporter.cs:139 | `AGB Studio can't import {ext} files.` | PARTIAL (no fix) | `AGB Studio can't import {ext} files. It imports .png, .tmx, .wav, .mod, .xm, .pal, .gpl, .ttf, .otf, .bdf and .cs.` |
| Windows/Import/AssetImporter.cs:146 | `{file} can't be read: {e.Message}` | PARTIAL (no fix; the reason is the reader's text) | `{file} can't be read: {reason}. Open it in the program that made it, save it again, and import the new file.` |
| Windows/Import/AssetImporter.cs:176 | `Frames are {w} × {h}; the biggest GBA sprite is 64 × 64.` | PARTIAL (no fix) | `Frames are {w} × {h}; the biggest GBA sprite is 64 × 64. Set a frame size of 64 × 64 or less, or cut the sheet into smaller frames.` |
| Windows/Import/AssetImporter.cs:206 | `Empty TMX file.` | POOR (via "can't be read"; no fix) | `{file} is empty. Save the map again in Tiled.` |
| Windows/Import/AssetImporter.cs:227 | `A layer has no data.` | POOR (no which layer, no fix) | `Layer {layer} of {file} has no tiles. Save the map again in Tiled.` |
| Windows/Import/AssetImporter.cs:243 | `Tiled {compression} compression is not supported; save the map as CSV.` | OK | - |
| Windows/Import/AssetImporter.cs:256 | `Tiles are {n} px; AGB Studio maps use 16 × 16 tiles.` | PARTIAL (no fix) | `Tiles are {n} px; AGB Studio maps use 16 × 16 tiles. Use a 16 × 16 tileset in Tiled and save the map again.` |
| Windows/Import/AssetImporter.cs:257 | `The map has no tile layers.` | PARTIAL (no fix) | `The map has no tile layers. Add one in Tiled and save the map again.` |
| Windows/Import/AssetImporter.cs:258 | `The map has {n} layers; the GBA shows at most 4 backgrounds.` | PARTIAL (no fix) | `The map has {n} layers; the GBA shows at most 4 backgrounds. Merge or delete layers in Tiled until 4 are left.` |
| Windows/Audio/WavFile.cs:16 | `Not a WAV file.` | PARTIAL (no fix) | `{file} is not really a WAV file. Export it again as .wav.` |
| Windows/Audio/WavFile.cs:37 | `The WAV file has no audio data.` | PARTIAL (no fix) | `{file} has no sound in it. Export it again from your audio editor.` |
| Windows/Audio/WavFile.cs:38 | `Only PCM and float WAV files can be imported.` | PARTIAL (no fix) | `{file} is a compressed WAV. Export it again as 16-bit PCM WAV.` |
| Windows/Audio/WavFile.cs:55 | `{bits}-bit WAV files are not supported.` | PARTIAL (no fix) | `{file} is {bits}-bit. Export it again as 8-, 16-, 24- or 32-bit WAV.` |
| Windows/Audio/TrackerModule.cs:86 | `Not a MOD or XM module.` | PARTIAL (no fix) | `{file} is not a MOD or XM song. Save it again from your tracker.` |
| Windows/Audio/TrackerModule.cs:154 | `Unknown MOD signature '{sig}'.` | POOR (jargon, no fix; old 15-sample MOD files end here) | `{file} is a MOD variant AGB Studio can't read. Save it again as a ProTracker .mod or as .xm.` |
| Windows/Import/ImportErrorsDialog.cs:109 | `Open a project before importing.` | OK | - |
| Windows/Import/ImportErrorsDialog.cs:120 | `{file} could not be imported: {e.Message}` | PARTIAL (no fix) | `{file} could not be imported: {reason}. Close programs that use the project folder and try again.` |
| Windows/Import/ImportErrorsDialog.cs:37 | hint `Files with errors are skipped.` | OK | - |
| Windows/Sprite/SpriteModel.cs:184 | `{file}: only PNG sheets can be imported.` | OK | - |
| Windows/Launcher/LauncherPage.cs:192 | `No .agbproj file in {folder}.` | PARTIAL (no fix) | `No .agbproj file in {folder}. Pick the folder that holds the project file.` |
| Windows/Launcher/LauncherPage.cs:519 | `The GBA hardware guide is not installed.` | PARTIAL (no fix) | `The GBA hardware guide is not installed. Reinstall AGB Studio to get it back.` |
| Windows/Launcher/LauncherService.cs:292 | `{sample} is not installed.` | PARTIAL (no fix) | `{sample} is not installed. Reinstall AGB Studio to get the samples back.` |
| Windows/Launcher/LauncherService.cs:316 | `{project} was not found.` | PARTIAL (no why, no fix) | `{project} was not found at {path}; it was moved or deleted. Open it from its new folder.` |
| Windows/Launcher/LauncherService.cs:351 | `Could not duplicate {project}: {e.Message}` | PARTIAL (no fix) | `Could not duplicate {project}: {reason}. Check there is free space and try again.` |
| Windows/Launcher/LauncherService.cs:382 | `Could not rename {project}: {e.Message}` | PARTIAL (no fix) | `Could not rename {project}: {reason}. Close programs that use its files and try again.` |
| Windows/Launcher/LauncherService.cs:409 | `Could not delete {folder}.` | POOR (no why, no fix) | `Could not move {folder} to the Recycle Bin. Close programs that use files in it, then try again.` |
| Windows/Launcher/NewProjectDialog.cs:254 | `Could not create the project: {e.Message}` | PARTIAL (depends on the inner text) | `Could not create the project: {reason}. Check the location and try again.` |
| Windows/Launcher/ProjectCreator.cs:132 | `The project needs a name.` | OK | - |
| Windows/Launcher/ProjectCreator.cs:133 | `ROM title: 12 characters max (A-Z, 0-9).` | OK | - |
| Windows/Launcher/ProjectCreator.cs:134 | `Game code: 4 characters (A-Z, 0-9).` | OK | - |
| Windows/Launcher/ProjectCreator.cs:136, Windows/Launcher/NewProjectDialog.cs:191 | `{folder} is not empty.` / `{folder} is not empty` | PARTIAL (no fix) | `{folder} is not empty. Pick an empty folder or another project name.` |
| Windows/Launcher/ProjectCreator.cs:137 | `The location needs a parent folder.` | PARTIAL (no why, no fix) | `{location} is a drive root. Pick a folder inside it.` |
| Windows/Launcher/NewProjectDialog.cs:191 | `A folder outside {devkitPro} is safer for updates` | OK | - |
| Windows/Launcher/ProjectCreator.cs:248 (toast via LauncherService.cs:276) | `Noto Sans Thai was not found, so the Thai font was not added.` | PARTIAL (no fix; shown as an error toast though the project was made) | `The project was made without the Thai font (Noto Sans Thai not found). Add a Thai font later in Font Manager.` |
| Windows/Launcher/ProjectCreator.cs:283 (toast via LauncherService.cs:276) | `git init failed: {git output}` | POOR (raw git output, no consequence, no fix) | `The project was made, but without a Git repository (git init failed: {first line}). Run git init in the project folder if you want one.` |
| Windows/Launcher/ProjectCreator.cs:287 (toast via LauncherService.cs:276) | `Git was not found, so no repository was made.` | PARTIAL (no fix) | `Git was not found, so no repository was made. Install Git and run git init in the project folder if you want version control.` |
| Windows/Launcher/OpeningProjectDialog.cs:241 | `{n} assets of {project} could not be read.` | PARTIAL (no which, no fix) | `{n} assets of {project} could not be read (first: {file}). Their cards say "cannot read"; open them or import them again.` |
| Windows/Launcher/OpeningProjectDialog.cs:250 | `Could not open {project}: {e.Message}` | PARTIAL (depends on the inner text, see Project files) | `Could not open {project}: {reason}.` with the inner texts fixed as under Project files |
| Windows/Launcher/ProjectContextMenu.cs:63 | `The project file could not be read.` (Delete project dialog) | PARTIAL (no why) | `The project file could not be read, so its contents are not listed. The whole folder is still deleted.` |
| Windows/Launcher/ProjectContextMenu.cs:57, 73 | `Delete {name}?` + `The project folder and everything in it is moved to the Recycle Bin and removed from the recent list:` | OK | - |
| Shell/Dialogs.cs:107, 124 | `Save changes before exiting?` + `{n} files have changes that will be lost if you don't save them.` | OK | - |
| Windows/SceneEditor/SceneCommands.cs:57 | `All four backgrounds are in use (BG0-BG3).` | PARTIAL (no fix) | `All four backgrounds (BG0-BG3) are in use. Delete or merge a layer before adding another.` |
| Windows/SceneEditor/SceneCommands.cs:107 | `{actor} has no C# script behaviour.` | PARTIAL (no fix) | `{actor} has no C# script behaviour. Add one in the Inspector first.` |
| Windows/SceneEditor/SceneCommands.cs:218 | `{type} is a built-in behaviour; it has no C# script.` | OK | - |
| Windows/SceneEditor/AssetActions.cs:161 | asset card `cannot read` | POOR (no why, no fix; the reason is dropped) | `can't read · {reason}` |
| Windows/SceneEditor/AssetActions.cs:365-366 | `{asset}: {problem}` + `Reimported {asset} · {n} warnings` | OK | - (problem texts are judged under Build pipeline) |
| Windows/Scripts/ExternalEditors.cs:244 | `{editor} was not found. Set the Script editor in Preferences › Paths.` | OK | - |
| Windows/Scripts/ExternalEditors.cs:258 | `Could not start {exe}: {e.Message}` | PARTIAL (no fix) | `Could not start {exe}: {reason}. Check the Script editor path in Preferences › Paths.` |
| Windows/Scripts/ScriptWorkspace.cs:259 | `Script check failed: {e.Message}` | PARTIAL (no where, no fix) | `Script check failed in {file}: {reason}.` |
| Windows/Scripts/ScriptWorkspace.cs:392 | `{file} changed on disk; your unsaved edits are kept` | OK | - |
| Windows/LogicGraph/LogicGraphPage.cs:227 | status `C# generation failed` (+ `// {e.Message}` in the code pane) | PARTIAL (no fix) | `C# generation failed: {reason}. Fix the graph and the code updates.` |
| Windows/Database/DatabasePage.cs:242 | `{field} must be a whole number.` | OK | - |
| Windows/Database/DatabasePage.cs:245 | `{field} must be a number.` | OK | - |
| Windows/Database/DatabasePage.cs:251 | `“{value}” is not a valid id.` | PARTIAL (no rule) | `“{value}” is not a valid id. Use letters, digits and _, starting with a letter.` |
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
| Windows/Settings/ProjectSettingsModel.cs:174 | `Two buttons have the same action name.` | PARTIAL (no which) | `{button} and {button} both use the action {name}. Rename one.` |
| Windows/Settings/ProjectSettingsModel.cs:177 | `Save field "{name}" must be one word (letters, digits, _).` | OK | - |
| Windows/Settings/ProjectSettingsModel.cs:178 | `Save field "{name}" has an unknown type "{type}".` | PARTIAL (no list of types) | `Save field "{name}" has an unknown type "{type}". Use one of: {types}.` |
| Windows/Settings/ProjectSettingsModel.cs:180 | `Slots must be 1 to 16.` | OK | - |
| Windows/Settings/ProjectSettingsModel.cs:182 | `The save slots do not fit in the save chip.` | PARTIAL (no numbers, no fix) | `{slots} slots need {n} bytes; the {type} chip holds {cap}. Use fewer slots or save fields, or a bigger save type.` |
| Windows/Settings/ProjectSettingsModel.cs:183 | `Warn ROM must be 1 to 32 MB.` | OK | - |
| Windows/Settings/ProjectSettingsModel.cs:184 | `Output folder is empty.` | PARTIAL (no tab named; shown in the footer) | `Build · Output folder is empty. Enter a folder, like build.` |
| Windows/Settings/ProjectSettingsModel.cs:185 | `Generated folder is empty.` | PARTIAL (no tab named; shown in the footer) | `Scripting · Generated folder is empty. Enter a folder.` |
| Windows/Settings/ProjectSettingsModel.cs:186 | `Namespace must be a C# name, like SkyHopper.` | OK | - |
| Windows/Build/ExportRomDialog.cs:125 | `Enter a file name.` | PARTIAL (also shown for a name with characters Windows does not allow) | `Enter a file name without \ / : * ? " < > \| characters.` |
| Windows/Build/ExportRomDialog.cs:126 | `Up to 12 letters, digits or spaces.` | OK | - |
| Windows/Build/ExportRomDialog.cs:127 | `4 letters or digits.` | OK | - |
| Windows/Build/ExportRomDialog.cs:128 | `major.minor.patch, e.g. 1.0.3` | OK | - |
| Windows/Build/ExportRomDialog.cs:129 | `Choose a folder.` | OK | - |
| Windows/Audio/AudioPage.cs:212 | `{file} could not be read: {e.Message}` | PARTIAL (no fix) | `{file} could not be read: {reason}. Import it again.` |
| Windows/Audio/AudioPage.cs:411 | `{file} could not be played: {e.Message}` | PARTIAL (no fix) | `{file} could not be played: {reason}. Check the audio output in Windows.` |
| Windows/Audio/SfxDesignerPage.cs:281 | `{file} could not be read: {e.Message}` | PARTIAL (no fix; the sound is replaced by an empty one) | `{file} could not be read: {reason}. An empty sound is shown; do not save over the file until it is fixed.` |
| Windows/Palette/PaletteManagerPage.cs:356 | `Could not import {file}` | POOR (reason dropped, no fix) | `Could not import {file}: {reason}. Palettes must be JASC .pal or GIMP .gpl files.` |
| Windows/Sprite/SpriteEditorPage.cs:365 | `Could not open {file}: {e.Message}` | PARTIAL (no fix) | `Could not open {file}: {reason}. Open it in a paint program and save it again as PNG.` |
| Windows/Sprite/SpriteEditorPage.cs:865 | `Could not import {file}: {e.Message}` | PARTIAL (no fix) | `Could not import {file}: {reason}. Save it again as PNG and import it.` |
| Windows/Menus/AboutDialog.cs:45 | `The third-party notices (THIRD_PARTY.md) are missing from this install.` | PARTIAL (no fix) | `The third-party notices (THIRD_PARTY.md) are missing from this install. Reinstall AGB Studio to restore them.` |
| Windows/Menus/MenuActions.cs:108 | `Start the game to take a screenshot.` | OK | - |
| Windows/Menus/MenuActions.cs:146 | `Could not clean the build folder: {e.Message}` | PARTIAL (no fix) | `Could not clean the build folder: {reason}. Stop the game and close programs that use the build folder, then try again.` |
| Windows/Menus/EditorLinks.cs:69 | `No update source configured` | PARTIAL (no fix) | `No update source is set. Add updateFeed to editor.json next to AGBStudio.exe.` |
| Windows/Menus/EditorLinks.cs:74 | `Could not check for updates: {e.Message}` | PARTIAL (no fix) | `Could not check for updates: {reason}. Check the internet connection and try again.` |
| Windows/Menus/EditorLinks.cs:84 | `Could not check for updates: the feed has no version` | PARTIAL (no fix) | `Could not check for updates: the update feed has no version. Check updateFeed in editor.json.` |
| Windows/Menus/EditorLinks.cs:91 | `Could not check for updates: the feed is not JSON` | PARTIAL (no fix) | `Could not check for updates: the update feed is not JSON. Check updateFeed in editor.json.` |
| Shell/WindowService.cs:176 | `{id} {name} is not built yet. Requested by {source}.` (for example "T-D23 ... Requested by BuildRomFailed.") | POOR (shows internal design ids and caller names, no fix) | `{name} is not in this version of AGB Studio yet.` (keep the ids for the log only) |

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
- Where the emulator exceptions (`LibretroCore.cs:114/180/187`) surface was not traced end to end; no catch for them
  was seen in `PlayController.Run`.
- The analyzer help links point to `https://agbstudio.local/docs/scripting/rules#` (`GaeRules.cs:11`), which is not a
  public address; whether the editor shows them was not checked.
- No crash or unhandled-exception message exists in the editor at this snapshot (G-ERR.06), so there was nothing to review.
- Strings were judged from the code. The editor was not run, so whether each text fits its box, wraps, or is hidden by
  another message was not verified.
- The editor component `Gallery` and the Script Editor terminal panel (process output and `exit code {n}`) were skipped.
