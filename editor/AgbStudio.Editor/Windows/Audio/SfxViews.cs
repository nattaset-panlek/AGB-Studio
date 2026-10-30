using AgbStudio.Core.Project;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace AgbStudio.Editor.Windows.Audio;

/// <summary>
/// The WAVEFORM box of the SFX Designer: the square wave at the chosen duty (accent line), the 32 samples of the Wave
/// channel, or the noise pattern. With the pencil tool the Wave channel's samples are drawn with the mouse.
/// </summary>
public sealed class WaveformBox : Control
{
    SfxFile _sfx = new();
    bool _drawing;

    public SfxFile Sfx { get => _sfx; set { _sfx = value; InvalidateVisual(); } }
    public bool PencilEnabled { get; set; }
    /// <summary>A wave sample was drawn: (index 0..31, value 0..15).</summary>
    public event Action<int, int>? WaveDrawn;

    public WaveformBox() { Height = 90; ActualThemeVariantChanged += (_, _) => InvalidateVisual(); }

    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        ctx.DrawRectangle(Draw.Brush(this, "bg/panel"), new Pen(Draw.Brush(this, "border/default")), new Rect(0.5, 0.5, w - 1, h - 1), 8, 8);
        var pen = new Pen(Draw.Brush(this, "accent/default"), 2);
        double top = 20, bottom = h - 18, left = 2, right = w - 2 - 74;
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            switch (_sfx.Channel)
            {
                case DmgChannelKind.Wave:
                    var wave = DmgSound.WaveSamples(_sfx);
                    var step = (right - left) / 32;
                    g.BeginFigure(new Point(left, Y(wave[0])), false);
                    for (var i = 0; i < 32; i++)
                    {
                        g.LineTo(new Point(left + i * step, Y(wave[i])));
                        g.LineTo(new Point(left + (i + 1) * step, Y(wave[i])));
                    }
                    break;
                case DmgChannelKind.Noise:
                    var lfsr = 0x7FFF;
                    var n = 96;
                    var dx = (right - left) / n;
                    var prev = true;
                    g.BeginFigure(new Point(left, top), false);
                    for (var i = 0; i < n; i++)
                    {
                        var bit = (lfsr ^ (lfsr >> 1)) & 1;
                        lfsr = (lfsr >> 1) | (bit << 14);
                        if (_sfx.NoiseShort) lfsr = (lfsr & ~0x40) | (bit << 6);
                        var high = (lfsr & 1) == 0;
                        if (high != prev) g.LineTo(new Point(left + i * dx, high ? top : bottom));
                        g.LineTo(new Point(left + (i + 1) * dx, high ? top : bottom));
                        prev = high;
                    }
                    break;
                default:
                    var duty = DmgSound.Duties.MinBy(d => Math.Abs(d - _sfx.Duty));
                    const int periods = 11;
                    var pw = (right - left - 28) / periods;
                    var x = left;
                    g.BeginFigure(new Point(x, top), false);
                    x += 28;
                    g.LineTo(new Point(x, top));
                    for (var i = 0; i < periods; i++)
                    {
                        g.LineTo(new Point(x, bottom));
                        g.LineTo(new Point(x + pw * (1 - duty), bottom));
                        g.LineTo(new Point(x + pw * (1 - duty), top));
                        g.LineTo(new Point(x + pw, top));
                        x += pw;
                    }
                    g.LineTo(new Point(x, bottom));
                    break;
            }
            g.EndFigure(false);
        }
        ctx.DrawGeometry(null, pen, geo);
        double Y(int v) => bottom - (bottom - top) * v / 15.0;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!PencilEnabled || _sfx.Channel != DmgChannelKind.Wave) return;
        _drawing = true;
        e.Pointer.Capture(this);
        DrawAt(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_drawing) DrawAt(e.GetPosition(this));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _drawing = false;
        e.Pointer.Capture(null);
    }

    /// <summary>Sets the wave sample under a point (pencil tool).</summary>
    public void DrawAt(Point p)
    {
        double top = 20, bottom = Bounds.Height - 18, left = 2, right = Bounds.Width - 2 - 74;
        var i = (int)Math.Clamp((p.X - left) / ((right - left) / 32), 0, 31);
        var v = (int)Math.Clamp(Math.Round((bottom - p.Y) / (bottom - top) * 15), 0, 15);
        WaveDrawn?.Invoke(i, v);
    }
}

/// <summary>PITCH AND VOLUME OVER TIME: the pitch (status/success) and the dashed volume (status/warning) of the effect, from the hardware simulation.</summary>
public sealed class PitchVolumeGraph : Control
{
    DmgSound.Trace? _trace;

    public DmgSound.Trace? Trace { get => _trace; set { _trace = value; InvalidateVisual(); } }

    public PitchVolumeGraph() { Height = 90; ActualThemeVariantChanged += (_, _) => InvalidateVisual(); }

    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        ctx.DrawRectangle(Draw.Brush(this, "bg/panel"), new Pen(Draw.Brush(this, "border/default")), new Rect(0.5, 0.5, w - 1, h - 1), 8, 8);
        if (_trace is not { Time.Length: > 1 } t) return;
        double left = 10, right = w - 10, top = 10, bottom = h - 10;
        var end = Math.Max(t.Time[^1], 1e-6);
        double X(double time) => left + (right - left) * time / end;
        var pitches = t.Pitch.Where(p => !double.IsNaN(p) && p > 0).Select(Math.Log2).ToList();
        if (pitches.Count > 0)
        {
            var lo = pitches.Min();
            var hi = pitches.Max();
            var range = hi - lo < 1e-6 ? 1 : hi - lo;
            var flat = hi - lo < 1e-6;
            var geo = new StreamGeometry();
            using (var g = geo.Open())
            {
                var open = false;
                for (var i = 0; i < t.Time.Length; i++)
                {
                    if (double.IsNaN(t.Pitch[i]) || t.Pitch[i] <= 0) { if (open) { g.EndFigure(false); open = false; } continue; }
                    var y = flat ? (top + bottom) / 2 : bottom - (bottom - top) * (Math.Log2(t.Pitch[i]) - lo) / range;
                    if (!open) { g.BeginFigure(new Point(X(t.Time[i]), y), false); open = true; }
                    else g.LineTo(new Point(X(t.Time[i]), y));
                }
                if (open) g.EndFigure(false);
            }
            ctx.DrawGeometry(null, new Pen(Draw.Brush(this, "status/success"), 2, lineCap: PenLineCap.Round), geo);
        }
        var vg = new StreamGeometry();
        using (var g = vg.Open())
        {
            for (var i = 0; i < t.Time.Length; i++)
            {
                var y = bottom - (bottom - top) * t.Volume[i] / 15.0;
                if (i == 0) g.BeginFigure(new Point(X(t.Time[i]), y), false);
                else g.LineTo(new Point(X(t.Time[i]), y));
            }
            g.EndFigure(false);
        }
        ctx.DrawGeometry(null, new Pen(Draw.Brush(this, "status/warning"), 2, new DashStyle([2.5, 2], 0)), vg);
    }
}
