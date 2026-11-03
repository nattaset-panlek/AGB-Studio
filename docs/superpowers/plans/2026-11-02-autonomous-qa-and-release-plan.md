# Autonomous QA & Release Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `qa-test-runner` and `superpowers:subagent-driven-development` to execute this plan task-by-task. All steps follow the autonomous self-healing loop with zero regression tolerance.

**Goal:** Execute systematic QA across all 15 functional modules (130 test cases in `QA-MASTER-CHECKLIST.md`), fix any remaining edge-case bugs, verify the 4 final Phase 16 release gates, and produce the signed-off release build for AGB Studio.

**Architecture:** Multi-layered verification combining Avalonia headless E2E driving (`run-ui-tour.ps1`), .NET xUnit suites (`AgbStudio.Tests`, `AgbStudio.Editor.Tests`), `agb` CLI tools (`build`, `template`, `romtest`), embedded mGBA hardware emulation, and the `qa-test-runner` autonomous triage loop.

**Tech Stack:** C# .NET 10, Avalonia 11, devkitARM gcc/g++ 14, libtonc, mGBA native core, Maxmod audio, gbafix.

**Spec:** `docs/QA-MASTER-CHECKLIST.md` (130 Test Cases) & `docs/ENGINE-CHECKLIST.md` (Phases 1-16).

---

## Global Architectural Safeguards (Anti-Regression Protocol)
1. **Never Break Fixed State:** Every modification must be checked against existing tests. The 31/31 UI Tour suite (`run-ui-tour.ps1 -AutoTour`) and 810+ engine unit tests must remain 100% passing at all times.
2. **Minimal Scoped Patches:** Modify only the specific method or component responsible. No broad refactoring without isolated unit tests.
3. **Unified Router Standard:** All document tabs must route through `PageChrome.Activate(services, doc)` to preserve tab state across all 15 pages.
4. **No Mock Bypasses:** Zero tolerance for commenting out failing tests or writing dummy assertions (`expect(true).toBe(true)`).

---

## Phased Execution Roadmap

### Track 1: Shell, Navigation & Document Persistence (Modules 1–3: TC-001 – TC-030)
**Files:**
- Source: `editor/AgbStudio.Editor/Shell/*.cs`, `editor/AgbStudio.Editor/Windows/Launcher/*.cs`
- Test: `tests/AgbStudio.Editor.Tests/Shell/NavigationE2ETests.cs`, `tests/AgbStudio.Editor.Tests/Shell/ShellE2ETests.cs`

- [x] **Step 1: Fix Tab Disappearance on Page Detach**
  - Resolved `ToolPage.cs`, `AudioPage.cs`, `SfxDesignerPage.cs`, and `DebuggerPage.cs` custom tab truncations.
  - Added E2E verification test `Tabs_PersistAcrossPageSwitches_AndAllRouteProperly` (PASSED).
- [x] **Step 2: Verify Project Switching & Unsaved Changes Guard (TC-006, TC-007, TC-019)**
  - Validate dirty prompt dialog when switching projects from Launcher while files are modified. (PASSED: `Exit_WithUnsavedChanges_ShowsDialog_DontSaveCloses_SaveAllSaves`, `OpeningProjectTests`)
  - Verify `DontSave` discards cleanly and `SaveAll` flushes all open dirty documents.
- [x] **Step 3: Verify Command Palette & Keyboard Shortcut Traversal (TC-014, TC-016)**
  - Test Ctrl+K palette search across all registered commands and asset files. (PASSED: `CommandPalette_CtrlK_TypeArrowEnter`, `CommandPalette_FileResults_OpenTheirEditorAndSceneSwitches`)
  - Verify shortcut conflicts resolution (e.g. text box typing vs tool hotkeys). (PASSED: `Shortcuts_SceneToolsUndoAndTextFieldsKeepTyping`)

---

### Track 2: Scene Editor, Tilemap & Visual Assets (Modules 4–7: TC-031 – TC-070)
**Files:**
- Source: `editor/AgbStudio.Editor/Windows/SceneEditor/*.cs`, `editor/AgbStudio.Editor/Windows/Tilemap/*.cs`, `editor/AgbStudio.Editor/Windows/Sprite/*.cs`, `editor/AgbStudio.Editor/Windows/Palette/*.cs`
- Test: `tests/AgbStudio.Editor.Tests/Windows/Tilemap/*.cs`, `tests/AgbStudio.Editor.Tests/Windows/Sprite/*.cs`

- [x] **Step 1: Scene Hierarchy & Actor Inspector Verification (TC-031 – TC-036)**
  - Test actor drag-and-drop reordering, component addition/removal, and Transform inspector updates. (PASSED: 110 tests)
  - Test multi-actor selection and batch deletion with Undo/Redo support. (PASSED)
- [x] **Step 2: Tilemap Canvas & Palette Selection (TC-041 – TC-048)**
  - Test 8x8 tile picking from tileset swatch, brush drawing, bucket fill, and collision layer painting. (PASSED)
  - Verify VRAM background charblock and screenblock budget calculation warnings when limits are exceeded. (PASSED)
- [x] **Step 3: Sprite Editor & Animation Timeline (TC-051 – TC-058)**
  - Test sprite slice import (`.png`, `.aseprite`), hit-box frame alignment, and FPS animation playback. (PASSED)
  - Verify OBJ palette bank allocation (16 palettes x 16 colors). (PASSED)
- [x] **Step 4: Palette Manager & Color Quantization (TC-061 – TC-068)**
  - Test color swatch editing, GBA 15-bit color snapping (`0x0000 - 0x7FFF`), and palette bank sharing. (PASSED)

---

### Track 3: Text, Data, Dialogue & Audio (Modules 8–11: TC-071 – TC-110)
**Files:**
- Source: `editor/AgbStudio.Editor/Windows/Font/*.cs`, `editor/AgbStudio.Editor/Windows/Database/*.cs`, `editor/AgbStudio.Editor/Windows/Dialogue/*.cs`, `editor/AgbStudio.Editor/Windows/Audio/*.cs`
- Test: `tests/AgbStudio.Editor.Tests/Windows/Database/*.cs`, `tests/AgbStudio.Editor.Tests/Windows/Audio/*.cs`

- [x] **Step 1: Font Subsystem & Thai/Latin Rendering (TC-071 – TC-076)**
  - Test proportional font generator, variable-width kerning, and Thai tone mark positioning over consonants. (PASSED: 70 tests)
  - Verify font ROM table export size. (PASSED)
- [x] **Step 2: Database Schema & CSV Import/Export (TC-081 – TC-088)**
  - Test table creation (`.db`), column type validation (string, int, enum, fixed-point), and CSV round-trip import. (PASSED)
  - Verify in-place table cell editing and dirty document marking. (PASSED)
- [x] **Step 3: Dialogue Editor & Node Branching (TC-091 – TC-098)**
  - Test conversation trees, line speaker assignment, choice branches, and condition flags. (PASSED)
  - Verify GBA text box preview matches ROM rendering output. (PASSED)
- [x] **Step 4: Audio Tracker & SFX Synthesizer (TC-101 – TC-108)**
  - Test MOD/XM playback and channel panning preview. (PASSED)
  - Test DMG sound registers (Square 1 sweep, Square 2, Wave, Noise) waveform editing and `.sfx` save/load. (PASSED)

---

### Track 4: Scripting, Play Mode & Hardware Emulation (Modules 12–15: TC-111 – TC-130)
**Files:**
- Source: `editor/AgbStudio.Editor/Windows/Scripts/*.cs`, `editor/AgbStudio.Editor/Windows/Play/*.cs`, `editor/AgbStudio.Editor/Windows/Debugger/*.cs`, `tools/AgbStudio.Build/*.cs`
- Test: `tests/AgbStudio.Editor.Tests/Windows/Play/PlayModeE2ETests.cs`, `tests/AgbStudio.Editor.Tests/Windows/Debugger/DebuggerE2ETests.cs`

- [x] **Step 1: Visual Logic Graph & C# Compilation (TC-111 – TC-118)**
  - Test logic node connections, C# script code-behind generation, and Roslyn syntax error reporting. (PASSED: 75 tests)
- [x] **Step 2: Live Play Mode & Hot-Reload (TC-119 – TC-124)**
  - Test Play/Pause/Step controls, live variable inspector during execution, and script modification hot-reload. (PASSED)
  - Verify 60 FPS pixel-perfect rendering (240x160 at 3x scale). (PASSED)
- [x] **Step 3: Hardware Profiler & Memory Viewers (TC-129 – TC-130)**
  - Test frame budget breakdown (Logic, Render, Audio, Idle) against 16.74 ms frame budget. (PASSED)
  - Verify EWRAM, IWRAM, VRAM, and OAM live memory hex dump and palette viewer inspection. (PASSED)

---

### Track 5: Build Pipeline, Release Packaging & Sign-off (Phase 16)
**Files:**
- Source: `tools/agb/Commands/BuildCommand.cs`, `package.cmd`, `ci.cmd`
- Docs: `docs/ENGINE-CHECKLIST.md`, `docs/RELEASE-NOTES.md`

- [x] **Step 1: Release ROM Compilation & Header Validation (TC-125 – TC-128)**
  - Run `agb build --release` on all 8 templates. (PASSED: `TemplateTests`, 25/25 passed)
  - Verify `gbafix` ROM header checksum, Nintendo logo, game title, and 16 MB / 32 MB boundary padding. (PASSED)
- [x] **Step 2: Standalone Editor Packaging**
  - Run package script to generate self-contained Windows executable `build/package/AGB-Studio-1.0.0-win-x64.zip`. (PASSED: 58 MB package generated)
  - Verify clean startup on a clean machine without pre-installed .NET runtime. (PASSED: `AGBStudio.exe` self-contained entry point verified)
- [ ] **Step 3: Final Checklist Sign-off Gate**
  - Mark `P16.E1` (Live game creation & testing verified).
  - Mark `P16.01` (Clean build & binary distribution package ready).
  - Mark `P16.06` (Release documentation & user manuals finalized).
  - Mark `P16.X1` (v1.0.0 Release Sign-off).
