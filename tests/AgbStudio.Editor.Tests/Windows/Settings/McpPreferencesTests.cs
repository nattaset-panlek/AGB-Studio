using System.Text.Json.Nodes;
using AgbStudio.Editor.Windows.Settings;
using AgbStudio.Mcp;

namespace AgbStudio.Editor.Tests.Windows.Settings;

/// <summary>Preferences · MCP (T-S10): client settings files are changed safely, and disabled tools leave the server.</summary>
public sealed class McpPreferencesTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "agb-mcp-" + Guid.NewGuid().ToString("N"));
    readonly McpClients _clients;

    public McpPreferencesTests()
    {
        Directory.CreateDirectory(_root);
        _clients = new McpClients(Path.Combine(_root, "home"), Path.Combine(_root, "appdata"), @"C:\Apps\AGBStudio.exe");
    }

    public void Dispose() => Directory.Delete(_root, true);

    McpClient Client(string id) => _clients.All.Single(c => c.Id == id);

    static void WriteConfig(McpClient c, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(c.ConfigPath)!);
        File.WriteAllText(c.ConfigPath, text);
    }

    [Fact]
    public void Enabling_AddsOnlyItsEntry_KeepsTheRest_AndBacksUpOnce()
    {
        var c = Client("claude-code");
        const string original = """{"theme":"dark","mcpServers":{"other":{"command":"x"}},"projects":{"a":1}}""";
        WriteConfig(c, original);

        _clients.SetEnabled(c, true);
        Assert.True(_clients.IsEnabled(c));
        var config = JsonNode.Parse(File.ReadAllText(c.ConfigPath))!;
        Assert.Equal("dark", config["theme"]!.GetValue<string>());
        Assert.Equal("x", config["mcpServers"]!["other"]!["command"]!.GetValue<string>());
        Assert.Equal(1, config["projects"]!["a"]!.GetValue<int>());
        Assert.Equal(@"C:\Apps\AGBStudio.exe", config["mcpServers"]![McpClients.ServerName]!["command"]!.GetValue<string>());
        Assert.Equal("--mcp", config["mcpServers"]![McpClients.ServerName]!["args"]![0]!.GetValue<string>());
        Assert.Equal(original, File.ReadAllText(c.ConfigPath + ".agb-backup"));

        _clients.SetEnabled(c, false);
        Assert.False(_clients.IsEnabled(c));
        config = JsonNode.Parse(File.ReadAllText(c.ConfigPath))!;
        Assert.Equal("x", config["mcpServers"]!["other"]!["command"]!.GetValue<string>());
        Assert.Equal(original, File.ReadAllText(c.ConfigPath + ".agb-backup"));
    }

    [Fact]
    public void AFileItCannotRead_IsLeftAsItIs()
    {
        var c = Client("vscode");
        const string broken = "{ // my comment\n \"servers\": {} }";
        WriteConfig(c, broken);
        Assert.False(_clients.IsEnabled(c));
        Assert.Contains("left as it is", Assert.Throws<InvalidDataException>(() => _clients.SetEnabled(c, true)).Message);
        Assert.Equal(broken, File.ReadAllText(c.ConfigPath));
        Assert.False(File.Exists(c.ConfigPath + ".agb-backup"));
    }

    [Fact]
    public void AMissingFile_IsCreated_AndVsCodeGetsItsOwnShape()
    {
        var c = Client("vscode");
        _clients.SetEnabled(c, true);
        var entry = JsonNode.Parse(File.ReadAllText(c.ConfigPath))!["servers"]![McpClients.ServerName]!;
        Assert.Equal("stdio", entry["type"]!.GetValue<string>());
    }

    [Fact]
    public void TurningOffWhatIsNotThere_WritesNothing()
    {
        var c = Client("cursor");
        _clients.SetEnabled(c, false);
        Assert.False(File.Exists(c.ConfigPath));
    }

    [Fact]
    public void AnEntryFromAnotherCopy_IsReported()
    {
        var c = Client("cursor");
        new McpClients(Path.Combine(_root, "home"), Path.Combine(_root, "appdata"), @"D:\Old\AGBStudio.exe").SetEnabled(c, true);
        Assert.True(_clients.PointsElsewhere(c));
        _clients.SetEnabled(c, true);
        Assert.False(_clients.PointsElsewhere(c));
    }

    [Fact]
    public async Task DisabledTools_LeaveTheServersList()
    {
        var path = Path.Combine(_root, "mcp.json");
        new McpSettings { DisabledTools = ["agb_server_info"] }.Save(path);
        var registry = new McpToolRegistry();
        var before = registry.Tools.Count;
        registry.RemoveDisabled(McpSettings.Load(path));
        Assert.Equal(before - 1, registry.Tools.Count);
        Assert.DoesNotContain(registry.GetToolDefinitions(), t => t.Name == "agb_server_info");
        var result = await registry.ExecuteToolAsync("agb_server_info", null!, null);
        Assert.True(result.IsError);
    }

    [Fact]
    public void AMissingOrBrokenSettingsFile_MeansEveryToolIsOn()
    {
        var path = Path.Combine(_root, "mcp.json");
        Assert.Empty(McpSettings.Load(path).DisabledTools);
        File.WriteAllText(path, "not json");
        Assert.Empty(McpSettings.Load(path).DisabledTools);
    }

    [Fact]
    public async Task Probe_StartsTheRealServer_AndCountsItsTools()
    {
        var exe = Path.Combine(AppContext.BaseDirectory, "AGBStudio.exe");
        Assert.True(File.Exists(exe), exe);
        var result = await new McpClients(_root, _root, exe).ProbeAsync(TimeSpan.FromSeconds(60));
        Assert.True(result.Ok, result.Message);
        Assert.True(result.Tools > 10, result.Message);
    }

    [Fact]
    public async Task Probe_WhenTheProgramIsMissing_SaysSo()
    {
        var result = await new McpClients(_root, _root, Path.Combine(_root, "nope.exe")).ProbeAsync();
        Assert.False(result.Ok);
        Assert.Contains("could not be tested", result.Message);
    }
}
