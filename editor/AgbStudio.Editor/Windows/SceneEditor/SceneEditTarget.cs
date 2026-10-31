using AgbStudio.Editor.Services;

namespace AgbStudio.Editor.Windows.SceneEditor;

/// <summary>
/// The Edit menu (P10.11) and the Rename / Delete / Duplicate entries of the Actor and Asset context menus act on the
/// Scene Editor panel used last: actors (Hierarchy, viewport) or the selected asset (Assets panel).
/// </summary>
public sealed class SceneEditTarget : IEditTarget
{
    public enum Panel { Scene, Assets }

    readonly SceneEditorModel _m;
    SceneEditTarget(SceneEditorModel m) => _m = m;

    public static SceneEditTarget Of(SceneEditorModel m)
    {
        if (m.Services.GetService(typeof(SceneEditTarget)) is SceneEditTarget t) return t;
        t = new SceneEditTarget(m);
        m.Services.Set(t);
        return t;
    }

    public Panel Active { get; set; } = Panel.Scene;
    public AssetActions Assets => AssetActions.Of(_m);

    public bool CanCopy => Active == Panel.Scene ? _m.SelectedActorIndex >= 0 : Assets.Selected != null;
    public bool CanPaste => Active == Panel.Scene ? _m.ActorClipboard != null : Assets.Clipboard != null;

    public void Cut()
    {
        Copy();
        Delete();
    }

    public void Copy()
    {
        if (Active == Panel.Scene) _m.CopyActor(_m.SelectedActorIndex);
        else Assets.Copy();
    }

    public void Paste()
    {
        if (Active == Panel.Scene) _m.PasteActor();
        else Assets.Paste();
    }

    public void Duplicate()
    {
        if (Active == Panel.Scene) _m.DuplicateActor(_m.SelectedActorIndex);
        else Assets.Duplicate();
    }

    public void Delete()
    {
        if (Active == Panel.Scene) _m.DeleteActor(_m.SelectedActorIndex);
        else Assets.Delete();
    }

    /// <summary>Select All in the scene selects the scene (every actor is under it); in Assets, every asset of the shown category.</summary>
    public void SelectAll()
    {
        if (Active == Panel.Assets) Assets.SelectAll(Assets.ShownCategory);
        else if (_m.Scene != null) _m.Selection = new SceneItem(SceneItemKind.Scene);
    }

    /// <summary>F2: rename the selected actor (inline in the Hierarchy) or the selected asset.</summary>
    public event Action<Panel>? RenameRequested;
    public void Rename() => RenameRequested?.Invoke(Active);
}
