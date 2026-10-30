using System.Runtime.InteropServices;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace AgbStudio.Editor.Windows.Debugger;

/// <summary>Helpers shared by the Debugger views.</summary>
public static class DebugUI
{
    /// <summary>A token brush, or a brush made from a colour literal of Theme/Literals.axaml (lit/accent-pink ...).</summary>
    public static IBrush? Brush(Control c, string key)
    {
        if (!c.TryFindResource(key, c.ActualThemeVariant, out var v) && Application.Current?.TryFindResource(key, c.ActualThemeVariant, out v) != true) return null;
        return v switch { IBrush b => b, Color col => new SolidColorBrush(col), _ => null };
    }

    /// <summary>A Section Header with rows under it, in a block with a bottom border (Inspector-style right panel section).</summary>
    public static Border Section(string title, string glyph, Control content, string name, Thickness? pad = null, double gap = 6)
    {
        var header = new SectionHeader { Title = title, Glyph = glyph, Name = "Section_" + name };
        var col = UI.Column(gap, header, content);
        header.PropertyChanged += (_, e) => { if (e.Property == SectionHeader.IsOpenProperty) content.IsVisible = header.IsOpen; };
        return UI.Frame(col, null, "border/default", new Thickness(0, 0, 0, 1), pad ?? new Thickness(12, 8, 12, 10)).Named(name);
    }

    public static TextBlock Caption(string text) => UI.Text(text, "Caption/10", "text/muted");

    /// <summary>Fills a shape with a colour token (follows the theme) or a colour literal (lit/*).</summary>
    public static void Fill(Avalonia.Controls.Shapes.Shape s, string key)
    {
        if (key.StartsWith("lit/", StringComparison.Ordinal) && Application.Current?.TryFindResource(key, out var v) == true && v is Color c)
            s.Fill = new SolidColorBrush(c);
        else s.Token(Avalonia.Controls.Shapes.Shape.FillProperty, key);
    }
}

/// <summary>Pixels of the game (VRAM tiles, sprites) drawn at an integer scale without smoothing; game data, not UI colours.</summary>
public sealed class PixelImage : Control
{
    WriteableBitmap? _bitmap;
    int _w, _h;

    public int Scale { get; set; } = 2;
    /// <summary>The pixels shown (ARGB, row-major) and their size.</summary>
    public uint[] Pixels { get; private set; } = [];
    public int PixelWidth => _w;

    public PixelImage() => RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);

    public void SetPixels(uint[] argb, int w, int h)
    {
        Pixels = argb;
        if (w <= 0 || h <= 0) { _bitmap = null; InvalidateMeasure(); InvalidateVisual(); return; }
        if (_bitmap == null || _w != w || _h != h)
        {
            _bitmap = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
            _w = w;
            _h = h;
            InvalidateMeasure();
        }
        using (var fb = _bitmap.Lock())
        {
            var row = new int[w];
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var c = argb[y * w + x];
                    row[x] = (c >> 24) == 0 ? 0 : unchecked((int)c);
                }
                Marshal.Copy(row, 0, fb.Address + y * fb.RowBytes, w);
            }
        }
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize) => _bitmap == null ? default : new Size(_w * Scale, _h * Scale);

    public override void Render(DrawingContext context)
    {
        if (_bitmap != null) context.DrawImage(_bitmap, new Rect(0, 0, _w, _h), new Rect(0, 0, _w * Scale, _h * Scale));
    }
}

/// <summary>Frame time bars of the last 120 frames against the 16.7 ms budget line (Profiler).</summary>
public sealed class FrameGraph : Control
{
    public const double BudgetMs = 1000 / (16777216.0 / 280896.0);
    public double[] Frames { get; private set; } = [];

    public void SetFrames(double[] ms)
    {
        Frames = ms;
        InvalidateVisual();
    }

    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        ctx.DrawRectangle(DebugUI.Brush(this, "bg/base"), new Pen(DebugUI.Brush(this, "border/default"), 1), new Rect(0.5, 0.5, w - 1, h - 1), 6, 6);
        var top = 4.0;
        var bottom = h - 2;
        var scale = (bottom - top - 16) / BudgetMs;  // the budget line sits 20 px under the top edge
        var ok = DebugUI.Brush(this, "status/success");
        var over = DebugUI.Brush(this, "status/danger");
        if (Frames.Length > 0)
        {
            var bw = (w - 4) / Frames.Length;
            for (var i = 0; i < Frames.Length; i++)
            {
                var bh = Math.Min(bottom - top, Frames[i] * scale);
                if (bh <= 0) continue;
                ctx.FillRectangle(Frames[i] > BudgetMs ? over! : ok!, new Rect(2 + i * bw, bottom - bh, Math.Max(1, bw - 1), bh));
            }
        }
        var y = Math.Round(bottom - BudgetMs * scale) + 0.5;
        ctx.DrawLine(new Pen(over, 1), new Point(1, y), new Point(w - 1, y));
    }
}

/// <summary>The memory map bar: one coloured segment per category, the rest free (Debugger · Memory).</summary>
public sealed class MemoryBar : Control
{
    IReadOnlyList<(long Bytes, string Token)> _segments = [];
    long _total = 1;

    public void Set(IReadOnlyList<(long Bytes, string Token)> segments, long total)
    {
        _segments = segments;
        _total = Math.Max(1, total);
        InvalidateVisual();
    }

    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        using (ctx.PushClip(new RoundedRect(new Rect(0, 0, w, h), 4)))
        {
            ctx.FillRectangle(DebugUI.Brush(this, "bg/hover")!, new Rect(0, 0, w, h));
            var x = 0.0;
            foreach (var (bytes, token) in _segments)
            {
                var sw = Math.Round(w * bytes / _total);
                if (sw <= 0) continue;
                ctx.FillRectangle(DebugUI.Brush(this, token)!, new Rect(x, 0, sw, h));
                x += sw;
            }
        }
    }
}
