using System.Globalization;
using System.Text;
using AgbStudio.Build.Graphics;
using AgbStudio.Core.Project;

namespace AgbStudio.Build.Pipeline;

// UI pages (ui/*.uipage): every rectangle and D-pad neighbour is worked out here, so the game only draws. All pages share
// one 16-colour palette, because a page opened over another shows both.
public sealed partial class GameBuilder
{
    const int UiPaletteSlots = 15;  // index 0 is see-through

    bool _hasPages;
    long _largestPageBytes;

    sealed class UiPalette(GameBuilder b, string file, int shades, ushort paper)
    {
        readonly List<ushort> _colors = [];
        readonly Dictionary<(ushort Ink, ushort Back), int> _inks = [];
        bool _warned;

        public ushort[] Table()
        {
            var t = new ushort[16];
            for (var i = 0; i < _colors.Count && i < UiPaletteSlots; i++) t[i + 1] = _colors[i];
            return t;
        }

        static ushort Mix(ushort a, ushort b, int t)
        {
            int M(int shift)
            {
                int x = a >> shift & 31, y = b >> shift & 31;
                return x + (y - x) * t / 16;
            }
            return (ushort)(M(0) | M(5) << 5 | M(10) << 10);
        }

        static int Distance(ushort a, ushort b)
        {
            int D(int shift) => Math.Abs((a >> shift & 31) - (b >> shift & 31));
            return D(0) + D(5) + D(10);
        }

        void Full()
        {
            if (_warned) return;
            _warned = true;
            b.Add(new Diagnostic(Severity.Warning, file, 0, 0, "", $"UI pages use more than {UiPaletteSlots} colours, so some show as the nearest colour. Use fewer colours across the project's UI pages."));
        }

        int Nearest(ushort c)
        {
            Full();
            var best = 0;
            for (var i = 1; i < _colors.Count && i < UiPaletteSlots; i++)
                if (Distance(_colors[i], c) < Distance(_colors[best], c)) best = i;
            return _colors.Count == 0 ? 0 : best + 1;
        }

        public int Color(ushort c)
        {
            var i = _colors.IndexOf(c);
            if (i >= 0) return i + 1;
            if (_colors.Count >= UiPaletteSlots) return Nearest(c);
            _colors.Add(c);
            return _colors.Count;
        }

        // Fonts draw with ink, ink + 1 and ink + 2, so a text colour takes that many slots in a row, shaded to what is behind it.
        public int Ink(ushort ink, ushort? back)
        {
            var behind = back ?? paper;
            if (_inks.TryGetValue((ink, behind), out var known)) return known;
            if (_colors.Count + shades > UiPaletteSlots) return Nearest(ink);
            var at = _colors.Count + 1;
            _colors.Add(ink);
            if (shades > 1) _colors.Add(Mix(ink, behind, 6));
            if (shades > 2) _colors.Add(Mix(ink, behind, 11));
            _inks[(ink, behind)] = at;
            return at;
        }
    }

    static ushort? UiColor(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;
        return uint.TryParse(hex.Trim().TrimStart('#'), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v) ? Gba.ToBgr555(0xFF000000u | v) : null;
    }

    static bool UiDraws(UiElement e) =>
        e.Kind is not UiElementKind.Panel || e.Fill.Length + e.Border.Length + e.Image.Length + e.Text.Length > 0;

    void GenerateUiPages(List<string> sceneNames)
    {
        var files = _p.UiPageFiles().ToList();
        var pages = files.Select(f => (File: f, Page: GameProject.Load<UiPageFile>(f))).ToList();
        // A ready-made page goes in when a script names it and the project has no page of that name.
        var scripts = string.Concat(_p.ScriptFiles().Select(File.ReadAllText)) + string.Concat(_p.SceneFiles().Select(f => '"' + GameProject.Load<SceneFile>(f).Page + '"'));
        foreach (var name in UiPresets.Names)
            if (scripts.Contains('"' + name + '"', StringComparison.Ordinal) && !pages.Any(x => (x.Page.Name.Length > 0 ? x.Page.Name : GameProject.AssetName(x.File)) == name))
                pages.Add((Path.Combine(_p.UiDir, name + ".uipage"), UiPresets.Create(name, _p.Settings.Ui)!));
        _hasPages = pages.Count > 0;
        _largestPageBytes = 0;
        if (!_hasPages) return;
        if (files.Count == 0) files.Add(pages[0].File);
        var pageNames = pages.Select(x => x.Page.Name.Length > 0 ? x.Page.Name : GameProject.AssetName(x.File)).ToList();
        var font = UiFont();
        var paper = UiColor(_p.Settings.Ui.Paper) ?? (ushort)0x7FFF;
        var palette = new UiPalette(this, files[0], font?.Font.Bpp >= 2 ? 3 : 1, paper);

        // <c1>..<c3> first: strings are the same on every page, so their inks are the same on every page.
        var usesRich = _texts.Any(t => t.Strings.Values.Any(v => v.Contains("<c", StringComparison.Ordinal)));
        var richInks = _p.Settings.Ui.RichColors.Concat(["#C83030", "#3050C8", "#208838"]).Take(3)
            .Select(c => usesRich && UiColor(c) is { } ink ? palette.Ink(ink, paper) : 0).ToList();
        // Text first: its colours need runs of slots.
        foreach (var (_, page) in pages) CollectInks(page.Root, null, palette);
        foreach (var (_, page) in pages)
            foreach (var e in page.Root.Walk())
                foreach (var c in new[] { e.Fill, e.Border, e.Accent, e.FocusFill, e.FocusBorder, e.DisabledTextColor, e.Keyboard?.Fill ?? "", e.Keyboard?.Border ?? "", e.Keyboard?.FocusFill ?? "" })
                    if (UiColor(c) is { } color) palette.Color(color);

        var images = new Dictionary<string, (string Symbol, int W, int H)>(StringComparer.Ordinal);
        var body = new StringBuilder();
        for (var i = 0; i < pages.Count; i++) PageCpp(body, pages[i].File, pages[i].Page, i, pageNames, sceneNames, palette, images);
        foreach (var (file, page) in pages)
            foreach (var e in page.Root.Walk())
                foreach (var v in e.ImageVariants) UiImage(file, v, palette, images);

        var sb = new StringBuilder("// Generated by AGB Studio from the ui folder. Do not edit.\n#include \"agb/text.hpp\"\n#include \"agb/ui.hpp\"\nnamespace {\n");
        sb.Append(_uiImages);
        sb.Append(body);
        Cpp.U16(sb, "const unsigned short kPagePalette", palette.Table().Select(c => (int)c));
        sb.Append("const agb::ui::PageDef kPages[] = {\n");
        for (var i = 0; i < pages.Count; i++) sb.Append(_uiPageRows[i]);
        sb.Append("};\n");
        sb.Append($"const unsigned char kRichInks[] = {{{string.Join(",", richInks)}}};\n");
        sb.Append("const agb::ui::NamedImage kNamedImages[] = {");
        sb.Append(images.Count == 0 ? "{nullptr, nullptr}" : string.Join(", ", images.Select(kv => $"{{{Cpp.Str(kv.Key)}, &{kv.Value.Symbol}}}")));
        sb.Append("};\n}  // namespace\n");
        sb.Append($"extern const agb::ui::PageSet agb_pages = {{kPages, {pages.Count}, kPagePalette, kNamedImages, {images.Count}, kRichInks}};\n");
        Write(Path.Combine(GenDir, "ui_pages.cpp"), sb.ToString());
        _uiImages.Clear();
        _uiPageRows.Clear();
    }

    readonly StringBuilder _uiImages = new();
    readonly List<string> _uiPageRows = [];

    void CollectInks(UiElement e, ushort? back, UiPalette palette)
    {
        var behind = UiColor(e.Fill) ?? back;
        if (e.Keyboard is { } kb)
        {
            var keyBack = UiColor(kb.Fill) ?? behind;
            if (UiColor(kb.TextColor.Length > 0 ? kb.TextColor : e.TextColor) is { } keyInk) palette.Ink(keyInk, keyBack);
            if (UiColor(kb.FocusTextColor) is { } keyFocus) palette.Ink(keyFocus, UiColor(kb.FocusFill) ?? keyBack);
        }
        if (e.Text.Length > 0)
        {
            if (UiColor(e.TextColor) is { } ink) palette.Ink(ink, behind);
            if (UiColor(e.FocusTextColor) is { } focus) palette.Ink(focus, UiColor(e.FocusFill) ?? behind);
        }
        foreach (var c in e.Children) CollectInks(c, behind, palette);
    }

    (string Symbol, int W, int H)? UiImage(string file, string name, UiPalette palette, Dictionary<string, (string Symbol, int W, int H)> images)
    {
        if (images.TryGetValue(name, out var known)) return known;
        var path = _p.ImageFiles().FirstOrDefault(f => GameProject.AssetName(f) == name);
        if (path == null)
        {
            Add(new Diagnostic(Severity.Warning, file, 0, 0, "", $"UI image '{name}' is not in assets/images, so it shows nothing. Import it, or pick another image."));
            return null;
        }
        var img = ColorImage.Load(path);
        var pixels = new byte[img.Width * img.Height];
        for (var i = 0; i < pixels.Length; i++)
            pixels[i] = img.Transparent[i] ? (byte)0 : (byte)palette.Color(img.Colors[i]);
        var symbol = "kImage" + Cpp.Ident(name);
        _uiImages.Append($"const unsigned char {symbol}Pixels[] = {{{string.Join(",", pixels)}}};\n");
        _uiImages.Append($"const agb::ui::ImageDef {symbol} = {{{symbol}Pixels, {img.Width}, {img.Height}}};\n");
        var made = (symbol, img.Width, img.Height);
        images[name] = made;
        return made;
    }

    // Text that does not fit its element is cut by the runtime, so it is a warning at build time, in every language.
    void CheckTextFits(string file, string pageName, UiElement e, UiPixelRect r)
    {
        var check = new List<string>();
        if (e.Text.Length > 0 && StringKey(e.Text) >= 0) check.Add(e.Text);
        foreach (var t in check)
            foreach (var table in _texts)
            {
                if (!table.Strings.TryGetValue(t, out var s)) continue;
                var left = e.Kind switch
                {
                    UiElementKind.Button when e.Cursor => 8,
                    UiElementKind.Toggle => Math.Min(r.H, 10) + 4,
                    UiElementKind.Dropdown => 8,
                    _ => 0,
                };
                var w = r.W - e.PaddingLeft - e.PaddingRight - left;
                var h = r.H - e.PaddingTop - e.PaddingBottom;
                var layout = Fonts.TextLayout.Layout(s, table.Font.Font, table.BoxWidth(t), _p.Settings.Localization.ThaiWrap);
                var textW = 0;
                // A value keeps kTextVarReserve pixels in the layout; a short number is what most of them show.
                var valueLine = -1;
                var gaps = 0;
                foreach (var g in layout.Glyphs)
                {
                    if (g.Line != valueLine)
                    {
                        valueLine = g.Line;
                        gaps = 0;
                    }
                    var from = g.Bold && table.Font.Font.Emphasis is { } emphasis ? emphasis : table.Font.Font;
                    var adv = Fonts.TextLayout.IsToken(g.CodePoint) ? (Fonts.TextLayout.IsVarToken(g.CodePoint) ? 8 : 0) : from.Get(g.CodePoint)?.Advance ?? 0;
                    textW = Math.Max(textW, g.X - gaps + adv);
                    if (Fonts.TextLayout.IsVarToken(g.CodePoint)) gaps += Fonts.TextLayout.VarReserve - 8;
                }
                var textH = layout.Lines * table.Font.Font.LineHeight;
                if (textW > w || textH > h + 2)
                    Add(new Diagnostic(Severity.Warning, file, 0, 0, "", $"UI page {pageName}: the text of {e.Name} ({t}, {table.Language}) is {textW} x {textH} px and its space is {w} x {h} px, so it is cut. Make the text shorter, or the element larger."));
            }
    }

    void PageCpp(StringBuilder sb, string file, UiPageFile page, int pageIndex, List<string> pageNames, List<string> sceneNames, UiPalette palette, Dictionary<string, (string Symbol, int W, int H)> images)
    {
        var rects = UiLayoutEngine.Resolve(page);
        var list = page.Root.Walk().Where(rects.ContainsKey).ToList();
        if (list.Count > 64)
            Add(new Diagnostic(Severity.Error, file, 0, 0, "", $"UI page {pageNames[pageIndex]} has {list.Count} elements; a page can have 64. Move part of it to another page."));
        foreach (var dup in list.GroupBy(e => e.Name).Where(g => g.Count() > 1))
            Add(new Diagnostic(Severity.Warning, file, 0, 0, "", $"UI page {pageNames[pageIndex]} has {dup.Count()} elements named '{dup.Key}'; scripts reach only the first. Give each its own name."));
        var parents = new Dictionary<UiElement, UiElement>(ReferenceEqualityComparer.Instance);
        foreach (var e in list)
            foreach (var c in e.Children) parents[c] = e;
        var groups = list.Where(e => e.Group.Length > 0).Select(e => e.Group).Distinct().ToList();
        int IndexOf(string name) => list.FindIndex(e => e.Name == name);

        ushort? Behind(UiElement e)
        {
            for (UiElement? p = e; p != null; p = parents.GetValueOrDefault(p))
                if (UiColor(p.Fill) is { } c) return c;
            return null;
        }

        int Nav(UiElement e, string name, int dx, int dy)
        {
            if (!e.IsSelectable) return -1;
            if (name.Length > 0)
            {
                var at = IndexOf(name);
                if (at < 0) Add(new Diagnostic(Severity.Warning, file, 0, 0, "", $"{e.Name}: navigation goes to '{name}', which is not on page {pageNames[pageIndex]}."));
                return at;
            }
            return UiLayoutEngine.Neighbour(page, rects, e, dx, dy) is { } n ? list.IndexOf(n) : -1;
        }

        var id = pageIndex.ToString(CultureInfo.InvariantCulture);
        var optionTables = new StringBuilder();
        var elementRows = new StringBuilder();
        var pageBody = sb;
        sb = elementRows;
        sb.Append($"const agb::ui::ElementDef kElements{id}[] = {{\n");
        foreach (var e in list)
        {
            var r = rects[e];
            if (e.Text.Length > 0 && StringKey(e.Text) < 0 && e.Text.Any(ch => ch > 126))
                Add(new Diagnostic(Severity.Warning, file, 0, 0, "", $"{e.Name}: '{e.Text}' is not a string key, and only ASCII text can be drawn without one. Add it as a string key in Localization, or use ASCII text."));
            var label = e.Text.Length > 0 ? Label(e.Text, e.Text) : "{-1, nullptr}";
            var image = e.Image.Length > 0 ? UiImage(file, e.Image, palette, images) : null;
            var target = 0;
            if (e.Action.Kind == UiActionKind.OpenPage) target = pageNames.IndexOf(e.Action.Target);
            else if (e.Action.Kind == UiActionKind.LoadScene) target = sceneNames.IndexOf(e.Action.Target);
            if (target < 0)
                Add(new Diagnostic(Severity.Error, file, 0, 0, "", $"{e.Name}: {e.Action.Kind} goes to '{e.Action.Target}', which is not in the project. Pick an existing one."));
            int C(string hex) => UiColor(hex) is { } c ? palette.Color(c) : 0;
            var back = Behind(e);
            var ink = UiColor(e.TextColor) is { } tc ? palette.Ink(tc, back) : 0;
            var focusInk = UiColor(e.FocusTextColor) is { } fc ? palette.Ink(fc, UiColor(e.FocusFill) ?? back) : 0;
            var parent = parents.TryGetValue(e, out var p) ? list.IndexOf(p) : -1;
            sb.Append($"    {{{Cpp.Str(e.Name)}, agb::ui::ElementKind::{e.Kind}, {parent}, {r.X}, {r.Y}, {r.W}, {r.H}, ");
            sb.Append($"{C(e.Fill)}, {C(e.Border)}, {Math.Clamp(e.BorderWidth, 0, 255)}, {C(e.Accent)}, {C(e.FocusFill)}, {C(e.FocusBorder)}, {ink}, {focusInk}, ");
            sb.Append($"agb::ui::Align::{e.TextAlignX}, agb::ui::Align::{e.TextAlignY}, {label}, {(image is { } im ? "&" + im.Symbol : "nullptr")}, agb::ui::ImageMode::{e.ImageMode}, ");
            sb.Append($"{Math.Clamp(e.SliceLeft, 0, 255)}, {Math.Clamp(e.SliceTop, 0, 255)}, {Math.Clamp(e.SliceRight, 0, 255)}, {Math.Clamp(e.SliceBottom, 0, 255)}, {Math.Clamp(e.Amount, 0, 100)}, ");
            sb.Append($"{(e.On ? "true" : "false")}, {(e.Hidden ? "true" : "false")}, {(e.Cursor ? "true" : "false")}, {groups.IndexOf(e.Group)}, ");
            sb.Append($"{Nav(e, e.NavUp, 0, -1)}, {Nav(e, e.NavDown, 0, 1)}, {Nav(e, e.NavLeft, -1, 0)}, {Nav(e, e.NavRight, 1, 0)}, ");
            var optionsSymbol = "nullptr";
            if (e.Options.Count > 0)
            {
                optionsSymbol = $"kOptions{id}_{list.IndexOf(e)}";
                optionTables.Append($"const agb::ui::Label {optionsSymbol}[] = {{{string.Join(", ", e.Options.Select(o => Label(o, o)))}}};\n");
            }
            var data = e.Kind == UiElementKind.ScrollRect ? e.Layout?.Spacing ?? 0 : e.Action.Data;
            var padT = e.Kind == UiElementKind.ScrollRect ? e.Layout?.PaddingTop ?? 0 : e.PaddingTop;
            var padB = e.Kind == UiElementKind.ScrollRect ? e.Layout?.PaddingBottom ?? 0 : e.PaddingBottom;
            var scrollTarget = e.Kind == UiElementKind.Scrollbar && e.ScrollTarget.Length > 0 ? IndexOf(e.ScrollTarget) : -1;
            if (e.Kind == UiElementKind.Scrollbar && scrollTarget < 0)
                Add(new Diagnostic(Severity.Warning, file, 0, 0, "", $"{e.Name}: this Scrollbar has no Scroll Rect called '{e.ScrollTarget}' on page {pageNames[pageIndex]}. Set its Scroll Target."));
            sb.Append($"agb::ui::ActionKind::{e.Action.Kind}, {Math.Max(0, target)}, {Math.Clamp(data, short.MinValue, short.MaxValue)}, ");
            sb.Append($"{Math.Clamp(e.PaddingLeft, 0, 255)}, {Math.Clamp(padT, 0, 255)}, {Math.Clamp(e.PaddingRight, 0, 255)}, {Math.Clamp(padB, 0, 255)}, ");
            sb.Append($"{(UiColor(e.DisabledTextColor) is { } dc ? palette.Ink(dc, back) : 0)}, {(e.Interactable ? "true" : "false")}, {scrollTarget}, ");
            var keyboardSymbol = "nullptr";
            if (e.Kind == UiElementKind.InputField)
            {
                var kb = e.Keyboard ?? new UiKeyboard();
                keyboardSymbol = $"&kKeyboard{id}_{list.IndexOf(e)}";
                var keyBack = UiColor(kb.Fill) ?? UiColor(e.Fill) ?? back;
                var keyInk = UiColor(kb.TextColor) ?? UiColor(e.TextColor);
                var keyFocus = UiColor(kb.FocusTextColor) ?? UiColor(e.FocusTextColor);
                var keyFocusFill = kb.FocusFill.Length > 0 ? kb.FocusFill : e.FocusFill;
                optionTables.Append($"const agb::ui::KeyboardDef kKeyboard{id}_{list.IndexOf(e)} = {{{kb.X}, {kb.Y}, {kb.W}, {kb.H}, {C(kb.Fill.Length > 0 ? kb.Fill : e.Fill)}, {C(kb.Border.Length > 0 ? kb.Border : e.Border)}, ");
                optionTables.Append($"{(keyInk is { } ki ? palette.Ink(ki, keyBack) : 0)}, {C(keyFocusFill)}, {(keyFocus is { } kf ? palette.Ink(kf, UiColor(keyFocusFill) ?? keyBack) : 0)}}};\n");
                if ((kb.W - 8) / 13 < 8 || (kb.H - 6) / 6 < 8)
                    Add(new Diagnostic(Severity.Warning, file, 0, 0, "", $"{e.Name}: the keyboard is {kb.W} x {kb.H} px, too small for its keys. Make it at least 112 x 54 px."));
            }
            sb.Append($"{optionsSymbol}, {e.Options.Count}, {Math.Clamp(e.MaxLength, 0, 12)}, {keyboardSymbol}}},\n");
            CheckTextFits(file, pageNames[pageIndex], e, r);
        }
        sb.Append("};\n");
        sb = pageBody;
        sb.Append(optionTables).Append(elementRows);

        // The panel: whatever draws, out to whole tiles and cut to the screen.
        var drawn = list.Where(UiDraws).Select(e => rects[e]).ToList();
        // An open Dropdown list and the Input Field keyboard draw in the panel too.
        foreach (var e in list.Where(e => e.Kind == UiElementKind.Dropdown))
        {
            var r = rects[e];
            var h = e.Options.Count * r.H + 2;
            drawn.Add(r.Bottom + h > UiLayoutEngine.ScreenH ? new UiPixelRect(r.X, r.Y - h, r.W, h) : new UiPixelRect(r.X, r.Bottom, r.W, h));
        }
        foreach (var e in list.Where(e => e.Kind == UiElementKind.InputField))
        {
            var kb = e.Keyboard ?? new UiKeyboard();
            drawn.Add(new UiPixelRect(kb.X, kb.Y, kb.W, kb.H));
        }
        if (list.Count(e => e.Kind == UiElementKind.InputField) > 1)
            Add(new Diagnostic(Severity.Warning, file, 0, 0, "", $"UI page {pageNames[pageIndex]} has more than one Input Field; a page keeps one typed text. Use one Input Field a page."));
        if (list.Count(e => e.Kind == UiElementKind.ScrollRect) > 2)
            Add(new Diagnostic(Severity.Warning, file, 0, 0, "", $"UI page {pageNames[pageIndex]} has more than two Scroll Rects, and the others do not scroll. Move one to another page."));
        int x0 = 0, y0 = 0, x1 = 8, y1 = 8;
        if (drawn.Count > 0)
        {
            x0 = Math.Clamp(drawn.Min(r => r.X), 0, UiLayoutEngine.ScreenW - 1) / 8;
            y0 = Math.Clamp(drawn.Min(r => r.Y), 0, UiLayoutEngine.ScreenH - 1) / 8;
            x1 = (Math.Clamp(drawn.Max(r => r.Right), 1, UiLayoutEngine.ScreenW) + 7) / 8;
            y1 = (Math.Clamp(drawn.Max(r => r.Bottom), 1, UiLayoutEngine.ScreenH) + 7) / 8;
            if (x1 <= x0) x1 = x0 + 1;
            if (y1 <= y0) y1 = y0 + 1;
        }
        _largestPageBytes = Math.Max(_largestPageBytes, (x1 - x0) * (y1 - y0) * 32L);
        var first = page.FirstFocus.Length > 0 ? IndexOf(page.FirstFocus) : list.FindIndex(e => e.IsSelectable);
        _uiPageRows.Add($"    {{{Cpp.Str(pageNames[pageIndex])}, kElements{id}, {list.Count}, {x0}, {y0}, {x1 - x0}, {y1 - y0}, {first}, {(page.Cancel ? "true" : "false")}, {(page.ScenePlays ? "true" : "false")}, {Math.Clamp(page.Dim, 0, 16)}}},\n");
    }
}
