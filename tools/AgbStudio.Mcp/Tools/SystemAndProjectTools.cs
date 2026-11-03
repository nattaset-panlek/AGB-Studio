using System.Text.Json;
using AgbStudio.Build;
using AgbStudio.Core.Project;
using AgbStudio.Mcp.Protocol;

namespace AgbStudio.Mcp.Tools;

public sealed class ServerInfoTool : IMcpToolHandler
{
    #region [Properties]
    public string Name => "agb_server_info";
    public string Description => "Returns server version, host environment, and active project information.";
    public McpToolInputSchema InputSchema => new();
    #endregion

    #region [Public Methods]
    public Task<McpToolResult> ExecuteAsync(McpContext context, JsonElement? arguments)
    {
        var info = new
        {
            server = "agb-studio-mcp",
            version = "1.0.0",
            protocolVersion = "2024-11-05",
            os = Environment.OSVersion.ToString(),
            dotnet = Environment.Version.ToString(),
            activeProject = context.CurrentProject?.Settings.Name,
            projectPath = context.CurrentProject?.ProjectPath,
            isSimulating = context.IsSimulating,
        };
        return Task.FromResult(McpToolResult.Json(info));
    }
    #endregion
}

public sealed class ToolchainStatusTool : IMcpToolHandler
{
    #region [Properties]
    public string Name => "agb_toolchain_status";
    public string Description => "Checks the availability and paths of devkitARM GCC, Ninja build engine, and mGBA core.";
    public McpToolInputSchema InputSchema => new();
    #endregion

    #region [Public Methods]
    public Task<McpToolResult> ExecuteAsync(McpContext context, JsonElement? arguments)
    {
        var tc = context.Toolchain;
        var status = new
        {
            isComplete = tc.IsComplete,
            devkitArm = tc.DevkitArm,
            gxx = tc.Gxx,
            ninja = tc.Ninja,
            gxxAvailable = File.Exists(tc.Gxx),
            ninjaAvailable = File.Exists(tc.Ninja),
            emulatorCore = AgbStudio.Emu.LibretroCore.DefaultCorePath,
            emulatorCoreAvailable = File.Exists(AgbStudio.Emu.LibretroCore.DefaultCorePath),
        };
        return Task.FromResult(McpToolResult.Json(status));
    }
    #endregion
}

public sealed class OpenProjectTool : IMcpToolHandler
{
    #region [Properties]
    public string Name => "agb_open_project";
    public string Description => "Opens an AGB Studio project from a directory path or an .agbproj file.";
    public McpToolInputSchema InputSchema => new()
    {
        Properties = new()
        {
            ["path"] = new McpPropertySchema { Type = "string", Description = "Path to the project directory or .agbproj file" }
        },
        Required = ["path"]
    };
    #endregion

    #region [Public Methods]
    public Task<McpToolResult> ExecuteAsync(McpContext context, JsonElement? arguments)
    {
        var path = ToolHelpers.GetString(arguments, "path");
        if (string.IsNullOrWhiteSpace(path))
            return Task.FromResult(McpToolResult.Text("Error: 'path' argument is required.", isError: true));

        try
        {
            var p = context.OpenProject(path);
            var result = new
            {
                name = p.Settings.Name,
                path = p.ProjectPath,
                root = p.Root,
                sceneCount = p.SceneFiles().Count(),
                tableCount = p.TableFiles().Count(),
                scriptCount = p.ScriptFiles().Count()
            };
            return Task.FromResult(McpToolResult.Json(result));
        }
        catch (Exception ex)
        {
            return Task.FromResult(McpToolResult.Text($"Failed to open project: {ex.Message}", isError: true));
        }
    }
    #endregion
}

public sealed class GetProjectSummaryTool : IMcpToolHandler
{
    #region [Properties]
    public string Name => "agb_get_project_summary";
    public string Description => "Returns comprehensive summary of the active project (scenes, scripts, tilemaps, data tables, languages).";
    public McpToolInputSchema InputSchema => new();
    #endregion

    #region [Public Methods]
    public Task<McpToolResult> ExecuteAsync(McpContext context, JsonElement? arguments)
    {
        var p = context.CurrentProject;
        if (p == null)
            return Task.FromResult(McpToolResult.Text("No active project. Call 'agb_open_project' first.", isError: true));

        var summary = new
        {
            name = p.Settings.Name,
            projectPath = p.ProjectPath,
            root = p.Root,
            format = p.Settings.Format,
            scenes = p.SceneFiles().Select(f => Path.GetFileNameWithoutExtension(f)).ToList(),
            tilemaps = p.MapFiles().Select(f => Path.GetFileNameWithoutExtension(f)).ToList(),
            scripts = p.ScriptFiles().Select(f => Path.GetFileName(f)).ToList(),
            tables = p.TableFiles().Select(f => Path.GetFileNameWithoutExtension(f)).ToList(),
            dialogues = p.DialogueFiles().Select(f => Path.GetFileNameWithoutExtension(f)).ToList(),
            sprites = p.SpriteFiles().Select(f => Path.GetFileNameWithoutExtension(f)).ToList(),
            languages = p.Settings.Localization.Languages.Select(l => new { code = l.Code, name = l.Name, font = l.Font }).ToList(),
            defaultScene = p.Settings.LastScene
        };

        return Task.FromResult(McpToolResult.Json(summary));
    }
    #endregion
}

public sealed class CreateProjectTool : IMcpToolHandler
{
    #region [Properties]
    public string Name => "agb_create_project";
    public string Description => "Creates and initializes a new AGB Studio project in the target folder.";
    public McpToolInputSchema InputSchema => new()
    {
        Properties = new()
        {
            ["folder"] = new McpPropertySchema { Type = "string", Description = "Folder directory where the project will be created" },
            ["name"] = new McpPropertySchema { Type = "string", Description = "Project name" }
        },
        Required = ["folder", "name"]
    };
    #endregion

    #region [Public Methods]
    public Task<McpToolResult> ExecuteAsync(McpContext context, JsonElement? arguments)
    {
        var folder = ToolHelpers.GetString(arguments, "folder");
        var name = ToolHelpers.GetString(arguments, "name");
        if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(name))
            return Task.FromResult(McpToolResult.Text("Error: 'folder' and 'name' arguments are required.", isError: true));

        try
        {
            var p = context.CreateProject(folder, name);
            return Task.FromResult(McpToolResult.Json(new { success = true, name = p.Settings.Name, path = p.ProjectPath }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(McpToolResult.Text($"Failed to create project: {ex.Message}", isError: true));
        }
    }
    #endregion
}

public sealed class GetSettingsTool : IMcpToolHandler
{
    #region [Properties]
    public string Name => "agb_get_settings";
    public string Description => "Returns the project settings from the active project.";
    public McpToolInputSchema InputSchema => new();
    #endregion

    #region [Public Methods]
    public Task<McpToolResult> ExecuteAsync(McpContext context, JsonElement? arguments)
    {
        var p = context.CurrentProject;
        if (p == null) return Task.FromResult(McpToolResult.Text("No active project.", isError: true));
        return Task.FromResult(McpToolResult.Json(p.Settings));
    }
    #endregion
}

public sealed class UpdateSettingsTool : IMcpToolHandler
{
    #region [Properties]
    public string Name => "agb_update_settings";
    public string Description => "Updates specified project settings and saves them to disk.";
    public McpToolInputSchema InputSchema => new()
    {
        Properties = new()
        {
            ["name"] = new McpPropertySchema { Type = "string", Description = "Project display name" },
            ["lastScene"] = new McpPropertySchema { Type = "string", Description = "Default or initial scene name" }
        }
    };
    #endregion

    #region [Public Methods]
    public Task<McpToolResult> ExecuteAsync(McpContext context, JsonElement? arguments)
    {
        var p = context.CurrentProject;
        if (p == null) return Task.FromResult(McpToolResult.Text("No active project.", isError: true));

        var name = ToolHelpers.GetString(arguments, "name");
        var lastScene = ToolHelpers.GetString(arguments, "lastScene");

        if (name != null) p.Settings.Name = name;
        if (lastScene != null) p.Settings.LastScene = lastScene;

        p.SaveSettings();
        return Task.FromResult(McpToolResult.Json(new { success = true, settings = p.Settings }));
    }
    #endregion
}
