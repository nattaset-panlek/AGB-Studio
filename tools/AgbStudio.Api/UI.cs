namespace GBA;

/// <summary>Image display type matching Unity UI Image.</summary>
public enum ImageType
{
    /// <summary>Simple untiled image.</summary>
    Simple,
    /// <summary>9-sliced bordered image.</summary>
    Sliced,
    /// <summary>Repeated tiled image.</summary>
    Tiled,
    /// <summary>Filled image with partial amount.</summary>
    Filled
}

/// <summary>Fill method for filled UI images matching Unity UI Image.</summary>
public enum FillMethod
{
    /// <summary>Horizontal fill from left/right.</summary>
    Horizontal,
    /// <summary>Vertical fill from bottom/top.</summary>
    Vertical,
    /// <summary>90-degree radial fill.</summary>
    Radial90,
    /// <summary>180-degree radial fill.</summary>
    Radial180,
    /// <summary>360-degree radial fill.</summary>
    Radial360
}

/// <summary>Fill origin direction matching Unity UI Image.</summary>
public enum FillOrigin
{
    /// <summary>Fill origin at left edge.</summary>
    Left = 0,
    /// <summary>Fill origin at right edge.</summary>
    Right = 1,
    /// <summary>Fill origin at bottom edge.</summary>
    Bottom = 0,
    /// <summary>Fill origin at top edge.</summary>
    Top = 1
}

/// <summary>Unity-style UI Image component with fill and slice capabilities.</summary>
public class Image : Behaviour
{
    /// <summary>Creates an Image component with the default settings.</summary>
    public Image() { }

    #region [Variables/Fields]
    /// <summary>Sprite asset name.</summary>
    public string sprite = "";
    /// <summary>Image display type.</summary>
    public ImageType type = ImageType.Simple;
    /// <summary>Fill method for filled images.</summary>
    public FillMethod fillMethod = FillMethod.Horizontal;
    /// <summary>Fill origin direction.</summary>
    public FillOrigin fillOrigin = FillOrigin.Left;
    /// <summary>Fill amount between 0.0 and 1.0.</summary>
    public float fillAmount = 1.0f;
    /// <summary>Whether to preserve aspect ratio.</summary>
    public bool preserveAspect = false;
    /// <summary>Palette color index.</summary>
    public int colorIndex = 1;
    #endregion

    #region [Properties]
    /// <summary>Fill amount between 0.0 and 1.0.</summary>
    public float FillAmount
    {
        get => fillAmount;
        set => SetFillAmount(value);
    }
    #endregion

    #region [Public Methods]
    /// <summary>Sets the fill amount between 0.0 and 1.0.</summary>
    public void SetFillAmount(float amount)
    {
        fillAmount = amount < 0f ? 0f : (amount > 1f ? 1f : amount);
        actor.SetFillRatio((int)(fillAmount * 100), 100);
    }

    /// <summary>Sets the fill ratio using integer numerator and denominator.</summary>
    public void SetFillRatio(int current, int max)
    {
        actor.SetFillRatio(current, max);
    }
    #endregion
}

/// <summary>Panel background and frame style.</summary>
public enum PanelStyle
{
    /// <summary>Solid paper window with frame.</summary>
    SolidWindow,
    /// <summary>Transparent background.</summary>
    Transparent,
    /// <summary>Border frame only.</summary>
    BorderOnly,
    /// <summary>9-slice textured frame.</summary>
    NineSlice
}

/// <summary>A structured UI panel.</summary>
public sealed class Panel
{
    #region [Variables/Fields]
    int _x;
    int _y;
    int _width;
    int _height;
    PanelStyle _style;
    #endregion

    #region [Properties]
    /// <summary>Horizontal position in screen pixels.</summary>
    public int X => _x;
    /// <summary>Vertical position in screen pixels.</summary>
    public int Y => _y;
    /// <summary>Width in screen pixels.</summary>
    public int Width => _width;
    /// <summary>Height in screen pixels.</summary>
    public int Height => _height;
    /// <summary>Panel background style.</summary>
    public PanelStyle Style => _style;
    #endregion

    #region [Public Methods]
    /// <summary>Creates a new Panel.</summary>
    public Panel(int x, int y, int width, int height, PanelStyle style = PanelStyle.SolidWindow)
    {
        _x = x;
        _y = y;
        _width = width;
        _height = height;
        _style = style;
    }

    /// <summary>Sets the screen position.</summary>
    public void SetPosition(int x, int y)
    {
        _x = x;
        _y = y;
    }

    /// <summary>Sets the panel size in pixels.</summary>
    public void SetSize(int width, int height)
    {
        _width = width;
        _height = height;
    }
    #endregion
}

/// <summary>UI Panel and dialogue helpers.</summary>
[Cpp("agb::api::UI")]
public static class UI
{
    #region [Public Methods]
    /// <summary>Creates a UI Canvas root container.</summary>
    public static Actor CreateCanvas(string name = "Canvas")
    {
        return null;
    }

    /// <summary>Creates a UI Panel at the given screen coordinates.</summary>
    [Cpp("agb::api::UI::CreatePanel({0}, {1}, {2}, {3}, static_cast<int>({4}))")]
    [ScriptOp("native:CreatePanel")]
    public static Panel CreatePanel(int x, int y, int width, int height, PanelStyle style = PanelStyle.SolidWindow)
    {
        return new Panel(x, y, width, height, style);
    }

    /// <summary>Creates a UI Panel parented to an existing actor or canvas.</summary>
    public static Panel CreatePanel(int x, int y, int width, int height, PanelStyle style, Actor parent)
    {
        return new Panel(x, y, width, height, style);
    }

    /// <summary>Creates a localized text element parented under a canvas or panel.</summary>
    public static Actor CreateText([Asset(AssetKind.Text)] string textKey, int x = 0, int y = 0, Actor parent = null)
    {
        return null;
    }

    /// <summary>Shows a localized text dialogue box with the specified style.</summary>
    [Cpp("agb::api::UI::ShowDialog({0}, static_cast<int>({1}))")]
    [ScriptOp("native:ShowDialog")]
    public static void ShowDialog([Asset(AssetKind.Text)] string textKey, PanelStyle style = PanelStyle.SolidWindow)
    {
        Text.Show(textKey);
    }

    /// <summary>Opens a UI page (ui/&lt;Name&gt;.uipage) over the scene. The scene waits until the page closes.</summary>
    /// <param name="page">The page's name.</param>
    /// <param name="data">A number the page's script can read with <see cref="Data"/>.</param>
    /// <returns>False when there is no such page or too many are open.</returns>
    [Cpp("agb::api::UI::Open({0}, {1})")]
    public static bool Open(string page, int data = 0) => true;

    /// <summary>Closes the UI page on top.</summary>
    [Cpp("agb::api::UI::Close()")]
    public static void Close() { }

    /// <summary>True while the named UI page is open.</summary>
    [Cpp("agb::api::UI::IsOpen({0})")]
    public static bool IsOpen(string page) => false;

    /// <summary>True while any UI page is open.</summary>
    [Cpp("agb::api::UI::AnyOpen()")]
    public static bool AnyOpen() => false;

    /// <summary>The number the top page was opened with.</summary>
    [Cpp("agb::api::UI::Data()")]
    public static int Data => 0;

    /// <summary>True once when the named Button or Toggle was the last one pressed with A.</summary>
    [Cpp("agb::api::UI::WasChosen({0})")]
    public static bool WasChosen(string element) => false;

    /// <summary>Changes the text of an element on the top page.</summary>
    [Cpp("agb::api::UI::SetText({0}, {1})")]
    public static void SetText(string element, [Asset(AssetKind.Text)] string textKey) { }

    /// <summary>Changes the text of an element on the top page to a string id from a database row (<c>Items.Get(id).name</c>).</summary>
    [Cpp("agb::api::UI::SetText({0}, static_cast<agb::u16>({1}))")]
    public static void SetText(string element, int textId) { }

    /// <summary>Shows a number in an element on the top page: the {0} of its text, or after its text when it has no {0}.</summary>
    /// <param name="element">Element name.</param>
    /// <param name="value">The number.</param>
    /// <param name="digits">Pads with zeros to this many digits (0 = no padding).</param>
    [Cpp("agb::api::UI::SetNumber({0}, {1}, {2})")]
    public static void SetNumber(string element, int value, int digits = 0) { }

    /// <summary>Shows two numbers in an element on the top page: the {0} and {1} of its text, or "a/b" after its text.</summary>
    [Cpp("agb::api::UI::SetFraction({0}, {1}, {2})")]
    public static void SetFraction(string element, int a, int b) { }

    /// <summary>Takes away the numbers set with SetNumber or SetFraction.</summary>
    [Cpp("agb::api::UI::ClearNumber({0})")]
    public static void ClearNumber(string element) { }

    /// <summary>Moves the cursor to a control on the top page, and scrolls its list to show it.</summary>
    [Cpp("agb::api::UI::SetFocus({0})")]
    public static void SetFocus(string element) { }

    /// <summary>True when the cursor is on this control of the top page.</summary>
    [Cpp("agb::api::UI::IsFocused({0})")]
    public static bool IsFocused(string element) => false;

    /// <summary>The Data of the control under the cursor on the top page, -1 when nothing is selected.</summary>
    [Cpp("agb::api::UI::FocusData()")]
    public static int FocusData => -1;

    /// <summary>Lets the cursor reach a control, or skips it and shows its Disabled Text Color. On a panel it covers
    /// everything inside, like a Canvas Group.</summary>
    [Cpp("agb::api::UI::SetInteractable({0}, {1})")]
    public static void SetInteractable(string element, bool on) { }

    /// <summary>Shows another image in an element on the top page; the image must be in an Image Variants list of a page.</summary>
    [Cpp("agb::api::UI::SetImage({0}, {1})")]
    public static void SetImage(string element, string image) { }

    /// <summary>Number of characters typed in the top page's Input Field.</summary>
    [Cpp("agb::api::UI::InputLength()")]
    public static int InputLength => 0;

    /// <summary>A character typed in the top page's Input Field, 0 past the end.</summary>
    [Cpp("agb::api::UI::InputChar({0})")]
    public static int InputChar(int index) => 0;

    /// <summary>Sets a character of the top page's Input Field; 0 ends the text there.</summary>
    [Cpp("agb::api::UI::SetInputChar({0}, {1})")]
    public static void SetInputChar(int index, int ch) { }

    /// <summary>The name of an element in a list row by number: <c>UI.Name("Row", 3, "Name")</c> is "Row3Name". Use the text at
    /// once: the next call replaces it.</summary>
    [Cpp("agb::api::UI::Name({0}, {1}, {2})")]
    public static string Name(string prefix, int index, string suffix = "") => null;

    /// <summary>Opens the built-in Options screen (volumes, language, button remap) over the scene.</summary>
    [Cpp("agb::api::UI::OpenOptions()")]
    public static void OpenOptions() { }

    /// <summary>In a cutscene: waits for the player on the top page and gives the Data of the control pressed with A.
    /// B gives -1. With <paramref name="wakeOnMove"/> 1 it also gives -2 when the cursor or a value moved. The page stays
    /// open, so the cutscene can change it, open a page over it, Say a line over it, and call this again.</summary>
    /// <param name="wakeOnMove">1 to wake when the cursor moves, else 0.</param>
    [ScriptOp("native:UiChoose")]
    public static System.Threading.Tasks.Task<int> Choose(int wakeOnMove = 0) => System.Threading.Tasks.Task.FromResult(0);

    /// <summary>Hides or shows an element, and its children, on the top page.</summary>
    [Cpp("agb::api::UI::SetHidden({0}, {1})")]
    public static void SetHidden(string element, bool hidden) { }

    /// <summary>Sets the value of a Slider or filled Image on the top page, 0 to 100.</summary>
    [Cpp("agb::api::UI::SetAmount({0}, {1})")]
    public static void SetAmount(string element, int amount) { }

    /// <summary>The value of a Slider or filled Image on the top page, 0 to 100.</summary>
    [Cpp("agb::api::UI::GetAmount({0})")]
    public static int GetAmount(string element) => 0;

    /// <summary>Turns a Toggle on the top page on or off.</summary>
    [Cpp("agb::api::UI::SetOn({0}, {1})")]
    public static void SetOn(string element, bool on) { }

    /// <summary>True when a Toggle on the top page is on.</summary>
    [Cpp("agb::api::UI::IsOn({0})")]
    public static bool IsOn(string element) => false;

    /// <summary>Loads and instantiates a UI widget prefab onto the screen (Unreal-style Widget Blueprint / Unity UI Prefab).</summary>
    public static Actor LoadWidget(string prefabName)
    {
        return null;
    }
    #endregion
}
