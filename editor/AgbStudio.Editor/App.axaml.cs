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
            // G-ERR.06: an unexpected error saves the changed documents and writes a crash log
            new AgbStudio.Editor.Services.CrashGuard(Services).Install();
            // P10.15: the last project (Preferences › Editor "Open last project on start"), else the Launcher
            desktop.MainWindow = new MainWindow(Services, AgbStudio.Editor.Services.Startup.Choose(Services));
        }
        base.OnFrameworkInitializationCompleted();
    }
}
