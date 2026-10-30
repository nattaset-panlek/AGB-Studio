using System.Globalization;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Controls;
using AgbStudio.Editor.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;

namespace AgbStudio.Editor.Windows.SceneEditor;

/// <summary>
/// New Scene (T-D02, 520 x 470; Scene › New Scene…, Scene Switcher › New Scene…, World Map › New Scene): name, start
/// from a template, size in tiles, video mode, copy the current scene's backgrounds, a camera that follows the player.
/// Create Scene writes the .scene and .map files (undoable) and opens the new scene.
/// </summary>
[EditorWindow("T-D02")]
public sealed class NewSceneDialog : FigmaDialog
{
    readonly SceneEditorModel _m;
    readonly Segment[] _modes;
    readonly DialogFooter _footer;
    Button? _confirm;
    public TextField SceneName { get; } = new() { Label = "Scene name", Name = "SceneName" };
    public SelectField StartFrom { get; } = new() { Label = "Start from", Name = "StartFrom", Options = SceneFiles.Templates, Selected = SceneFiles.Templates[0] };
    public TextField WidthTiles { get; } = new() { Label = "Width (tiles)", Name = "WidthTiles" };
    public TextField HeightTiles { get; } = new() { Label = "Height (tiles)", Name = "HeightTiles" };
    public CheckBox CopyBackgrounds { get; } = new() { Name = "CopyBackgrounds" };
    public CheckBox FollowCamera { get; } = new() { Content = "Add a camera that follows the player", Name = "FollowCamera", IsChecked = true };

    public NewSceneDialog(WindowContext ctx) : this(ctx.Services) { }

    public NewSceneDialog(EditorServices s)
    {
        _m = SceneEditorModel.Of(s);
        Name = "NewSceneDialog";
        var p = _m.Project;
        SceneName.Text = p == null ? "" : SceneFiles.SuggestName(p, _m.SceneName);
        var size = (W: _m.Map?.Width ?? _m.Scene?.Width ?? 30, H: _m.Map?.Height ?? _m.Scene?.Height ?? 20);
        WidthTiles.Text = size.W.ToString(CultureInfo.InvariantCulture);
        HeightTiles.Text = size.H.ToString(CultureInfo.InvariantCulture);
        CopyBackgrounds.Content = $"Copy backgrounds from {(_m.Scene == null ? "this scene" : _m.SceneName)}";
        CopyBackgrounds.IsChecked = _m.Map?.Layers.Any(l => l.Type == LayerType.Image) == true;
        CopyBackgrounds.IsEnabled = CopyBackgrounds.IsChecked == true;
        var group = "video-mode-" + Guid.NewGuid().ToString("N");
        _modes = Enumerable.Range(0, 4).Select(i => new Segment { Content = $"Mode {i}", GroupName = group, IsChecked = i == (_m.Scene?.VideoMode ?? 0), Name = "Mode_" + i }).ToArray();
        var modes = UI.Frame(UI.Row(2, _modes), "bg/base", "border/default", null, new Thickness(2), 7);
        modes.HorizontalAlignment = HorizontalAlignment.Left;
        var sizeRow = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("*,10,*") };
        Grid.SetColumn(HeightTiles, 2);
        sizeRow.Children.AddRange([WidthTiles, HeightTiles]);
        var content = UI.Column(14, SceneName, StartFrom, sizeRow,
            UI.Column(14, UI.Text("Video mode", "Overline/11", "text/secondary"), modes),
            UI.Column(14, CopyBackgrounds, FollowCamera));
        content.Margin = new Thickness(20, 20, 20, 16);
        _footer = new DialogFooter { ConfirmText = "Create Scene" };
        _footer.Cancel += (_, _) => Close(null);
        _footer.Confirm += (_, _) => Create();
        _footer.TemplateApplied += (_, e) => { _confirm = e.NameScope.Find<Button>("PART_Confirm"); Validate(); };
        Build("New Scene", "map", content, _footer, 520);
        SelectFieldMenu.Hook(StartFrom);
        // the name box has the keyboard when the dialog opens (Figma shows it focused)
        SceneName.TemplateApplied += (_, e) =>
        {
            if (e.NameScope.Find<TextBox>("PART_Box") is { } box)
                box.AttachedToVisualTree += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() => { box.Focus(); box.SelectAll(); }, Avalonia.Threading.DispatcherPriority.Loaded);
        };
        foreach (var f in new[] { SceneName, WidthTiles, HeightTiles }) f.PropertyChanged += (_, e) => { if (e.Property == TextField.TextProperty) Validate(); };
        Validate();
    }

    public string? Problem { get; private set; }

    void Validate()
    {
        var name = SceneName.Text?.Trim() ?? "";
        Problem = _m.Project == null ? "Open a project first."
            : !SceneFiles.ValidName(name) ? "Use letters, digits and _ (start with a letter)."
            : SceneFiles.Exists(_m.Project, name) ? $"A scene named {name} already exists."
            : !int.TryParse(WidthTiles.Text, out var w) || w < 15 || w > 256 ? "Width is 15 to 256 tiles."
            : !int.TryParse(HeightTiles.Text, out var h) || h < 10 || h > 256 ? "Height is 10 to 256 tiles."
            : null;
        _footer.Hint = Problem;
        if (_confirm != null) _confirm.IsEnabled = Problem == null;
    }

    public int VideoMode => Array.FindIndex(_modes, m => m.IsChecked == true) is var i and >= 0 ? i : 0;

    /// <summary>Creates the scene (the Create Scene button); returns its path, or null when a field is not valid.</summary>
    public string? Create()
    {
        Validate();
        if (Problem != null) return null;
        var path = SceneFiles.Create(_m, new NewSceneOptions(SceneName.Text!.Trim(), StartFrom.Selected ?? SceneFiles.Templates[0],
            int.Parse(WidthTiles.Text!, CultureInfo.InvariantCulture), int.Parse(HeightTiles.Text!, CultureInfo.InvariantCulture), VideoMode,
            CopyBackgrounds.IsChecked == true, FollowCamera.IsChecked == true));
        Close(path);
        return path;
    }
}

/// <summary>Opens a Select's options as a menu under it (the Select component has no list of its own).</summary>
static class SelectFieldMenu
{
    public static void Hook(SelectField f) => f.TemplateApplied += (_, e) =>
    {
        if (e.NameScope.Find<Button>("PART_Box") is not { } box) return;
        box.Click += (_, _) =>
        {
            var menu = new ContextMenu
            {
                ItemsSource = (f.Options ?? []).Select(o =>
                {
                    var item = new MenuItem { Header = o, ToggleType = o == f.Selected ? MenuItemToggleType.CheckBox : MenuItemToggleType.None, IsChecked = o == f.Selected };
                    item.Click += (_, _) => f.Selected = o;
                    return item;
                }).ToList(),
            };
            menu.Open(box);
        };
    };
}

/// <summary>
/// Scene Settings (T-D03, 600 x 646; #18 scene size chip, Scene › Scene Settings…): General (name, size, start scene),
/// Camera (follow, dead zone, clamp to map), Backgrounds (parallax per layer), Music &amp; events (music, OnEnter, OnExit).
/// Save applies everything as one undoable change of the scene, its map, the project and linked scenes.
/// </summary>
[EditorWindow("T-D03")]
public sealed class SceneSettingsDialog : FigmaDialog
{
    readonly SceneEditorModel _m;
    readonly List<(InspectorField Field, MapLayerData Layer)> _parallax = [];
    public InspectorField SceneNameField { get; } = new() { Kind = FieldKind.Text, Label = "Name", Name = "Field_Name" };
    public InspectorField Size { get; } = new() { Kind = FieldKind.XY, Label = "Size", Name = "Field_Size" };
    public InspectorField StartScene { get; } = new() { Kind = FieldKind.Toggle, Label = "Start scene", Name = "Field_StartScene" };
    public InspectorField Follow { get; } = new() { Kind = FieldKind.Dropdown, Label = "Follow", Name = "Field_Follow" };
    public InspectorField DeadZone { get; } = new() { Kind = FieldKind.XY, Label = "Dead zone", Name = "Field_DeadZone" };
    public InspectorField Clamp { get; } = new() { Kind = FieldKind.Toggle, Label = "Clamp to map", Name = "Field_Clamp" };
    public InspectorField Music { get; } = new() { Kind = FieldKind.Dropdown, Label = "Music", Name = "Field_Music" };
    public InspectorField OnEnter { get; } = new() { Kind = FieldKind.Text, Label = "OnEnter", Name = "Field_OnEnter" };
    public InspectorField OnExit { get; } = new() { Kind = FieldKind.Text, Label = "OnExit", Name = "Field_OnExit" };
    public IReadOnlyList<InspectorField> ParallaxFields => _parallax.Select(x => x.Field).ToList();

    public SceneSettingsDialog(WindowContext ctx) : this(ctx.Services) { }

    public SceneSettingsDialog(EditorServices s)
    {
        _m = SceneEditorModel.Of(s);
        Name = "SceneSettingsDialog";
        var sc = _m.Scene ?? new SceneFile();
        var p = _m.Project;
        SceneNameField.Value = _m.SceneName;
        Size.Value = (_m.Map?.Width ?? sc.Width).ToString(CultureInfo.InvariantCulture);
        Size.Value2 = (_m.Map?.Height ?? sc.Height).ToString(CultureInfo.InvariantCulture);
        StartScene.IsOn = p?.Settings.General.StartScene == _m.SceneName && _m.SceneName.Length > 0;
        Follow.Value = sc.Camera.Follow.Length > 0 ? sc.Camera.Follow : "None";
        DeadZone.Value = sc.Camera.DeadZoneW.ToString(CultureInfo.InvariantCulture);
        DeadZone.Value2 = sc.Camera.DeadZoneH.ToString(CultureInfo.InvariantCulture);
        Clamp.IsOn = sc.Camera.ClampToMap;
        var musicFiles = p?.MusicFiles().Select(Path.GetFileName).OfType<string>().ToList() ?? [];
        Music.Value = sc.Music.Track.Length == 0 ? "None" : musicFiles.FirstOrDefault(f => GameProject.AssetName(f) == sc.Music.Track) ?? sc.Music.Track;
        OnEnter.Value = sc.Events.OnEnter;
        OnExit.Value = sc.Events.OnExit;
        Dropdown(Follow, () => ["None", .. sc.Actors.Select(a => a.Name)]);
        Dropdown(Music, () => ["None", .. musicFiles]);

        var sections = new StackPanel();
        sections.Children.Add(Section("General", "map", null, SceneNameField, Size, StartScene));
        sections.Children.Add(Section("Camera", "camera", null, Follow, DeadZone, Clamp));
        if (_m.Map is { } map)
        {
            var rows = new List<Control>();
            foreach (var l in map.Layers.Where(l => l.Type != LayerType.CollisionOnly))
            {
                var f = new InspectorField { Kind = FieldKind.Slider, Label = $"BG{l.Bg} {l.Name}", Name = "Parallax_BG" + l.Bg };
                f.SliderValue = Math.Clamp(l.ParallaxX * 100, 0, 100);
                f.Value = l.ParallaxX.ToString("0.0", CultureInfo.InvariantCulture);
                f.PropertyChanged += (_, e) => { if (e.Property == InspectorField.SliderValueProperty) f.Value = (Math.Round(f.SliderValue) / 100).ToString("0.0#", CultureInfo.InvariantCulture); };
                _parallax.Add((f, l));
                rows.Add(f);
            }
            if (rows.Count > 0) sections.Children.Add(Section("Backgrounds", "layers", "parallax", [.. rows]));
        }
        sections.Children.Add(Section("Music & events", "music", null, Music, OnEnter, OnExit));
        var footer = new DialogFooter { ConfirmText = "Save" };
        footer.Cancel += (_, _) => Close(null);
        footer.Confirm += (_, _) => Save();
        Build("Scene Settings", "sliders", new ScrollViewer { Content = sections, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden }, footer, 600);
    }

    static Control Section(string title, string glyph, string? meta, params Control[] rows)
    {
        var header = new SectionHeader { Title = title, Glyph = glyph, Meta = meta };
        var body = UI.Column(6, rows);
        header.PropertyChanged += (_, e) => { if (e.Property == SectionHeader.IsOpenProperty) body.IsVisible = header.IsOpen; };
        return UI.Frame(UI.Column(6, header, body), null, "border/default", new Thickness(0, 0, 0, 1), new Thickness(12, 7, 12, 11));
    }

    static void Dropdown(InspectorField f, Func<IReadOnlyList<string>> options) => f.TemplateApplied += (_, e) =>
    {
        if (e.NameScope.Find<Button>("PART_Dropdown") is not { } b) return;
        b.Click += (_, _) =>
        {
            var menu = new ContextMenu
            {
                ItemsSource = options().Select(o =>
                {
                    var item = new MenuItem { Header = o, ToggleType = o == f.Value ? MenuItemToggleType.CheckBox : MenuItemToggleType.None, IsChecked = o == f.Value };
                    item.Click += (_, _) => f.Value = o;
                    return item;
                }).ToList(),
            };
            menu.Open(b);
        };
    };

    static int Int(string? s, int fallback) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    /// <summary>Applies the fields (the Save button).</summary>
    public void Save()
    {
        var sc = _m.Scene;
        if (sc == null) { Close(null); return; }
        var w = Int(Size.Value, _m.Map?.Width ?? sc.Width);
        var h = Int(Size.Value2, _m.Map?.Height ?? sc.Height);
        var music = Music.Value is null or "None" ? "" : GameProject.AssetName(Music.Value);
        var values = new SceneSettingsValues((SceneNameField.Value ?? "").Trim(), w, h, StartScene.IsOn, Follow.Value is null or "None" ? "" : Follow.Value,
            Int(DeadZone.Value, sc.Camera.DeadZoneW), Int(DeadZone.Value2, sc.Camera.DeadZoneH), Clamp.IsOn,
            _parallax.Select(x => Math.Round(x.Field.SliderValue) / 100).ToList(), music, OnEnter.Value ?? "", OnExit.Value ?? "");
        SceneFiles.Apply(_m, values);
        Close(values);
    }
}
