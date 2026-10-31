using System.Globalization;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Avalonia.Platform;

namespace AgbStudio.Editor.Graphics;

/// <summary>
/// Minimal renderer for the SVG files exported from Figma (icons, art, brand): path, rect, g with clip-path,
/// clipPath of rects and translate transforms. Nothing else appears in those exports. Paths fill with the SVG fill
/// rule (nonzero unless <c>fill-rule="evenodd"</c>): Avalonia's path markup defaults to even-odd, which would leave the
/// overlap of two sub-paths empty (the path cross of art/genre/topdown).
/// </summary>
public sealed class SvgImage
{
    sealed record Shape(Geometry Geometry, Color? Fill, Color? Stroke, double StrokeWidth, PenLineCap Cap, PenLineJoin Join, Rect? Clip);

    readonly List<Shape> _shapes = [];
    public double Width { get; private set; }
    public double Height { get; private set; }

    public static SvgImage Load(Uri uri)
    {
        using var s = AssetLoader.Open(uri);
        using var r = new StreamReader(s);
        return Parse(r.ReadToEnd());
    }

    public static SvgImage Parse(string xml)
    {
        var doc = XDocument.Parse(xml);
        var root = doc.Root ?? throw new FormatException("Empty SVG");
        var img = new SvgImage { Width = Num(root, "width"), Height = Num(root, "height") };
        var clips = new Dictionary<string, Rect>();
        foreach (var cp in root.Descendants().Where(e => e.Name.LocalName == "clipPath"))
        {
            var r = cp.Elements().First(e => e.Name.LocalName == "rect");
            clips[(string?)cp.Attribute("id") ?? ""] = RectOf(r);
        }
        img.Walk(root, null, clips);
        return img;
    }

    void Walk(XElement e, Rect? clip, Dictionary<string, Rect> clips)
    {
        foreach (var c in e.Elements())
        {
            switch (c.Name.LocalName)
            {
                case "defs": break;
                case "g":
                    var cpRef = (string?)c.Attribute("clip-path");
                    var inner = clip;
                    if (cpRef != null && clips.TryGetValue(cpRef[5..^1], out var r)) inner = clip is { } outer ? outer.Intersect(r) : r;
                    Walk(c, inner, clips);
                    break;
                case "path":
                    // "F1" = nonzero, "F0" = even-odd (Avalonia path markup); SVG's default is nonzero
                    var rule = (string?)c.Attribute("fill-rule") == "evenodd" ? "F0 " : "F1 ";
                    Add(Geometry.Parse(rule + (string)c.Attribute("d")!), c, clip);
                    break;
                case "rect":
                    var rect = RectOf(c);
                    var rx = Num(c, "rx");
                    Add(rx > 0 ? RoundedRect(rect, rx) : new RectangleGeometry(rect), c, clip);
                    break;
            }
        }
    }

    void Add(Geometry g, XElement e, Rect? clip)
    {
        _shapes.Add(new Shape(g, ColorOf((string?)e.Attribute("fill")), ColorOf((string?)e.Attribute("stroke")),
            e.Attribute("stroke-width") is { } sw ? double.Parse(sw.Value, CultureInfo.InvariantCulture) : 1,
            (string?)e.Attribute("stroke-linecap") == "round" ? PenLineCap.Round : PenLineCap.Flat,
            (string?)e.Attribute("stroke-linejoin") == "round" ? PenLineJoin.Round : PenLineJoin.Miter, clip));
    }

    /// <summary>
    /// Draws the image scaled to <paramref name="size"/>; strokes scale with it (art, brand). At a whole-number scale
    /// the shapes are drawn as vectors (pixel edges stay on pixel edges). At any other scale each shape anti-aliased on
    /// its own would let the background through where two shapes meet inside a pixel (a dark 1 px seam between colour
    /// bands), so the image is rasterised instead: drawn without anti-aliasing at several samples per pixel and averaged
    /// down, which blends the two colours of a shared edge by their area as Figma's export does. <paramref name="scaling"/>
    /// is the screen's render scaling, so the raster has one pixel per device pixel.
    /// </summary>
    public void Render(DrawingContext ctx, Size size, double scaling = 1)
    {
        if (Width <= 0 || Height <= 0 || size.Width <= 0 || size.Height <= 0) return;
        double sx = size.Width / Width, sy = size.Height / Height;
        if (scaling <= 0) scaling = 1;
        if (!(IsWhole(sx * scaling) && IsWhole(sy * scaling)))
        {
            var px = new PixelSize((int)Math.Ceiling(size.Width * scaling - 1e-6), (int)Math.Ceiling(size.Height * scaling - 1e-6));
            if (Raster(px, sx * scaling, sy * scaling) is { } bmp)
            {
                ctx.DrawImage(bmp, new Rect(0, 0, px.Width / scaling, px.Height / scaling));
                return;
            }
        }
        using var _ = ctx.PushTransform(Matrix.CreateScale(sx, sy));
        DrawShapes(ctx);
    }

    void DrawShapes(DrawingContext ctx)
    {
        foreach (var s in _shapes) Draw(ctx, s, s.Geometry, s.Fill is { } f ? new ImmutableSolidColorBrush(f) : null, s.Stroke is { } k ? new ImmutableSolidColorBrush(k) : null, s.StrokeWidth);
    }

    static bool IsWhole(double v) => Math.Abs(v - Math.Round(v)) < 1e-6 && v >= 1;

    readonly Dictionary<(int W, int H, double Sx, double Sy), Bitmap?> _rasters = [];

    /// <summary>Samples per pixel per axis: up to 8, fewer for large images (at most about 4 M samples).</summary>
    const double MaxSamples = 4_000_000;

    Bitmap? Raster(PixelSize px, double sx, double sy)
    {
        var key = (px.Width, px.Height, sx, sy);
        if (_rasters.TryGetValue(key, out var cached)) return cached;
        Bitmap? result = null;
        try
        {
            var k = (int)Math.Clamp(Math.Floor(Math.Sqrt(MaxSamples / ((double)px.Width * px.Height))), 2, 8);
            using var big = new RenderTargetBitmap(new PixelSize(px.Width * k, px.Height * k), new Vector(96, 96));
            using (var dc = big.CreateDrawingContext(true))
            using (dc.PushRenderOptions(new RenderOptions { EdgeMode = EdgeMode.Aliased }))
            using (dc.PushTransform(Matrix.CreateScale(sx * k, sy * k)))
                DrawShapes(dc);
            int bw = big.PixelSize.Width, bh = big.PixelSize.Height;
            var src = new byte[bw * bh * 4];
            var handle = System.Runtime.InteropServices.GCHandle.Alloc(src, System.Runtime.InteropServices.GCHandleType.Pinned);
            try { big.CopyPixels(new PixelRect(0, 0, bw, bh), handle.AddrOfPinnedObject(), src.Length, bw * 4); }
            finally { handle.Free(); }
            // box filter in premultiplied space: every channel is the mean of its k x k samples
            var dst = new byte[px.Width * px.Height * 4];
            var n = k * k;
            for (var y = 0; y < px.Height; y++)
                for (var x = 0; x < px.Width; x++)
                    for (var c = 0; c < 4; c++)
                    {
                        var sum = 0;
                        for (var j = 0; j < k; j++)
                        {
                            var row = ((y * k + j) * bw + x * k) * 4 + c;
                            for (var i = 0; i < k; i++) sum += src[row + i * 4];
                        }
                        dst[(y * px.Width + x) * 4 + c] = (byte)((sum + n / 2) / n);
                    }
            var wb = new WriteableBitmap(px, new Vector(96, 96), big.Format ?? PixelFormat.Bgra8888, big.AlphaFormat ?? AlphaFormat.Premul);
            using (var fb = wb.Lock())
                for (var y = 0; y < px.Height; y++)
                    System.Runtime.InteropServices.Marshal.Copy(dst, y * px.Width * 4, fb.Address + y * fb.RowBytes, px.Width * 4);
            result = wb;
        }
        catch (Exception e) when (e is InvalidOperationException or NotSupportedException or PlatformNotSupportedException)
        {
            // no render backend for bitmaps (headless drawing only): the vectors are drawn instead
        }
        _rasters[key] = result;
        return result;
    }

    readonly Dictionary<double, Geometry[]> _scaled = [];

    /// <summary>
    /// Draws an icon at <paramref name="size"/> tinted with <paramref name="brush"/>. Like a resized Figma instance,
    /// the geometry scales but the stroke keeps its 1.33 px weight.
    /// </summary>
    public void RenderIcon(DrawingContext ctx, double size, IBrush? brush)
    {
        if (brush is null) return;
        if (!_scaled.TryGetValue(size, out var geos))
        {
            var k = size / Width;
            geos = _shapes.Select(s => { var g = s.Geometry.Clone(); g.Transform = new MatrixTransform(Matrix.CreateScale(k, k)); return g; }).ToArray();
            _scaled[size] = geos;
        }
        for (var i = 0; i < _shapes.Count; i++)
        {
            var s = _shapes[i];
            Draw(ctx, s, geos[i], s.Fill != null ? brush : null, s.Stroke != null ? brush : null, s.StrokeWidth);
        }
    }

    static void Draw(DrawingContext ctx, Shape s, Geometry g, IBrush? fill, IBrush? stroke, double width)
    {
        var pen = stroke is null ? null : new Pen(stroke, width, lineCap: s.Cap, lineJoin: s.Join);
        if (s.Clip is { } clip)
        {
            using (ctx.PushClip(clip)) ctx.DrawGeometry(fill, pen, g);
        }
        else ctx.DrawGeometry(fill, pen, g);
    }

    static Geometry RoundedRect(Rect r, double rad)
    {
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(new Point(r.X + rad, r.Y), true);
            c.LineTo(new Point(r.Right - rad, r.Y));
            c.ArcTo(new Point(r.Right, r.Y + rad), new Size(rad, rad), 0, false, SweepDirection.Clockwise);
            c.LineTo(new Point(r.Right, r.Bottom - rad));
            c.ArcTo(new Point(r.Right - rad, r.Bottom), new Size(rad, rad), 0, false, SweepDirection.Clockwise);
            c.LineTo(new Point(r.X + rad, r.Bottom));
            c.ArcTo(new Point(r.X, r.Bottom - rad), new Size(rad, rad), 0, false, SweepDirection.Clockwise);
            c.LineTo(new Point(r.X, r.Y + rad));
            c.ArcTo(new Point(r.X + rad, r.Y), new Size(rad, rad), 0, false, SweepDirection.Clockwise);
            c.EndFigure(true);
        }
        return g;
    }

    static Rect RectOf(XElement e)
    {
        double x = Num(e, "x"), y = Num(e, "y");
        if ((string?)e.Attribute("transform") is { } t && t.StartsWith("translate(", StringComparison.Ordinal))
        {
            var p = t[10..^1].Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
            x += double.Parse(p[0], CultureInfo.InvariantCulture);
            y += p.Length > 1 ? double.Parse(p[1], CultureInfo.InvariantCulture) : 0;
        }
        return new Rect(x, y, Num(e, "width"), Num(e, "height"));
    }

    static double Num(XElement e, string name) => e.Attribute(name) is { } a ? double.Parse(a.Value, CultureInfo.InvariantCulture) : 0;

    static Color? ColorOf(string? v) => v switch
    {
        null or "none" => null,
        "white" => Colors.White,
        "black" => Colors.Black,
        _ => Color.Parse(v),
    };
}
