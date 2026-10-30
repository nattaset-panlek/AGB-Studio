using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AgbStudio.Editor.Services;

/// <summary>An open document (a Doc Tab). Its editor provides the save action.</summary>
public sealed class EditorDocument : INotifyPropertyChanged
{
    bool _dirty;

    public required string Name { get; init; }
    public string Glyph { get; init; } = "map";
    public string? Path { get; init; }
    /// <summary>Writes the document; set by the editor that owns it (Phase 11 windows).</summary>
    public Func<EditorDocument, Task>? SaveHandler { get; set; }

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
        Open.Add(doc);
        doc.PropertyChanged += (_, _) => Changed?.Invoke();
        if (activate || Active is null) Active = doc;
        Changed?.Invoke();
        return doc;
    }

    public void Close(EditorDocument doc)
    {
        var i = Open.IndexOf(doc);
        Open.Remove(doc);
        if (Active == doc) Active = Open.Count == 0 ? null : Open[Math.Min(i, Open.Count - 1)];
        Changed?.Invoke();
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
}
