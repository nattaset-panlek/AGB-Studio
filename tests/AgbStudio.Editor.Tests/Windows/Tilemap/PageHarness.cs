using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Tests.Support;
using AgbStudio.Editor.Windows.Tilemap;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.Tilemap;

/// <summary>A headless editor window on the real Sky Hopper project, driven with real clicks and keys (E2E).</summary>
public sealed class PageHarness : IDisposable
{
    public Fixture Fx { get; } = new();
    public MainWindow Win { get; private set; } = null!;
    public string Root { get; private set; } = "";
    public EditorServices S => Fx.Services;

    public static PageHarness ForTilemap(string startPage = "T-P01")
    {
        var h = new PageHarness();
        h.Root = SkyHopperContent.OpenForTilemap(h.Fx);
        h.Show(startPage);
        return h;
    }

    public static PageHarness ForFonts(string startPage = "T-P01")
    {
        var h = new PageHarness();
        h.Root = SkyHopperContent.OpenForFonts(h.Fx);
        h.Show(startPage);
        return h;
    }

    void Show(string startPage)
    {
        Win = new MainWindow(Fx.Services, startPage) { Width = 1440, Height = 900 };
        Win.Show();
        Capture.Pump();
    }

    public void Dispose()
    {
        Win.Close();
        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        Fx.Dispose();
    }

    public T Find<T>(string name) where T : Control => Win.GetVisualDescendants().OfType<T>().First(c => c.Name == name);
    public T? TryFind<T>(string name) where T : Control => Win.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == name);
    public IEnumerable<T> All<T>() where T : Control => Win.GetVisualDescendants().OfType<T>();

    public Point Center(Control c) => c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), Win)!.Value;

    public void Click(Control c, MouseButton button = MouseButton.Left) => ClickAt(Center(c), button);

    public void ClickAt(Point p, MouseButton button = MouseButton.Left, RawInputModifiers mods = RawInputModifiers.None)
    {
        Win.MouseMove(p, mods);
        Win.MouseDown(p, button, mods);
        Win.MouseUp(p, button, mods);
        Capture.Pump();
    }

    public void Drag(Point from, Point to, int steps = 4)
    {
        Win.MouseMove(from);
        Win.MouseDown(from, MouseButton.Left);
        for (var i = 1; i <= steps; i++) Win.MouseMove(new Point(from.X + (to.X - from.X) * i / steps, from.Y + (to.Y - from.Y) * i / steps));
        Win.MouseUp(to, MouseButton.Left);
        Capture.Pump();
    }

    public void Key(PhysicalKey k, RawInputModifiers m = RawInputModifiers.None)
    {
        Win.KeyPressQwerty(k, m);
        Win.KeyReleaseQwerty(k, m);
        Capture.Pump();
    }

    /// <summary>Types into a field's text box and presses Enter (Inspector fields commit on Enter).</summary>
    public void Type(Control field, string text, int box = 0, bool enter = true)
    {
        var tb = field.GetVisualDescendants().OfType<TextBox>().Where(t => t.IsVisible).ElementAt(box);
        tb.Focus();
        tb.SelectAll();
        Capture.Pump();
        Win.KeyTextInput(text);
        if (enter) Key(PhysicalKey.Enter);
        else Pump();
    }

    /// <summary>Clicks a dropdown (Inspector Field / Select) and picks a choice by its text.</summary>
    public void Choose(Control field, string choice)
    {
        var button = field as Button ?? field.GetVisualDescendants().OfType<Button>().First(b => b.IsVisible);
        Click(button);
        var menu = PageChrome.LastChoices ?? throw new InvalidOperationException("No choice list opened");
        var item = menu.Items.OfType<MenuItem>().First(i => (string?)i.Header == choice);
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        menu.Close();
        Capture.Pump();
    }

    public void Pump() => Capture.Pump();

    public IReadOnlyList<string> Texts(Control root) => root.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text ?? "").ToList();
}
