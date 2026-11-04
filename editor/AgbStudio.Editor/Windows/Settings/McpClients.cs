using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AgbStudio.Editor.Windows.Settings;

/// <summary>An AI client that can start AGB Studio's MCP server, and where its configuration lives.</summary>
public sealed record McpClient(string Id, string Name, string ConfigPath, string ServersKey, bool NeedsType = false)
{
    /// <summary>The client looks installed: its configuration file or folder exists.</summary>
    public bool Detected => File.Exists(ConfigPath) || (Path.GetFileName(ConfigPath) != ".claude.json" && Directory.Exists(Path.GetDirectoryName(ConfigPath)));
}

/// <summary>The answer of a server started to test it.</summary>
public sealed record McpProbeResult(bool Ok, int Tools, string Message);

/// <summary>
/// Preferences · MCP: AI clients start the server themselves (it speaks over standard input and output), so turning it on
/// means adding it to the client's configuration. Only the "agb-studio" entry is touched; the rest of the file is kept, the
/// first change leaves a backup beside it, and a file AGB Studio cannot read is never overwritten.
/// </summary>
public sealed class McpClients(string home, string appData, string exe)
{
    public const string ServerName = "agb-studio";
    static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public static McpClients ForThisMachine() => new(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        Environment.ProcessPath ?? "AGBStudio.exe");

    public string Exe { get; } = exe;

    public IReadOnlyList<McpClient> All { get; } =
    [
        new("claude-code", "Claude Code", Path.Combine(home, ".claude.json"), "mcpServers"),
        new("claude-desktop", "Claude Desktop", Path.Combine(appData, "Claude", "claude_desktop_config.json"), "mcpServers"),
        new("cursor", "Cursor", Path.Combine(home, ".cursor", "mcp.json"), "mcpServers"),
        new("vscode", "VS Code", Path.Combine(appData, "Code", "User", "mcp.json"), "servers", NeedsType: true),
    ];

    /// <summary>The configuration of any other client, for the Copy button.</summary>
    public string Snippet() =>
        new JsonObject { ["mcpServers"] = new JsonObject { [ServerName] = Entry(false) } }.ToJsonString(Indented);

    JsonObject Entry(bool needsType)
    {
        var entry = new JsonObject();
        if (needsType) entry["type"] = "stdio";
        entry["command"] = Exe;
        entry["args"] = new JsonArray("--mcp");
        return entry;
    }

    static JsonObject ReadConfig(McpClient c)
    {
        if (!File.Exists(c.ConfigPath)) return [];
        var text = File.ReadAllText(c.ConfigPath);
        if (string.IsNullOrWhiteSpace(text)) return [];
        try
        {
            return JsonNode.Parse(text) as JsonObject ?? throw new InvalidDataException($"{c.Name}'s settings file is not a JSON object.");
        }
        catch (JsonException e)
        {
            throw new InvalidDataException($"{c.Name}'s settings file can't be read, so it was left as it is ({e.Message}). Fix {c.ConfigPath}, then try again.");
        }
    }

    public bool IsEnabled(McpClient c)
    {
        try { return ReadConfig(c)[c.ServersKey]?[ServerName] is JsonObject; }
        catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException) { return false; }
    }

    /// <summary>The entry starts another copy of AGB Studio than this one (it was moved, or it is another version).</summary>
    public bool PointsElsewhere(McpClient c)
    {
        try
        {
            return ReadConfig(c)[c.ServersKey]?[ServerName]?["command"]?.GetValue<string>() is { } command
                && !string.Equals(command, Exe, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException or InvalidOperationException) { return false; }
    }

    /// <summary>Adds or removes the server. Throws <see cref="InvalidDataException"/> when the file cannot be read.</summary>
    public void SetEnabled(McpClient c, bool on)
    {
        var config = ReadConfig(c);
        if (config[c.ServersKey] is not JsonObject servers)
        {
            if (!on) return;
            config[c.ServersKey] = servers = [];
        }
        if (on) servers[ServerName] = Entry(c.NeedsType);
        else if (!servers.Remove(ServerName)) return;

        Directory.CreateDirectory(Path.GetDirectoryName(c.ConfigPath)!);
        var backup = c.ConfigPath + ".agb-backup";
        if (File.Exists(c.ConfigPath) && !File.Exists(backup)) File.Copy(c.ConfigPath, backup);
        var temp = c.ConfigPath + ".agb-tmp";
        File.WriteAllText(temp, config.ToJsonString(Indented));
        File.Move(temp, c.ConfigPath, true);
    }

    /// <summary>Starts a server as a client would, asks for its tools and stops it.</summary>
    public async Task<McpProbeResult> ProbeAsync(TimeSpan? timeout = null)
    {
        var info = new ProcessStartInfo(Exe, "--mcp")
        {
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
        };
        Process? process = null;
        using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(20));
        try
        {
            process = Process.Start(info);
            if (process == null) return new McpProbeResult(false, 0, "The server did not start.");
            process.ErrorDataReceived += (_, _) => { };
            process.BeginErrorReadLine();
            async Task<JsonNode?> Ask(int id, string method, string parameters)
            {
                await process.StandardInput.WriteLineAsync($"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"method\":\"{method}\",\"params\":{parameters}}}");
                await process.StandardInput.FlushAsync(cts.Token);
                while (await process.StandardOutput.ReadLineAsync(cts.Token) is { } line)
                    if (JsonNode.Parse(line) is { } reply && reply["id"]?.GetValue<int>() == id) return reply;
                return null;
            }
            const string hello = "{\"protocolVersion\":\"2024-11-05\",\"capabilities\":{},\"clientInfo\":{\"name\":\"AGB Studio Preferences\",\"version\":\"1\"}}";
            if (await Ask(1, "initialize", hello) is not { } init || init["result"] == null) return new McpProbeResult(false, 0, "The server started but did not answer.");
            var tools = (await Ask(2, "tools/list", "{}"))?["result"]?["tools"]?.AsArray().Count ?? 0;
            return new McpProbeResult(true, tools, $"Working · {tools} tools.");
        }
        catch (OperationCanceledException) { return new McpProbeResult(false, 0, "The server did not answer in time."); }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or IOException or JsonException or InvalidOperationException)
        {
            return new McpProbeResult(false, 0, $"The server could not be tested: {e.Message}");
        }
        finally
        {
            if (process != null)
            {
                try { if (!process.HasExited) process.Kill(true); }
                catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { }
                process.Dispose();
            }
        }
    }
}
