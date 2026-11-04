using System.Text.Json.Serialization;

namespace AgbStudio.Core.Project;

// File formats (D-03): every file is UTF-8 JSON with a "format" version. These classes are the single
// definition used by the editor, the build and the tests. Names follow Project Settings in Figma.

public sealed class ProjectFile
{
    public int Format { get; set; } = 1;
    public string Name { get; set; } = "My Game";
    public GeneralSettings General { get; set; } = new();
    public List<InputAction> Input { get; set; } = InputAction.Defaults();
    public bool AllowRemap { get; set; }
    public SaveSettings Save { get; set; } = new();
    public BuildSettings Build { get; set; } = new();
    public ScriptingSettings Scripting { get; set; } = new();
    public LocalizationSettings Localization { get; set; } = new();
    public AudioSettings Audio { get; set; } = new();
    public UiSettings Ui { get; set; } = new();
    public List<string> Tags { get; set; } = new() { "Player", "Enemy", "Coin" };
    public string LastScene { get; set; } = "";
}

// Project Settings · General
public sealed class GeneralSettings
{
    public string GameTitle { get; set; } = "My Game";
    public string Version { get; set; } = "1.0.0";       // major.minor.patch
    public string RomTitle { get; set; } = "MYGAME";     // 12 characters max
    public string GameCode { get; set; } = "AGBE";       // 4 characters
    public string Maker { get; set; } = "00";            // 2 characters
    public string StartScene { get; set; } = "";
    public string DefaultFont { get; set; } = "";
    public string DefaultLanguage { get; set; } = "en";
    public bool BootSplash { get; set; } = true;
    public int SplashSeconds { get; set; } = 2;
    public bool SkipSplashInDebug { get; set; } = true;
}

// Menu / UI kit (Figma "AGB Studio · Screens", In-game row): the pause menu, the options screen and the
// colours of every in-game menu. Labels come from string keys "ui.*" when the Localization tables have them.
public sealed class UiSettings
{
    public bool PauseMenu { get; set; } = true;      // the "Pause" input action opens it in any scene
    public string TitleScene { get; set; } = "";     // "Quit to title" (empty = General · Start scene)
    public string Font { get; set; } = "";           // empty = General · Default font
    public string Ink { get; set; } = "#1E1A28";     // menu text on paper and on title screens
    public string Muted { get; set; } = "#9AA0AE";
    public string Paper { get; set; } = "#F8F4E8";   // pause box
    public string Screen { get; set; } = "#1E1A28";  // options screen background
    public string ScreenInk { get; set; } = "#E6E8EE";
    public string BarTrack { get; set; } = "#3A3F4E";
    public string BarFill { get; set; } = "#3B4FD8";
    public int Dim { get; set; } = 9;                 // pause overlay: darkens the scene by Dim/16
    public DialogueBoxSettings? Dialogue { get; set; } // null = the default box
    public List<string> RichColors { get; set; } = new() { "#C83030", "#3050C8", "#208838" };  // <c1>..<c3> in strings
}

// Project Settings · Dialogue box: the Say box. X, Y, W and H snap to whole tiles.
public sealed class DialogueBoxSettings
{
    public int X { get; set; } = 8;
    public int Y { get; set; } = 112;
    public int W { get; set; } = 224;
    public int H { get; set; } = 40;
    public int PaddingLeft { get; set; } = 8;
    public int PaddingTop { get; set; } = 4;
    public int PaddingRight { get; set; } = 8;
    public int PaddingBottom { get; set; } = 4;
    public int BorderWidth { get; set; } = 1;
    public string Paper { get; set; } = "#F8F4E8";
    public string Ink { get; set; } = "#1E1A28";
    public string Border { get; set; } = "#1E1A28";
    public string Shadow { get; set; } = "#C8C4B8";
    public int TextSpeed { get; set; } = 2;           // characters a frame
}

// Project Settings · Input: GBA button -> action name -> keyboard / gamepad in the emulator.
public sealed class InputAction
{
    public string Button { get; set; } = "A";
    public string Action { get; set; } = "";
    public string Keyboard { get; set; } = "";
    public string Gamepad { get; set; } = "";

    public static List<InputAction> Defaults() => new()
    {
        new() { Button = "A", Action = "Jump", Keyboard = "Z", Gamepad = "South" },
        new() { Button = "B", Action = "Run", Keyboard = "X", Gamepad = "West" },
        new() { Button = "L", Action = "PrevItem", Keyboard = "A", Gamepad = "LB" },
        new() { Button = "R", Action = "NextItem", Keyboard = "S", Gamepad = "RB" },
        new() { Button = "Start", Action = "Pause", Keyboard = "Enter", Gamepad = "Menu" },
        new() { Button = "Select", Action = "Map", Keyboard = "Backspace", Gamepad = "View" },
        new() { Button = "D-pad", Action = "Move", Keyboard = "Arrows", Gamepad = "Stick / D-pad" },
    };
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SaveType { None, Sram32K, Flash64K, Flash128K, Eeprom8K }

// Project Settings · Save data
public sealed class SaveSettings
{
    public SaveType Type { get; set; } = SaveType.Sram32K;
    public int Slots { get; set; } = 3;
    public bool Autosave { get; set; } = true;   // on every scene change
    public bool Checksum { get; set; } = true;
    // A save made while a [Cutscene] runs also keeps where it is, and loading goes on from there. It takes 320 bytes a slot.
    public bool Cutscenes { get; set; }
    public List<SaveField> Fields { get; set; } = new();
    // Saved Event Script flags that dialogues declare: name -> flag id, written by the build. A flag keeps its id and
    // a new flag never reuses one, so adding a flag does not change what existing save data means.
    public Dictionary<string, int> FlagIds { get; set; } = new();
}

public sealed class SaveField
{
    public string Name { get; set; } = "";
    // int, byte, short, fixed, bool, fixed2, bitset <N>, <Table>[<N>] (e.g. Items[16])
    public string Type { get; set; } = "int";

    public int SizeBytes()
    {
        var t = Type.Trim();
        if (t.StartsWith("bitset", StringComparison.Ordinal)) return (int.Parse(t[6..].Trim()) + 7) / 8;
        var open = t.IndexOf('[');
        if (open > 0) return 2 * int.Parse(t[(open + 1)..t.IndexOf(']')]);
        return t switch
        {
            "bool" or "byte" => 1,
            "short" => 2,
            "fixed2" => 8,
            _ => 4,
        };
    }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BuildConfiguration { Debug, Release }

// Project Settings · Build
public sealed class BuildSettings
{
    public BuildConfiguration Configuration { get; set; } = BuildConfiguration.Debug;
    public string Optimise { get; set; } = "-O2";
    public bool CompressGraphics { get; set; } = true;   // LZ77
    public string Output { get; set; } = "build/";
    public bool StripDebugLog { get; set; } = true;      // Release
    public int WarnRomMegabytes { get; set; } = 16;
    public bool KeepAsserts { get; set; }
    public bool RunAfterBuild { get; set; }
}

// Project Settings · Scripting
public sealed class ScriptingSettings
{
    public string Language { get; set; } = "C# · GBA subset";
    public bool HotReload { get; set; } = true;
    public bool TreatWarningsAsErrors { get; set; }
    public string Generated { get; set; } = "build/gen";
    public bool InlineHints { get; set; } = true;
    public string Namespace { get; set; } = "MyGame";
    public string ShopCurrency { get; set; } = "coins";   // save field Shop.Buy spends (Database tables with a price)
    public Dictionary<string, string> AnalyzerRules { get; set; } = new()
    {
        ["GAE001"] = "Error", ["GAE002"] = "Warning", ["GAE003"] = "Error",
        ["GAE004"] = "Error", ["GAE005"] = "Warning", ["GAE006"] = "Warning",
    };
}

// Project Settings · Localization
public sealed class LocalizationSettings
{
    public string Source { get; set; } = "en";
    public List<LanguageInfo> Languages { get; set; } = new() { new() { Code = "en", Name = "English" } };
    public bool ThaiWrap { get; set; } = true;            // dictionary word wrap
    public bool ShowMissingKeys { get; set; } = true;     // [key] in Debug builds
    public bool FailOnMissing { get; set; }
}

public sealed class LanguageInfo
{
    public string Code { get; set; } = "en";
    public string Name { get; set; } = "English";
    public string Font { get; set; } = "";
    public string Fallback { get; set; } = "";
}

public sealed class AudioSettings
{
    public int Channels { get; set; } = 8;
    public int MixKHz { get; set; } = 21;
}
