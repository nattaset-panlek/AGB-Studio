using System.Text.Json;
using AgbStudio.Core.Project;
using AgbStudio.Mcp.Protocol;

namespace AgbStudio.Mcp.Tools;

public sealed class ListScenesTool : IMcpToolHandler
{
    #region [Properties]
    public string Name => "agb_list_scenes";
    public string Description => "Lists all scenes in the active project with their tile dimensions and actor counts.";
    public McpToolInputSchema InputSchema => new();
    #endregion

    #region [Public Methods]
    public Task<McpToolResult> ExecuteAsync(McpContext context, JsonElement? arguments)
    {
        var p = context.CurrentProject;
        if (p == null) return Task.FromResult(McpToolResult.Text("No active project.", isError: true));

        var list = new List<object>();
        foreach (var file in p.SceneFiles())
        {
            try
            {
                var s = GameProject.Load<SceneFile>(file);
                list.Add(new
                {
                    name = Path.GetFileNameWithoutExtension(file),
                    path = file,
                    width = s.Width,
                    height = s.Height,
                    actors = s.Actors.Count,
                    map = s.Map,
                    isDefault = s.Name == p.Settings.LastScene
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

public sealed class GetSceneTool : IMcpToolHandler
{
    #region [Properties]
    public string Name => "agb_get_scene";
    public string Description => "Returns the complete scene data (camera, music, events, actors, components) for a given scene name.";
    public McpToolInputSchema InputSchema => new()
    {
        Properties = new()
        {
            ["name"] = new McpPropertySchema { Type = "string", Description = "Scene name (e.g. 'Title' or 'Forest_01')" }
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

        var scenePath = Path.Combine(p.ScenesDir, name.EndsWith(".scene", StringComparison.OrdinalIgnoreCase) ? name : name + ".scene");
        if (!File.Exists(scenePath))
            return Task.FromResult(McpToolResult.Text($"Scene '{name}' not found at {scenePath}", isError: true));

        try
        {
            var s = GameProject.Load<SceneFile>(scenePath);
            return Task.FromResult(McpToolResult.Json(s));
        }
        catch (Exception ex)
        {
            return Task.FromResult(McpToolResult.Text($"Failed to read scene: {ex.Message}", isError: true));
        }
    }
    #endregion
}

public sealed class CreateSceneTool : IMcpToolHandler
{
    #region [Properties]
    public string Name => "agb_create_scene";
    public string Description => "Creates a new scene in the project with specified width and height.";
    public McpToolInputSchema InputSchema => new()
    {
        Properties = new()
        {
            ["name"] = new McpPropertySchema { Type = "string", Description = "Scene name" },
            ["width"] = new McpPropertySchema { Type = "number", Description = "Width in 16px tiles (default: 30)" },
            ["height"] = new McpPropertySchema { Type = "number", Description = "Height in 16px tiles (default: 20)" },
            ["map"] = new McpPropertySchema { Type = "string", Description = "Optional tilemap name to associate" }
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

        var width = ToolHelpers.GetInt(arguments, "width") ?? 30;
        var height = ToolHelpers.GetInt(arguments, "height") ?? 20;
        var map = ToolHelpers.GetString(arguments, "map") ?? "";

        var scenePath = Path.Combine(p.ScenesDir, name.EndsWith(".scene", StringComparison.OrdinalIgnoreCase) ? name : name + ".scene");
        if (File.Exists(scenePath))
            return Task.FromResult(McpToolResult.Text($"Scene '{name}' already exists.", isError: true));

        var s = new SceneFile
        {
            Name = Path.GetFileNameWithoutExtension(name),
            Width = width,
            Height = height,
            Map = map,
        };

        GameProject.Save(scenePath, s);
        return Task.FromResult(McpToolResult.Json(new { success = true, name = s.Name, path = scenePath }));
    }
    #endregion
}

public sealed class AddActorTool : IMcpToolHandler
{
    #region [Properties]
    public string Name => "agb_add_actor";
    public string Description => "Adds a new actor into a scene.";
    public McpToolInputSchema InputSchema => new()
    {
        Properties = new()
        {
            ["scene"] = new McpPropertySchema { Type = "string", Description = "Scene name" },
            ["name"] = new McpPropertySchema { Type = "string", Description = "Actor name" },
            ["kind"] = new McpPropertySchema { Type = "string", Description = "Actor kind: Empty, Player, Enemy, Pickup, TriggerZone, CameraZone, TextLabel" },
            ["x"] = new McpPropertySchema { Type = "number", Description = "X position in pixels" },
            ["y"] = new McpPropertySchema { Type = "number", Description = "Y position in pixels" },
            ["sprite"] = new McpPropertySchema { Type = "string", Description = "Sprite name" }
        },
        Required = ["scene", "name"]
    };
    #endregion

    #region [Public Methods]
    public Task<McpToolResult> ExecuteAsync(McpContext context, JsonElement? arguments)
    {
        var p = context.CurrentProject;
        if (p == null) return Task.FromResult(McpToolResult.Text("No active project.", isError: true));

        var scene = ToolHelpers.GetString(arguments, "scene");
        var name = ToolHelpers.GetString(arguments, "name");
        if (string.IsNullOrWhiteSpace(scene) || string.IsNullOrWhiteSpace(name))
            return Task.FromResult(McpToolResult.Text("Error: 'scene' and 'name' are required.", isError: true));

        var scenePath = Path.Combine(p.ScenesDir, scene.EndsWith(".scene", StringComparison.OrdinalIgnoreCase) ? scene : scene + ".scene");
        if (!File.Exists(scenePath))
            return Task.FromResult(McpToolResult.Text($"Scene '{scene}' not found.", isError: true));

        var s = GameProject.Load<SceneFile>(scenePath);
        if (s.Actors.Any(a => a.Name == name))
            return Task.FromResult(McpToolResult.Text($"Actor '{name}' already exists in scene '{scene}'.", isError: true));

        var kindStr = ToolHelpers.GetString(arguments, "kind") ?? "Empty";
        Enum.TryParse<ActorKind>(kindStr, true, out var kind);

        var actor = new ActorData
        {
            Name = name,
            Kind = kind,
            X = ToolHelpers.GetInt(arguments, "x") ?? 0,
            Y = ToolHelpers.GetInt(arguments, "y") ?? 0,
            Sprite = ToolHelpers.GetString(arguments, "sprite") ?? ""
        };

        s.Actors.Add(actor);
        GameProject.Save(scenePath, s);
        return Task.FromResult(McpToolResult.Json(new { success = true, actor }));
    }
    #endregion
}

public sealed class UpdateActorTool : IMcpToolHandler
{
    #region [Properties]
    public string Name => "agb_update_actor";
    public string Description => "Updates an actor's position, sprite, or visibility in a scene.";
    public McpToolInputSchema InputSchema => new()
    {
        Properties = new()
        {
            ["scene"] = new McpPropertySchema { Type = "string", Description = "Scene name" },
            ["name"] = new McpPropertySchema { Type = "string", Description = "Actor name" },
            ["x"] = new McpPropertySchema { Type = "number", Description = "New X position" },
            ["y"] = new McpPropertySchema { Type = "number", Description = "New Y position" },
            ["sprite"] = new McpPropertySchema { Type = "string", Description = "New sprite name" },
            ["hidden"] = new McpPropertySchema { Type = "boolean", Description = "Set visibility" }
        },
        Required = ["scene", "name"]
    };
    #endregion

    #region [Public Methods]
    public Task<McpToolResult> ExecuteAsync(McpContext context, JsonElement? arguments)
    {
        var p = context.CurrentProject;
        if (p == null) return Task.FromResult(McpToolResult.Text("No active project.", isError: true));

        var scene = ToolHelpers.GetString(arguments, "scene");
        var name = ToolHelpers.GetString(arguments, "name");
        if (string.IsNullOrWhiteSpace(scene) || string.IsNullOrWhiteSpace(name))
            return Task.FromResult(McpToolResult.Text("Error: 'scene' and 'name' are required.", isError: true));

        var scenePath = Path.Combine(p.ScenesDir, scene.EndsWith(".scene", StringComparison.OrdinalIgnoreCase) ? scene : scene + ".scene");
        if (!File.Exists(scenePath)) return Task.FromResult(McpToolResult.Text($"Scene '{scene}' not found.", isError: true));

        var s = GameProject.Load<SceneFile>(scenePath);
        var actor = s.Actors.FirstOrDefault(a => a.Name == name);
        if (actor == null) return Task.FromResult(McpToolResult.Text($"Actor '{name}' not found in scene '{scene}'.", isError: true));

        if (ToolHelpers.GetInt(arguments, "x") is { } x) actor.X = x;
        if (ToolHelpers.GetInt(arguments, "y") is { } y) actor.Y = y;
        if (ToolHelpers.GetString(arguments, "sprite") is { } sp) actor.Sprite = sp;
        if (ToolHelpers.GetBool(arguments, "hidden") is { } h) actor.Hidden = h;

        GameProject.Save(scenePath, s);
        return Task.FromResult(McpToolResult.Json(new { success = true, actor }));
    }
    #endregion
}

public sealed class RemoveActorTool : IMcpToolHandler
{
    #region [Properties]
    public string Name => "agb_remove_actor";
    public string Description => "Removes an actor from a scene.";
    public McpToolInputSchema InputSchema => new()
    {
        Properties = new()
        {
            ["scene"] = new McpPropertySchema { Type = "string", Description = "Scene name" },
            ["name"] = new McpPropertySchema { Type = "string", Description = "Actor name to remove" }
        },
        Required = ["scene", "name"]
    };
    #endregion

    #region [Public Methods]
    public Task<McpToolResult> ExecuteAsync(McpContext context, JsonElement? arguments)
    {
        var p = context.CurrentProject;
        if (p == null) return Task.FromResult(McpToolResult.Text("No active project.", isError: true));

        var scene = ToolHelpers.GetString(arguments, "scene");
        var name = ToolHelpers.GetString(arguments, "name");
        if (string.IsNullOrWhiteSpace(scene) || string.IsNullOrWhiteSpace(name))
            return Task.FromResult(McpToolResult.Text("Error: 'scene' and 'name' are required.", isError: true));

        var scenePath = Path.Combine(p.ScenesDir, scene.EndsWith(".scene", StringComparison.OrdinalIgnoreCase) ? scene : scene + ".scene");
        if (!File.Exists(scenePath)) return Task.FromResult(McpToolResult.Text($"Scene '{scene}' not found.", isError: true));

        var s = GameProject.Load<SceneFile>(scenePath);
        var removed = s.Actors.RemoveAll(a => a.Name == name);
        if (removed == 0) return Task.FromResult(McpToolResult.Text($"Actor '{name}' not found in scene.", isError: true));

        GameProject.Save(scenePath, s);
        return Task.FromResult(McpToolResult.Json(new { success = true, removedCount = removed }));
    }
    #endregion
}
