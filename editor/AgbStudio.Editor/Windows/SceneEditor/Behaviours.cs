using System.Globalization;
using System.Text.RegularExpressions;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;

namespace AgbStudio.Editor.Windows.SceneEditor;

public enum FieldType { Fixed, Int, Bool, Text }

/// <summary>An Inspector field of a behaviour: label, type, default and slider range.</summary>
public sealed record BehaviourField(string Name, string Label, FieldType Type, string Default, double Min, double Max, bool Slider);

/// <summary>A behaviour the Component Picker offers: built-in (runtime) or a C# script class of the project.</summary>
public sealed record BehaviourDef(string Name, string Title, string Category, string Description, string Glyph, bool Script, IReadOnlyList<BehaviourField> Fields);

/// <summary>
/// Built-in behaviours (BuiltinBehaviours, Component Picker T-O05 categories) and the project's script behaviours with
/// their public fields (from the scripting compiler's last result when there is one, else read from the .cs files).
/// </summary>
public static class Behaviours
{
    static readonly Dictionary<string, string> Titles = new()
    {
        ["PlatformerBody"] = "Platformer Body", ["TopDownMover"] = "Top-down Mover", ["Projectile"] = "Projectile", ["Health"] = "Health",
        ["DamageOnTouch"] = "Damage on Touch", ["Patrol"] = "Patrol", ["ChasePlayer"] = "Chase Player", ["SoundOnEvent"] = "Sound on Event",
    };

    // Runtime fields the Figma Inspector does not show (they keep their default unless the scene file sets them).
    static readonly HashSet<string> Hidden = ["PlatformerBody.maxFall"];

    public static bool HiddenInInspector(string type, string field) => Hidden.Contains(type + "." + field);

    public static string CategoryGlyph(string category) => category switch
    {
        "Physics" => "rocket", "Combat" => "sword", "AI" => "bug", "Audio" => "sound", _ => "code",
    };

    // Slider ranges of fixed-point fields (Inspector Platformer Body: Gravity, Jump force, Max speed).
    static (double Min, double Max) Range(string field) => field switch
    {
        "gravity" => (0, 0.8),
        "jumpForce" => (0, 6),
        "maxSpeed" or "speed" => (0, 4),
        "maxFall" => (0, 8),
        "vx" or "vy" => (-4, 4),
        "volume" => (0, 1),
        _ => (0, 4),
    };

    /// <summary>"jumpForce" -> "Jump force".</summary>
    public static string Humanize(string name)
    {
        var words = Regex.Replace(name, "([a-z0-9])([A-Z])", "$1 $2").ToLowerInvariant();
        return words.Length == 0 ? words : char.ToUpperInvariant(words[0]) + words[1..];
    }

    public static IReadOnlyList<BehaviourDef> Builtins { get; } = BuiltinBehaviours.All.Select(b => new BehaviourDef(
        b.Name, Titles.GetValueOrDefault(b.Name, b.Name), b.Category, b.Description, CategoryGlyph(b.Category), false,
        b.Fields.Select(f =>
        {
            var (min, max) = Range(f.Name);
            var type = f.Kind switch
            {
                BuiltinBehaviours.Kind.Fixed => FieldType.Fixed,
                BuiltinBehaviours.Kind.Int or BuiltinBehaviours.Kind.U16 => FieldType.Int,
                BuiltinBehaviours.Kind.Bool => FieldType.Bool,
                _ => FieldType.Text,
            };
            return new BehaviourField(f.Name, Humanize(f.Name), type, f.Default, min, max, type == FieldType.Fixed);
        }).ToList())).ToList();

    /// <summary>The project's script behaviours (classes deriving from Behaviour).</summary>
    public static IReadOnlyList<BehaviourDef> Scripts(GameProject? p, AgbStudio.Scripting.ScriptCompileResult? last)
    {
        if (p == null) return [];
        if (last is { Behaviours.Count: > 0 })
            return last.Behaviours.Select(b => new BehaviourDef(b.Name, b.Name, "Scripts", "script", "code", true,
                b.Fields.Select(f => new BehaviourField(f.Name, f.Name, TypeOf(f.Type), f.Default ?? "", f.Min ?? 0, f.Max ?? 4, false)).ToList())).ToList();
        var list = new List<BehaviourDef>();
        foreach (var file in p.ScriptFiles())
        {
            string text;
            try { text = File.ReadAllText(file); }
            catch (IOException) { continue; }
            foreach (Match c in Regex.Matches(text, @"class\s+(\w+)\s*:\s*Behaviour\b[^{]*\{"))
            {
                var body = ClassBody(text, c.Index + c.Length - 1);
                var fields = new List<BehaviourField>();
                foreach (Match f in Regex.Matches(body, @"(?:\[Range\(([-\d.]+),\s*([-\d.]+)\)\]\s*)?public\s+(fixed|int|bool|string|byte|short)\s+(\w+)\s*(?:=\s*([^;]+))?;"))
                {
                    var min = f.Groups[1].Success ? double.Parse(f.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
                    var max = f.Groups[2].Success ? double.Parse(f.Groups[2].Value, CultureInfo.InvariantCulture) : 4;
                    var def = f.Groups[5].Success ? f.Groups[5].Value.Trim().Trim('"').TrimEnd('f') : "";
                    // P6.06: a [Range(min, max)] number is a slider in the Inspector; other public fields are text
                    var type = TypeOf(f.Groups[3].Value);
                    fields.Add(new BehaviourField(f.Groups[4].Value, f.Groups[4].Value, type, def, min, max, f.Groups[1].Success && type is FieldType.Fixed or FieldType.Int && max > min));
                }
                list.Add(new BehaviourDef(c.Groups[1].Value, c.Groups[1].Value, "Scripts", "script", "code", true, fields));
            }
        }
        return list;
    }

    static string ClassBody(string text, int open)
    {
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}' && --depth == 0) return text[open..i];
        }
        return text[open..];
    }

    static FieldType TypeOf(string t) => t switch
    {
        "fixed" or "Fixed" or "agb::fixed" => FieldType.Fixed,
        "int" or "byte" or "short" => FieldType.Int,
        "bool" => FieldType.Bool,
        _ => FieldType.Text,
    };

    public static BehaviourDef? Find(string type, GameProject? p, AgbStudio.Scripting.ScriptCompileResult? last) =>
        Builtins.FirstOrDefault(b => b.Name == type) ?? Scripts(p, last).FirstOrDefault(b => b.Name == type);

    /// <summary>A field value as the Inspector shows it: fixed values with two decimals for sliders ("0.25", "3.50").</summary>
    public static string Display(BehaviourField f, string value)
    {
        if (f.Type == FieldType.Fixed && f.Slider && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
            return v.ToString("0.00", CultureInfo.InvariantCulture);
        if (f.Type == FieldType.Int && f.Slider && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
            return Math.Round(n).ToString(CultureInfo.InvariantCulture);
        return value;
    }
}
