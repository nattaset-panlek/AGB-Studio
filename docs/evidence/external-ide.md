# P14.03 / P14.07 record: external IDEs on the development machine (2026-10-31)

Checklist items:

- P14.03 "Open .cs with" and Open externally open the file at the right line [T-S07, T-Y03] · Verify: `E2E` in each
  installed IDE; record `n/a` for IDEs not installed.
- P14.07 Analyzer rules and the "Change to fixed" fix work inside VS Code, Rider and Visual Studio · Verify: `E2E` per
  IDE.

This record says which IDEs are installed and what has and has not been verified. No IDE window was opened while
writing it (the session was told not to), so no in-IDE check was run.

## Which IDEs are installed

Checked on 2026-10-31 by looking at the same places `ExternalEditors.Candidates`
(`editor/AgbStudio.Editor/Windows/Scripts/ExternalEditors.cs`) looks: the install folders under `%LOCALAPPDATA%`,
`%ProgramFiles%` and `%ProgramFiles(x86)%`, the `App Paths` registry keys (HKCU and HKLM), `PATH`, the JetBrains
Toolbox folder and `vswhere.exe`. Nothing was started except `vswhere.exe`.

| IDE | Installed | Where it was found | Result for P14.03 / P14.07 |
|---|---|---|---|
| Visual Studio | yes: Visual Studio 18 Community, `devenv.exe` 18.9.12120.119 | `D:\Program Files\Microsoft Visual Studio\18\Community\common7\ide\devenv.exe`, through the HKLM `App Paths\devenv.exe` key | not verified yet (see below) |
| VS Code | no | no `Code.exe` in `%LOCALAPPDATA%\Programs\Microsoft VS Code`, `%ProgramFiles%` or `%ProgramFiles(x86)%`, no `App Paths` key, no `Microsoft VS Code\bin` on `PATH` | n/a until installed |
| Rider | no | no `rider64.exe` in `%LOCALAPPDATA%\Programs\Rider`, no JetBrains Toolbox Rider folder, nothing under `%ProgramFiles%\JetBrains`, no `App Paths` key | n/a until installed |

Notes on detection:

- `vswhere.exe -all -prerelease -products *` exists but returned no instance on this machine, and the fixed path list in
  `ExternalEditors` only covers `%ProgramFiles%` and `%ProgramFiles(x86)%` (drive C:). Visual Studio installed on drive
  D: is found only through the `App Paths` registry key. If that key is missing on another machine, Preferences ·
  Editor would not find Visual Studio there; the user can still pick it as a custom editor.
- The test `ScriptToolsTests.ThisMachine_RecordsWhichIdesAreInstalled` writes the detection result to the test output
  only; it asserts nothing about this machine.

## What is verified without an IDE

| Check | Evidence | Covers |
|---|---|---|
| Command line per IDE: VS Code `-g "file:line:column"` with the project folder, Rider `--line N --column M "file"`, Visual Studio `/Edit "file" /Command "Edit.GoTo N"`, Custom with `{file}` `{line}` `{column}` `{project}` | `ScriptToolsTests.OpenExternally_StartsTheConfiguredEditorAtTheCaretLine` (fake executables) | P14.03 up to starting the process; not that the IDE lands on the line |
| Generated IDE project: `<Project>.csproj` references the API assembly and both analyzer assemblies (`build/ide/sdk/`), `.editorconfig` holds the GAE severities, `.vscode` settings turn on Roslyn analyzers | `tools/AgbStudio.Scripting/CsprojGenerator.cs`; `IdeTests.GeneratedProjectBuildsWithTheSameGaeDiagnostics` (`dotnet build` of the generated project reports the same GAE diagnostics) | P14.07 on the command line, not inside an IDE |
| "Change to fixed" code fix | `CodeFixTests.ChangeToFixedRewritesTypeAndLiteral`, `ChangeToFixedOnLocalsAndReturnTypes` (in-process Roslyn); the fix writes `Fixed` when the project has the alias (the generated IDE project adds `global using Fixed = GBA.@fixed;`), else `@fixed` (D-22) | P14.07 logic, not the IDE light bulb |

These tests are listed from the code and the audits (`docs/evidence/audit-p10-p16.md` on `main`); they were not run
for this record.

## Not verified

- P14.03 in Visual Studio 18: open a script from the Script Editor (Open externally, and "Open .cs with" set to Visual
  Studio in Preferences · Editor) and check that Visual Studio shows the file at the caret line.
- P14.07 in Visual Studio 18: open the generated `<Project>.csproj` and check that GAE001-GAE006 appear with the
  severities from Project Settings · Scripting, and that the "Change to fixed" light-bulb fix is offered and gives code
  that compiles.
- P14.03 and P14.07 in VS Code and Rider: `n/a` until they are installed.

Both Visual Studio checks need someone to open the IDE (the user, or a session allowed to start it); until then
P14.03 and P14.07 stay open.

## In-IDE check in Visual Studio 18 (2026-10-31, owner approved opening VS)

Driven through VS DTE automation (`docs/evidence/ide/vs-check.ps1`, log `vs-check.log`):

- P14.03: the `/Edit file /Command "Edit.GoTo 20"` line AGB Studio uses opened PlayerController.cs with the caret on line 20 (`P14.03-vs-open-at-line.png`).
- P14.07: building the generated project in VS shows 1 error and 7 warnings in the Error List, the same GAE001-GAE006 set `dotnet build` reports (`P14.07-vs-error-list.png`); Quick Actions on the `float` field applied "Change to fixed" (line 8 became `Fixed coyoteTime = 0.1;`, `P14.07-vs-after-fix.png`). Nothing was saved.
- VS Code and Rider: n/a until installed.
