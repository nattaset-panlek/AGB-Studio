namespace AgbStudio.Editor.Commands;

/// <summary>Receives requests for Figma windows (T-IDs) that commands open.</summary>
public interface IWindowRouter
{
    void Open(string windowId, EditorCommand source);
}

/// <summary>All commands by id (P10.12). Commands without a handler fall back to the window router.</summary>
public sealed class CommandRegistry(IWindowRouter router)
{
    readonly Dictionary<string, EditorCommand> _byId = new(StringComparer.Ordinal);
    readonly List<EditorCommand> _ordered = [];

    public IWindowRouter Router { get; } = router;
    public IReadOnlyList<EditorCommand> All => _ordered;
    public event Action<EditorCommand>? Executed;

    public EditorCommand Add(EditorCommand c)
    {
        if (!_byId.TryAdd(c.Id, c)) throw new InvalidOperationException("Duplicate command " + c.Id);
        c.Fallback = cmd =>
        {
            if (cmd.Window != null) Router.Open(cmd.Window, cmd);
        };
        c.Executed = cmd => Executed?.Invoke(cmd);
        _ordered.Add(c);
        return c;
    }

    public EditorCommand Get(string id) => _byId.TryGetValue(id, out var c) ? c : throw new KeyNotFoundException("Unknown command " + id);
    public bool TryGet(string id, out EditorCommand c) => _byId.TryGetValue(id, out c!);
    public bool Remove(string id) { if (!_byId.Remove(id, out var c)) return false; _ordered.Remove(c); return true; }

    public bool Execute(string id)
    {
        var c = Get(id);
        if (!c.CanExecute()) return false;
        c.Execute();
        return true;
    }
}
