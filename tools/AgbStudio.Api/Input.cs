namespace GBA;

/// <summary>The ten GBA buttons.</summary>
[Cpp("agb::Key")]
public enum Key
{
    /// <summary>A button.</summary>
    A,
    /// <summary>B button.</summary>
    B,
    /// <summary>Select button.</summary>
    Select,
    /// <summary>Start button.</summary>
    Start,
    /// <summary>D-pad right.</summary>
    Right,
    /// <summary>D-pad left.</summary>
    Left,
    /// <summary>D-pad up.</summary>
    Up,
    /// <summary>D-pad down.</summary>
    Down,
    /// <summary>Right shoulder button.</summary>
    R,
    /// <summary>Left shoulder button.</summary>
    L,
}

/// <summary>Buttons, read once per frame. Player input is ignored while a cutscene runs.</summary>
[Cpp("agb::api::Input")]
public static class Input
{
    /// <summary>True on the frame the button goes down.</summary>
    /// <param name="key">Button to check.</param>
    [Cpp("agb::api::Input::Pressed({0})")]
    public static bool Pressed(Key key) => false;

    /// <summary>True while the button is held.</summary>
    /// <param name="key">Button to check.</param>
    [Cpp("agb::api::Input::Held({0})")]
    public static bool Held(Key key) => false;

    /// <summary>True on the frame the button goes up.</summary>
    /// <param name="key">Button to check.</param>
    [Cpp("agb::api::Input::Released({0})")]
    public static bool Released(Key key) => false;

    /// <summary>-1, 0 or 1 from the D-pad (left / right).</summary>
    [Cpp("agb::api::Input::AxisX()")]
    public static @fixed AxisX => default;

    /// <summary>-1, 0 or 1 from the D-pad (up / down).</summary>
    [Cpp("agb::api::Input::AxisY()")]
    public static @fixed AxisY => default;

    /// <summary>True when any of the ten buttons was pressed this frame.</summary>
    [Cpp("agb::api::Input::AnyKey()")]
    public static bool AnyKey => false;
}
