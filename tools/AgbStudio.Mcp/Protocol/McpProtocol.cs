using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgbStudio.Mcp.Protocol;

public sealed class McpRequest
{
    #region [Properties]
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("id")]
    public JsonElement? Id { get; set; }

    [JsonPropertyName("method")]
    public string Method { get; set; } = "";

    [JsonPropertyName("params")]
    public JsonElement? Params { get; set; }
    #endregion
}

public sealed class McpResponse
{
    #region [Properties]
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("id")]
    public JsonElement? Id { get; set; }

    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Result { get; set; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public McpError? Error { get; set; }
    #endregion

    #region [Public Methods]
    public static McpResponse Success(JsonElement? id, object result) => new() { Id = id, Result = result };
    public static McpResponse Failure(JsonElement? id, int code, string message, object? data = null) =>
        new() { Id = id, Error = new McpError { Code = code, Message = message, Data = data } };
    #endregion
}

public sealed class McpError
{
    #region [Properties]
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; set; }
    #endregion
}

public sealed class McpTool
{
    #region [Properties]
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("inputSchema")]
    public McpToolInputSchema InputSchema { get; set; } = new();
    #endregion
}

public sealed class McpToolInputSchema
{
    #region [Properties]
    [JsonPropertyName("type")]
    public string Type { get; set; } = "object";

    [JsonPropertyName("properties")]
    public Dictionary<string, McpPropertySchema> Properties { get; set; } = new(StringComparer.Ordinal);

    [JsonPropertyName("required")]
    public List<string> Required { get; set; } = [];
    #endregion
}

public sealed class McpPropertySchema
{
    #region [Properties]
    [JsonPropertyName("type")]
    public string Type { get; set; } = "string";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("enum")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Enum { get; set; }

    [JsonPropertyName("default")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Default { get; set; }
    #endregion
}

public sealed class McpToolResult
{
    #region [Properties]
    [JsonPropertyName("content")]
    public List<McpContent> Content { get; set; } = [];

    [JsonPropertyName("isError")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsError { get; set; }
    #endregion

    #region [Public Methods]
    public static McpToolResult Text(string text, bool isError = false) =>
        new() { Content = [new McpContent { Type = "text", Text = text }], IsError = isError };

    public static McpToolResult Json(object obj, bool isError = false) =>
        new() { Content = [new McpContent { Type = "text", Text = JsonSerializer.Serialize(obj, McpJson.Options) }], IsError = isError };

    public static McpToolResult Image(string base64Png, string? caption = null)
    {
        var list = new List<McpContent>();
        if (!string.IsNullOrEmpty(caption)) list.Add(new McpContent { Type = "text", Text = caption });
        list.Add(new McpContent { Type = "image", Data = base64Png, MimeType = "image/png" });
        return new McpToolResult { Content = list };
    }
    #endregion
}

public sealed class McpContent
{
    #region [Properties]
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";

    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; set; }

    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Data { get; set; }

    [JsonPropertyName("mimeType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MimeType { get; set; }
    #endregion
}

public sealed class McpResource
{
    #region [Properties]
    [JsonPropertyName("uri")]
    public string Uri { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("mimeType")]
    public string MimeType { get; set; } = "application/json";
    #endregion
}

public sealed class McpResourceContent
{
    #region [Properties]
    [JsonPropertyName("uri")]
    public string Uri { get; set; } = "";

    [JsonPropertyName("mimeType")]
    public string MimeType { get; set; } = "application/json";

    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; set; }

    [JsonPropertyName("blob")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Blob { get; set; }
    #endregion
}

public sealed class McpPrompt
{
    #region [Properties]
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("arguments")]
    public List<McpPromptArgument> Arguments { get; set; } = [];
    #endregion
}

public sealed class McpPromptArgument
{
    #region [Properties]
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("required")]
    public bool Required { get; set; }
    #endregion
}

public static class McpJson
{
    #region [Variables/Fields]
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
    #endregion
}
