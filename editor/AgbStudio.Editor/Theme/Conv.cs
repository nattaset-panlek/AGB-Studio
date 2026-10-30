using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Input;

namespace AgbStudio.Editor.Theme;

/// <summary>Small converters used by the control themes.</summary>
public static class Conv
{
    public static readonly IValueConverter Text = new FuncValueConverter<object?, string?>(o => o?.ToString());
    public static readonly IValueConverter Gesture = new FuncValueConverter<KeyGesture?, string?>(g => g is null ? null : Shortcuts.Format(g));
    public static readonly IValueConverter NotEmpty = new FuncValueConverter<string?, bool>(s => !string.IsNullOrEmpty(s));
    public static readonly IValueConverter CountText = new FuncValueConverter<int?, string?>(n => n?.ToString(CultureInfo.InvariantCulture));
}

/// <summary>Shortcut text as Figma writes it: "Ctrl+Shift+S", "Ctrl+,", "Del", "Alt+F4".</summary>
public static class Shortcuts
{
    public static string Format(KeyGesture g)
    {
        var parts = new List<string>();
        if (g.KeyModifiers.HasFlag(KeyModifiers.Control)) parts.Add("Ctrl");
        if (g.KeyModifiers.HasFlag(KeyModifiers.Shift)) parts.Add("Shift");
        if (g.KeyModifiers.HasFlag(KeyModifiers.Alt)) parts.Add("Alt");
        parts.Add(KeyName(g.Key));
        return string.Join("+", parts);
    }

    public static string KeyName(Key k) => k switch
    {
        Key.OemComma => ",",
        Key.OemPeriod => ".",
        Key.OemQuestion => "/",
        Key.OemPlus => "=",
        Key.OemMinus => "-",
        Key.Delete => "Del",
        Key.Return => "Enter",
        Key.Escape => "Esc",
        >= Key.D0 and <= Key.D9 => ((int)(k - Key.D0)).ToString(CultureInfo.InvariantCulture),
        _ => k.ToString(),
    };

    /// <summary>Parses the Figma notation back into a gesture ("Ctrl+," -> Ctrl + OemComma).</summary>
    public static KeyGesture Parse(string text)
    {
        var mods = KeyModifiers.None;
        var parts = text.Split('+');
        // "Ctrl++" is not used in Figma; the last part is always the key
        for (var i = 0; i < parts.Length - 1; i++)
            mods |= parts[i] switch { "Ctrl" => KeyModifiers.Control, "Shift" => KeyModifiers.Shift, "Alt" => KeyModifiers.Alt, _ => throw new FormatException(text) };
        var key = parts[^1] switch
        {
            "," => Key.OemComma, "." => Key.OemPeriod, "/" => Key.OemQuestion, "=" => Key.OemPlus, "-" => Key.OemMinus,
            "Del" => Key.Delete, "Enter" => Key.Return, "Esc" => Key.Escape,
            var s when s.Length == 1 && char.IsDigit(s[0]) => Key.D0 + (s[0] - '0'),
            var s => Enum.Parse<Key>(s),
        };
        return new KeyGesture(key, mods);
    }
}
