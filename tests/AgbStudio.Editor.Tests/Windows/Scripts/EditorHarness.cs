using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Tests.Support;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.Scripts;

/// <summary>A headless editor window with the full Sky Hopper project, and real mouse / keyboard input for E2E tests.</summary>
public sealed class EditorHarness : IDisposable
{
    public Fixture Fx { get; } = new();
    public MainWindow Win { get; }
    public EditorServices S => Fx.Services;
    public string Root { get; }

    public EditorHarness(Action<Fixture>? before = null)
    {
        Root = SkyHopperProject.Open(Fx);
        before?.Invoke(Fx);
        Win = new MainWindow(Fx.Services) { Width = 1440, Height = 1150 };  // 900 + the shared bottom panel, so the page has the room of its Figma frame
        Win.Show();
        Capture.Pump();
    }

    public void Dispose()
    {
        // unsaved test edits would open the Unsaved Changes dialog and keep the window open
        foreach (var d in Fx.Services.Documents.Open) d.IsDirty = false;
        Win.Close();
        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        Fx.Dispose();
    }

    public T Page<T>() where T : Control => Assert.IsType<T>(Win.CurrentPage);

    public void Open(string id, object? args = null) => Phase11ScriptShots.Wait(S.Windows.Open(id, args, source: "test"));

    public T Find<T>(string name) where T : Control
    {
        Capture.Pump();
        return Win.GetVisualDescendants().OfType<T>().First(c => c.Name == name);
    }
    public T? TryFind<T>(string name) where T : Control => Win.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == name);

    public Point Center(Control c) => c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), Win)!.Value;
    public Point At(Control c, Point local) => c.TranslatePoint(local, Win)!.Value;

    public void Click(Control c)
    {
        Capture.Pump();
        ClickAt(Center(c));
    }

    public void ClickAt(Point p, MouseButton button = MouseButton.Left, RawInputModifiers mods = RawInputModifiers.None)
    {
        Win.MouseMove(p, mods);
        Win.MouseDown(p, button, mods);
        Win.MouseUp(p, button, mods);
        Capture.Pump();
    }

    public void DoubleClickAt(Point p)
    {
        ClickAt(p);
        ClickAt(p);
    }

    public void DragAt(Point from, Point to, MouseButton button = MouseButton.Left)
    {
        Win.MouseMove(from);
        Win.MouseDown(from, button);
        Capture.Pump();
        for (var i = 1; i <= 4; i++) Win.MouseMove(new Point(from.X + (to.X - from.X) * i / 4, from.Y + (to.Y - from.Y) * i / 4));
        Win.MouseUp(to, button);
        Capture.Pump();
    }

    public void Key(Key k, RawInputModifiers m = RawInputModifiers.None)
    {
        Win.KeyPressQwerty(Physical(k), m);
        Win.KeyReleaseQwerty(Physical(k), m);
        Capture.Pump();
    }

    public void Type(string text)
    {
        Win.KeyTextInput(text);
        Capture.Pump();
    }

    public static PhysicalKey Physical(Key k) => k switch
    {
        Avalonia.Input.Key.Enter => PhysicalKey.Enter,
        Avalonia.Input.Key.Escape => PhysicalKey.Escape,
        Avalonia.Input.Key.Down => PhysicalKey.ArrowDown,
        Avalonia.Input.Key.Up => PhysicalKey.ArrowUp,
        Avalonia.Input.Key.Left => PhysicalKey.ArrowLeft,
        Avalonia.Input.Key.Right => PhysicalKey.ArrowRight,
        Avalonia.Input.Key.Tab => PhysicalKey.Tab,
        Avalonia.Input.Key.Back => PhysicalKey.Backspace,
        Avalonia.Input.Key.Delete => PhysicalKey.Delete,
        Avalonia.Input.Key.Home => PhysicalKey.Home,
        Avalonia.Input.Key.End => PhysicalKey.End,
        Avalonia.Input.Key.Space => PhysicalKey.Space,
        Avalonia.Input.Key.F2 => PhysicalKey.F2,
        Avalonia.Input.Key.OemPeriod => PhysicalKey.Period,
        >= Avalonia.Input.Key.A and <= Avalonia.Input.Key.Z => PhysicalKey.A + (k - Avalonia.Input.Key.A),
        _ => throw new ArgumentOutOfRangeException(nameof(k)),
    };

    /// <summary>The context menu that is open (menus open as popups in the window's overlay layer).</summary>
    public ContextMenu? OpenContextMenu() => Win.GetVisualDescendants().OfType<ContextMenu>().FirstOrDefault(m => m.IsOpen);

    /// <summary>Runs the command of an item of the open context menu by its label.</summary>
    public void RunMenuItem(string label)
    {
        var menu = OpenContextMenu() ?? throw new InvalidOperationException("no context menu is open");
        var item = menu.Items.OfType<MenuItem>().First(i => (string?)i.Header == label);
        menu.Close();
        item.Command!.Execute(null);
        Capture.Pump();
    }
}
