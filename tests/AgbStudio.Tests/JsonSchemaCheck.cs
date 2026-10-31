using System.Text.Json;
using System.Text.Json.Nodes;

namespace AgbStudio.Tests;

/// <summary>
/// A small JSON Schema validator for the keywords docs/schemas uses (type, enum, anyOf, $ref, properties,
/// additionalProperties, required, items). A field the schema does not list is an error. Shared by SchemaTests and the
/// editor tests that validate files the editor writes (P1.04).
/// </summary>
public static class JsonSchemaCheck
{
    /// <summary>The errors of a JSON text against a schema file (empty when it validates).</summary>
    public static List<string> Validate(string schemaPath, string json)
    {
        var schema = JsonNode.Parse(File.ReadAllText(schemaPath))!;
        var errors = new List<string>();
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        Check(schema, schema, doc.RootElement, "", errors);
        return errors;
    }

    static void Check(JsonNode schema, JsonNode root, JsonElement v, string at, List<string> errors)
    {
        if (schema is JsonValue b && b.TryGetValue<bool>(out var allowed))
        {
            if (!allowed) errors.Add($"{at}: not allowed");
            return;
        }
        var o = schema.AsObject();
        if (o["$ref"] is { } r)
        {
            var target = root;
            foreach (var part in r.GetValue<string>().TrimStart('#').Split('/', StringSplitOptions.RemoveEmptyEntries))
                target = target![part.Replace("~1", "/").Replace("~0", "~")]!;
            Check(target!, root, v, at, errors);
            return;
        }
        if (o["type"] is { } t)
        {
            var types = t is JsonArray a ? a.Select(x => x!.GetValue<string>()).ToList() : [t.GetValue<string>()];
            if (!types.Any(type => Is(v, type)))
            {
                errors.Add($"{at}: {v.ValueKind} is not {string.Join(" or ", types)}");
                return;
            }
        }
        if (o["enum"] is JsonArray e && !e.Any(x => JsonNode.DeepEquals(x, JsonNode.Parse(v.GetRawText()))))
            errors.Add($"{at}: {v.GetRawText()} is not one of {e.ToJsonString()}");
        if (o["anyOf"] is JsonArray any && !any.Any(s => { var sub = new List<string>(); Check(s!, root, v, at, sub); return sub.Count == 0; }))
            errors.Add($"{at}: matches none of anyOf");
        if (v.ValueKind == JsonValueKind.Object)
        {
            var props = o["properties"]?.AsObject();
            foreach (var p in v.EnumerateObject())
            {
                if (props != null && props.TryGetPropertyValue(p.Name, out var ps)) Check(ps!, root, p.Value, $"{at}/{p.Name}", errors);
                else if (o["additionalProperties"] is { } ap) Check(ap, root, p.Value, $"{at}/{p.Name}", errors);
                else if (props != null) errors.Add($"{at}/{p.Name}: not a field of this format");
            }
            if (o["required"] is JsonArray req)
                foreach (var name in req.Select(x => x!.GetValue<string>()))
                    if (!v.TryGetProperty(name, out _)) errors.Add($"{at}/{name}: missing");
        }
        if (v.ValueKind == JsonValueKind.Array && o["items"] is { } items)
        {
            var i = 0;
            foreach (var item in v.EnumerateArray()) Check(items, root, item, $"{at}/{i++}", errors);
        }
    }

    static bool Is(JsonElement v, string type) => type switch
    {
        "object" => v.ValueKind == JsonValueKind.Object,
        "array" => v.ValueKind == JsonValueKind.Array,
        "string" => v.ValueKind == JsonValueKind.String,
        "boolean" => v.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "null" => v.ValueKind == JsonValueKind.Null,
        "integer" => v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out _),
        "number" => v.ValueKind == JsonValueKind.Number,
        _ => false,
    };
}
