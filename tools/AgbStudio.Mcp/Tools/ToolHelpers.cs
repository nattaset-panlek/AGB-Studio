using System.Text.Json;

namespace AgbStudio.Mcp.Tools;

public static class ToolHelpers
{
    #region [Public Methods]
    public static string? GetString(JsonElement? args, string name)
    {
        if (args is not { } element || element.ValueKind != JsonValueKind.Object) return null;
        if (element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
            return prop.GetString();
        return null;
    }

    public static int? GetInt(JsonElement? args, string name)
    {
        if (args is not { } element || element.ValueKind != JsonValueKind.Object) return null;
        if (element.TryGetProperty(name, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var val)) return val;
            if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out var parsed)) return parsed;
        }
        return null;
    }

    public static double? GetDouble(JsonElement? args, string name)
    {
        if (args is not { } element || element.ValueKind != JsonValueKind.Object) return null;
        if (element.TryGetProperty(name, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDouble(out var val)) return val;
            if (prop.ValueKind == JsonValueKind.String && double.TryParse(prop.GetString(), System.Globalization.CultureInfo.InvariantCulture, out var parsed)) return parsed;
        }
        return null;
    }

    public static bool? GetBool(JsonElement? args, string name)
    {
        if (args is not { } element || element.ValueKind != JsonValueKind.Object) return null;
        if (element.TryGetProperty(name, out var prop))
        {
            if (prop.ValueKind is JsonValueKind.True or JsonValueKind.False) return prop.GetBoolean();
            if (prop.ValueKind == JsonValueKind.String && bool.TryParse(prop.GetString(), out var parsed)) return parsed;
        }
        return null;
    }

    public static JsonElement? GetProperty(JsonElement? args, string name)
    {
        if (args is not { } element || element.ValueKind != JsonValueKind.Object) return null;
        if (element.TryGetProperty(name, out var prop)) return prop;
        return null;
    }

    public static AgbStudio.Scripting.ScriptCompileContext CreateCompileContext(AgbStudio.Core.Project.GameProject p)
    {
        return new AgbStudio.Scripting.ScriptCompileContext
        {
            ProjectRoot = p.Root,
            ProjectName = p.Settings.Name,
            SaveFields = p.Settings.Save.Fields,
            Tables = p.TableFiles().Select(AgbStudio.Core.Project.GameProject.Load<AgbStudio.Core.Project.TableFile>).ToList(),
            Tags = p.Settings.Tags.Select((t, i) => (t, i)).ToDictionary(x => x.t, x => x.i),
            Scenes = p.SceneFiles().Select(Path.GetFileNameWithoutExtension).Select((s, i) => (s!, i)).ToDictionary(x => x.Item1, x => x.i),
            Sounds = p.SoundFiles().Select(Path.GetFileNameWithoutExtension).Select((s, i) => (s!, i)).ToDictionary(x => x.Item1, x => x.i),
            Music = p.MusicFiles().Select(Path.GetFileNameWithoutExtension).Select((m, i) => (m!, i)).ToDictionary(x => x.Item1, x => x.i),
            Strings = AgbStudio.Scripting.ScriptCompileContext.Indexed(p.StringTables().Select(AgbStudio.Core.Project.GameProject.Load<AgbStudio.Core.Project.StringTable>).SelectMany(t => t.Strings.Keys).Distinct().OrderBy(k => k, StringComparer.Ordinal))
        };
    }
    #endregion
}
