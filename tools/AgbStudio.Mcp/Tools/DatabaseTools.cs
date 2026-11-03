using System.Text.Json;
using AgbStudio.Core.Project;
using AgbStudio.Mcp.Protocol;

namespace AgbStudio.Mcp.Tools;

public sealed class ListTablesTool : IMcpToolHandler
{
    #region [Properties]
    public string Name => "agb_list_tables";
    public string Description => "Lists all database tables (.db) in the active project.";
    public McpToolInputSchema InputSchema => new();
    #endregion

    #region [Public Methods]
    public Task<McpToolResult> ExecuteAsync(McpContext context, JsonElement? arguments)
    {
        var p = context.CurrentProject;
        if (p == null) return Task.FromResult(McpToolResult.Text("No active project.", isError: true));

        var list = new List<object>();
        foreach (var file in p.TableFiles())
        {
            try
            {
                var t = GameProject.Load<TableFile>(file);
                list.Add(new
                {
                    name = t.Name,
                    path = file,
                    keyField = t.KeyField,
                    rowCount = t.Rows.Count,
                    fieldCount = t.Fields.Count,
                    fields = t.Fields.Select(f => new { name = f.Name, type = f.Type, localized = f.Localized }).ToList()
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

public sealed class GetTableTool : IMcpToolHandler
{
    #region [Properties]
    public string Name => "agb_get_table";
    public string Description => "Returns the schema and all rows for a database table.";
    public McpToolInputSchema InputSchema => new()
    {
        Properties = new()
        {
            ["name"] = new McpPropertySchema { Type = "string", Description = "Table name (e.g. 'Items')" }
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

        var tablePath = Path.Combine(p.DataDir, name.EndsWith(".db", StringComparison.OrdinalIgnoreCase) ? name : name + ".db");
        if (!File.Exists(tablePath)) return Task.FromResult(McpToolResult.Text($"Table '{name}' not found at {tablePath}", isError: true));

        try
        {
            var t = GameProject.Load<TableFile>(tablePath);
            return Task.FromResult(McpToolResult.Json(t));
        }
        catch (Exception ex)
        {
            return Task.FromResult(McpToolResult.Text($"Failed to read table: {ex.Message}", isError: true));
        }
    }
    #endregion
}

public sealed class InsertTableRowTool : IMcpToolHandler
{
    #region [Properties]
    public string Name => "agb_insert_table_row";
    public string Description => "Inserts a new record row into a database table.";
    public McpToolInputSchema InputSchema => new()
    {
        Properties = new()
        {
            ["table"] = new McpPropertySchema { Type = "string", Description = "Table name" },
            ["row"] = new McpPropertySchema { Type = "object", Description = "Key-value dictionary representing the record row" }
        },
        Required = ["table", "row"]
    };
    #endregion

    #region [Public Methods]
    public Task<McpToolResult> ExecuteAsync(McpContext context, JsonElement? arguments)
    {
        var p = context.CurrentProject;
        if (p == null) return Task.FromResult(McpToolResult.Text("No active project.", isError: true));

        var table = ToolHelpers.GetString(arguments, "table");
        var rowProp = ToolHelpers.GetProperty(arguments, "row");
        if (string.IsNullOrWhiteSpace(table) || rowProp == null || rowProp.Value.ValueKind != JsonValueKind.Object)
            return Task.FromResult(McpToolResult.Text("Error: 'table' and 'row' (object) are required.", isError: true));

        var tablePath = Path.Combine(p.DataDir, table.EndsWith(".db", StringComparison.OrdinalIgnoreCase) ? table : table + ".db");
        if (!File.Exists(tablePath)) return Task.FromResult(McpToolResult.Text($"Table '{table}' not found.", isError: true));

        var t = GameProject.Load<TableFile>(tablePath);
        var rowDict = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var prop in rowProp.Value.EnumerateObject())
            rowDict[prop.Name] = prop.Value.GetString() ?? prop.Value.ToString();

        t.Rows.Add(rowDict);
        GameProject.Save(tablePath, t);
        return Task.FromResult(McpToolResult.Json(new { success = true, rowCount = t.Rows.Count, row = rowDict }));
    }
    #endregion
}

public sealed class UpdateTableRowTool : IMcpToolHandler
{
    #region [Properties]
    public string Name => "agb_update_table_row";
    public string Description => "Updates a record row in a database table identified by key field value.";
    public McpToolInputSchema InputSchema => new()
    {
        Properties = new()
        {
            ["table"] = new McpPropertySchema { Type = "string", Description = "Table name" },
            ["keyValue"] = new McpPropertySchema { Type = "string", Description = "Value of the key field identifying the row" },
            ["values"] = new McpPropertySchema { Type = "object", Description = "Dictionary of column values to update" }
        },
        Required = ["table", "keyValue", "values"]
    };
    #endregion

    #region [Public Methods]
    public Task<McpToolResult> ExecuteAsync(McpContext context, JsonElement? arguments)
    {
        var p = context.CurrentProject;
        if (p == null) return Task.FromResult(McpToolResult.Text("No active project.", isError: true));

        var table = ToolHelpers.GetString(arguments, "table");
        var keyValue = ToolHelpers.GetString(arguments, "keyValue");
        var valProp = ToolHelpers.GetProperty(arguments, "values");
        if (string.IsNullOrWhiteSpace(table) || keyValue == null || valProp == null || valProp.Value.ValueKind != JsonValueKind.Object)
            return Task.FromResult(McpToolResult.Text("Error: 'table', 'keyValue', and 'values' are required.", isError: true));

        var tablePath = Path.Combine(p.DataDir, table.EndsWith(".db", StringComparison.OrdinalIgnoreCase) ? table : table + ".db");
        if (!File.Exists(tablePath)) return Task.FromResult(McpToolResult.Text($"Table '{table}' not found.", isError: true));

        var t = GameProject.Load<TableFile>(tablePath);
        var keyField = t.KeyField;
        var row = t.Rows.FirstOrDefault(r => r.TryGetValue(keyField, out var k) && string.Equals(k, keyValue, StringComparison.OrdinalIgnoreCase));
        if (row == null) return Task.FromResult(McpToolResult.Text($"Row with {keyField}='{keyValue}' not found in table '{table}'.", isError: true));

        foreach (var prop in valProp.Value.EnumerateObject())
            row[prop.Name] = prop.Value.GetString() ?? prop.Value.ToString();

        GameProject.Save(tablePath, t);
        return Task.FromResult(McpToolResult.Json(new { success = true, row }));
    }
    #endregion
}

public sealed class DeleteTableRowTool : IMcpToolHandler
{
    #region [Properties]
    public string Name => "agb_delete_table_row";
    public string Description => "Deletes a record row from a database table identified by key field value.";
    public McpToolInputSchema InputSchema => new()
    {
        Properties = new()
        {
            ["table"] = new McpPropertySchema { Type = "string", Description = "Table name" },
            ["keyValue"] = new McpPropertySchema { Type = "string", Description = "Value of the key field identifying the row to delete" }
        },
        Required = ["table", "keyValue"]
    };
    #endregion

    #region [Public Methods]
    public Task<McpToolResult> ExecuteAsync(McpContext context, JsonElement? arguments)
    {
        var p = context.CurrentProject;
        if (p == null) return Task.FromResult(McpToolResult.Text("No active project.", isError: true));

        var table = ToolHelpers.GetString(arguments, "table");
        var keyValue = ToolHelpers.GetString(arguments, "keyValue");
        if (string.IsNullOrWhiteSpace(table) || keyValue == null)
            return Task.FromResult(McpToolResult.Text("Error: 'table' and 'keyValue' are required.", isError: true));

        var tablePath = Path.Combine(p.DataDir, table.EndsWith(".db", StringComparison.OrdinalIgnoreCase) ? table : table + ".db");
        if (!File.Exists(tablePath)) return Task.FromResult(McpToolResult.Text($"Table '{table}' not found.", isError: true));

        var t = GameProject.Load<TableFile>(tablePath);
        var keyField = t.KeyField;
        var removed = t.Rows.RemoveAll(r => r.TryGetValue(keyField, out var k) && string.Equals(k, keyValue, StringComparison.OrdinalIgnoreCase));

        if (removed == 0) return Task.FromResult(McpToolResult.Text($"Row with {keyField}='{keyValue}' not found.", isError: true));

        GameProject.Save(tablePath, t);
        return Task.FromResult(McpToolResult.Json(new { success = true, removedCount = removed, remainingRows = t.Rows.Count }));
    }
    #endregion
}
