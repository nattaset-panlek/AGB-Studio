using System.Text.Json;
using AgbStudio.Core.Project;
using AgbStudio.Mcp.Protocol;

namespace AgbStudio.Mcp.Tools;

public sealed class ListTilemapsTool : IMcpToolHandler
{
    #region [Properties]
    public string Name => "agb_list_tilemaps";
    public string Description => "Lists all tilemaps (.map) in the active project.";
    public McpToolInputSchema InputSchema => new();
    #endregion

    #region [Public Methods]
    public Task<McpToolResult> ExecuteAsync(McpContext context, JsonElement? arguments)
    {
        var p = context.CurrentProject;
        if (p == null) return Task.FromResult(McpToolResult.Text("No active project.", isError: true));

        var list = new List<object>();
        foreach (var file in p.MapFiles())
        {
            try
            {
                var m = GameProject.Load<MapFile>(file);
                list.Add(new
                {
                    name = Path.GetFileNameWithoutExtension(file),
                    path = file,
                    tileset = m.Tileset,
                    width = m.Width,
                    height = m.Height,
                    layers = m.Layers.Select(l => new { name = l.Name, bg = l.Bg, type = l.Type.ToString() }).ToList()
                });
            }
            catch
            {
                list.Add(new { name = Path.GetFileNameWithoutExtension(file), path = file, error = "Failed to parse" });
            }
        }
        return Task.FromResult(McpToolResult.Json(list));
    }
    #endregion
}

public sealed class GetTilemapTool : IMcpToolHandler
{
    #region [Properties]
    public string Name => "agb_get_tilemap";
    public string Description => "Returns the complete data of a tilemap including layers and cell counts.";
    public McpToolInputSchema InputSchema => new()
    {
        Properties = new()
        {
            ["name"] = new McpPropertySchema { Type = "string", Description = "Tilemap name (e.g. 'Forest_01')" }
        },
        Required = ["name"]
    };
    #endregion

    #region [Public Methods]
    public Task<McpToolResult> ExecuteAsync(McpContext context, JsonElement? arguments)
    {
        var p = context.CurrentProject;
        if (p == null) return Task.FromResult(McpToolResult.Text("No active project.", isError: true));

        var name = ToolHelpers.GetString(arguments, "name");
        if (string.IsNullOrWhiteSpace(name)) return Task.FromResult(McpToolResult.Text("Error: 'name' is required.", isError: true));

        var mapPath = Path.Combine(p.MapsDir, name.EndsWith(".map", StringComparison.OrdinalIgnoreCase) ? name : name + ".map");
        if (!File.Exists(mapPath)) return Task.FromResult(McpToolResult.Text($"Map '{name}' not found at {mapPath}", isError: true));

        try
        {
            var m = GameProject.Load<MapFile>(mapPath);
            return Task.FromResult(McpToolResult.Json(m));
        }
        catch (Exception ex)
        {
            return Task.FromResult(McpToolResult.Text($"Failed to read map: {ex.Message}", isError: true));
        }
    }
    #endregion
}

public sealed class SetTileTool : IMcpToolHandler
{
    #region [Properties]
    public string Name => "agb_set_tile";
    public string Description => "Sets a single tile or fills a region on a tilemap layer.";
    public McpToolInputSchema InputSchema => new()
    {
        Properties = new()
        {
            ["map"] = new McpPropertySchema { Type = "string", Description = "Tilemap name" },
            ["layerIndex"] = new McpPropertySchema { Type = "number", Description = "Index of the layer (default: 0)" },
            ["x"] = new McpPropertySchema { Type = "number", Description = "X coordinate in tiles" },
            ["y"] = new McpPropertySchema { Type = "number", Description = "Y coordinate in tiles" },
            ["tileId"] = new McpPropertySchema { Type = "number", Description = "Tile ID to set" }
        },
        Required = ["map", "x", "y", "tileId"]
    };
    #endregion

    #region [Public Methods]
    public Task<McpToolResult> ExecuteAsync(McpContext context, JsonElement? arguments)
    {
        var p = context.CurrentProject;
        if (p == null) return Task.FromResult(McpToolResult.Text("No active project.", isError: true));

        var map = ToolHelpers.GetString(arguments, "map");
        var x = ToolHelpers.GetInt(arguments, "x");
        var y = ToolHelpers.GetInt(arguments, "y");
        var tileId = ToolHelpers.GetInt(arguments, "tileId");
        var layerIdx = ToolHelpers.GetInt(arguments, "layerIndex") ?? 0;

        if (string.IsNullOrWhiteSpace(map) || x == null || y == null || tileId == null)
            return Task.FromResult(McpToolResult.Text("Error: 'map', 'x', 'y', and 'tileId' are required.", isError: true));

        var mapPath = Path.Combine(p.MapsDir, map.EndsWith(".map", StringComparison.OrdinalIgnoreCase) ? map : map + ".map");
        if (!File.Exists(mapPath)) return Task.FromResult(McpToolResult.Text($"Map '{map}' not found.", isError: true));

        var m = GameProject.Load<MapFile>(mapPath);
        if (layerIdx < 0 || layerIdx >= m.Layers.Count)
            return Task.FromResult(McpToolResult.Text($"Invalid layer index {layerIdx}. Total layers: {m.Layers.Count}.", isError: true));

        var layer = m.Layers[layerIdx];
        var totalCells = m.Width * m.Height;
        while (layer.Cells.Count < totalCells) layer.Cells.Add(0);

        if (x < 0 || x >= m.Width || y < 0 || y >= m.Height)
            return Task.FromResult(McpToolResult.Text($"Coordinates ({x}, {y}) out of map bounds ({m.Width}x{m.Height}).", isError: true));

        var index = y.Value * m.Width + x.Value;
        layer.Cells[index] = tileId.Value;

        GameProject.Save(mapPath, m);
        return Task.FromResult(McpToolResult.Json(new { success = true, x, y, layerIndex = layerIdx, tileId }));
    }
    #endregion
}
