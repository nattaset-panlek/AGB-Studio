using System.Text.Json;
using AgbStudio.Core.Project;
using AgbStudio.Editor.Services;
using AgbStudio.Editor.Shell;

namespace AgbStudio.Editor.Windows.UiEditor;

/// <summary>The project's UI pages (ui/*.uipage) for the UI Editor (T-P27): the page and element being edited, undoable
/// edits, and saving. Every edit is written to the file at once, so the next build has it.</summary>
public sealed class UiPageLibrary
{
    readonly EditorServices _s;
    UiPageFile? _liveBefore;

    UiPageLibrary(EditorServices s) => _s = s;

    public static UiPageLibrary Of(EditorServices s)
    {
        if (s.GetService(typeof(UiPageLibrary)) is UiPageLibrary l) return l;
        l = new UiPageLibrary(s);
        s.Set(l);
        return l;
    }

    public GameProject? Project => _s.Project.Current;
    public string? Path { get; private set; }
    public UiPageFile? Page { get; private set; }
    /// <summary>Child indexes from the root to the selected element; empty = the root.</summary>
    public IReadOnlyList<int> Selection { get; private set; } = [];
    public string PageName => Path is null ? "" : GameProject.AssetName(Path);

    /// <summary>The page changed (values, not what is selected).</summary>
    public event Action? Changed;
    /// <summary>Another page or element was selected, or the element tree changed.</summary>
    public event Action? Structure;

    public IReadOnlyList<string> Files => Project?.UiPageFiles().ToList() ?? [];

    public static UiPageFile Clone(UiPageFile p) => JsonSerializer.Deserialize<UiPageFile>(JsonSerializer.Serialize(p, GameProject.Json), GameProject.Json)!;

    static string Text(UiPageFile p) => JsonSerializer.Serialize(p, GameProject.Json);

    public UiElement? Selected
    {
        get
        {
            if (Page is null) return null;
            var e = Page.Root;
            foreach (var i in Selection)
            {
                if (i >= e.Children.Count) return Page.Root;
                e = e.Children[i];
            }
            return e;
        }
    }

    public UiElement? ParentOf(UiElement e)
    {
        if (Page is null) return null;
        foreach (var p in Page.Root.Walk())
            if (p.Children.Contains(e)) return p;
        return null;
    }

    public IReadOnlyList<int> PathOf(UiElement e)
    {
        var path = new List<int>();
        for (var c = e; ParentOf(c) is { } p; c = p) path.Insert(0, p.Children.IndexOf(c));
        return path;
    }

    public void Open(string? path)
    {
        Path = path;
        Page = path != null && File.Exists(path) ? GameProject.Load<UiPageFile>(path) : null;
        Selection = [];
        Structure?.Invoke();
    }

    /// <summary>Opens the page again from its file if another page or the project changed underneath.</summary>
    public void OpenFirstIfNeeded()
    {
        var files = Files;
        if (Path != null && files.Any(f => string.Equals(System.IO.Path.GetFullPath(f), System.IO.Path.GetFullPath(Path), StringComparison.OrdinalIgnoreCase))) return;
        Open(files.Count > 0 ? files[0] : null);
    }

    public void Select(IReadOnlyList<int> path)
    {
        Selection = path.ToList();
        Structure?.Invoke();
    }

    public void Select(UiElement e) => Select(PathOf(e));

    void Save()
    {
        if (Path != null && Page != null) GameProject.Save(Path, Page);
    }

    void Apply(UiPageFile page, IReadOnlyList<int> selection, bool structure)
    {
        Page = Clone(page);
        Selection = selection.ToList();
        Save();
        if (structure) Structure?.Invoke();
        Changed?.Invoke();
    }

    /// <summary>One undoable change of the page. <paramref name="structure"/>: the tree or the selection changes.</summary>
    public void Edit(string name, Action<UiPageFile> change, bool structure = false, Func<UiPageFile, IReadOnlyList<int>>? select = null)
    {
        if (Page is null) return;
        var before = Clone(Page);
        var beforeSelection = Selection.ToList();
        var after = Clone(Page);
        change(after);
        var afterSelection = select?.Invoke(after) ?? beforeSelection;
        if (Text(before) == Text(after) && afterSelection.SequenceEqual(beforeSelection)) return;
        _s.Undo.Do(new DelegateAction(name, () => Apply(before, beforeSelection, true), () => Apply(after, afterSelection, structure)));
    }

    /// <summary>Changes the selected element by name lookup in the clone.</summary>
    public void EditSelected(string name, Action<UiElement> change, bool structure = false)
    {
        var path = Selection.ToList();
        Edit(name, p =>
        {
            var e = p.Root;
            foreach (var i in path) e = e.Children[i];
            change(e);
        }, structure);
    }

    // A drag on the canvas: changes show at once and become one undo step when the mouse is released.
    public void BeginLive()
    {
        if (Page != null) _liveBefore = Clone(Page);
    }

    public void Live(Action<UiElement> change)
    {
        if (Selected is { } e)
        {
            change(e);
            Changed?.Invoke();
        }
    }

    public void EndLive(string name)
    {
        if (Page is null || _liveBefore is null) return;
        var before = _liveBefore;
        _liveBefore = null;
        var selection = Selection.ToList();
        var after = Clone(Page);
        if (Text(before) == Text(after)) return;
        Save();
        _s.Undo.Push(new DelegateAction(name, () => Apply(before, selection, true), () => Apply(after, selection, true)));
    }

    // ---- pages ----------------------------------------------------------------------------------------

    public string UniquePageName(string wanted)
    {
        var used = Files.Select(GameProject.AssetName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!used.Contains(wanted)) return wanted;
        for (var i = 2; ; i++)
            if (!used.Contains(wanted + i)) return wanted + i;
    }

    string PagePath(string name) => System.IO.Path.Combine(Project!.UiDir, name + ".uipage");

    void CreateFile(UiPageFile page)
    {
        Directory.CreateDirectory(Project!.UiDir);
        var path = PagePath(page.Name);
        var saved = Clone(page);
        var previous = Path;
        _s.Undo.Do(new DelegateAction("New page",
            () => { if (File.Exists(path)) File.Delete(path); Open(previous); },
            () => { GameProject.Save(path, saved); Open(path); }));
    }

    public void NewPage()
    {
        if (Project is null) return;
        CreateFile(new UiPageFile { Name = UniquePageName("Page") });
    }

    /// <summary>Copies a ready-made page into the project, where it can be changed.</summary>
    public void AddPreset(string preset)
    {
        if (Project is null) return;
        var keys = Project.StringTables().SelectMany(f => GameProject.Load<StringTable>(f).Strings.Keys).ToHashSet();
        var first = Project.SceneFiles().Select(GameProject.AssetName).FirstOrDefault(n => n != Project.Settings.General.StartScene) ?? "";
        if (UiPresets.Create(preset, Project.Settings.Ui, first, keys) is not { } page) return;
        page.Name = UniquePageName(preset);
        CreateFile(page);
    }

    public void DeletePage()
    {
        if (Path is not { } path || Page is not { } page) return;
        var saved = Clone(page);
        _s.Undo.Do(new DelegateAction("Delete page",
            () => { GameProject.Save(path, saved); Open(path); },
            () => { if (File.Exists(path)) File.Delete(path); Path = null; OpenFirstIfNeeded(); }));
    }

    public void RenamePage(string name)
    {
        name = name.Trim();
        if (Path is not { } oldPath || Page is null || name.Length == 0 || name == PageName || name.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0) return;
        if (Files.Any(f => string.Equals(GameProject.AssetName(f), name, StringComparison.OrdinalIgnoreCase))) return;
        var newPath = PagePath(name);
        var oldName = PageName;
        void Move(string from, string to, string pageName)
        {
            if (Page is null) return;
            Page.Name = pageName;
            GameProject.Save(from, Page);
            File.Move(from, to);
            // The scenes that open this page go with it.
            foreach (var f in Project?.SceneFiles() ?? [])
            {
                var s = GameProject.Load<SceneFile>(f);
                var from1 = pageName == oldName ? name : oldName;
                var hit = s.Page == from1 || s.Hud == from1;
                if (s.Page == from1) s.Page = pageName;
                if (s.Hud == from1) s.Hud = pageName;
                if (hit) GameProject.Save(f, s);
            }
            Open(to);
        }
        _s.Undo.Do(new DelegateAction("Rename page", () => Move(newPath, oldPath, oldName), () => Move(oldPath, newPath, name)));
    }

    // ---- elements -------------------------------------------------------------------------------------

    public static string UniqueName(UiPageFile page, string wanted)
    {
        var used = page.Root.Walk().Select(e => e.Name).ToHashSet();
        if (!used.Contains(wanted)) return wanted;
        for (var i = 2; ; i++)
            if (!used.Contains(wanted + i)) return wanted + i;
    }

    /// <summary>A new element of a kind with sizes that show it, in the theme of Project Settings · Menu / UI kit.</summary>
    public UiElement NewElement(UiElementKind kind, UiPageFile page)
    {
        var ui = Project?.Settings.Ui ?? new UiSettings();
        var e = new UiElement { Name = UniqueName(page, kind.ToString()), Kind = kind, Rect = UiRect.TopLeft(8, 8, 96, 16) };
        switch (kind)
        {
            case UiElementKind.Panel:
                e.Rect = UiRect.TopLeft(8, 8, 96, 48);
                e.Fill = ui.Paper;
                e.Border = ui.Ink;
                break;
            case UiElementKind.Image:
                e.Rect = UiRect.TopLeft(8, 8, 16, 16);
                break;
            case UiElementKind.Text:
                e.Text = "Text";
                e.TextColor = ui.Ink;
                break;
            case UiElementKind.Button:
                e.Text = "Button";
                e.TextColor = ui.Ink;
                e.Cursor = true;
                e.Accent = ui.Ink;
                e.FocusFill = ui.BarTrack;
                e.FocusTextColor = ui.ScreenInk;
                e.PaddingLeft = 2;
                e.PaddingRight = 2;
                break;
            case UiElementKind.Toggle:
                e.Text = "Toggle";
                e.TextColor = ui.Ink;
                e.Accent = ui.BarFill;
                e.FocusFill = ui.BarTrack;
                e.FocusTextColor = ui.ScreenInk;
                break;
            case UiElementKind.Slider:
                e.Rect = UiRect.TopLeft(8, 8, 96, 8);
                e.Fill = ui.BarTrack;
                e.Accent = ui.BarFill;
                e.BorderWidth = 0;
                e.Amount = 50;
                break;
            case UiElementKind.ScrollRect:
                e.Rect = UiRect.TopLeft(8, 8, 120, 68);
                e.Fill = ui.Paper;
                e.Border = ui.Ink;
                e.Layout = new UiLayoutGroup { Kind = UiLayoutKind.Vertical, PaddingLeft = 2, PaddingTop = 2, PaddingRight = 2, PaddingBottom = 2, StretchCross = true };
                break;
            case UiElementKind.Scrollbar:
                e.Rect = UiRect.TopLeft(128, 8, 8, 68);
                e.Fill = ui.BarTrack;
                e.Accent = ui.BarFill;
                e.ScrollTarget = page.Root.Walk().FirstOrDefault(x => x.Kind == UiElementKind.ScrollRect)?.Name ?? "";
                break;
            case UiElementKind.Dropdown:
                e.Rect = UiRect.TopLeft(8, 8, 120, 16);
                e.Text = "Dropdown";
                e.TextColor = ui.Ink;
                e.Fill = ui.Paper;
                e.FocusFill = ui.BarTrack;
                e.FocusTextColor = ui.ScreenInk;
                e.Options = ["One", "Two"];
                e.PaddingLeft = 2;
                e.PaddingRight = 2;
                break;
            case UiElementKind.InputField:
                e.Rect = UiRect.TopLeft(8, 8, 120, 20);
                e.Fill = ui.Paper;
                e.Border = ui.Ink;
                e.TextColor = ui.Ink;
                e.FocusBorder = ui.BarFill;
                e.PaddingLeft = 6;
                e.PaddingRight = 6;
                e.Action = new UiAction { Data = 1 };
                e.Keyboard = new UiKeyboard { Fill = ui.Paper, Border = ui.Ink, TextColor = ui.Ink, FocusFill = ui.BarTrack, FocusTextColor = ui.ScreenInk };
                break;
        }
        return e;
    }

    static UiElement At(UiPageFile page, IReadOnlyList<int> path)
    {
        var e = page.Root;
        foreach (var i in path) e = e.Children[i];
        return e;
    }

    /// <summary>Adds an element inside the selection (or next to it when the selection holds no children).</summary>
    public void AddElement(UiElementKind kind)
    {
        if (Page is null) return;
        var selected = Selection.ToList();
        var into = selected.Count == 0 || At(Page, selected).Kind is UiElementKind.Panel or UiElementKind.ScrollRect ? selected : selected.Take(selected.Count - 1).ToList();
        Edit("Add " + kind, p =>
        {
            var parent = At(p, into);
            var e = NewElement(kind, p);
            if (parent.Layout is null or { Kind: UiLayoutKind.None })
            {
                e.Rect.X += parent.Children.Count * 6;
                e.Rect.Y += parent.Children.Count * 6;
            }
            parent.Children.Add(e);
        }, true, p => [.. into, At(p, into).Children.Count - 1]);
    }

    public void DeleteSelected()
    {
        if (Selection.Count == 0) return;
        var path = Selection.ToList();
        Edit("Delete element", p => At(p, path.Take(path.Count - 1).ToList()).Children.RemoveAt(path[^1]), true, _ => path.Take(path.Count - 1).ToList());
    }

    public void DuplicateSelected()
    {
        if (Selection.Count == 0 || Page is null) return;
        var path = Selection.ToList();
        Edit("Duplicate element", p =>
        {
            var parent = At(p, path.Take(path.Count - 1).ToList());
            var copy = JsonSerializer.Deserialize<UiElement>(JsonSerializer.Serialize(parent.Children[path[^1]], GameProject.Json), GameProject.Json)!;
            foreach (var c in copy.Walk()) c.Name = UniqueName(p, c.Name);
            parent.Children.Insert(path[^1] + 1, copy);
        }, true, _ => [.. path.Take(path.Count - 1), path[^1] + 1]);
    }

    public void MoveSelected(int by)
    {
        if (Selection.Count == 0 || Page is null) return;
        var path = Selection.ToList();
        var siblings = At(Page, path.Take(path.Count - 1).ToList()).Children.Count;
        var to = path[^1] + by;
        if (to < 0 || to >= siblings) return;
        Edit("Reorder element", p =>
        {
            var parent = At(p, path.Take(path.Count - 1).ToList());
            var e = parent.Children[path[^1]];
            parent.Children.RemoveAt(path[^1]);
            parent.Children.Insert(to, e);
        }, true, _ => [.. path.Take(path.Count - 1), to]);
    }

    /// <summary>Moves an element under a new parent at a position (a drag in the element tree).</summary>
    public void Reparent(IReadOnlyList<int> element, IReadOnlyList<int> newParent, int index)
    {
        if (Page is null || element.Count == 0) return;
        if (newParent.Count >= element.Count && newParent.Take(element.Count).SequenceEqual(element)) return;  // into itself
        UiElement? moved = null;
        Edit("Move element", p =>
        {
            var oldParent = At(p, element.Take(element.Count - 1).ToList());
            var target = At(p, newParent);
            var e = oldParent.Children[element[^1]];
            oldParent.Children.RemoveAt(element[^1]);
            var at = ReferenceEquals(oldParent, target) && element[^1] < index ? index - 1 : index;
            target.Children.Insert(Math.Clamp(at, 0, target.Children.Count), e);
            moved = e;
        }, true, p =>
        {
            var path = new List<int>();
            bool Find(UiElement n)
            {
                for (var i = 0; i < n.Children.Count; i++)
                {
                    path.Add(i);
                    if (ReferenceEquals(n.Children[i], moved) || Find(n.Children[i])) return true;
                    path.RemoveAt(path.Count - 1);
                }
                return false;
            }
            Find(p.Root);
            return path;
        });
    }
}
