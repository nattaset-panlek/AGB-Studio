using AgbStudio.Editor.Tests.Support;
using Avalonia.Headless.XUnit;
using Xunit.Abstractions;

namespace AgbStudio.Editor.Tests.Prototype;

/// <summary>
/// T-X01 and T-X02: each prototype flow of "AGB Studio · Screens" works end to end in the app. One app session starts
/// the way a user starts AGB Studio (no project: the Launcher; the Sky Hopper project: the Scene Editor), then every
/// link the flow reaches is clicked in order (window by window, breadth first from the start frame). To get to a
/// link's window the walk clicks prototype links too: it closes dialogs with their Close / Cancel link, opens a dialog
/// with the link that opens it, and goes to a page along the shortest chain of NAVIGATE links. A page with no link
/// back (the Debugger, Scripting API, Database, Font Manager, Build Report in the editor flow) ends the chain: the walk
/// restarts the flow at its start frame, as the Figma presenter does ("Restart flow").
/// </summary>
[Collection(EmulatorCollection.Name)]
public class PrototypeFlowTests(ITestOutputHelper log)
{
    [AvaloniaFact]
    public void T_X01_FlowStart_FromTheLauncher_EveryLinkItReaches() => Walk(PrototypeData.Flows.Single(f => f.Name == "AGB Studio · start"));

    [AvaloniaFact]
    public void T_X02_FlowEditor_FromTheSceneEditor_EveryLinkItReaches() => Walk(PrototypeData.Flows.Single(f => f.Name == "AGB Studio · editor"));

    void Walk(ProtoFlow flow)
    {
        var links = PrototypeData.Reachable(flow.StartId);
        using var s = new AppSession(project: !AppSession.BeforeProject(flow.StartId));
        // the app opens where the flow starts
        Assert.Equal(flow.StartId, s.Win.CurrentPageId);
        var walker = new FlowWalker(s, flow, links);
        foreach (var l in links)
        {
            if (walker.Done.Contains(l.Index)) continue;
            walker.Reach(l.FromId);
            walker.Follow(l);
        }
        Assert.Equal(links.Select(l => l.Index).Order(), walker.Done.Order());
        log.WriteLine($"{flow.Name}: {links.Count} links from {flow.Start}, {walker.Clicks} link clicks, {walker.Restarts} restarts");
        foreach (var step in walker.Trace) log.WriteLine(step);
    }

    sealed class FlowWalker(AppSession s, ProtoFlow flow, IReadOnlyList<ProtoLink> links)
    {
        public HashSet<int> Done { get; } = [];
        public List<string> Trace { get; } = [];
        public int Clicks { get; private set; }
        public int Restarts { get; private set; }

        public void Follow(ProtoLink l)
        {
            // a build started by an earlier step (Play, Debug) has ended by the time the user clicks on a page;
            // a Build ROM dialog keeps its build until it is closed
            if (!s.Win.Overlays.IsOpen) s.EndBuild();
            LinkRunner.Follow(s, l);
            Done.Add(l.Index);
            Clicks++;
            Trace.Add($"{l}  ->  {s.Win.CurrentPageId}{(s.Win.Overlays.IsOpen ? " + " + s.Win.Overlays.Open[^1].GetType().Name : "")}");
        }

        /// <summary>Gets the app to <paramref name="id"/> through the flow's own links.</summary>
        public void Reach(string id)
        {
            if (!AppSession.IsPage(id))
            {
                var top = s.Win.Overlays.Open.LastOrDefault();
                if (top != null && AppSession.TypeOf(id).IsInstanceOfType(top)) return;
                // the link that opens it, from a page of the flow
                var opener = links.First(o => o.ToId == id && o.Action == "OVERLAY");
                Reach(opener.FromId);
                Follow(opener);
                return;
            }
            CloseOverlays();
            if (s.Win.CurrentPageId == id) return;
            var path = PathTo(s.Win.CurrentPageId!, id);
            if (path is null)
            {
                // no chain of links from here: restart the flow at its start frame
                _ = s.S.Windows.Open(flow.StartId, source: "restart flow");
                AppSession.Pump();
                Restarts++;
                Trace.Add($"restart flow at {flow.Start}");
                Assert.Equal(flow.StartId, s.Win.CurrentPageId);
                path = PathTo(flow.StartId, id) ?? throw new Xunit.Sdk.XunitException($"{id} is not reachable from {flow.Start}");
            }
            foreach (var step in path) Follow(step);
        }

        /// <summary>Closes the open dialogs and popovers with their prototype Close / Cancel link.</summary>
        void CloseOverlays()
        {
            while (s.Win.Overlays.Open.LastOrDefault() is { } top)
            {
                var close = links.FirstOrDefault(l => l.Action == "CLOSE" && AppSession.TypeOf(l.FromId).IsInstanceOfType(top))
                    ?? throw new Xunit.Sdk.XunitException($"{top.GetType().Name} is open and the flow has no link that closes it ({s.Describe()})");
                Follow(close);
            }
        }

        /// <summary>The shortest chain of page-to-page links of the flow, or null.</summary>
        List<ProtoLink>? PathTo(string from, string to)
        {
            var prev = new Dictionary<string, ProtoLink>();
            var queue = new Queue<string>([from]);
            var seen = new HashSet<string> { from };
            while (queue.Count > 0)
            {
                var at = queue.Dequeue();
                if (at == to)
                {
                    var path = new List<ProtoLink>();
                    for (var x = to; x != from; x = prev[x].FromId) path.Insert(0, prev[x]);
                    return path;
                }
                foreach (var l in links.Where(l => l.FromId == at && (l.Action is "NAVIGATE" or "SWAP") && l.ToId != null && AppSession.IsPage(l.ToId)))
                    if (seen.Add(l.ToId!)) { prev[l.ToId!] = l; queue.Enqueue(l.ToId!); }
            }
            return null;
        }
    }
}
