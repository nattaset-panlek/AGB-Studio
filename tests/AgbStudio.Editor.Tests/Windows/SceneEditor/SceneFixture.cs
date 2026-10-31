using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Tests.Support;
using AgbStudio.Editor.Windows.SceneEditor;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.SceneEditor;

/// <summary>Editor services with the Sky Hopper project open (Forest_01 in the Scene Editor, as in the Figma frame).</summary>
public sealed class SceneFixture : IDisposable
{
    public Fixture Fx { get; } = new();
    public string Root { get; }
    public EditorServices Services => Fx.Services;
    public SceneEditorModel Model => SceneEditorModel.Of(Services);

    public SceneFixture() => Root = Fx.OpenSkyHopper();

    public void Dispose() => Fx.Dispose();
}

/// <summary>A headless main window on the Sky Hopper project and helpers to click and type in it.</summary>
public sealed class SceneWindow : IDisposable
{
    public SceneFixture Fx { get; } = new();
    public MainWindow Win { get; }
    public EditorServices S => Fx.Services;
    public SceneEditorModel M => Fx.Model;

    public SceneWindow()
    {
        Win = new MainWindow(Fx.Services) { Width = 1440, Height = 900 };
        Win.Show();
        Capture.Pump();
    }

    public T Find<T>(string name) where T : Control => Win.GetVisualDescendants().OfType<T>().First(c => c.Name == name);
    public IEnumerable<T> All<T>() where T : Control => Win.GetVisualDescendants().OfType<T>();

    public Point Center(Control c) => c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), Win)!.Value;

    Point? _last;

    /// <summary>
    /// One click at the control's centre. A click on the spot of the previous one lands 6 px aside: Avalonia counts two
    /// clicks within 500 ms and 4 px as a double-click, and how fast a test runs depends on what ran before it.
    /// </summary>
    public void Click(Control c, MouseButton button = MouseButton.Left, RawInputModifiers mods = RawInputModifiers.None)
    {
        var p = Center(c);
        if (_last is { } l && Math.Abs(l.X - p.X) <= 4 && Math.Abs(l.Y - p.Y) <= 4)
            p += new Point(Math.Max(0, Math.Min(6, c.Bounds.Width / 2 - 1)) * (l.X > p.X ? -1 : 1), 0);
        ClickAt(p, button, mods);
    }

    public void ClickAt(Point p, MouseButton button = MouseButton.Left, RawInputModifiers mods = RawInputModifiers.None)
    {
        Win.MouseMove(p, mods);
        Win.MouseDown(p, button, mods);
        Win.MouseUp(p, button, mods);
        _last = p;
        Capture.Pump();
    }

    public void DoubleClick(Control c)
    {
        var p = Center(c);
        _last = p;
        Win.MouseMove(p);
        Win.MouseDown(p, MouseButton.Left);
        Win.MouseUp(p, MouseButton.Left);
        Win.MouseDown(p, MouseButton.Left);
        Win.MouseUp(p, MouseButton.Left);
        Capture.Pump();
    }

    public void Drag(Point from, Point to, MouseButton button = MouseButton.Left)
    {
        // moves carry the held button, as a real mouse reports it
        var held = button switch { MouseButton.Middle => RawInputModifiers.MiddleMouseButton, MouseButton.Right => RawInputModifiers.RightMouseButton, _ => RawInputModifiers.LeftMouseButton };
        Win.MouseMove(from);
        Win.MouseDown(from, button);
        for (var i = 1; i <= 4; i++) Win.MouseMove(new Point(from.X + (to.X - from.X) * i / 4, from.Y + (to.Y - from.Y) * i / 4), held);
        Win.MouseUp(to, button);
        Capture.Pump();
    }

    public void Key(PhysicalKey k, RawInputModifiers m = RawInputModifiers.None)
    {
        Win.KeyPressQwerty(k, m);
        Win.KeyReleaseQwerty(k, m);
        Capture.Pump();
    }

    public void Type(string text)
    {
        Win.KeyTextInput(text);
        Capture.Pump();
    }

    public SceneWorkspace Workspace => (SceneWorkspace)Win.CurrentPage!;

    public void Dispose()
    {
        Win.Close();
        Fx.Dispose();
    }
}
