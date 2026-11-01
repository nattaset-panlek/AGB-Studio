using System.Globalization;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Graphics;
using AgbStudio.Editor.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;

namespace AgbStudio.Editor.Windows.SceneEditor;

/// <summary>
/// Inspector (T-P01, P11.13): the selected actor's header (name, layer and OBJ priority), the Components list (every
/// part of the actor at a glance), Transform, Sprite (sheet via
/// Asset Picker T-O04, animation via Animation List T-M16, frames, palette to Palette Manager T-P10), its behaviours
/// with their fields and the Collider, script fields (double click the header to open the Script Editor), Add
/// Component (T-O05) and the GBA Budget meters (click for Budget Details T-O06). Every change is an undoable edit of
/// the .scene file; typing and slider drags merge into one undo step. With several actors selected, the first one's
/// fields are shown read-only under "N actors selected".
/// </summary>
[EditorWindow("T-P01:Inspector", Kind = WindowKind.Panel)]
public sealed class InspectorPanel : Panel
{
    readonly EditorServices _s;
    readonly SceneEditorModel _m;
    readonly StackPanel _content = new() { Name = "InspectorContent" };
    readonly List<Action> _refresh = [];
    string _structure = "";

    public InspectorPanel(WindowContext ctx) : this(ctx.Services) { }

    public InspectorPanel(EditorServices s)
    {
        _s = s;
        _m = SceneEditorModel.Of(s);
        Name = "InspectorPanel";
        Children.Add(new ScrollViewer { Content = _content, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden });
        _m.Changed += Update;
        _m.SelectionChanged += Update;
        s.Build.Finished += _ => Avalonia.Threading.Dispatcher.UIThread.Post(Rebuild);
        Rebuild();
    }

    /// <summary>Rebuilds when what is shown changes; otherwise refreshes the values (keeps the field being typed in).</summary>
    void Update()
    {
        if (StructureKey() != _structure) Rebuild();
        else foreach (var r in _refresh) r();
    }

    string StructureKey()
    {
        var sel = _m.Selection;
        var a = _m.SelectedActor;
        return $"{_m.ScenePath}|{sel}|{_m.SelectedActorIndices.Count}|{a?.Kind}|{a?.Sprite}|{a?.Animation}|{string.Join(",", a?.Components.Select(c => c.Type) ?? [])}|{_m.Map?.Layers.Count}";
    }

    public void Rebuild()
    {
        _structure = StructureKey();
        _refresh.Clear();
        _content.Children.Clear();
        var sel = _m.Selection;
        if (_m.Scene == null) return;
        if (_m.SelectedActor is { } actor && _m.SelectedActorIndices.Count is var n and > 1) BuildActors(actor, _m.SelectedActorIndex, n);
        else if (_m.SelectedActor is { } one) BuildActor(one, _m.SelectedActorIndex);
        else if (sel is { Kind: SceneItemKind.Layer } l && _m.Map?.Layers.ElementAtOrDefault(l.Index) is { } layer) BuildLayer(layer, l.Index);
        else if (sel is { Kind: SceneItemKind.Camera }) BuildCamera();
        else BuildScene(sel);
        _content.Children.Add(BudgetSection());
    }

    // ---- layout helpers -------------------------------------------------------------------------------

    // Figma: 8 px above the header, 10 px under the last row (Transform 73 px with its divider)
    static Border Section(Control child, Thickness? pad = null) =>
        UI.Frame(child, null, "border/default", new Thickness(0, 0, 0, 1), pad ?? new Thickness(12, 8, 12, 10));

    StackPanel SectionBody(SectionHeader header, IEnumerable<Control> rows)
    {
        var body = UI.Column(6, rows.ToArray());
        var col = UI.Column(6, header, body);
        header.PropertyChanged += (_, e) => { if (e.Property == SectionHeader.IsOpenProperty) body.IsVisible = header.IsOpen; };
        return col;
    }

    void Refresh(Action a)
    {
        _refresh.Add(a);
        a();
    }

    static bool Editing(Control c) => c.IsKeyboardFocusWithin;

    // ---- actor ----------------------------------------------------------------------------------------

    void BuildActor(ActorData a, int index)
    {
        _content.Children.Add(ActorHeader(a, index));
        _content.Children.Add(Section(ComponentList(a)).Named("ComponentList"));
        _content.Children.Add(Section(TransformSection(index)));
        var assets = _m.Assets;
        var sheet = a.Sprite.Length > 0 ? assets?.Sprite(a.Sprite) : null;
        if (a.Kind is not (ActorKind.TriggerZone or ActorKind.CameraZone or ActorKind.TextLabel) || a.Sprite.Length > 0)
            _content.Children.Add(Section(SpriteSection(a, index, sheet)));
        var colliderPlaced = false;
        for (var c = 0; c < a.Components.Count; c++)
        {
            var physics = !colliderPlaced && Behaviours.Builtins.FirstOrDefault(b => b.Name == a.Components[c].Type)?.Category == "Physics";
            colliderPlaced |= physics;
            _content.Children.Add(Section(ComponentSection(a, index, c, physics)));
        }
        if (!colliderPlaced && (a.Sprite.Length > 0 || a.Collider != null))
            _content.Children.Add(Section(SectionBody(new SectionHeader { Title = "Collider", Glyph = "square" }, [ColliderField(index)])));
        _content.Children.Add(Section(AddComponentButton(), new Thickness(12, 10, 12, 11)));
    }

    /// <summary>
    /// Several actors selected (Hierarchy Ctrl / Shift + click, Select All): Figma has no Inspector state for it, so the
    /// first selected actor's fields are shown read-only under "N actors selected"; the Edit menu acts on all of them.
    /// </summary>
    void BuildActors(ActorData first, int index, int count)
    {
        var start = _content.Children.Count;
        BuildActor(first, index);
        var built = _content.Children.Skip(start).ToList();
        _content.Children.RemoveRange(start, built.Count);
        var icon = new Icon { Glyph = "layers", Size = 14, VerticalAlignment = VerticalAlignment.Center }.Token(Icon.ForegroundProperty, "accent/default");
        var text = UI.Text($"{count} actors selected", "Body/12 Semibold", "text/primary");
        text.Name = "MultiSelectionText";
        _content.Children.Add(Section(UI.Row(8, icon, text)).Named("MultiSelection"));
        var fields = new StackPanel { IsEnabled = false, Name = "MultiSelectionFields" };
        fields.Children.AddRange(built);
        _content.Children.Add(fields);
    }

    Control ActorHeader(ActorData a, int index)
    {
        var avatar = UI.Frame(PixelArt.View(a.Sprite.Length > 0 ? PixelArt.SpriteBitmap(_m.Assets, a.Sprite, FirstFrame(a)) : null, 32, 32), "bg/base", "border/default", null, null, 8);
        avatar.Width = avatar.Height = 40;
        avatar.Name = "ActorAvatar";
        if (a.Sprite.Length == 0) avatar.Child = new Icon { Glyph = HierarchyPanel.ActorGlyph(a), Size = 18, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }.Token(Icon.ForegroundProperty, "text/secondary");
        var name = UI.Text(a.Name, "Title/14", "text/primary");
        name.Name = "ActorName";
        var chip = new Chip { Content = "ACTOR", Style = ChipStyle.Accent, Name = "ActorChip", IsHitTestVisible = false };
        var prio = new Button { Theme = (Avalonia.Styling.ControlTheme?)Application.Current?.FindResource("BareButton"), Name = "PriorityButton", VerticalAlignment = VerticalAlignment.Center };
        var prioText = UI.Text($"Layer OBJ · Prio {a.Priority}", "Label/11", "text/muted");
        prio.Content = prioText;
        ToolTip.SetTip(prio, new TipContent("OBJ priority 0-3 (click to change)"));
        prio.Click += (_, _) => _m.Edit("Change Priority", () => _m.Scene!.Actors[index].Priority = (_m.Scene.Actors[index].Priority + 1) & 3);
        Refresh(() => { name.Text = _m.Scene!.Actors[index].Name; prioText.Text = $"Layer OBJ · Prio {_m.Scene.Actors[index].Priority}"; });
        name.Margin = new Thickness(0, -2, 0, 0);
        var text = UI.Column(4, name, UI.Row(8, chip, prio));
        text.VerticalAlignment = VerticalAlignment.Top;
        avatar.VerticalAlignment = VerticalAlignment.Top;
        var row = UI.Row(10, avatar, text);
        return UI.Frame(row, null, "border/default", new Thickness(0, 0, 0, 1), new Thickness(12, 14, 12, 12)).Named("ActorHeader");
    }

    int FirstFrame(ActorData a) =>
        _m.Assets?.Sprite(a.Sprite) is { } s ? GbaScreenRenderer.FirstFrame(s, _m.Assets.SpriteMeta(a.Sprite), a.Animation) : 0;

    Control TransformSection(int index)
    {
        var pos = new InspectorField { Kind = FieldKind.XY, Label = "Position", Name = "Field_Position" };
        Refresh(() =>
        {
            if (Editing(pos)) return;
            var a = _m.Scene!.Actors[index];
            pos.Value = a.X.ToString(CultureInfo.InvariantCulture);
            pos.Value2 = a.Y.ToString(CultureInfo.InvariantCulture);
        });
        pos.PropertyChanged += (_, e) =>
        {
            if (!Editing(pos) || (e.Property != InspectorField.ValueProperty && e.Property != InspectorField.Value2Property)) return;
            if (int.TryParse(pos.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) && int.TryParse(pos.Value2, NumberStyles.Integer, CultureInfo.InvariantCulture, out var y))
                _m.MoveActor(index, x, y, merge: true);
        };
        return SectionBody(new SectionHeader { Title = "Transform", Glyph = "move" }, [pos]);
    }

    Control SpriteSection(ActorData a, int index, AgbStudio.Build.Graphics.ConvertedSprite? sheet)
    {
        var rows = new List<Control>();
        var sheetField = new InspectorField { Kind = FieldKind.Dropdown, Label = "Sheet", Value = a.Sprite.Length > 0 ? a.Sprite + ".png" : "None", Name = "Field_Sheet" };
        OnDropdown(sheetField, async anchor =>
        {
            if (await _s.Windows.Open("T-O04", "Sprites", anchor, source: "SheetDropdown") is string path && File.Exists(path))
            {
                var name = GameProject.AssetName(path);
                var anim = _m.Assets?.SpriteMeta(name).Animations.FirstOrDefault()?.Name ?? "";
                _m.Edit("Change Sprite", () => { _m.Scene!.Actors[index].Sprite = name; _m.Scene.Actors[index].Animation = anim; });
            }
        });
        rows.Add(sheetField);
        var animField = new InspectorField { Kind = FieldKind.Dropdown, Label = "Animation", Value = a.Animation.Length > 0 ? a.Animation : "None", Name = "Field_Animation" };
        OnDropdown(animField, anchor => { _ = _s.Windows.Open("T-M16", anchor: anchor, source: "AnimationDropdown"); return Task.CompletedTask; });
        rows.Add(animField);
        if (sheet != null)
        {
            var meta = _m.Assets!.SpriteMeta(a.Sprite);
            var anim = meta.Animations.FirstOrDefault(x => x.Name == a.Animation);
            var frames = (anim?.Frames.Where(f => f < sheet.FrameCount).ToList() is { Count: > 0 } fs ? fs : Enumerable.Range(0, sheet.FrameCount).ToList()).Take(4).ToList();
            var strip = UI.Row(5);
            strip.Name = "FrameStrip";
            for (var i = 0; i < frames.Count; i++)
            {
                var frame = UI.Frame(PixelArt.Fit(PixelArt.SpriteBitmap(_m.Assets, a.Sprite, frames[i]), 26, 26), "bg/base", i == 0 ? "accent/default" : "border/default", null, null, 6);
                frame.Width = frame.Height = 34;
                strip.Children.Add(frame);
            }
            rows.Add(Labelled("Frames", strip, 34));
            var swatches = UI.Row(1);
            swatches.Name = "PaletteSwatches";
            // the colours the sheet uses (index 0 is transparent)
            for (var i = 1; i <= Math.Min(15, sheet.ColorsUsed); i++)
                swatches.Children.Add(new Border { Width = 15, Height = 15, CornerRadius = new CornerRadius(3), Background = new SolidColorBrush(PixelArt.Color(sheet.Palette[i])) });
            var paletteButton = new Button { Theme = (Avalonia.Styling.ControlTheme?)Application.Current?.FindResource("BareButton"), Content = swatches, Name = "PaletteButton" };
            paletteButton.Click += (_, _) => _ = _s.Windows.Open("T-P10", _m.Assets.SpritePath(a.Sprite), source: "InspectorPalette");
            rows.Add(Labelled("Palette", paletteButton, 15));
        }
        return SectionBody(new SectionHeader { Title = "Sprite", Glyph = "image", Name = "Section_Sprite" }, rows);
    }

    /// <summary>
    /// Components (not in Figma, docs/evidence/ui-changes-since-figma.md "Templates"): one row per part of the actor
    /// (sprite, animation, behaviours, collider, text, trigger zone, camera, links) with what kind it is, so the parts a
    /// template uses are named in one place. A click on a row scrolls to the section that edits it.
    /// </summary>
    Control ComponentList(ActorData a)
    {
        var parts = ActorComponents.Of(a, _m.Scene);
        // compact pills that wrap: icon and name; scripts in the accent colour, the kind in the tooltip
        var wrap = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = 4, LineSpacing = 4, Name = "ComponentPills" };
        foreach (var part in parts)
        {
            var script = part.Kind == "script";
            var icon = new Icon { Glyph = part.Glyph, Size = 11, VerticalAlignment = VerticalAlignment.Center }.Token(Icon.ForegroundProperty, script ? "accent/default" : "text/secondary");
            var title = UI.Text(part.Title, "Label/11", "text/primary");
            title.VerticalAlignment = VerticalAlignment.Center;
            var pill = UI.Frame(UI.Row(4, icon, title), "bg/base", script ? "accent/default" : "border/default", new Thickness(1), new Thickness(6, 2, 7, 2), 9);
            var row = new Button
            {
                Theme = (Avalonia.Styling.ControlTheme?)Application.Current?.FindResource("BareButton"), Content = pill, Name = "Component_" + part.Title.Replace(' ', '_'),
                Tag = part.Kind,
            };
            ToolTip.SetTip(row, new TipContent(part.Kind switch
            {
                "script" => $"Script behaviour: scripts/{part.Title}.cs (double click its section header to open it)",
                "built-in" => "Built-in behaviour of the runtime",
                "sprite" => $"Sprite sheet {a.Sprite}.png",
                "animation" => "Animation that plays when the scene starts",
                "collider" => "Hit box used by OnTouch and blocking",
                "text" => "Text label: a string key from Localization",
                "zone" => "Invisible area that reacts when the player enters it",
                "camera" => "The scene camera (Main Camera) follows this actor",
                "link" => "Touching this actor loads another scene (Link Settings)",
                _ => part.Title,
            }));
            row.Click += (_, _) =>
            {
                if (part.Section is { } name && _content.GetLogicalDescendants().OfType<Control>().FirstOrDefault(c => c.Name == name) is { } target)
                    target.BringIntoView();
            };
            wrap.Children.Add(row);
        }
        Control body = parts.Count == 0 ? UI.Text("No components yet: use Add Component below.", "Body/12", "text/muted") : wrap;
        var header = new SectionHeader { Title = "Components", Glyph = "layers", Meta = parts.Count.ToString(CultureInfo.InvariantCulture), Name = "Section_Components" };
        return SectionBody(header, [body]);
    }

    static Control Labelled(string label, Control value, double height)
    {
        var dock = new DockPanel { HorizontalSpacing = 8, Height = height };
        var t = UI.Text(label, "Body/12", "text/secondary", 86);
        DockPanel.SetDock(t, Dock.Left);
        value.HorizontalAlignment = HorizontalAlignment.Left;
        value.VerticalAlignment = VerticalAlignment.Center;
        dock.Children.AddRange([t, value]);
        return dock;
    }

    static void OnDropdown(InspectorField f, Func<Control, Task> open) =>
        f.TemplateApplied += (_, e) =>
        {
            if (e.NameScope.Find<Button>("PART_Dropdown") is { } b) b.Click += async (_, _) => await open(b);
        };

    Control ComponentSection(ActorData a, int index, int ci, bool withCollider)
    {
        var comp = a.Components[ci];
        var def = Behaviours.Find(comp.Type, _m.Project, _s.Build.Scripts.Last);
        var script = def?.Script ?? BuildBuiltin(comp.Type) == null;
        var header = new SectionHeader
        {
            Title = def?.Title ?? comp.Type, Glyph = def?.Glyph ?? "code", Meta = script ? "script" : null, Name = "Section_" + comp.Type,
        };
        // a click marks the component for the Component Menu; a double click on a script opens the Script Editor (#31)
        header.AddHandler(PointerPressedEvent, (_, e) =>
        {
            _m.SelectedComponent = ci;
            if (e.ClickCount == 2 && script && SceneCommands.ScriptFile(_m, comp.Type) is { } file)
            {
                _ = Scripts.ScriptEditorPage.OpenScriptFile(_s, file, source: "ScriptComponent");
                header.IsOpen = true;   // the first click of the double click folded it
                e.Handled = true;
            }
        }, RoutingStrategies.Tunnel);
        var rows = new List<Control>();
        var fields = (def?.Fields ?? comp.Fields.Keys.Select(k => new BehaviourField(k, k, FieldType.Text, "", 0, 4, false)).ToList())
            .Where(f => !Behaviours.HiddenInInspector(comp.Type, f.Name) || comp.Fields.ContainsKey(f.Name));
        foreach (var f in fields) rows.Add(FieldRow(index, ci, f));
        if (withCollider) rows.Add(ColliderField(index));
        var section = SectionBody(header, rows);
        section.AddHandler(PointerPressedEvent, (_, _) => _m.SelectedComponent = ci, RoutingStrategies.Tunnel);
        return section;
    }

    static object? BuildBuiltin(string type) => AgbStudio.Build.Pipeline.BuiltinBehaviours.Find(type);

    Control FieldRow(int actor, int ci, BehaviourField f)
    {
        string Current() => _m.Scene!.Actors[actor].Components.ElementAtOrDefault(ci)?.Fields.GetValueOrDefault(f.Name) ?? f.Default;
        var field = new InspectorField { Label = f.Label, Name = "Field_" + f.Name };
        switch (f.Type)
        {
            case FieldType.Bool:
                field.Kind = FieldKind.Toggle;
                Refresh(() => field.IsOn = Current().Equals("true", StringComparison.OrdinalIgnoreCase));
                field.PropertyChanged += (_, e) =>
                {
                    if (e.Property == InspectorField.IsOnProperty && (field.IsOn ? "true" : "false") != Current().ToLowerInvariant())
                        _m.SetComponentField(actor, ci, f.Name, field.IsOn ? "true" : "false");
                };
                break;
            case FieldType.Fixed or FieldType.Int when f.Slider:
                field.Kind = FieldKind.Slider;
                var syncing = false;
                Refresh(() =>
                {
                    syncing = true;
                    field.Value = Behaviours.Display(f, Current());
                    if (double.TryParse(Current(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                        field.SliderValue = Math.Clamp((v - f.Min) / (f.Max - f.Min) * 100, 0, 100);
                    syncing = false;
                });
                field.PropertyChanged += (_, e) =>
                {
                    if (syncing || e.Property != InspectorField.SliderValueProperty) return;
                    var raw = f.Min + field.SliderValue / 100 * (f.Max - f.Min);
                    var v = f.Type == FieldType.Int ? Math.Round(raw) : Math.Round(raw * 256) / 256;   // whole numbers, or fixed 24.8 steps
                    _m.SetComponentField(actor, ci, f.Name, v.ToString(f.Type == FieldType.Int ? "0" : "0.00###", CultureInfo.InvariantCulture));
                };
                break;
            default:
                field.Kind = FieldKind.Text;
                Refresh(() => { if (!Editing(field)) field.Value = Current(); });
                field.PropertyChanged += (_, e) =>
                {
                    if (e.Property != InspectorField.ValueProperty || !Editing(field) || field.Value == Current()) return;
                    if ((f.Type is FieldType.Fixed or FieldType.Int) && !double.TryParse(field.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double _)) return;
                    _m.SetComponentField(actor, ci, f.Name, field.Value ?? "");
                };
                break;
        }
        return field;
    }

    Control ColliderField(int index)
    {
        var f = new InspectorField { Kind = FieldKind.XY, Label = "Collider", Name = "Field_Collider" };
        ColliderData Current()
        {
            var a = _m.Scene!.Actors[index];
            return a.Collider ?? (a.Sprite.Length > 0 && _m.Assets != null ? _m.Assets.SpriteMeta(a.Sprite).Collider : new ColliderData());
        }
        Refresh(() =>
        {
            if (Editing(f)) return;
            var c = Current();
            f.Value = $"{c.W} × {c.H}";
            f.Value2 = $"{c.X}, {c.Y}";
        });
        f.PropertyChanged += (_, e) =>
        {
            if (!Editing(f) || (e.Property != InspectorField.ValueProperty && e.Property != InspectorField.Value2Property)) return;
            var size = (f.Value ?? "").Split(['×', 'x', 'X', ','], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var off = (f.Value2 ?? "").Split([',', ' '], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (size.Length == 2 && off.Length == 2 && int.TryParse(size[0], out var w) && int.TryParse(size[1], out var h) && int.TryParse(off[0], out var x) && int.TryParse(off[1], out var y))
                _m.Edit("Change Collider", () => _m.Scene!.Actors[index].Collider = new ColliderData { X = x, Y = y, W = w, H = h }, "collider:" + index);
        };
        return f;
    }

    Control AddComponentButton()
    {
        var label = UI.Row(6, new Icon { Glyph = "plus", Size = 14, VerticalAlignment = VerticalAlignment.Center }.Token(Icon.ForegroundProperty, "text/secondary"),
            UI.Text("Add Component", "Body/12 Medium", "text/secondary"));
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        var dash = new Rectangle { RadiusX = 6, RadiusY = 6, StrokeThickness = 1, StrokeDashArray = [4, 3] }.Token(Shape.StrokeProperty, "border/strong");
        var b = new Button
        {
            Theme = (Avalonia.Styling.ControlTheme?)Application.Current?.FindResource("BareButton"), Height = 26, HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch,
            Content = new Panel { Children = { dash, label } }, Name = "AddComponentButton",
        };
        b.Click += (_, _) => _ = _s.Windows.Open("T-O05", anchor: b, source: "AddComponent");
        return b;
    }

    // ---- other selections -----------------------------------------------------------------------------

    void Header(string title, string glyph, string kind, string meta)
    {
        var icon = UI.Frame(new Icon { Glyph = glyph, Size = 18, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }.Token(Icon.ForegroundProperty, "text/secondary"),
            "bg/base", "border/default", null, null, 8);
        icon.Width = icon.Height = 40;
        var text = UI.Column(4, UI.Text(title, "Title/14", "text/primary"), UI.Row(8, new Chip { Content = kind, Style = ChipStyle.Accent, IsHitTestVisible = false }, UI.Text(meta, "Label/11", "text/muted")));
        text.VerticalAlignment = VerticalAlignment.Center;
        _content.Children.Add(UI.Frame(UI.Row(10, icon, text), null, "border/default", new Thickness(0, 0, 0, 1), new Thickness(12, 14, 12, 16)));
    }

    void BuildScene(SceneItem? sel)
    {
        var sc = _m.Scene!;
        if (sel is { Kind: SceneItemKind.Group } g)
        {
            var n = sc.Actors.Count(a => a.Group == g.Name);
            Header(g.Name, "folder", "GROUP", $"{n} actors");
            return;
        }
        Header(_m.SceneName, "map", "SCENE", $"Mode {sc.VideoMode} · {(_m.Map?.Width ?? sc.Width)}×{(_m.Map?.Height ?? sc.Height)} tiles");
        var settings = new AgbButton { Content = "Scene Settings…", Glyph = "sliders", HorizontalAlignment = HorizontalAlignment.Stretch, Name = "SceneSettingsButton" };
        settings.Click += (_, _) => _ = _s.Windows.Open("T-D03", source: "InspectorScene");
        _content.Children.Add(Section(UI.Column(6,
            new InspectorField { Kind = FieldKind.Text, Label = "Actors", Value = sc.Actors.Count.ToString(CultureInfo.InvariantCulture), IsEnabled = false },
            settings), new Thickness(12, 12, 12, 12)));
    }

    void BuildCamera()
    {
        var sc = _m.Scene!;
        Header("Main Camera", "camera", "CAMERA", sc.Camera.Follow.Length > 0 ? "follows " + sc.Camera.Follow : "fixed");
        var dz = new InspectorField { Kind = FieldKind.XY, Label = "Dead zone", Name = "Field_DeadZone" };
        Refresh(() => { if (!Editing(dz)) { dz.Value = _m.Scene!.Camera.DeadZoneW.ToString(CultureInfo.InvariantCulture); dz.Value2 = _m.Scene.Camera.DeadZoneH.ToString(CultureInfo.InvariantCulture); } });
        dz.PropertyChanged += (_, e) =>
        {
            if (!Editing(dz) || (e.Property != InspectorField.ValueProperty && e.Property != InspectorField.Value2Property)) return;
            if (int.TryParse(dz.Value, out var w) && int.TryParse(dz.Value2, out var h))
                _m.EditScene("Change Dead Zone", (s, _) => { s.Camera.DeadZoneW = Math.Clamp(w, 0, 240); s.Camera.DeadZoneH = Math.Clamp(h, 0, 160); }, "deadzone");
        };
        var clamp = new InspectorField { Kind = FieldKind.Toggle, Label = "Clamp to map", Name = "Field_Clamp" };
        Refresh(() => clamp.IsOn = _m.Scene!.Camera.ClampToMap);
        clamp.PropertyChanged += (_, e) => { if (e.Property == InspectorField.IsOnProperty && clamp.IsOn != _m.Scene!.Camera.ClampToMap) _m.EditScene("Clamp to Map", (s, _) => s.Camera.ClampToMap = clamp.IsOn); };
        _content.Children.Add(Section(SectionBody(new SectionHeader { Title = "Camera", Glyph = "camera" }, [dz, clamp])));
    }

    void BuildLayer(MapLayerData layer, int index)
    {
        Header($"BG{layer.Bg} · {layer.Name}", layer.Type == LayerType.Image ? "image" : "tiles", "LAYER", $"{layer.Type} · Prio {layer.Priority}");
        var p = new InspectorField { Kind = FieldKind.Slider, Label = "Parallax", Name = "Field_Parallax" };
        var syncing = false;
        Refresh(() =>
        {
            syncing = true;
            var l = _m.Map!.Layers[index];
            p.Value = l.ParallaxX.ToString("0.0", CultureInfo.InvariantCulture);
            p.SliderValue = Math.Clamp(l.ParallaxX * 100, 0, 100);
            syncing = false;
        });
        p.PropertyChanged += (_, e) =>
        {
            if (syncing || e.Property != InspectorField.SliderValueProperty) return;
            var v = Math.Round(p.SliderValue) / 100;
            _m.EditScene("Change Parallax", (_, map) => { map!.Layers[index].ParallaxX = v; map.Layers[index].ParallaxY = v; }, "parallax:" + index);
        };
        var open = new AgbButton { Content = "Open in Tilemap Editor", Glyph = "tiles", HorizontalAlignment = HorizontalAlignment.Stretch, Name = "OpenTilemapButton" };
        open.Click += (_, _) => _ = _s.Windows.Open("T-P03", _m.MapPath, source: "InspectorLayer");
        _content.Children.Add(Section(SectionBody(new SectionHeader { Title = "Layer", Glyph = "layers" }, [p, open])));
    }

    // ---- GBA Budget (#33) -----------------------------------------------------------------------------

    Control BudgetSection()
    {
        var status = UI.Row(5, new Ellipse { Width = 7, Height = 7, VerticalAlignment = VerticalAlignment.Center }.Token(Shape.FillProperty, "status/success"),
            UI.Text("all good", "Label/11 Medium", "status/success"));
        status.Name = "BudgetStatus";
        var header = new DockPanel { HorizontalSpacing = 8, Height = 18 };
        var icon = new Icon { Glyph = "chip", Size = 14, VerticalAlignment = VerticalAlignment.Center }.Token(Icon.ForegroundProperty, "text/secondary");
        DockPanel.SetDock(icon, Dock.Left);
        DockPanel.SetDock(status, Dock.Right);
        header.Children.AddRange([icon, status, UI.Text("GBA Budget", "Overline/11", "text/secondary")]);
        var meters = UI.Column(7);
        meters.Name = "BudgetMeters";
        var col = UI.Column(5, header, meters);
        Refresh(() =>
        {
            var b = SceneBudget.For(_m);
            meters.Children.Clear();
            foreach (var label in new[] { "VRAM BG", "VRAM OBJ", "OAM" })
                if (b.Meters.FirstOrDefault(x => x.Label == label) is { } mt)
                    meters.Children.Add(new Meter { Label = mt.Label, ValueText = mt.Text, Percent = mt.Percent, Status = mt.Status });
            var ok = b.AllGood;
            ((TextBlock)status.Children[1]).Text = ok ? "all good" : "over budget";
            var token = ok ? "status/success" : b.Meters.Any(x => x.Status == ProgressStatus.Danger) ? "status/danger" : "status/warning";
            status.Children[0].Token(Shape.FillProperty, token);
            status.Children[1].Token(TextBlock.ForegroundProperty, token);
        });
        var button = new Button
        {
            Theme = (Avalonia.Styling.ControlTheme?)Application.Current?.FindResource("BareButton"), Content = col, Name = "BudgetButton",
            HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };
        button.Click += (_, _) => _ = _s.Windows.Open("T-O06", anchor: button, source: "GbaBudget");
        // Preferences › Editor › "Show GBA budget in Inspector"
        var section = new Border { Padding = new Thickness(12, 8, 12, 12), Child = button, Name = "BudgetSection", IsVisible = _s.Preferences.Current.ShowBudgetInInspector };
        void Show() => section.IsVisible = _s.Preferences.Current.ShowBudgetInInspector;
        section.AttachedToVisualTree += (_, _) => { _s.Preferences.Saved -= Show; _s.Preferences.Saved += Show; Show(); };
        section.DetachedFromVisualTree += (_, _) => _s.Preferences.Saved -= Show;
        return section;
    }
}
