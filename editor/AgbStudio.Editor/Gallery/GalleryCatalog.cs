using AgbStudio.Editor.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;

namespace AgbStudio.Editor.Gallery;

/// <summary>One variant of a Figma component, with the id used by SHOT (docs/evidence/figma/&lt;Id&gt;.png).</summary>
public sealed record GalleryItem(string Id, string Set, string Variant, double Width, double Height, Func<Control> Create);

/// <summary>
/// Every buildable component (checklist 4.16) in every Figma variant and state, with the default texts of the Figma
/// component properties. Hover / Pressed / Focus / Open states are shown by setting the pseudo-class the real
/// interaction sets. Used by the Component Gallery window and the SHOT comparison.
/// </summary>
public static class GalleryCatalog
{
    /// <summary>Sets a state pseudo-class on a control, or on a named part of its template.</summary>
    public static T Force<T>(T c, string pseudo, string? part = null) where T : Control
    {
        if (part is null) { ((IPseudoClasses)c.Classes).Add(pseudo); return c; }
        if (c is Avalonia.Controls.Primitives.TemplatedControl tc)
            tc.TemplateApplied += (_, e) => { if (e.NameScope.Find<Control>(part) is { } p) ((IPseudoClasses)p.Classes).Add(pseudo); };
        return c;
    }

    static string Slug(string s) => new string(s.ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray()).Trim('-').Replace("--", "-");

    static GalleryItem I(string set, string variant, double w, double h, Func<Control> f) =>
        new("C-" + Slug(set) + (variant.Length > 0 ? "-" + Slug(variant) : ""), set, variant, w, h, f);

    public static IReadOnlyList<GalleryItem> Items { get; } = Build().ToList();

    static IEnumerable<GalleryItem> Build()
    {
        // Menus
        foreach (var (kind, state) in new[] { ("Default", "Default"), ("Default", "Hover"), ("Default", "Disabled"), ("Submenu", "Default"), ("Submenu", "Hover"), ("Checked", "Default"), ("Danger", "Default") })
            yield return I("Menu Item", $"{kind} {state}", 248, 26, () =>
            {
                var m = new MenuItem { Header = "Menu item", InputGesture = new KeyGesture(Key.K, KeyModifiers.Control) };
                if (kind == "Submenu") m.Items.Add(new MenuItem { Header = "-" });
                if (kind == "Checked") { m.ToggleType = MenuItemToggleType.CheckBox; m.IsChecked = true; }
                if (kind == "Danger") m.Classes.Add("danger");
                if (state == "Hover") Force(m, ":selected");
                if (state == "Disabled") m.IsEnabled = false;
                return m;
            });
        yield return I("Menu Separator", "", 248, 9, () => new Separator());
        foreach (var st in new[] { "Default", "Open" })
            yield return I("Menu Bar Item", st, 36, 24, () =>
            {
                var m = new MenuItem { Header = "File", Theme = (Avalonia.Styling.ControlTheme?)Application.Current!.FindResource("MenuBarItem") };
                return st == "Open" ? Force(m, ":open") : m;
            });

        // Buttons and controls
        foreach (var state in new[] { "Default", "Hover", "Pressed", "Disabled" })
            foreach (var size in new[] { ButtonSize.SM, ButtonSize.LG })
                foreach (var kind in new[] { ButtonKind.Primary, ButtonKind.Secondary, ButtonKind.Ghost, ButtonKind.Danger })
                    yield return I("Button", $"{kind} {size} {state}", kind == ButtonKind.Secondary ? (size == ButtonSize.SM ? 80 : 90) : (size == ButtonSize.SM ? 78 : 88), size == ButtonSize.SM ? 26 : 32, () =>
                    {
                        var b = new AgbButton { Content = "Button", Glyph = "zap", Kind = kind, Size = size, HorizontalAlignment = HorizontalAlignment.Left };
                        return state switch { "Hover" => Force(b, ":pointerover"), "Pressed" => Force(b, ":pressed"), "Disabled" => Disable(b), _ => b };
                    });
        foreach (var state in new[] { "Default", "Active", "Disabled", "Hover", "Pressed" })
            yield return I("Icon Button", state, 28, 26, () =>
            {
                var b = new IconButton { Glyph = "cursor", IsActive = state == "Active" };
                return state switch { "Hover" => Force(b, ":pointerover"), "Pressed" => Force(b, ":pressed"), "Disabled" => Disable(b), _ => b };
            });
        foreach (var k in Enum.GetValues<PlayKind>())
            yield return I("Play Control", k.ToString(), k is PlayKind.Play or PlayKind.Stop ? 44 : 34, 30, () => new PlayControl { Kind = k });
        foreach (var st in new[] { "On", "Off", "Active" })
            yield return I("Layer Toggle", st, 53, 26, () => new LayerToggle { Content = "BG0", IsOn = st != "Off", IsActive = st == "Active", HorizontalAlignment = HorizontalAlignment.Left });
        foreach (var st in Enum.GetValues<ChipStyle>())
            yield return I("Chip", st.ToString(), st == ChipStyle.Default ? 62 : 60, 24, () => new Chip { Content = "Chip", Glyph = "chip", Style = st, HorizontalAlignment = HorizontalAlignment.Left });
        yield return I("Badge", "", 17, 16, () => new Badge { Label = "2", HorizontalAlignment = HorizontalAlignment.Left });
        foreach (var st in new[] { "On", "Off" })
            yield return I("Switch", st, 32, 18, () => new AgbSwitch { IsChecked = st == "On" });
        foreach (var st in new[] { "On", "Off" })
            yield return I("Checkbox", st, 60, 16, () => new CheckBox { Content = "Option", IsChecked = st == "On", HorizontalAlignment = HorizontalAlignment.Left });
        foreach (var st in new[] { "Default", "Active", "Hover" })
            yield return I("Segment", st, 60, 24, () =>
            {
                var s = new Segment { Content = "Mode 0", IsChecked = st == "Active", GroupName = Guid.NewGuid().ToString(), HorizontalAlignment = HorizontalAlignment.Left };
                return st == "Hover" ? Force(s, ":pointerover") : s;
            });
        foreach (var st in Enum.GetValues<ProgressStatus>())
            for (var v = 0; v <= 100; v += 10)
            {
                var value = v;
                yield return I("Progress", $"{st} {v}", 160, 6, () => new AgbProgress { Status = st, Value = value });
            }
        for (var v = 0; v <= 100; v += 10)
        {
            var value = v;
            yield return I("Slider", v.ToString(), 160, 12, () => new AgbSlider { Value = value });
        }
        yield return I("Keycap", "", 43, 22, () => new Keycap { Label = "Ctrl", HorizontalAlignment = HorizontalAlignment.Left });

        // Navigation
        yield return I("Panel Header", "", 260, 34, () => new PanelHeader { Title = "Hierarchy", Glyph = "layers", ActionGlyph = "plus" });
        yield return I("Search Field", "", 240, 28, () => new SearchField { Watermark = "Search…" });
        foreach (var st in new[] { "Default", "Selected", "Hover" })
            foreach (var tg in Enum.GetValues<TreeToggle>())
                yield return I("Tree Item", $"{st} {tg}", 248, 26, () =>
                {
                    var t = new TreeItem { Label = "Actor", Glyph = "cube", Toggle = tg, IsSelected = st == "Selected" };
                    return st == "Hover" ? Force(t, ":pointerover") : t;
                });
        foreach (var st in new[] { "Default", "Active", "Hover" })
            yield return I("Doc Tab", st, st == "Active" ? 84 : 85, 34, () =>
            {
                var t = new DocTab { Label = "Scene", Glyph = "map", IsSelected = st == "Active", HorizontalAlignment = HorizontalAlignment.Left };
                return st == "Hover" ? Force(t, ":pointerover") : t;
            });
        foreach (var st in new[] { "Default", "Active", "Hover" })
            yield return I("Panel Tab", st, 82, 32, () =>
            {
                var t = new PanelTab { Label = "Assets", Glyph = "folder", IsSelected = st == "Active", HorizontalAlignment = HorizontalAlignment.Left };
                return st == "Hover" ? Force(t, ":pointerover") : t;
            });
        foreach (var st in new[] { "Default", "Selected", "Hover" })
            yield return I("List Item", st, 158, 24, () =>
            {
                var t = new ListRow { Label = "Sprites", Glyph = "image", Meta = "12", IsSelected = st == "Selected" };
                return st == "Hover" ? Force(t, ":pointerover") : t;
            });
        foreach (var st in new[] { "Default", "Active", "Hover" })
            yield return I("Nav Item", st, 200, 32, () =>
            {
                var t = new NavItem { Label = "General", Glyph = "sliders", IsSelected = st == "Active" };
                return st == "Hover" ? Force(t, ":pointerover") : t;
            });

        // Inspector
        foreach (var st in new[] { "Open", "Closed" })
            yield return I("Section Header", st, 296, 24, () => new SectionHeader { Title = "Transform", Glyph = "move", IsOpen = st == "Open" });
        foreach (var k in Enum.GetValues<FieldKind>())
            yield return I("Field", k.ToString(), 296, 24, () => new InspectorField { Kind = k, Label = "Label", Value = "value", Value2 = "112", SliderValue = 50 });
        yield return I("Meter", "", 296, 18, () => new Meter { Label = "VRAM BG", ValueText = "22 / 64 KB", Percent = 30 });
        foreach (var st in Enum.GetValues<SwatchState>())
            yield return I("Swatch", st.ToString(), 30, 30, () => new Swatch { State = st, Index = "4", Color = DemoRed });

        // Feedback
        foreach (var dot in new[] { false, true })
            yield return I("Status Item", dot ? "Dot" : "Default", dot ? 46 : 51, 26, () => new StatusItem { Content = "Ready", IsDot = dot, HorizontalAlignment = HorizontalAlignment.Left });
        foreach (var lv in Enum.GetValues<ConsoleLevel>())
            yield return I("Console Line", lv.ToString(), 560, 20, () => new ConsoleLine { Time = "12:04:31", Text = "Build OK in 0.80 s", Level = lv });
        foreach (var st in Enum.GetValues<ToastStatus>())
            yield return I("Toast", st.ToString(), 340, 38, () => new Toast { Text = "sky_hopper.gba built · 412 KB", Status = st });
        yield return I("Tooltip", "", 61, 26, () => new ToolTip { Content = new TipContent("Play", "F5"), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top });

        // Chrome
        yield return I("Title Bar", "", 1440, 36, () =>
        {
            var menu = new Menu();
            foreach (var t in new[] { "File", "Edit", "Assets", "Scene", "Build", "Tools", "Help" }) menu.Items.Add(new MenuItem { Header = t });
            return new TitleBar { Project = "Sky Hopper", Document = "Forest_01.scene", Menus = menu };
        });

        // Cards
        foreach (var sel in new[] { false, true })
            yield return I("Asset Card", sel ? "Selected" : "Default", 102, 98, () => new AssetCard { NameText = "hero.png", Meta = "16×16 · 8 frames", IsSelected = sel, VerticalAlignment = VerticalAlignment.Top });
        foreach (var sel in new[] { false, true })
            yield return I("Frame Thumb", sel ? "Active" : "Default", 66, 83, () => new FrameThumb { Label = "1 · 6t", IsSelected = sel, VerticalAlignment = VerticalAlignment.Top });
        foreach (var sel in new[] { false, true })
            yield return I("Scene Card", sel ? "Selected" : "Default", 166, sel ? 150 : 148, () => new SceneCard { NameText = "Forest_01", Meta = "30×20", IsSelected = sel, VerticalAlignment = VerticalAlignment.Top });
        foreach (var sel in new[] { false, true })
            yield return I("Template Card", sel ? "Selected" : "Default", 252, sel ? 240 : 238, () => new TemplateCard { Title = "Platformer", Description = "Run, jump and collect. Gravity, slopes, one-way platforms.", IsSelected = sel, VerticalAlignment = VerticalAlignment.Top });
        foreach (var st in Enum.GetValues<GlyphCellState>())
            yield return I("Glyph Cell", st.ToString(), 44, 48, () => new GlyphCell { Char = "ก", Code = "0E01", State = st });

        // Tables and forms
        foreach (var k in Enum.GetValues<TableCellKind>())
            yield return I("Table Cell", k.ToString(), 120, k == TableCellKind.Header ? 28 : 32, () => new TableCell { Kind = k, Text = "Potion" });
        foreach (var st in new[] { "Default", "Focus" })
            yield return I("Text Input", st, 280, 52, () =>
            {
                var f = new TextField { Label = "Project name", Text = "Sky Hopper", VerticalAlignment = VerticalAlignment.Top };
                return st == "Focus" ? Force(f, ":focus", "PART_Box") : f;
            });
        foreach (var st in new[] { "Default", "Focus" })
            yield return I("Select", st, 280, 52, () =>
            {
                var f = new SelectField { Label = "Project name", Selected = "SRAM · 32 KB", VerticalAlignment = VerticalAlignment.Top };
                return st == "Focus" ? Force(f, ":focus-within") : f;
            });
        foreach (var st in new[] { "Default", "Active" })
            yield return I("Tab", st, 49, 34, () => new Tab { Content = "General", IsChecked = st == "Active", GroupName = Guid.NewGuid().ToString(), HorizontalAlignment = HorizontalAlignment.Left });
        yield return I("Dialog Header", "", 640, 52, () => new DialogHeader { Title = "New Project", Glyph = "folderplus" });
        yield return I("Dialog Footer", "", 640, 60, () => new DialogFooter { Hint = "You can change this later in Project Settings.", CancelText = "Cancel", ConfirmText = "Create Project" });
        yield return I("Popover Header", "", 320, 40, () => new PopoverHeader { Title = "Video Mode" });

        // Logic graph
        foreach (var c in Enum.GetValues<NodeCategory>())
            yield return I("Node Header", c.ToString(), 200, 30, () => new NodeHeader { Category = c, Title = "On Update" });
        // the Figma pin variants (Vector pins came later and have no Figma frame)
        foreach (var t in Enum.GetValues<PinType>().Where(t => t != PinType.Vector))
            foreach (var d in Enum.GetValues<PinDir>())
                yield return I("Node Pin", $"{t} {d}", t == PinType.Exec ? 12 : 10, t == PinType.Exec ? 12 : 10, () => new NodePin { Type = t, Dir = d });

        // Brand
        yield return I("brand/mark", "", 96, 96, () => new BrandMark());
        foreach (var v in Enum.GetValues<AppIconTheme>())
            yield return I("brand/app-icon", v.ToString(), 128, 128, () => new AppIcon { Variant = v });
    }

    /// <summary>Palette colour of the Figma swatch sample (game data, not a UI token).</summary>
    static readonly Avalonia.Media.Color DemoRed = Avalonia.Media.Color.FromRgb(0xFF, 0x4F, 0x6D);

    static T Disable<T>(T c) where T : Control { c.IsEnabled = false; return c; }
}
