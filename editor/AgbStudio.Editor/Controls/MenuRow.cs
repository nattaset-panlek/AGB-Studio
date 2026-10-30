using Avalonia;
using Avalonia.Controls.Primitives;

namespace AgbStudio.Editor.Controls;

/// <summary>
/// A Figma "Menu Item" outside a menu (Command Palette results): icon, label, right text (shortcut or folder) and
/// the Hover state as keyboard highlight.
/// </summary>
public class MenuRow : SelectableRow
{
    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<MenuRow, string?>(nameof(Label));
    public static readonly StyledProperty<string?> GlyphProperty = AvaloniaProperty.Register<MenuRow, string?>(nameof(Glyph));
    public static readonly StyledProperty<string?> RightTextProperty = AvaloniaProperty.Register<MenuRow, string?>(nameof(RightText));

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }
    public string? RightText { get => GetValue(RightTextProperty); set => SetValue(RightTextProperty, value); }
}
