using AgbStudio.Editor.Commands;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Graphics;
using AgbStudio.Editor.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Fmt = AgbStudio.Editor.Theme.Shortcuts;

namespace AgbStudio.Editor.Windows.Settings;

/// <summary>What Preferences · Shortcuts passes to Record Shortcut: the command, its name and how to find conflicts.</summary>
public sealed record RecordShortcutRequest(string CommandId, string Title, KeyGesture? Current, Func<KeyGesture, IReadOnlyList<ShortcutConflict>> Conflicts);

/// <summary>
/// Record Shortcut (T-O12, 340 x 264; Preferences › Shortcuts › pencil): the next key combination pressed is shown as
/// keycaps with the conflict it would cause ("Ctrl+G is used by “Go to line”."). Replace closes with the gesture,
/// Cancel / Esc / X with null.
/// </summary>
[EditorWindow("T-O12")]
public sealed class RecordShortcutPopover : Border, IOverlayContent
{
    readonly RecordShortcutRequest _req;
    readonly StackPanel _keys = new() { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Name = "RecordedKeys" };
    readonly TextBlock _conflictText = UI.Text("", "Body/12", "text/secondary");
    readonly Border _conflict;
    readonly AgbButton _replace = new() { Kind = ButtonKind.Primary, Content = "Replace", Name = "Replace", IsEnabled = false };
    TopLevel? _top;

    public event Action<object?>? Closed;
    public KeyGesture? Recorded { get; private set; }
    public IReadOnlyList<ShortcutConflict> Conflicts { get; private set; } = [];
    public string ConflictMessage => _conflictText.Text ?? "";
    public bool ShowsConflict => _conflict.IsVisible;

    public RecordShortcutPopover(WindowContext ctx) : this((RecordShortcutRequest)ctx.Args!) { }

    public RecordShortcutPopover(RecordShortcutRequest req)
    {
        _req = req;
        Name = "RecordShortcut";
        Theme = (Avalonia.Styling.ControlTheme?)Application.Current?.FindResource("PopoverSurface");
        Width = 340;
        var header = new PopoverHeader { Title = "New shortcut · " + req.Title };
        header.CloseRequested += (_, _) => Closed?.Invoke(null);
        var box = UI.Frame(_keys, "bg/base", "accent/default", radius: 8);
        box.Height = 64;
        var warn = new Icon { Glyph = "warn", Size = 16, VerticalAlignment = VerticalAlignment.Center }.Token(Icon.ForegroundProperty, "status/warning");
        _conflict = UI.Frame(UI.Row(6, warn, _conflictText), "bg/base", pad: new Thickness(10, 0), radius: 6);
        _conflict.Height = 34;
        _conflict.IsVisible = false;
        var cancel = new AgbButton { Kind = ButtonKind.Secondary, Content = "Cancel", Name = "Cancel" };
        cancel.Click += (_, _) => Closed?.Invoke(null);
        _replace.Click += (_, _) => { if (Recorded != null) Closed?.Invoke(Recorded); };
        var buttons = UI.Row(8, cancel, _replace);
        buttons.HorizontalAlignment = HorizontalAlignment.Right;
        var body = UI.Column(14, UI.Text("Press the keys you want to use", "Body/12", "text/secondary"), box, _conflict, buttons);
        body.Margin = new Thickness(20, 20, 20, 20);
        Child = UI.Column(0, header, body);
        ShowKeys(req.Current);
        AttachedToVisualTree += (_, _) =>
        {
            _top = TopLevel.GetTopLevel(this);
            _top?.AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Tunnel);
        };
        DetachedFromVisualTree += (_, _) => _top?.RemoveHandler(KeyDownEvent, OnKey);
    }

    void OnKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape || e.Handled) return;
        e.Handled = true;
        Record(e.Key, e.KeyModifiers);
    }

    /// <summary>Records a key press (modifier keys alone are ignored).</summary>
    public void Record(Key key, KeyModifiers mods)
    {
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin or Key.None) return;
        var g = new KeyGesture(key, mods & (KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Alt));
        Recorded = g;
        ShowKeys(g);
        Conflicts = _req.Conflicts(g);
        _conflict.IsVisible = Conflicts.Count > 0;
        _conflictText.Text = Conflicts.Count > 0 ? ShortcutService.ConflictMessage(Conflicts[0]) : "";
        _replace.IsEnabled = true;
    }

    void ShowKeys(KeyGesture? g)
    {
        _keys.Children.Clear();
        if (g is null) return;
        foreach (var part in KeyboardShortcutsDialog.KeyParts(Fmt.Format(g))) _keys.Children.Add(new Keycap { Label = part });
    }
}
