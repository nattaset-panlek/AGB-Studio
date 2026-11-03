using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgbStudio.Editor.Docking;

/// <summary>Where a dragged panel lands on a group: in its tabs, or beside it as a new group.</summary>
public enum DockZone { Center, Left, Right, Top, Bottom }

/// <summary>A split (columns or rows of children) or, without <see cref="Split"/>, a group of tabbed panels.</summary>
public sealed class DockNode
{
    /// <summary>"Columns" or "Rows"; null for a group.</summary>
    public string? Split { get; set; }
    public List<DockNode> Children { get; set; } = [];
    public List<string> Panels { get; set; } = [];
    public string? Active { get; set; }
    /// <summary>Width (in columns) or height (in rows) in pixels; 0 takes the space that is left.</summary>
    public double Size { get; set; }

    [JsonIgnore] public bool IsGroup => Split == null;
    [JsonIgnore] public bool IsColumns => Split == DockLayout.Columns;

    public IEnumerable<DockNode> Groups() => IsGroup ? [this] : Children.SelectMany(c => c.Groups());
    public bool Holds(string panel) => Groups().Any(g => g.Panels.Contains(panel));
}

/// <summary>A panel in a window of its own, and the panel it goes back beside when that window closes.</summary>
public sealed class DockFloat
{
    public string Panel { get; set; } = "";
    public string ReturnTo { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 360;
    public double Height { get; set; } = 480;
}

/// <summary>
/// The Scene Editor workspace as data: a tree of splits and tab groups plus the floating panels. The document group
/// (the viewport with the document tabs) always stays in the tree and never takes other tabs; in every split the child
/// that holds it, or else the last child, takes the space that is left. Saved as JSON in the preferences.
/// </summary>
public sealed class DockLayout
{
    public const string Columns = "Columns", Rows = "Rows";
    public const string Hierarchy = "T-P01:Hierarchy", Viewport = "T-P01:Viewport", Inspector = "T-P01:Inspector";
    public const string Assets = "T-P01:Assets", Console = "T-P05", Animation = "T-P06", Database = "T-P07";
    const double DefaultColumn = 260, DefaultRow = 250, MinSplit = 120;

    static readonly JsonSerializerOptions Json = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault };

    public DockNode Root { get; set; } = new();
    public List<DockFloat> Floats { get; set; } = [];

    /// <summary>The layout of the Figma frame: Hierarchy 260 px, viewport over the bottom panel 250 px, Inspector 320 px.</summary>
    public static DockLayout Default() => new()
    {
        Root = new DockNode
        {
            Split = Columns,
            Children =
            [
                new DockNode { Panels = [Hierarchy], Active = Hierarchy, Size = 260 },
                new DockNode
                {
                    Split = Rows,
                    Children =
                    [
                        new DockNode { Panels = [Viewport], Active = Viewport },
                        new DockNode { Panels = [Assets, Console, Animation, Database], Active = Assets, Size = 250 },
                    ],
                },
                new DockNode { Panels = [Inspector], Active = Inspector, Size = 320 },
            ],
        },
    };

    public DockNode? GroupOf(string panel) => Root.Groups().FirstOrDefault(g => g.Panels.Contains(panel));

    public IEnumerable<string> AllPanels() => Root.Groups().SelectMany(g => g.Panels).Concat(Floats.Select(f => f.Panel));

    /// <summary>Moves a panel into the tabs of the group that holds <paramref name="target"/> or beside it. False when
    /// the move is not possible (the viewport itself, tabs on the viewport, onto itself alone).</summary>
    public bool Move(string panel, string target, DockZone zone, int index = -1)
    {
        if (panel == Viewport) return false;
        var into = GroupOf(target);
        if (into == null) return false;
        if (zone == DockZone.Center && into.Panels.Contains(Viewport)) return false;
        var from = GroupOf(panel);
        if (from == into)
        {
            if (zone != DockZone.Center && from.Panels.Count == 1) return false;
            if (zone == DockZone.Center)
            {
                from.Panels.Remove(panel);
                from.Panels.Insert(index < 0 || index > from.Panels.Count ? from.Panels.Count : index, panel);
                from.Active = panel;
                return true;
            }
        }
        Floats.RemoveAll(f => f.Panel == panel);
        if (from != null) Remove(from, panel);
        into = GroupOf(target)!;
        if (zone == DockZone.Center)
        {
            into.Panels.Insert(index < 0 || index > into.Panels.Count ? into.Panels.Count : index, panel);
            into.Active = panel;
        }
        else Beside(into, panel, zone);
        Normalize();
        return true;
    }

    /// <summary>Takes a panel out of the tree into its own window.</summary>
    public bool Float(string panel, double x, double y, double width = 360, double height = 480)
    {
        if (panel == Viewport || Floats.Any(f => f.Panel == panel)) return false;
        var from = GroupOf(panel);
        if (from == null) return false;
        // back beside a panel of the same group, or else in the bottom group
        var returnTo = from.Panels.FirstOrDefault(p => p != panel) ?? Root.Groups().FirstOrDefault(g => g != from && !g.Panels.Contains(Viewport))?.Panels[0] ?? Viewport;
        Remove(from, panel);
        Floats.Add(new DockFloat { Panel = panel, ReturnTo = returnTo, X = x, Y = y, Width = width, Height = height });
        Normalize();
        return true;
    }

    /// <summary>Brings a floating panel back into the tree.</summary>
    public void Dock(string panel)
    {
        var f = Floats.FirstOrDefault(x => x.Panel == panel);
        if (f == null) return;
        Floats.Remove(f);
        var target = GroupOf(f.ReturnTo) != null && !f.ReturnTo.Equals(Viewport) ? f.ReturnTo : null;
        if (target != null) { var g = GroupOf(target)!; g.Panels.Add(panel); g.Active = panel; }
        else Beside(GroupOf(Viewport)!, panel, DockZone.Bottom);
        Normalize();
    }

    /// <summary>Removes a panel from its group; an empty group leaves the tree and a split of one child is replaced by that child.</summary>
    void Remove(DockNode group, string panel)
    {
        group.Panels.Remove(panel);
        if (group.Active == panel) group.Active = group.Panels.FirstOrDefault();
        if (group.Panels.Count == 0) Prune(Root, group);
        Collapse(Root);
    }

    static bool Prune(DockNode at, DockNode group)
    {
        if (at.Children.Remove(group)) return true;
        return at.Children.Any(c => Prune(c, group));
    }

    static void Collapse(DockNode at)
    {
        for (var i = 0; i < at.Children.Count; i++)
        {
            var c = at.Children[i];
            Collapse(c);
            if (!c.IsGroup && c.Children.Count == 1)
            {
                var only = c.Children[0];
                only.Size = c.Size;
                at.Children[i] = only;
            }
        }
    }

    void Beside(DockNode group, string panel, DockZone zone)
    {
        var columns = zone is DockZone.Left or DockZone.Right;
        var axis = columns ? Columns : Rows;
        var before = zone is DockZone.Left or DockZone.Top;
        var added = new DockNode { Panels = [panel], Active = panel };
        var parent = Parent(Root, group);
        if (parent == null)
        {
            // the group is the root: it becomes the first child of a new split
            Root = new DockNode { Split = axis, Children = before ? [added, group] : [group, added] };
            group.Size = 0;
            added.Size = columns ? DefaultColumn : DefaultRow;
            return;
        }
        var i = parent.Children.IndexOf(group);
        if (parent.Split == axis)
        {
            added.Size = group.Size > 0 ? Math.Max(MinSplit, group.Size / 2) : columns ? DefaultColumn : DefaultRow;
            if (group.Size > 0) group.Size = Math.Max(MinSplit, group.Size / 2);
            parent.Children.Insert(before ? i : i + 1, added);
            return;
        }
        // across the parent's direction: a new split takes the group's place and its size
        var wrap = new DockNode { Split = axis, Size = group.Size };
        group.Size = 0;
        added.Size = columns ? DefaultColumn : DefaultRow;
        wrap.Children = before ? [added, group] : [group, added];
        parent.Children[i] = wrap;
    }

    static DockNode? Parent(DockNode at, DockNode child)
    {
        if (at.Children.Contains(child)) return at;
        foreach (var c in at.Children) if (Parent(c, child) is { } p) return p;
        return null;
    }

    /// <summary>Every split gets exactly one child that fills; the others keep a size in pixels.</summary>
    public void Normalize() => Normalize(Root);

    static void Normalize(DockNode split)
    {
        if (split.IsGroup) return;
        var fill = split.Children.FirstOrDefault(c => c.Holds(Viewport)) ?? split.Children[^1];
        foreach (var c in split.Children)
        {
            if (c == fill) c.Size = 0;
            else if (c.Size <= 0) c.Size = split.IsColumns ? DefaultColumn : DefaultRow;
            Normalize(c);
        }
    }

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    /// <summary>Reads a saved layout and makes it fit the panels of this version: unknown panels go, missing ones join
    /// the bottom group. A damaged layout gives the default.</summary>
    public static DockLayout FromJson(string? json, IReadOnlyCollection<string>? known = null)
    {
        if (string.IsNullOrWhiteSpace(json)) return Default();
        try
        {
            var l = JsonSerializer.Deserialize<DockLayout>(json, Json);
            if (l?.Root == null || l.GroupOf(Viewport) == null) return Default();
            if (known != null) l.Fit(known);
            l.Normalize();
            return l;
        }
        catch (JsonException) { return Default(); }
    }

    void Fit(IReadOnlyCollection<string> known)
    {
        foreach (var g in Root.Groups().ToList())
            foreach (var p in g.Panels.ToList())
                if (!known.Contains(p)) Remove(g, p);
        Floats.RemoveAll(f => !known.Contains(f.Panel));
        foreach (var p in known.Except(AllPanels()).ToList())
        {
            var g = GroupOf(Assets) ?? Root.Groups().FirstOrDefault(x => !x.Panels.Contains(Viewport));
            if (g != null) g.Panels.Add(p);
            else Beside(GroupOf(Viewport)!, p, DockZone.Bottom);
        }
    }
}
