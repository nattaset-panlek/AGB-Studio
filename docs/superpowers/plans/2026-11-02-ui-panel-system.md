# Implementation Plan: Structured UI Panel / RectTransform System with Padding, Margins & 9-Slice Frame

## Overview
This plan implements a structured UI Panel and RectTransform layout constraint system in AGB Studio across:
1. **Core Data Model** (`tools/AgbStudio.Core/Project/SceneModel.cs`): `ActorKind.Panel`, `UiPadding`, `UiMargin`, `PanelBackground`, and layout properties on `ActorData`.
2. **Build Pipeline & Validation Guard** (`tools/AgbStudio.Build/Pipeline/GameBuilder.cs`): Tile overlap detection & guard warning, ascender-safe box height calculation taking padding into account, and code generation for Panel actors.
3. **Native Runtime C++ Engine** (`runtime/include/agb/render/text.hpp`, `runtime/src/render/text.cpp`, `runtime/include/agb/actor.hpp`, `runtime/src/render/textdb.cpp`, `runtime/src/framework/game.cpp`): Padding-aware `TextBox`, `drawPanelFrame(Style style)`, fallback font resolution and paper fill in `choose()`, and `ActorKind::Panel` handling.
4. **C# Scripting API** (`tools/AgbStudio.Api`): `UI`, `Panel`, and `PanelStyle` with strict Unity `#region` organization.
5. **Integration & Regression Testing** (`tests/AgbStudio.Tests/PokeEmeraldTests.cs` and new unit tests): Verification of zero glyph clipping, zero tile overwrite collisions, no transparent background leakage in choice menus, and 100% passing tests.

---

## Phase 1: Core Data Model (`tools/AgbStudio.Core`)

### Task 1.1: Extend `SceneModel.cs` with `ActorKind.Panel` and Layout Properties
- Add `Panel` to `ActorKind` enum.
- Define `UiPadding(int Left, int Top, int Right, int Bottom)` record.
- Define `UiMargin(int Left, int Top, int Right, int Bottom)` record.
- Define `PanelBackground` enum (`None`, `SolidPaper`, `NineSlice`, `Transparent`).
- Add `Padding`, `Margin`, `Background`, `PaperColorIndex`, `BorderColorIndex`, `TileSnap` to `ActorData`.

---

## Phase 2: Native Runtime C++ Engine (`runtime/`)

### Task 2.1: Update `runtime/include/agb/actor.hpp`
- Add `Panel` to `enum class ActorKind : u8`.

### Task 2.2: Add Padding and Panel Frame Drawing to `TextBox`
- In `runtime/include/agb/render/text.hpp`:
  - Add `struct Padding { u8 left = 4, top = 2, right = 4, bottom = 2; };`
  - Add `Padding padding_` field, `padding()` getter, and `setPadding(Padding p)` method.
  - Declare `void drawPanelFrame(Style style);`
- In `runtime/src/render/text.cpp`:
  - Implement `TextBox::drawPanelFrame(Style style)`: Fills background with `style.paper` (index 5) and draws a 1-pixel border or box frame with `style.ink` (index 1).
  - In `TextBox::drawRun`: offset each placed glyph by `padding_.left` and `padding_.top`.
  - In `TextBox::drawGlyph`: offset by `padding_.left` and `padding_.top` when rendering through `blitGlyph`.
  - In `TextBox::show` and `TextBox::update`: ensure `padding_.top` is added to `py` calculation.

### Task 2.3: Fix `runtime/src/render/textdb.cpp` Standalone Choice & Transparency
- In `drawChoices()`:
  - When `g_current == nullptr`, ensure fallback to `run(g_options[0])->font`.
  - Ensure background of choice options is cleared to solid paper color 5 (not transparent index 0).
  - Ensure `choose()` calls `ensureOpen()` and updates `g_box`.

### Task 2.4: Update Actor Initialization in `runtime/src/framework/game.cpp`
- Handle `ActorKind::Panel` alongside `ActorKind::TextLabel` in `game.cpp` scene loading loop.

---

## Phase 3: Build Pipeline & Validation Guard (`tools/AgbStudio.Build`)

### Task 3.1: Tile Overlap Guard in `GameBuilder.cs`
- In `CheckHardwareLimits` or a dedicated `CheckTextLayoutGuards` method:
  - Iterate through all `ActorKind.TextLabel` and `ActorKind.Panel` actors.
  - Calculate `yStartTile = y / 8` and `yEndTile = (y + heightPx + 7) / 8`.
  - Check for vertical overlaps (`yEndTile_A > yStartTile_B`) among actors on the same text layer.
  - Emit diagnostic warning `Scene {scene.Name}: TextLabel/Panel '{a.Name}' and '{b.Name}' share VRAM tile rows...`.

### Task 3.2: Ascender-Safe Box Height Calculation
- Calculate box height considering padding: `heightPx = paddingTop + (lines * font.LineHeight) + paddingBottom;`

### Task 3.3: Codegen for Panel Actors in `GameBuilder.cs`
- In `WriteActorDef`: output `agb::ActorKind::Panel` when `a.Kind == ActorKind.Panel`.

---

## Phase 4: C# Scripting API (`tools/AgbStudio.Api`)

### Task 4.1: Add `UI.cs` in `tools/AgbStudio.Api`
- Organized strictly with Unity `#region` conventions:
  - `[Variables/Fields]`
  - `[Properties]`
  - `[Public Methods]`
  - `[Private Methods]`
- Define `PanelStyle` enum (`SolidWindow`, `Transparent`, `BorderOnly`, `NineSlice`).
- Define `UI` static class with `CreatePanel` and `ShowDialog`.

---

## Phase 5: Verification & Testing

### Task 5.1: Unit & Integration Tests
- Verify `PokeEmeraldTests`:
  `Set-Location D:\devkitPro\AGB-Studio; dotnet test tests/AgbStudio.Tests/AgbStudio.Tests.csproj --filter "FullyQualifiedName~PokeEmeraldTests"`
- Verify Editor navigation & debugger tests:
  `dotnet test tests/AgbStudio.Editor.Tests/AgbStudio.Editor.Tests.csproj --filter "FullyQualifiedName~NavigationE2ETests|FullyQualifiedName~DebuggerE2ETests"`
- Verify MCP tests:
  `dotnet test tests/AgbStudio.Mcp.Tests/AgbStudio.Mcp.Tests.csproj`
- Verify published package in `build/package/win-x64/`.
