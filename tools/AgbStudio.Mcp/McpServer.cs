using System.Text.Json;
using AgbStudio.Core.Project;
using AgbStudio.Mcp.Protocol;
using AgbStudio.Mcp.Tools;

namespace AgbStudio.Mcp;

public sealed class McpServer : IDisposable
{
    #region [Variables/Fields]
    readonly McpContext _context;
    readonly McpToolRegistry _registry;
    readonly TextReader _reader;
    readonly TextWriter _writer;
    bool _running;
    #endregion

    #region [Properties]
    public McpContext Context => _context;
    public McpToolRegistry Registry => _registry;
    public bool IsRunning => _running;
    #endregion

    #region [Unity Methods]
    public McpServer(McpContext? context = null, McpToolRegistry? registry = null, TextReader? reader = null, TextWriter? writer = null)
    {
        _context = context ?? new McpContext();
        _registry = registry ?? new McpToolRegistry();
        _reader = reader ?? Console.In;
        _writer = writer ?? Console.Out;
    }
    #endregion

    #region [Public Methods]
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _running = true;
        while (_running && !cancellationToken.IsCancellationRequested)
        {
            string? line;
            try
            {
                line = await _reader.ReadLineAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (line == null) break;
            if (string.IsNullOrWhiteSpace(line)) continue;

            await ProcessMessageAsync(line, cancellationToken);
        }
        _running = false;
    }

    public async Task<string> ProcessMessageAsync(string jsonMessage, CancellationToken cancellationToken = default)
    {
        jsonMessage = jsonMessage.TrimStart('\uFEFF');
        McpRequest? req;
        try
        {
            req = JsonSerializer.Deserialize<McpRequest>(jsonMessage, McpJson.Options);
        }
        catch (Exception ex)
        {
            var err = McpResponse.Failure(null, -32700, $"Parse error: {ex.Message}");
            var errJson = JsonSerializer.Serialize(err, McpJson.Options);
            await WriteLineAsync(errJson, cancellationToken);
            return errJson;
        }

        if (req == null)
        {
            var err = McpResponse.Failure(null, -32600, "Invalid Request");
            var errJson = JsonSerializer.Serialize(err, McpJson.Options);
            await WriteLineAsync(errJson, cancellationToken);
            return errJson;
        }

        if (req.Method.StartsWith("notifications/", StringComparison.OrdinalIgnoreCase) || req.Id == null)
        {
            return "";
        }

        McpResponse resp;
        try
        {
            resp = await HandleRequestAsync(req);
        }
        catch (Exception ex)
        {
            resp = McpResponse.Failure(req.Id, -32603, $"Internal server error: {ex.Message}");
        }

        var json = JsonSerializer.Serialize(resp, McpJson.Options);
        await WriteLineAsync(json, cancellationToken);
        return json;
    }

    public void Stop()
    {
        _running = false;
    }

    public void Dispose()
    {
        Stop();
        _context.Dispose();
    }
    #endregion

    #region [Private Methods]
    async Task<McpResponse> HandleRequestAsync(McpRequest req)
    {
        return req.Method switch
        {
            "initialize" => HandleInitialize(req),
            "ping" => McpResponse.Success(req.Id, new { }),
            "tools/list" => HandleToolsList(req),
            "tools/call" => await HandleToolsCallAsync(req),
            "resources/list" => HandleResourcesList(req),
            "resources/read" => HandleResourcesRead(req),
            "prompts/list" => HandlePromptsList(req),
            "prompts/get" => HandlePromptsGet(req),
            _ => McpResponse.Failure(req.Id, -32601, $"Method not found: {req.Method}")
        };
    }

    McpResponse HandleInitialize(McpRequest req)
    {
        var result = new
        {
            protocolVersion = "2024-11-05",
            capabilities = new
            {
                tools = new { listChanged = false },
                resources = new { listChanged = false, subscribe = false },
                prompts = new { listChanged = false }
            },
            serverInfo = new
            {
                name = "agb-studio-mcp",
                version = "1.0.0"
            }
        };
        return McpResponse.Success(req.Id, result);
    }

    McpResponse HandleToolsList(McpRequest req)
    {
        var tools = _registry.GetToolDefinitions();
        return McpResponse.Success(req.Id, new { tools });
    }

    async Task<McpResponse> HandleToolsCallAsync(McpRequest req)
    {
        if (req.Params is not { } p || p.ValueKind != JsonValueKind.Object)
            return McpResponse.Failure(req.Id, -32602, "Invalid params for tools/call");

        var toolName = ToolHelpers.GetString(p, "name");
        if (string.IsNullOrWhiteSpace(toolName))
            return McpResponse.Failure(req.Id, -32602, "Missing 'name' in tools/call parameters");

        var arguments = ToolHelpers.GetProperty(p, "arguments");
        var result = await _registry.ExecuteToolAsync(toolName, _context, arguments);
        return McpResponse.Success(req.Id, result);
    }

    McpResponse HandleResourcesList(McpRequest req)
    {
        var list = new List<McpResource>();
        var p = _context.CurrentProject;
        if (p != null)
        {
            list.Add(new McpResource { Uri = "agb://project/settings", Name = "Project Settings", Description = "Active project configuration file", MimeType = "application/json" });
            list.Add(new McpResource { Uri = "agb://project/scenes", Name = "Project Scenes", Description = "List of scenes in active project", MimeType = "application/json" });
            list.Add(new McpResource { Uri = "agb://project/tables", Name = "Database Tables", Description = "List of database tables in active project", MimeType = "application/json" });
            list.Add(new McpResource { Uri = "agb://budget/current", Name = "Hardware Budget", Description = "Current GBA hardware memory and palette budget", MimeType = "application/json" });
        }
        return McpResponse.Success(req.Id, new { resources = list });
    }

    McpResponse HandleResourcesRead(McpRequest req)
    {
        if (req.Params is not { } p || p.ValueKind != JsonValueKind.Object)
            return McpResponse.Failure(req.Id, -32602, "Invalid params for resources/read");

        var uri = ToolHelpers.GetString(p, "uri");
        if (string.IsNullOrWhiteSpace(uri))
            return McpResponse.Failure(req.Id, -32602, "Missing 'uri' parameter");

        var proj = _context.CurrentProject;
        if (proj == null)
            return McpResponse.Failure(req.Id, -32603, "No active project");

        McpResourceContent content;
        switch (uri)
        {
            case "agb://project/settings":
                content = new McpResourceContent { Uri = uri, Text = JsonSerializer.Serialize(proj.Settings, McpJson.Options) };
                break;
            case "agb://project/scenes":
                var scenes = proj.SceneFiles().Select(f => Path.GetFileNameWithoutExtension(f)).ToList();
                content = new McpResourceContent { Uri = uri, Text = JsonSerializer.Serialize(scenes, McpJson.Options) };
                break;
            case "agb://project/tables":
                var tables = proj.TableFiles().Select(f => Path.GetFileNameWithoutExtension(f)).ToList();
                content = new McpResourceContent { Uri = uri, Text = JsonSerializer.Serialize(tables, McpJson.Options) };
                break;
            default:
                return McpResponse.Failure(req.Id, -32602, $"Resource not found: {uri}");
        }

        return McpResponse.Success(req.Id, new { contents = new[] { content } });
    }

    McpResponse HandlePromptsList(McpRequest req)
    {
        var prompts = new List<McpPrompt>
        {
            new McpPrompt { Name = "audit-gba-budget", Description = "Audits ROM size, VRAM, OAM and palette limits for GBA hardware" },
            new McpPrompt { Name = "inspect-game-scene", Description = "Audits scene actors, components and map bindings", Arguments = [new McpPromptArgument { Name = "scene", Description = "Scene name", Required = true }] },
            new McpPrompt { Name = "debug-running-game", Description = "Runs simulation step, checks live actors, and captures screenshot" }
        };
        return McpResponse.Success(req.Id, new { prompts });
    }

    McpResponse HandlePromptsGet(McpRequest req)
    {
        if (req.Params is not { } p || p.ValueKind != JsonValueKind.Object)
            return McpResponse.Failure(req.Id, -32602, "Invalid params for prompts/get");

        var name = ToolHelpers.GetString(p, "name");
        var promptMsg = name switch
        {
            "audit-gba-budget" => "Inspect the active project hardware budget using agb_get_budget and identify any memory or palette optimizations.",
            "inspect-game-scene" => "Inspect the specified scene using agb_get_scene and verify actor positions, colliders and behaviour fields.",
            "debug-running-game" => "Start simulation using agb_start_simulation, step 60 frames, read live actors with agb_read_live_actors, and capture screen using agb_capture_screen.",
            _ => null
        };

        if (promptMsg == null)
            return McpResponse.Failure(req.Id, -32602, $"Prompt not found: {name}");

        var messages = new[]
        {
            new { role = "user", content = new { type = "text", text = promptMsg } }
        };
        return McpResponse.Success(req.Id, new { description = name, messages });
    }

    async Task WriteLineAsync(string line, CancellationToken cancellationToken)
    {
        await _writer.WriteLineAsync(line.AsMemory(), cancellationToken);
        await _writer.FlushAsync(cancellationToken);
    }
    #endregion
}
