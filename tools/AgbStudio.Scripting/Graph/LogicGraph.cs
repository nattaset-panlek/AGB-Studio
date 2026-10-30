using System.Text;
using System.Text.Json;

namespace AgbStudio.Scripting.Graph;

/// <summary>Pin kinds of the Logic Graph (pin colours in Figma).</summary>
public enum PinKind { Exec, Bool, Fixed, Actor, Text }

public enum NodeRole
{
    /// <summary>Starts a C# method (On Update, On Touch ...).</summary>
    Event,
    /// <summary>A statement on the exec chain (Set Velocity Y, Play Sound ...).</summary>
    Action,
    /// <summary>Exec flow with branches (Branch, Compare Tag).</summary>
    Flow,
    /// <summary>A value (Button Pressed, AND, Is On Ground ...).</summary>
    Value,
}

public sealed record PinDef(string Name, PinKind Kind, bool Output);

/// <summary>A property shown on the node; Number props keep the text the user typed ("-3.50").</summary>
public sealed record PropDef(string Name, PropKind Kind, string Default);

public enum PropKind { Number, Text, Key, Bool, Compare }

/// <summary>One node type of the palette (category as in the Figma Logic Graph sidebar).</summary>
public sealed record NodeDef(string Type, string Category, string Title, NodeRole Role, IReadOnlyList<PinDef> Pins, IReadOnlyList<PropDef> Props)
{
    public PinDef? Pin(string name) => Pins.FirstOrDefault(p => p.Name == name);
}

/// <summary>A node instance in a graph.</summary>
public sealed class GraphNode
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public Dictionary<string, string> Props { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>A connection from an output pin to an input pin ("node.pin").</summary>
public sealed record GraphLink(string From, string To)
{
    public string FromNode => From[..From.IndexOf('.')];
    public string FromPin => From[(From.IndexOf('.') + 1)..];
    public string ToNode => To[..To.IndexOf('.')];
    public string ToPin => To[(To.IndexOf('.') + 1)..];
}

/// <summary>
/// A Logic Graph file (<c>scripts/&lt;Name&gt;.graph</c>, UTF-8 JSON). Format 1:
/// <code>
/// { "format": 1, "class": "PlayerLogic",
///   "nodes": [ { "id": "n1", "type": "OnUpdate", "x": 40, "y": 60 },
///              { "id": "n6", "type": "SetVelocityY", "x": 520, "y": 60, "props": { "value": -3.50 } } ],
///   "links": [ { "from": "n1.out", "to": "n5.in" }, { "from": "n2.value", "to": "n4.a" } ] }
/// </code>
/// Exec pins are "in" / "out" ("true" / "false" on Branch, "match" on Compare Tag); value outputs are "value".
/// Graph -> C# is always generated; C# -> Graph only for files the graph generated (D-11).
/// </summary>
public sealed class LogicGraph
{
    public int Format { get; set; } = 1;
    public string Class { get; set; } = "";
    public List<GraphNode> Nodes { get; set; } = new();
    public List<GraphLink> Links { get; set; } = new();

    public GraphNode? Node(string id) => Nodes.FirstOrDefault(n => n.Id == id);

    /// <summary>The link into an input pin, if any.</summary>
    public GraphLink? Into(string node, string pin) => Links.FirstOrDefault(l => l.ToNode == node && l.ToPin == pin);

    /// <summary>Links out of an output pin.</summary>
    public IEnumerable<GraphLink> OutOf(string node, string pin) => Links.Where(l => l.FromNode == node && l.FromPin == pin);

    public static LogicGraph Parse(string json)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        }
        catch (JsonException e)
        {
            throw new FormatException($"The graph file is not valid JSON (line {e.LineNumber + 1}): {e.Message}", e);
        }
        using (doc)
        {
            var root = doc.RootElement;
            var g = new LogicGraph
            {
                Format = root.TryGetProperty("format", out var fmt) ? fmt.GetInt32() : 1,
                Class = root.TryGetProperty("class", out var cls) ? cls.GetString() ?? "" : "",
            };
            if (g.Format != 1) throw new FormatException($"Graph format {g.Format} is newer than this AGB Studio understands (1).");
            if (root.TryGetProperty("nodes", out var nodes))
                foreach (var n in nodes.EnumerateArray())
                {
                    var node = new GraphNode
                    {
                        Id = n.GetProperty("id").GetString() ?? "",
                        Type = n.GetProperty("type").GetString() ?? "",
                        X = n.TryGetProperty("x", out var x) ? x.GetDouble() : 0,
                        Y = n.TryGetProperty("y", out var y) ? y.GetDouble() : 0,
                    };
                    if (n.TryGetProperty("props", out var props))
                        foreach (var p in props.EnumerateObject())
                            node.Props[p.Name] = p.Value.ValueKind switch
                            {
                                JsonValueKind.String => p.Value.GetString() ?? "",
                                JsonValueKind.True => "true",
                                JsonValueKind.False => "false",
                                _ => p.Value.GetRawText(),
                            };
                    g.Nodes.Add(node);
                }
            if (root.TryGetProperty("links", out var links))
                foreach (var l in links.EnumerateArray())
                    g.Links.Add(new GraphLink(l.GetProperty("from").GetString() ?? "", l.GetProperty("to").GetString() ?? ""));
            return g;
        }
    }

    public string ToJson()
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true }))
        {
            w.WriteStartObject();
            w.WriteNumber("format", Format);
            w.WriteString("class", Class);
            w.WriteStartArray("nodes");
            foreach (var n in Nodes)
            {
                w.WriteStartObject();
                w.WriteString("id", n.Id);
                w.WriteString("type", n.Type);
                w.WriteNumber("x", n.X);
                w.WriteNumber("y", n.Y);
                if (n.Props.Count > 0)
                {
                    w.WriteStartObject("props");
                    var def = NodeCatalog.Find(n.Type);
                    foreach (var (k, v) in n.Props)
                    {
                        var kind = def?.Props.FirstOrDefault(p => p.Name == k)?.Kind;
                        if (kind == PropKind.Number && double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))
                        {
                            w.WritePropertyName(k);
                            w.WriteRawValue(v);
                        }
                        else if (kind == PropKind.Bool) w.WriteBoolean(k, v == "true");
                        else w.WriteString(k, v);
                    }
                    w.WriteEndObject();
                }
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteStartArray("links");
            foreach (var l in Links)
            {
                w.WriteStartObject();
                w.WriteString("from", l.From);
                w.WriteString("to", l.To);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(ms.ToArray()) + "\n";
    }
}

/// <summary>The node palette by category: Events, Input, Physics, Flow, Math, Actor, Sound, Data &amp; Save, Text &amp; UI, Other.</summary>
public static class NodeCatalog
{
    static PinDef In(string n, PinKind k) => new(n, k, false);
    static PinDef Out(string n, PinKind k) => new(n, k, true);
    static readonly PinDef ExecIn = In("in", PinKind.Exec), ExecOut = Out("out", PinKind.Exec);
    static PropDef Num(string n, string d) => new(n, PropKind.Number, d);
    static PropDef Txt(string n, string d) => new(n, PropKind.Text, d);

    public static readonly string[] Categories = ["Events", "Input", "Physics", "Flow", "Math", "Actor", "Sound", "Data & Save", "Text & UI", "Other"];

    public static readonly IReadOnlyList<NodeDef> All =
    [
        // Events
        new("OnStart", "Events", "On Start", NodeRole.Event, [ExecOut], []),
        new("OnUpdate", "Events", "On Update", NodeRole.Event, [ExecOut], []),
        new("OnLateUpdate", "Events", "On Late Update", NodeRole.Event, [ExecOut], []),
        new("OnTouch", "Events", "On Touch", NodeRole.Event, [ExecOut, Out("other", PinKind.Actor)], []),
        new("OnDestroy", "Events", "On Destroy", NodeRole.Event, [ExecOut], []),
        // Input
        new("ButtonPressed", "Input", "Button Pressed", NodeRole.Value, [Out("value", PinKind.Bool)], [new("button", PropKind.Key, "A")]),
        new("ButtonHeld", "Input", "Button Held", NodeRole.Value, [Out("value", PinKind.Bool)], [new("button", PropKind.Key, "A")]),
        new("ButtonReleased", "Input", "Button Released", NodeRole.Value, [Out("value", PinKind.Bool)], [new("button", PropKind.Key, "A")]),
        new("AxisX", "Input", "Axis X", NodeRole.Value, [Out("value", PinKind.Fixed)], []),
        new("AxisY", "Input", "Axis Y", NodeRole.Value, [Out("value", PinKind.Fixed)], []),
        new("AnyKey", "Input", "Any Key", NodeRole.Value, [Out("value", PinKind.Bool)], []),
        // Physics (PlatformerBody of the actor)
        new("IsOnGround", "Physics", "Is On Ground", NodeRole.Value, [Out("value", PinKind.Bool)], []),
        new("VelocityX", "Physics", "Velocity X", NodeRole.Value, [Out("value", PinKind.Fixed)], []),
        new("VelocityY", "Physics", "Velocity Y", NodeRole.Value, [Out("value", PinKind.Fixed)], []),
        new("SetVelocityX", "Physics", "Set Velocity X", NodeRole.Action, [ExecIn, ExecOut, In("value", PinKind.Fixed)], [Num("value", "0")]),
        new("SetVelocityY", "Physics", "Set Velocity Y", NodeRole.Action, [ExecIn, ExecOut, In("value", PinKind.Fixed)], [Num("value", "0")]),
        new("Jump", "Physics", "Jump", NodeRole.Action, [ExecIn, ExecOut], []),
        // Flow
        new("Branch", "Flow", "Branch", NodeRole.Flow, [ExecIn, In("condition", PinKind.Bool), Out("true", PinKind.Exec), Out("false", PinKind.Exec)], []),
        // Math
        new("And", "Math", "AND", NodeRole.Value, [In("a", PinKind.Bool), In("b", PinKind.Bool), Out("value", PinKind.Bool)], []),
        new("Or", "Math", "OR", NodeRole.Value, [In("a", PinKind.Bool), In("b", PinKind.Bool), Out("value", PinKind.Bool)], []),
        new("Not", "Math", "NOT", NodeRole.Value, [In("a", PinKind.Bool), Out("value", PinKind.Bool)], []),
        new("Add", "Math", "Add", NodeRole.Value, [In("a", PinKind.Fixed), In("b", PinKind.Fixed), Out("value", PinKind.Fixed)], [Num("a", "0"), Num("b", "0")]),
        new("Subtract", "Math", "Subtract", NodeRole.Value, [In("a", PinKind.Fixed), In("b", PinKind.Fixed), Out("value", PinKind.Fixed)], [Num("a", "0"), Num("b", "0")]),
        new("Multiply", "Math", "Multiply", NodeRole.Value, [In("a", PinKind.Fixed), In("b", PinKind.Fixed), Out("value", PinKind.Fixed)], [Num("a", "0"), Num("b", "1")]),
        new("Compare", "Math", "Compare", NodeRole.Value, [In("a", PinKind.Fixed), In("b", PinKind.Fixed), Out("value", PinKind.Bool)], [Num("a", "0"), Num("b", "0"), new("op", PropKind.Compare, ">")]),
        // Actor
        new("CompareTag", "Actor", "Compare Tag", NodeRole.Flow, [ExecIn, In("actor", PinKind.Actor), Out("match", PinKind.Exec)], [Txt("tag", "Player")]),
        new("Destroy", "Actor", "Destroy", NodeRole.Action, [ExecIn, ExecOut, In("actor", PinKind.Actor)], []),
        new("Self", "Actor", "Self", NodeRole.Value, [Out("value", PinKind.Actor)], []),
        // Sound
        new("PlaySound", "Sound", "Play Sound", NodeRole.Action, [ExecIn, ExecOut], [Txt("sound", "jump"), Num("volume", "1")]),
        new("PlayMusic", "Sound", "Play Music", NodeRole.Action, [ExecIn, ExecOut], [Txt("music", "")]),
        new("StopMusic", "Sound", "Stop Music", NodeRole.Action, [ExecIn, ExecOut], []),
        // Data & Save
        new("GetVariable", "Data & Save", "Get Variable", NodeRole.Value, [Out("value", PinKind.Fixed)], [Txt("variable", "score")]),
        new("SetVariable", "Data & Save", "Set Variable", NodeRole.Action, [ExecIn, ExecOut, In("value", PinKind.Fixed)], [Txt("variable", "score"), Num("value", "0")]),
        new("AddToVariable", "Data & Save", "Add to Variable", NodeRole.Action, [ExecIn, ExecOut, In("amount", PinKind.Fixed)], [Txt("variable", "score"), Num("amount", "1")]),
        new("SaveGame", "Data & Save", "Save Game", NodeRole.Action, [ExecIn, ExecOut], [Num("slot", "0")]),
        new("Checkpoint", "Data & Save", "Checkpoint", NodeRole.Action, [ExecIn, ExecOut], []),
        // Text & UI
        new("ShowText", "Text & UI", "Show Text", NodeRole.Action, [ExecIn, ExecOut], [Txt("key", "")]),
        // Other
        new("DebugLog", "Other", "Debug Log", NodeRole.Action, [ExecIn, ExecOut], [Txt("text", "")]),
        new("LoadScene", "Other", "Load Scene", NodeRole.Action, [ExecIn, ExecOut], [Txt("scene", "")]),
        new("ShakeCamera", "Other", "Shake Camera", NodeRole.Action, [ExecIn, ExecOut], [Num("amplitude", "2"), Num("frames", "20")]),
    ];

    public static NodeDef? Find(string type) => All.FirstOrDefault(n => n.Type == type);
}
