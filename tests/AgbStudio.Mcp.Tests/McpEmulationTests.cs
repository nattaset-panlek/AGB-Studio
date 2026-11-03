using System.Text.Json;
using AgbStudio.Build;
using AgbStudio.Mcp.Protocol;
using Xunit;

namespace AgbStudio.Mcp.Tests;

public sealed class McpEmulationTests : IDisposable
{
    #region [Variables/Fields]
    readonly McpServer _server;
    readonly string? _romPath;
    #endregion

    #region [Properties]
    public McpServer Server => _server;
    #endregion

    #region [Unity Methods]
    public McpEmulationTests()
    {
        _server = new McpServer();
        var candidate = Path.Combine(NativeBuild.RepoRoot, "build", "test-projects", "sample_Sky_Hopper_demo", "build", "sky_hopper.gba");
        if (File.Exists(candidate))
        {
            _romPath = candidate;
        }
        else
        {
            var anyRom = Directory.EnumerateFiles(NativeBuild.RepoRoot, "*.gba", SearchOption.AllDirectories).FirstOrDefault();
            if (anyRom != null) _romPath = anyRom;
        }
    }

    public void Dispose()
    {
        _server.Context.StopSimulation();
    }
    #endregion

    #region [Public Methods]
    [Fact]
    public async Task EmulationLifecycle_StartStepCaptureReadStop()
    {
        if (string.IsNullOrEmpty(_romPath) || !File.Exists(_romPath)) return;

        var startRes = await CallToolAsync(_server, "agb_start_simulation", new { romPath = _romPath });
        Assert.False(startRes.IsError);
        Assert.NotNull(_server.Context.Session);

        var stepRes = await CallToolAsync(_server, "agb_step_simulation", new { frames = 30 });
        Assert.False(stepRes.IsError);
        Assert.True(_server.Context.Session?.Frame >= 30);

        var shotRes = await CallToolAsync(_server, "agb_capture_screen", new { });
        Assert.False(shotRes.IsError);
        Assert.NotEmpty(shotRes.Content);
        var imageContent = shotRes.Content.FirstOrDefault(c => c.Type == "image");
        Assert.NotNull(imageContent);
        Assert.Equal("image/png", imageContent.MimeType);
        Assert.False(string.IsNullOrEmpty(imageContent.Data));

        var memRes = await CallToolAsync(_server, "agb_read_memory", new { address = "0x02000000", length = 16 });
        Assert.False(memRes.IsError);
        Assert.Contains("bytes", memRes.Content[0].Text);

        var inputRes = await CallToolAsync(_server, "agb_send_input", new { buttons = new[] { "A", "Right" }, frames = 5 });
        Assert.False(inputRes.IsError);

        var stopRes = await CallToolAsync(_server, "agb_stop_simulation", new { });
        Assert.False(stopRes.IsError);
        Assert.Null(_server.Context.Session);
    }
    #endregion

    #region [Private Methods]
    static async Task<McpToolResult> CallToolAsync(McpServer server, string toolName, object args)
    {
        var json = JsonSerializer.Serialize(args, McpJson.Options);
        using var doc = JsonDocument.Parse(json);
        return await server.Registry.ExecuteToolAsync(toolName, server.Context, doc.RootElement);
    }
    #endregion
}
