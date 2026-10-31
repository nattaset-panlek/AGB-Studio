using System.Text;
using System.Text.RegularExpressions;
using AgbStudio.Build.Data;
using AgbStudio.Core.Project;

namespace AgbStudio.Editor.Windows.Database;

/// <summary>
/// "Used by" of a table row (P9.05, T-P07): reference fields of other tables ("Shop_01 · slot 2"), scene actors whose
/// component fields name the row ("Chest · Cave_01"), scripts that use <c>ItemId.Potion</c> ("Slime.cs · drop table")
/// and dialogue "Give item" commands ("forest_intro.dlg · Give item").
/// </summary>
public static partial class UsedBy
{
    public enum Kind { Table, Scene, Script, Dialogue }

    public sealed record Reference(string Label, Kind Kind, string Path, string? Detail, int Line = 0);

    [GeneratedRegex(@"\b([A-Za-z_]\w*)\s*(?:\[\s*\])?\s*=(?!=)")]
    private static partial Regex Assigned();

    public static IReadOnlyList<Reference> Find(ProjectData data, TableFile table, string id)
    {
        var list = new List<Reference>();
        if (id.Length == 0) return list;
        foreach (var other in data.Tables)
            foreach (var f in other.Table.Fields.Where(f => f.Type == "reference" && f.Reference == table.Name))
                foreach (var row in other.Table.Rows.Where(r => r.GetValueOrDefault(f.Name) == id))
                    list.Add(new Reference($"{row.GetValueOrDefault(other.Table.KeyField)} · {Humanize(f.Name)}", Kind.Table, other.Path, row.GetValueOrDefault(other.Table.KeyField)));

        var qualified = DatabaseCodegen.IdEnum(table.Name) + "." + id;
        foreach (var path in data.Project.SceneFiles())
        {
            SceneFile scene;
            try { scene = GameProject.Load<SceneFile>(path); }
            catch (Exception e) when (e is IOException or System.Text.Json.JsonException or InvalidDataException) { continue; }
            foreach (var a in scene.Actors)
                if (a.Components.Any(c => c.Fields.Values.Any(v => v == id || v == qualified)))
                    list.Add(new Reference($"{a.Name} · {scene.Name}", Kind.Scene, path, a.Name));
        }

        var rx = new Regex(@"\b" + Regex.Escape(qualified) + @"\b");
        foreach (var path in data.Project.ScriptFiles())
        {
            string[] lines;
            try { lines = File.ReadAllLines(path); }
            catch (IOException) { continue; }
            for (var i = 0; i < lines.Length; i++)
            {
                if (!rx.IsMatch(lines[i])) continue;
                var m = Assigned().Match(lines[i]);
                var where = m.Success && m.Index < rx.Match(lines[i]).Index ? Humanize(m.Groups[1].Value) : $"line {i + 1}";
                list.Add(new Reference($"{Path.GetFileName(path)} · {where}", Kind.Script, path, null, i + 1));
                break;
            }
        }

        foreach (var d in data.Dialogues)
            if (d.File.Conversations.SelectMany(c => c.Lines).Any(l => l.Command == "giveItem" && l.Table == table.Name && l.Item == id))
                list.Add(new Reference($"{d.FileName} · Give item", Kind.Dialogue, d.Path, d.Name));
        return list;
    }

    /// <summary>"dropTable" -> "drop table".</summary>
    public static string Humanize(string name)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (c == '_') { sb.Append(' '); continue; }
            if (char.IsUpper(c) && i > 0 && !char.IsUpper(name[i - 1])) sb.Append(' ');
            else if (char.IsDigit(c) && i > 0 && char.IsLetter(name[i - 1])) sb.Append(' ');   // "slot2" -> "slot 2"
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString().Trim();
    }
}
