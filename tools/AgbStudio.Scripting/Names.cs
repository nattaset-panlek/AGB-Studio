using System.Globalization;
using System.Text;

namespace AgbStudio.Scripting;

/// <summary>Identifier helpers shared by the C++ and C# generators.</summary>
public static class Names
{
    static readonly HashSet<string> s_cppReserved = new(StringComparer.Ordinal)
    {
        "alignas", "alignof", "and", "and_eq", "asm", "auto", "bitand", "bitor", "char8_t", "char16_t", "char32_t",
        "compl", "concept", "consteval", "constexpr", "constinit", "const_cast", "co_await", "co_return", "co_yield",
        "decltype", "delete", "dynamic_cast", "export", "friend", "inline", "mutable", "noexcept", "not", "not_eq",
        "nullptr", "or", "or_eq", "register", "reinterpret_cast", "requires", "signed", "static_assert", "static_cast",
        "template", "thread_local", "typedef", "typeid", "typename", "union", "unsigned", "wchar_t", "xor", "xor_eq",
        "NULL", "main", "agb", "agbgen", "std",
        // members of agb::Behaviour that user code must not hide by accident
        "typeId", "typeName", "start", "update", "lateUpdate", "onTouch", "onDestroy", "fieldCount", "fieldName",
        "fieldPtr", "fieldKind", "actor", "enabled", "started", "kTypeId",
    };

    /// <summary>C++ spelling of a C# identifier (reserved C++ words get a trailing underscore).</summary>
    public static string Cpp(string name)
    {
        if (name.StartsWith('@')) name = name[1..];
        if (s_cppReserved.Contains(name) || name.Contains("__") || (name.Length > 1 && name[0] == '_' && char.IsUpper(name[1])))
            return name + "_";
        return name;
    }

    /// <summary>A valid identifier made from any name ("jump.wav" -> "jump", "Forest 01" -> "Forest_01").</summary>
    public static string Identifier(string name, bool stripExtension = false)
    {
        if (stripExtension) name = StripExtension(name);
        var sb = new StringBuilder();
        foreach (var c in name)
            sb.Append(char.IsLetterOrDigit(c) && c < 128 ? c : '_');
        if (sb.Length == 0 || char.IsDigit(sb[0])) sb.Insert(0, '_');
        var s = sb.ToString();
        while (s.Contains("__")) s = s.Replace("__", "_");
        if (s.Length > 1 && s[0] == '_' && char.IsUpper(s[1])) s = "n" + s;
        return s;
    }

    public static string StripExtension(string name)
    {
        var dot = name.LastIndexOf('.');
        if (dot <= 0) return name;
        var ext = name[(dot + 1)..].ToLowerInvariant();
        return ext is "wav" or "mod" or "xm" or "sfx" or "png" or "scene" or "dlg" ? name[..dot] : name;
    }

    /// <summary>Items -> Item, Enemies -> Enemy, Bosses -> Boss.</summary>
    public static string Singular(string name)
    {
        if (name.EndsWith("ies", StringComparison.Ordinal) && name.Length > 3) return name[..^3] + "y";
        if (name.EndsWith("sses", StringComparison.Ordinal) || name.EndsWith("shes", StringComparison.Ordinal) || name.EndsWith("ches", StringComparison.Ordinal))
            return name[..^2];
        if (name.EndsWith('s') && !name.EndsWith("ss", StringComparison.Ordinal) && name.Length > 1) return name[..^1];
        return name;
    }

    /// <summary>C++ string literal (UTF-8 bytes; control characters and quotes escaped).</summary>
    public static string CppString(string s)
    {
        var sb = new StringBuilder("\"");
        foreach (var b in Encoding.UTF8.GetBytes(s))
        {
            switch (b)
            {
                case (byte)'"': sb.Append("\\\""); break;
                case (byte)'\\': sb.Append("\\\\"); break;
                case (byte)'\n': sb.Append("\\n"); break;
                case (byte)'\t': sb.Append("\\t"); break;
                case (byte)'?': sb.Append("\\?"); break;  // no trigraphs
                default:
                    if (b < 32 || b >= 127) sb.Append('\\').Append(Convert.ToString(b, 8).PadLeft(3, '0'));
                    else sb.Append((char)b);
                    break;
            }
        }
        return sb.Append('"').ToString();
    }

    /// <summary>C# string literal.</summary>
    public static string CsString(string s) =>
        "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t") + "\"";

    /// <summary>Decimal text of a double without exponent ("1.5", "-3.5", "2.0").</summary>
    public static string Decimal(double v)
    {
        var s = v.ToString("0.0###########", CultureInfo.InvariantCulture);
        return s;
    }

    /// <summary>The closest name (edit distance at most a third of the length), for "Did you mean" hints.</summary>
    public static string? Closest(string name, IEnumerable<string> candidates)
    {
        string? best = null;
        var bestD = int.MaxValue;
        foreach (var c in candidates)
        {
            var d = Distance(name.ToLowerInvariant(), c.ToLowerInvariant());
            if (d < bestD) { bestD = d; best = c; }
        }
        return best != null && bestD <= Math.Max(1, name.Length / 3) ? best : null;
    }

    static int Distance(string a, string b)
    {
        var prev = new int[b.Length + 1];
        var cur = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) prev[j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            cur[0] = i;
            for (var j = 1; j <= b.Length; j++)
                cur[j] = Math.Min(Math.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            (prev, cur) = (cur, prev);
        }
        return prev[b.Length];
    }
}

/// <summary>Assigns unique C++ identifiers to project names inside one id namespace.</summary>
internal sealed class IdNames
{
    readonly Dictionary<string, string> _byName = new(StringComparer.Ordinal);
    readonly HashSet<string> _used = new(StringComparer.Ordinal);

    public string Get(string name)
    {
        if (_byName.TryGetValue(name, out var id)) return id;
        var baseId = Names.Cpp(Names.Identifier(name, stripExtension: true));
        id = baseId;
        for (var i = 2; !_used.Add(id); i++) id = baseId + "_" + i;
        _byName[name] = id;
        return id;
    }
}
