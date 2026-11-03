using AgbStudio.Build;
using System.Text.Json;
using AgbStudio.Build.Fonts;
using AgbStudio.Core.Project;
using AgbStudio.Mcp.Protocol;

namespace AgbStudio.Mcp.Tools;

public sealed class GetLocalizationTool : IMcpToolHandler
{
    #region [Properties]
    public string Name => "agb_get_localization";
    public string Description => "Returns all localization string tables, configured languages, and string keys.";
    public McpToolInputSchema InputSchema => new();
    #endregion

    #region [Public Methods]
    public Task<McpToolResult> ExecuteAsync(McpContext context, JsonElement? arguments)
    {
        var p = context.CurrentProject;
        if (p == null) return Task.FromResult(McpToolResult.Text("No active project.", isError: true));

        var languages = p.Settings.Localization.Languages.Select(l => l.Code).ToList();
        var tables = new Dictionary<string, SortedDictionary<string, string>>(StringComparer.Ordinal);
        var allKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in p.StringTables())
        {
            try
            {
                var st = GameProject.Load<StringTable>(file);
                tables[st.Language] = st.Strings;
                foreach (var k in st.Strings.Keys) allKeys.Add(k);
            }
            catch { }
        }

        var metaPath = Path.Combine(p.TextDir, "strings.meta.json");
        StringMeta? meta = null;
        if (File.Exists(metaPath))
        {
            try { meta = GameProject.Load<StringMeta>(metaPath); } catch { }
        }

        var result = new
        {
            sourceLanguage = p.Settings.Localization.Source,
            languages = p.Settings.Localization.Languages,
            totalKeys = allKeys.Count,
            keys = allKeys.OrderBy(k => k).ToList(),
            tables = tables,
            meta = meta?.Keys
        };

        return Task.FromResult(McpToolResult.Json(result));
    }
    #endregion
}

public sealed class SetLocalizationStringTool : IMcpToolHandler
{
    #region [Properties]
    public string Name => "agb_set_localization_string";
    public string Description => "Sets or creates a localized text entry for a given language and key.";
    public McpToolInputSchema InputSchema => new()
    {
        Properties = new()
        {
            ["language"] = new McpPropertySchema { Type = "string", Description = "Language code (e.g. 'en', 'th')" },
            ["key"] = new McpPropertySchema { Type = "string", Description = "String key (e.g. 'ui.start')" },
            ["text"] = new McpPropertySchema { Type = "string", Description = "Translated text" }
        },
        Required = ["language", "key", "text"]
    };
    #endregion

    #region [Public Methods]
    public Task<McpToolResult> ExecuteAsync(McpContext context, JsonElement? arguments)
    {
        var p = context.CurrentProject;
        if (p == null) return Task.FromResult(McpToolResult.Text("No active project.", isError: true));

        var lang = ToolHelpers.GetString(arguments, "language");
        var key = ToolHelpers.GetString(arguments, "key");
        var text = ToolHelpers.GetString(arguments, "text");
        if (string.IsNullOrWhiteSpace(lang) || string.IsNullOrWhiteSpace(key) || text == null)
            return Task.FromResult(McpToolResult.Text("Error: 'language', 'key', and 'text' are required.", isError: true));

        var tablePath = Path.Combine(p.TextDir, $"strings.{lang.ToLowerInvariant()}.json");
        StringTable table;
        if (File.Exists(tablePath)) table = GameProject.Load<StringTable>(tablePath);
        else table = new StringTable { Language = lang.ToLowerInvariant() };

        table.Strings[key] = text;
        GameProject.Save(tablePath, table);

        return Task.FromResult(McpToolResult.Json(new { success = true, language = lang, key, text }));
    }
    #endregion
}

public sealed class CheckTextFitTool : IMcpToolHandler
{
    #region [Properties]
    public string Name => "agb_check_text_fit";
    public string Description => "Validates whether text fits on the GBA screen (Thai word-wrapping, line widths, line count, box overflow).";
    public McpToolInputSchema InputSchema => new()
    {
        Properties = new()
        {
            ["text"] = new McpPropertySchema { Type = "string", Description = "Text string to measure" },
            ["boxWidth"] = new McpPropertySchema { Type = "number", Description = "Box width in pixels (default: 224 for GBA dialogue)" },
            ["maxLines"] = new McpPropertySchema { Type = "number", Description = "Maximum allowed lines (default: 3)" },
            ["fontName"] = new McpPropertySchema { Type = "string", Description = "Optional font name from fonts/ directory" }
        },
        Required = ["text"]
    };
    #endregion

    #region [Public Methods]
    public Task<McpToolResult> ExecuteAsync(McpContext context, JsonElement? arguments)
    {
        var text = ToolHelpers.GetString(arguments, "text");
        if (string.IsNullOrEmpty(text))
            return Task.FromResult(McpToolResult.Text("Error: 'text' is required.", isError: true));

        var boxWidth = ToolHelpers.GetInt(arguments, "boxWidth") ?? 224;
        var maxLines = ToolHelpers.GetInt(arguments, "maxLines") ?? 3;
        var fontName = ToolHelpers.GetString(arguments, "fontName");

        GameFont? font = null;
        var p = context.CurrentProject;
        if (p != null)
        {
            var fontPath = fontName != null
                ? Path.Combine(p.FontsDir, fontName.EndsWith(".font", StringComparison.OrdinalIgnoreCase) ? fontName : fontName + ".font")
                : p.FontFiles().FirstOrDefault();

            if (fontPath != null && File.Exists(fontPath))
            {
                try
                {
                    var ff = GameProject.Load<FontFile>(fontPath);
                    font = FontBuilder.Build(ff, p.FontsDir);
                }
                catch { }
            }
        }

        if (font == null)
        {
            var tpDir = Path.Combine(NativeBuild.RepoRoot, "third_party", "fonts");
            if (File.Exists(Path.Combine(tpDir, "NotoSansThai-Regular.ttf")))
            {
                var ff = new FontFile
                {
                    Name = "thai_vwf",
                    Source = "NotoSansThai-Regular.ttf",
                    Size = 12,
                    CharacterSets = { "Basic Latin", "Thai", "Symbols" },
                    LineHeight = 14,
                    Baseline = 10,
                    Spacing = 1,
                    Bpp = 2,
                    DropShadow = true
                };
                font = FontBuilder.Build(ff, tpDir, ThaiText.CodePoints(text));
            }
            else if (File.Exists(Path.Combine(tpDir, "agb_pixel.bdf")))
            {
                var ff = AgbPixelFont.Settings("agb_pixel");
                font = FontBuilder.Build(ff, tpDir, ThaiText.CodePoints(text));
            }
        }

        if (font == null)
            return Task.FromResult(McpToolResult.Text("Error: No font available to compute text layout.", isError: true));
        var layout = TextLayout.Layout(text, font, boxWidth, true);

        var fits = layout.Lines <= maxLines && layout.MaxWidth <= boxWidth;
        var result = new
        {
            fits,
            lines = layout.Lines,
            maxLines,
            maxWidth = layout.MaxWidth,
            boxWidth,
            lineWidths = layout.LineWidths,
            missingGlyphs = layout.Missing.ToList(),
            warning = !fits ? (layout.Lines > maxLines ? $"Exceeds {maxLines} lines ({layout.Lines} lines)" : "Exceeds box width") : null
        };

        return Task.FromResult(McpToolResult.Json(result));
    }
    #endregion
}

public sealed class GetDialogueTool : IMcpToolHandler
{
    #region [Properties]
    public string Name => "agb_get_dialogue";
    public string Description => "Returns the dialogue graph structure from a .dlg file.";
    public McpToolInputSchema InputSchema => new()
    {
        Properties = new()
        {
            ["name"] = new McpPropertySchema { Type = "string", Description = "Dialogue file name (e.g. 'intro')" }
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

        var dlgPath = Path.Combine(p.DialogueDir, name.EndsWith(".dlg", StringComparison.OrdinalIgnoreCase) ? name : name + ".dlg");
        if (!File.Exists(dlgPath)) return Task.FromResult(McpToolResult.Text($"Dialogue '{name}' not found at {dlgPath}", isError: true));

        var content = File.ReadAllText(dlgPath);
        return Task.FromResult(McpToolResult.Text(content));
    }
    #endregion
}

public sealed class UpdateDialogueTool : IMcpToolHandler
{
    #region [Properties]
    public string Name => "agb_update_dialogue";
    public string Description => "Writes or updates a .dlg dialogue file in the dialogue directory.";
    public McpToolInputSchema InputSchema => new()
    {
        Properties = new()
        {
            ["name"] = new McpPropertySchema { Type = "string", Description = "Dialogue file name" },
            ["content"] = new McpPropertySchema { Type = "string", Description = "Dialogue file content" }
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

        var dlgPath = Path.Combine(p.DialogueDir, name.EndsWith(".dlg", StringComparison.OrdinalIgnoreCase) ? name : name + ".dlg");
        Directory.CreateDirectory(Path.GetDirectoryName(dlgPath)!);
        File.WriteAllText(dlgPath, content);

        return Task.FromResult(McpToolResult.Json(new { success = true, path = dlgPath }));
    }
    #endregion
}
