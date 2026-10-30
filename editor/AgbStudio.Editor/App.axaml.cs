using AgbStudio.Editor.Shell;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace AgbStudio.Editor;

public partial class App : Application
{
    /// <summary>Services of the running editor (null in tests, which build their own).</summary>
    public EditorServices? Services { get; private set; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.Args?.Contains("--gallery") == true)
            {
                desktop.MainWindow = new Gallery.ComponentGallery();
                base.OnFrameworkInitializationCompleted();
                return;
            }
            Services = EditorServices.CreateDefault(this);
            var prefs = Services.Preferences.Current;
            // P10.15: open the last project on start, if set and still there
            if (prefs.OpenLastProjectOnStart && prefs.LastProject is { } last && File.Exists(last))
            {
                try { Services.Project.Open(last); }
                catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException) { }
            }
            // The Launcher (T-P12) is the start page when no project is open; until it is built the Scene Editor shell shows.
            var start = Services.Project.Current is null && Services.Windows.IsRegistered("T-P12") ? "T-P12" : "T-P01";
            desktop.MainWindow = new MainWindow(Services, start);
        }
        base.OnFrameworkInitializationCompleted();
    }
}
