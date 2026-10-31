using System.Diagnostics;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Graphics;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Windows.SceneEditor;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace AgbStudio.Editor.Windows.LogicGraph;

/// <summary>
/// Logic Graph › Preview (T-P17): the project's scene (the Scene Editor's last scene, else the start scene) as the ROM
/// draws it at the game's start camera, filling the frame like the Figma picture, with its sprite animations playing
/// at game speed (59.73 frames a second). It stays on the first frame while Preferences › Reduce motion is on, and
/// shows the Figma sample picture when the project has no scene.
/// </summary>
public sealed class ScenePreview : Panel
{
    const double Fps = 59.7275;
    readonly EditorServices _s;
    readonly ArtImage _still = new() { Source = "art/scene/day", Width = 294, Height = 194 };
    // the GBA screen fills the frame (uniform to fill, centred), pixels kept sharp
    readonly Image _view = new() { Stretch = Stretch.UniformToFill, Name = "PreviewScreen" };
    readonly Stopwatch _clock = new();
    readonly DispatcherTimer _timer;
    SceneAssets? _assets;
    SceneFile? _scene;
    MapFile? _map;

    public ScenePreview(EditorServices s)
    {
        _s = s;
        Name = "ScenePreview";
        ClipToBounds = true;
        RenderOptions.SetBitmapInterpolationMode(_view, BitmapInterpolationMode.None);
        Children.Add(_view);
        Children.Add(_still);
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1 / 15.0) };
        _timer.Tick += (_, _) => SetTick((long)(_clock.Elapsed.TotalSeconds * Fps));
        AttachedToVisualTree += (_, _) => { Load(); Start(); };
        DetachedFromVisualTree += (_, _) => _timer.Stop();
        s.Preferences.Saved += Start;
    }

    /// <summary>Game frames since the preview started; the sprites show their animation frame at this time.</summary>
    public long Tick { get; private set; }
    public string? SceneName => _scene?.Name;
    /// <summary>The picture shown now (null while the Figma sample picture stands in).</summary>
    public ScreenFrame? Frame { get; private set; }
    public bool Playing => _timer.IsEnabled;

    void Start()
    {
        if (_scene != null && VisualRoot != null && !_s.Preferences.Current.ReduceMotion)
        {
            _clock.Restart();
            _timer.Start();
        }
        else
        {
            _timer.Stop();
            SetTick(0);
        }
    }

    void Load()
    {
        _scene = null;
        if (_s.Project.Current is not { } p) { Show(); return; }
        var files = p.SceneFiles().ToList();
        var file = new[] { p.Settings.LastScene, p.Settings.General.StartScene }
            .Select(n => files.FirstOrDefault(f => GameProject.AssetName(f) == n)).FirstOrDefault(f => f != null) ?? files.FirstOrDefault();
        if (file == null) { Show(); return; }
        try
        {
            _assets = new SceneAssets(p);
            _scene = GameProject.Load<SceneFile>(file);
            _map = _assets.Map(_scene);
        }
        catch (Exception e) when (e is IOException or System.Text.Json.JsonException) { _scene = null; }
        Render(Tick);
    }

    /// <summary>Shows the scene at a time (the timer calls it; tests step it).</summary>
    public void SetTick(long tick)
    {
        if (tick == Tick && Frame != null) return;
        Tick = tick;
        Render(tick);
    }

    void Render(long tick)
    {
        if (_scene == null || _assets == null) { Show(); return; }
        var (x, y) = GbaScreenRenderer.StartCamera(_scene, _map);
        Frame = GbaScreenRenderer.Render(_assets, _scene, _map, x, y, ScreenLayers.All, tick);
        _view.Source = PixelArt.Bitmap(Frame.ToArgb(), ScreenFrame.Width, ScreenFrame.Height);
        Show();
    }

    void Show()
    {
        _still.IsVisible = _scene == null;
        _view.IsVisible = _scene != null;
    }
}
