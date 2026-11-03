using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AgbStudio.Editor.Services;

/// <summary>An open document (a Doc Tab). Its editor provides the save action.</summary>
public sealed class EditorDocument : INotifyPropertyChanged
{
    bool _dirty;

    /// <summary>The name of the Play Mode document ("Game" Doc Tab).</summary>
    public const string GameName = "Game";

    public required string Name { get; init; }
    /// <summary>The Game tab: Play Mode, not a file.</summary>
    public bool IsGame => Name == GameName;
    /// <summary>The Scene Editor's document (the "Scene" tab, or any .scene file).</summary>
    public static bool IsSceneDocument(EditorDocument? d) =>
        d != null && (d.Name == "Scene" || d.Path?.EndsWith(".scene", StringComparison.OrdinalIgnoreCase) == true);
    public string Glyph { get; init; } = "map";
    public string? Path { get; set; }
    /// <summary>Writes the document; set by the editor that owns it (Phase 11 windows).</summary>
    public Func<EditorDocument, Task>? SaveHandler { get; set; }

    public bool IsClosable { get; init; } = true;
    public bool CanClose => IsClosable && !IsGame && Name != "Scene";

    public bool IsDirty { get => _dirty; set { if (_dirty == value) return; _dirty = value; Raise(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    void Raise([CallerMemberName] string? p = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}

/// <summary>Open documents, dirty tracking, Save / Save All (P10.08, T-D12 and the Doc Tab dirty dot).</summary>
public sealed class DocumentService
{
    EditorDocument? _active;

    public ObservableCollection<EditorDocument> Open { get; } = [];
    public event Action? Changed;

    public EditorDocument? Active
    {
        get => _active;
        set { if (_active == value) return; _active = value; Changed?.Invoke(); }
    }

    public IReadOnlyList<EditorDocument> Dirty => Open.Where(d => d.IsDirty).ToList();
    public bool HasUnsaved => Open.Any(d => d.IsDirty);

    public EditorDocument Add(EditorDocument doc, bool activate = false)
    {
        if (doc.IsGame)
        {
            if (Open.FirstOrDefault(d => d.IsGame) is { } existingGame)
            {
                if (activate) Active = existingGame;
                return existingGame;
            }
        }
        else if (EditorDocument.IsSceneDocument(doc))
        {
            if (Open.FirstOrDefault(EditorDocument.IsSceneDocument) is { } existingScene)
            {
                if (doc.Path != null && (existingScene.Path == null || string.Equals(existingScene.Path, doc.Path, StringComparison.OrdinalIgnoreCase)))
                {
                    existingScene.SaveHandler = doc.SaveHandler ?? existingScene.SaveHandler;
                    if (activate || (Active is null && !existingScene.IsGame)) Active = existingScene;
                    Changed?.Invoke();
                    return existingScene;
                }
                else if (doc.Path != null && !string.Equals(existingScene.Path, doc.Path, StringComparison.OrdinalIgnoreCase))
                {
                    var idx = Open.IndexOf(existingScene);
                    Open.RemoveAt(idx);
                    Open.Insert(idx, doc);
                    doc.PropertyChanged += (_, _) => Changed?.Invoke();
                    if (activate || (Active is null && !doc.IsGame)) Active = doc;
                    Changed?.Invoke();
                    return doc;
                }
                else
                {
                    if (activate) Active = existingScene;
                    return existingScene;
                }
            }
        }
        else if (doc.Path != null)
        {
            if (Open.FirstOrDefault(d => string.Equals(d.Path, doc.Path, StringComparison.OrdinalIgnoreCase)) is { } existingFile)
            {
                if (activate) Active = existingFile;
                return existingFile;
            }
        }

        Open.Add(doc);
        doc.PropertyChanged += (_, _) => Changed?.Invoke();
        // the Game tab only becomes active by showing Play Mode: a project opened without a scene used to make it the
        // active document, so every page showed "Game" selected
        if (activate || (Active is null && !doc.IsGame)) Active = doc;
        Changed?.Invoke();
        return doc;
    }

    public Action<EditorDocument>? DocumentActivated { get; set; }

    /// <param name="showNext">False when the caller replaces the document on the same page: the next tab becomes active without opening its page.</param>
    public void Move(EditorDocument doc, int index)
    {
        var from = Open.IndexOf(doc);
        if (from < 0) return;
        index = Math.Clamp(index, 0, Open.Count - 1);
        if (index == from) return;
        Open.Move(from, index);
        Changed?.Invoke();
    }

    public void Close(EditorDocument doc, bool force = false, bool showNext = true)
    {
        if (!force && !doc.CanClose) return;
        var i = Open.IndexOf(doc);
        if (i < 0) return;
        Open.RemoveAt(i);
        if (Active == doc)
        {
            var next = Open.Count == 0 ? null : Open[Math.Min(i, Open.Count - 1)];
            Active = next;
            if (next != null && showNext) DocumentActivated?.Invoke(next);
        }
        Changed?.Invoke();
    }

    public EditorDocument EnsureToolDocument(string name, string glyph, Func<EditorDocument, Task>? saveHandler = null)
    {
        var existing = Open.FirstOrDefault(d => d.Name == name);
        if (existing != null)
        {
            if (saveHandler != null) existing.SaveHandler ??= saveHandler;
            Active = existing;
            return existing;
        }
        var doc = new EditorDocument
        {
            Name = name,
            Glyph = glyph,
            SaveHandler = saveHandler,
            IsClosable = true,
        };
        Add(doc, activate: true);
        return doc;
    }

    public async Task SaveAsync(EditorDocument doc)
    {
        if (doc.SaveHandler != null) await doc.SaveHandler(doc);
        doc.IsDirty = false;
    }

    /// <summary>Saves every dirty document and returns how many were written.</summary>
    public async Task<int> SaveAllAsync()
    {
        var dirty = Dirty;
        foreach (var d in dirty) await SaveAsync(d);
        return dirty.Count;
    }

    public Task SaveActiveAsync() => Active is { } a ? SaveAsync(a) : Task.CompletedTask;

    void InsertDoc(int index, EditorDocument doc)
    {
        doc.PropertyChanged += (_, _) => Changed?.Invoke();
        if (index >= 0 && index <= Open.Count) Open.Insert(index, doc);
        else Open.Add(doc);
    }

    public void EnsureProjectDocuments(AgbStudio.Core.Project.GameProject? project, string? currentScenePath = null)
    {
        if (project is null) return;
        var sceneDoc = Open.FirstOrDefault(EditorDocument.IsSceneDocument);
        if (sceneDoc is null)
        {
            var targetScene = currentScenePath ?? (!string.IsNullOrEmpty(project.Settings.LastScene)
                ? Path.Combine(project.ScenesDir, project.Settings.LastScene + ".scene")
                : project.SceneFiles().FirstOrDefault());
            sceneDoc = new EditorDocument
            {
                Name = "Scene",
                Glyph = "map",
                Path = targetScene,
                IsClosable = false,
            };
            InsertDoc(0, sceneDoc);
        }
        else if (currentScenePath != null && !string.Equals(sceneDoc.Path, currentScenePath, StringComparison.OrdinalIgnoreCase))
        {
            sceneDoc.Path = currentScenePath;
            Changed?.Invoke();
        }

        var gameDoc = Open.FirstOrDefault(d => d.IsGame);
        if (gameDoc is null)
        {
            gameDoc = new EditorDocument { Name = EditorDocument.GameName, Glyph = "pad", IsClosable = false };
            var sceneIdx = Open.IndexOf(sceneDoc);
            InsertDoc(sceneIdx >= 0 ? sceneIdx + 1 : Open.Count, gameDoc);
        }

        if (Active is null) Active = sceneDoc;
        Changed?.Invoke();
    }
}
