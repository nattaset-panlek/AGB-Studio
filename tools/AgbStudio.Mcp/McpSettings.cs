using System.Text.Json;

namespace AgbStudio.Mcp;

/// <summary>What Preferences · MCP saves for the server: the tools an AI client may not call. Read when the server starts.</summary>
public sealed class McpSettings
{
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public List<string> DisabledTools { get; set; } = [];

    /// <summary>AGB_MCP_SETTINGS, else mcp.json beside the editor's preferences.</summary>
    public static string DefaultPath =>
        Environment.GetEnvironmentVariable("AGB_MCP_SETTINGS") is { Length: > 0 } path
            ? path
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AGBStudio", "mcp.json");

    public static McpSettings Load(string? path = null)
    {
        path ??= DefaultPath;
        if (!File.Exists(path)) return new McpSettings();
        try { return JsonSerializer.Deserialize<McpSettings>(File.ReadAllText(path)) ?? new McpSettings(); }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException) { return new McpSettings(); }
    }

    public void Save(string? path = null)
    {
        path ??= DefaultPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, Json));
    }
}
