using AgbStudio.Build.Fonts;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Tests.Support;
using AgbStudio.Editor.Tests.Windows.Tilemap;
using AgbStudio.Editor.Windows.Font;
using AgbStudio.Editor.Windows.Tilemap;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.Font;

/// <summary>Unit tests of the Font Manager model (11.G): glyph groups, missing characters, glyph edits, previews, New Font.</summary>
public class FontModelTests : IDisposable
{
    readonly Fixture _fx = new();
    readonly FontLibrary _lib;
    readonly FontEntry _thai;

    public FontModelTests()
    {
        SkyHopperContent.OpenForFonts(_fx);
        _lib = FontLibrary.Of(_fx.Services);
        _thai = _lib.Resolve(null)!;
    }

    public void Dispose() => _fx.Dispose();

    [AvaloniaFact]
    public void ProjectFonts_DefaultFirst_BuiltByFontBuilder()
    {
        Assert.Equal(["pixel8", "thai_vwf", "title_big"], _lib.Fonts.Select(f => f.Name));
        Assert.Equal("thai_vwf", _thai.Name);
        var built = FontBuilder.Build(_thai.File, _thai.Project.FontsDir);
        Assert.Equal(built.Glyphs.Count, _thai.Font.Glyphs.Count);
        Assert.Equal(built.RomBytes, _thai.Font.RomBytes);
        Assert.Equal(95 + 87 + 12, _thai.GlyphCount);
        Assert.Equal("VWF · 12px", FontManagerPage.Meta(_thai.File));
        Assert.Equal("8×8 fixed", FontManagerPage.Meta(_lib.Fonts[0].File));
        Assert.Equal("VWF · 12 px · 2bpp", FontManagerPage.Summary(_thai.File));
    }

    [AvaloniaFact]
    public void Groups_ThaiConsonantsAreThe44Letters()
    {
        var groups = FontEntry.Groups(_thai.File);
        Assert.Equal(["Basic Latin", "Thai consonants", "Thai vowels and marks", "Thai digits and signs", "Symbols"], groups.Select(g => g.Name));
        Assert.Equal([95, 44, 27, 16, 12], groups.Select(g => g.CodePoints.Count));
        Assert.Equal(0x0E01, groups[1].CodePoints[0]);
        Assert.Equal(0x0E2E, groups[1].CodePoints[^1]);
        Assert.DoesNotContain(0x0E24, groups[1].CodePoints);
        Assert.Equal(87, groups.Where(g => g.Set == "Thai").Sum(g => g.CodePoints.Count));
    }

    [AvaloniaFact]
    public void MissingIn_ListsCharactersWithoutGlyphs()
    {
        var missing = _thai.MissingIn(TilemapFontShots.FigmaPaste);
        Assert.Equal([0x00B7, 0x00BD], missing.Order());
        Assert.Empty(_thai.MissingIn("สวัสดี AGB Studio 0123\n"));
        var pixel8 = _lib.Fonts.First(f => f.Name == "pixel8");
        Assert.Contains(0x0E2E, pixel8.MissingIn(TilemapFontShots.FigmaPaste));
    }

    [AvaloniaFact]
    public void GlyphEditor_PaintWritesAnOverrideFontBuilderApplies_Undo()
    {
        var editor = new GlyphEditor();
        editor.Load(_thai, 0x0E01);
        var g = _thai.Font.Get(0x0E01)!;
        Assert.Equal(g.Top, editor.EditTop);
        Assert.Equal(_thai.Font.Baseline, editor.Baseline);
        // an empty pixel inside the glyph box becomes ink
        var (x, y) = Enumerable.Range(g.Top, g.Height).SelectMany(yy => Enumerable.Range(0, g.Advance).Select(xx => (xx, yy))).First(p => editor.PixelAt(p.xx, p.yy) == 0);
        editor.Paint(x, y);
        Assert.True(_thai.File.Glyphs.ContainsKey("U+0E01"));
        var after = _thai.Font.Get(0x0E01)!;
        Assert.Equal(3, after.Shades[(y - after.Top) * after.Width + (x - after.Left)]);
        Assert.True(_thai.IsDirty);
        _fx.Services.Undo.Undo();
        Assert.False(_thai.File.Glyphs.ContainsKey("U+0E01"));
        // the pixels above the source glyph's top cannot be stored in an override
        editor.Load(_thai, 0x0E01);
        Assert.True(editor.GridTop < editor.EditTop);
        Assert.False(editor.Paint(0, editor.GridTop));
        Assert.False(_thai.File.Glyphs.ContainsKey("U+0E01"));
    }

    [AvaloniaFact]
    public void SettingsEdits_AreUndoable_AndSavedWithDrawTo()
    {
        // the fixture's thai_vwf draws to BG 2 (Figma Font Manager)
        Assert.Equal(2, _thai.DrawTo);
        _thai.Edit("Font size", f => f.Size = 14);
        _thai.SetDrawTo(3);
        Assert.Equal(14, _thai.File.Size);
        _thai.Save();
        var text = File.ReadAllText(_thai.Path);
        Assert.Contains("\"drawTo\": 3", text);
        Assert.Equal(14, GameProject.Load<FontFile>(_thai.Path).Size);
        Assert.Equal(3, FontEntry.Load(_thai.Project, _thai.Path, new AgbStudio.Editor.Services.UndoService(5)).DrawTo);
        _fx.Services.Undo.Undo();
        _fx.Services.Undo.Undo();
        Assert.Equal((12, 2), (_thai.File.Size, _thai.DrawTo));
    }

    [AvaloniaFact]
    public void DialoguePreview_TextPixelsAreTheRomRenderer()
    {
        var lib = MapLibrary.Of(_fx.Services);
        var map = lib.SceneMap();
        Assert.NotNull(map);
        var ui = _thai.Project.Settings.Ui;
        var px = TextPreview.Dialogue(_thai, map, lib.Renderer, ui);
        Assert.Equal(240 * 160, px.Length);
        // the two text lines: TextLayout + TextRenderer exactly as the runtime places them
        var gf = _thai.Font;
        int lh = gf.LineHeight, bh = 3 * lh + 6, by = 160 - bh - 6, x0 = 6 + 8, y0 = by + 3 + lh;
        var layout = TextLayout.Layout(TextPreview.ThaiLine, gf, 228 - 16, true);
        var idx = TextRenderer.Render(layout, gf, 212, 2 * lh, new TextRenderer.Style(1, 4, 0));
        var palette = TextPreview.Palette(0xFF000000u | Convert.ToUInt32(ui.ScreenInk.TrimStart('#'), 16), 0xFF000000u | Convert.ToUInt32(ui.Screen.TrimStart('#'), 16));
        var ink = 0;
        for (var y = 0; y < 2 * lh; y++)
            for (var x = 0; x < 212; x++)
            {
                if (idx[y * 212 + x] == 0) continue;
                Assert.Equal(palette[idx[y * 212 + x]], px[(y0 + y) * 240 + x0 + x]);
                ink++;
            }
        Assert.True(ink > 200);
        Assert.True(layout.Lines >= 2);
    }

    [AvaloniaFact]
    public void NewFont_DefaultsBuildAPreviewAndCreateTheFile()
    {
        var dlg = new NewFontDialog(_lib, _thai.Project);
        Assert.Equal(["NotoSans.ttf", "NotoSansThai-Regular.ttf"], dlg.Files());
        Assert.Equal("NotoSansThai-Regular.ttf", dlg.File.Selected);
        var f = dlg.Settings();
        Assert.Equal(("notosansthai_regular_12", 12, true), (f.Name, f.Size, f.Variable));
        Assert.Equal(["Basic Latin", "Thai", "Symbols"], f.CharacterSets);
        Assert.True(dlg.RomBytes > 1000);
        Assert.Equal($"12 px · 2bpp · about {(int)Math.Round(dlg.RomBytes / 1024.0)} KB of ROM", dlg.Caption);
        var created = dlg.CreateFont()!;
        Assert.True(File.Exists(Path.Combine(_thai.Project.FontsDir, "notosansthai_regular_12.font")));
        Assert.True(File.Exists(Path.Combine(_thai.Project.FontsDir, "NotoSansThai-Regular.ttf")));
        Assert.Equal(194, created.Font.Glyphs.Count);
        Assert.Contains(_lib.Fonts, e => e == created);
        // a second one gets a unique name
        Assert.Equal("notosansthai_regular_12_2", dlg.CreateFont()!.Name);
    }

    [AvaloniaFact]
    public void AddCharacters_RenderFromSourceOrEmptyGlyphs()
    {
        var dlg = new AddCharactersDialog(_thai);
        dlg.Text.Text = TilemapFontShots.FigmaPaste;
        Assert.Equal(2, dlg.Missing.Count);
        var added = dlg.AddGlyphs();
        Assert.Equal(dlg.Missing, added);
        Assert.Contains("½", _thai.File.ExtraCharacters);
        Assert.Contains(0x00BD, FontBuilder.CodePointsOf(_thai.File));
        _fx.Services.Undo.Undo();
        Assert.Equal("", _thai.File.ExtraCharacters);
        dlg.RenderFromSource.IsChecked = false;
        dlg.AddGlyphs();
        Assert.True(_thai.File.Glyphs.ContainsKey("U+00BD"));
        Assert.Empty(_thai.MissingIn(TilemapFontShots.FigmaPaste));
        Assert.Contains(FontEntry.Groups(_thai.File), g => g.Name == "Extra characters" && g.CodePoints.Contains(0x00BD));
        // scanning the project's strings finds what its dialogue and localization tables need
        var strings = AddCharactersDialog.ProjectStrings(_thai.Project);
        Assert.Contains("เหรียญหายไปไหนหมดนะ?", strings);
        var pixel8 = new AddCharactersDialog(_lib.Fonts.First(f => f.Name == "pixel8"));
        pixel8.Scan.IsChecked = true;
        Assert.Contains(0x0E01, pixel8.Missing);
    }
}

/// <summary>Headless E2E of the Font Manager (T-P09), New Font (T-D15) and Add Characters (T-D16): P11.37, P11.38.</summary>
public class FontE2ETests : IDisposable
{
    readonly PageHarness _h = PageHarness.ForFonts();

    public void Dispose() => _h.Dispose();

    FontManagerPage Open()
    {
        _h.S.Commands.Execute("tools.fontManager");
        _h.Pump();
        return Assert.IsType<FontManagerPage>(_h.Win.CurrentPage);
    }

    [AvaloniaFact]
    public void FontManager_ListSetsGlyphsDetailPreviewStats()
    {
        var page = Open();
        Assert.Equal("thai_vwf", page.Font!.Name);
        Assert.Equal("thai_vwf.font", _h.Win.TitleBar.Document);
        Assert.Equal(["Font_thai_vwf", "Font_pixel8", "Font_title_big"], _h.All<TreeItem>().Where(t => t.Name!.StartsWith("Font_")).Select(t => t.Name));
        Assert.Equal("VWF · 12 px · 2bpp", _h.Find<Chip>("FontSummaryChip").Content);
        Assert.Equal(["Basic Latin · 95", "Thai · 87", "Symbols · 12", "Japanese kana · 0"], _h.All<CheckBox>().Where(c => c.Name!.StartsWith("Set_")).Select(c => c.Content as string));
        // glyph grid: Thai consonants with code points; click a glyph to see it
        Assert.Equal("Thai consonants · click a glyph to edit", ((TextBlock)_h.Find<Button>("GlyphGroupButton").Content!).Text);
        Assert.Equal(44, _h.Find<WrapPanel>("GlyphGrid").Children.Count);
        Assert.Equal("0E01", _h.Find<GlyphCell>("Glyph_0E01").Code);
        Assert.Equal(GlyphCellState.Selected, _h.Find<GlyphCell>("Glyph_0E01").State);
        _h.Click(_h.Find<GlyphCell>("Glyph_0E02"));
        Assert.Equal(0x0E02, page.SelectedGlyph);
        Assert.Equal($"ข · U+0E02 · ADVANCE {page.Font.Font.Get(0x0E02)!.Advance}", _h.Find<TextBlock>("GlyphDetailHeader").Text);
        Assert.Equal(0x0E02, page.Editor.CodePoint);
        // group menu: Basic Latin
        _h.Choose(_h.Find<Button>("GlyphGroupButton"), "Basic Latin");
        Assert.Equal(95, _h.Find<WrapPanel>("GlyphGrid").Children.Count);
        // stats and preview
        Assert.Equal(FontManagerPage.Kb(page.Font.Font.RomBytes), _h.Find<Meter>("Meter_ROM").ValueText);
        Assert.Equal("4 / 16 KB", _h.Find<Meter>("Meter_VRAMtext").ValueText);
        Assert.Equal("194 glyphs · 0 missing", _h.Find<StatusItem>("Status_Glyphs").Content);
        Assert.NotNull(_h.Find<Image>("Preview").Source);
        // another font
        _h.Click(_h.Find<TreeItem>("Font_pixel8"));
        Assert.Equal("pixel8", page.Font.Name);
        Assert.Equal("pixel8.font", _h.S.Documents.Active!.Name);
        Assert.Equal("Fixed 8×8 · 1bpp", _h.Find<Chip>("FontSummaryChip").Content);
        Assert.Equal("Basic Latin", page.Group!.Name);
    }

    [AvaloniaFact]
    public void FontManager_EditSettingsSetsAndPixels_SaveUndo()
    {
        var page = Open();
        var font = page.Font!;
        _h.Click(_h.Find<CheckBox>("Set_Japanesekana"));
        Assert.Contains("Japanese kana", font.File.CharacterSets);
        Assert.Equal($"Japanese kana · {FontBuilder.CharacterSets["Japanese kana"].Count}", _h.Find<CheckBox>("Set_Japanesekana").Content);
        _h.Type(_h.Find<InspectorField>("SizeField"), "14 px");
        Assert.Equal(14, font.File.Size);
        Assert.Equal("14", _h.Find<InspectorField>("HeightBaseField").Value);
        _h.Click(_h.Find<Segment>("Width_Fixed"));
        Assert.False(font.File.Variable);
        _h.Type(_h.Find<InspectorField>("SpacingLineField"), "2");
        Assert.Equal(2, font.File.Spacing);
        _h.Click(_h.Find<CheckBox>("LowerVowels"));
        Assert.False(font.File.PlaceLowerVowels);
        _h.Choose(_h.Find<InspectorField>("ColoursField"), "1bpp · 2 shades");
        Assert.Equal(1, font.File.Bpp);
        _h.Click(_h.Find<InspectorField>("ShadowField").GetVisualDescendants().OfType<AgbSwitch>().First());
        Assert.False(font.File.DropShadow);
        _h.Choose(_h.Find<InspectorField>("DrawToField"), "BG 3 text layer");
        Assert.Equal(3, font.DrawTo);
        _h.Choose(_h.Find<InspectorField>("FromField"), "NotoSans.ttf");
        Assert.Equal("NotoSans.ttf", font.File.Source);
        // a pixel of the selected glyph
        var editor = page.Editor;
        var p = editor.TranslatePoint(new Point(GlyphEditor.Pitch * (editor.Columns - 1) + 4, GlyphEditor.Pitch * (editor.Rows - 1) + 4), _h.Win)!.Value;
        _h.ClickAt(p);
        Assert.True(font.File.Glyphs.ContainsKey("U+0E01"));
        _h.Key(PhysicalKey.S, RawInputModifiers.Control);
        var saved = GameProject.Load<FontFile>(font.Path);
        Assert.Equal((14, false, 2, false, 1, false, "NotoSans.ttf"), (saved.Size, saved.Variable, saved.Spacing, saved.PlaceLowerVowels, saved.Bpp, saved.DropShadow, saved.Source));
        Assert.Contains("Japanese kana", saved.CharacterSets);
        Assert.True(saved.Glyphs.ContainsKey("U+0E01"));
        Assert.Contains("\"drawTo\": 3", File.ReadAllText(font.Path));
        Assert.True(_h.Find<StatusItem>("Status_Saved").IsVisible);
        _h.Key(PhysicalKey.Z, RawInputModifiers.Control);
        Assert.False(font.File.Glyphs.ContainsKey("U+0E01"));
        Assert.False(_h.Find<StatusItem>("Status_Saved").IsVisible);
    }

    [AvaloniaFact]
    public void NewFont_FromFontsPlus_CreatesAndShowsTheFont()
    {
        var page = Open();
        _h.Click(_h.Find<PanelHeader>("FontsHeader").GetVisualDescendants().OfType<Button>().First());
        Assert.Equal(("T-D15", "FontsPlus"), _h.S.Windows.Requests[^1]);
        var dlg = Assert.IsType<NewFontDialog>(_h.Win.Overlays.Open.Single());
        Assert.Equal(560, dlg.Bounds.Width);
        _h.Click(_h.Find<Segment>("Kind_1"));
        Assert.Equal(FontSourceKind.Bdf, dlg.Kind);
        // BDF offers AGB Pixel, the bundled Thai and Latin pixel font, drawn at one bit per pixel
        Assert.Equal(AgbPixelFont.FileName, dlg.File.Selected);
        Assert.Equal(1, dlg.Settings().Bpp);
        _h.Click(_h.Find<Segment>("Kind_0"));
        _h.Choose(dlg.File, "NotoSans.ttf");
        _h.Choose(dlg.WidthField, "Fixed 8×8");
        _h.Click(_h.Find<CheckBox>("NewFontSet_Thai"));
        _h.Type(dlg.Size, "8 px", enter: false);
        Assert.StartsWith("8 px · 2bpp · about", dlg.Caption);
        var confirm = dlg.GetVisualDescendants().OfType<AgbButton>().First(b => b.Name == "PART_Confirm");
        Assert.Equal("Create Font", confirm.Content);
        _h.Click(confirm);
        Assert.False(_h.Win.Overlays.IsOpen);
        Assert.Equal("notosans_8", page.Font!.Name);
        var file = GameProject.Load<FontFile>(page.Font.Path);
        Assert.Equal((false, 8, "NotoSans.ttf"), (file.Variable, file.Size, file.Source));
        Assert.Equal(["Basic Latin", "Symbols"], file.CharacterSets);
        Assert.Contains(_h.All<TreeItem>(), t => t.Label == "notosans_8" && t.IsSelected);
    }

    /// <summary>
    /// 4.9 "Warning: missing glyphs" on T-P09: an English string of the project (English is drawn with thai_vwf) uses ½
    /// and →, which thai_vwf has no glyph for. The status says so with the warning icon; clicking it shows the
    /// "Missing from project text" group with two Missing glyph cells. Add Characters, scanning the project's strings,
    /// adds them and the warning goes.
    /// </summary>
    [AvaloniaFact]
    public void FontManager_FlagsTheGlyphsTheProjectTextNeeds_StatusAndMissingCells()
    {
        var path = Path.Combine(_h.S.Project.Current!.TextDir, "strings.en.json");
        var en = GameProject.Load<StringTable>(path);
        en.Strings["hud.bonus"] = "BONUS ½ → NEXT";
        GameProject.Save(path, en);
        var page = Open();
        Assert.Equal("thai_vwf", page.Font!.Name);
        var status = _h.Find<StatusItem>("Status_Glyphs");
        Assert.Equal("196 glyphs · 2 missing", status.Content);
        Assert.Equal("warn", status.Glyph);
        // the grid opens on Thai consonants as before; the group menu has the missing ones last
        Assert.Equal("Thai consonants", page.Group!.Name);
        Assert.Equal(FontEntry.TextMissingGroup, page.Groups[^1].Name);
        _h.Click(status);
        Assert.Equal("Missing from project text · click a glyph to edit", ((TextBlock)_h.Find<Button>("GlyphGroupButton").Content!).Text);
        var cells = _h.Find<WrapPanel>("GlyphGrid").Children.OfType<GlyphCell>().ToList();
        Assert.Equal(["Glyph_00BD", "Glyph_2192"], cells.Select(c => c.Name));
        Assert.Equal(["½", "→"], cells.Select(c => c.Char));
        Assert.All(cells, c => Assert.Equal(GlyphCellState.Missing, c.State));
        // as in the build: Japanese (font jp_kana, not in fonts/) is drawn with the first font, pixel8, and falls back to
        // the English text; title_big draws no project text
        var fonts = FontLibrary.Of(_h.S).Fonts;
        Assert.Equal([0x00BD, 0x2192], fonts.First(f => f.Name == "pixel8").TextMissing.Order());
        Assert.Empty(fonts.First(f => f.Name == "title_big").TextMissing);

        // Add Characters › Also scan all dialogue and localization strings, as empty glyphs to draw: no warning any more
        _h.Click(_h.Find<Button>("AddCharacters"));
        var dlg = Assert.IsType<AddCharactersDialog>(_h.Win.Overlays.Open.Single());
        _h.Click(dlg.Scan);
        _h.Click(dlg.RenderFromSource);
        Assert.Equal([0x00BD, 0x2192], dlg.Missing.Order());
        _h.Click(dlg.GetVisualDescendants().OfType<AgbButton>().First(b => b.Name == "PART_Confirm"));
        Assert.False(_h.Win.Overlays.IsOpen);
        Assert.Equal("196 glyphs · 0 missing", status.Content);
        Assert.Equal("type", status.Glyph);
        Assert.DoesNotContain(page.Groups, g => g.Name == FontEntry.TextMissingGroup);
    }

    [AvaloniaFact]
    public void AddCharacters_PasteTextAddMissingGlyphs()
    {
        var page = Open();
        _h.Click(_h.Find<Button>("AddCharacters"));
        Assert.Equal(("T-D16", "AddCharacters"), _h.S.Windows.Requests[^1]);
        var dlg = Assert.IsType<AddCharactersDialog>(_h.Win.Overlays.Open.Single());
        Assert.Equal(600, dlg.Bounds.Width);
        Assert.False(_h.Find<StackPanel>("MissingWarning").IsVisible);
        dlg.Text.Focus();
        _h.Win.KeyTextInput("ฮอปเปอร์ ½ · ©");
        _h.Pump();
        Assert.Equal([0x00BD, 0x00B7], dlg.Missing);
        Assert.Equal("2 characters are not in thai_vwf yet", _h.Find<StackPanel>("MissingWarning").Children.OfType<TextBlock>().Single().Text);
        Assert.Equal(["Missing_00BD", "Missing_00B7"], _h.All<GlyphCell>().Where(c => c.Name!.StartsWith("Missing_")).Select(c => c.Name));
        Assert.Equal("Render the new glyphs from NotoSansThai-Regular.ttf", dlg.RenderFromSource.Content);
        var confirm = dlg.GetVisualDescendants().OfType<AgbButton>().First(b => b.Name == "PART_Confirm");
        Assert.Equal("Add 2 glyphs", confirm.Content);
        _h.Click(confirm);
        Assert.False(_h.Win.Overlays.IsOpen);
        Assert.Contains("½", page.Font!.File.ExtraCharacters);
        Assert.Equal(0x00BD, page.SelectedGlyph);
        Assert.True(_h.S.Documents.Active!.IsDirty);
    }
}
