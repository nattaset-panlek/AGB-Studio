using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Tests.Support;
using AgbStudio.Editor.Windows.Palette;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.Sprite;

/// <summary>Headless main window on the Figma fixture project for the Sprite Editor / Palette Manager E2E tests.</summary>
public abstract class ArtWindowHarness : IDisposable
{
    protected Fixture Fx { get; } = new();
    protected ArtLibrary Lib { get; }
    protected MainWindow Win { get; }

    protected ArtWindowHarness()
    {
        Lib = ArtFixture.Open(Fx);
        ArtFixture.AddSceneDocument(Fx);
        Win = new MainWindow(Fx.Services) { Width = 1440, Height = 1150 };  // 900 + the shared bottom panel, so the page has the room of its Figma frame
        Win.Show();
        Capture.Pump();
    }

    public void Dispose()
    {
        Win.Close();
        Fx.Dispose();
        GC.SuppressFinalize(this);
    }

    protected string Sprite(string name) => ArtFixture.SpritePath(Lib, name);

    protected T Find<T>(string name) where T : Control =>
        Win.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == name) ?? throw new InvalidOperationException($"{typeof(T).Name} {name} not found");

    protected IEnumerable<T> All<T>() where T : Control => Win.GetVisualDescendants().OfType<T>();

    protected Point At(Control c, Point? local = null) =>
        c.TranslatePoint(local ?? new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), Win) ?? throw new InvalidOperationException("not in the window");

    protected void Click(Control c, Point? local = null, KeyModifiers mods = KeyModifiers.None, int clicks = 1)
    {
        var p = At(c, local);
        var raw = mods.HasFlag(KeyModifiers.Control) ? RawInputModifiers.Control : RawInputModifiers.None;
        Win.MouseMove(p, raw);
        for (var i = 0; i < clicks; i++)
        {
            Win.MouseDown(p, MouseButton.Left, raw);
            Win.MouseUp(p, MouseButton.Left, raw);
        }
        Capture.Pump();
    }

    protected void Drag(Control c, Point from, Point to)
    {
        Win.MouseMove(At(c, from));
        Win.MouseDown(At(c, from), MouseButton.Left);
        Win.MouseMove(At(c, to));
        Win.MouseUp(At(c, to), MouseButton.Left);
        Capture.Pump();
    }

    protected void Key(PhysicalKey k, RawInputModifiers m = RawInputModifiers.None)
    {
        Win.KeyPressQwerty(k, m);
        Win.KeyReleaseQwerty(k, m);
        Capture.Pump();
    }

    /// <summary>Clicks the box of an Inspector field (dropdown) or a Select field.</summary>
    protected void ClickBox(Control field) => Click(field.GetVisualDescendants().OfType<Button>().First());
}
