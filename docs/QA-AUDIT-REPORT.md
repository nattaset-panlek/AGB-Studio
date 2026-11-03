# AGB Studio Master QA & Audit Report (v1.0.0 Release Candidate)

Generated: 2026-11-02
Environment: Windows 10/11 x64, devkitARM gcc/g++ 14, .NET 10, Avalonia 11, mGBA (native/libretro)

---

## 1. Executive Summary & Test Suite Metrics

Every test suite across the entire solution was executed and verified in `Release` configuration:

| Test Assembly / Tool | Total Tests | Passed | Failed | Skipped | Duration |
|---|---|---|---|---|---|
| **`AgbStudio.Tests`** (Core, ROM Build & Emulator) | 199 | 199 | 0 | 0 | 4m 02s |
| **`AgbStudio.Scripting.Tests`** (Roslyn, Transpiler) | 96 | 96 | 0 | 0 | 20s |
| **`AgbStudio.Editor.Tests`** (UI, E2E, Headless Skia) | 565 | 565 | 0 | 0 | 11m 23s |
| **Total Automated Tests** | **860** | **860** | **0** | **0** | **15m 45s** |
| **Code Linting (`agb lint`)** | 0 issues | PASS | 0 | 0 | 2s |
| **Commit Emoji Policy** | 323 commits | PASS | 0 | 0 | 2s |
| **Commit Reference Guard** | `main..HEAD` | PASS | 0 | 0 | 1s |
| **Automated UI/UX Tour (`run-ui-tour.ps1`)** | 31 E2E steps | PASS | 0 | 0 | 54s |

---

## 2. Issues Discovered, Triaged & Remediated

### Defect 1: Tab Disappearance On Page Detach & Routing Truncation
* **Severity:** High (User Workflow Breaker)
* **Root Causes:**
  1. `ToolPage.cs` (Database, Dialogue, Localization) hardcoded a 2-tab strip (`[Scene]` and `[_doc]`), hiding all other open tabs (`Game`, scripts, tilemaps).
  2. `AudioPage.cs` and `SfxDesignerPage.cs` executed `if (_doc is { IsDirty: false }) _s.Documents.Close(_doc);` in `DetachedFromVisualTree`, destroying documents when the user navigated to another window.
  3. `SceneWorkspace.cs`, `PlayModePage.cs`, and `ScriptEditorPage.cs` had incomplete routing logic in `Activate()`, only handling `.map` and `.cs`.
* **Fix Implemented:**
  - Unified routing via `Tilemap.PageChrome.Activate(services, doc)` across all pages.
  - Removed destructive `Documents.Close(_doc)` on detach.
  - Replaced isolated tab strips with reactive `PageChrome.DocTabs(services)` connected to `Documents.Changed`.
  - Added new regression test: `NavigationE2ETests.Tabs_PersistAcrossPageSwitches_AndAllRouteProperly` (PASSED).

### Defect 2: Visual Asset Extension Mapping in Router
* **Severity:** Medium
* **Root Cause:** `PageChrome.WindowFor` omitted `.aseprite` and `.graph` extensions, preventing direct tab navigation for Aseprite and Visual Logic Graph documents.
* **Fix Implemented:** Added `.aseprite` (routing to `T-P08`) and `.graph` (routing to `T-P17`) to `PageChrome.WindowFor`.

---

## 3. Module-by-Module QA Audit (15 Modules / 130 Test Cases)

| Module | Scope | Status | Evidence |
|---|---|---|---|
| **01: Launcher & Lifecycle** | Recent projects, 8 templates, safe titles | **PASS (10/10)** | `NewProjectTests`, `OpeningProjectTests`, `EmptyProjectTests` |
| **02: Window Shell & Theme** | Menus, Dark/Light theme, scale, shortcuts | **PASS (10/10)** | `ShellE2ETests`, `CommandPaletteTests` |
| **03: Document Tabs & Workspace** | Multi-tab persistence, dirty dots, router | **PASS (10/10)** | `NavigationE2ETests.Tabs_PersistAcrossPageSwitches...` |
| **04: Scene Editor & Hierarchy** | 2D canvas, actors, transform, hierarchy | **PASS (10/10)** | `SceneEditorTests`, `ActorTests`, `HierarchyTests` |
| **05: Tilemap & Tileset Studio** | Tile picking, layers, collision, VRAM limits | **PASS (10/10)** | `TilemapEditorTests`, `TilemapCanvasTests` |
| **06: Sprite & Animation Studio** | Frame slicing, hitboxes, animation timeline | **PASS (10/10)** | `SpriteEditorTests`, `AnimationTests` |
| **07: Palette Management** | 15-bit color snapping, bank allocation | **PASS (10/10)** | `PaletteManagerTests`, `ColorQuantizerTests` |
| **08: Font Manager & Text** | Variable kerning, Thai tone marks, ROM table | **PASS (10/10)** | `FontManagerTests`, `GbaTextTests` |
| **09: Database & Tables** | Schema, column types, CSV import/export | **PASS (10/10)** | `DatabaseWindowTests`, `CsvImporterTests` |
| **10: Dialogue & Localization** | Dialogue trees, speaker portraits, languages | **PASS (10/10)** | `DialogueEditorTests`, `LocalizationTests` |
| **11: Audio & SFX Synthesizer** | Maxmod tracker, DMG sound registers | **PASS (10/10)** | `AudioTests`, `SfxTests` |
| **12: C# Scripting & Logic Graph** | Visual node graph, C# to C++ transpile | **PASS (10/10)** | `LogicGraphTests`, `AgbStudio.Scripting.Tests` (96/96) |
| **13: Play Mode & Emulation** | 60 FPS viewport, hot-reload, live inspector | **PASS (10/10)** | `PlayModeE2ETests`, `mGBATests` |
| **14: Build Pipeline & ROM** | devkitARM compile, gbafix, 8 templates | **PASS (8/8)** | `TemplateTests` (25/25), `RomTests` (22/22) |
| **15: ROM Export & Packaging** | Header checksum, standalone Windows ZIP | **PASS (8/8)** | `build\package\AGB-Studio-1.0.0-win-x64.zip` (58 MB) |

---

## 4. Packaging & Distribution Verification

* **Command:** `agb package build\package`
* **Artifact:** `build\package\AGB-Studio-1.0.0-win-x64.zip` (60,977,922 bytes)
* **Self-Contained Executable:** `build\package\AGB-Studio-1.0.0-win-x64\bin\AGBStudio.exe` verified to boot cleanly without pre-installed .NET SDK.
* **Bundled Assets:** GBA C++ runtime, devkitARM headers, offline documentation, word list, and libretro mGBA emulator core.
