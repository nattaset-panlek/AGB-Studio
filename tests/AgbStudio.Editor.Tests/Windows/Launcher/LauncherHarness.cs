using System.Diagnostics;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Tests.Shot;
using AgbStudio.Editor.Tests.Support;
using AgbStudio.Editor.Windows.Launcher;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Tests.Windows.Launcher;

/// <summary>A headless editor window with the Launcher fixture, and the clicks and keys the E2E tests use.</summary>
public sealed class LauncherHarness : IDisposable
{
    public Fixture Fx { get; } = new();
    public LauncherService Launcher { get; }
    public MainWindow Win { get; private set; } = null!;
    public EditorServices S => Fx.Services;
    public List<string> Opened { get; } = [];

    public LauncherHarness(string? startPage = "T-P12", bool recent = true)
    {
        Launcher = LauncherFixture.Launcher(Fx);
        Launcher.OpenDocument = Opened.Add;
        Launcher.ProjectsFolder = Path.Combine(Fx.Folder, "GBAProjects");
        Launcher.ReleaseNotesFolder = Path.Combine(Fx.Folder, "notes");
        if (recent) LauncherFixture.AddRecentProjects(Fx);
        if (startPage != null) Show(startPage);
    }

    public void Show(string? startPage)
    {
        Win = new MainWindow(S, startPage) { Width = 1440, Height = 900 };
        Win.Show();
        Capture.Pump();
    }

    public void Dispose()
    {
        Launcher.TutorialHost.Session.Stop();
        Win?.Close();
        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        Fx.Dispose();
    }

    public T Find<T>(string name) where T : Control => Win.GetVisualDescendants().OfType<T>().First(c => c.Name == name);
    public T? TryFind<T>(string name) where T : Control => Win.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == name);
    public IEnumerable<string?> Texts(Visual root) => root.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text);

    Point Center(Control c) => c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), Win)!.Value;

    bool _alt;

    /// <summary>One click. Successive clicks land 12 px apart so two single clicks never count as a double-click.</summary>
    public void Click(Control c, MouseButton button = MouseButton.Left)
    {
        _alt = !_alt;
        var p = Center(c) + new Point(Math.Min(6, c.Bounds.Width / 2 - 1) * (_alt ? 1 : -1), 0);
        Win.MouseMove(p);
        Win.MouseDown(p, button);
        Win.MouseUp(p, button);
        Capture.Pump();
    }

    public void DoubleClick(Control c)
    {
        var p = Center(c);
        Win.MouseMove(p);
        Win.MouseDown(p, MouseButton.Left);
        Win.MouseUp(p, MouseButton.Left);
        Win.MouseDown(p, MouseButton.Left);
        Win.MouseUp(p, MouseButton.Left);
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

    /// <summary>Pumps the UI thread until <paramref name="done"/> holds (background work posts back to it).</summary>
    public static async Task Until(Func<bool> done, int timeoutMs = 60000)
    {
        var sw = Stopwatch.StartNew();
        while (!done())
        {
            if (sw.ElapsedMilliseconds > timeoutMs) throw new TimeoutException("condition not met in time");
            Capture.Pump();
            await Task.Delay(10);
        }
        Capture.Pump();
    }

    public LauncherPage Page => Assert.IsType<LauncherPage>(Win.CurrentPage);
}
