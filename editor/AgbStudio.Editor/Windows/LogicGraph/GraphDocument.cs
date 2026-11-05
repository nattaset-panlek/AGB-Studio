using System.Globalization;
using System.Text;
using System.Text.Json;
using AgbStudio.Editor.Controls;
using AgbStudio.Scripting.Graph;
using Avalonia;

namespace AgbStudio.Editor.Windows.LogicGraph;

/// <summary>A comment frame around nodes ("Collect coin"), in graph coordinates.</summary>
public sealed class GraphComment
{
    public string Title { get; set; } = "Comment";
    public double X { get; set; }
    public double Y { get; set; }
    public double W { get; set; }
    public double H { get; set; }
}

/// <summary>A pin as the canvas draws it: owner node, pin name, kind, side and its centre relative to the node.</summary>
public sealed record PinGeometry(string Node, string Pin, PinKind Kind, bool Output, Point Offset);

/// <summary>One row of a node body: label, optional property box, pins at the left / right edge.</summary>
public sealed record NodeRow(string? Label, string? Prop, PinDef? Left, PinDef? Right);

/// <summary>
/// A Logic Graph file as the editor works on it (T-P17): the compiled graph (AgbStudio.Scripting LogicGraph) plus what only
/// the editor needs, stored as extra properties of the same .graph file that the compiler ignores: comment frames,
/// collapsed nodes and disabled nodes (kept out of "nodes" so they are not compiled).
/// </summary>
public sealed class GraphDocument
{
    public const double NodeWidth = 180, HeaderHeight = 30, RowHeight = 26, BodyPad = 7;

    public Scripting.Graph.LogicGraph Graph { get; private set; } = new();
    public List<GraphComment> Comments { get; } = [];
    public HashSet<string> Collapsed { get; } = new(StringComparer.Ordinal);
    /// <summary>Disabled nodes with the links that touched them (not compiled).</summary>
    public List<GraphNode> DisabledNodes { get; } = [];
    public List<GraphLink> DisabledLinks { get; } = [];
    public string Path { get; set; } = "";

    public string ClassName => Graph.Class.Length > 0 ? Graph.Class : System.IO.Path.GetFileNameWithoutExtension(Path);

    /// <summary>"Player · Logic" for PlayerLogic.graph (title bar, Doc Tab).</summary>
    public string DisplayName => ClassName.EndsWith("Logic", StringComparison.Ordinal) && ClassName.Length > 5 ? $"{ClassName[..^5]} · Logic" : ClassName;

    public IEnumerable<GraphNode> AllNodes => Graph.Nodes.Concat(DisabledNodes);
    public IEnumerable<GraphLink> AllLinks => Graph.Links.Concat(DisabledLinks);
    public GraphNode? Node(string id) => AllNodes.FirstOrDefault(n => n.Id == id);
    public bool IsDisabled(string id) => DisabledNodes.Any(n => n.Id == id);

    // ---- file ------------------------------------------------------------------------------------------------

    public static GraphDocument Load(string path)
    {
        var d = new GraphDocument { Path = path };
        d.FromJson(File.ReadAllText(path));
        return d;
    }

    public static GraphDocument New(string path, string className) => new() { Path = path, Graph = new Scripting.Graph.LogicGraph { Class = className } };

    public void FromJson(string json)
    {
        Graph = Scripting.Graph.LogicGraph.Parse(json);
        Comments.Clear();
        Collapsed.Clear();
        DisabledNodes.Clear();
        DisabledLinks.Clear();
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        var root = doc.RootElement;
        if (root.TryGetProperty("comments", out var cs))
            foreach (var c in cs.EnumerateArray())
                Comments.Add(new GraphComment
                {
                    Title = c.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "",
                    X = c.GetProperty("x").GetDouble(), Y = c.GetProperty("y").GetDouble(), W = c.GetProperty("w").GetDouble(), H = c.GetProperty("h").GetDouble(),
                });
        if (root.TryGetProperty("collapsed", out var col))
            foreach (var c in col.EnumerateArray()) Collapsed.Add(c.GetString() ?? "");
        if (root.TryGetProperty("disabled", out var dis))
        {
            // the disabled part is a graph of its own (same JSON shape)
            var g = Scripting.Graph.LogicGraph.Parse(dis.GetRawText());
            DisabledNodes.AddRange(g.Nodes);
            DisabledLinks.AddRange(g.Links);
        }
    }

    public string ToJson()
    {
        var json = Graph.ToJson().TrimEnd();
        var extra = new StringBuilder();
        if (Comments.Count > 0)
        {
            extra.Append(",\n  \"comments\": [\n");
            extra.Append(string.Join(",\n", Comments.Select(c => string.Create(CultureInfo.InvariantCulture,
                $"    {{ \"title\": {JsonSerializer.Serialize(c.Title)}, \"x\": {c.X}, \"y\": {c.Y}, \"w\": {c.W}, \"h\": {c.H} }}"))));
            extra.Append("\n  ]");
        }
        if (Collapsed.Count > 0) extra.Append(",\n  \"collapsed\": [").Append(string.Join(", ", Collapsed.Order(StringComparer.Ordinal).Select(c => JsonSerializer.Serialize(c)))).Append(']');
        if (DisabledNodes.Count > 0)
        {
            var g = new Scripting.Graph.LogicGraph { Class = Graph.Class, Nodes = [.. DisabledNodes], Links = [.. DisabledLinks] };
            var inner = g.ToJson().TrimEnd().Replace("\n", "\n  ");
            extra.Append(",\n  \"disabled\": ").Append(inner);
        }
        if (extra.Length == 0) return json + "\n";
        return json[..json.LastIndexOf('}')].TrimEnd() + extra + "\n}\n";
    }

    public void Save()
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        File.WriteAllText(Path, ToJson(), new UTF8Encoding(false));
        // the generated .cs next to the graph, for the Script Editor and external IDEs (read-only, D-11)
        File.WriteAllText(System.IO.Path.ChangeExtension(Path, ".cs"), GenerateCSharp(), new UTF8Encoding(false));
    }

    public string GenerateCSharp() => GraphCodeGen.Generate(Graph, ClassName);

    // ---- editing helpers -----------------------------------------------------------------------------------

    public string NextId()
    {
        var used = AllNodes.Select(n => n.Id).ToHashSet(StringComparer.Ordinal);
        var i = 1;
        while (used.Contains("n" + i)) i++;
        return "n" + i;
    }

    public void SetDisabled(string id, bool disabled)
    {
        if (disabled && Graph.Node(id) is { } n)
        {
            Graph.Nodes.Remove(n);
            DisabledNodes.Add(n);
            var moved = Graph.Links.Where(l => l.FromNode == id || l.ToNode == id).ToList();
            foreach (var l in moved) Graph.Links.Remove(l);
            DisabledLinks.AddRange(moved);
        }
        else if (!disabled && DisabledNodes.FirstOrDefault(x => x.Id == id) is { } d)
        {
            DisabledNodes.Remove(d);
            Graph.Nodes.Add(d);
            // links come back when both ends are enabled
            foreach (var l in DisabledLinks.Where(l => (l.FromNode == id || l.ToNode == id) && !IsDisabled(l.FromNode) && !IsDisabled(l.ToNode)).ToList())
            {
                DisabledLinks.Remove(l);
                Graph.Links.Add(l);
            }
        }
    }

    public void RemoveNode(string id)
    {
        Graph.Nodes.RemoveAll(n => n.Id == id);
        DisabledNodes.RemoveAll(n => n.Id == id);
        Graph.Links.RemoveAll(l => l.FromNode == id || l.ToNode == id);
        DisabledLinks.RemoveAll(l => l.FromNode == id || l.ToNode == id);
        Collapsed.Remove(id);
    }

    /// <summary>Connects an output pin to an input pin; a data input takes one link (the old one is replaced).</summary>
    public bool Connect(string fromNode, string fromPin, string toNode, string toPin)
    {
        if (fromNode == toNode) return false;
        var a = Node(fromNode);
        var b = Node(toNode);
        if (a is null || b is null) return false;
        var pa = NodeCatalog.Find(a.Type)?.Pin(fromPin);
        var pb = NodeCatalog.Find(b.Type)?.Pin(toPin);
        if (pa is null || pb is null || !pa.Output || pb.Output || pa.Kind != pb.Kind) return false;
        if (AllLinks.Any(l => l.FromNode == fromNode && l.FromPin == fromPin && l.ToNode == toNode && l.ToPin == toPin)) return false;
        // a data input takes one value; an exec output drives one chain: replace the old link
        Predicate<GraphLink> old = pa.Kind == PinKind.Exec ? l => l.FromNode == fromNode && l.FromPin == fromPin : l => l.ToNode == toNode && l.ToPin == toPin;
        Graph.Links.RemoveAll(old);
        DisabledLinks.RemoveAll(old);
        (IsDisabled(fromNode) || IsDisabled(toNode) ? DisabledLinks : Graph.Links).Add(new GraphLink($"{fromNode}.{fromPin}", $"{toNode}.{toPin}"));
        return true;
    }

    // ---- presentation ----------------------------------------------------------------------------------------

    /// <summary>Figma header colour group of a palette category.</summary>
    public static NodeCategory CategoryOf(string category) => category switch
    {
        "Events" => NodeCategory.Event,
        "Input" => NodeCategory.Input,
        "Physics" => NodeCategory.Physics,
        "Flow" => NodeCategory.Flow,
        "Math" => NodeCategory.Math,
        "Actor" => NodeCategory.Actor,
        "Sound" => NodeCategory.Sound,
        "Data & Save" or "Text & UI" => NodeCategory.Data,
        _ => NodeCategory.Flow,
    };

    /// <summary>Palette categories in the order of the Figma Nodes panel.</summary>
    public static readonly string[] PaletteOrder = ["Events", "Input", "Physics", "Actor", "Flow", "Math", "Sound", "Data & Save", "Text & UI", "Other"];

    public static string CategoryGlyph(string category) => category switch
    {
        "Events" => "refresh", "Input" => "pad", "Physics" => "rocket", "Actor" => "cube", "Flow" => "nodes", "Math" => "plus",
        "Sound" => "sound", "Data & Save" => "db", "Text & UI" => "type", _ => "dots",
    };

    public static string CategoryColor(string category) => category switch
    {
        "Events" => "lit/node-event", "Input" => "lit/node-input", "Physics" => "lit/node-physics", "Actor" => "lit/node-actor", "Math" => "lit/node-math",
        "Sound" => "lit/node-sound", "Data & Save" or "Text & UI" => "lit/node-data", _ => "lit/node-flow",
    };

    static readonly Dictionary<string, string> Labels = new(StringComparer.Ordinal)
    {
        ["other"] = "Other", ["condition"] = "Condition", ["actor"] = "Target", ["a"] = "A", ["b"] = "B", ["value"] = "Value", ["amount"] = "Amount",
        ["button"] = "Key", ["sound"] = "Clip", ["volume"] = "Volume", ["variable"] = "Variable", ["tag"] = "Tag", ["music"] = "Music", ["slot"] = "Slot",
        ["key"] = "Key", ["text"] = "Text", ["scene"] = "Scene", ["amplitude"] = "Amplitude", ["frames"] = "Frames", ["op"] = "Compare",
    };

    public static string Label(string name) => Labels.TryGetValue(name, out var l) ? l : char.ToUpperInvariant(name[0]) + name[1..];

    /// <summary>Label of a node's value output: Pressed / Held / Released, Result, or none for operators (the pin sits on the last input row).</summary>
    static string? OutputLabel(NodeDef def) => def.Type switch
    {
        "ButtonPressed" => "Pressed", "ButtonHeld" => "Held", "ButtonReleased" => "Released", "IsOnGround" => "Result", "AnyKey" => "Pressed",
        "And" or "Or" or "Not" or "Add" or "Subtract" or "Multiply" or "Compare" => null,
        _ => "Value",
    };

    /// <summary>
    /// The body rows of a node (Figma node layout): exec row, inputs and properties, outputs. As the Figma graph (T-P17)
    /// draws them: a choice property (Set Velocity Y "Mode") is edited in the Inspector only; an actor input (Target)
    /// that is not wired is "Self" and gets a row only when the node has no other input (Is On Ground "Target  Self");
    /// an event with inputs draws its exec output under them (On Touch "Tag  Coin", then the exec row, then "Other").
    /// <paramref name="wired"/> tells which inputs have a wire (none when null).
    /// </summary>
    public static IReadOnlyList<NodeRow> Rows(NodeDef def, Func<string, bool>? wired = null)
    {
        var rows = new List<NodeRow>();
        var execIn = def.Pins.FirstOrDefault(p => p.Kind == PinKind.Exec && !p.Output);
        var execOuts = def.Pins.Where(p => p.Kind == PinKind.Exec && p.Output).ToList();
        var dataOuts = def.Pins.Where(p => p.Kind != PinKind.Exec && p.Output).ToList();
        var label = OutputLabel(def);
        var inputs = new List<(string Name, PinDef? Pin, string? Prop)>();
        // properties in catalog order (with their input pin when they have one: "Amount [+10]"), then pins without a property
        foreach (var prop in def.Props.Where(p => p.Kind != PropKind.Choice))
            inputs.Add((prop.Name, def.Pins.FirstOrDefault(p => p.Name == prop.Name && !p.Output && p.Kind != PinKind.Exec), prop.Name));
        var others = inputs.Count + def.Pins.Count(p => p.Kind is not (PinKind.Exec or PinKind.Actor) && !p.Output && def.Props.All(x => x.Name != p.Name));
        foreach (var p in def.Pins.Where(p => p.Kind != PinKind.Exec && !p.Output && def.Props.All(x => x.Name != p.Name)))
            if (p.Kind != PinKind.Actor || others == 0 || wired?.Invoke(p.Name) == true) inputs.Add((p.Name, p, null));
        // an event with inputs draws them above its exec output (On Touch: Tag, then the exec row, then Other)
        var execAfterInputs = execIn == null && execOuts.Count > 0 && inputs.Count > 0;
        if ((execIn != null || execOuts.Count > 0) && !execAfterInputs) rows.Add(new NodeRow(null, null, execIn, execOuts.FirstOrDefault()));
        var extraOuts = new Queue<PinDef>(execOuts.Skip(1));
        for (var i = 0; i < inputs.Count; i++)
        {
            var (name, pin, prop) = inputs[i];
            PinDef? right = extraOuts.Count > 0 ? extraOuts.Dequeue() : null;
            if (right is null && label is null && i == inputs.Count - 1 && dataOuts.Count > 0) right = dataOuts[0];
            rows.Add(new NodeRow(Label(name), prop, pin, right));
        }
        if (execAfterInputs) rows.Add(new NodeRow(null, null, null, execOuts[0]));
        foreach (var o in dataOuts)
        {
            if (label is null && inputs.Count > 0) continue;
            rows.Add(new NodeRow(def.Type == "OnTouch" ? Label(o.Name) : label ?? Label(o.Name), null, null, o));
        }
        while (extraOuts.Count > 0) rows.Add(new NodeRow(null, null, null, extraOuts.Dequeue()));
        return rows;
    }

    public double Height(GraphNode n)
    {
        if (Collapsed.Contains(n.Id) || NodeCatalog.Find(n.Type) is not { } def) return HeaderHeight;
        return HeaderHeight + BodyPad * 2 + Rows(def, Wired(n)).Count * RowHeight;
    }

    /// <summary>The body rows of a node with its wires (see <see cref="Rows(NodeDef, Func{string, bool}?)"/>).</summary>
    public IReadOnlyList<NodeRow> RowsOf(GraphNode n) => NodeCatalog.Find(n.Type) is { } def ? Rows(def, Wired(n)) : [];

    Func<string, bool> Wired(GraphNode n) => pin => AllLinks.Any(l => l.ToNode == n.Id && l.ToPin == pin);

    public Rect Bounds(GraphNode n) => new(n.X, n.Y, WidthOf(n.Type), Height(n));

    /// <summary>Node width by content, as in the Figma graph: operators are narrow, nodes with two value boxes wider.</summary>
    public static double WidthOf(string type) => type switch
    {
        "And" or "Or" or "Not" or "Add" or "Subtract" or "Multiply" or "Compare" => 120,
        "Destroy" => 170,
        "SetVelocityX" or "SetVelocityY" or "PlaySound" or "ShakeCamera" => 188,
        "AddToVariable" or "SetVariable" or "GetVariable" => 196,
        _ => NodeWidth,
    };

    /// <summary>Every pin of a node with its centre relative to the node's top-left corner.</summary>
    public IReadOnlyList<PinGeometry> Pins(GraphNode n)
    {
        if (NodeCatalog.Find(n.Type) is not { } def) return [];
        var list = new List<PinGeometry>();
        if (Collapsed.Contains(n.Id))
        {
            // collapsed: every pin on the header line
            foreach (var p in def.Pins) list.Add(new PinGeometry(n.Id, p.Name, p.Kind, p.Output, new Point(p.Output ? WidthOf(n.Type) : 0, HeaderHeight / 2)));
            return list;
        }
        var rows = Rows(def, Wired(n));
        for (var i = 0; i < rows.Count; i++)
        {
            var y = HeaderHeight + BodyPad + i * RowHeight + RowHeight / 2;
            if (rows[i].Left is { } l) list.Add(new PinGeometry(n.Id, l.Name, l.Kind, false, new Point(0, y)));
            if (rows[i].Right is { } r) list.Add(new PinGeometry(n.Id, r.Name, r.Kind, true, new Point(WidthOf(n.Type), y)));
        }
        return list;
    }

    public Point? PinPoint(string node, string pin, bool output)
    {
        var n = Node(node);
        if (n is null) return null;
        var g = Pins(n).FirstOrDefault(p => p.Pin == pin && p.Output == output);
        return g is null ? null : new Point(n.X + g.Offset.X, n.Y + g.Offset.Y);
    }

    /// <summary>Text of a property box ("-3.50", "jump.wav", "+10" for Add to Variable amounts).</summary>
    public static string PropText(GraphNode n, string prop)
    {
        var v = n.Props.TryGetValue(prop, out var x) ? x : NodeCatalog.Find(n.Type)?.Props.FirstOrDefault(p => p.Name == prop)?.Default ?? "";
        if (n.Type == "AddToVariable" && prop == "amount" && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && d >= 0 && !v.StartsWith('+')) return "+" + v;
        return v;
    }

    /// <summary>Short help of each node (Add Node list, node inspector).</summary>
    public static string Description(string type) => type switch
    {
        "OnStart" => "Runs once when the actor is created.",
        "OnUpdate" => "Runs every frame, 60 times a second.",
        "OnLateUpdate" => "Runs every frame after all Update events.",
        "OnTouch" => "Runs when another actor touches this one; Other is that actor.",
        "OnDestroy" => "Runs when the actor is removed from the scene.",
        "ButtonPressed" => "True on the frame a button goes down.",
        "ButtonHeld" => "True while a button is held.",
        "ButtonReleased" => "True on the frame a button goes up.",
        "AxisX" => "The D-pad as -1, 0 or 1 (left / right).",
        "AxisY" => "The D-pad as -1, 0 or 1 (up / down).",
        "AnyKey" => "True when any button was pressed this frame.",
        "IsOnGround" => "True while the platformer body stands on solid ground.",
        "VelocityX" => "Reads the horizontal speed of the body.",
        "VelocityY" => "Reads the vertical speed of the body.",
        "SetVelocityX" => "Sets the horizontal speed of the target. Negative values move left.",
        "SetVelocityY" => "Sets the vertical speed of the target. Negative values move up.",
        "Jump" => "Jumps with the body's jump force when on the ground.",
        "Branch" => "Runs True or False depending on the condition.",
        "And" => "True when both A and B are true.",
        "Or" => "True when A or B is true.",
        "Not" => "True when A is false.",
        "Add" => "A plus B.",
        "Subtract" => "A minus B.",
        "Multiply" => "A times B.",
        "Compare" => "Compares A with B.",
        "CompareTag" => "Continues when the actor has this tag.",
        "Destroy" => "Removes the target actor from the scene.",
        "Self" => "The actor this graph runs on.",
        "PlaySound" => "Plays a sound effect at a volume from 0 to 1.",
        "PlayMusic" => "Starts a music module.",
        "StopMusic" => "Stops the music.",
        "GetVariable" => "Reads a Game field (Project Settings · Save data).",
        "SetVariable" => "Writes a Game field.",
        "AddToVariable" => "Adds an amount to a Game field, e.g. score.",
        "SaveGame" => "Writes the game state to a save slot.",
        "Checkpoint" => "Remembers the scene and position, then saves.",
        "ShowText" => "Shows a localized string in the dialogue box.",
        "DebugLog" => "Writes a line to the Console.",
        "LoadScene" => "Loads another scene with a fade.",
        "ShakeCamera" => "Shakes the view for a number of frames.",
        "CameraFollow" => "Makes the camera follow an actor.",
        "RandomRange" => "A whole number from Lo to Hi, both included.",
        "LoadGame" => "Reads the game state from a save slot.",
        "SaveExists" => "True when a save slot holds a save.",
        "SetTile" => "Changes one tile of a map layer while the game runs.",
        _ => "",
    };

    /// <summary>Short phrase for the Add Node list ("jump or fall").</summary>
    public static string Hint(string type) => CuratedHints.TryGetValue(type, out var h) ? h
        : Description(type).TrimEnd('.') is var d && d.Length > 28 ? d[..28].TrimEnd() + "…" : Description(type).TrimEnd('.').ToLowerInvariant();

    /// <summary>The short hints the Figma Add Node popover (T-O08) gives its nodes; Add Node ranks these nodes first.</summary>
    static readonly Dictionary<string, string> CuratedHints = new(StringComparer.Ordinal)
    {
        ["SetVelocityX"] = "move left / right at a speed",
        ["SetVelocityY"] = "jump or fall",
        ["GetVelocity"] = "read vx, vy",
        ["VectorFromVelocity"] = "vx, vy → direction",
        ["OnUpdate"] = "every frame",
        ["OnStart"] = "once at start",
        ["OnTouch"] = "when actors touch",
        ["ButtonPressed"] = "button goes down",
        ["Branch"] = "if / else",
        ["PlaySound"] = "sound effect",
        ["AddToVariable"] = "score += amount",
        ["Destroy"] = "remove an actor",
    };

    /// <summary>True for the nodes with a hint of their own (the ones the Figma Add Node lists).</summary>
    public static bool HasCuratedHint(string type) => CuratedHints.ContainsKey(type);

    // ---- Generated C# panel -----------------------------------------------------------------------------------

    /// <summary>
    /// The Generated C# panel text: the event methods of the generated class with braces on the same line and two-space
    /// indents, long conditions wrapped before &amp;&amp; / || (as in Figma).
    /// </summary>
    public static string CompactView(string generated, int wrapAt = 40)
    {
        var lines = generated.Replace("\r\n", "\n").Split('\n');
        var start = Array.FindIndex(lines, l => l.StartsWith("public class", StringComparison.Ordinal));
        var body = lines.Skip(start + 2).Take(Math.Max(0, lines.Length - start - 2)).ToList();
        // drop the class closing brace and the body field / Start initializer
        while (body.Count > 0 && body[^1].Trim().Length == 0) body.RemoveAt(body.Count - 1);
        if (body.Count > 0 && body[^1].Trim() == "}") body.RemoveAt(body.Count - 1);
        var outLines = new List<string>();
        foreach (var raw in body)
        {
            var t = raw.TrimEnd();
            if (t.Trim() is "PlatformerBody body;" or "void Start() => body = Get<PlatformerBody>();" || t.Trim().Length == 0) continue;
            var indent = (t.Length - t.TrimStart().Length) / 4 - 1;
            var text = t.Trim();
            if (text == "{" && outLines.Count > 0) { outLines[^1] += " {"; continue; }
            var pad = new string(' ', Math.Max(0, indent) * 2);
            var line = pad + text;
            if (line.Length > wrapAt && (text.StartsWith("if (", StringComparison.Ordinal) || text.StartsWith("else if", StringComparison.Ordinal)))
            {
                var cut = Math.Max(line.LastIndexOf(" && ", StringComparison.Ordinal), line.LastIndexOf(" || ", StringComparison.Ordinal));
                if (cut > pad.Length)
                {
                    outLines.Add(line[..cut]);
                    line = pad + "    " + line[(cut + 1)..];
                }
            }
            outLines.Add(line);
        }
        return string.Join("\n", outLines);
    }

    /// <summary>Turns an edited Generated C# panel back into the full generated file (for C# -> Graph, D-11).</summary>
    public string FromCompactView(string compact) =>
        GraphCodeGen.HeaderPrefix + System.IO.Path.GetFileNameWithoutExtension(Path) + ".graph. Edit the graph; this file is rewritten when it changes.\n" +
        "using GBA;\n\npublic class " + ClassName + " : Behaviour\n{\n    PlatformerBody body;\n\n" + compact + "\n}\n";
}
