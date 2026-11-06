using AgbStudio.Editor.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

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
/// Modals can be moved freely by dragging their header / title bar to prevent obscuring background elements.
/// </summary>
public sealed class OverlayHost : Panel
{
    readonly List<(Panel Layer, Control Content, TaskCompletionSource<object?> Done)> _open = [];

    public IReadOnlyList<Control> Open => _open.Select(o => o.Content).ToList();
    public bool IsOpen => _open.Count > 0;

    public Task<object?> Show(Control content, OverlayPlacement placement = OverlayPlacement.Center, bool lightDismiss = false, Point? at = null)
    {
        var layer = new Panel { Background = Brushes.Transparent, Focusable = true };
        if (at is { } p)
        {
            // at a point (a context menu at the mouse): moved left / up as needed so it stays inside the window
            content.HorizontalAlignment = HorizontalAlignment.Left;
            content.VerticalAlignment = VerticalAlignment.Top;
            content.Measure(Size.Infinity);
            var size = content.DesiredSize;
            var x = Bounds.Width > 0 ? Math.Clamp(p.X, 8, Math.Max(8, Bounds.Width - size.Width - 8)) : p.X;
            var y = Bounds.Height > 0 ? Math.Clamp(p.Y, 8, Math.Max(8, Bounds.Height - size.Height - 8)) : p.Y;
            content.Margin = new Thickness(x, y, 0, 0);
        }
        else
        {
            content.HorizontalAlignment = HorizontalAlignment.Center;
            content.VerticalAlignment = placement == OverlayPlacement.Center ? VerticalAlignment.Center : VerticalAlignment.Top;
            if (placement == OverlayPlacement.TopCenter) content.Margin = new Thickness(0, 72, 0, 0);
        }
        layer.Children.Add(content);
        MakeDraggable(layer, content);
        var done = new TaskCompletionSource<object?>();
        _open.Add((layer, content, done));
        Children.Add(layer);
        if (content is IOverlayContent oc) oc.Closed += r => Close(content, r);
        if (lightDismiss)
            layer.PointerPressed += (_, e) => { if (e.Source == layer) Close(content, null); };
        content.AttachedToVisualTree += (_, _) => FocusFirst(layer, content);
        return done.Task;
    }

    static void MakeDraggable(Panel layer, Control content)
    {
        var transform = new TranslateTransform();
        content.RenderTransform = transform;

        bool isDragging = false;
        Point dragStart = default;
        double startX = 0, startY = 0;
        bool hasMoved = false;

        content.AddHandler(InputElement.PointerPressedEvent, (s, e) =>
        {
            var pt = e.GetCurrentPoint(content);
            if (!pt.Properties.IsLeftButtonPressed) return;

            if (IsDraggableArea(content, e.Source, pt.Position))
            {
                if (e.ClickCount == 2)
                {
                    transform.X = 0;
                    transform.Y = 0;
                    e.Handled = true;
                    return;
                }

                isDragging = true;
                hasMoved = false;
                dragStart = e.GetPosition(layer);
                startX = transform.X;
                startY = transform.Y;
                e.Pointer.Capture(content);
            }
        }, RoutingStrategies.Tunnel, handledEventsToo: true);

        content.AddHandler(InputElement.PointerMovedEvent, (s, e) =>
        {
            if (isDragging)
            {
                var cur = e.GetPosition(layer);
                var dx = cur.X - dragStart.X;
                var dy = cur.Y - dragStart.Y;
                if (Math.Abs(dx) > 1 || Math.Abs(dy) > 1)
                {
                    hasMoved = true;
                }

                var newX = startX + dx;
                var newY = startY + dy;

                if (layer.Bounds.Width > 100 && layer.Bounds.Height > 100 && content.Bounds.Width > 0 && content.Bounds.Height > 0)
                {
                    var visualX = content.Bounds.X + newX;
                    var visualY = content.Bounds.Y + newY;

                    var minVisualX = -(content.Bounds.Width - 60);
                    var maxVisualX = layer.Bounds.Width - 60;
                    var minVisualY = 0.0;
                    var maxVisualY = Math.Max(0.0, layer.Bounds.Height - 40);

                    var clampedX = Math.Clamp(visualX, minVisualX, maxVisualX);
                    var clampedY = Math.Clamp(visualY, minVisualY, maxVisualY);

                    newX = clampedX - content.Bounds.X;
                    newY = clampedY - content.Bounds.Y;
                }

                transform.X = newX;
                transform.Y = newY;
                content.Cursor = new Cursor(StandardCursorType.SizeAll);
                e.Handled = true;
            }
            else
            {
                var pos = e.GetPosition(content);
                if (IsDraggableArea(content, e.Source, pos))
                    content.Cursor = new Cursor(StandardCursorType.SizeAll);
                else
                    content.Cursor = Cursor.Default;
            }
        }, RoutingStrategies.Tunnel, handledEventsToo: true);

        content.AddHandler(InputElement.PointerReleasedEvent, (s, e) =>
        {
            if (isDragging)
            {
                isDragging = false;
                e.Pointer.Capture(null);
                if (hasMoved) e.Handled = true;
                var pos = e.GetPosition(content);
                if (!IsDraggableArea(content, e.Source, pos))
                    content.Cursor = Cursor.Default;
            }
        }, RoutingStrategies.Tunnel, handledEventsToo: true);

        content.AddHandler(InputElement.PointerCaptureLostEvent, (s, e) =>
        {
            isDragging = false;
            content.Cursor = Cursor.Default;
        });

        content.PointerExited += (_, _) =>
        {
            if (!isDragging) content.Cursor = Cursor.Default;
        };
    }

    static bool IsDraggableArea(Control content, object? source, Point posOnContent)
    {
        if (source is Visual visual)
        {
            for (var cur = visual; cur != null && cur != content; cur = cur.GetVisualParent())
            {
                if (cur is Button or TextBox or ComboBox or Slider or ScrollBar or ToggleSwitch)
                    return false;
            }
        }

        bool inHeaderZone = posOnContent.Y >= 0 && posOnContent.Y <= 54;
        bool inDialogHeader = false;

        if (source is Visual v)
        {
            for (var cur = v; cur != null; cur = cur.GetVisualParent())
            {
                if (cur is DialogHeader)
                {
                    inDialogHeader = true;
                    break;
                }
                if (cur == content) break;
            }
        }

        return inHeaderZone || inDialogHeader;
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
