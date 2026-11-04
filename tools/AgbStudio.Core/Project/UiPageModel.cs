using System.Text.Json.Serialization;

namespace AgbStudio.Core.Project;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UiElementKind { Panel, Image, Text, Button, Toggle, Slider, ScrollRect, Scrollbar, Dropdown, InputField }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UiImageMode { Simple, Sliced, Tiled, FilledHorizontal, FilledVertical }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UiAlign { Start, Center, End }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UiLayoutKind { None, Horizontal, Vertical, Grid }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UiActionKind { None, OpenPage, ClosePage, LoadScene }

// ui/<Name>.uipage: a screen of UI made once and opened by scripts or buttons (UI.Open("Bag", data)). It is apart from
// the scene: a page is drawn on the text layer over whatever scene is showing, and the scene waits while it is open.
public sealed class UiPageFile
{
    public int Format { get; set; } = 1;
    public string Name { get; set; } = "";
    public string Tag { get; set; } = "";
    public bool Cancel { get; set; } = true;       // B closes the page
    public bool WrapFocus { get; set; } = true;    // the D-pad wraps from the last control to the first
    public string FirstFocus { get; set; } = "";   // element name, empty = the first control
    public bool ScenePlays { get; set; }           // the scene keeps running under the page (a HUD); else it waits
    public int Dim { get; set; }                   // darkens the scene under the page by Dim/16
    public UiElement Root { get; set; } = new() { Name = "Canvas", Rect = UiRect.Stretch() };
}

// Rect Transform. Anchors and pivot run 0..1 from the top left of the parent, because screen y grows down here.
public sealed class UiRect
{
    public double AnchorMinX { get; set; } = 0.5;
    public double AnchorMinY { get; set; } = 0.5;
    public double AnchorMaxX { get; set; } = 0.5;
    public double AnchorMaxY { get; set; } = 0.5;
    public double PivotX { get; set; } = 0.5;
    public double PivotY { get; set; } = 0.5;
    public int X { get; set; }                     // from the anchor to the pivot
    public int Y { get; set; }
    public int W { get; set; } = 64;               // size; added to the anchors' span when they stretch
    public int H { get; set; } = 16;

    public static UiRect Stretch() => new() { AnchorMinX = 0, AnchorMinY = 0, AnchorMaxX = 1, AnchorMaxY = 1, W = 0, H = 0 };
    public static UiRect TopLeft(int x, int y, int w, int h) => new() { AnchorMinX = 0, AnchorMinY = 0, AnchorMaxX = 0, AnchorMaxY = 0, PivotX = 0, PivotY = 0, X = x, Y = y, W = w, H = h };
}

// Horizontal, Vertical and Grid Layout Group with Content Size Fitter.
public sealed class UiLayoutGroup
{
    public UiLayoutKind Kind { get; set; }
    public int PaddingLeft { get; set; }
    public int PaddingTop { get; set; }
    public int PaddingRight { get; set; }
    public int PaddingBottom { get; set; }
    public int Spacing { get; set; }
    public int SpacingY { get; set; }              // Grid rows
    public int CellW { get; set; } = 32;           // Grid
    public int CellH { get; set; } = 16;
    public int Columns { get; set; }               // Grid; 0 = as many as fit
    public UiAlign AlignX { get; set; }
    public UiAlign AlignY { get; set; }
    public bool StretchCross { get; set; }         // children take the full width of a Vertical group, or height of a Horizontal one
    public bool FitW { get; set; }                 // Content Size Fitter: the group's size follows its children
    public bool FitH { get; set; }
}

// The on-screen keyboard of an Input Field: where it opens and its colours. Empty colours follow the field's.
public sealed class UiKeyboard
{
    public int X { get; set; } = 8;
    public int Y { get; set; } = 56;
    public int W { get; set; } = 224;
    public int H { get; set; } = 96;
    public string Fill { get; set; } = "";
    public string Border { get; set; } = "";
    public string TextColor { get; set; } = "";
    public string FocusFill { get; set; } = "";
    public string FocusTextColor { get; set; } = "";
}

public sealed class UiAction
{
    public UiActionKind Kind { get; set; }
    public string Target { get; set; } = "";       // page or scene name
    public int Data { get; set; }                  // OpenPage: the data the page opens with
}

public sealed class UiElement
{
    public string Name { get; set; } = "Element";
    public string Tag { get; set; } = "";
    public UiElementKind Kind { get; set; }
    public bool Hidden { get; set; }
    public UiRect Rect { get; set; } = new();
    public UiLayoutGroup? Layout { get; set; }
    public bool IgnoreLayout { get; set; }         // Layout Element
    public bool Flexible { get; set; }             // Layout Element: takes what the group has left along its direction
    public double AspectRatio { get; set; }        // Aspect Ratio Fitter: width / height, 0 = off

    // Colours are #RRGGBB; empty = none.
    public string Fill { get; set; } = "";
    public string Border { get; set; } = "";
    public int BorderWidth { get; set; } = 1;
    public string Accent { get; set; } = "";       // Slider bar, Toggle check, Button cursor
    public string FocusFill { get; set; } = "";
    public string FocusBorder { get; set; } = "";
    public string TextColor { get; set; } = "";
    public string FocusTextColor { get; set; } = "";
    public string DisabledTextColor { get; set; } = "";

    // Space between the rectangle and the text.
    public int PaddingLeft { get; set; }
    public int PaddingTop { get; set; }
    public int PaddingRight { get; set; }
    public int PaddingBottom { get; set; }

    public string Text { get; set; } = "";         // string key in the Localization tables, or plain ASCII text
    public UiAlign TextAlignX { get; set; }
    public UiAlign TextAlignY { get; set; } = UiAlign.Center;

    public string Image { get; set; } = "";        // image asset name
    public UiImageMode ImageMode { get; set; }
    public int SliceLeft { get; set; }
    public int SliceTop { get; set; }
    public int SliceRight { get; set; }
    public int SliceBottom { get; set; }

    public int Amount { get; set; } = 100;         // 0..100: Slider value, filled Image
    public bool On { get; set; }                   // Toggle
    public string Group { get; set; } = "";        // Toggle Group: one on at a time among the same name
    public bool Interactable { get; set; } = true; // a Slider that is only a bar (HP) turns this off
    public bool Cursor { get; set; }               // Button: the triangle cursor when focused
    public UiAction Action { get; set; } = new();
    public List<string> Options { get; set; } = new();  // Dropdown: string keys
    public int MaxLength { get; set; } = 12;       // Input Field: at most 12
    public UiKeyboard? Keyboard { get; set; }      // Input Field: null = the default place and the field's colours
    public string ScrollTarget { get; set; } = ""; // Scrollbar: the Scroll Rect it follows
    public List<string> ImageVariants { get; set; } = new();  // images UI.SetImage can show here
    // Explicit navigation by element name; empty = the nearest control that way.
    public string NavUp { get; set; } = "";
    public string NavDown { get; set; } = "";
    public string NavLeft { get; set; } = "";
    public string NavRight { get; set; } = "";

    public List<UiElement> Children { get; set; } = new();

    [JsonIgnore]
    public bool IsSelectable => !Hidden && Interactable && Kind is UiElementKind.Button or UiElementKind.Toggle or UiElementKind.Slider or UiElementKind.Dropdown or UiElementKind.InputField;

    public IEnumerable<UiElement> Walk()
    {
        yield return this;
        foreach (var c in Children)
            foreach (var d in c.Walk()) yield return d;
    }
}

public readonly record struct UiPixelRect(int X, int Y, int W, int H)
{
    public int Right => X + W;
    public int Bottom => Y + H;
}

// Page layout in screen pixels. The screen never changes size, so the build works it out once, and the UI Editor
// shows the same numbers the game draws.
public static class UiLayoutEngine
{
    public const int ScreenW = 240, ScreenH = 160;

    public static Dictionary<UiElement, UiPixelRect> Resolve(UiPageFile page)
    {
        var rects = new Dictionary<UiElement, UiPixelRect>(ReferenceEqualityComparer.Instance);
        var screen = new UiPixelRect(0, 0, ScreenW, ScreenH);
        Place(page.Root, FromAnchors(page.Root, screen), rects);
        return rects;
    }

    static int Round(double v) => (int)Math.Floor(v + 0.5);

    static (int W, int H) Measure(UiElement e)
    {
        var w = e.Rect.W;
        var h = e.Rect.H;
        if (e.Layout is { Kind: not UiLayoutKind.None } l && (l.FitW || l.FitH))
        {
            var (cw, ch) = ContentSize(e, l);
            if (l.FitW) w = cw;
            if (l.FitH) h = ch;
        }
        if (e.AspectRatio > 0) h = Math.Max(1, Round(w / e.AspectRatio));
        return (w, h);
    }

    static List<UiElement> LaidOut(UiElement e) => e.Children.Where(c => !c.Hidden && !c.IgnoreLayout).ToList();

    static (int W, int H) ContentSize(UiElement e, UiLayoutGroup l)
    {
        var kids = LaidOut(e);
        int padW = l.PaddingLeft + l.PaddingRight, padH = l.PaddingTop + l.PaddingBottom;
        if (kids.Count == 0) return (padW, padH);
        var sizes = kids.Select(Measure).ToList();
        switch (l.Kind)
        {
            case UiLayoutKind.Horizontal:
                return (padW + sizes.Sum(s => s.W) + l.Spacing * (kids.Count - 1), padH + sizes.Max(s => s.H));
            case UiLayoutKind.Vertical:
                return (padW + sizes.Max(s => s.W), padH + sizes.Sum(s => s.H) + l.Spacing * (kids.Count - 1));
            default:
                var cols = l.Columns > 0 ? l.Columns : kids.Count;
                var rows = (kids.Count + cols - 1) / cols;
                cols = Math.Min(cols, kids.Count);
                return (padW + cols * l.CellW + (cols - 1) * l.Spacing, padH + rows * l.CellH + (rows - 1) * l.SpacingY);
        }
    }

    static UiPixelRect FromAnchors(UiElement e, UiPixelRect parent)
    {
        var r = e.Rect;
        var (w, h) = Measure(e);
        double left = parent.X + r.AnchorMinX * parent.W, right = parent.X + r.AnchorMaxX * parent.W;
        double top = parent.Y + r.AnchorMinY * parent.H, bottom = parent.Y + r.AnchorMaxY * parent.H;
        var width = Math.Max(0, Round(right - left) + w);
        var height = Math.Max(0, Round(bottom - top) + h);
        var x = Round(left + (right - left) * r.PivotX + r.X - r.PivotX * width);
        var y = Round(top + (bottom - top) * r.PivotY + r.Y - r.PivotY * height);
        return new UiPixelRect(x, y, width, height);
    }

    static int Aligned(UiAlign a, int free) => a switch { UiAlign.Center => free / 2, UiAlign.End => free, _ => 0 };

    static void Place(UiElement e, UiPixelRect rect, Dictionary<UiElement, UiPixelRect> rects)
    {
        rects[e] = rect;
        var grouped = e.Layout is { Kind: not UiLayoutKind.None };
        if (grouped) PlaceLaidOut(e, rect, e.Layout!, rects);
        foreach (var c in e.Children)
            if (!grouped || c.IgnoreLayout || c.Hidden)
                Place(c, FromAnchors(c, rect), rects);
    }

    static void PlaceLaidOut(UiElement e, UiPixelRect rect, UiLayoutGroup l, Dictionary<UiElement, UiPixelRect> rects)
    {
        var kids = LaidOut(e);
        if (kids.Count == 0) return;
        int x0 = rect.X + l.PaddingLeft, y0 = rect.Y + l.PaddingTop;
        int w = rect.W - l.PaddingLeft - l.PaddingRight, h = rect.H - l.PaddingTop - l.PaddingBottom;
        if (l.Kind == UiLayoutKind.Grid)
        {
            var cols = l.Columns > 0 ? l.Columns : Math.Max(1, (w + l.Spacing) / Math.Max(1, l.CellW + l.Spacing));
            var rows = (kids.Count + cols - 1) / cols;
            var usedCols = Math.Min(cols, kids.Count);
            var ox = x0 + Aligned(l.AlignX, w - (usedCols * l.CellW + (usedCols - 1) * l.Spacing));
            var oy = y0 + Aligned(l.AlignY, h - (rows * l.CellH + (rows - 1) * l.SpacingY));
            for (var i = 0; i < kids.Count; i++)
                Place(kids[i], new UiPixelRect(ox + i % cols * (l.CellW + l.Spacing), oy + i / cols * (l.CellH + l.SpacingY), l.CellW, l.CellH), rects);
            return;
        }
        var across = l.Kind == UiLayoutKind.Horizontal;
        var sizes = kids.Select(Measure).ToList();
        var space = across ? w : h;
        var used = l.Spacing * (kids.Count - 1) + sizes.Select((s, i) => kids[i].Flexible ? 0 : across ? s.W : s.H).Sum();
        var flexible = kids.Count(k => k.Flexible);
        var share = flexible > 0 ? Math.Max(0, space - used) / flexible : 0;
        var total = used + share * flexible;
        var pen = across ? x0 + Aligned(l.AlignX, space - total) : y0 + Aligned(l.AlignY, space - total);
        for (var i = 0; i < kids.Count; i++)
        {
            var (cw, ch) = sizes[i];
            if (kids[i].Flexible)
            {
                if (across) cw = share;
                else ch = share;
            }
            if (l.StretchCross)
            {
                if (across) ch = h;
                else cw = w;
            }
            var r = across
                ? new UiPixelRect(pen, y0 + Aligned(l.AlignY, h - ch), cw, ch)
                : new UiPixelRect(x0 + Aligned(l.AlignX, w - cw), pen, cw, ch);
            Place(kids[i], r, rects);
            pen += (across ? cw : ch) + l.Spacing;
        }
    }

    // Automatic navigation: the nearest control in a direction, then around to the far side when the page wraps.
    public static UiElement? Neighbour(UiPageFile page, IReadOnlyDictionary<UiElement, UiPixelRect> rects, UiElement from, int dx, int dy)
    {
        var a = rects[from];
        double ax = a.X + a.W / 2.0, ay = a.Y + a.H / 2.0;
        UiElement? best = null, far = null;
        double bestScore = double.MaxValue, farAlong = 0;
        foreach (var c in page.Root.Walk())
        {
            if (c == from || !c.IsSelectable || !rects.TryGetValue(c, out var r)) continue;
            double vx = r.X + r.W / 2.0 - ax, vy = r.Y + r.H / 2.0 - ay;
            var along = vx * dx + vy * dy;
            var side = Math.Abs(vx * dy) + Math.Abs(vy * dx);
            if (along > 0)
            {
                var score = along + 2 * side;
                if (score < bestScore) { bestScore = score; best = c; }
            }
            else if (along < 0 && side <= Math.Max(a.W, a.H) && -along > farAlong)
            {
                farAlong = -along;
                far = c;
            }
        }
        return best ?? (page.WrapFocus ? far : null);
    }
}
