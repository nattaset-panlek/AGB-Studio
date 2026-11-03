# Game Creation & Re-check Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Thoroughly recheck and test all game creation, template generation, ROM compilation, and emulation workflows for AGB Studio to prepare for final Phase 16 release sign-off.

**Architecture:** Automated multi-tier testing pipeline combining .NET xUnit suites (`AgbStudio.Tests`, `AgbStudio.Editor.Tests`), `agb` CLI tools (`template`, `build`, `romtest`), embedded mGBA emulation verification, and manual QA flow derived from `docs/QA-RECHECK.md`.

**Tech Stack:** C# .NET 10, Avalonia 11, devkitARM (gcc 14 / g++), libtonc, mGBA (libretro/native core), gbafix.

**Spec:** `docs/ENGINE-CHECKLIST.md` (Phases 11, 12, 15, 16) and `docs/QA-RECHECK.md`.

## Global Constraints
- Target platform: Windows 10/11 x64
- GBA ROM specifications: Mode 0-3, VRAM limits (BG <= 64 KB, OBJ <= 32 KB), 256 KB WRAM, 32 KB IWRAM
- Zero compiler warnings in C# -> C++ emitted code
- All 8 templates and 4 sample projects must compile to valid bootable `.gba` ROMs

---

### Task 1: Re-verify All 8 Templates & 4 Sample Projects Compilation and Booting
**Files:**
- Test: `tests/AgbStudio.Tests/TemplateTests.cs`
- Test: `tests/AgbStudio.Tests/FrameworkPlayTests.cs`
- Source: `tools/AgbStudio.Build/Content/Templates/*.cs`

**Interfaces:**
- Consumes: `TemplateCatalog.All`, `GameProject`, `BuildPipeline`
- Produces: 8 playable template ROMs + 4 sample project ROMs booted in mGBA

- [x] **Step 1: Run template automated unit & emulator boot test suite**
  Run: `dotnet test tests/AgbStudio.Tests/AgbStudio.Tests.csproj --filter "FullyQualifiedName~Template" -c Release`
  Expected: 25/25 PASSED (Verified: 25 passed in 1.09 min)

- [x] **Step 2: Manually test end-to-end template project creation via CLI**
  Run: `agb template platformer build/tmp/test_platformer "Test Platformer"`
  Expected: Creates project file `TestPlatformer.agbproj` with default scene, scripts, audio, and maps.

- [x] **Step 3: Manually test end-to-end ROM build via devkitARM toolchain**
  Run: `agb build build/tmp/test_platformer`
  Expected: Converts assets -> transpiles C# to C++ -> compiles with arm-none-eabi-g++ -> links `test_platformer.gba`.

- [x] **Step 4: Manually test ROM emulation boot & screenshot capture**
  Run: `agb romtest build/tmp/test_platformer/build/test_platformer.gba --frames 120 --shot build/tmp/test_platformer/shot.png`
  Expected: ROM boots, fades into Title screen, captures frame screenshot without crash.

---

### Task 2: Re-verify Editor Game Creation & ROM Export Workflows
**Files:**
- Test: `tests/AgbStudio.Editor.Tests/Windows/Launcher/NewProjectTests.cs`
- Test: `tests/AgbStudio.Editor.Tests/Windows/Build/BuildRomTests.cs`
- Test: `tests/AgbStudio.Editor.Tests/Windows/Build/ExportRomTests.cs`
- Source: `editor/AgbStudio.Editor/Windows/Launcher/NewProjectDialog.axaml.cs`
- Source: `editor/AgbStudio.Editor/Windows/Build/BuildRomDialog.axaml.cs`

**Interfaces:**
- Consumes: `NewProjectDialog`, `BuildRomDialog`, `ExportRomDialog`
- Produces: Editor UI actions for game creation and binary release

- [x] **Step 1: Run Editor NewProject, BuildRom, and ExportRom test suite**
  Run: `dotnet test tests/AgbStudio.Editor.Tests/AgbStudio.Editor.Tests.csproj --filter "FullyQualifiedName~NewProject|FullyQualifiedName~BuildRom|FullyQualifiedName~ExportRom" -c Release`
  Expected: 27/27 PASSED (Verified: 27 passed in 1.13 min)

- [ ] **Step 2: Verify Unsaved Changes prompt on creating a new project over an open project**
  Test case: `NewProject_WithAnUnsavedFile_AsksFirst_CancelKeepsIt_SaveAllSavesThenCreates` passes cleanly.

- [ ] **Step 3: Verify Release ROM padding and header validation**
  Test case: `PaddedReleaseRom_BootsToTheSameFramesAsTheBuiltRom` confirms release export matches mGBA execution.

---

### Task 3: Interactive QA Re-check (Manual Verification Guide)
**Files:**
- Guide: `docs/QA-RECHECK.md`
- Target: `build/package/AGB-Studio-1.0.0-win-x64.zip` or `build-and-run.cmd`

**Interfaces:**
- Consumes: User interaction on Windows desktop
- Produces: QA check log for P16.01, P16.06, P16.X1

- [ ] **Step 1: Test App Launch**
  Run `build-and-run.cmd` or launch `build/package/AGB-Studio-1.0.0-win-x64/AGBStudio.exe`.
  Expected: Launcher window opens cleanly with Recent Projects and Templates cards.

- [ ] **Step 2: Test Create & Play**
  Click New Project -> select *Platformer* -> Name "MyFirstGbaGame" -> Create -> Click Play.
  Expected: Embedded emulator plays the game, player runs/jumps, frame counter increments.

- [ ] **Step 3: Test Export ROM**
  Click File -> Export ROM -> Select target folder -> Verify generated `.gba` file size and boot in external emulator (mGBA/VBA-M).

---

### Task 4: Release Packaging & Final Sign-off Gate (Phase 16)
**Files:**
- Doc: `docs/ENGINE-CHECKLIST.md`
- Doc: `docs/RELEASE-NOTES.md`

- [ ] **Step 1: Close P16.01**
  Verify portable zip package on clean user profile.
- [ ] **Step 2: Close P16.06**
  Verify CI report is green and confirm hardware testing status (or skip per D-06).
- [ ] **Step 3: Close P16.E1 & P16.X1**
  User explicitly approves release in chat.
