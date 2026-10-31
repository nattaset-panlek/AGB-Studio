using System.Text.Json;
using System.Text.RegularExpressions;
using AgbStudio.Editor.Tests.Support;

namespace AgbStudio.Editor.Tests.Prototype;

/// <summary>
/// One prototype link of "AGB Studio · Screens" as exported read-only from Figma (docs/evidence/figma/prototype.json):
/// the source window, the clicked node's path inside it, trigger, action and destination window.
/// </summary>
public sealed record ProtoLink(int Index, string From, string Node, string NodeName, string Trigger, string Action, string? To)
{
    /// <summary>The source window's checklist id ("T-P01").</summary>
    public string FromId => PrototypeData.IdOf(From);
    /// <summary>The destination window's id, or null for CLOSE / BACK.</summary>
    public string? ToId => To is null ? null : PrototypeData.IdOf(To);

    public override string ToString() => $"#{Index} {From} › {Node} › {Action}{(To is null ? "" : " " + To)}";
}

/// <summary>A prototype flow: its name and the frame it starts on.</summary>
public sealed record ProtoFlow(string Name, string Start)
{
    public string StartId => PrototypeData.IdOf(Start);
}

/// <summary>
/// Reads the Figma prototype export at test time (T-X01..T-X03). Frame names map to window ids through the FRAMES table
/// of Shot/figma-export.mjs, the same table the SHOT export uses.
/// </summary>
public static class PrototypeData
{
    sealed record JsonLink(string from, string node, string nodeName, string trigger, string action, string? to);
    sealed record JsonFlow(string name, string start);
    sealed record JsonFile(List<JsonFlow> flows, List<JsonLink> links);

    static readonly Lazy<JsonFile> s_file = new(() =>
        JsonSerializer.Deserialize<JsonFile>(File.ReadAllText(Path.Combine(Repo.FigmaEvidence, "prototype.json")))!);

    static readonly Lazy<IReadOnlyDictionary<string, string>> s_frames = new(() =>
    {
        var script = File.ReadAllText(Path.Combine(Repo.Root, "tests", "AgbStudio.Editor.Tests", "Shot", "figma-export.mjs"));
        var m = Regex.Match(script, @"const FRAMES = (\{.*?\});", RegexOptions.Singleline);
        if (!m.Success) throw new InvalidDataException("FRAMES not found in figma-export.mjs");
        var byId = JsonSerializer.Deserialize<Dictionary<string, string>>(m.Groups[1].Value)!;
        return byId.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.Ordinal);
    });

    /// <summary>Frame name to window id ("Scene Editor" → "T-P01").</summary>
    public static IReadOnlyDictionary<string, string> Frames => s_frames.Value;

    public static string IdOf(string frame) =>
        Frames.TryGetValue(frame, out var id) ? id : throw new KeyNotFoundException($"Frame \"{frame}\" is not in FRAMES of figma-export.mjs");

    public static IReadOnlyList<ProtoLink> Links { get; } = s_file.Value.links
        .Select((l, i) => new ProtoLink(i, l.from, l.node, l.nodeName, l.trigger, l.action, l.to)).ToList();

    public static IReadOnlyList<ProtoFlow> Flows { get; } = s_file.Value.flows.Select(f => new ProtoFlow(f.name, f.start)).ToList();

    /// <summary>The links that leave <paramref name="windowId"/>, in export order.</summary>
    public static IEnumerable<ProtoLink> From(string windowId) => Links.Where(l => l.FromId == windowId);

    /// <summary>
    /// Every link reachable from a flow's start frame, window by window in the order the flow discovers them
    /// (breadth first; each window's links in export order).
    /// </summary>
    public static IReadOnlyList<ProtoLink> Reachable(string startId)
    {
        var order = new List<string> { startId };
        for (var i = 0; i < order.Count; i++)
            foreach (var l in From(order[i]))
                if (l.ToId is { } to && !order.Contains(to)) order.Add(to);
        return order.SelectMany(From).ToList();
    }
}
