using System.Text;
using AgbStudio.Build.Fonts;
using AgbStudio.Build.Graphics;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Tests.Windows.Database;
using AgbStudio.Editor.Windows.Database;
using AgbStudio.Editor.Windows.Localization;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.Localization;

/// <summary>11.I Localization: P11.41 (languages, string table, search, groups, notes, speaker, GBA preview, missing strings) and P11.42 (Add Language, Export .csv).</summary>
public class LocalizationWindowTests : DataE2E
{
    LocalizationPage Page => Assert.IsType<LocalizationPage>(Win.CurrentPage);

    LocalizationPage OpenFromToolsMenu()
    {
        S.Commands.Execute("tools.localization");
        Capture.Pump();
        return Page;
    }

    [AvaloniaFact]
    public void P11_41_LanguagesWithProgressGroupsAndStatus()
    {
        var page = OpenFromToolsMenu();
        Assert.Contains(("T-P14", (string?)"tools.localization"), S.Windows.Requests);
        Assert.Equal("Localization", Win.TitleBar.Document);
        Assert.Equal(["English (source)", "ไทย Thai", "日本語 Japanese"], page.LanguageItems.Select(l => l.Label));
        Assert.Equal(["340 / 340", "326 / 340", "0 / 340"], page.LanguageItems.Select(l => l.Meta));
        Assert.True(page.LanguageItems[1].IsSelected);
        Assert.Equal("th", page.Target);
        var groups = page.GroupItems.Select(g => (g.Label, g.Meta)).ToList();
        Assert.Equal(("All", "340"), groups[0]);
        Assert.Equal("Dialogue", groups[1].Label);
        Assert.Equal(("UI & menus", "86"), groups[2]);
        Assert.Equal(("Items", "48"), groups[3]);
        Assert.Equal(("Missing only", "14"), groups[^1]);
        Assert.Equal("ไทย 326 / 340", Find<Chip>("ProgressChip").Content);
        Assert.Equal("14 strings missing in ไทย", Find<StatusItem>("Status_Missing").Content);
        Assert.Equal("340 keys", Find<StatusItem>("Status_Keys").Content);
        Assert.Equal(340, page.VisibleKeys.Count);
        // the table: key, English, ไทย, fit
        Assert.Equal(["KEY", "ENGLISH", "ไทย", "FIT"], Find<Grid>("StringHeader").Children.OfType<TableCell>().Select(c => c.Text!.ToUpperInvariant()));
        var missing = page.RowOf("shop.welcome");
        Assert.Null(missing.Target);
        Assert.True(page.RowOf("dlg.forest.hopper_02").Fit!.Fits);
        // Japanese as the target: nothing translated
        Click(page.LanguageItems[2]);
        Assert.Equal("ja", page.Target);
        Assert.Equal("日本語 0 / 340", Find<Chip>("ProgressChip").Content);
        Assert.Equal("340 strings missing in 日本語", Find<StatusItem>("Status_Missing").Content);
    }

    [AvaloniaFact]
    public void P11_41_SearchGroupsAndMissingOnly()
    {
        var page = OpenFromToolsMenu();
        Click(Find<ListRow>("Group_Missing"));
        Assert.Equal(14, page.VisibleKeys.Count);
        Assert.Contains("shop.welcome", page.VisibleKeys);
        Assert.All(page.VisibleKeys, k => Assert.Null(page.Data!.Text("th", k)));
        Click(Find<ListRow>("Group_UImenus"));
        Assert.Equal(86, page.VisibleKeys.Count);
        Click(Find<ListRow>("Group_All"));
        page.SearchBox.Focus();
        Type("sunset");
        Assert.Equal(["dlg.forest.hopper_02"], page.VisibleKeys);
        // Thai search finds the Thai text
        page.SearchBox.Text = "";
        page.SearchBox.Focus();
        Type("ยาฟื้น");
        Assert.Equal(["item.potion.name"], page.VisibleKeys);
        // the toolbar filter toggles Missing only
        page.SearchBox.Text = "";
        Click(Find<IconButton>("Tool_Missing"));
        Assert.Equal(LocalizationPage.MissingOnly, page.Group);
    }

    [AvaloniaFact]
    public void P11_41_StringPanelAndGbaPreviewDrawLikeTheRom()
    {
        var page = OpenFromToolsMenu();
        page.SelectKey("dlg.forest.hopper_02");
        Capture.Pump();
        Assert.Equal("Hopper", page.SpeakerField!.Value);
        Assert.Equal("thai_vwf", page.FontField!.Value);
        Assert.Equal("I have to find them before sunset!", page.SourceBox!.Text);
        Assert.Equal("ต้องรีบตามหาก่อนพระอาทิตย์ตก!", page.TargetBox!.Text);
        Assert.Equal("Hopper is worried; keep it short so it fits two lines.", page.NotesBox!.Text);
        // the preview is the runtime dialogue box: same font build, layout and renderer as the ROM
        var text = "ต้องรีบตามหาก่อนพระอาทิตย์ตก!";
        var p = page.Data!.Project;
        var ff = GameProject.Load<FontFile>(Path.Combine(p.FontsDir, "thai_vwf.font"));
        var all = string.Concat(page.Data.Strings.Values.SelectMany(t => t.Strings.Values)) + string.Concat(p.Settings.Localization.Languages.Select(l => l.Name));
        var font = FontBuilder.Build(ff, p.FontsDir, ThaiText.CodePoints(all).Where(c => c >= 0x20).Distinct().ToList());
        var layout = TextLayout.Layout(text, font, 224, true);
        var px = TextRenderer.Render(layout, font, 224, 40, new TextRenderer.Style(1, 4, 5));
        var box = page.Screen.Box!;
        Assert.Equal((224, 40), (box.Width, box.Height));
        ushort[] runtime = [0, 0x0C63, 0x294A, 0x4E73, 0x6318, 0x7BDE];
        for (var y = 0; y < 34; y++)   // above the continue marker
            for (var x = 0; x < 224; x++)
                Assert.Equal(px[y * 224 + x] == 0 ? 0u : Gba.ToArgb(runtime[px[y * 224 + x]]), box.Argb[y * 224 + x]);
        Assert.Equal(Gba.ToArgb(0x0C63), box.Argb[(40 - 6) * 224 + 224 - 9]);   // the continue marker
        // scope log T-P14, T-P16: what the ROM shows, the runtime's light box (paper 0x7BDE, as IG-03) and not Figma's
        // dark one, with the text on one line as the ROM breaks it, not Figma's two
        Assert.Equal(1, layout.Lines);
        Assert.Equal(Gba.ToArgb(0x7BDE), box.Argb[39 * 224]);
        Assert.Equal(Gba.ToArgb(0x7BDE), box.Argb[223]);
        Assert.Equal(page.Gba!.Layout("th", text)!.Lines, layout.Lines);
        Assert.Equal($"{layout.MaxWidth} / 224 px", page.TargetMeter!.ValueText);
        Assert.Equal($"พอดี {layout.Lines} บรรทัดในกล่องข้อความ (สูงสุด 3) · ตัดคำไทยตามพจนานุกรม", page.Caption);
        // speaker and notes are edited in strings.meta.json
        Click(page.SpeakerField.GetVisualDescendants().OfType<Button>().First(b => b.Name == "PART_Dropdown"));
        Assert.True(Dropdown.Choose("Owl"));
        Assert.Equal("Owl", page.Data.Info("dlg.forest.hopper_02").Speaker);
        page.NotesBox.Focus();
        page.NotesBox.SelectAll();
        Type("Short!");
        page.SearchBox.Focus();
        Capture.Pump();
        Assert.Equal("Short!", page.Data.Info("dlg.forest.hopper_02").Notes);
        Save();
        Assert.Contains("\"notes\": \"Short!\"", File.ReadAllText(Path.Combine(Root, "text", "strings.meta.json")));
    }

    [AvaloniaFact]
    public void P11_41_TypingThaiUpdatesTableFitAndPreview_UndoAndSave()
    {
        var page = OpenFromToolsMenu();
        page.SelectKey("shop.welcome");
        Capture.Pump();
        // missing: the preview shows the fallback language, as the ROM does until it is translated
        Assert.Equal(page.Gba!.RenderBox("en", "Welcome! Take a look around.")!.Argb, page.Screen.Box!.Argb);
        page.TargetBox!.Focus();
        Type("ยินดีต้อนรับ ลองดูรอบๆ");
        Assert.Equal("ยินดีต้อนรับ ลองดูรอบๆ", page.Data!.Text("th", "shop.welcome"));
        Assert.Equal("ok", Find<Border>("Fit_shop.welcome").GetVisualDescendants().OfType<Chip>().Single().Content);
        Assert.Equal(page.Gba.RenderBox("th", "ยินดีต้อนรับ ลองดูรอบๆ")!.Argb, page.Screen.Box!.Argb);
        Assert.True(S.Documents.Active!.IsDirty);
        page.SearchBox.Focus();
        Capture.Pump();
        Assert.Equal("ไทย 327 / 340", Find<Chip>("ProgressChip").Content);
        S.Commands.Execute("edit.undo");
        Capture.Pump();
        Assert.Null(page.Data.Text("th", "shop.welcome"));
        S.Commands.Execute("edit.redo");
        Capture.Pump();
        Save();
        Assert.False(S.Documents.Active!.IsDirty);
        Assert.Equal("ยินดีต้อนรับ ลองดูรอบๆ", GameProject.Load<StringTable>(Path.Combine(Root, "text", "strings.th.json")).Strings["shop.welcome"]);
    }

    [AvaloniaFact]
    public void P11_41_FitCheckSaysHowManyLinesTheBoxNeeds()
    {
        var page = OpenFromToolsMenu();
        var data = page.Data!;
        const string key = "item.potion.desc";
        Assert.Equal(1, data.Info(key).MaxLines);
        var longText = "ฟื้นพลัง 2 หัวใจ รสสตรอว์เบอร์รี หอมหวานชื่นใจ ใช้ได้ทุกเวลา";
        data.SetText("th", key, longText);
        data.NotifyChanged();
        page.SelectKey(key);
        Capture.Pump();
        var layout = page.Gba!.Layout("th", longText, 224)!;
        Assert.True(layout.Lines >= 2);
        Assert.Equal("long", Find<Border>("Fit_" + key).GetVisualDescendants().OfType<Chip>().Single().Content);
        Assert.Equal($"needs {layout.Lines} lines; the box shows 1", page.Caption);
        Assert.Equal(ProgressStatus.Warn, page.TargetMeter!.Status);
        // Auto-check fit off: the column keeps its last result while the text changes
        Click(page.AutoCheckButton);
        Assert.False(page.AutoCheck);
        Assert.Equal(ButtonKind.Secondary, page.AutoCheckButton.Kind);
        data.SetText("th", key, "สั้น");
        data.NotifyChanged();
        Assert.False(page.RowOf(key).Fit!.Fits);
        Click(page.AutoCheckButton);
        Assert.True(page.RowOf(key).Fit!.Fits);
    }

    [AvaloniaFact]
    public void P11_42_AddLanguageJapaneseCreatesAKanaFont_FrenchUsesTheCurrentFont()
    {
        var page = OpenFromToolsMenu();
        var data = page.Data!;
        // the fixture already has Japanese: remove it to add it again
        data.Loc.Languages.RemoveAll(l => l.Code == "ja");
        data.Strings.Remove("ja");
        File.Delete(Path.Combine(Root, "text", "strings.ja.json"));
        page.Refresh();
        Find<PanelHeader>("LanguagesHeader").ActionCommand!.Execute(null);
        Capture.Pump();
        var dlg = Assert.IsType<AddLanguageDialog>(Win.Overlays.Open.Single());
        Assert.Equal("日本語 Japanese", dlg.LanguageField.Selected);
        Assert.Equal("English", dlg.FromField.Selected);
        Assert.Equal("jp_kana · new font", dlg.FontField.Selected);
        Assert.Equal("thai_vwf has no kana — a new font will be created", dlg.FontField.Help);
        Assert.Equal("Mark all 340 strings as missing", dlg.MarkMissing.Content);
        Assert.Equal("Fall back to English until translated", dlg.Fallback.Content);
        Click(dlg.GetVisualDescendants().OfType<AgbButton>().First(b => (string?)b.Content == "Add Language"));
        Assert.False(Win.Overlays.IsOpen);
        var ja = data.Loc.Languages.Single(l => l.Code == "ja");
        Assert.Equal(("日本語", "jp_kana", "en"), (ja.Name, ja.Font, ja.Fallback));
        var font = GameProject.Load<FontFile>(Path.Combine(Root, "fonts", "jp_kana.font"));
        Assert.Contains("Japanese kana", font.CharacterSets);
        Assert.Empty(GameProject.Load<StringTable>(Path.Combine(Root, "text", "strings.ja.json")).Strings);
        Assert.Contains("\"code\": \"ja\"", File.ReadAllText(S.Project.Current!.ProjectPath));
        Assert.Equal("日本語 Japanese", page.LanguageItems[^1].Label);
        S.Commands.Execute("edit.undo");
        Capture.Pump();
        Assert.False(File.Exists(Path.Combine(Root, "fonts", "jp_kana.font")));
        Assert.DoesNotContain(data.Loc.Languages, l => l.Code == "ja");

        // French: Latin is in thai_vwf, so no new font; not marked missing = starts from the English text
        var fr = Dialog<AddLanguageDialog>("T-D20", page);
        fr.Choose("fr");
        Assert.Equal("thai_vwf", fr.FontField.Selected);
        Assert.Null(fr.FontField.Help);
        fr.MarkMissing.IsChecked = false;
        fr.Fallback.IsChecked = false;
        Assert.Equal("fr", fr.Create());
        Assert.Equal(340, data.Translated("fr"));
        Assert.Equal("", data.Loc.Languages.Single(l => l.Code == "fr").Fallback);
    }

    [AvaloniaFact]
    public async Task P11_42_ExportCsvWritesEveryKeyAndLanguage()
    {
        var page = OpenFromToolsMenu();
        var target = Path.Combine(Fx.Folder, "export.csv");
        FileChooser.For(S).SaveOverride = (title, name) =>
        {
            Assert.Equal("Export .csv", title);
            Assert.Equal("SkyHopper_strings.csv", name);
            return Task.FromResult<string?>(target);
        };
        Click(page.ExportButton);
        await Task.Yield();
        Capture.Pump();
        var bytes = File.ReadAllBytes(target);
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);
        var lines = Encoding.UTF8.GetString(bytes[3..]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("key,group,speaker,notes,en,th,ja", lines[0]);
        Assert.Equal(341, lines.Length);
        Assert.Contains("dlg.forest.hopper_02,Dialogue,Hopper,Hopper is worried; keep it short so it fits two lines.,I have to find them before sunset!,ต้องรีบตามหาก่อนพระอาทิตย์ตก!,", lines);
        Assert.Contains(S.Toasts.Visible, t => t.Text.Contains("340 keys"));
    }
}
