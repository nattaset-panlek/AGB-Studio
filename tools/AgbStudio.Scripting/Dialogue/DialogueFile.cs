using System.Text;
using System.Text.Json;

namespace AgbStudio.Scripting.Dialogue;

/// <summary>
/// A Dialogue Editor file (<c>dialogue/&lt;name&gt;.dlg</c>, UTF-8 JSON, D-03). Format 1:
/// <code>
/// {
///   "format": 1, "name": "forest_intro", "textSpeed": 2, "blip": "blip",
///   "speakers": [ { "name": "Elder", "portrait": "elder", "blip": "blip_low", "textSpeed": 1 } ],
///   "conversations": [ { "id": "intro", "lines": [
///     { "id": "l1", "speaker": "Elder", "key": "dlg.forest.help_me", "text": { "en": "Help!", "th": "..." },
///       "portrait": "elder_worried", "speed": 1, "blip": "blip_low",
///       "showIf": "!Flags.MetElder", "thenSet": [ "Flags.MetElder = true" ], "next": "l3" },
///     { "id": "l2", "speaker": "Elder", "key": "dlg.forest.ask",
///       "choices": [ { "key": "dlg.forest.yes", "goto": "l4", "showIf": "...", "thenSet": [ "..." ] },
///                    { "key": "dlg.forest.no", "scene": "Forest_02" } ] },
///     { "command": "setFlag", "flag": "MetElder", "value": true },
///     { "command": "giveItem", "table": "Items", "item": "Potion", "count": 1 },
///     { "command": "playSound", "sound": "jump" },
///     { "command": "wait", "frames": 30 }, { "command": "wait", "seconds": 0.5 },
///     { "command": "shakeCamera", "amplitude": 2, "frames": 20 },
///     { "command": "goToScene", "scene": "Forest_02" },
///     { "command": "end" } ] } ]
/// }
/// </code>
/// Text per language is kept with the line for the editor; the build takes string ids from the Localization tables,
/// where the Dialogue Editor saves the same keys.
/// </summary>
public sealed class DialogueFile
{
    public int Format { get; set; } = 1;
    public string Name { get; set; } = "";
    public int TextSpeed { get; set; } = -1;
    public string? Blip { get; set; }
    public List<DialogueSpeaker> Speakers { get; set; } = new();
    public List<Conversation> Conversations { get; set; } = new();

    /// <summary>JSON pointer ("/conversations/0/lines/2") to the 1-based line in the file, for diagnostics.</summary>
    public Dictionary<string, int> LineOf { get; } = new(StringComparer.Ordinal);

    static readonly JsonSerializerOptions s_json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static DialogueFile Parse(string json)
    {
        DialogueFile? f;
        try
        {
            f = JsonSerializer.Deserialize<DialogueFile>(json, s_json);
        }
        catch (JsonException e)
        {
            throw new FormatException($"The dialogue file is not valid JSON (line {e.LineNumber + 1}): {e.Message}", e);
        }
        if (f == null) throw new FormatException("The dialogue file is empty.");
        if (f.Format != 1) throw new FormatException($"Dialogue format {f.Format} is newer than this AGB Studio understands (1).");
        foreach (var (path, line) in JsonLines.Map(json)) f.LineOf[path] = line;
        return f;
    }

    public string ToJson() => JsonSerializer.Serialize(this, s_json);

    /// <summary>Every string key with its text per language (for Localization).</summary>
    public Dictionary<string, Dictionary<string, string>> Texts()
    {
        var d = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        foreach (var c in Conversations)
            foreach (var l in c.Lines)
            {
                if (l.Key != null && l.Text != null) d[l.Key] = l.Text;
                foreach (var ch in l.Choices ?? [])
                    if (ch.Key != null && ch.Text != null) d[ch.Key] = ch.Text;
            }
        return d;
    }
}

public sealed class DialogueSpeaker
{
    public string Name { get; set; } = "";
    public string? Portrait { get; set; }
    public string? Blip { get; set; }
    public int TextSpeed { get; set; } = -1;
}

public sealed class Conversation
{
    public string Id { get; set; } = "";
    public List<DialogueLine> Lines { get; set; } = new();
}

public sealed class DialogueLine
{
    public string? Id { get; set; }
    public string? Speaker { get; set; }
    public string? Key { get; set; }
    public Dictionary<string, string>? Text { get; set; }
    public string? Portrait { get; set; }
    public int? Speed { get; set; }
    public string? Blip { get; set; }
    public string? ShowIf { get; set; }
    public JsonElement? ThenSet { get; set; }
    public string? Next { get; set; }
    public List<DialogueChoice>? Choices { get; set; }

    // Commands (a line without text): goToScene, setFlag, giveItem, playSound, wait, shakeCamera, end.
    public string? Command { get; set; }
    public string? Scene { get; set; }
    public string? Flag { get; set; }
    public bool? Value { get; set; }
    public string? Table { get; set; }
    public string? Item { get; set; }
    public int? Count { get; set; }
    public string? Sound { get; set; }
    public double? Volume { get; set; }
    public int? Frames { get; set; }
    public double? Seconds { get; set; }
    public int? Amplitude { get; set; }
}

public sealed class DialogueChoice
{
    public string? Key { get; set; }
    public Dictionary<string, string>? Text { get; set; }
    public string? ShowIf { get; set; }
    public JsonElement? ThenSet { get; set; }
    public string? Goto { get; set; }
    public string? Scene { get; set; }
    public bool End { get; set; }
}

/// <summary>Line numbers of JSON values by JSON pointer.</summary>
internal static class JsonLines
{
    public static Dictionary<string, int> Map(string json)
    {
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        var bytes = Encoding.UTF8.GetBytes(json);
        var lineStarts = new List<int> { 0 };
        for (var i = 0; i < bytes.Length; i++) if (bytes[i] == '\n') lineStarts.Add(i + 1);
        int LineAt(long offset)
        {
            var i = lineStarts.BinarySearch((int)offset);
            return (i >= 0 ? i : ~i - 1) + 1;
        }
        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var stack = new List<(bool Array, int Index, string? Prop)>();
        string Path()
        {
            var sb = new StringBuilder();
            foreach (var (array, index, prop) in stack) sb.Append('/').Append(array ? index.ToString(System.Globalization.CultureInfo.InvariantCulture) : prop);
            return sb.ToString();
        }
        try
        {
            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName:
                        stack[^1] = (false, 0, reader.GetString());
                        break;
                    case JsonTokenType.StartObject:
                    case JsonTokenType.StartArray:
                        if (stack.Count > 0) map.TryAdd(Path(), LineAt(reader.TokenStartIndex));
                        stack.Add((reader.TokenType == JsonTokenType.StartArray, 0, null));
                        break;
                    case JsonTokenType.EndObject:
                    case JsonTokenType.EndArray:
                        stack.RemoveAt(stack.Count - 1);
                        if (stack.Count > 0 && stack[^1].Array) stack[^1] = (true, stack[^1].Index + 1, null);
                        break;
                    default:
                        if (stack.Count == 0) break;
                        map.TryAdd(Path(), LineAt(reader.TokenStartIndex));
                        if (stack[^1].Array) stack[^1] = (true, stack[^1].Index + 1, null);
                        break;
                }
            }
        }
        catch (JsonException) { }
        return map;
    }
}
