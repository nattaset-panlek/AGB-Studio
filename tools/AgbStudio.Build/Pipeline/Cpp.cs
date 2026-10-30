using System.Globalization;
using System.Text;

namespace AgbStudio.Build.Pipeline;

/// <summary>Small helpers to write generated C++ data.</summary>
public static class Cpp
{
    public static string Ident(string name)
    {
        var sb = new StringBuilder();
        foreach (var c in name) sb.Append(char.IsAsciiLetterOrDigit(c) ? c : '_');
        if (sb.Length == 0 || char.IsDigit(sb[0])) sb.Insert(0, '_');
        return sb.ToString();
    }

    public static string Str(string s)
    {
        var sb = new StringBuilder("\"");
        foreach (var b in Encoding.UTF8.GetBytes(s))
        {
            if (b == '"' || b == '\\') sb.Append('\\').Append((char)b);
            else if (b >= 32 && b < 127) sb.Append((char)b);
            else sb.Append("\\").Append(Convert.ToString(b, 8).PadLeft(3, '0'));
        }
        return sb.Append('"').ToString();
    }

    public static void Bytes(StringBuilder sb, string decl, ReadOnlySpan<byte> data)
    {
        // Emitted as 32-bit words so tile data is aligned for DMA and CpuFastSet.
        var words = (data.Length + 3) / 4;
        sb.Append(decl).Append("[] = {");
        for (var i = 0; i < words; i++)
        {
            if (i % 8 == 0) sb.Append("\n    ");
            uint w = 0;
            for (var k = 0; k < 4; k++)
                if (i * 4 + k < data.Length) w |= (uint)data[i * 4 + k] << (8 * k);
            sb.Append("0x").Append(w.ToString("X8")).Append("u,");
        }
        sb.Append("\n};\n");
    }

    public static void U16(StringBuilder sb, string decl, IEnumerable<int> values)
    {
        sb.Append(decl).Append("[] = {");
        var i = 0;
        foreach (var v in values)
        {
            if (i++ % 16 == 0) sb.Append("\n    ");
            sb.Append(v.ToString(CultureInfo.InvariantCulture)).Append(',');
        }
        if (i == 0) sb.Append('0');
        sb.Append("\n};\n");
    }

    public static string Fixed(double v) => $"agb::fixed::fromRaw({(int)Math.Round(v * 256)})";

    public static int Parallax(double v) => (int)Math.Round(v * 256);
}
