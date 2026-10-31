using System.Globalization;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;
using AgbStudio.Editor.Windows.Palette;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Windows.SceneEditor;

/// <summary>
/// Animation panel (T-P06, 860 x 250; bottom panel tab #22): the animations of the selected actor's sprite with frame
/// count and loop, the frames with their ticks, play / step at the game's 59.73 fps, the speed ("6 ticks ≈ 10 fps")
/// and onion skin (the previous frame drawn under the current one). The speed and the frame order are edited here and
/// saved to the sprite's meta with undo: the mouse wheel over the speed line (or + / − with the panel focused) changes
/// the ticks of the animation, the wheel over a frame changes that frame's ticks, and dragging a frame (or Ctrl + ← / →)
/// moves it.
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
        // Figma: the list without a divider, rows from y 6; Play on a bg/hover square and Step a bare icon; the bar and
        // the frames from x 14 of the right part
        var left = UI.Frame(_list, null, null, null, new Thickness(6, 6, 6, 6));
        left.Width = 170;
        SetDock(left, Avalonia.Controls.Dock.Left);
        Children.Add(left);
        var bar = new DockPanel { HorizontalSpacing = 10, Height = 26 };
        _play.Token(Avalonia.Controls.Primitives.TemplatedControl.BackgroundProperty, "bg/hover");
        _play.CornerRadius = new CornerRadius(6);
        var buttons = UI.Row(8, _play, _step);
        SetDock(buttons, Avalonia.Controls.Dock.Left);
        SetDock(_onion, Avalonia.Controls.Dock.Right);
        bar.Children.AddRange([buttons, _onion, _info]);
        var right = UI.Column(10, bar, _frames);
        right.Margin = new Thickness(14, 8, 12, 10);
        Children.Add(right);
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1 / 59.7275) };
        _timer.Tick += (_, _) => Tick();
        _play.Click += (_, _) => Playing = !Playing;
        _step.Click += (_, _) => Step();
        _onion.Click += (_, _) => OnionSkin = !OnionSkin;
        Focusable = true;
        _info.Name = "AnimationSpeed";
        _info.Background = Avalonia.Media.Brushes.Transparent;   // the whole line takes the wheel
        _info.PointerWheelChanged += (_, e) => { if (ChangeTicks(e.Delta.Y > 0 ? 1 : -1)) e.Handled = true; };
        _frames.AddHandler(PointerPressedEvent, OnFramePressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        _frames.AddHandler(PointerReleasedEvent, OnFrameReleased, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        _frames.AddHandler(PointerWheelChangedEvent, OnFrameWheel, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(PointerPressedEvent, (_, _) => Focus(), RoutingStrategies.Tunnel, handledEventsToo: true);
        _m.SelectionChanged += Reset;
        _m.Changed += Rebuild;
        DetachedFromVisualTree += (_, _) => { _timer.Stop(); _m.SelectionChanged -= Reset; _m.Changed -= Rebuild; };
        AttachedToVisualTree += (_, _) => { _m.SelectionChanged -= Reset; _m.Changed -= Rebuild; _m.SelectionChanged += Reset; _m.Changed += Rebuild; Rebuild(); };
        Reset();
    }

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

    // ---- editing the speed and the frame order (saved to the sprite meta, undoable) ------------------------

    /// <summary>
    /// Applies a change to the current animation in the sprite's meta (assets/sprites/&lt;sprite&gt;.png.meta.json),
    /// writes it and records one undo step. Shares the Sprite Editor's copy of the meta, so both show the change.
    /// </summary>
    public bool EditCurrent(string undoName, Action<AnimationData> change)
    {
        if (Sprite is not { } sprite || Current is not { } cur || _m.Assets?.SpritePath(sprite) is not { } path || _s.Project.Current is null) return false;
        var lib = ArtLibrary.For(_s);
        var name = cur.Name;
        AnimationData? Find() => lib.Meta(path).Animations.FirstOrDefault(a => a.Name == name);
        if (Find() is not { } anim) return false;
        var before = System.Text.Json.JsonSerializer.Serialize(anim, GameProject.Json);
        change(anim);
        var after = System.Text.Json.JsonSerializer.Serialize(anim, GameProject.Json);
        if (before == after) return false;
        void Apply(string json)
        {
            if (Find() is not { } a) return;
            var v = System.Text.Json.JsonSerializer.Deserialize<AnimationData>(json, GameProject.Json)!;
            (a.Frames, a.Ticks, a.FrameTicks, a.Loop, a.PingPong) = (v.Frames, v.Ticks, v.FrameTicks, v.Loop, v.PingPong);
            lib.SaveMeta(path);
            Rebuild();
        }
        lib.SaveMeta(path);
        _s.Undo.Push(new DelegateAction(undoName, () => Apply(before), () => Apply(after)));
        Rebuild();
        return true;
    }

    /// <summary>Ticks per frame of the current animation, 1-60 ("6 ticks ≈ 10 fps"); per-frame ticks move with it.</summary>
    public bool SetTicks(int ticks)
    {
        ticks = Math.Clamp(ticks, 1, 60);
        return EditCurrent("Animation speed", a =>
        {
            var d = ticks - a.Ticks;
            a.Ticks = ticks;
            if (a.FrameTicks != null) a.FrameTicks = a.FrameTicks.Select(t => Math.Clamp(t + d, 1, 60)).ToList();
        });
    }

    bool ChangeTicks(int delta) => Current is { } a && SetTicks(a.Ticks + delta);

    /// <summary>Ticks of one frame (a per-frame override; the others keep the animation's ticks).</summary>
    public bool SetFrameTicks(int frame, int ticks)
    {
        ticks = Math.Clamp(ticks, 1, 60);
        return EditCurrent("Frame speed", a =>
        {
            if (frame < 0 || frame >= a.Frames.Count) return;
            var list = Enumerable.Range(0, a.Frames.Count).Select(i => TicksOf(a, i)).ToList();
            list[frame] = ticks;
            a.FrameTicks = list.All(t => t == a.Ticks) ? null : list;
        });
    }

    /// <summary>Moves a frame to another place in the animation (its ticks move with it); the moved frame stays selected.</summary>
    public bool MoveFrame(int from, int to)
    {
        var ok = EditCurrent("Move frame", a =>
        {
            if (from < 0 || from >= a.Frames.Count || to < 0 || to >= a.Frames.Count || from == to) return;
            var f = a.Frames[from];
            a.Frames.RemoveAt(from);
            a.Frames.Insert(to, f);
            if (a.FrameTicks is { } ft && ft.Count == a.Frames.Count)
            {
                var t = ft[from];
                ft.RemoveAt(from);
                ft.Insert(to, t);
            }
        });
        if (ok) SelectFrame(to);
        return ok;
    }

    int _pressedFrame = -1;
    Point _pressedAt;

    /// <summary>The frame cell under a point of the frame strip (the nearest one left or right of the strip).</summary>
    int FrameAt(Point p)
    {
        var n = _frames.Children.Count;
        if (n == 0) return -1;
        for (var i = 0; i < n; i++)
            if (p.X < _frames.Children[i].Bounds.Right + _frames.Spacing / 2) return i;
        return n - 1;
    }

    void OnFramePressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(_frames).Properties.IsLeftButtonPressed) return;
        _pressedAt = e.GetPosition(_frames);
        _pressedFrame = FrameOf(e.Source);
    }

    void OnFrameReleased(object? sender, PointerReleasedEventArgs e)
    {
        var from = _pressedFrame;
        _pressedFrame = -1;
        var p = e.GetPosition(_frames);
        if (from < 0 || Math.Abs(p.X - _pressedAt.X) < 24) return;
        MoveFrame(from, FrameAt(p));
        e.Handled = true;
    }

    /// <summary>The frame cell an event came from (its source inside the strip).</summary>
    int FrameOf(object? source)
    {
        for (var v = source as Visual; v != null && v != _frames; v = v.GetVisualParent())
            if (v.GetVisualParent() == _frames && v is Control c) return _frames.Children.IndexOf(c);
        return -1;
    }

    void OnFrameWheel(object? sender, PointerWheelEventArgs e)
    {
        var i = FrameOf(e.Source);
        if (i < 0 || Current is not { } a) return;
        if (SetFrameTicks(i, TicksOf(a, i) + (e.Delta.Y > 0 ? 1 : -1))) e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || Current is not { } a) return;
        var ctrl = (e.KeyModifiers & KeyModifiers.Control) != 0;
        e.Handled = e.Key switch
        {
            Key.OemPlus or Key.Add => ChangeTicks(1),
            Key.OemMinus or Key.Subtract => ChangeTicks(-1),
            Key.Left when ctrl => MoveFrame(_frame, _frame - 1),
            Key.Right when ctrl => MoveFrame(_frame, _frame + 1),
            _ => false,
        };
        _ = a;
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
            // the whole cell takes clicks, the wheel and drags (the thumb is transparent around its picture)
            var holder = new Panel { Width = 66, Background = Avalonia.Media.Brushes.Transparent, Children = { thumb } };
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
