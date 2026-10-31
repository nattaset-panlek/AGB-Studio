namespace AgbStudio.Scripting.Bytecode;

/// <summary>Emits VM instructions with forward labels and tracks the operand stack depth.</summary>
internal sealed class ScriptAssembler
{
    readonly OpcodeTable _ops;
    readonly List<byte> _code = new();
    readonly List<(int At, int Label)> _fixups = new();
    readonly List<int> _labels = new();
    public int Depth { get; private set; }
    public int MaxDepth { get; private set; }
    public List<(int Pc, int Line)> Lines { get; } = new();

    public ScriptAssembler(OpcodeTable ops) => _ops = ops;

    public int Pc => _code.Count;

    void Stack(int delta)
    {
        Depth += delta;
        if (Depth < 0) Depth = 0;
        if (Depth > MaxDepth) MaxDepth = Depth;
    }

    public void Op(string name, int stackDelta = 0)
    {
        var op = _ops[name];
        if (op.OperandBytes != 0) throw new InvalidOperationException($"{name} needs operands.");
        _code.Add(op.Code);
        Stack(stackDelta);
    }

    void Op8(string name, int value, int stackDelta)
    {
        _code.Add(_ops[name].Code);
        _code.Add((byte)value);
        Stack(stackDelta);
    }

    void Op16(string name, int value, int stackDelta)
    {
        _code.Add(_ops[name].Code);
        _code.Add((byte)(value & 0xFF));
        _code.Add((byte)((value >> 8) & 0xFF));
        Stack(stackDelta);
    }

    public void Push(int v)
    {
        if (v is >= sbyte.MinValue and <= sbyte.MaxValue) Op8("PushI8", v, 1);
        else if (v is >= short.MinValue and <= short.MaxValue) Op16("PushI16", v, 1);
        else
        {
            _code.Add(_ops["PushI32"].Code);
            _code.AddRange(BitConverter.GetBytes(v));
            Stack(1);
        }
    }

    public void LoadLocal(int slot) => Op8("LoadLocal", slot, 1);
    public void StoreLocal(int slot) => Op8("StoreLocal", slot, -1);
    public void LoadVar(int id) => Op16("LoadVar", id, 1);
    public void StoreVar(int id) => Op16("StoreVar", id, -1);
    public void GetFlag(int id) => Op16("GetFlag", id, 1);
    public void SetFlag(int id) => Op16("SetFlag", id, -1);
    public void Call(int script, int argc) => Op16("Call", script, 1 - argc);

    public void Native(int id, int argc)
    {
        _code.Add(_ops["Native"].Code);
        _code.Add((byte)(id & 0xFF));
        _code.Add((byte)((id >> 8) & 0xFF));
        _code.Add((byte)argc);
        Stack(1 - argc);
    }

    public void Line(int line)
    {
        // One marker per line unless a jump can land between two markers of the same line.
        if (Lines.Count > 0 && Lines[^1].Line == line && !_labelSinceLine) return;
        _labelSinceLine = false;
        Lines.Add((Pc, line));
        Op16("Line", line, 0);
    }

    public int NewLabel()
    {
        _labels.Add(-1);
        return _labels.Count - 1;
    }

    public void Mark(int label)
    {
        _labels[label] = Pc;
        _labelSinceLine = true;
    }

    bool _labelSinceLine;

    /// <summary>Jump, JumpIfFalse, JumpIfTrue or WaitUntil to a label (relative s16 from the next instruction).</summary>
    public void Jump(string name, int label)
    {
        _code.Add(_ops[name].Code);
        _fixups.Add((_code.Count, label));
        _code.Add(0);
        _code.Add(0);
        if (name != "Jump") Stack(-1);
    }

    /// <summary>Stack depth bookkeeping across branches that join (the two arms of ?: push one value each).</summary>
    public void SetDepth(int depth) => Depth = depth;

    public byte[] Finish()
    {
        foreach (var (at, label) in _fixups)
        {
            var target = _labels[label];
            if (target < 0) throw new InvalidOperationException("Jump to a label that was never placed.");
            var rel = target - (at + 2);
            if (rel is < short.MinValue or > short.MaxValue) throw new InvalidOperationException("Script too long: a jump exceeds 32 KB.");
            _code[at] = (byte)(rel & 0xFF);
            _code[at + 1] = (byte)((rel >> 8) & 0xFF);
        }
        return _code.ToArray();
    }
}
