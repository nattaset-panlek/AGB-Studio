# Audit: Phase 10-16 against the code (2026-10-31)

Scope: every entry gate, item and exit gate of "Phase 10" to "Phase 16" in `docs/ENGINE-CHECKLIST.md`, checked
read-only against the repository. Companion of `docs/evidence/audit-p0-p9-gates.md` (same status words and format).

Snapshot: branch `main` at `24a26b6` (merge of `p11-integrate`, editor suite 341/341). The working tree also holds
re-rendered SHOT images (`docs/evidence/app|diff/T-P02, T-P04, T-P08, T-P19, T-P23.png`, uncommitted, from another
session); all SHOT numbers below come from the committed `docs/evidence/shot-report.md`.

Status: **DONE** = the feature exists and the evidence its Verify code asks for exists. **GAP** = missing or partial.
**USER** = only the user can confirm (entry and exit gates, decisions). **HW** = needs real hardware ("n/a until
hardware" is allowed by section 2.5). C# tests are `Class.Method`; editor tests live under
`tests/AgbStudio.Editor.Tests`, others under `tests/AgbStudio.Tests` and `tests/AgbStudio.Scripting.Tests`.

Notes that apply to every SHOT row:
- `ShotTests.Shot_AllComponentsAndWindows` writes the report but asserts no diff threshold; it only fails when a built
  window or component is not rendered. A SHOT row below is DONE when its offset is within the 2 px tolerance and its
  diff is in the low range of the report (anti-aliasing level). The "matches Figma" box of section 4 still needs a
  reviewer who did not write the window (section 2.2).
- The checklist itself has no ticked box yet (the two `[x]` are format examples); none of the evidence below has been
  written into it.

## Phase 10 · Editor shell + design system

| Item | Status | Evidence or gap |
|---|---|---|
| P10.E1 | USER | Needs P1.X1 (GAP in `audit-p0-p9-gates.md`) and the user's confirmation of D-01 (Avalonia 11 / .NET 10) and D-10 (Windows only), both "decided (default)" in `docs/DECISIONS.md`. |
| P10.01 | DONE | `ShellE2ETests.TitleBar_HasSevenMenusAndNoBrandText`, `StatusBar_ShowsFixtureValuesAndRoutesClicks`; `CommandAndShortcutTests.Menu_MatchesFigmaItemsShortcutsIconsAndKinds` (T-M01..T-M07, T-M09, T-M10 vs `docs/evidence/figma/menus.json`). SHOT chrome regions of T-P01: Title Bar 2.58, Toolbar 1.85, Hierarchy header 0.94, Doc tabs 2.49, Inspector header 1.26, Bottom tabs 2.53, Status bar 3.58 %; T-M01..T-M07 2.23 / 1.40 / 2.26 / 3.11 / 2.69 / 2.63 / 3.22 %, offset 0; C-title-bar 2.58 %, offset 0. Commit `7820b78`. |
| P10.02 | DONE | `TokenTests.TokenFile_NamesAndValuesMatchFigma` (29 tokens, Dark and Light), `BothThemes_HaveIdenticalKeys`, `TokenFile_IsGeneratedFromFigmaExport`; `DesignSystemTests.Sources_NoColourLiteralsOutsideTheTheme`. |
| P10.03 | GAP | UT done (`TokenTests.TextStyles_MatchFigma`, 20 styles; `DesignSystemTests.Fonts_BundledWithOflLicences`, `Fonts_EveryTextStyleResolvesToItsBundledFace`). The SHOT specimen is only rendered (`docs/evidence/app/P10-type-specimen.png`, `DesignSystemTests.TypeSpecimen_RendersAllTwentyStyles`): there is no Figma export of the text-style specimen and no diff or report row. Export the Figma text-style frame read-only and diff it. |
| P10.04 | DONE | `DesignSystemTests.Icons_All70ExportedFromFigma`, `Icons_EveryNameUsedInTheUiExists` (`editor/AgbStudio.Editor/Assets/Icons/*.svg`). |
| P10.05 | GAP | 215 variants rendered, all 47 sets (`DesignSystemTests.Components_All47BuildableComponentsAreInTheGallery`); median diff 1.84 %, max 9.42 %. Two variants are outside the 2 px tolerance: `C-icon-button-default` (shift 3,-3, offset 3) and `C-play-control-step` (shift 3,0, offset 3). All 8 pressed button variants have a mean channel error of 19.9-30.3 against 1.8-10.8 for the other button states: check the pressed fill colours. |
| P10.06 | DONE | `ShellE2ETests.Docking_PanelsTabsAndDirtyDot` (260 / 320 / 250 px panels, Assets / Console / Animation / Database tabs, dirty dot on `Doc_Scene`). |
| P10.07 | GAP | `ShellE2ETests.Theme_DarkLightAccentScaleAndReduceMotion` and `PreferencesTests.EveryField_PersistsToPreferencesJson_AndAppliesOnSave` cover Dark / Light / System, accent and UI scale. Reduce motion, "Show pixel grid at 400 % and above" and code font size are stored only: `ThemeService.cs:36-37` sets `agb/reduce-motion` and `agb/code-font-size` but nothing reads them, and `ShowPixelGrid` / `PixelGridMinZoom` have no consumer (also `docs/evidence/editor-gaps.md` T-S06). SHOT: T-L03 0.84 %, T-S06 0.87 %, T-L01 chrome 1.05-3.80 %, but T-L01 whole frame 14.24 % (scene content). |
| P10.08 | GAP | `ShellE2ETests.Exit_WithUnsavedChanges_ShowsDialog_DontSaveCloses_SaveAllSaves`; `OpeningProjectTests.P11_06_UnsavedChangesAreAskedFirstAndBrokenProjectsFailCleanly`. The dialog always says "Save changes before exiting?" (`Shell/Dialogs.cs:124`), also when another project is being opened (editor-gaps). |
| P10.09 | DONE | `ServiceTests.Undo_RespectsHistoryLimitFromPreferences` (250 pushes keep 200; limit 10 after a Preferences change). |
| P10.10 | DONE | `ServiceTests.Autosave_SavesDirtyDocumentsEveryNMinutes` (2 min default, off at 0). |
| P10.11 | GAP | Edit menu E2E exists for actors (`HierarchyTests.EditMenu_DeleteDuplicateCopyPasteOnTheSelectedActor`), table rows (`DatabaseWindowTests.P11_39_RecordEditCommitsToItsRowWhenAnotherRowIsClicked_EditMenuOnRows`) and dialogue lines (`DialogueWindowTests.P11_47_AddLineChoiceCommandsConditionAndPaste`); routing through a fake target (`ShellE2ETests.Edit_RoutesToFocusedTextBoxOrEditTarget`). No E2E runs the Edit commands on asset cards (`AssetsPanel`) or Logic Graph nodes (`GraphCanvas`), and Cut / Select All are only exercised on the fake target. |
| P10.12 | GAP | `ShellE2ETests.CommandPalette_CtrlK_TypeArrowEnter` (Ctrl+K, "pla", arrows, Enter, ">" commands only), `CommandAndShortcutTests.Palette_FindsTheFigmaResultsForPla`; Find in Project opens the same palette (`MenuActionTests`). Opening a file result is not tested, and picking a `.scene` file does not switch to that scene (editor-gaps T-O07). |
| P10.13 | DONE | `CommandAndShortcutTests.Shortcuts_RebindToCtrlG_ReportsGoToLine` ("Ctrl+G is used by “Go to line”."), `Shortcuts_DefaultsHaveNoConflicts`, `Shortcuts_PersistInPreferencesAndResetToDefaults`, `Shortcuts_ResolveByContext`; `PreferencesTests.Shortcuts_SearchPressAKeyRecordWithConflictReplaceSaveAndReset`. |
| P10.14 | DONE | `ServiceTests.Preferences_DefaultPathIsAppDataAgbStudio`, `Preferences_DefaultsMatchFigmaAndRoundTrip`, `ProjectSettings_AreStoredPerProjectInTheAgbproj`. |
| P10.15 | GAP | The Launcher shows when a test passes start page "T-P12" (`LauncherE2ETests`), but the start-up choice (Launcher vs last project, "Open last project on start") lives in `App.axaml.cs:26-34` and no test runs it. |
| P10.16 | GAP | `ServiceTests.Toolchain_ReportsFoundAndNotFoundWithFakeFolders` covers devkitPro, libtonc, maxmod, gbafix found and devkitARM not found; `ToolchainHelpTests` covers missing tools. No fake-folder UT for the devkitARM version, the script editor, the image editor or Tiled (`ExternalEditorDetectionTests.DetectsVsCodeInLocalAppDataPrograms` covers the IDE paths only). |
| P10.17 | GAP | `SystemShellTests.ShowInExplorer_SelectsFilesOpensFoldersRefusesMissing`, `AssetsShowInExplorer_RevealsTheSelectionOrTheProject`; T-Y01 (`LauncherE2ETests.P11_01_*`, `PickFolder`), T-Y02 (`LocalizationWindowTests.P11_42_ExportCsvWritesEveryKeyAndLanguage`), T-Y03 (`ScriptToolsTests.OpenExternally_StartsTheConfiguredEditorAtTheCaretLine`), T-M17 (`LauncherE2ETests.P11_05_*`). No test clicks Preferences › Paths › Browse… (T-Y04, `PreferencesDialog.cs:313-319`). |
| P10.18 | DONE | SHOT `C-toast-success` 4.41 % offset 1, `C-toast-error` 4.41 % offset 0, `C-tooltip` 3.53 % offset 0; `ShellE2ETests.Toasts_ShowAndCloseAndTooltipsUseTheTooltipComponent`. |
| P10.19 | DONE | `ServiceTests.Recent_OpenRecentClearAndTimes`; `LauncherE2ETests.P11_01_Projects_RecentListSearchThumbnailsTimesOpenAndNewProject` (times "2 h ago" ...); SHOT T-M08 2.16 %, offset 0. |
| P10.20 | DONE | Thai typed with `KeyTextInput`: `ShellE2ETests.Thai_TypingIntoTextFieldsAndSearch` (`docs/evidence/app/P10-thai-input.png`), `LocalizationWindowTests.P11_41_TypingThaiUpdatesTableFitAndPreview_UndoAndSave`, `DialogueWindowTests.P11_46_EditTextInACardSavesTheDialogueAndTheStringTable`, `DatabaseWindowTests.P11_39_RecordPanelShowsTheRowAndEditsIt`, `FontE2ETests.AddCharacters_PasteTextAddMissingGlyphs`. |
| P10.X1 | GAP | Blocked by P10.03, P10.05, P10.07; then USER reviews the SHOT report and diffs. |

## Phase 11 · Editor windows

| Item | Status | Evidence or gap |
|---|---|---|
| P11.E1 | USER | Tick after P10.X1 and the runtime phase exits each window depends on. |
| P11.01 | DONE | `LauncherE2ETests.P11_01_Projects_RecentListSearchThumbnailsTimesOpenAndNewProject`, `P11_01_Navigation_SwitchesTabsAndSettingsOpensPreferences`. SHOT T-P12 1.47 %. Commit `8056752`. |
| P11.02 | DONE | `LauncherE2ETests.P11_02_Templates_FiltersSearchDetailsAndUseThisTemplate`. SHOT T-P20 1.92 %. |
| P11.03 | DONE | `LauncherE2ETests.P11_03_Learn_TutorialProgressDocumentationAndSamples`, `P11_03_Samples_EachIsMadeAndOpened`. SHOT T-P21 1.03 %. |
| P11.04 | DONE | `TutorialTests.P11_04_FirstPlatformerStep3_SubStepsTickThemselves_BackSkipNext`, `P11_04_FinishingTheLastStepMarksTheTutorialDone`, `P11_04_OpenedThroughTheWindowServiceItShowsTheTutorial`. SHOT T-O13 2.00 %. |
| P11.05 | DONE | `LauncherE2ETests.P11_05_ProjectContextMenu_ActionsAndDeleteAsksFirst` (Delete asks, names folder and "1 scene · 0 assets · 0 scripts", Recycle Bin). SHOT T-M17 2.18 %. |
| P11.06 | DONE | `OpeningProjectTests.P11_06_CancelDuringLoadAssets_LeavesTheOpenProjectAsItWas`, `P11_06_FullOpen_ReadsLoadsCompilesAndOpensTheLastScene`. SHOT T-D22 0.86 %. |
| P11.07 | DONE | `LauncherE2ETests.P11_07_WhatsNew_ComesFromTheReleaseNotes` (bundled notes, a `release-notes.md` next to the exe replaces them). The content shown by the packaged editor is wrong: see P16.05. |
| P11.08 | DONE | `NewProjectTests.Create_PlatformerWithSampleLevelThaiFontAndGit`, `Create_PuzzleWithLogicGraphOnly` (each include option on and off changes the project), `Create_VisualNovel_WritesTheTemplateGame`, `Rules_RomTitleGameCodeLocationAndDevkitPro`, `Fields_DeriveFromTheNameValidateAndWarnInsideDevkitPro` (save type, devkitPro warning). SHOT T-D01 2.16 %, T-L02 2.28 %. |
| P11.09 | DONE | `EmptyProjectTests.P11_09_EmptyProject_OpensWithEveryEmptyState`, `P11_09_AddPlayer_PutsThePlayerInTheSceneAndOpensTheSceneEditor`. SHOT T-P22 1.20 %. |
| P11.10 | DONE | `ViewportRomTests.Viewport_DrawsTheSceneLikeTheRom` (BGR555 frame, palette RAM and OAM vs the mGBA core; moving actors masked), `ViewportTests.Viewport_ShowsTheGbaScreenAtThreeHundredPercentWithTheStartCamera`, `Cursor_ZoomAndPan`. Commit `dc26df6`. |
| P11.11 | DONE | `ViewportTests.LayerTogglesHideBackgroundsAndSprites`, `ChipsOpenTheirWindows`, `GridButtonAndGridSnapDrawTheOverlays`. |
| P11.12 | DONE | `HierarchyTests.Tree_ShowsTheSceneAsInFigma`, `Search_FiltersRowsAndKeepsTheirFolders`, `Selection_IsSharedWithViewportAndInspector`, `AddButtonAndContextMenu`, `F2_RenamesInPlace_AndUndo`. |
| P11.13 | DONE | `InspectorTests.Header_TransformAndSpriteOfThePlayer` (layer, priority, palette swatches, Palette opens T-P10), `SheetAndAnimationDropdowns`, `BehaviourFields_SliderTextColliderAndUndo`, `ScriptHeaderOpensTheScriptEditor_AddComponentAndComponentMenu`, `GbaBudget_ShowsLiveMetersAndOpensBudgetDetails`. |
| P11.14 | DONE | `BottomPanelTests.Assets_CategoriesWithCountsAndCards` (category list filters the cards), `Assets_DoubleClickOpensTheRightEditor`, `Assets_ContextMenuAndDragIntoTheScene`. |
| P11.15 | DONE | `SceneDialogTests.SceneSwitcher_StoryOrderSearchSwitchAndNewScene`. SHOT T-O01 7.18 % (no "boss" tag, editor-gaps; see P11.X1). |
| P11.16 | DONE | `BottomPanelTests.Console_LinesFiltersCountsAndClickToCode`, `Console_TakesBuildResultsFromTheBuildService`. SHOT T-P05 1.63 %. |
| P11.17 | GAP | `BottomPanelTests.Animation_ListFramesPlayStepAndOnionSkin` shows the list, frame ticks, play / step and onion skin, but speed (ticks) and frame order cannot be changed in the panel (Figma caption; `docs/user/windows/T-P06-animation-panel.md`, editor-gaps). |
| P11.18 | DONE | `SceneDialogTests.NewScene_CreatesTheSceneFromTheOptions_AndUndo`, `SceneSettings_AppliesEverySection_RenamesWithLinks_AndUndo`. SHOT T-D02 1.77 %, T-D03 2.15 %. |
| P11.19 | DONE | `PopoverAndMenuTests.VideoMode_ModesAlphaAndMosaic`, `GridSnap_SizeSwitchesAndColour`, `AssetPicker_SearchTabsAndPick`, `ComponentPicker_CategoriesSearchKeyboardAndNewScript`, `BudgetDetails_LiveNumbersHintAndBuildReport`. |
| P11.20 | DONE | `PopoverAndMenuTests.AddActorAndAddToScene_EveryItem`, `ActorContextMenu_EveryItem`, `AssetContextMenu_EveryItem`, `ComponentMenu_EveryItem`, `ZoomAndAnimationList_EveryItem`. |
| P11.21 | DONE | `SceneDialogTests.SetAsStartScene_AndBackgroundLayers`. |
| P11.22 | DONE | `SpriteEditorE2ETests.P11_22_Canvas_TileGridZoomToolsAndFrameIndex`, `P11_22_SaveWritesThePng`. SHOT T-P08 1.70 %. Commit `ba6a163`. |
| P11.23 | DONE | `SpriteEditorE2ETests.P11_23_PalettePanel_RgbHex15BitSlotsAndSharing`. |
| P11.24 | GAP | `SpriteEditorE2ETests.P11_24_Import_DropZoneAutoReducesAndSplitsSheet` (PNG). The drop zone says "Drop PNG / Aseprite here" but only PNG is imported, and it does not open Import Asset as the Figma link T-C13 does (editor-gaps T-P08, T-D04). |
| P11.25 | DONE | `SpriteEditorE2ETests.P11_25_SpriteSettings_OamSizeOriginColliderMirrorVram`. SHOT T-M19 3.70 %. |
| P11.26 | DONE | `SpriteEditorE2ETests.P11_26_Timeline_AnimationsTicksLoopPingPongOnionSkin`. |
| P11.27 | DONE | `SpriteEditorE2ETests.P11_27_NewSprite_FromSpritesPlus`, `CreateSprite_CommandOpensEditorAndDialog`. SHOT T-D13 1.55 %. |
| P11.28 | GAP | `TilemapE2ETests.TilesetPanel_DropdownBrushStampRandom_PaintWithTheMouseSaveUndo` only checks that the dropdown requests T-O04. `TilemapEditorPage.cs:224` passes "Tileset" while the picker knows "Tilesets", so it opens on Sprites (editor-gaps T-O04). |
| P11.29 | DONE | `TilemapE2ETests.TileProperties_CollisionFlipTagAnimationAutoTile_CollisionView`; `TilemapModelTests.TileProperties_CollisionTagAnimationAutoTile_SavedAndUndoable`. Commit `274a75c`. |
| P11.30 | DONE | `TilemapE2ETests.LayersTable_RowsVisibilityLockSelection_AddLayerMenu`; `TilemapModelTests.Layers_AddTypesOnFreeBackgrounds_CollisionOnlyBecomesTheSource`, `VramPlan_*`. SHOT T-M20 4.02 %. |
| P11.31 | DONE | `TilemapE2ETests.MapPanel_SizeBgSizeColoursWrap_AndBudget`; `TilemapModelTests.MapSettings_ResizeBgSizeColoursWrap_PersistAndChangeThePlan`. |
| P11.32 | DONE | `TilemapE2ETests.AutoTileRules_TabRulesMaskAddRuleRuleSet`; `TilemapModelTests.AutoTileRules_AddEditDelete`, `AutoTile_PaintingAMemberResolvesTheGrassEdgesRules`. SHOT T-P26 6.46 %, offset 1. |
| P11.33 | DONE | `PaletteManagerE2ETests.P11_33_BanksUsedByListsAndColourEditor`; `ArtLibraryTests.Banks_LoadedWithUsedByLists`, `Colour15_LevelsHexAnd15BitValue`. SHOT T-P10 2.41 %. |
| P11.34 | DONE | UT `ArtLibraryTests.MergeNearDuplicates_KeepsTheFirstColourAndMovesPixels`, `MergeNearDuplicates_NeverMergesTheTransparentIndexOrDistantColours`, `SortByHue_OrdersLikeTheToolchainAndKeepsIndex0`, `LockIndex0_KeepsItTransparentUntilUnlocked`, `Optimize_RemovesUnusedColours`; E2E `PaletteManagerE2ETests.P11_34_ToolsMergeSortLockAndOptimize`. |
| P11.35 | DONE | `PaletteManagerE2ETests.P11_35_ImportPalGplAndExport`; `ArtLibraryTests.ExportAndImport_PalAndGpl`. (Import does not go through Import Asset as T-C29 shows; tracked under P11.64.) |
| P11.36 | DONE | `PaletteManagerE2ETests.P11_36_PaletteEffectsPopoverAndPreview`; `ArtLibraryTests.Effects_FadeCycleFlashAsTheRuntime`. SHOT T-O10 12.45 % (preview strip colours differ; see P11.X1). |
| P11.37 | DONE | `FontE2ETests.FontManager_ListSetsGlyphsDetailPreviewStats`, `FontManager_EditSettingsSetsAndPixels_SaveUndo` (sets, size, width, spacing, lower vowels, bpp, shadow, draw-to, source); `FontModelTests.DialoguePreview_TextPixelsAreTheRomRenderer`, `GlyphEditor_PaintWritesAnOverrideFontBuilderApplies_Undo` (all in `Windows/Font/FontTests.cs`). SHOT T-P09 11.53 % (see P11.X1). |
| P11.38 | DONE | `FontE2ETests.NewFont_FromFontsPlus_CreatesAndShowsTheFont`, `AddCharacters_PasteTextAddMissingGlyphs`; `FontModelTests.NewFont_DefaultsBuildAPreviewAndCreateTheFile`, `AddCharacters_RenderFromSourceOrEmptyGlyphs`. SHOT T-D15 1.98 %, T-D16 3.97 %. |
| P11.39 | DONE | `DatabaseWindowTests.P11_39_*` (7 tests: counts, filter, Add row, record panel, Used by, "Use it in a script"). SHOT T-P07 1.75 %. Commit `83585fb`. |
| P11.40 | DONE | `DatabaseWindowTests.P11_40_NewTableFromEmptyCsvAndCopy_UndoRemovesTheFile`, `P11_40_AddFieldRarityEnumIsOneBytePerRow`, `P11_40_ImportCsvWarnsOnRows7And15ThenImports`. SHOT T-D17 1.80, T-D18 1.32, T-D19 2.30 %. |
| P11.41 | DONE | `LocalizationWindowTests.P11_41_LanguagesWithProgressGroupsAndStatus`, `P11_41_SearchGroupsAndMissingOnly`, `P11_41_StringPanelAndGbaPreviewDrawLikeTheRom` (speaker, notes), `P11_41_TypingThaiUpdatesTableFitAndPreview_UndoAndSave`, `P11_41_FitCheckSaysHowManyLinesTheBoxNeeds`. SHOT T-P14 3.74 %. |
| P11.42 | DONE | `LocalizationWindowTests.P11_42_AddLanguageJapaneseCreatesAKanaFont_FrenchUsesTheCurrentFont`, `P11_42_ExportCsvWritesEveryKeyAndLanguage`. SHOT T-D20 1.82 %. |
| P11.43 | DONE | `AudioPageTests.ToolsAudio_ListsTheProjectsMusicAndSoundsWithLengths`, `TrackInfo_ComesFromTheModuleAndMeta`, `Tracker_ShowsThePatternAndFollowsSeek`, `Play_RendersTheModuleToPcmAndFollowsThePlayhead`, `Inspector_EditsTheMetaUndoAndSave`, `TrackerTools_SelectPencilEraser_WriteTheModule`; `TrackerModuleTests` (MOD / XM parsing, playback) in `Windows/Audio/AudioTests.cs`. SHOT T-P15 5.28 %. Commit `470c1a7`. |
| P11.44 | DONE | `AudioPageTests.ChannelsMixerBudget_AndPreviewSfx`. |
| P11.45 | DONE | E2E `SfxDesignerTests.OpensTheEffectWithItsFieldsRegistersAndScriptLine`, `PresetsChannelDutyAndSliders_ChangeTheEffect`, `BakeToWav_SavesTheSampleAndHardwareKeepsZeroBytes`, `WaveChannel_PencilDrawsWaveRam_AndPlusCreatesAnEffect`; EMU `DmgSoundTests.Preview_SoundsLikeTheEmulator` (`Windows/Audio/SfxTests.cs`) (Coin, Jump, Select, Hurt played by the mGBA core, pitch and length compared); `PipelineTests.SfxDesignerEffectsPlayOnTheDmgChannels` (commit `8946a14`). SHOT T-P25 2.95 %. |
| P11.46 | DONE | `DialogueWindowTests.P11_46_ConversationsLinesChoicesSpeakersAndLanguageToggle`, `P11_46_LineInspectorAndGbaPreview`, `P11_46_EditTextInACardSavesTheDialogueAndTheStringTable`, `P11_46_NewConversationFromCreateMenuAndUndo`. SHOT T-P16 2.09 %. |
| P11.47 | DONE | `DialogueWindowTests.P11_47_NewSpeakerWithThaiNamePortraitColourAndBlip`, `P11_47_AddToDialogueAndCommandMenusMatchFigma`, `P11_47_AddLineChoiceCommandsConditionAndPaste`. SHOT T-D21 2.76, T-M23 1.16, T-M24 2.44 %. |
| P11.48 | DONE | `LogicGraphTests.ToolsLogicGraph_ShowsNodesWiresPaletteStatusAndGeneratedCode`, `ZoomButtonsWheelAndFit`, `SelectDragWireInspectorAndUndo`, `GeneratedCodeEditsUpdateTheGraph_AndSaveWritesGraphAndCs`, `Inspector_TagModeTarget_AndVectorPins`. SHOT T-P17 4.01 %. Commits `eda1a26`, `d125b57`. (Link tool and Preview: see P11.X1.) |
| P11.49 | DONE | `LogicGraphTests.RightClickCanvas_AddNodeSearchEnterAndShiftEnter`, `RightClickNode_ContextMenuDuplicateDisableGroupCollapseShowCodeDelete`. SHOT T-O08 5.85 %, T-M21 2.68 %. (Nodes › Add node… is a search box, not this popover: see P11.X1.) |
| P11.50 | DONE | `WorldMapTests.CtrlM_ShowsScenesLinksStartSceneAndInspector`, `DragCardEditInspectorStartSceneZoomAndUndo` (New Scene opens T-D02). SHOT T-P13 1.64 %. |
| P11.51 | DONE | `WorldMapTests.LinkChipOpensLinkSettings_ChangesAreSaved`, `SceneContextMenu_OpenRenameDuplicateSetStartDelete`. SHOT T-O09 2.73, T-M22 1.94 %. |
| P11.52 | GAP | `ScriptEditorTests.LineNumbersHighlightingAndBreadcrumb`, `TypingAutoIndentTabSizeAndUndo`, `AutocompleteListsApiMembersAndInsertsTheChoice`, `AnalyzerUnderlineAndDiagnosticPopupQuickFix`, `FormatOnSaveWritesTheFileAndRunsTheScriptCheck`. The T-S07 built-in editor options code font, code font size and code theme are not applied (`CodeFont` / `CodeTheme` only in `ExternalEditors.cs:40-42`, `agb/code-font-size` unread; editor-gaps T-S07). |
| P11.53 | DONE | `ScriptEditorTests.SidePanelAttachedToCompilesToExposedFieldsOutlineAndApi`. |
| P11.54 | DONE | `ScriptEditorTests.BottomPanelProblemsTerminalOutputAndStatusLine`. SHOT T-P04 2.73 %. |
| P11.55 | DONE | `ScriptToolsTests.CtrlF_OpensFindWithTheWordAtTheCaret_NextAndReplace`, `FindOptions_CaseWholeWordRegexAndScopes`, `NewScript_CreatesFromTemplateAttachesAndOpens`, `OpenExternally_StartsTheConfiguredEditorAtTheCaretLine`. SHOT T-O11 5.47, T-D14 3.01 %. |
| P11.56 | DONE | `ScriptingApiTests.HelpScriptingApi_F1_ShowsTheTreeAndTheInputPage`, `SearchSeeAlsoKeyEnumAndCopyExample`, `WorksOffline_NoNetworkNeeded` (HTTP(S)_PROXY to a dead port; "Docs v1.0", "Offline"). SHOT T-P18 1.34 %. |
| P11.57 | DONE | `BuildRomTests.BuildRom_RealBuild_StreamsStepsAndUpdatesStatusAndReport`, `BuildRom_ScriptError_OpensBuildFailedAndRecordsHistory`, `Stream_FromTheBuildThread_UpdatesStepsProgressAndLog`, `Failure_ShowsTheFailedStepItsPredecessorSkippedStepsAndErrors`, `GoToFirstError_OpensTheScriptAtItsLine` (`Windows/Build/BuildTests.cs`). SHOT T-D06 2.32, T-D23 1.32 %. |
| P11.58 | DONE | `BuildReportTests.Report_ShowsSizeTimesWarningsVramBreakdownAssetsAndHistory`, `HeaderFields_EditProjectSettings_AndExportActionsOpenTheirWindows` (Open build folder, Run in mGBA, Build again, Flash), `MenuCommands_OpenTheBuildWindows`; `BuildRomTests.RunInEmulator_AfterSuccess_OpensPlayModeWithTheRom_AndExportCopiesPadded`. SHOT T-P11 1.47 %. |
| P11.59 | DONE | `ExportRomTests.Fields_ComeFromTheProject_ValidateAndExportARelease`. SHOT T-D05 2.45 %. |
| P11.60 | HW | n/a until hardware (`docs/TOOLCHAIN.md` Hardware). Fakes cover the flow: `FlashTests.Detects_BacksUp_Writes_Verifies_WithProgress`, `VerifyFailure_NoCartridge_AndGbxCartWriteAreReported`, `SdCardFlashCart_CopiesVerifiesAndFindsTheSave`; GBxCart RW writing is refused until verified (D-12). Code gap: the cartridge line shows the game code, not the flash and save sizes (editor-gaps T-D07). |
| P11.61 | GAP | `ProjectSettingsTests.EveryField_PersistsToTheAgbproj`, `Build_ReadsTheSettings_RomHeaderSaveTypeStartSceneSplashRemap` (title, code, maker, version, start scene, splash, remap, save chip), `TreatWarningsAsErrors_FailsTheScriptStepOnAWarning`. Missing: "Show inline hints" has no effect (T-S04); Save data shows every `int` as 4 B where Figma shows 1 B / 2 B fields (T-S02); no E2E that the Build tab fields (configuration, optimisation, output folder, ROM size warning) change the build. |
| P11.62 | GAP | `PreferencesTests.EveryField_PersistsToPreferencesJson_AndAppliesOnSave` proves every field persists, but many do nothing (editor-gaps): Emulator tab ignored by Play Mode (T-D09); Reduce motion, pixel grid (T-S06); default snap, collision overlay, GBA budget in Inspector, code font and theme (T-S07); projects folder, image editor, Tiled (T-S08). |
| P11.63 | GAP | `AboutTests.KeyboardShortcuts_GroupsAndSearch`, `HelpAbout_ShowsVersionDateCreditsLicensesAndWebsite`, `Website_IsDisabledWhenNoUrlIsConfigured`. Keyboard Shortcuts lists ten shortcuts, not all (Figma caption), and has no link to Preferences › Shortcuts (editor-gaps T-D10). |
| P11.64 | GAP | `AssetImporterTests.Detects_EachTypeWithItsOptionsAndProblems`, `Import_ConvertsIntoTheProjectFolders`, `Fixes_ReduceOrSplitIntoLayeredSprites`; `ImportDialogTests.FileImportAsset_ListsFilesShowsOptionsAndImports`, `Problems_OpenTheErrorsDialog_FixesAndSkipsErrors` (`Windows/Import/ImportTests.cs`). SHOT T-D04 3.33 %, T-D24 4.46 %. `.aseprite` is not detected (Figma caption lists Aseprite); Sprite Editor drop zone (T-C13) and Palette Manager › Import (T-C29) do not open Import Asset. |
| P11.65 | GAP | `MenuActionTests.EveryTitleBarMenuCommand_ActsOrOpensItsWindow`, `ProjectCommands_AreDisabledWithoutAProject_AndEnabledWhenOneOpens`, plus the per-window menu tests above. Create › Tileset / Tilemap opens the current map instead of making a new one and Create › Font opens Font Manager instead of New Font (editor-gaps); the test table only checks the window id, so it passes. |
| P11.66 | DONE | `MenuActionTests.ScreenshotGif_F12SavesThePngThenRecordsAndSavesTheGif`, `ScreenshotGif_StopsByItselfAfter30Seconds`, `ScreenshotGif_CapturesTheEmulatorCore`; `PlayModeTests.GifEncodesFrames`. |
| P11.67 | DONE | `MenuActionTests.ReportABug_OpensTheConfiguredPage`, `CheckForUpdates_NoSourceNewerSameAndOffline`; `AboutTests.EditorJson_IsReadWithItsD13Keys`. |
| P11.X1 | GAP | No section 4 box is ticked. Open items besides the GAP rows above: SHOT whole-frame diffs T-P03 14.48, T-L01 14.24, T-P01 14.06, T-P02 12.77, T-O10 12.45, T-P09 11.53 % (scene content, art and preview colours differ from Figma); editor-gaps not covered by an item: Scene Editor Paint / Fill / Erase only highlight the tool (T-P01), no "boss" tag (T-O01), Nodes › Add node… is not the T-O08 popover, Logic Graph Link tool does nothing and Preview is a still picture, title bar Pause / Step do nothing on 15 pages, Script Editor "Analyzer: GBA rules on" chip opens Project Settings on Input instead of Scripting (`ScriptEditorPage.cs:80`); no test follows the prototype flows T-X01..T-X03; the "matches Figma" boxes need a non-author reviewer. Then USER. |

## Phase 12 · Play / Simulate mode

| Item | Status | Evidence or gap |
|---|---|---|
| P12.E1 | USER | Needs P7.X1 and P10.X1 (both GAP); D-06 decided by the user, D-16 by default. |
| P12.01 | GAP | `PlayModeE2ETests.P12_01_GameTab_Core3xPixelPerfect_Audio_PauseWhenUnfocused_FastForwardTab` (3x nearest neighbour, audio samples, pause when unfocused, Tab 4x, `PlaySession.Fps` 59.7275). The test sets `Play.Options` directly: Preferences · Emulator (scale, filter, audio, pause when unfocused, fast-forward key and speed) never reaches Play Mode (`EmulatorIO.cs:19-20` defaults; editor-gaps T-D09). |
| P12.02 | DONE | `PlayModeE2ETests.P12_02_Play_IncrementalBuildAndRun_PlayFromHere_BuildAndRun_RunInMgba`. Commit `7613e92`. |
| P12.03 | DONE | `PlayModeE2ETests.P12_03_PlayControls_PauseStepStop_StopReturnsToSceneEditor`. |
| P12.04 | DONE | `PlayModeE2ETests.P12_04_PlaySpeed_QuarterToFourTimes_AndFrameStep`; `PlayModeTests.SessionPacesFramesBySpeedAndSteps`. SHOT T-M18 0.78 %. |
| P12.05 | GAP | E2E pixel checks exist (`PlayModeE2ETests.P12_05_Overlays_HitboxesAndTileCollision_DrawnOverTheGame`), but the Verify is SHOT and T-P02 differs by 12.77 %: the capture shows another moment of the game (Figma: player jumping with its hitbox and velocity arrow; app: player idle), so the overlays are never compared with Figma. |
| P12.06 | DONE | `PlayModeE2ETests.P12_06_LiveInspector_StatePositionVelocityOnGroundScriptFields_ChangeWhileRunning`; `PlayModeTests.LiveInspectorProfilerOamAndPalettes`. |
| P12.07 | GAP | `PlayModeE2ETests.P12_07_Console_EngineLogsAndDebugLog_WithTimestamps` shows real engine lines, but the Debug.Log line is injected with `Play.Console.AddGameLine`, not logged by a running script; and game lines reach only Play Mode's console, not the Console panel T-P05 (editor-gaps T-P05). |
| P12.08 | DONE | `PlayModeE2ETests.P12_08_StatusBar_FrameFpsVBlankCpu_RunningPaused_HotReload`. |
| P12.09 | DONE | `PlayModeE2ETests.P12_09_KeyboardAndGamepad_FromProjectSettingsInput` (key rebinding and XInput pad); `PlayModeTests.KeyboardMapFollowsProjectSettings`; `PlayUnitTests.InputMap_KeyboardAndGamepadColumns`. |
| P12.10 | DONE | `PlayModeE2ETests.P12_10_SaveFile_KeptPerProject_SaveStopPlayLoad` (save under `build/play`). |
| P12.11 | DONE | `PlayModeE2ETests.P12_11_HotReload_SaveAScript_GameContinuesInTheSameSceneWithTheNewCode`; `PlayModeTests.HotReloadContinuesInTheSameSceneWithTheSameState`. |
| P12.X1 | GAP | Blocked by P12.01, P12.05, P12.07; then USER watches the fixture play in the editor with live Inspector and Console. |

## Phase 13 · Dev tools

| Item | Status | Evidence or gap |
|---|---|---|
| P13.E1 | USER | Needs P12.X1; D-21 decided by default. |
| P13.01 | DONE | RT `RuntimeRomTests.RomPasses(core_basics)` `frame_loop_counts_and_profiles` (`runtime/tests/core_basics/main.cpp:171`); E2E `DebuggerE2ETests.P13_01_Profiler_120FramesAgainstBudget_ThisFrameLogicRenderAudioIdle`. SHOT T-P19 5.25 %. |
| P13.02 | DONE | `DebuggerE2ETests.P13_02_VramTiles_ByCharBlock_MatchEmulatorMemory` (every BG tile pixel vs VRAM read from the core). |
| P13.03 | DONE | `DebuggerE2ETests.P13_03_OamTable_ActorXYSizeTilePalPrioFlags_InUse`. |
| P13.04 | GAP | `DebuggerE2ETests.P13_04_Palettes_LiveRam_ChangedOutline_AddressBgr555Rgb_UsedBy_OpenAndBreak` covers live RAM, outline, address, BGR555, RGB, Open in Palette Manager, Break on change. "Used by" does not name the palette effect ("Palette effect · water cycle (6-9)" in Figma; editor-gaps T-P24). SHOT T-P24 2.53 %. |
| P13.05 | DONE | `DebuggerE2ETests.P13_05_Memory_MapByCategory_WatchList_HexView_AddWatch`. SHOT T-P23 3.19 %. |
| P13.06 | GAP | `DebuggerE2ETests.P13_06_Breakpoints_StopAtPlayerController19_PausedAtFrame_CallStackInCSharpNames` and `CSharpDebuggerTests.BreakpointPausesWithCallStackAndResumes` prove the runtime side, but breakpoints are added through `DebuggerModel.AddBreakpoint` in the test only: no window calls it (`DebuggerModel.cs:107`), so a user cannot set a breakpoint and the list stays empty (editor-gaps T-P19). |
| P13.07 | DONE | UT `BudgetAndHistoryTests.P13_07_PaletteSharingSuggestion_ForTheFixture` (hero / slime share OBJ 0, block / flag share OBJ 2), `P13_07_LiveBudget_FromTheRunningGame` (`Windows/Debugger/DebuggerE2ETests.cs`); `PopoverAndMenuTests.BudgetDetails_LiveNumbersHintAndBuildReport`. SHOT T-O06 4.66 %. |
| P13.08 | DONE | `BudgetAndHistoryTests.P13_08_BuildReportHistory_AcrossBuilds_IncludingFailures`. |
| P13.09 | DONE | `DebuggerE2ETests.P13_09_DebugButtonAndToolsDebugger_OpenTheDebugger_ViewsMemoryAndPalettes`. |
| P13.X1 | GAP | Blocked by P13.04, P13.06; then USER checks the views against the running fixture. |

## Phase 14 · External IDE support

| Item | Status | Evidence or gap |
|---|---|---|
| P14.E1 | USER | Needs the Phase 6 tier 1 exit and P10.X1. |
| P14.01 | DONE | `ExternalEditorDetectionTests.DetectsVsCodeInLocalAppDataPrograms`, `DetectsRiderVisualStudioFromAppPathsVsWhereAndProgramFiles`, `SettingsPersistWithFigmaDefaults` (in `ScriptToolsTests.cs`); found status in `PreferencesTests.EveryField_PersistsToPreferencesJson_AndAppliesOnSave`. |
| P14.02 | DONE | `IdeTests.GeneratedProjectBuildsWithTheSameGaeDiagnostics` (`dotnet build` of the generated `.csproj` gives "GAE001 13,17 Error", "GAE002 8,5 Warning" like the built-in check; `.vscode/settings.json`, `.editorconfig`; unchanged files not rewritten). Regenerated on each build (`tools/AgbStudio.Scripting/ScriptBackend.cs:43`) and before an external open (`ExternalEditors.cs:248-251`). |
| P14.03 | GAP | `ScriptToolsTests.OpenExternally_StartsTheConfiguredEditorAtTheCaretLine` checks the command lines for VS Code, Rider, Visual Studio and Custom with fake executables; `ExternalEditorDetectionTests.ThisMachine_RecordsWhichIdesAreInstalled` only logs to test output. No recorded run in each installed IDE and no written "n/a" for the others. |
| P14.04 | GAP | `ScriptToolsTests.HotReload_ExternalSaveReloadsTheEditorAndChecksTheScripts` runs without a game; the branch that reloads the running game (`ScriptWorkspace.cs:412-416`, "updated in the running game") is never exercised from an external save. |
| P14.05 | DONE | `ScriptToolsTests.NewScript_OpenInExternalEditorAfterCreating` (opens at line 5, generates the `.csproj` first). |
| P14.06 | DONE | `IdeTests.AutocompleteListsTheApiMembers`, `ReferenceAssemblyShipsWithXmlDocs`, `ApiDocsCoverEveryPublicMember`; `AgbStudio.Api.xml` is in the package `bin/` (`build/package/AGB-Studio-1.0.0-win-x64/bin`). |
| P14.07 | GAP | Rules and fixes work in-process (`CodeFixTests.ChangeToFixedRewritesTypeAndLiteral`, `PopupOffersBothFixes`) and on the command line (`IdeTests.GeneratedProjectBuildsWithTheSameGaeDiagnostics`); no E2E or record inside VS Code, Rider or Visual Studio. |
| P14.X1 | GAP | Blocked by P14.03, P14.04; then USER edits a script in VS Code while the game runs. |

## Phase 15 · Genre kits, templates, tutorials, samples

Each template is generated in code (`tools/AgbStudio.Build/Content/Templates/*.cs`), built with its C# scripts and
played by a bot in the embedded mGBA core in `TemplateTests`, which fails on any gameplay frame overrun (the 60 fps
check). Common gaps for P15.01-P15.08: EMU asks for mGBA **and** VBA-M with a picture or clip, but the only VBA-M
evidence in the repo is `docs/evidence/app/P0.07-vbam-hello.png` and the template frames the tests write
(`story.png`, `stage.png`, `race.png`, `brawl.png`) go to the git-ignored `build/` folder; HW is n/a until hardware.

| Item | Status | Evidence or gap |
|---|---|---|
| P15.E1 | USER | Needs the Phase 4-9 and P11.X1 exits; D-09 and D-20 decided by default. |
| P15.01 | GAP | RT + 60 fps `TemplateTests.PlatformerPlaysThroughToTheGoal` (stats "Mode 0 · 3 BG layers · 9 actors · 4 scripts", slope, coin score, checkpoint, goal back to title); E2E `NewProjectTests.Create_PlatformerWithSampleLevelThaiFontAndGit`. Missing: VBA-M run with a committed capture; stomp, coyote time, losing a life and the pause menu are not asserted. Commit `ab8df3a`. |
| P15.02 | GAP | RT + 60 fps `TemplateTests.TopDownAdventureTalksPushesUnlocksAndEnters`. Missing: VBA-M capture; E2E of New Project creating this template (only `ProjectCreator.BuildTemplate` not null, in `NewProjectTests.Create_VisualNovel_WritesTheTemplateGame`). Commit `385947c`. |
| P15.03 | GAP | RT + 60 fps `TemplateTests.RpgShopsAndWinsABattle` (Heroes / Enemies / Items tables, shop, battle). Missing: VBA-M capture; New Project E2E. Commit `0c2a8a1`. |
| P15.04 | GAP | RT + 60 fps `TemplateTests.ShootEmUpScrollsFiresAndClearsTheStage`. Missing: VBA-M capture; New Project E2E; the power-up is not asserted. Commit `0c2a8a1`. |
| P15.05 | GAP | RT + 60 fps `TemplateTests.PuzzleDropsPairsClearsLinesAndEnds`; E2E `NewProjectTests.Create_PuzzleWithLogicGraphOnly`. Missing: VBA-M capture. Commit `3e8c9fd`. |
| P15.06 | GAP | RT + 60 fps `TemplateTests.VisualNovelTellsTheStoryAndRemembersTheChoice`; E2E `NewProjectTests.Create_VisualNovel_WritesTheTemplateGame`. Missing: VBA-M capture. Commit `6c37ea6`. |
| P15.07 | GAP | RT + 60 fps `TemplateTests.BeatEmUpFightsThroughTheWavesWithCombos`. Missing: VBA-M capture; New Project E2E. Commit `7f8ad70`. |
| P15.08 | GAP | RT + 60 fps `TemplateTests.RacingDrivesTheMode7CircuitAndCountsLaps`. Missing: VBA-M capture; New Project E2E. Commit `7f8ad70`. |
| P15.09 | DONE | REV in this audit: art drawn in code (`Content/PixelArt.cs`, `SplashArt.cs`, `Templates/*`), music written as XM (`Audio/XmWriter.cs`, `Audio/Chiptune.cs`), text written fresh; `templates/` and `samples/` hold no files; besides the OFL fonts in `third_party/fonts/` (listed in THIRD_PARTY.md) the only tracked game-side binaries are the runtime test assets `runtime/tests/audio_basic/audio/beep.wav`, `tune.mod`; `LintTests.PokeemeraldNamesAndWrongProductNamesAreReported`, `RepositoryIsClean`. Matches G-LEGAL.06 in `audit-p0-p9-gates.md`. |
| P15.10 | GAP | Five tutorials in `editor/AgbStudio.Editor/Windows/Launcher/TutorialCatalog.cs` ("Your first platformer" has 8 steps; the others 2-3). E2E ticks only step 3 of "Your first platformer" and finishes "Palettes & VRAM" (`TutorialTests`). No E2E walks every step of every tutorial, and there is no DOC page for the tutorials beyond the T-O13 / T-P21 window pages. |
| P15.11 | DONE | RT `TemplateTests.SampleProjectsBuildAndBootToTheTitle` (Sky Hopper demo, Cave Quest, Snow Run, Dusk Story); `LauncherE2ETests.P11_03_Samples_EachIsMadeAndOpened`. Commit `7d9ad86`. |
| P15.12 | GAP | The Learn hub opens Scripting API, GBA hardware guide (`docs/user/gba-hardware-guide.md`), Keyboard shortcuts, Release notes and Report a bug (`LauncherE2ETests.P11_03_Learn_TutorialProgressDocumentationAndSamples`). Release notes opens the bundled "0.1" notes, not 1.0.0 (P16.05), and "Keyboard shortcuts · all shortcuts in one list" opens a dialog that lists ten (P11.63). |
| P15.X1 | GAP | Blocked by P15.01-P15.08 EMU evidence; then USER plays each template to its end in mGBA and VBA-M (hardware n/a). |

## Phase 16 · Packaging and release

| Item | Status | Evidence or gap |
|---|---|---|
| P16.E1 | USER | Needs every earlier exit and every section 6 gate (many GAP in `audit-p0-p9-gates.md`). |
| P16.01 | GAP | `tools/AgbStudio.Build/Packager.cs` (portable zip, README with uninstall-by-delete), `agb package` (`tools/agb/Program.cs:143`), `PackagingTests.PackageHoldsTheRuntimeFontsCoreAndDocsAndFindsItsRoot` (`publish: false`), first run without devkitPro `ToolchainHelpTests.FirstRun_WithoutDevkitPro_NamesTheFoldersSearched_InstallerPage_AndPaths`; a 60 MB `build/package/AGB-Studio-1.0.0-win-x64.zip` exists (untracked). No record of the Verify: install on a clean Windows user account, first start, uninstall leaving projects. |
| P16.02 | GAP | `AboutTests.HelpAbout_ShowsVersionDateCreditsLicensesAndWebsite` (version 1.0.0, date, credits, Licenses opens `THIRD_PARTY.md`, Website). REV against the package: `bin/` ships HarfBuzzSharp + `libHarfBuzzSharp.dll` (HarfBuzz), Humanizer, MicroCom.Runtime, ANGLE (`av_libglesv2.dll`), Skia (`libSkiaSharp.dll`, BSD-3), the .NET runtime (`coreclr.dll` etc.), Microsoft.DiaSymReader.Native and msquic, none of which is in `docs/THIRD_PARTY.md`. |
| P16.03 | DONE | `MenuActionTests.ReportABug_OpensTheConfiguredPage`, `CheckForUpdates_NoSourceNewerSameAndOffline`; `AboutTests.EditorJson_IsReadWithItsD13Keys`; the package writes an empty `bin/editor.json` (D-13). |
| P16.04 | GAP | SHOT `C-brand-mark` 0.00 %, `C-brand-app-icon-light / dark / indigo` 0.24 / 0.00 / 0.00 %, Launcher brand block in T-P12 1.47 %, About T-D11 2.73 %. Missing: no exe / taskbar icon (no `ApplicationIcon` in `AgbStudio.Editor.csproj`, no `Window.Icon`); the four banners (T-R03) do not exist in the repo. |
| P16.05 | GAP | `docs/RELEASE-NOTES.md` has 1.0.0, but the Launcher reads `release-notes.md` next to `AGBStudio.exe` (`ReleaseNotes.cs:46`) and `Packager.cs:41-42` copies the notes to `docs/RELEASE-NOTES.md` only, so the packaged editor shows the bundled "0.1" notes (`ReleaseNotes.cs:16-41`); RELEASE-NOTES.md also has no "### Highlights" section for What's new. |
| P16.06 | GAP | No CI report of a release candidate, no section 4 box ticked, many gates open; sample ROMs on hardware are n/a until hardware (HW part). |
| P16.07 | USER | D-19 (decided by default): builds are unsigned and the release notes say so (`RELEASE-NOTES.md` Known limits); the update channel is Check for Updates per D-13 but the package's `updateFeed` is empty. Show the user and get acceptance of unsigned builds and of a feed URL (or none), since the "signature check" cannot pass. |
| P16.X1 | GAP | Blocked by P16.01, P16.02, P16.04, P16.05, P16.06; then USER approves the release. |

## Counts

| Group | DONE | GAP | USER | HW | Total |
|---|---|---|---|---|---|
| Phase 10 | 11 | 10 | 1 | 0 | 22 |
| Phase 11 | 57 | 10 | 1 | 1 | 69 |
| Phase 12 | 8 | 4 | 1 | 0 | 13 |
| Phase 13 | 7 | 3 | 1 | 0 | 11 |
| Phase 14 | 4 | 4 | 1 | 0 | 9 |
| Phase 15 | 2 | 11 | 1 | 0 | 14 |
| Phase 16 | 1 | 6 | 2 | 0 | 9 |
| **All** | **90** | **48** | **8** | **1** | **147** |

## Gaps to fix

Grouped by area so they can be handed out; small first inside each group. Exit gates (P10.X1-P16.X1) close when
their listed items close and the user confirms.

### Editor windows

Small (a line of code or one more assertion):
1. P11.28: pass "Tilesets" from `TilemapEditorPage.cs:224`; assert the picker tab and pick a tileset in `TilemapE2ETests`.
2. P11.X1: Script Editor "Analyzer: GBA rules on" chip opens Project Settings on Scripting (T-S04), not Input (`ScriptEditorPage.cs:80`).
3. P10.08: Unsaved Changes says "opening another project" when that is the reason (`Shell/Dialogs.cs:124`); assert it in `OpeningProjectTests`.
4. P13.04: name the palette effect in Debugger · Palettes "Used by"; assert it in `P13_04`.
5. P10.17: E2E clicking Preferences › Paths › Browse… (T-Y04) through a fake `SystemShell` picker.
6. P11.63 (and P15.12): Keyboard Shortcuts lists every shortcut by group and links to Preferences › Shortcuts.
7. P11.X1: "boss" tag on Scene Switcher rows (T-O01); T-D07 cartridge line shows flash and save sizes.
8. P11.65: Create › Tileset / Tilemap makes a new one; Create › Font opens New Font (T-D15); make `MenuActionTests` assert the result, not only the window id.

Medium (a contained feature with its E2E):
9. P10.15: move the start-up choice out of `App.axaml.cs` and E2E it (last project on / off / missing).
10. P10.12: opening a file result in the Command Palette / Find in Project; a `.scene` result switches the scene.
11. P10.16: fake-folder UT for the devkitARM version (gcc stub), script editor, image editor and Tiled.
12. P10.11: E2E of Cut / Copy / Paste / Duplicate / Delete / Select All on asset cards and Logic Graph nodes, and Cut / Select All on actors, rows and lines.
13. P11.52: apply code font, code font size and code theme in the Script Editor.
14. P10.07, P11.62: make Reduce motion, pixel grid at 400 %+, default snap, collision overlay, budget in Inspector, projects folder, image editor and Tiled take effect.
15. P12.01, P11.62: feed Preferences · Emulator (scale, filter, audio, pause when unfocused, fast-forward key and speed) into Play Mode; E2E from the Preferences dialog.
16. P12.07: game log lines (Debug.Log) also go to the Console panel (T-P05); E2E with a real `Debug.Log` from a script.
17. P11.17: Animation panel edits ticks per frame and frame order with undo.
18. P11.61: "Show inline hints" takes effect; Save data field sizes 1 / 2 / 4 B as in Figma (T-S02); E2E that Build tab fields change the build.
19. P11.X1: title bar Pause / Step act on the 15 pages where they do nothing; Logic Graph Link tool and a live Preview; Nodes › Add node… opens the T-O08 popover.
20. P14.04: E2E of an external file save while Play runs ("updated in the running game").

Large:
21. P11.24, P11.64: Aseprite import (detection, frames, palette); Sprite Editor drop zone (T-C13) and Palette Manager › Import (T-C29) go through Import Asset.
22. P13.06: a way to add breakpoints (Script Editor gutter and / or the Debugger list) and an E2E that clicks it.
23. P11.X1: Scene Editor Paint tiles / Fill / Erase paint in the scene view (T-P01).
24. P10.05, P10.03, P12.05, P11.X1 (SHOT): fix `C-icon-button-default` and `C-play-control-step` (offset 3) and check pressed button colours; export the Figma type specimen and diff it; render T-P02 in Figma's state (hitbox on a jumping player) or region-compare the overlays; bring T-P01, T-L01, T-P03, T-P02, T-O10, T-P09 below the anti-aliasing level or record the accepted differences in the Scope log; non-author reviewer ticks for every "matches Figma" box.
25. P11.X1: E2E that follows the prototype flows T-X01, T-X02 and the 120 links of T-X03.

### Runtime and templates

1. P15.01, P15.04: assert stomp, coyote time, losing a life and the pause menu (Platformer) and the power-up (Shoot 'em up) in `TemplateTests`.
2. P15.02, P15.03, P15.04, P15.07, P15.08: New Project E2E creating each of these templates (as `NewProjectTests` does for Platformer, Puzzle, Visual Novel).
3. P15.01-P15.08, P15.X1: run every template ROM in VBA-M and commit an mGBA and a VBA-M capture per template under `docs/evidence/app/`.
4. P15.10: E2E that walks every step of all five tutorials to "Done".

### Build and packaging

1. P16.05, P15.12, P11.07: write `bin/release-notes.md` from `docs/RELEASE-NOTES.md` in `Packager.cs` (or read the package's `docs/` copy), add a "### Highlights" list to 1.0.0, replace the bundled 0.1 text, and assert in `PackagingTests` that What's new shows 1.0.0.
2. P16.02: a test that every DLL in the package `bin/` maps to a `THIRD_PARTY.md` row.
3. P16.04: exe and window icon from the brand app icon (`ApplicationIcon` .ico, `Window.Icon`); SHOT of the exe icon against Plus Pad.
4. P16.01: run `agb package` with publish, install on a clean Windows user account, record first run (devkitPro found / guided) and that deleting the folder keeps projects.
5. P16.06: a CI run of the release candidate with its report kept.

### Docs and records

1. P16.02: add HarfBuzzSharp / HarfBuzz, Humanizer, MicroCom.Runtime, ANGLE, Skia, the .NET runtime, Microsoft.DiaSymReader.Native and msquic to `docs/THIRD_PARTY.md` with their licences.
2. P16.07: get the user's acceptance of unsigned builds (D-19) and of the update feed URL (or none).
3. P14.03, P14.07: record per IDE (VS Code, Rider, Visual Studio) that Open externally lands on the right line and that the analyzer and "Change to fixed" work, with "n/a" for IDEs not installed.
4. P15.10: a user doc page for the five tutorials, linked from Launcher · Learn.
5. P16.04: design the four banners (T-R03) in Figma and record the REV.
6. All phases: write the DONE evidence above into `docs/ENGINE-CHECKLIST.md` in the section 2.3 format.
