using System.Text.Json;
using AgbStudio.Mcp.Protocol;
using AgbStudio.Mcp.Tools;

namespace AgbStudio.Mcp;

public sealed class McpToolRegistry
{
    #region [Variables/Fields]
    readonly Dictionary<string, IMcpToolHandler> _tools = new(StringComparer.OrdinalIgnoreCase);
    #endregion

    #region [Properties]
    public IReadOnlyCollection<IMcpToolHandler> Tools => _tools.Values;
    #endregion

    #region [Unity Methods]
    public McpToolRegistry()
    {
        RegisterDefaults();
    }
    #endregion

    #region [Public Methods]
    public void Register(IMcpToolHandler handler)
    {
        _tools[handler.Name] = handler;
    }

    /// <summary>Takes out the tools Preferences · MCP turned off: clients neither see nor can call them.</summary>
    public void RemoveDisabled(McpSettings settings)
    {
        foreach (var name in settings.DisabledTools) _tools.Remove(name);
    }

    public List<McpTool> GetToolDefinitions()
    {
        return _tools.Values.Select(t => new McpTool
        {
            Name = t.Name,
            Description = t.Description,
            InputSchema = t.InputSchema
        }).OrderBy(t => t.Name).ToList();
    }

    public async Task<McpToolResult> ExecuteToolAsync(string toolName, McpContext context, JsonElement? arguments)
    {
        if (!_tools.TryGetValue(toolName, out var handler))
            return McpToolResult.Text($"Unknown tool: '{toolName}'. Call tools/list to see available tools.", isError: true);

        try
        {
            return await handler.ExecuteAsync(context, arguments);
        }
        catch (Exception ex)
        {
            return McpToolResult.Text($"Tool execution error in '{toolName}': {ex.Message}", isError: true);
        }
    }
    #endregion

    #region [Private Methods]
    void RegisterDefaults()
    {
        // Category 1 & 2: System & Project
        Register(new ServerInfoTool());
        Register(new ToolchainStatusTool());
        Register(new OpenProjectTool());
        Register(new GetProjectSummaryTool());
        Register(new CreateProjectTool());
        Register(new GetSettingsTool());
        Register(new UpdateSettingsTool());

        // Category 3: Scene & Actor
        Register(new ListScenesTool());
        Register(new GetSceneTool());
        Register(new CreateSceneTool());
        Register(new AddActorTool());
        Register(new UpdateActorTool());
        Register(new RemoveActorTool());

        // Category 4: Tilemaps
        Register(new ListTilemapsTool());
        Register(new GetTilemapTool());
        Register(new SetTileTool());

        // Category 5: Scripts & Roslyn
        Register(new ListScriptsTool());
        Register(new ReadScriptTool());
        Register(new WriteScriptTool());
        Register(new AnalyzeScriptsTool());
        Register(new PreviewTranspilationTool());

        // Category 6: Database & Tables
        Register(new ListTablesTool());
        Register(new GetTableTool());
        Register(new InsertTableRowTool());
        Register(new UpdateTableRowTool());
        Register(new DeleteTableRowTool());

        // Category 7: Localization & Dialogue
        Register(new GetLocalizationTool());
        Register(new SetLocalizationStringTool());
        Register(new CheckTextFitTool());
        Register(new GetDialogueTool());
        Register(new UpdateDialogueTool());

        // Category 8: Build & Budget
        Register(new BuildRomTool());
        Register(new GetBudgetTool());
        Register(new GetPaletteSuggestionsTool());

        // Category 9: Emulation & Live Inspection
        Register(new StartSimulationTool());
        Register(new StepSimulationTool());
        Register(new CaptureScreenTool());
        Register(new ReadMemoryTool());
        Register(new ReadLiveActorsTool());
        Register(new SendInputTool());
        Register(new StopSimulationTool());
    }
    #endregion
}
