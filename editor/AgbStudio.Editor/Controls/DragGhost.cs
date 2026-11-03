using AgbStudio.Editor.Graphics;
using AgbStudio.Editor.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;

namespace AgbStudio.Editor.Controls;

/// <summary>
/// The chip beside the pointer that names what is being dragged (panels, tabs, actors, assets). A small window of its
/// own, because a drag can leave the editor window, for instance to float a panel.
/// </summary>
public static class DragGhost
{
    static Window? _window;
    static TextBlock? _hint;

    public static string? Title { get; private set; }
    public static string? Hint { get; private set; }
    public static bool IsVisible => _window != null;

    public static void Show(Visual from, PointerEventArgs e, string glyph, string title)
    {
        Hide();
        Title = title;
        _hint = UI.Text("", "Body/12", "text/secondary");
        _hint.IsVisible = false;
        var chip = UI.Frame(
            UI.Column(2,
                UI.Row(6, new Icon { Glyph = glyph, Size = 14, VerticalAlignment = VerticalAlignment.Center }.Token(Icon.ForegroundProperty, "text/primary"), UI.Text(title, "Body/12 Medium", "text/primary")),
                _hint),
            "bg/panel", "accent/default", new Thickness(1), new Thickness(8, 5), 6);
        _window = new Window
        {
            SystemDecorations = SystemDecorations.None,
            SizeToContent = SizeToContent.WidthAndHeight,
            ShowInTaskbar = false,
            ShowActivated = false,
            Topmost = true,
            CanResize = false,
            IsHitTestVisible = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Content = chip,
            Name = "DragGhost",
        };
        Place(from, e);
        _window.Show();
    }

    public static void Move(Visual from, PointerEventArgs e) => Place(from, e);

    /// <summary>The second line: what a drop here would do.</summary>
    public static void SetHint(string? text, bool refused = false)
    {
        Hint = text;
        if (_hint == null) return;
        _hint.Text = text;
        _hint.IsVisible = !string.IsNullOrEmpty(text);
        _hint.Token(TextBlock.ForegroundProperty, refused ? "status/danger" : "text/secondary");
    }

    public static void Hide()
    {
        Title = null;
        Hint = null;
        _hint = null;
        var w = _window;
        _window = null;
        w?.Close();
    }

    static void Place(Visual from, PointerEventArgs e)
    {
        if (_window == null || TopLevel.GetTopLevel(from) is not { } top) return;
        var p = top.PointToScreen(e.GetPosition(top));
        _window.Position = new PixelPoint(p.X + 14, p.Y + 12);
    }
}
