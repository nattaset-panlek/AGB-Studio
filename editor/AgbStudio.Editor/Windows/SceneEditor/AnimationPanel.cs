using System.Globalization;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;

namespace AgbStudio.Editor.Windows.SceneEditor;

/// <summary>
/// Animation panel (T-P06, 860 x 250; bottom panel tab #22): the animations of the selected actor's sprite with frame
/// count and loop, the frames with their ticks, play / step at the game's 59.73 fps, the speed ("6 ticks ≈ 10 fps")
/// and onion skin (the previous frame drawn under the current one).
/// </summary>
[EditorWindow("T-P06", Kind = WindowKind.Panel)]
public sealed class AnimationPanel : DockPanel
{
    readonly EditorServices _s;
    readonly SceneEditorModel _m;
    readonly StackPanel _list = new() { Spacing = 1, Name = "AnimationList" };
    readonly StackPanel _frames = new() { Orientation = Orientation.Horizontal, Spacing = 8, Name = "AnimationFrames" };
    readonly TextBlock _info = UI.Text("", "Label/11", "text/secondary");
    readonly IconButton _play = new() { Glyph = "play", Name = "AnimPlay" };
    readonly IconButton _step = new() { Glyph = "step", Name = "AnimStep" };
    readonly Chip _onion = new() { Content = "Onion skin", Glyph = "layers", Name = "OnionSkin" };
    readonly DispatcherTimer _timer;
    string? _animation;
    int _frame;
    int _tick;

    public AnimationPanel(WindowContext ctx) : this(ctx.Services) { }

    public AnimationPanel(EditorServices s)
    {
        _s = s;
        _m = SceneEditorModel.Of(s);
        Name = "AnimationPanel";
        var left = UI.Frame(_list, null, "border/default", new Thickness(0, 0, 1, 0), new Thickness(6, 8, 6, 6));
        left.Width = 170;
        SetDock(left, Avalonia.Controls.Dock.Left);
        Children.Add(left);
        var bar = new DockPanel { HorizontalSpacing = 10, Height = 28 };
        var buttons = UI.Row(4, Boxed(_play), Boxed(_step));
        SetDock(buttons, Avalonia.Controls.Dock.Left);
        SetDock(_onion, Avalonia.Controls.Dock.Right);
        bar.Children.AddRange([buttons, _onion, _info]);
        var right = UI.Column(11, bar, _frames);
        right.Margin = new Thickness(12, 8, 12, 10);
        Children.Add(right);
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1 / 59.7275) };
        _timer.Tick += (_, _) => Tick();
        _play.Click += (_, _) => Playing = !Playing;
        _step.Click += (_, _) => Step();
        _onion.Click += (_, _) => OnionSkin = !OnionSkin;
        _m.SelectionChanged += Reset;
        _m.Changed += Rebuild;
        DetachedFromVisualTree += (_, _) => { _timer.Stop(); _m.SelectionChanged -= Reset; _m.Changed -= Rebuild; };
        AttachedToVisualTree += (_, _) => { _m.SelectionChanged -= Reset; _m.Changed -= Rebuild; _m.SelectionChanged += Reset; _m.Changed += Rebuild; Rebuild(); };
        Reset();
    }

    static Border Boxed(IconButton b) => UI.Frame(b, "bg/raised", "border/default", null, new Thickness(0), 6);

    ActorData? Actor => _m.SelectedActor;
    string? Sprite => Actor?.Sprite is { Length: > 0 } sp ? sp : null;
    IReadOnlyList<AnimationData> Animations => Sprite != null && _m.Assets is { } a ? a.SpriteMeta(Sprite).Animations : [];
    public AnimationData? Current => Animations.FirstOrDefault(a => a.Name == _animation) ?? Animations.FirstOrDefault();
    public int FrameIndex => _frame;
    bool _onionSkin;

    public bool OnionSkin
    {
        get => _onionSkin;
        set
        {
            _onionSkin = value;
            Rebuild();
        }
    }

    public bool Playing
    {
        get => _timer.IsEnabled;
        set
        {
            if (value) _timer.Start(); else _timer.Stop();
            _play.Glyph = value ? "pause" : "play";
        }
    }

    void Reset()
    {
        _animation = Actor?.Animation is { Length: > 0 } a ? a : null;
        _frame = 0;
        _tick = 0;
        Rebuild();
    }

    public void SelectAnimation(string name)
    {
        _animation = name;
        _frame = 0;
        _tick = 0;
        Rebuild();
    }

    public void SelectFrame(int i)
    {
        _frame = i;
        _tick = 0;
        Rebuild();
    }

    /// <summary>Next frame (ping-pong and non-looping animations stop at the ends like the runtime).</summary>
    public void Step()
    {
        if (Current is not { } a) return;
        var n = Frames(a).Count;
        if (n == 0) return;
        _frame = _frame + 1 < n ? _frame + 1 : a.Loop ? 0 : _frame;
        _tick = 0;
        Rebuild();
    }

    void Tick()
    {
        if (Current is not { } a) return;
        if (++_tick < TicksOf(a, _frame)) return;
        Step();
    }

    List<int> Frames(AnimationData a)
    {
        var count = Sprite != null ? _m.Assets?.Sprite(Sprite)?.FrameCount ?? 0 : 0;
        return a.Frames.Where(f => f < count).ToList();
    }

    static int TicksOf(AnimationData a, int k) => a.FrameTicks != null && k < a.FrameTicks.Count ? a.FrameTicks[k] : a.Ticks;

    void Rebuild()
    {
        _list.Children.Clear();
        _frames.Children.Clear();
        var cur = Current;
        foreach (var a in Animations)
        {
            var n = Frames(a).Count;
            var row = new ListRow { Label = a.Name, Glyph = "film", Meta = $"{n}f" + (a.Loop ? " · loop" : ""), IsSelected = a.Name == cur?.Name, Name = "Anim_" + a.Name };
            var name = a.Name;
            row.Activated += (_, _) => SelectAnimation(name);
            _list.Children.Add(row);
        }
        _play.IsEnabled = _step.IsEnabled = cur != null;
        _onion.Style = OnionSkin ? ChipStyle.Accent : ChipStyle.Default;
        if (cur == null || Sprite == null)
        {
            _info.Text = Actor == null ? "Select an actor with a sprite" : $"{Actor.Name} has no sprite animations";
            return;
        }
        var ticks = cur.Ticks;
        var fps = ticks > 0 ? 60.0 / ticks : 0;
        _info.Text = string.Create(CultureInfo.InvariantCulture, $"{Sprite}.png · {cur.Name} · {ticks} ticks ≈ {fps:0.#} fps");
        var frames = Frames(cur);
        if (_frame >= frames.Count) _frame = 0;
        for (var i = 0; i < frames.Count; i++)
        {
            var thumb = new FrameThumb { Label = $"{i + 1} · {TicksOf(cur, i)}t", IsSelected = i == _frame, Preview = null, Name = $"Frame_{i + 1}" };
            var holder = new Panel { Width = 66, Children = { thumb } };
            if (OnionSkin && i == _frame && frames.Count > 1)
            {
                var prev = PixelArt.View(PixelArt.SpriteBitmap(_m.Assets, Sprite, frames[(i + frames.Count - 1) % frames.Count]), 48, 48);
                prev.Opacity = 0.3;
                prev.VerticalAlignment = VerticalAlignment.Top;
                prev.Margin = new Thickness(0, 9, 0, 0);
                prev.IsHitTestVisible = false;
                holder.Children.Add(prev);
            }
            var img = PixelArt.View(PixelArt.SpriteBitmap(_m.Assets, Sprite, frames[i]), 48, 48);
            img.VerticalAlignment = VerticalAlignment.Top;
            img.Margin = new Thickness(0, 9, 0, 0);
            img.IsHitTestVisible = false;
            holder.Children.Add(img);
            var k = i;
            thumb.Activated += (_, _) => SelectFrame(k);
            _frames.Children.Add(holder);
        }
    }
}
