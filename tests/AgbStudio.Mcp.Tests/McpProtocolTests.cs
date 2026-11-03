using System.Text.Json;
using AgbStudio.Mcp.Protocol;
using Xunit;

namespace AgbStudio.Mcp.Tests;

public sealed class McpProtocolTests
{
    #region [Public Methods]
    [Fact]
    public async Task Initialize_ReturnsProtocolVersionAndCapabilities()
    {
        using var server = new McpServer();
        var req = new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "initialize",
            @params = new { protocolVersion = "2024-11-05" }
        };

        var responseJson = await server.ProcessMessageAsync(JsonSerializer.Serialize(req));
        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        Assert.Equal("2.0", root.GetProperty("jsonrpc").GetString());
        Assert.Equal(1, root.GetProperty("id").GetInt32());
        var result = root.GetProperty("result");
        Assert.Equal("2024-11-05", result.GetProperty("protocolVersion").GetString());
        Assert.Equal("agb-studio-mcp", result.GetProperty("serverInfo").GetProperty("name").GetString());
        Assert.True(result.GetProperty("capabilities").TryGetProperty("tools", out _));
        Assert.True(result.GetProperty("capabilities").TryGetProperty("resources", out _));
        Assert.True(result.GetProperty("capabilities").TryGetProperty("prompts", out _));
    }

    [Fact]
    public async Task ToolsList_ReturnsOver35RegisteredTools()
    {
        using var server = new McpServer();
        var req = new { jsonrpc = "2.0", id = 2, method = "tools/list" };
        var responseJson = await server.ProcessMessageAsync(JsonSerializer.Serialize(req));
        using var doc = JsonDocument.Parse(responseJson);

        var tools = doc.RootElement.GetProperty("result").GetProperty("tools");
        Assert.True(tools.GetArrayLength() >= 35, $"Expected >= 35 tools, found {tools.GetArrayLength()}");

        var toolNames = tools.EnumerateArray().Select(t => t.GetProperty("name").GetString()).ToList();
        Assert.Contains("agb_server_info", toolNames);
        Assert.Contains("agb_open_project", toolNames);
        Assert.Contains("agb_list_scenes", toolNames);
        Assert.Contains("agb_get_scene", toolNames);
        Assert.Contains("agb_create_scene", toolNames);
        Assert.Contains("agb_add_actor", toolNames);
        Assert.Contains("agb_list_tilemaps", toolNames);
        Assert.Contains("agb_set_tile", toolNames);
        Assert.Contains("agb_list_scripts", toolNames);
        Assert.Contains("agb_analyze_scripts", toolNames);
        Assert.Contains("agb_list_tables", toolNames);
        Assert.Contains("agb_get_localization", toolNames);
        Assert.Contains("agb_check_text_fit", toolNames);
        Assert.Contains("agb_build_rom", toolNames);
        Assert.Contains("agb_get_budget", toolNames);
        Assert.Contains("agb_start_simulation", toolNames);
        Assert.Contains("agb_capture_screen", toolNames);
    }

    [Fact]
    public async Task Ping_ReturnsSuccess()
    {
        using var server = new McpServer();
        var req = new { jsonrpc = "2.0", id = "ping-123", method = "ping" };
        var responseJson = await server.ProcessMessageAsync(JsonSerializer.Serialize(req));
        using var doc = JsonDocument.Parse(responseJson);

        Assert.Equal("ping-123", doc.RootElement.GetProperty("id").GetString());
        Assert.True(doc.RootElement.TryGetProperty("result", out _));
    }

    [Fact]
    public async Task MalformedJson_ReturnsParseError()
    {
        using var server = new McpServer();
        var responseJson = await server.ProcessMessageAsync("{ not valid json }");
        using var doc = JsonDocument.Parse(responseJson);

        Assert.True(doc.RootElement.TryGetProperty("error", out var err));
        Assert.Equal(-32700, err.GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task UnknownMethod_ReturnsMethodNotFound()
    {
        using var server = new McpServer();
        var req = new { jsonrpc = "2.0", id = 99, method = "non_existent_method" };
        var responseJson = await server.ProcessMessageAsync(JsonSerializer.Serialize(req));
        using var doc = JsonDocument.Parse(responseJson);

        Assert.True(doc.RootElement.TryGetProperty("error", out var err));
        Assert.Equal(-32601, err.GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task Notification_ReturnsEmptyString()
    {
        using var server = new McpServer();
        var notification = new { jsonrpc = "2.0", method = "notifications/initialized" };
        var result = await server.ProcessMessageAsync(JsonSerializer.Serialize(notification));
        Assert.Equal("", result);
    }
    #endregion
}
