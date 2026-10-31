using AgbStudio.Editor.Theme;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace AgbStudio.Editor.Shell;

/// <summary>Helpers for windows built in code: token brushes, Figma text styles, auto-layout rows.</summary>
public static class UI
{
    /// <summary>Binds a brush property to a colour token so it follows the theme.</summary>
    public static T Token<T>(this T c, AvaloniaProperty property, string token) where T : Control
    {
        c.Bind(property, c.GetResourceObservable(token));
        return c;
    }

    public static TextBlock Text(string? text, string style, string token, double? width = null)
    {
        var t = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
        Fig.SetStyle(t, style);
        t.Token(TextBlock.ForegroundProperty, token);
        if (width is { } w) t.Width = w;
        return t;
    }

    /// <summary>
    /// A wrapping text that may need two lines in a row lower than those lines (Figma lets them overflow the row; give
    /// the text negative vertical margins for the room). Avalonia arranges a centred text in exactly its measured height
    /// and its layout then drops the second line, so a text that wraps gets the two lines as its minimum height.
    /// </summary>
    public static void OverflowTwoLines(TextBlock t, double lineHeight = 16)
    {
        t.LayoutUpdated += (_, _) =>
        {
            var lines = t.MinHeight > 0 ? t.TextLayout.TextLines.Count
                : t.DesiredSize.Height - t.Margin.Top - t.Margin.Bottom > lineHeight * 1.5 ? 2 : 1;
            var min = lines > 1 ? 2 * lineHeight : 0;
            if (t.MinHeight != min) t.MinHeight = min;
        };
    }

    /// <summary>A Figma auto-layout frame: horizontal or vertical stack with gap and padding.</summary>
    public static StackPanel Row(double gap, params Control[] children)
    {
        var s = new StackPanel { Orientation = Orientation.Horizontal, Spacing = gap };
        s.Children.AddRange(children);
        return s;
    }

    public static StackPanel Column(double gap, params Control[] children)
    {
        var s = new StackPanel { Orientation = Orientation.Vertical, Spacing = gap };
        s.Children.AddRange(children);
        return s;
    }

    public static Border Frame(Control? child, string? fill = null, string? stroke = null, Thickness? strokes = null, Thickness? pad = null, double radius = 0)
    {
        var b = new Border { Child = child, Padding = pad ?? default, CornerRadius = new CornerRadius(radius), BorderThickness = stroke is null ? default : strokes ?? new Thickness(1) };
        if (fill != null) b.Token(Border.BackgroundProperty, fill);
        if (stroke != null) b.Token(Border.BorderBrushProperty, stroke);
        return b;
    }

    public static T Named<T>(this T c, string name) where T : Control
    {
        c.Name = name;
        return c;
    }
}
