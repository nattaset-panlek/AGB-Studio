using System.Globalization;
using AgbStudio.Build;
using AgbStudio.Build.Fonts;
using AgbStudio.Build.Graphics;
using AgbStudio.Core;
using AgbStudio.Core.Project;

namespace AgbStudio.Editor.Windows.UiEditor;

/// <summary>A place where text does not fit its element.</summary>
public sealed record UiTextProblem(UiElement Element, int TextWidth, int TextHeight, int Width, int Height);

/// <summary>Draws a UI page to 240 x 160 pixels the way the runtime does (runtime/src/framework/ui.cpp PageScreen::draw),
/// with the ROM's layout, font and text layout, so what the editor shows is what the game shows. Game pixels are data, not
/// UI colours. The colour of the empty area is a dark checker that no page colour is taken from.</summary>
public sealed class UiPageRenderer
{
    public const int W = 240, H = 160;

    readonly GameProject _p;
    readonly Dictionary<string, string> _strings = new(StringComparer.Ordinal);
    readonly StringMeta _meta = new();
    readonly Dictionary<string, ColorImage?> _images = [];
    readonly uint[] _px = new uint[W * H];
    GameFont? _font;
    int _clipL, _clipT, _clipR, _clipB;

    public IReadOnlyList<string> Languages { get; }
    public string Language { get; private set; }
    public IReadOnlyList<string> StringKeys => _strings.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
    public List<UiTextProblem> Problems { get; } = [];

    public UiPageRenderer(GameProject p, string? language = null)
    {
        _p = p;
        Languages = p.StringTables().Select(f => GameProject.Load<StringTable>(f).Language).ToList();
        Language = language != null && Languages.Contains(language) ? language : p.Settings.Localization.Source;
        Reload();
    }

    public void SetLanguage(string language)
    {
        Language = language;
        Reload();
    }

    public void Reload()
    {
        _strings.Clear();
        _images.Clear();
        foreach (var f in _p.StringTables())
        {
            var t = GameProject.Load<StringTable>(f);
            if (t.Language != Language) continue;
            foreach (var (k, v) in t.Strings) _strings[k] = v;
        }
        var metaPath = Path.Combine(_p.TextDir, "strings.meta.json");
        _meta.Keys.Clear();
        if (File.Exists(metaPath))
            foreach (var (k, v) in GameProject.Load<StringMeta>(metaPath).Keys) _meta.Keys[k] = v;
        _font = LoadFont();
    }

    GameFont? LoadFont()
    {
        var name = _p.Settings.Ui.Font.Length > 0 ? _p.Settings.Ui.Font : _p.Settings.General.DefaultFont;
        var path = _p.FontFiles().FirstOrDefault(f => GameProject.AssetName(f) == name) ?? _p.FontFiles().FirstOrDefault();
        if (path == null) return null;
        var used = ThaiText.CodePoints(string.Concat(_strings.Values) + "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .-'/").Where(c => c >= 0x20).Distinct().ToList();
        var file = GameProject.Load<FontFile>(path);
        var font = FontBuilder.Build(file, _p.FontsDir, used);
        if (file.EmphasisFont.Length > 0 && _p.FontFiles().FirstOrDefault(f => GameProject.AssetName(f) == file.EmphasisFont) is { } boldPath)
        {
            var bold = FontBuilder.Build(GameProject.Load<FontFile>(boldPath), _p.FontsDir, used);
            var shift = font.Baseline - bold.Baseline;
            foreach (var g in bold.Glyphs.Values) g.Top += shift;
            font.Emphasis = bold;
        }
        return font;
    }

    public string? TextOf(string text) => _strings.TryGetValue(text, out var s) ? s : text.Length > 0 ? text : null;

    /// <summary>Hex #RRGGBB as the GBA shows it: five bits a channel.</summary>
    public static uint? Colour(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex) || !uint.TryParse(hex.Trim().TrimStart('#'), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v)) return null;
        return FromBgr555(Gba.ToBgr555(0xFF000000u | v));
    }

    static uint FromBgr555(ushort c)
    {
        uint E(int shift)
        {
            var x = (uint)(c >> shift & 31);
            return x << 3 | x >> 2;
        }
        return 0xFF000000u | E(0) << 16 | E(5) << 8 | E(10);
    }

    /// <summary>Colours all the project's pages use together; a page set has 15.</summary>
    public int ColoursInUse()
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in _p.UiPageFiles())
        {
            UiPageFile page;
            try { page = GameProject.Load<UiPageFile>(f); }
            catch (System.Text.Json.JsonException) { continue; }
            foreach (var e in page.Root.Walk())
                foreach (var c in new[] { e.Fill, e.Border, e.Accent, e.FocusFill, e.FocusBorder, e.TextColor, e.FocusTextColor, e.DisabledTextColor })
                    if (c.Length > 0) used.Add(c);
        }
        return used.Count;
    }

    void Fill(int x, int y, int w, int h, uint? color)
    {
        if (color is not { } c) return;
        var r = Math.Min(x + w, _clipR);
        var b = Math.Min(y + h, _clipB);
        for (var py = Math.Max(y, _clipT); py < b; py++)
            for (var px = Math.Max(x, _clipL); px < r; px++)
                _px[py * W + px] = c;
    }

    void Frame(int x, int y, int w, int h, int t, uint? color)
    {
        if (t <= 0) return;
        Fill(x, y, w, t, color);
        Fill(x, y + h - t, w, t, color);
        Fill(x, y + t, t, h - 2 * t, color);
        Fill(x + w - t, y + t, t, h - 2 * t, color);
    }

    ColorImage? Image(string name)
    {
        if (_images.TryGetValue(name, out var known)) return known;
        var path = _p.ImageFiles().FirstOrDefault(f => GameProject.AssetName(f) == name);
        var img = path == null ? null : ColorImage.Load(path);
        _images[name] = img;
        return img;
    }

    void Blit(ColorImage im, int sx, int sy, int w, int h, int x, int y)
    {
        for (var j = 0; j < h; j++)
            for (var i = 0; i < w; i++)
            {
                var px = x + i;
                var py = y + j;
                if (px < _clipL || px >= _clipR || py < _clipT || py >= _clipB) continue;
                var o = (sy + j) * im.Width + sx + i;
                if (o >= 0 && o < im.Colors.Length && !im.Transparent[o]) _px[py * W + px] = FromBgr555(im.Colors[o]);
            }
    }

    void TileImage(ColorImage im, int sx, int sy, int sw, int sh, int x, int y, int w, int h)
    {
        if (sw <= 0 || sh <= 0) return;
        for (var dy = 0; dy < h; dy += sh)
            for (var dx = 0; dx < w; dx += sw)
                Blit(im, sx, sy, Math.Min(sw, w - dx), Math.Min(sh, h - dy), x + dx, y + dy);
    }

    void DrawImage(UiElement e, UiPixelRect r, ColorImage im)
    {
        int iw = im.Width, ih = im.Height;
        switch (e.ImageMode)
        {
            case UiImageMode.Sliced:
                int l = e.SliceLeft, t = e.SliceTop, rr = e.SliceRight, b = e.SliceBottom;
                int mw = iw - l - rr, mh = ih - t - b, cw = r.W - l - rr, ch = r.H - t - b;
                Blit(im, 0, 0, l, t, r.X, r.Y);
                Blit(im, iw - rr, 0, rr, t, r.Right - rr, r.Y);
                Blit(im, 0, ih - b, l, b, r.X, r.Bottom - b);
                Blit(im, iw - rr, ih - b, rr, b, r.Right - rr, r.Bottom - b);
                TileImage(im, l, 0, mw, t, r.X + l, r.Y, cw, t);
                TileImage(im, l, ih - b, mw, b, r.X + l, r.Bottom - b, cw, b);
                TileImage(im, 0, t, l, mh, r.X, r.Y + t, l, ch);
                TileImage(im, iw - rr, t, rr, mh, r.Right - rr, r.Y + t, rr, ch);
                TileImage(im, l, t, mw, mh, r.X + l, r.Y + t, cw, ch);
                break;
            case UiImageMode.Tiled:
                TileImage(im, 0, 0, iw, ih, r.X, r.Y, r.W, r.H);
                break;
            case UiImageMode.FilledHorizontal:
                Blit(im, 0, 0, iw * e.Amount / 100, ih, r.X, r.Y);
                break;
            case UiImageMode.FilledVertical:
                var part = ih * e.Amount / 100;
                Blit(im, 0, ih - part, iw, part, r.X, r.Y + ih - part);
                break;
            default:
                Blit(im, 0, 0, iw, ih, r.X, r.Y);
                break;
        }
    }

    void Glyph(GameFont.Glyph g, GameFont font, int x, int y, uint color)
    {
        for (var j = 0; j < g.Height; j++)
            for (var i = 0; i < g.Width; i++)
            {
                if (g.Shades[j * g.Width + i] == 0) continue;
                var px = x + g.Left + i;
                var py = y + g.Top + j;
                if (px < _clipL || px >= _clipR || py < _clipT || py >= _clipB) continue;
                _px[py * W + px] = color;
            }
        _ = font;
    }

    // Text with its tokens filled by a sample number, as the runtime closes the gap a value keeps.
    (int Width, int Lines) Measure(string text, int boxWidth)
    {
        if (_font is null) return (0, 1);
        var layout = TextLayout.Layout(text, _font, boxWidth, _p.Settings.Localization.ThaiWrap);
        var w = 0;
        var line = -1;
        var shift = 0;
        foreach (var g in layout.Glyphs)
        {
            if (g.Line != line) { line = g.Line; shift = 0; }
            var from = g.Bold && _font.Emphasis is { } b ? b : _font;
            if (TextLayout.IsVarToken(g.CodePoint))
            {
                w = Math.Max(w, g.X + shift + 2 * Digit());
                shift += 2 * Digit() - TextLayout.VarReserve;
            }
            else if (!TextLayout.IsToken(g.CodePoint))
            {
                w = Math.Max(w, g.X + shift + (from.Get(g.CodePoint)?.Advance ?? 0));
            }
        }
        return (w, Math.Max(1, layout.Lines));
    }

    int Digit() => _font?.Get('0')?.Advance ?? 6;

    int BoxWidth(string key) => _meta.Keys.GetValueOrDefault(key)?.BoxWidth is { } b && b != 224 ? b
        : _p.Settings.Ui.Dialogue is { } d ? Math.Clamp(d.W / 8 * 8 - d.PaddingLeft - d.PaddingRight, 8, 224) : 224;

    void DrawText(string key, string text, uint color, int x, int y, int w, int h, UiAlign alignX, UiAlign alignY, UiElement? report)
    {
        if (_font is null) return;
        var box = BoxWidth(key);
        var (tw, lines) = Measure(text, box);
        var lh = _font.LineHeight;
        if (report != null && (tw > w || lines * lh > h + 2)) Problems.Add(new UiTextProblem(report, tw, lines * lh, w, h));
        var tx = x + alignX switch { UiAlign.Center => (w - tw) / 2, UiAlign.End => w - tw, _ => 0 };
        var ty = y + alignY switch { UiAlign.Center => (h - lines * lh) / 2, UiAlign.End => h - lines * lh, _ => 0 };
        var (cl, cr) = (_clipL, _clipR);
        _clipL = Math.Max(_clipL, x);
        _clipR = Math.Min(_clipR, x + w);
        var layout = TextLayout.Layout(text, _font, box, _p.Settings.Localization.ThaiWrap);
        var rich = _p.Settings.Ui.RichColors.Select(Colour).ToList();
        var ink = color;
        var line = -1;
        var shift = 0;
        foreach (var g in layout.Glyphs)
        {
            if (g.Line != line) { line = g.Line; shift = 0; }
            var gy = ty + g.Line * lh + g.Dy;
            if (TextLayout.IsColorToken(g.CodePoint))
            {
                var n = g.CodePoint - TextLayout.ColorToken;
                ink = n >= 1 && n <= rich.Count && rich[n - 1] is { } rc ? rc : color;
            }
            else if (TextLayout.IsVarToken(g.CodePoint))
            {
                for (var k = 0; k < 2; k++)
                    if (_font.Get('0') is { } zero) Glyph(zero, _font, tx + g.X + shift + k * Digit(), gy, ink);
                shift += 2 * Digit() - TextLayout.VarReserve;
            }
            else if (!TextLayout.IsToken(g.CodePoint))
            {
                var from = g.Bold && _font.Emphasis is { } b ? b : _font;
                if (from.Get(g.CodePoint) is { } glyph) Glyph(glyph, from, tx + g.X + shift, gy, ink);
            }
        }
        _clipL = cl;
        _clipR = cr;
    }

    static int ContentHeight(UiElement s, UiPixelRect view, IReadOnlyDictionary<UiElement, UiPixelRect> rects)
    {
        var bottom = s.Children.Where(c => rects.ContainsKey(c) && !c.Hidden).Select(c => rects[c].Bottom).DefaultIfEmpty(view.Y).Max();
        return bottom - view.Y + (s.Layout?.PaddingBottom ?? 0);
    }

    /// <summary>Draws the page; <paramref name="focused"/> shows in its focus colours, and an Input Field's keyboard opens.</summary>
    public uint[] Render(UiPageFile page, UiElement? focused, uint[]? backdrop = null)
    {
        Problems.Clear();
        for (var y = 0; y < H; y++)
            for (var x = 0; x < W; x++)
                _px[y * W + x] = backdrop?[y * W + x] ?? ((x / 8 + y / 8) % 2 == 0 ? 0xFF2A2D36u : 0xFF24262Eu);
        var rects = UiLayoutEngine.Resolve(page);
        void Walk(UiElement e, bool parentHidden, (int L, int T, int R, int B) clip)
        {
            if (e.Hidden || parentHidden || !rects.TryGetValue(e, out var r)) return;
            (_clipL, _clipT, _clipR, _clipB) = clip;
            DrawElement(e, r, ReferenceEquals(e, focused), page, rects);
            var childClip = clip;
            if (e.Kind == UiElementKind.ScrollRect)
            {
                var b = e.Border.Length > 0 ? e.BorderWidth : 0;
                childClip = (Math.Max(clip.L, r.X + b), Math.Max(clip.T, r.Y + b), Math.Min(clip.R, r.Right - b), Math.Min(clip.B, r.Bottom - b));
            }
            foreach (var c in e.Children) Walk(c, false, childClip);
        }
        Walk(page.Root, false, (0, 0, W, H));
        (_clipL, _clipT, _clipR, _clipB) = (0, 0, W, H);
        if (focused is { Kind: UiElementKind.InputField } field) DrawKeyboard(field);
        return _px;
    }

    void DrawElement(UiElement e, UiPixelRect r, bool focused, UiPageFile page, IReadOnlyDictionary<UiElement, UiPixelRect> rects)
    {
        var off = !e.Interactable && e.Kind is UiElementKind.Button or UiElementKind.Toggle or UiElementKind.Dropdown or UiElementKind.InputField && e.DisabledTextColor.Length > 0;
        var fill = focused && e.FocusFill.Length > 0 ? e.FocusFill : e.Fill;
        var border = focused && e.FocusBorder.Length > 0 ? e.FocusBorder : e.Border;
        var inkHex = off && !e.Interactable ? e.DisabledTextColor : focused && e.FocusTextColor.Length > 0 ? e.FocusTextColor : e.TextColor;
        var ink = Colour(inkHex);
        Fill(r.X, r.Y, r.W, r.H, Colour(fill));
        if (e.Image.Length > 0 && Image(e.Image) is { } im) DrawImage(e, r, im);
        if (e.Kind == UiElementKind.Slider)
        {
            var inset = e.Border.Length > 0 ? e.BorderWidth : 0;
            Fill(r.X + inset, r.Y + inset, (r.W - 2 * inset) * e.Amount / 100, r.H - 2 * inset, Colour(e.Accent));
        }
        if (e.Kind == UiElementKind.Scrollbar)
        {
            var target = page.Root.Walk().FirstOrDefault(x => x.Name == e.ScrollTarget);
            var thumb = r.H;
            if (target != null && rects.TryGetValue(target, out var view))
            {
                var content = ContentHeight(target, view, rects);
                if (content > view.H) thumb = Math.Max(6, r.H * view.H / content);
            }
            Fill(r.X, r.Y, r.W, thumb, Colour(e.Accent));
            return;
        }
        Frame(r.X, r.Y, r.W, r.H, e.BorderWidth, Colour(border));
        var tx = r.X + e.PaddingLeft;
        var tw = r.W - e.PaddingLeft - e.PaddingRight;
        var ty = r.Y + e.PaddingTop;
        var th = r.H - e.PaddingTop - e.PaddingBottom;
        switch (e.Kind)
        {
            case UiElementKind.Toggle:
            {
                var size = Math.Min(r.H, 10);
                var top = r.Y + (r.H - size) / 2;
                Frame(tx, top, size, size, 1, ink ?? Colour(e.Accent));
                if (e.On) Fill(tx + 2, top + 2, size - 4, size - 4, Colour(e.Accent) ?? ink);
                tx += size + 4;
                tw -= size + 4;
                break;
            }
            case UiElementKind.Button when e.Cursor:
                if (focused)
                {
                    var top = r.Y + r.H / 2 - 3;
                    for (var k = 0; k < 3; k++) Fill(tx + 1 + k, top + k, 1, 7 - 2 * k, Colour(e.Accent) ?? ink);
                }
                tx += 8;
                tw -= 8;
                break;
            case UiElementKind.Dropdown:
            {
                var arrow = tx + tw - 5;
                var top = r.Y + r.H / 2 - 1;
                for (var k = 0; k < 3; k++) Fill(arrow + k, top + k, 5 - 2 * k, 1, ink);
                tw -= 8;
                if (e.Options.Count > 0 && ink is { } optionInk && TextOf(e.Options[Math.Min(e.Amount, e.Options.Count - 1)]) is { } option)
                    DrawText(e.Options[0], option, optionInk, tx, ty, tw, th, UiAlign.End, e.TextAlignY, null);
                break;
            }
            case UiElementKind.InputField:
                if (ink is { } typed) DrawText("", "ABC", typed, tx, ty, tw, th, UiAlign.Start, e.TextAlignY, e);
                return;
        }
        if (ink is { } c && e.Text.Length > 0 && TextOf(e.Text) is { } text)
            DrawText(e.Text, text, c, tx, ty, tw, th, e.Kind == UiElementKind.Dropdown ? UiAlign.Start : e.TextAlignX, e.TextAlignY, e);
    }

    void DrawKeyboard(UiElement e)
    {
        var k = e.Keyboard ?? new UiKeyboard();
        var fill = Colour(k.Fill.Length > 0 ? k.Fill : e.Fill);
        Fill(k.X, k.Y, k.W, k.H, fill);
        Frame(k.X, k.Y, k.W, k.H, 1, Colour(k.Border.Length > 0 ? k.Border : e.Border));
        var ink = Colour(k.TextColor.Length > 0 ? k.TextColor : e.TextColor);
        var focusInk = Colour(k.FocusTextColor.Length > 0 ? k.FocusTextColor : e.FocusTextColor) ?? ink;
        var focusFill = Colour(k.FocusFill.Length > 0 ? k.FocusFill : e.FocusFill);
        int kw = (k.W - 8) / 13, kh = (k.H - 6) / 6;
        if (kw <= 0 || kh <= 0 || ink is not { } normal) return;
        int ox = k.X + (k.W - kw * 13) / 2, oy = k.Y + (k.H - kh * 6) / 2;
        string[] rows = ["ABCDEFGHIJKLM", "NOPQRSTUVWXYZ", "abcdefghijklm", "nopqrstuvwxyz", "0123456789.-'"];
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < 13; c++)
            {
                var at = r == 0 && c == 0;
                if (at) Fill(ox, oy, kw, kh, focusFill);
                DrawText("", rows[r][c].ToString(), at ? focusInk ?? normal : normal, ox + c * kw, oy + r * kh, kw, kh, UiAlign.Center, UiAlign.Center, null);
            }
        (string Label, int Col, int Cols)[] bottom = [("SPACE", 0, 5), ("DEL", 5, 4), ("OK", 9, 4)];
        foreach (var (label, col, cols) in bottom)
            DrawText("", label, normal, ox + col * kw, oy + 5 * kh, cols * kw, kh, UiAlign.Center, UiAlign.Center, null);
    }
}
