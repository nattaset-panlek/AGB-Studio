using System.Text.Json;
using AgbStudio.Core.Project;
using AgbStudio.Mcp.Protocol;
using AgbStudio.Scripting;

namespace AgbStudio.Mcp.Tools;

public sealed class ListScriptsTool : IMcpToolHandler
{
    #region [Properties]
    public string Name => "agb_list_scripts";
    public string Description => "Lists all C# behaviour script files (.cs) in the active project.";
    public McpToolInputSchema InputSchema => new();
    #endregion

    #region [Public Methods]
    public Task<McpToolResult> ExecuteAsync(McpContext context, JsonElement? arguments)
    {
        var p = context.CurrentProject;
        if (p == null) return Task.FromResult(McpToolResult.Text("No active project.", isError: true));

        var list = p.ScriptFiles().Select(f => new
        {
            name = Path.GetFileName(f),
            path = f,
            relativePath = Path.GetRelativePath(p.ScriptsDir, f),
            sizeBytes = new FileInfo(f).Length
        }).ToList();

        return Task.FromResult(McpToolResult.Json(list));
    }
    #endregion
}

public sealed class ReadScriptTool : IMcpToolHandler
{
    #region [Properties]
    public string Name => "agb_read_script";
    public string Description => "Reads the full source code of a C# script.";
    public McpToolInputSchema InputSchema => new()
    {
        Properties = new()
        {
            ["name"] = new McpPropertySchema { Type = "string", Description = "Script file name (e.g. 'PlayerController.cs')" }
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

        var scriptPath = Path.Combine(p.ScriptsDir, name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ? name : name + ".cs");
        if (!File.Exists(scriptPath)) return Task.FromResult(McpToolResult.Text($"Script '{name}' not found at {scriptPath}", isError: true));

        var code = File.ReadAllText(scriptPath);
        return Task.FromResult(McpToolResult.Text(code));
    }
    #endregion
}

public sealed class WriteScriptTool : IMcpToolHandler
{
    #region [Properties]
    public string Name => "agb_write_script";
    public string Description => "Creates or updates a C# script file in the project's scripts directory.";
    public McpToolInputSchema InputSchema => new()
    {
        Properties = new()
        {
            ["name"] = new McpPropertySchema { Type = "string", Description = "Script file name" },
            ["content"] = new McpPropertySchema { Type = "string", Description = "C# script source code" }
        },
        Required = ["name", "content"]
    };
    #endregion

    #region [Public Methods]
    public Task<McpToolResult> ExecuteAsync(McpContext context, JsonElement? arguments)
    {
        var p = context.CurrentProject;
        if (p == null) return Task.FromResult(McpToolResult.Text("No active project.", isError: true));

        var name = ToolHelpers.GetString(arguments, "name");
        var content = ToolHelpers.GetString(arguments, "content");
        if (string.IsNullOrWhiteSpace(name) || content == null)
            return Task.FromResult(McpToolResult.Text("Error: 'name' and 'content' are required.", isError: true));

        var scriptPath = Path.Combine(p.ScriptsDir, name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ? name : name + ".cs");
        Directory.CreateDirectory(Path.GetDirectoryName(scriptPath)!);
        File.WriteAllText(scriptPath, content);

        return Task.FromResult(McpToolResult.Json(new { success = true, path = scriptPath, length = content.Length }));
    }
    #endregion
}

public sealed class AnalyzeScriptsTool : IMcpToolHandler
{
    #region [Properties]
    public string Name => "agb_analyze_scripts";
    public string Description => "Runs Roslyn C# analyzers on project scripts and reports GBA-specific constraint violations and compiler errors.";
    public McpToolInputSchema InputSchema => new();
    #endregion

    #region [Public Methods]
    public Task<McpToolResult> ExecuteAsync(McpContext context, JsonElement? arguments)
    {
        var p = context.CurrentProject;
        if (p == null) return Task.FromResult(McpToolResult.Text("No active project.", isError: true));

        var sources = p.ScriptFiles().Select(f => new ScriptSource(f, File.ReadAllText(f))).ToList();
        var compiler = new ScriptCompiler();
        var tempDir = Path.Combine(Path.GetTempPath(), "agb_mcp_analyze_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            var ctx = ToolHelpers.CreateCompileContext(p);
            var result = compiler.Compile(sources, ctx, tempDir);

            var report = new
            {
                success = result.Success,
                scriptCount = sources.Count,
                behaviours = result.Behaviours.Select(b => new { name = b.Name, file = b.File, fields = b.Fields.Select(f => new { name = f.Name, type = f.Type }) }).ToList(),
                diagnostics = result.Diagnostics.Select(d => new
                {
                    id = d.Id,
                    severity = d.Severity.ToString(),
                    message = d.Message,
                    file = Path.GetFileName(d.File),
                    line = d.Line,
                    column = d.Column
                }).ToList()
            };

            return Task.FromResult(McpToolResult.Json(report));
        }
        finally
        {
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
        }
    }
    #endregion
}

public sealed class PreviewTranspilationTool : IMcpToolHandler
{
    #region [Properties]
    public string Name => "agb_preview_transpilation";
    public string Description => "Transpiles project C# behaviours into native GBA C++ code and returns the generated C++ source.";
    public McpToolInputSchema InputSchema => new()
    {
        Properties = new()
        {
            ["script"] = new McpPropertySchema { Type = "string", Description = "Optional specific script name to inspect (e.g. 'PlayerController.cs')" }
        }
    };
    #endregion

    #region [Public Methods]
    public Task<McpToolResult> ExecuteAsync(McpContext context, JsonElement? arguments)
    {
        var p = context.CurrentProject;
        if (p == null) return Task.FromResult(McpToolResult.Text("No active project.", isError: true));

        var filter = ToolHelpers.GetString(arguments, "script");
        var sources = p.ScriptFiles().Select(f => new ScriptSource(f, File.ReadAllText(f))).ToList();
        var compiler = new ScriptCompiler();
        var tempDir = Path.Combine(Path.GetTempPath(), "agb_mcp_transpile_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            var ctx = ToolHelpers.CreateCompileContext(p);
            var result = compiler.Compile(sources, ctx, tempDir);

            if (!result.Success)
            {
                var errors = result.Diagnostics.Where(d => d.IsError).Select(d => $"{d.File}({d.Line}): {d.Message}");
                return Task.FromResult(McpToolResult.Text($"Script compilation failed:\n{string.Join('\n', errors)}", isError: true));
            }

            var generatedFiles = Directory.GetFiles(tempDir, "*.cpp", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(tempDir, "*.hpp", SearchOption.AllDirectories)).ToList();

            var outputs = new Dictionary<string, string>();
            foreach (var gf in generatedFiles)
            {
                var relName = Path.GetFileName(gf);
                if (filter != null && !relName.Contains(Path.GetFileNameWithoutExtension(filter), StringComparison.OrdinalIgnoreCase))
                    continue;
                outputs[relName] = File.ReadAllText(gf);
            }

            return Task.FromResult(McpToolResult.Json(new { totalFiles = generatedFiles.Count, files = outputs }));
        }
        finally
        {
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
        }
    }
    #endregion
}
