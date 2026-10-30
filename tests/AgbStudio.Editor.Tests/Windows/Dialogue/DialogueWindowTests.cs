using System.Text.Json;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Commands;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Tests.Support;
using AgbStudio.Editor.Tests.Windows.Database;
using AgbStudio.Editor.Theme;
using AgbStudio.Editor.Windows.Database;
using AgbStudio.Editor.Windows.Dialogue;
using AgbStudio.Scripting;
using AgbStudio.Scripting.Dialogue;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.Dialogue;

/// <summary>11.K Dialogue Editor: P11.46 (conversations, lines, language toggle, choices with targets, speakers, line inspector, GBA preview) and P11.47 (New Speaker, Add to Dialogue and Command menus).</summary>
public class DialogueWindowTests : DataE2E
{
    DialogueEditorPage Page => Assert.IsType<DialogueEditorPage>(Win.CurrentPage);

    DialogueEditorPage OpenFromToolsMenu()
    {
        S.Commands.Execute("tools.dialogueEditor");
        Capture.Pump();
        return Page;
    }

    string DlgPath(string name) => Path.Combine(Root, "dialogue", name + ".dlg");

    /// <summary>The saved file compiles with the dialogue compiler of the build (no diagnostics).</summary>
    static string Compile(string path)
    {
        var f = DialogueFile.Parse(File.ReadAllText(path));
        var cs = DialogueCompiler.ToCSharp(f, path, new ScriptCompileContext(), [], [], out var diags);
        Assert.Empty(diags);
        return cs;
    }

    [AvaloniaFact]
    public void P11_46_ConversationsLinesChoicesSpeakersAndLanguageToggle()
    {
        var page = OpenFromToolsMenu();
        Assert.Contains(("T-P16", (string?)"tools.dialogueEditor"), S.Windows.Requests);
        Assert.Equal("forest_intro.dlg", Win.TitleBar.Document);
        Assert.Equal(["forest_intro", "shop_welcome", "cave_hint", "boss_summit"], page.ConversationItems.Select(c => c.Label));
        Assert.Equal(["3", "4", "3", "9"], page.ConversationItems.Select(c => c.Meta));
        Assert.Equal(["Hopper", "Owl", "Slime King"], page.SpeakerItems.Select(s => s.Label));
        Assert.Equal(["hero", "block", "slime"], page.SpeakerItems.Select(s => s.Meta));
        Assert.Equal("forest_intro · 3 lines · 2 choices", page.StatusText);
        Assert.Equal("ไทย / English", page.LanguageChipText);
        string Texts(string name) => string.Join("|", Find<Control>(name).GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));
        Assert.Equal("Hopper|เหรียญหายไปไหนหมดนะ?|Where did all the coins go?", Texts("Line_1"));
        Assert.Contains("ไปที่ถ้ำ|Go to the cave|Scene.Load(\"Cave_01\")", Texts("Choice_2_1"));
        Assert.Contains("ถามอีกครั้ง|Ask again|goto line 2", Texts("Choice_2_2"));
        // the toolbar chip swaps the two languages
        Click(Find<Chip>("LanguageChip"));
        Assert.Equal("English / ไทย", page.LanguageChipText);
        Assert.Equal("Hopper|Where did all the coins go?|เหรียญหายไปไหนหมดนะ?", Texts("Line_1"));
        // another conversation
        Click(page.ConversationItems[3]);
        Assert.Equal("boss_summit · 9 lines · 0 choices", page.StatusText);
        Assert.Equal("boss_summit.dlg", Win.TitleBar.Document);
    }

    [AvaloniaFact]
    public void P11_46_LineInspectorAndGbaPreview()
    {
        var page = OpenFromToolsMenu();
        Click(Find<Border>("Line_2"));
        Assert.Equal(1, page.SelectedLine);
        var f = page.Inspector.Fields;
        Assert.Equal("Line 2", page.Inspector.SectionTitle);
        Assert.Equal("Owl", f["Speaker"].Value);
        Assert.Equal("owl_happy", f["Portrait"].Value);
        Assert.Equal("2 ch/frame", f["Text speed"].Value);
        Assert.Equal(50, f["Text speed"].SliderValue);
        Assert.Equal("blip.wav", f["Blip sound"].Value);
        Assert.Equal("!Flags.MetOwl", f["Show if"].Value);
        Assert.Equal("Flags.MetOwl = true", f["Then set"].Value);
        // the preview is the ROM's dialogue box: the line, then its choices with the cursor
        var expected = page.Gba!.RenderBox("th", "เจ้าสไลม์ขโมยไปไว้ในถ้ำน่ะสิ!", ["ไปที่ถ้ำ", "ถามอีกครั้ง"])!;
        Assert.Equal(expected.Argb, page.Inspector.Screen.Box!.Argb);
        // speaker from the dropdown; undo
        Click(f["Speaker"].GetVisualDescendants().OfType<Button>().First(b => b.Name == "PART_Dropdown"));
        Assert.True(Dropdown.Choose("Hopper"));
        Assert.Equal("Hopper", page.Line!.Speaker);
        Assert.Equal("Hopper", page.Inspector.Fields["Speaker"].Value);
        S.Commands.Execute("edit.undo");
        Capture.Pump();
        Assert.Equal("Owl", page.Line!.Speaker);
        // conditions are typed into the inspector
        var showIf = page.Inspector.Fields["Show if"].GetVisualDescendants().OfType<TextBox>().First(b => b.Name == "PART_Text");
        showIf.Focus();
        showIf.SelectAll();
        Type("Flags.MetOwl == false");
        Key(PhysicalKey.Enter);
        Assert.Equal("Flags.MetOwl == false", page.Line!.ShowIf);
        // text speed: slider to the end = 4 characters per frame
        var slider = page.Inspector.Fields["Text speed"].GetVisualDescendants().OfType<AgbSlider>().Single();
        var end = slider.TranslatePoint(new Avalonia.Point(slider.Bounds.Width - 1, 6), Win)!.Value;
        Win.MouseMove(end);
        Win.MouseDown(end, MouseButton.Left);
        Win.MouseUp(end, MouseButton.Left);
        Capture.Pump();
        Assert.Equal(4, page.Line!.Speed);
        // a choice shows its target and conditions
        Click(Find<Border>("Choice_2_2"));
        Assert.Equal(("Choice 2", "goto line 2"), (page.Inspector.SectionTitle, page.Inspector.Fields["Target"].Value));
        page.SetTarget(1, 1, "goto line 3");
        Assert.Equal("hopper_02", page.Line!.Choices![1].Goto);
        Save();
        Assert.Contains("\"goto\": \"hopper_02\"", File.ReadAllText(DlgPath("forest_intro")));
        Compile(DlgPath("forest_intro"));
    }

    [AvaloniaFact]
    public void P11_46_EditTextInACardSavesTheDialogueAndTheStringTable()
    {
        var page = OpenFromToolsMenu();
        var text = Find<Panel>("Text_th_1_0").Children.OfType<TextBlock>().Single();
        Click(text, clicks: 2);
        var editor = Find<TextBox>("LineEditor");
        editor.SelectAll();
        Type("เหรียญอยู่ไหนนะ?");
        Key(PhysicalKey.Enter);
        Assert.Equal("เหรียญอยู่ไหนนะ?", page.Data!.Text("th", "dlg.forest.hopper_01"));
        Assert.True(S.Documents.Active!.IsDirty);
        Assert.Contains("เหรียญอยู่ไหนนะ?", Find<Control>("Line_1").GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));
        Save();
        Assert.False(S.Documents.Active!.IsDirty);
        var file = DialogueFile.Parse(File.ReadAllText(DlgPath("forest_intro")));
        Assert.Equal("เหรียญอยู่ไหนนะ?", file.Conversations[0].Lines[0].Text!["th"]);
        Assert.Equal("เหรียญอยู่ไหนนะ?", GameProject.Load<StringTable>(Path.Combine(Root, "text", "strings.th.json")).Strings["dlg.forest.hopper_01"]);
        Assert.Contains("await Say(\"dlg.forest.hopper_01\", \"Hopper\", \"hero\", 2, \"blip\");", Compile(DlgPath("forest_intro")));
        S.Commands.Execute("edit.undo");
        Capture.Pump();
        Assert.Equal("เหรียญหายไปไหนหมดนะ?", page.Data.Text("th", "dlg.forest.hopper_01"));
    }

    [AvaloniaFact]
    public void P11_47_AddToDialogueAndCommandMenusMatchFigma()
    {
        var menus = JsonDocument.Parse(File.ReadAllText(Path.Combine(Repo.FigmaEvidence, "menus.json"))).RootElement.EnumerateArray().ToList();
        using var fx = new Fixture(withApp: false);
        foreach (var (title, entries) in new[] { ("Add to Dialogue", DialogueMenus.AddToDialogue()), ("Command", DialogueMenus.CommandMenu()) })
        {
            var fig = menus.Single(m => m.GetProperty("title").GetString() == title).GetProperty("items").EnumerateArray().ToList();
            Assert.Equal(fig.Count, entries.Count);
            for (var i = 0; i < fig.Count; i++)
            {
                var item = fig[i];
                if (item.TryGetProperty("separator", out _)) { Assert.IsType<SeparatorEntry>(entries[i]); continue; }
                var icon = item.TryGetProperty("icon", out var ic) ? ic.GetString() : null;
                var shortcut = item.GetProperty("shortcut").ValueKind == JsonValueKind.Null ? null : item.GetProperty("shortcut").GetString();
                switch (entries[i])
                {
                    case SubmenuEntry s:
                        Assert.Equal(("Submenu", item.GetProperty("label").GetString(), icon), ("Submenu", s.Title, s.Glyph));
                        Assert.Equal("T-M24", s.Id);
                        break;
                    case CommandEntry c:
                        var cmd = fx.Services.Commands.Get(c.CommandId);
                        Assert.Equal(item.GetProperty("label").GetString(), cmd.Title);
                        Assert.Equal(icon, c.ShowIcon ? cmd.Glyph : null);
                        Assert.Equal(shortcut, fx.Services.Shortcuts.GestureOf(cmd.Id) is { } g ? Shortcuts.Format(g) : null);
                        break;
                    default:
                        Assert.Fail($"{title} item {i}");
                        break;
                }
            }
        }
        Assert.True(fx.Services.Windows.IsRegistered("T-M23"));
        Assert.True(fx.Services.Windows.IsRegistered("T-M24"));
    }

    [AvaloniaFact]
    public void P11_47_AddLineChoiceCommandsConditionAndPaste()
    {
        var page = OpenFromToolsMenu();
        Click(page.AddBox);
        Assert.Contains(("T-M23", (string?)"Dialogue.AddBox"), S.Windows.Requests);
        var c = page.Conversation!;
        page.SelectLine(2);
        S.Commands.Execute("dialogue.add.line");
        Capture.Pump();
        Assert.Equal(4, c.Lines.Count);
        Assert.Equal(("Hopper", "dlg.forest_intro.l4"), (c.Lines[3].Speaker, c.Lines[3].Key));
        Assert.Equal(3, page.SelectedLine);
        S.Commands.Execute("dialogue.add.choice");
        Capture.Pump();
        Assert.Single(c.Lines[3].Choices!);
        Assert.Equal(0, page.SelectedChoice);
        Assert.Equal("end", page.Inspector.Fields["Target"].Value);
        foreach (var (cmd, kind) in new[] { ("dialogue.command.goToScene", "goToScene"), ("dialogue.command.setFlag", "setFlag"), ("dialogue.command.giveItem", "giveItem"),
                     ("dialogue.command.playSound", "playSound"), ("dialogue.command.wait", "wait"), ("dialogue.command.shakeCamera", "shakeCamera") })
        {
            S.Commands.Execute(cmd);
            Capture.Pump();
            Assert.Equal(kind, page.Line!.Command);
        }
        var give = c.Lines.First(l => l.Command == "giveItem");
        Assert.Equal(("Items", "Coin", 1), (give.Table, give.Item, give.Count));
        Assert.Equal("Items.Give(ItemId.Coin, 1)", LineCards.Summary(give));
        Assert.Equal(10, c.Lines.Count);
        // the command's fields: Shake camera
        Assert.Equal("Shake camera", page.Inspector.SectionTitle);
        Assert.Equal(("2", "20"), (page.Inspector.Fields["Amplitude"].Value, page.Inspector.Fields["Frames"].Value));
        // Condition opens "Show if"
        page.SelectLine(0);
        S.Commands.Execute("dialogue.add.condition");
        Capture.Pump();
        Assert.True(page.Inspector.Fields["Show if"].GetVisualDescendants().OfType<TextBox>().First(b => b.Name == "PART_Text").IsFocused);
        // copy / paste through the Edit menu
        Find<Border>("Line_1").Focus();
        S.Commands.Execute("edit.copy");
        S.Commands.Execute("edit.paste");
        Capture.Pump();
        Assert.Equal(11, c.Lines.Count);
        Assert.Equal("เหรียญหายไปไหนหมดนะ?", page.Data!.Text("th", c.Lines[1].Key!));
        Save();
        Compile(DlgPath("forest_intro"));
    }

    [AvaloniaFact]
    public void P11_47_NewSpeakerWithThaiNamePortraitColourAndBlip()
    {
        var page = OpenFromToolsMenu();
        Find<PanelHeader>("SpeakersHeader").ActionCommand!.Execute(null);
        Capture.Pump();
        var dlg = Assert.IsType<NewSpeakerDialog>(Win.Overlays.Open.Single());
        Assert.Equal("blip_low.wav", dlg.Blip.Selected);
        Type("Bat");
        Assert.Equal("bat", dlg.Portrait);   // the sprite named like the speaker
        Assert.Equal("Portrait_bat", dlg.GetVisualDescendants().OfType<StackPanel>().First(p => p.Name == "Portraits").Children[0].Name);
        Assert.Contains(dlg.GetVisualDescendants().OfType<Border>(), b => b.Name == "Portrait_Add");
        dlg.NameTh.Text = "ค้างคาว";
        Click(dlg.GetVisualDescendants().OfType<Swatch>().First(s => s.Name == "Tag_2"));
        Assert.Equal(2, dlg.ColorIndex);
        Click(dlg.GetVisualDescendants().OfType<AgbButton>().First(b => (string?)b.Content == "Add Speaker"));
        Assert.False(Win.Overlays.IsOpen);
        Assert.Equal(["Hopper", "Owl", "Slime King", "Bat"], page.SpeakerItems.Select(s => s.Label));
        var sp = page.Current!.File.Speakers[^1];
        Assert.Equal(("Bat", "bat", "blip_low"), (sp.Name, sp.Portrait, sp.Blip));
        Assert.Equal("ค้างคาว", page.Current.SpeakerName("Bat", "th"));
        Save();
        var json = File.ReadAllText(DlgPath("forest_intro"));
        Assert.Contains("\"th\": \"ค้างคาว\"", json);
        Assert.Contains($"\"color\": \"{AgbStudio.Build.Graphics.Gba.Hex(NewSpeakerDialog.TagColors[2])}\"", json);
        Assert.Equal("Bat", DialogueFile.Parse(json).Speakers[^1].Name);
        // a name in use is refused
        var again = Dialog<NewSpeakerDialog>("T-D21", page);
        again.NameEn.Text = "owl";
        Assert.Null(again.Create());
        Win.Overlays.CloseTop();
        S.Commands.Execute("edit.undo");
        Capture.Pump();
        Assert.Equal(3, page.Current.File.Speakers.Count);
    }

    [AvaloniaFact]
    public void P11_46_NewConversationFromCreateMenuAndUndo()
    {
        S.Commands.Execute("assets.create.dialogue");
        Capture.Pump();
        var page = Page;
        Assert.Equal("conversation_1", page.Current!.Name);
        Assert.True(File.Exists(DlgPath("conversation_1")));
        Assert.Equal(["Hopper", "Owl", "Slime King"], page.SpeakerItems.Select(s => s.Label));
        Assert.Equal("conversation_1 · 0 lines · 0 choices", page.StatusText);
        S.Commands.Execute("edit.undo");
        Capture.Pump();
        Assert.False(File.Exists(DlgPath("conversation_1")));
        Assert.Equal("forest_intro", page.Current!.Name);
    }
}
