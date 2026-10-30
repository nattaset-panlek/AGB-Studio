using System.Globalization;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Graphics;
using AgbStudio.Editor.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;

namespace AgbStudio.Editor.Windows.Play;

/// <summary>
/// Inspector · Live (P12.06): the selected actor of the running game, refreshed every frame from emulator memory:
/// state, Transform (position, velocity), Sprite, the built-in components (Platformer Body with On ground) and the
/// script fields. Edits are written into the game at once (D-15).
/// </summary>
public sealed class LiveInspector : DockPanel
{
    readonly PlayController _play;
    readonly StackPanel _body = new() { Name = "LiveSections" };
    readonly TextBlock _name = UI.Text("", "Title/14", "text/primary");
    readonly TextBlock _state = UI.Text("", "Label/11", "text/muted");
    readonly ArtImage _thumb = new() { Width = 16, Height = 16, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    readonly Icon _thumbIcon = new() { Glyph = "cube", Size = 16, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    readonly Border _header;
    string? _shownActor;
    string _shownShape = "";
    readonly List<Action<LiveActorInfo>> _updaters = [];

    public LiveInspector(PlayController play)
    {
        _play = play;
        Name = "LiveInspector";
        LastChildFill = true;
        var thumbBox = UI.Frame(new Panel { Children = { _thumb, _thumbIcon } }, "bg/base", "border/default", new Thickness(1), null, 8);
        thumbBox.Width = thumbBox.Height = 40;
        _thumb.Width = _thumb.Height = 32;
        _thumbIcon.Token(Icon.ForegroundProperty, "text/secondary");
        var chip = new Chip { Content = "ACTOR", Style = ChipStyle.Accent, Name = "ActorKindChip" };
        var meta = UI.Row(6, chip, _state);
        var text = UI.Column(4, _name, meta);
        text.VerticalAlignment = VerticalAlignment.Center;
        var row = UI.Row(10, thumbBox, text);
        thumbBox.VerticalAlignment = VerticalAlignment.Center;
        _header = UI.Frame(row, null, "border/default", new Thickness(0, 0, 0, 1), new Thickness(12, 11, 12, 12)).Named("LiveActorHeader");
        SetDock(_header, Avalonia.Controls.Dock.Top);
        Children.Add(_header);
        Children.Add(new ScrollViewer { Content = _body, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden });
        Refresh();
    }

    /// <summary>Re-reads the selected actor (call after each frame).</summary>
    public void Refresh()
    {
        var a = _play.World.Find(_play.SelectedActor);
        _header.IsVisible = a != null;
        if (a == null) { _body.Children.Clear(); _shownActor = null; _shownShape = ""; _updaters.Clear(); return; }
        var shape = string.Join("|", a.Behaviours.Select(b => b.Type + ":" + b.Fields.Count));
        if (a.Name != _shownActor || shape != _shownShape) Build(a, shape);
        _name.Text = a.Name;
        _state.Text = (_play.IsPaused ? "paused" : "running") + " · state " + a.State;
        foreach (var u in _updaters) u(a);
    }

    void Build(LiveActorInfo a, string shape)
    {
        _shownActor = a.Name;
        _shownShape = shape;
        _updaters.Clear();
        _body.Children.Clear();
        var sprite = a.Data?.Sprite ?? "";
        var art = "art/sprite/" + sprite;
        var hasArt = sprite.Length > 0 && ArtExists(art);
        _thumb.Source = hasArt ? art : null;
        _thumb.IsVisible = hasArt;
        _thumbIcon.IsVisible = !hasArt;

        // Transform
        var pos = new InspectorField { Kind = FieldKind.XY, Label = "Position", Name = "Live_Position" };
        var vel = new InspectorField { Kind = FieldKind.XY, Label = "Velocity", Name = "Live_Velocity" };
        Section("Transform", "move", null, pos, vel);
        Live(pos, x => F2(x.Actor.X), x => F2(x.Actor.Y));
        Live(vel, x => F2(x.Actor.Vx), x => F2(x.Actor.Vy));
        CommitXY(pos, (x, y) => { if (_play.World.Find(_shownActor) is { } cur) _play.WritePosition(cur, x, y); });

        if (sprite.Length > 0)
        {
            var sheet = new InspectorField { Kind = FieldKind.Dropdown, Label = "Sheet", Value = sprite + ".png", Name = "Live_Sheet" };
            var anim = new InspectorField { Kind = FieldKind.Dropdown, Label = "Animation", Value = a.Data?.Animation is { Length: > 0 } an ? an : "idle", Name = "Live_Animation" };
            Section("Sprite", "image", null, sheet, anim);
        }

        foreach (var b in a.Behaviours)
        {
            if (b.IsScript)
            {
                var rows = b.Fields.Select(f => (Field: f, Control: new InspectorField { Kind = FieldKind.Text, Label = f.Name, Name = "Live_" + b.Type + "_" + f.Name })).ToList();
                Section(b.Type, "code", "script", rows.Select(r => (Control)r.Control).ToArray());
                foreach (var (f, c) in rows)
                {
                    var name = f.Name;
                    var type = b.Type;
                    _updaters.Add(x => { if (!c.IsKeyboardFocusWithin && Find(x, type, name) is { } lf) c.Value = lf.Text; });
                    Commit(c, v => { if (_play.World.Find(_shownActor) is { } cur && Find(cur, type, name) is { } lf) _play.WriteField(lf, v); });
                }
            }
            else if (b.Type == "PlatformerBody")
            {
                var gravity = Slider("Gravity", b, "gravity", 0.8);
                var jump = Slider("Jump force", b, "jumpForce", 6);
                var max = Slider("Max speed", b, "maxSpeed", 4);
                var ground = new InspectorField { Kind = FieldKind.Toggle, Label = "On ground", Name = "Live_OnGround", IsOn = a.Actor.OnGround, IsHitTestVisible = false };
                Section("Platformer Body", "rocket", null, gravity, jump, max, ground);
                _updaters.Add(x => ground.IsOn = x.Actor.OnGround);
            }
        }
    }

    static bool ArtExists(string figmaName)
    {
        try { return Avalonia.Platform.AssetLoader.Exists(new Uri("avares://AGBStudio/Assets/Art/" + IconCatalog.ArtFile(figmaName))); }
        catch (InvalidOperationException) { return false; }
    }

    InspectorField Slider(string label, LiveBehaviour b, string member, double max)
    {
        var f = new InspectorField { Kind = FieldKind.Slider, Label = label, Name = "Live_" + member };
        var type = b.Type;
        _updaters.Add(x =>
        {
            if (Find(x, type, member) is not { } lf) return;
            f.Value = lf.Value.ToString("0.00", CultureInfo.InvariantCulture);
            f.SliderValue = Math.Clamp(lf.Value / max * 100, 0, 100);
        });
        return f;
    }

    static LiveField? Find(LiveActorInfo a, string type, string field) => a.Behaviours.FirstOrDefault(b => b.Type == type)?.Field(field);

    void Live(InspectorField f, Func<LiveActorInfo, string> x, Func<LiveActorInfo, string> y) =>
        _updaters.Add(a => { if (!f.IsKeyboardFocusWithin) { f.Value = x(a); f.Value2 = y(a); } });

    static string F2(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);

    static void Commit(InspectorField f, Action<double> write)
    {
        f.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key != Key.Enter || !double.TryParse(f.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return;
            write(v);
            e.Handled = true;
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    static void CommitXY(InspectorField f, Action<double, double> write)
    {
        f.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            if (double.TryParse(f.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) && double.TryParse(f.Value2, NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            {
                write(x, y);
                e.Handled = true;
            }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    void Section(string title, string glyph, string? meta, params Control[] rows)
    {
        var header = new SectionHeader { Title = title, Glyph = glyph, Meta = meta, Name = "Section_" + title.Replace(" ", "") };
        var list = UI.Column(6, rows);
        var col = UI.Column(6, header, list);
        header.PropertyChanged += (_, e) => { if (e.Property == SectionHeader.IsOpenProperty) list.IsVisible = header.IsOpen; };
        _body.Children.Add(UI.Frame(col, null, "border/default", new Thickness(0, 0, 0, 1), new Thickness(12, 8, 12, 10)));
    }
}
