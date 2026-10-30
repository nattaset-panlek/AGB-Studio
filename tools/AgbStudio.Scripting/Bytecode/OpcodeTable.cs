using System.Text;
using System.Text.RegularExpressions;

namespace AgbStudio.Scripting.Bytecode;

/// <summary>One VM opcode from runtime/include/agb/script_ops.def.</summary>
public sealed record Opcode(string Name, byte Code, int OperandBytes, string Note);

/// <summary>
/// The Event Script VM opcodes, read from runtime/include/agb/script_ops.def: the single source for the runtime
/// handlers (X-macro) and this compiler. Codes are the line order, as in the runtime's enum.
/// </summary>
public sealed partial class OpcodeTable
{
    [GeneratedRegex(@"^\s*AGB_OP\(\s*(\w+)\s*,\s*(\d+)\s*,\s*""([^""]*)""\s*\)", RegexOptions.Multiline)]
    private static partial Regex OpRegex();

    readonly Dictionary<string, Opcode> _byName = new(StringComparer.Ordinal);
    public IReadOnlyList<Opcode> All { get; }

    public OpcodeTable(string defText)
    {
        var list = new List<Opcode>();
        foreach (Match m in OpRegex().Matches(defText))
        {
            var op = new Opcode(m.Groups[1].Value, (byte)list.Count, int.Parse(m.Groups[2].Value), m.Groups[3].Value);
            list.Add(op);
            _byName[op.Name] = op;
        }
        if (list.Count == 0) throw new InvalidOperationException("script_ops.def has no AGB_OP entries.");
        All = list;
    }

    public static OpcodeTable Load(string? runtimeIncludeDir = null) => new(RuntimeFiles.Read("script_ops.def", runtimeIncludeDir));

    public Opcode this[string name] => _byName.TryGetValue(name, out var op) ? op
        : throw new InvalidOperationException($"The VM has no opcode '{name}' (runtime/include/agb/script_ops.def).");

    public bool Has(string name) => _byName.ContainsKey(name);
    public Opcode? ByCode(byte code) => code < All.Count ? All[code] : null;

    /// <summary>Readable listing: "0003: PushI8 5" per instruction (golden tests, Debugger).</summary>
    public string Disassemble(byte[] code)
    {
        var sb = new StringBuilder();
        var pc = 0;
        while (pc < code.Length)
        {
            var op = ByCode(code[pc]);
            if (op == null)
            {
                sb.Append($"{pc:X4}: ?? {code[pc]}\n");
                pc++;
                continue;
            }
            sb.Append($"{pc:X4}: {op.Name}");
            var p = pc + 1;
            switch (op.Name)
            {
                case "PushI8": sb.Append(' ').Append((sbyte)code[p]); break;
                case "PushI16": sb.Append(' ').Append(BitConverter.ToInt16(code, p)); break;
                case "PushI32": sb.Append(' ').Append(BitConverter.ToInt32(code, p)); break;
                case "Jump" or "JumpIfFalse" or "JumpIfTrue":
                    sb.Append(" -> ").Append((p + op.OperandBytes + BitConverter.ToInt16(code, p)).ToString("X4"));
                    break;
                case "Native": sb.Append($" #{BitConverter.ToUInt16(code, p)} argc {code[p + 2]}"); break;
                case "LoadVar" or "StoreVar" or "GetFlag" or "SetFlag": sb.Append($" 0x{BitConverter.ToUInt16(code, p):X4}"); break;
                default:
                    if (op.OperandBytes == 1) sb.Append(' ').Append(code[p]);
                    else if (op.OperandBytes == 2) sb.Append(' ').Append(BitConverter.ToUInt16(code, p));
                    break;
            }
            sb.Append('\n');
            pc += 1 + op.OperandBytes;
        }
        return sb.ToString();
    }
}

/// <summary>A native of the generated native table.</summary>
public sealed record NativeInfo(string Name, bool Waits, string CppFunction, string Note);

/// <summary>Built-in natives, read from the AGB_BUILTIN_NATIVES list in runtime/include/agb/natives.hpp.</summary>
public static partial class NativeCatalog
{
    [GeneratedRegex(@"X\(\s*(\w+)\s*,\s*(true|false)\s*,\s*""([^""]*)""\s*\)")]
    private static partial Regex EntryRegex();

    public static IReadOnlyList<NativeInfo> Builtins(string? runtimeIncludeDir = null)
    {
        var text = RuntimeFiles.Read("natives.hpp", runtimeIncludeDir);
        var start = text.IndexOf("#define AGB_BUILTIN_NATIVES", StringComparison.Ordinal);
        if (start < 0) throw new InvalidOperationException("natives.hpp has no AGB_BUILTIN_NATIVES list.");
        // The macro ends at the first line without a trailing backslash.
        var end = start;
        while (true)
        {
            var nl = text.IndexOf('\n', end);
            if (nl < 0) { end = text.Length; break; }
            var line = text[end..nl].TrimEnd('\r', ' ');
            end = nl + 1;
            if (!line.EndsWith('\\')) break;
        }
        return EntryRegex().Matches(text[start..end])
            .Select(m => new NativeInfo(m.Groups[1].Value, m.Groups[2].Value == "true", "agb::script::natives::" + m.Groups[1].Value, m.Groups[3].Value))
            .ToList();
    }
}
