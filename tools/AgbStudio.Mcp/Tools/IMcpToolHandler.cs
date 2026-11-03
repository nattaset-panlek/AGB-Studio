using System.Text.Json;
using AgbStudio.Mcp.Protocol;

namespace AgbStudio.Mcp.Tools;

public interface IMcpToolHandler
{
    #region [Properties]
    string Name { get; }
    string Description { get; }
    McpToolInputSchema InputSchema { get; }
    #endregion

    #region [Public Methods]
    Task<McpToolResult> ExecuteAsync(McpContext context, JsonElement? arguments);
    #endregion
}
