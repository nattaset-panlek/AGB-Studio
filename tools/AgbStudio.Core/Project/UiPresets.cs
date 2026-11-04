namespace AgbStudio.Core.Project;

/// <summary>Ready-made UI pages. A script can open one by name with no file in the project (UI.Open("TextInput")); the UI
/// Editor copies one into ui/ to change it, and a page of the same name in the project is used instead.</summary>
public static class UiPresets
{
    public static readonly IReadOnlyList<string> Names = ["TextInput", "Confirm", "Message", "Menu", "List", "Title", "Pause", "Options", "Choice"];

    /// <summary>Makes a ready-made page. <paramref name="firstScene"/>: where a Title page's Start goes. A word uses its string
    /// key when <paramref name="keys"/> has it, else plain English.</summary>
    public static UiPageFile? Create(string name, UiSettings ui, string firstScene = "", IReadOnlyCollection<string>? keys = null) => name switch
    {
        "Title" => Title(ui, firstScene, keys),
        "Pause" => Pause(ui, keys),
        "Options" => Options(ui, keys),
        "Choice" => Choice(ui),
        "TextInput" => TextInput(ui),
        "Confirm" => Confirm(ui),
        "Message" => Message(ui),
        "Menu" => Menu(ui),
        "List" => List(ui),
        _ => null,
    };

    static UiElement Canvas(params UiElement[] children) => new() { Name = "Canvas", Rect = UiRect.Stretch(), Children = children.ToList() };

    static UiRect At(double ax, double ay, int x, int y, int w, int h) =>
        new() { AnchorMinX = ax, AnchorMaxX = ax, AnchorMinY = ay, AnchorMaxY = ay, PivotX = ax, PivotY = ay, X = x, Y = y, W = w, H = h };

    static UiRect Size(int w, int h) => UiRect.TopLeft(0, 0, w, h);

    static UiElement Window(string name, UiRect rect, UiSettings ui, UiLayoutGroup? layout, params UiElement[] children) => new()
    {
        Name = name, Rect = rect, Fill = ui.Paper, Border = ui.Ink, Layout = layout, Children = children.ToList(),
    };

    static UiLayoutGroup Stack() => new()
    {
        Kind = UiLayoutKind.Vertical, PaddingLeft = 6, PaddingTop = 6, PaddingRight = 6, PaddingBottom = 6, StretchCross = true, FitH = true,
    };

    static UiElement Text(string name, string text, UiRect rect, UiSettings ui, UiAlign alignX = UiAlign.Start) => new()
    {
        Name = name, Kind = UiElementKind.Text, Rect = rect, Text = text, TextColor = ui.Ink, TextAlignX = alignX,
    };

    static UiElement Button(string name, string text, int data, UiRect rect, UiSettings ui) => new()
    {
        Name = name, Kind = UiElementKind.Button, Rect = rect, Text = text, TextColor = ui.Ink, Cursor = true, Accent = ui.Ink, DisabledTextColor = ui.Muted,
        FocusFill = ui.BarTrack, FocusTextColor = ui.ScreenInk, PaddingLeft = 2, PaddingRight = 2, Action = new UiAction { Data = data },
    };

    static string Word(IReadOnlyCollection<string>? keys, string key, string plain) => keys?.Contains(key) == true ? key : plain;

    // A title screen: the scene's UI Page. Start goes to a scene, Continue reads save slot 0 (off when it is empty), Options
    // opens the Options screen. No script is needed.
    static UiPageFile Title(UiSettings ui, string firstScene, IReadOnlyCollection<string>? keys)
    {
        UiElement Row(string name, string key, string plain, UiAction action)
        {
            var b = Button(name, Word(keys, key, plain), 0, Size(0, 16), ui);
            b.Action = action;
            return b;
        }
        return new UiPageFile
        {
            Name = "Title", Cancel = false,
            Root = Canvas(
                Window("Heading", At(0.5, 0, 0, 24, 208, 0), ui, Stack(), Text("GameTitle", Word(keys, "ui.title", "Game Title"), Size(192, 16), ui, UiAlign.Center)),
                Window("Menu", At(0.5, 1, 0, -16, 112, 0), ui, Stack(),
                    Row("Start", "ui.menu.start", "Start", firstScene.Length > 0 ? new UiAction { Kind = UiActionKind.LoadScene, Target = firstScene } : new UiAction()),
                    Row("Continue", "ui.menu.continue", "Continue", new UiAction { Kind = UiActionKind.ContinueGame, Data = 0 }),
                    Row("Options", "ui.menu.options", "Options", new UiAction { Kind = UiActionKind.OpenOptions }))),
        };
    }

    // The pause menu: the Pause action opens a page of this name in any scene, and closes it again.
    static UiPageFile Pause(UiSettings ui, IReadOnlyCollection<string>? keys)
    {
        UiElement Row(string name, string key, string plain, UiActionKind kind)
        {
            var b = Button(name, Word(keys, key, plain), 0, Size(0, 16), ui);
            b.Action = new UiAction { Kind = kind };
            return b;
        }
        return new UiPageFile
        {
            Name = "Pause", Dim = ui.Dim,
            Root = Canvas(Window("Window", At(0.5, 0.5, 0, 0, 128, 0), ui, Stack(),
                Text("Heading", Word(keys, "ui.pause.title", "Paused"), Size(116, 16), ui, UiAlign.Center),
                Row("Resume", "ui.pause.resume", "Resume", UiActionKind.ClosePage),
                Row("Options", "ui.pause.options", "Options", UiActionKind.OpenOptions),
                Row("Quit", "ui.pause.quit", "Quit to title", UiActionKind.QuitToTitle))),
        };
    }

    // The Options screen: Open Options goes to a page of this name. Each row works by its Action: nothing here is hard-wired
    // to a name but the page's own. A Language row hides in a one-language game, key rows past the game's actions hide.
    static UiPageFile Options(UiSettings ui, IReadOnlyCollection<string>? keys)
    {
        UiElement Volume(string name, string key, string plain, UiActionKind kind) => new()
        {
            Name = name, Kind = UiElementKind.Slider, Rect = Size(0, 16), Text = Word(keys, key, plain), TextColor = ui.ScreenInk, Amount = 100,
            Accent = ui.BarFill, DisabledTextColor = ui.Muted, FocusFill = ui.BarTrack, FocusTextColor = ui.ScreenInk, PaddingLeft = 10,
            Action = new UiAction { Kind = kind },
        };
        UiElement Row(string name, string text, UiActionKind kind, int data)
        {
            var b = Button(name, text, data, Size(0, 16), ui);
            b.TextColor = ui.ScreenInk;
            b.Accent = ui.ScreenInk;
            b.Action.Kind = kind;
            return b;
        }
        var heading = Text("Controls", Word(keys, "ui.options.controls", "Controls"), Size(0, 16), ui);
        heading.TextColor = ui.ScreenInk;
        heading.PaddingLeft = 10;
        var rows = new List<UiElement>
        {
            Volume("Music", "ui.options.music", "Music", UiActionKind.MusicVolume),
            Volume("Effects", "ui.options.effects", "Sound effects", UiActionKind.EffectsVolume),
            Row("Language", Word(keys, "ui.options.language", "Language"), UiActionKind.ChangeLanguage, 0),
            heading,
        };
        rows.AddRange(Enumerable.Range(0, 8).Select(i => Row("Key" + i, "", UiActionKind.RemapKey, i)));
        var title = Text("Heading", Word(keys, "ui.options.title", "Options"), UiRect.TopLeft(16, 8, 208, 16), ui, UiAlign.Center);
        title.TextColor = ui.ScreenInk;
        var hint = Text("Hint", Word(keys, "ui.options.hint", "A: change   B: back"), UiRect.TopLeft(16, 140, 208, 16), ui, UiAlign.Center);
        hint.TextColor = ui.Muted;
        return new UiPageFile
        {
            Name = "Options", FirstFocus = "Music",
            Root = Canvas(title, new UiElement
            {
                Name = "Rows", Kind = UiElementKind.ScrollRect, Rect = UiRect.TopLeft(16, 26, 208, 112),
                Layout = new UiLayoutGroup { Kind = UiLayoutKind.Vertical, StretchCross = true },
                Children = rows,
            }, hint),
        };
    }

    // What Choice in a script or the Dialogue Editor shows: Option0 to Option5 get the choices' text, the rest hide. Above the
    // dialogue box of Project Settings.
    static UiPageFile Choice(UiSettings ui)
    {
        var above = ui.Dialogue is { } d ? 160 - d.Y / 8 * 8 + 2 : 50;
        return new UiPageFile
        {
            Name = "Choice", FirstFocus = "Option0",
            Root = Canvas(Window("Window", At(1, 1, -8, -above, 136, 0), ui, Stack(),
                Enumerable.Range(0, 6).Select(i => Button("Option" + i, "Option " + (i + 1), i, Size(0, 16), ui)).ToArray())),
        };
    }

    // Asks for a line of text (English letters, digits and a few signs). UI.Choose gives 1 when it is done; read it with
    // UI.InputLength and UI.InputChar. Set the question with UI.SetText("Prompt", key).
    static UiPageFile TextInput(UiSettings ui) => new()
    {
        Name = "TextInput", Cancel = false, FirstFocus = "Field",
        Root = Canvas(
            new UiElement { Name = "Sheet", Rect = UiRect.Stretch(), Fill = ui.Paper },
            Text("Prompt", "A: type  START: done", At(0.5, 0, 0, 6, 224, 16), ui, UiAlign.Center),
            new UiElement
            {
                Name = "Field", Kind = UiElementKind.InputField, Rect = At(0.5, 0, 0, 24, 120, 20), Fill = ui.Paper, Border = ui.Ink, TextColor = ui.Ink,
                FocusFill = ui.Paper, FocusBorder = ui.BarFill, PaddingLeft = 6, PaddingRight = 6, MaxLength = 12, Action = new UiAction { Data = 1 },
                Keyboard = new UiKeyboard { Fill = ui.Paper, Border = ui.Ink, TextColor = ui.Ink, FocusFill = ui.BarTrack, FocusTextColor = ui.ScreenInk },
            }),
    };

    // A question with Yes (1) and No (0). Set the question with UI.SetText("Question", key).
    static UiPageFile Confirm(UiSettings ui) => new()
    {
        Name = "Confirm", FirstFocus = "No",
        Root = Canvas(Window("Window", At(0.5, 1, 0, -8, 224, 0), ui, Stack(),
            Text("Question", "Are you sure?", Size(208, 32), ui),
            new UiElement
            {
                Name = "Buttons", Rect = Size(208, 16), Layout = new UiLayoutGroup { Kind = UiLayoutKind.Horizontal, Spacing = 8, AlignX = UiAlign.End },
                Children = { Button("Yes", "Yes", 1, Size(64, 16), ui), Button("No", "No", 0, Size(64, 16), ui) },
            })),
    };

    // A line of text with OK (1). Set it with UI.SetText("Body", key).
    static UiPageFile Message(UiSettings ui) => new()
    {
        Name = "Message",
        Root = Canvas(Window("Window", At(0.5, 0.5, 0, 0, 208, 0), ui, Stack(),
            Text("Body", "Message", Size(192, 32), ui),
            Button("Ok", "OK", 1, Size(0, 16), ui))),
    };

    // A short menu; UI.Choose gives the Data of the row.
    static UiPageFile Menu(UiSettings ui) => new()
    {
        Name = "Menu",
        Root = Canvas(Window("Menu", At(1, 0, -6, 6, 100, 0), ui, Stack(),
            Button("Row0", "First", 0, Size(0, 16), ui), Button("Row1", "Second", 1, Size(0, 16), ui),
            Button("Row2", "Third", 2, Size(0, 16), ui), Button("Close", "Close", 3, Size(0, 16), ui))),
    };

    // A list longer than the screen, with a scrollbar; hide the rows you do not need with UI.SetHidden.
    static UiPageFile List(UiSettings ui) => new()
    {
        Name = "List", FirstFocus = "Row0",
        Root = Canvas(
            new UiElement
            {
                Name = "Items", Kind = UiElementKind.ScrollRect, Rect = UiRect.TopLeft(4, 4, 222, 84), Fill = ui.Paper, Border = ui.Ink,
                Layout = new UiLayoutGroup { Kind = UiLayoutKind.Vertical, PaddingLeft = 2, PaddingTop = 2, PaddingRight = 2, PaddingBottom = 2, StretchCross = true },
                Children = Enumerable.Range(0, 8).Select(i => Button("Row" + i, "Row " + (i + 1), i, Size(0, 16), ui)).ToList(),
            },
            new UiElement { Name = "Bar", Kind = UiElementKind.Scrollbar, ScrollTarget = "Items", Rect = UiRect.TopLeft(228, 4, 8, 84), Fill = ui.BarTrack, Accent = ui.BarFill },
            Window("Info", UiRect.TopLeft(4, 92, 232, 44), ui, null, new UiElement
            {
                Name = "Desc", Kind = UiElementKind.Text, Rect = new UiRect { AnchorMinX = 0, AnchorMinY = 0, AnchorMaxX = 1, AnchorMaxY = 1, PivotX = 0, PivotY = 0, X = 8, Y = 4, W = -16, H = -8 },
                TextColor = ui.Ink, TextAlignY = UiAlign.Start,
            })),
    };
}
