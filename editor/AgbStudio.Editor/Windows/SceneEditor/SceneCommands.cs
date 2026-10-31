using System.Text.Json;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Commands;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;

namespace AgbStudio.Editor.Windows.SceneEditor;

/// <summary>
/// Scene Editor commands and the context menus of P11.20: Add to Scene (T-M11), Actor Context Menu (T-M12), Asset
/// Context Menu (T-M13), Component Menu (T-M14), Zoom (T-M15) and Animation List (T-M16); handlers for the Scene
/// menu's Add Actor (T-M10), Set as Start Scene, Background Layers… and Focus selection (P11.21).
/// </summary>
public static class SceneCommands
{
    static SceneEditorModel M(EditorServices s) => SceneEditorModel.Of(s);

    // ---- Add Actor (T-M10) and Add to Scene (T-M11) ----------------------------------------------------

    [CommandHandler("scene.addActor.empty")] public static void AddEmpty(EditorServices s) => AddActor(s, ActorKind.Empty);
    [CommandHandler("scene.addActor.player")] public static void AddPlayer(EditorServices s) => AddActor(s, ActorKind.Player);
    [CommandHandler("scene.addActor.enemy")] public static void AddEnemy(EditorServices s) => AddActor(s, ActorKind.Enemy);
    [CommandHandler("scene.addActor.pickup")] public static void AddPickup(EditorServices s) => AddActor(s, ActorKind.Pickup);
    [CommandHandler("scene.addActor.trigger")] public static void AddTrigger(EditorServices s) => AddActor(s, ActorKind.TriggerZone);
    [CommandHandler("scene.addActor.camera")] public static void AddCamera(EditorServices s) => AddActor(s, ActorKind.CameraZone);
    [CommandHandler("scene.addActor.text")] public static void AddText(EditorServices s) => AddActor(s, ActorKind.TextLabel);

    /// <summary>Adds an actor of a kind; its sprite is the one the project uses most for that kind (or <paramref name="sprite"/>).</summary>
    public static int AddActor(EditorServices s, ActorKind kind, string? sprite = null)
    {
        var m = M(s);
        if (m.Scene == null) return -1;
        sprite ??= kind is ActorKind.Player or ActorKind.Enemy or ActorKind.Pickup ? MostUsedSprite(m, kind) : null;
        var a = m.NewActor(kind, sprite);
        return m.AddActor(a, "Add Actor");
    }

    static string? MostUsedSprite(SceneEditorModel m, ActorKind kind) =>
        m.Project?.SceneFiles().SelectMany(f => GameProject.Load<SceneFile>(f).Actors).Where(a => a.Kind == kind && a.Sprite.Length > 0)
            .GroupBy(a => a.Sprite).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault();

    /// <summary>Enemy types of the project for Add to Scene › Enemy: a plain enemy, then one per enemy sprite in use.</summary>
    public static IReadOnlyList<string> EnemySprites(SceneEditorModel m) =>
        m.Project?.SceneFiles().SelectMany(f => GameProject.Load<SceneFile>(f).Actors).Where(a => a.Kind == ActorKind.Enemy && a.Sprite.Length > 0)
            .Select(a => a.Sprite).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList() ?? [];

    [EditorCommand("scene.addLayer", "Tilemap Layer", Glyph = "tiles", InPalette = false)]
    public static void AddTilemapLayer(EditorServices s)
    {
        var m = M(s);
        if (m.Scene == null || m.Map is not { } map) return;
        var used = map.Layers.Select(l => l.Bg).ToHashSet();
        var bg = Enumerable.Range(0, 4).FirstOrDefault(b => !used.Contains(b), -1);
        if (bg < 0)
        {
            s.Toasts.Show("All four backgrounds (BG0-BG3) are in use, and the GBA has no more. Delete a background layer before adding another.", AgbStudio.Editor.Controls.ToastStatus.Error);
            return;
        }
        var index = map.Layers.Count;
        m.EditScene("Add Tilemap Layer", (_, mp) =>
            mp!.Layers.Add(new MapLayerData { Name = "Layer " + (index + 1), Bg = bg, Type = LayerType.Tile, Priority = Math.Min(3, bg), Cells = new int[mp.Width * mp.Height].ToList() }));
        m.Selection = new SceneItem(SceneItemKind.Layer, index);
    }

    [EditorCommand("scene.addPrefab", "Prefab…", Glyph = "folder", InPalette = false)]
    public static void AddPrefab(EditorServices s) => _ = s.Windows.Open("T-O04", AssetPicker.PrefabMode, source: "AddToScenePrefab");

    [EditorMenu("T-M11")]
    public static IReadOnlyList<MenuEntry> AddToScene(WindowContext ctx)
    {
        var m = M(ctx.Services);
        IReadOnlyList<MenuEntry> Enemies()
        {
            var list = new List<MenuEntry> { new RecentEntry("Enemy", "", false, new Relay(() => AddActor(ctx.Services, ActorKind.Enemy, ""))) };
            foreach (var sp in EnemySprites(m))
            {
                var sprite = sp;
                list.Add(new RecentEntry(sp + ".png", sp, false, new Relay(() => AddActor(ctx.Services, ActorKind.Enemy, sprite))));
            }
            return list;
        }
        return
        [
            new CommandEntry("scene.addActor.empty", true), new CommandEntry("scene.addActor.player", true), new SubmenuEntry("T-M11.Enemy", "Enemy", Enemies, "bug"),
            new CommandEntry("scene.addActor.pickup", true), new CommandEntry("scene.addActor.trigger", true), new CommandEntry("scene.addActor.camera", true),
            new CommandEntry("scene.addActor.text", true), SeparatorEntry.Instance,
            new CommandEntry("scene.addLayer", true), new CommandEntry("scene.addPrefab", true),
        ];
    }

    // ---- Actor Context Menu (T-M12) -------------------------------------------------------------------

    [EditorCommand("scene.rename", "Rename", Keys = "F2", Context = ShortcutContext.Scene, InPalette = false)]
    public static void Rename(EditorServices s) => SceneEditTarget.Of(M(s)).Rename();

    [EditorCommand("actor.addBehaviour", "Add Behaviour…", Glyph = "plus", InPalette = false)]
    public static void AddBehaviour(EditorServices s) => _ = s.Windows.Open("T-O05", source: "ActorAddBehaviour");

    [EditorCommand("actor.editScript", "Edit Script", Glyph = "code", InPalette = false)]
    public static void EditActorScript(EditorServices s)
    {
        var m = M(s);
        if (m.SelectedActor is not { } a) return;
        var script = a.Components.Select(c => ScriptFile(m, c.Type)).FirstOrDefault(f => f != null);
        if (script != null) _ = Scripts.ScriptEditorPage.OpenScriptFile(s, script, source: "actor.editScript");
        else s.Toasts.Show($"{a.Name} has no C# script behaviour to edit. Add one with Add Component in the Inspector first.", AgbStudio.Editor.Controls.ToastStatus.Error);
    }

    /// <summary>The .cs file that defines a behaviour class (scripts/**/Name.cs, else the file declaring it).</summary>
    public static string? ScriptFile(SceneEditorModel m, string type)
    {
        if (m.Project is not { } p || BuiltinBehaviours.Find(type) != null) return null;
        var files = p.ScriptFiles().ToList();
        return files.FirstOrDefault(f => GameProject.AssetName(f) == type)
               ?? files.FirstOrDefault(f => System.Text.RegularExpressions.Regex.IsMatch(File.ReadAllText(f), $@"class\s+{System.Text.RegularExpressions.Regex.Escape(type)}\b"));
    }

    public static string PrefabsDir(GameProject p) => Path.Combine(p.Root, "prefabs");

    [EditorCommand("actor.makePrefab", "Make Prefab", InPalette = false)]
    public static void MakePrefab(EditorServices s)
    {
        var m = M(s);
        if (m.Project is not { } p || m.SelectedActor is not { } a) return;
        var index = m.SelectedActorIndex;
        var name = GameProject.FileName(a.Name.Replace(' ', '_'));
        if (name.Length == 0) name = "Prefab";
        var copy = SceneEditorModel.Clone(a);
        copy.Prefab = "";
        GameProject.Save(Path.Combine(PrefabsDir(p), name + ".prefab"), copy);
        m.Edit("Make Prefab", () => m.Scene!.Actors[index].Prefab = name);
        s.Toasts.Show($"Prefab {name} saved", AgbStudio.Editor.Controls.ToastStatus.Success);
    }

    [CommandHandler("scene.focusSelection")]
    public static void FocusSelection(EditorServices s) => FocusSelection(M(s));

    /// <summary>Focus in View: pans the viewport so the selected actor is in the middle of the screen.</summary>
    public static void FocusSelection(SceneEditorModel m)
    {
        if (m.SelectedActor is not { } a || m.Scene == null) return;
        var (sx, sy) = GbaScreenRenderer.StartCamera(m.Scene, m.Map);
        m.SetPan(a.X - 120 - sx, a.Y - 80 - sy);
    }

    [EditorMenu("T-M12")]
    public static IReadOnlyList<MenuEntry> ActorContextMenu(WindowContext ctx)
    {
        SceneEditTarget.Of(M(ctx.Services)).Active = SceneEditTarget.Panel.Scene;
        return
        [
            new CommandEntry("scene.rename"), new CommandEntry("edit.duplicate"), new CommandEntry("edit.delete", Danger: true), SeparatorEntry.Instance,
            new CommandEntry("actor.addBehaviour", true), new CommandEntry("actor.editScript", true), new CommandEntry("actor.makePrefab"), SeparatorEntry.Instance,
            new CommandEntry("scene.focusSelection", Label: "Focus in View"),
        ];
    }

    // ---- Asset Context Menu (T-M13) -------------------------------------------------------------------

    static AssetActions A(EditorServices s) => AssetActions.Of(M(s));

    [EditorCommand("asset.open", "Open", Keys = "Enter", Context = ShortcutContext.Scene, InPalette = false)]
    public static void OpenAsset(EditorServices s)
    {
        if (SceneEditTarget.Of(M(s)).Active != SceneEditTarget.Panel.Assets || A(s).Selected is not { } a) return;
        _ = A(s).Open(a);
    }

    [EditorCommand("asset.duplicate", "Duplicate", InPalette = false)]
    public static void DuplicateAsset(EditorServices s) => A(s).Duplicate();

    [EditorCommand("asset.findUsages", "Find Usages", Glyph = "search", InPalette = false)]
    public static void FindUsages(EditorServices s) => A(s).FindUsages();

    [CommandHandler("assets.reimport")]
    public static void Reimport(EditorServices s) => A(s).Reimport();

    [EditorCommand("asset.createAnimation", "Create Animation", Glyph = "film", InPalette = false)]
    public static void CreateAnimation(EditorServices s) => A(s).CreateAnimation();

    [EditorMenu("T-M13")]
    public static IReadOnlyList<MenuEntry> AssetContextMenu(WindowContext ctx)
    {
        var m = M(ctx.Services);
        if (ctx.Args is string path) AssetActions.Of(m).Select(path);
        SceneEditTarget.Of(m).Active = SceneEditTarget.Panel.Assets;
        return
        [
            new CommandEntry("asset.open"), new CommandEntry("scene.rename"), new CommandEntry("asset.duplicate"), new CommandEntry("edit.delete", Danger: true), SeparatorEntry.Instance,
            new CommandEntry("asset.findUsages", true), new CommandEntry("assets.reimport"), new CommandEntry("assets.showInExplorer"), SeparatorEntry.Instance,
            new CommandEntry("asset.createAnimation", true),
        ];
    }

    // ---- Component Menu (T-M14) -----------------------------------------------------------------------

    /// <summary>The component the Component Menu acts on: the one last clicked in the Inspector (default the first).</summary>
    public static (int Actor, int Component) CurrentComponent(SceneEditorModel m) => (m.SelectedActorIndex, m.SelectedComponent);

    [EditorCommand("component.copy", "Copy Component", InPalette = false)]
    public static void CopyComponent(EditorServices s) { var (a, c) = CurrentComponent(M(s)); M(s).CopyComponent(a, c); }

    [EditorCommand("component.paste", "Paste Values", InPalette = false)]
    public static void PasteValues(EditorServices s) { var (a, c) = CurrentComponent(M(s)); M(s).PasteComponentValues(a, c); }

    [EditorCommand("component.reset", "Reset", InPalette = false)]
    public static void ResetComponent(EditorServices s) { var (a, c) = CurrentComponent(M(s)); M(s).ResetComponent(a, c); }

    [EditorCommand("component.editScript", "Edit Script", Glyph = "code", InPalette = false)]
    public static void EditComponentScript(EditorServices s)
    {
        var m = M(s);
        var (a, c) = CurrentComponent(m);
        var comp = m.Scene?.Actors.ElementAtOrDefault(a)?.Components.ElementAtOrDefault(c);
        var file = comp == null ? null : ScriptFile(m, comp.Type);
        if (file != null) _ = Scripts.ScriptEditorPage.OpenScriptFile(s, file, source: "component.editScript");
        else if (comp != null) s.Toasts.Show($"{comp.Type} is a built-in behaviour; it has no C# script.", AgbStudio.Editor.Controls.ToastStatus.Error);
    }

    [EditorCommand("component.remove", "Remove Component", InPalette = false)]
    public static void RemoveComponent(EditorServices s) { var (a, c) = CurrentComponent(M(s)); M(s).RemoveComponent(a, c); }

    [EditorMenu("T-M14")]
    public static IReadOnlyList<MenuEntry> ComponentMenu() =>
    [
        new CommandEntry("component.copy"), new CommandEntry("component.paste"), new CommandEntry("component.reset"), SeparatorEntry.Instance,
        new CommandEntry("component.editScript", true), new CommandEntry("component.remove", Danger: true),
    ];

    // ---- Zoom (T-M15) ---------------------------------------------------------------------------------

    [EditorCommand("scene.zoom.fit", "Fit to Screen", Keys = "Ctrl+0", Context = ShortcutContext.Scene, InPalette = false)]
    public static void FitToScreen(EditorServices s) => M(s).SetFitToScreen();

    [EditorMenu("T-M15")]
    public static IReadOnlyList<MenuEntry> ZoomMenu(WindowContext ctx)
    {
        var m = M(ctx.Services);
        var list = new List<MenuEntry>();
        foreach (var z in new[] { 100, 200, 300, 400 })
        {
            var zoom = z;
            list.Add(new RecentEntry($"{z}%", "", !m.FitToScreen && m.Zoom == z, new Relay(() => m.SetZoom(zoom))));
        }
        list.Add(SeparatorEntry.Instance);
        list.Add(new CommandEntry("scene.zoom.fit"));
        list.Add(new RecentEntry("Pixel-perfect", "", m.PixelPerfect, new Relay(() => m.SetPixelPerfect(!m.PixelPerfect))));
        return list;
    }

    // ---- Animation List (T-M16) -----------------------------------------------------------------------

    [EditorCommand("sprite.editAnimations", "Edit Animations…", Glyph = "film", InPalette = false)]
    public static void EditAnimations(EditorServices s)
    {
        var m = M(s);
        var sprite = m.SelectedActor?.Sprite;
        var path = sprite is { Length: > 0 } ? m.Assets?.SpritePath(sprite) : null;
        _ = s.Windows.Open("T-P08", path, source: "sprite.editAnimations");
    }

    /// <summary>The animations of the selected actor's sprite (checked: the actor's animation), then Edit Animations….</summary>
    [EditorMenu("T-M16")]
    public static IReadOnlyList<MenuEntry> AnimationList(WindowContext ctx)
    {
        var m = M(ctx.Services);
        var list = new List<MenuEntry>();
        var index = m.SelectedActorIndex;
        if (m.SelectedActor is { Sprite.Length: > 0 } a && m.Assets is { } assets)
            foreach (var anim in assets.SpriteMeta(a.Sprite).Animations)
            {
                var name = anim.Name;
                list.Add(new RecentEntry(name, "", a.Animation == name, new Relay(() => m.Edit("Change Animation", () => m.Scene!.Actors[index].Animation = name))));
            }
        if (list.Count > 0) list.Add(SeparatorEntry.Instance);
        list.Add(new CommandEntry("sprite.editAnimations", true));
        return list;
    }

    // ---- Scene menu (P11.21) --------------------------------------------------------------------------

    [CommandHandler("scene.setStart")]
    public static void SetStartScene(EditorServices s)
    {
        var m = M(s);
        if (m.Project is not { } p || m.Scene == null) return;
        var before = p.Settings.General.StartScene;
        var name = m.SceneName;
        if (before == name) return;
        s.Undo.Do(new DelegateAction("Set as Start Scene",
            () => { p.Settings.General.StartScene = before; p.SaveSettings(); },
            () => { p.Settings.General.StartScene = name; p.SaveSettings(); }));
        s.Toasts.Show($"{name} is the start scene", AgbStudio.Editor.Controls.ToastStatus.Success);
    }

    [CommandHandler("scene.backgroundLayers")]
    public static void BackgroundLayers(EditorServices s) => _ = s.Windows.Open("T-P03", M(s).MapPath, source: "scene.backgroundLayers");

    /// <summary>Reads a prefab file (prefabs/&lt;Name&gt;.prefab, written by Make Prefab).</summary>
    public static ActorData? LoadPrefab(string path)
    {
        try { return GameProject.Load<ActorData>(path); }
        catch (Exception e) when (e is IOException or JsonException or InvalidDataException) { return null; }
    }
}
