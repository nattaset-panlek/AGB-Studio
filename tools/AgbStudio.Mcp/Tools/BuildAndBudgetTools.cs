using System.Globalization;
using System.Text.Json;
using AgbStudio.Build;
using AgbStudio.Build.Graphics;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;
using AgbStudio.Mcp.Protocol;

namespace AgbStudio.Mcp.Tools;

public sealed class BuildRomTool : IMcpToolHandler
{
    #region [Properties]
    public string Name => "agb_build_rom";
    public string Description => "Builds and compiles the project into a Game Boy Advance ROM (.gba).";
    public McpToolInputSchema InputSchema => new()
    {
        Properties = new()
        {
            ["configuration"] = new McpPropertySchema { Type = "string", Description = "Build configuration: 'Debug' or 'Release' (default: 'Debug')", Enum = ["Debug", "Release"] },
            ["clean"] = new McpPropertySchema { Type = "boolean", Description = "Whether to clean build outputs before building (default: false)" }
        }
    };
    #endregion

    #region [Public Methods]
    public async Task<McpToolResult> ExecuteAsync(McpContext context, JsonElement? arguments)
    {
        var p = context.CurrentProject;
        if (p == null) return McpToolResult.Text("No active project.", isError: true);

        var configStr = ToolHelpers.GetString(arguments, "configuration") ?? "Debug";
        var isRelease = string.Equals(configStr, "Release", StringComparison.OrdinalIgnoreCase);
        var clean = ToolHelpers.GetBool(arguments, "clean") ?? false;

        var tc = context.Toolchain;
        var builder = new GameBuilder(p, tc) { Scripts = new AgbStudio.Scripting.ScriptBackend() };

        if (clean && Directory.Exists(p.BuildDir))
        {
            try { Directory.Delete(p.BuildDir, true); } catch { }
        }

        var opt = new BuildOptions
        {
            Configuration = isRelease ? BuildConfiguration.Release : BuildConfiguration.Debug
        };

        var buildResult = await Task.Run(() => builder.Build(opt));

        var romExists = File.Exists(buildResult.RomPath);
        var romKb = romExists ? new FileInfo(buildResult.RomPath).Length / 1024 : 0;

        var result = new
        {
            success = buildResult.Success,
            configuration = opt.Configuration.ToString(),
            romPath = buildResult.RomPath,
            romSizeKb = romKb,
            durationSeconds = buildResult.Elapsed.TotalSeconds,
            diagnostics = buildResult.Diagnostics.Select(d => new
            {
                severity = d.Severity.ToString(),
                message = d.Message,
                file = Path.GetFileName(d.File),
                line = d.Line
            }).ToList()
        };

        return McpToolResult.Json(result, isError: !buildResult.Success);
    }
    #endregion
}

public sealed class GetBudgetTool : IMcpToolHandler
{
    #region [Properties]
    public string Name => "agb_get_budget";
    public string Description => "Calculates hardware budget utilization (VRAM BG, VRAM OBJ, OAM, ROM, EWRAM, IWRAM, Palettes).";
    public McpToolInputSchema InputSchema => new()
    {
        Properties = new()
        {
            ["scene"] = new McpPropertySchema { Type = "string", Description = "Optional scene name to evaluate (default: active scene)" }
        }
    };
    #endregion

    #region [Public Methods]
    public Task<McpToolResult> ExecuteAsync(McpContext context, JsonElement? arguments)
    {
        var p = context.CurrentProject;
        if (p == null) return Task.FromResult(McpToolResult.Text("No active project.", isError: true));

        var scene = ToolHelpers.GetString(arguments, "scene") ?? p.Settings.LastScene ?? p.SceneFiles().Select(Path.GetFileNameWithoutExtension).FirstOrDefault() ?? "";

        var reportPath = Path.Combine(p.BuildDir, "report.json");
        BuildReport? report = null;
        if (File.Exists(reportPath))
        {
            try { report = GameProject.Load<BuildReport>(reportPath); } catch { }
        }

        var gb = new GameBuilder(p, context.Toolchain);
        var romPath = gb.RomPath;
        var romKb = File.Exists(romPath) ? new FileInfo(romPath).Length / 1024 : 0;

        long bgKb = 0, objKb = 0, bgPalettes = 0, objPalettes = 0;
        if (report != null && report.Scenes.TryGetValue(scene, out var sReport))
        {
            bgKb = (sReport.VramBgBytes + 1023) / 1024;
            objKb = (sReport.VramObjBytes + 1023) / 1024;
            bgPalettes = sReport.BgPalettes;
            objPalettes = sReport.ObjPalettes;
        }

        var lines = new List<object>
        {
            new { category = "VRAM BG", used = bgKb, total = 64, unit = "KB", percent = Math.Min(100, bgKb * 100 / 64) },
            new { category = "VRAM OBJ", used = objKb, total = 32, unit = "KB", percent = Math.Min(100, objKb * 100 / 32) },
            new { category = "BG palettes", used = bgPalettes, total = 16, unit = "banks", percent = Math.Min(100, bgPalettes * 100 / 16) },
            new { category = "OBJ palettes", used = objPalettes, total = 16, unit = "banks", percent = Math.Min(100, objPalettes * 100 / 16) },
            new { category = "ROM", used = romKb, total = 32768, unit = "KB", percent = Math.Min(100, romKb * 100 / 32768) }
        };

        var suggestions = ComputePaletteSuggestions(p, scene);

        var budget = new
        {
            scene,
            hasReport = report != null,
            lines,
            suggestions
        };

        return Task.FromResult(McpToolResult.Json(budget));
    }
    #endregion

    #region [Private Methods]
    static IReadOnlyList<string> ComputePaletteSuggestions(GameProject p, string scene)
    {
        var scenePath = Path.Combine(p.ScenesDir, scene.EndsWith(".scene", StringComparison.OrdinalIgnoreCase) ? scene : scene + ".scene");
        if (!File.Exists(scenePath)) return [];

        SceneFile s;
        try { s = GameProject.Load<SceneFile>(scenePath); }
        catch { return []; }

        var files = p.SpriteFiles().ToDictionary(GameProject.AssetName, f => f, StringComparer.Ordinal);
        var order = new List<(string Name, string File, HashSet<ushort> Colours)>();
        foreach (var a in s.Actors)
        {
            if (a.Sprite.Length == 0 || order.Any(o => o.Name == a.Sprite) || !files.TryGetValue(a.Sprite, out var file)) continue;
            var meta = p.LoadMeta<SpriteMeta>(file);
            if (meta.Palette.Length > 0) continue;
            try { order.Add((a.Sprite, file, ColorImage.Load(file).Histogram().Keys.ToHashSet())); }
            catch { }
        }

        var list = new List<string>();
        var used = new HashSet<string>();
        for (var i = 0; i < order.Count; i++)
        {
            if (used.Contains(order[i].Name)) continue;
            for (var j = i + 1; j < order.Count; j++)
            {
                if (used.Contains(order[j].Name)) continue;
                var union = new HashSet<ushort>(order[i].Colours);
                union.UnionWith(order[j].Colours);
                if (union.Count > 15) continue;
                list.Add(string.Create(CultureInfo.InvariantCulture,
                    $"{Path.GetFileName(order[i].File)} and {Path.GetFileName(order[j].File)} could share OBJ palette {i} — saves one palette slot."));
                used.Add(order[i].Name);
                used.Add(order[j].Name);
                break;
            }
        }
        return list;
    }
    #endregion
}

public sealed class GetPaletteSuggestionsTool : IMcpToolHandler
{
    #region [Properties]
    public string Name => "agb_get_palette_suggestions";
    public string Description => "Analyzes sprites in a scene and returns recommendations for sprite pairs that can share a 16-color palette bank.";
    public McpToolInputSchema InputSchema => new()
    {
        Properties = new()
        {
            ["scene"] = new McpPropertySchema { Type = "string", Description = "Scene name to analyze" }
        },
        Required = ["scene"]
    };
    #endregion

    #region [Public Methods]
    public Task<McpToolResult> ExecuteAsync(McpContext context, JsonElement? arguments)
    {
        var p = context.CurrentProject;
        if (p == null) return Task.FromResult(McpToolResult.Text("No active project.", isError: true));

        var scene = ToolHelpers.GetString(arguments, "scene");
        if (string.IsNullOrWhiteSpace(scene)) return Task.FromResult(McpToolResult.Text("Error: 'scene' is required.", isError: true));

        var budgetTool = new GetBudgetTool();
        return budgetTool.ExecuteAsync(context, arguments);
    }
    #endregion
}
