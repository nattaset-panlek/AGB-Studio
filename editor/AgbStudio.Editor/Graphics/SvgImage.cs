using System.Globalization;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Platform;

namespace AgbStudio.Editor.Graphics;

/// <summary>
/// Minimal renderer for the SVG files exported from Figma (icons, art, brand): path, rect, g with clip-path,
/// clipPath of rects and translate transforms. Nothing else appears in those exports.
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
                    Add(Geometry.Parse((string)c.Attribute("d")!), c, clip);
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

    /// <summary>Draws the image scaled to <paramref name="size"/>; strokes scale with it (art, brand).</summary>
    public void Render(DrawingContext ctx, Size size)
    {
        if (Width <= 0 || Height <= 0) return;
        using var _ = ctx.PushTransform(Matrix.CreateScale(size.Width / Width, size.Height / Height));
        foreach (var s in _shapes) Draw(ctx, s, s.Geometry, s.Fill is { } f ? new ImmutableSolidColorBrush(f) : null, s.Stroke is { } k ? new ImmutableSolidColorBrush(k) : null, s.StrokeWidth);
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
