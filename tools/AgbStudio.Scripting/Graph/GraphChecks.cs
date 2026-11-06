namespace AgbStudio.Scripting.Graph;

/// <summary>A node the graph can't build as it is, with the reason in the user's words.</summary>
public sealed record GraphProblem(string Node, string Message);

/// <summary>
/// P20.05: waiting nodes where nothing can wait, and nodes after a wait that the waiting part (Event Script bytecode)
/// can't run (docs/design/P20-graph-chains-that-wait.md). The Logic Graph shows these on the node; the build stops on them.
/// </summary>
public static class GraphChecks
{
    /// <summary>Events whose chains can't wait, with the reason.</summary>
    static readonly Dictionary<string, string> s_noWait = new(StringComparer.Ordinal)
    {
        ["OnUpdate"] = "On Update runs every frame",
        ["OnLateUpdate"] = "On Late Update runs every frame",
        ["OnDestroy"] = "the actor is gone after On Destroy",
    };

    /// <summary>Nodes the waiting part can run; every other node must come before the first wait.</summary>
    public static readonly IReadOnlySet<string> AfterWait = new HashSet<string>(StringComparer.Ordinal)
    {
        "Wait", "Say", "Choice", "Move", "FadeIn", "FadeOut",
        "PlaySound", "PlayMusic", "StopMusic", "ShakeCamera",
        "SetVariable", "AddToVariable", "GetVariable",
        "SaveGame", "LoadGame", "Checkpoint", "ShowText", "DebugLog", "LoadScene",
        "Branch", "And", "Or", "Not", "Add", "Subtract", "Multiply", "Compare", "RandomRange",
    };

    public static List<GraphProblem> Check(LogicGraph g)
    {
        var problems = new List<GraphProblem>();
        var reported = new HashSet<string>(StringComparer.Ordinal);
        void Report(GraphNode n, string message)
        {
            if (reported.Add(n.Id)) problems.Add(new GraphProblem(n.Id, message));
        }

        foreach (var ev in g.Nodes.Where(n => NodeCatalog.Find(n.Type)?.Role == NodeRole.Event))
        {
            var chain = GraphCodeGen.Reach(g, ev).Skip(1).ToList();
            if (s_noWait.TryGetValue(ev.Type, out var why))
            {
                foreach (var w in chain.Where(n => GraphCodeGen.WaitingNodes.Contains(n.Type)))
                    Report(w, $"{Title(w)} can't wait here: {why}. Start this chain from On Start, On Touch or On Button.");
                continue;
            }
            // From each waiting node on, the chain is the waiting part.
            var after = new HashSet<string>(StringComparer.Ordinal);
            foreach (var w in chain.Where(n => GraphCodeGen.WaitingNodes.Contains(n.Type)))
                foreach (var n in GraphCodeGen.Reach(g, w)) after.Add(n.Id);
            foreach (var id in after)
            {
                var n = g.Node(id)!;
                if (!AfterWait.Contains(n.Type))
                    Report(n, $"{Title(n)} can't run after a wait: the waiting part runs as a cutscene, which can't do that. Move it before the first wait.");
                foreach (var v in Values(g, n))
                {
                    if (v.Link.FromPin == "other" && v.Source.Type == "OnTouch")
                        Report(n, $"{Title(n)} can't use the actor On Touch met after a wait: a cutscene can't keep it. Use it before the first wait.");
                    else if (!AfterWait.Contains(v.Source.Type))
                        Report(v.Source, $"{Title(v.Source)} can't be read after a wait: a cutscene can't read it. Read it before the first wait, into a variable.");
                }
            }
        }
        foreach (var c in g.Nodes.Where(n => n.Type == "Choice"))
            if (Enumerable.Range(1, 4).All(i => !c.Props.TryGetValue("option" + i, out var o) || o.Length == 0))
                Report(c, "Choice has no options. Pick a text key for Option 1 at least.");
        return problems;
    }

    static string Title(GraphNode n) => NodeCatalog.Find(n.Type)?.Title ?? n.Type;

    // Every node feeding a value into this node, through value nodes too.
    static IEnumerable<(GraphNode Source, GraphLink Link)> Values(LogicGraph g, GraphNode n)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<GraphNode>([n]);
        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            foreach (var l in g.Links.Where(l => l.ToNode == cur.Id && NodeCatalog.Find(cur.Type)?.Pin(l.ToPin)?.Kind != PinKind.Exec))
                if (g.Node(l.FromNode) is { } src && seen.Add(src.Id + "." + l.FromPin))
                {
                    yield return (src, l);
                    if (NodeCatalog.Find(src.Type)?.Role == NodeRole.Value) queue.Enqueue(src);
                }
        }
    }
}
