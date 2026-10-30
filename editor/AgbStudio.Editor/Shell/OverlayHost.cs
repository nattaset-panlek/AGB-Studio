using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace AgbStudio.Editor.Shell;

/// <summary>A dialog or popover shown by <see cref="OverlayHost"/>; raise <see cref="Closed"/> with the result.</summary>
public interface IOverlayContent
{
    event Action<object?>? Closed;
}

public enum OverlayPlacement { Center, TopCenter }

/// <summary>
/// Shows Figma dialogs and popovers inside the main window, above the shell. The layer under the content takes the
/// pointer so the shell is modal while it is open (no dimming: Figma shows none); Esc closes the top overlay.
/// </summary>
public sealed class OverlayHost : Panel
{
    readonly List<(Panel Layer, Control Content, TaskCompletionSource<object?> Done)> _open = [];

    public IReadOnlyList<Control> Open => _open.Select(o => o.Content).ToList();
    public bool IsOpen => _open.Count > 0;

    public Task<object?> Show(Control content, OverlayPlacement placement = OverlayPlacement.Center, bool lightDismiss = false)
    {
        var layer = new Panel { Background = Brushes.Transparent, Focusable = true };
        content.HorizontalAlignment = HorizontalAlignment.Center;
        content.VerticalAlignment = placement == OverlayPlacement.Center ? VerticalAlignment.Center : VerticalAlignment.Top;
        if (placement == OverlayPlacement.TopCenter) content.Margin = new Thickness(0, 72, 0, 0);
        layer.Children.Add(content);
        var done = new TaskCompletionSource<object?>();
        _open.Add((layer, content, done));
        Children.Add(layer);
        if (content is IOverlayContent oc) oc.Closed += r => Close(content, r);
        if (lightDismiss)
            layer.PointerPressed += (_, e) => { if (e.Source == layer) Close(content, null); };
        content.AttachedToVisualTree += (_, _) => FocusFirst(layer, content);
        return done.Task;
    }

    // keyboard goes into the overlay: its first text box, else the layer itself (so Esc reaches this host)
    static void FocusFirst(Panel layer, Control content)
    {
        if (content.GetLogicalDescendantsOfType<TextBox>().FirstOrDefault() is { } box) box.Focus();
        else layer.Focus();
    }

    public void Close(Control content, object? result = null)
    {
        var i = _open.FindIndex(o => o.Content == content);
        if (i < 0) return;
        var (layer, _, done) = _open[i];
        _open.RemoveAt(i);
        Children.Remove(layer);
        done.TrySetResult(result);
    }

    public void CloseTop(object? result = null)
    {
        if (_open.Count > 0) Close(_open[^1].Content, result);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape && IsOpen) { CloseTop(); e.Handled = true; }
    }
}

static class LogicalExtensions
{
    public static IEnumerable<T> GetLogicalDescendantsOfType<T>(this Control c) =>
        Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(c).OfType<T>();
}
