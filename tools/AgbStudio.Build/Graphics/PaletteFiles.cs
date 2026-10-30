using System.Globalization;
using System.Text;

namespace AgbStudio.Build.Graphics;

/// <summary>Palette import / export: JASC .pal (Paint Shop Pro, Aseprite) and GIMP .gpl.</summary>
public static class PaletteFiles
{
    public static List<ushort> Read(string path)
    {
        var lines = File.ReadAllLines(path).Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        var colors = new List<ushort>();
        if (lines.Count > 0 && lines[0] == "JASC-PAL")
        {
            var count = int.Parse(lines[2], CultureInfo.InvariantCulture);
            for (var i = 0; i < count && 3 + i < lines.Count; i++) colors.Add(Parse(lines[3 + i]));
        }
        else if (lines.Count > 0 && lines[0].StartsWith("GIMP Palette", StringComparison.Ordinal))
        {
            foreach (var l in lines.Skip(1))
            {
                if (l.StartsWith('#') || l.StartsWith("Name:", StringComparison.Ordinal) || l.StartsWith("Columns:", StringComparison.Ordinal)) continue;
                colors.Add(Parse(l));
            }
        }
        else
        {
            throw new InvalidDataException($"{Path.GetFileName(path)} is not a JASC .pal or GIMP .gpl palette.");
        }
        return colors;
    }

    static ushort Parse(string line)
    {
        var p = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        uint r = uint.Parse(p[0], CultureInfo.InvariantCulture), g = uint.Parse(p[1], CultureInfo.InvariantCulture), b = uint.Parse(p[2], CultureInfo.InvariantCulture);
        return Gba.ToBgr555(0xFF000000u | (r << 16) | (g << 8) | b);
    }

    public static void Write(string path, IReadOnlyList<ushort> colors)
    {
        var sb = new StringBuilder();
        var gpl = path.EndsWith(".gpl", StringComparison.OrdinalIgnoreCase);
        if (gpl)
        {
            sb.Append("GIMP Palette\nName: ").Append(Path.GetFileNameWithoutExtension(path)).Append("\nColumns: 16\n#\n");
        }
        else
        {
            sb.Append("JASC-PAL\n0100\n").Append(colors.Count).Append('\n');
        }
        foreach (var c in colors)
        {
            var argb = Gba.ToArgb(c);
            sb.Append((argb >> 16) & 0xFF).Append(' ').Append((argb >> 8) & 0xFF).Append(' ').Append(argb & 0xFF);
            if (gpl) sb.Append("\tIndex");
            sb.Append('\n');
        }
        File.WriteAllText(path, sb.ToString());
    }
}
