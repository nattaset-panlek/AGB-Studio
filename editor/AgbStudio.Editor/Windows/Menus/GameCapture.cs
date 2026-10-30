using System.Globalization;
using AgbStudio.Core;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using AgbStudio.Emu;

namespace AgbStudio.Editor.Windows.Menus;

/// <summary>One frame of the Game view: 240 x 160 pixels as 0xAARRGGBB and the emulator's frame number.</summary>
public readonly record struct GameFrame(uint[] Pixels, long Number);

/// <summary>
/// Tools › Screenshot / GIF (F12, P11.66): the first press saves the Game view as a PNG and starts recording a GIF of
/// the running game; the next press (or 30 s) stops and saves the GIF. Frames come from the Play Mode session when one is
/// registered (<c>Services.Set(playSession)</c>), else from the editor's emulator core.
/// </summary>
public sealed class GameCapture
{
    public const int MaxSeconds = 30;
    readonly IRepeatingTimer _timer;
    GifWriter? _gif;
    long _lastFrame = -1;
    DateTime _lastTime;
    DateTime _started;

    public Func<GameFrame?> Source { get; init; }
    public Func<DateTime> Clock { get; init; } = () => DateTime.Now;
    /// <summary>Where captures are written (default Pictures\AGB Studio).</summary>
    public string Folder { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "AGB Studio");
    public Func<string> BaseName { get; init; } = () => "game";
    public bool Recording => _gif != null;
    public int RecordedFrames => _gif?.FrameCount ?? 0;
    public event Action? StateChanged;

    public GameCapture(Func<GameFrame?> source, IRepeatingTimer? timer = null)
    {
        Source = source;
        _timer = timer ?? new DispatcherRepeatingTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(1000.0 / 30);
        _timer.Tick += Sample;
    }

    public bool CanCapture => Recording || Source() != null;

    /// <summary>The capture service of an editor: frames from Play Mode or the emulator core, file names from the project.</summary>
    public static GameCapture Of(EditorServices s)
    {
        if (s.GetService(typeof(GameCapture)) is GameCapture c) return c;
        c = new GameCapture(() => EditorFrame(s)) { BaseName = () => s.Project.Current is { } p ? GameProject.FileName(p.Settings.Name).ToLowerInvariant() : "game" };
        s.Set(c);
        return c;
    }

    /// <summary>The current Game view frame of an editor, or null when no game runs.</summary>
    public static GameFrame? EditorFrame(EditorServices s)
    {
        if (s.GetService(typeof(PlaySession)) is PlaySession { Running: true } ps && ps.Frame > 0) return new GameFrame(ps.Core.Frame, ps.Frame);
        if (!s.Emulator.IsLoaded) return null;
        var core = s.Emulator.Acquire();
        return core.FrameCount > 0 ? new GameFrame(core.Frame, core.FrameCount) : null;
    }

    string NewPath(string ext)
    {
        Directory.CreateDirectory(Folder);
        return Path.Combine(Folder, $"{BaseName()}_{Clock().ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)}{ext}");
    }

    /// <summary>F12: saves a PNG and starts the GIF, or stops and saves the GIF. Returns the file written, or null when no game runs.</summary>
    public string? Toggle() => Recording ? Stop() : Start();

    string? Start()
    {
        if (Source() is not { } f) return null;
        var png = NewPath(".png");
        Png.WriteArgb(png, (uint[])f.Pixels.Clone(), LibretroCore.Width, LibretroCore.Height);
        _gif = new GifWriter(LibretroCore.Width, LibretroCore.Height);
        _started = _lastTime = Clock();
        _lastFrame = -1;
        Sample();
        _timer.Start();
        StateChanged?.Invoke();
        LastPng = png;
        return png;
    }

    string? Stop()
    {
        _timer.Stop();
        var gif = _gif;
        _gif = null;
        StateChanged?.Invoke();
        if (gif is null || gif.FrameCount == 0) return null;
        var path = NewPath(".gif");
        File.WriteAllBytes(path, gif.Encode());
        LastGif = path;
        LastGifFrames = gif.FrameCount;
        return path;
    }

    public string? LastPng { get; private set; }
    public string? LastGif { get; private set; }
    public int LastGifFrames { get; private set; }
    /// <summary>Set when a recording stopped by itself at <see cref="MaxSeconds"/>.</summary>
    public event Action<string>? AutoStopped;

    /// <summary>Adds the current frame when the game advanced (timer tick, 30 per second).</summary>
    public void Sample()
    {
        if (_gif is null) return;
        var now = Clock();
        if ((now - _started).TotalSeconds >= MaxSeconds)
        {
            if (Stop() is { } p) AutoStopped?.Invoke(p);
            return;
        }
        if (Source() is not { } f || f.Number == _lastFrame) return;
        var delay = _lastFrame < 0 ? 3 : Math.Clamp((int)Math.Round((now - _lastTime).TotalMilliseconds / 10), 2, 100);
        _gif.AddFrame(f.Pixels, delay);
        _lastFrame = f.Number;
        _lastTime = now;
    }
}
