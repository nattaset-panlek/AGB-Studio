namespace AgbStudio.Editor.Services;

/// <summary>An edit that can be undone and redone.</summary>
public interface IUndoableAction
{
    string Name { get; }
    void Undo();
    void Redo();
}

/// <summary>An undoable action made of two delegates.</summary>
public sealed record DelegateAction(string Name, Action UndoAction, Action RedoAction) : IUndoableAction
{
    public void Undo() => UndoAction();
    public void Redo() => RedoAction();
}

/// <summary>Undo / redo history (P10.09) limited to Preferences · Editor "Undo history" (200 steps by default).</summary>
public sealed class UndoService(Func<int> limit)
{
    readonly LinkedList<IUndoableAction> _undo = new();
    readonly Stack<IUndoableAction> _redo = new();

    public UndoService(int limit) : this(() => limit) { }

    public int Limit => Math.Max(1, limit());
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public int UndoCount => _undo.Count;
    public int RedoCount => _redo.Count;
    public string? NextUndo => _undo.Last?.Value.Name;
    public string? NextRedo => _redo.Count > 0 ? _redo.Peek().Name : null;
    public event Action? Changed;

    /// <summary>Records an action that has already been applied. Clears the redo stack and drops the oldest steps over the limit.</summary>
    public void Push(IUndoableAction action)
    {
        _undo.AddLast(action);
        _redo.Clear();
        Trim();
        Changed?.Invoke();
    }

    /// <summary>Applies an action and records it.</summary>
    public void Do(IUndoableAction action)
    {
        action.Redo();
        Push(action);
    }

    public bool Undo()
    {
        if (_undo.Last is not { } node) return false;
        _undo.RemoveLast();
        node.Value.Undo();
        _redo.Push(node.Value);
        Changed?.Invoke();
        return true;
    }

    public bool Redo()
    {
        if (_redo.Count == 0) return false;
        var a = _redo.Pop();
        a.Redo();
        _undo.AddLast(a);
        Trim();
        Changed?.Invoke();
        return true;
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        Changed?.Invoke();
    }

    /// <summary>Applies a new limit (after Preferences change) by dropping the oldest steps.</summary>
    public void Trim()
    {
        while (_undo.Count > Limit) _undo.RemoveFirst();
    }
}
