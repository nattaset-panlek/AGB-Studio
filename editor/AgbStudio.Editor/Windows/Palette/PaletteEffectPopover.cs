using System.Globalization;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace AgbStudio.Editor.Windows.Palette;

/// <summary>
/// Palette Effect (T-O10, 380 x 372; Palette Manager › Fade / Cycle). Effect Fade / Cycle / Flash, the bank, the colour
/// range (click the first and the last index), speed in frames, cycle direction and a live preview strip that plays
/// the effect frame by frame as the runtime does. Args: the <see cref="EffectKind"/> to show.
/// </summary>
[EditorWindow("T-O10")]
public sealed class PaletteEffectPopover : Border, IOverlayContent
{
    public event Action<object?>? Closed;

    readonly ArtLibrary? _lib;
    readonly PopoverHeader _header = new() { Name = "EffectHeader" };
    readonly Segment[] _effects;
    readonly InspectorField _bank = new() { Kind = FieldKind.Dropdown, Label = "Bank", Name = "EffectBank" };
    readonly TextBlock _range = UI.Text("", "Overline/11", "text/secondary").Named("RangeLabel");
    readonly StackPanel _swatches = new() { Orientation = Orientation.Horizontal, Spacing = 2, Name = "RangeSwatches" };
    readonly InspectorField _speed = new() { Kind = FieldKind.Slider, Label = "Speed", Name = "EffectSpeed", Height = double.NaN, Margin = new Thickness(0, -2) };
    readonly Segment[] _directions;
    readonly Grid _preview = new() { Name = "EffectPreview" };
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1 / 60.0) };
    readonly string _group = Guid.NewGuid().ToString("N");
    int? _anchor;
    bool _updating;

    public EffectKind Kind { get; private set; }
    /// <summary>Frames since the preview started (the runtime advances effects once per frame).</summary>
    public int PreviewFrame { get; private set; }
    public IReadOnlyList<Segment> EffectSegments => _effects;
    public IReadOnlyList<Segment> DirectionSegments => _directions;
    public InspectorField Speed => _speed;
    public InspectorField BankField => _bank;

    public PaletteEffectPopover(WindowContext ctx) : this(ctx.Services.Project.Current is null ? null : ArtLibrary.For(ctx.Services), ctx.Args as EffectKind? ?? EffectKind.Cycle)
    {
        // Preferences › Appearance › Reduce motion: the preview does not play by itself (drag across it to see the effect)
        AutoPlay = !ctx.Services.Preferences.Current.ReduceMotion;
    }

    /// <summary>The preview strip plays the effect on its own (off with Reduce motion).</summary>
    public bool AutoPlay { get; set; } = true;
    public bool PreviewPlaying => _timer.IsEnabled;

    public PaletteEffectPopover(ArtLibrary? lib, EffectKind kind)
    {
        _lib = lib;
        Kind = kind;
        Name = "PaletteEffect";
        Theme = (Avalonia.Styling.ControlTheme?)Application.Current?.FindResource("PopoverSurface");
        Width = 380;
        _header.CloseRequested += (_, _) => Closed?.Invoke(null);

        _effects = new[] { "Fade", "Cycle", "Flash" }.Select(t => new Segment { Content = t, GroupName = _group + "e", Name = "Effect_" + t }).ToArray();
        for (var i = 0; i < 3; i++)
        {
            var k = (EffectKind)i;
            _effects[i].IsCheckedChanged += (_, _) => { if (!_updating && _effects[(int)k].IsChecked == true) SetKind(k); };
        }
        _directions = new[] { "Left", "Right", "Ping-pong" }.Select(t => new Segment { Content = t, GroupName = _group + "d", Name = "Direction_" + t.Replace("-", "") }).ToArray();
        for (var i = 0; i < 3; i++)
        {
            var d = (CycleDirection)i;
            _directions[i].IsCheckedChanged += (_, _) => { if (!_updating && _directions[(int)d].IsChecked == true) Change(e => e.Direction = d); };
        }
        _bank.AddHandler(Button.ClickEvent, (_, e) => { OpenBankMenu(); e.Handled = true; });
        _speed.PropertyChanged += (_, e) =>
        {
            if (e.Property != InspectorField.SliderValueProperty || _updating) return;
            var frames = FramesFromSlider(_speed.SliderValue, MaxFrames(Kind));
            if (frames != Current?.Frames) Change(fx => fx.Frames = frames, "speed");
        };

        var body = UI.Column(14,
            Row("Effect", Group(_effects)),
            _bank,
            UI.Column(14, _range, _swatches),
            _speed,
            Row("Direction", Group(_directions)),
            UI.Frame(_preview, "bg/base", radius: 8).Named("PreviewStrip"));
        // Figma: the strip is a GBA screen (#1B1630) with 80 px frames 5 px apart, the last gap at the right end too
        ((Border)body.Children[^1]).ClipToBounds = true;
        ((Border)body.Children[^1]).Background = PageParts.DataBrush(0xFF1B1630);
        body.Margin = new Thickness(20, 20, 20, 21);
        var dock = new DockPanel();
        DockPanel.SetDock(_header, Dock.Top);
        dock.Children.AddRange([_header, body]);
        Child = dock;

        _timer.Tick += (_, _) => Advance(1);
        // the preview strip is also a slider: press and drag to scrub through the effect, release to play on
        _preview.Background = Avalonia.Media.Brushes.Transparent;
        _preview.Cursor = new Cursor(StandardCursorType.SizeWestEast);
        _preview.PointerPressed += (_, e) => { _timer.Stop(); e.Pointer.Capture(_preview); ScrubPreview(e.GetPosition(_preview).X / Math.Max(1, _preview.Bounds.Width)); e.Handled = true; };
        _preview.PointerMoved += (_, e) => { if (e.Pointer.Captured == _preview) ScrubPreview(e.GetPosition(_preview).X / Math.Max(1, _preview.Bounds.Width)); };
        _preview.PointerReleased += (_, e) => { e.Pointer.Capture(null); if (IsAttachedToVisualTree() && AutoPlay) _timer.Start(); };
        AttachedToVisualTree += (_, _) => { if (_lib != null) _lib.Changed += Refresh; if (AutoPlay) _timer.Start(); };
        DetachedFromVisualTree += (_, _) => { if (_lib != null) _lib.Changed -= Refresh; _timer.Stop(); };
        Refresh();
    }

    static Control Row(string label, Control content)
    {
        var dock = new DockPanel { HorizontalSpacing = 8 };
        var l = UI.Text(label, "Body/12", "text/secondary", 86);
        DockPanel.SetDock(l, Dock.Left);
        content.HorizontalAlignment = HorizontalAlignment.Left;
        dock.Children.AddRange([l, content]);
        return dock;
    }

    static Border Group(Segment[] items) => PageParts.ToolGroup(items);

    public static int MaxFrames(EffectKind k) => k switch { EffectKind.Cycle => 15, EffectKind.Fade => 60, _ => 30 };
    public static int FramesFromSlider(double v, int max) => 1 + (int)Math.Round(Math.Clamp(v, 0, 100) / 100 * (max - 1));
    public static double SliderFromFrames(int frames, int max) => (Math.Clamp(frames, 1, max) - 1) * 100.0 / (max - 1);

    PaletteEffectData? Current => _lib is null ? null : Kind switch
    {
        EffectKind.Fade => _lib.Effects.Fade,
        EffectKind.Cycle => _lib.Effects.Cycle,
        _ => _lib.Effects.Flash,
    };

    PaletteBank? Bank
    {
        get
        {
            if (_lib is null || Current is not { } e) return null;
            return e.Bank >= 0 ? _lib.Find(e.BankKind, e.Bank) : _lib.Of(e.BankKind).FirstOrDefault();
        }
    }

    public string Title => Kind switch
    {
        EffectKind.Cycle => $"Colour cycle · {Current?.Name}",
        EffectKind.Fade => $"Fade · to {PaletteEffects.ColourName(Current?.Color ?? 0)}",
        _ => "Flash on hit",
    };

    public void SetKind(EffectKind k)
    {
        Kind = k;
        _anchor = null;
        PreviewFrame = 0;
        Refresh();
    }

    void Change(Action<PaletteEffectData> change, string? key = null)
    {
        if (_lib is null) return;
        var kind = Kind;
        _lib.SetEffects(fx => change(kind switch { EffectKind.Fade => fx.Fade, EffectKind.Cycle => fx.Cycle, _ => fx.Flash }), key is null ? null : kind + key);
    }

    /// <summary>Clicks on the range swatches: the first click sets the start, the second the end.</summary>
    public void ClickIndex(int i)
    {
        if (Kind != EffectKind.Cycle) return;
        if (_anchor is { } a)
        {
            _anchor = null;
            Change(e => { e.First = Math.Min(a, i); e.Last = Math.Max(a, i); });
        }
        else
        {
            _anchor = i;
            Change(e => { e.First = i; e.Last = i; });
        }
    }

    public void SetBank(BankKind kind, int slot) => Change(e => { e.BankKind = kind; e.Bank = slot; });

    void OpenBankMenu()
    {
        if (_lib is null) return;
        var items = new List<MenuItem>();
        if (Kind != EffectKind.Cycle)
            foreach (var kind in new[] { BankKind.Bg, BankKind.Obj })
            {
                var k = kind;
                items.Add(new MenuItem { Header = kind == BankKind.Bg ? "All BG banks" : "All OBJ banks", Command = new Relay(() => SetBank(k, -1)) });
            }
        foreach (var b in _lib.Banks.OrderBy(b => b.Kind).ThenBy(b => b.Slot))
        {
            var bank = b;
            items.Add(new MenuItem { Header = b.Label, Command = new Relay(() => SetBank(bank.Kind, bank.Slot)) });
        }
        new ContextMenu { ItemsSource = items }.Open(_bank);
    }

    bool IsAttachedToVisualTree() => this.GetVisualRoot() != null;

    /// <summary>Frames until the effect preview repeats: one full cycle, or fade / flash out and back.</summary>
    public int PreviewPeriod
    {
        get
        {
            if (Current is not { } e) return 1;
            var frames = Math.Max(1, e.Frames);
            if (Kind != EffectKind.Cycle) return frames * 2;
            var len = Math.Max(1, e.Last - e.First + 1);
            return e.Direction == CycleDirection.PingPong ? Math.Max(1, 2 * (len - 1)) * frames : len * frames;
        }
    }

    /// <summary>Scrubs the preview to a point of its period (0 = start, 1 = end), as dragging across the strip does.</summary>
    public void ScrubPreview(double ratio)
    {
        PreviewFrame = (int)Math.Floor(Math.Clamp(ratio, 0, 0.999) * PreviewPeriod);
        RefreshPreview();
    }

    /// <summary>Advances the preview by <paramref name="frames"/> frames.</summary>
    public void Advance(int frames)
    {
        PreviewFrame += frames;
        RefreshPreview();
    }

    public void Refresh()
    {
        _updating = true;
        try
        {
            var e = Current;
            _header.Title = Title;
            for (var i = 0; i < 3; i++) _effects[i].IsChecked = (int)Kind == i;
            var bank = Bank;
            _bank.Value = e is null ? "" : e.Bank < 0 ? (e.BankKind == BankKind.Bg ? "All BG banks" : "All OBJ banks") : bank?.Label ?? $"{(e.BankKind == BankKind.Obj ? "OBJ" : "BG")} {e.Bank}";
            var count = bank?.Colors.Count ?? 0;
            var (first, last) = Range(e, count);
            _range.Text = $"Range · index {first} to {last}";
            _swatches.Children.Clear();
            for (var i = 0; i < count; i++)
            {
                var k = i;
                var transparent = bank!.IsTransparentIndex(i);
                var inRange = i >= first && i <= last;
                var sw = new Swatch
                {
                    Width = 24, Height = 24, Name = "Range_" + i,
                    State = inRange && Kind == EffectKind.Cycle ? SwatchState.Selected : transparent ? SwatchState.Transparent : SwatchState.Default,
                    Color = Colour15.ToColor(transparent ? 0 : bank.Colors[i]),
                    Cursor = new Cursor(StandardCursorType.Hand),
                };
                sw.PointerPressed += (_, pe) => { ClickIndex(k); pe.Handled = true; };
                _swatches.Children.Add(sw);
            }
            var max = MaxFrames(Kind);
            var frames = Math.Clamp(e?.Frames ?? 1, 1, max);
            _speed.SliderValue = SliderFromFrames(frames, max);
            _speed.Value = frames.ToString(CultureInfo.InvariantCulture) + (frames == 1 ? "\nframe" : "\nframes");
            for (var i = 0; i < 3; i++)
            {
                _directions[i].IsChecked = e != null && (int)e.Direction == i;
                _directions[i].IsEnabled = Kind == EffectKind.Cycle;
            }
        }
        finally { _updating = false; }
        RefreshPreview();
    }

    (int First, int Last) Range(PaletteEffectData? e, int count)
    {
        if (e is null || count == 0) return (0, 0);
        return Kind == EffectKind.Cycle ? (Math.Clamp(e.First, 0, count - 1), Math.Clamp(e.Last, 0, count - 1)) : (Bank!.LockTransparent ? 1 : 0, count - 1);
    }

    /// <summary>The colours the preview strip shows at <see cref="PreviewFrame"/> (15-bit).</summary>
    public IReadOnlyList<ushort> PreviewColours()
    {
        var e = Current;
        var bank = Bank;
        if (e is null || bank is null || bank.Colors.Count == 0) return [];
        var values = Enumerable.Range(0, bank.Colors.Count).Select(bank.Value).ToArray();
        var (first, last) = Range(e, values.Length);
        switch (Kind)
        {
            case EffectKind.Cycle:
                var cycled = PaletteEffects.Cycle(values, first, last, e.Frames, e.Direction, PreviewFrame);
                return cycled[first..(last + 1)];
            case EffectKind.Fade:
                // fade out over Frames, hold, then start again
                var period = Math.Max(1, e.Frames) * 2;
                var f = PreviewFrame % period;
                return values[first..(last + 1)].Take(4).Select(c => PaletteEffects.Fade(c, (ushort)e.Color, f, e.Frames)).ToArray();
            default:
                var on = PaletteEffects.FlashOn(PreviewFrame % (Math.Max(1, e.Frames) * 2), e.Frames);
                return values[first..(last + 1)].Take(4).Select(c => on ? (ushort)e.Color : c).ToArray();
        }
    }

    void RefreshPreview()
    {
        var colours = PreviewColours();
        _preview.Children.Clear();
        _preview.ColumnDefinitions.Clear();
        for (var i = 0; i < colours.Count; i++)
        {
            _preview.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            var block = new Border { Height = 60, Background = PageParts.DataBrush(Colour15.Argb(colours[i])), Name = "PreviewBlock_" + i };
            Grid.SetColumn(block, _preview.ColumnDefinitions.Count - 1);
            _preview.Children.Add(block);
            _preview.ColumnDefinitions.Add(new ColumnDefinition(5, GridUnitType.Pixel));
        }
        if (colours.Count == 0) _preview.Children.Add(new Border { Height = 60 });
    }
}
