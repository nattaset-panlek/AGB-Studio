using AgbStudio.Build.Graphics;
using AgbStudio.Editor.Commands;
using AgbStudio.Editor.Shell;

namespace AgbStudio.Editor.Windows.Sprite;

/// <summary>
/// OAM Size (T-M19; Sprite Editor › Sprite settings › OAM size): the four square sizes, then one entry for the wide
/// and one for the tall shapes, as in Figma. Choosing a size re-lays the sheet out in frames of that size (one undo
/// step). The Wide / Tall entries pick the smallest size of that shape that holds the frame, and the next size of the
/// shape when the sprite already has it, so every hardware size is reachable. Args: the <see cref="SpriteDoc"/>.
/// </summary>
public static class OamSizeMenu
{
    public static readonly (int W, int H)[] Square = [(8, 8), (16, 16), (32, 32), (64, 64)];
    public static readonly (int W, int H)[] Wide = [(16, 8), (32, 8), (32, 16), (64, 32)];
    public static readonly (int W, int H)[] Tall = [(8, 16), (8, 32), (16, 32), (32, 64)];

    [EditorMenu("T-M19")]
    public static IReadOnlyList<MenuEntry> Entries(WindowContext ctx) => For(ctx.Args as SpriteDoc);

    static string Sizes((int W, int H)[] list) => string.Join(" · ", list.Select(s => $"{s.W}×{s.H}"));

    public static IReadOnlyList<MenuEntry> For(SpriteDoc? doc)
    {
        var current = doc is null ? (0, 0) : (doc.FrameW, doc.FrameH);
        var list = new List<MenuEntry>();
        foreach (var s in Square)
        {
            var size = s;
            list.Add(new RecentEntry($"Square {s.W}×{s.H}", "", current == s, new Relay(() => doc?.SetFrameSize(size.W, size.H))));
        }
        list.Add(SeparatorEntry.Instance);
        list.Add(new RecentEntry("Wide " + Sizes(Wide), "", Wide.Contains(current), new Relay(() => Apply(doc, Wide))));
        list.Add(new RecentEntry("Tall " + Sizes(Tall), "", Tall.Contains(current), new Relay(() => Apply(doc, Tall))));
        return list;
    }

    static void Apply(SpriteDoc? doc, (int W, int H)[] shape)
    {
        if (doc is null) return;
        var (w, h) = Next(shape, doc.FrameW, doc.FrameH);
        doc.SetFrameSize(w, h);
    }

    /// <summary>The size a Wide / Tall entry picks for a w x h sprite.</summary>
    public static (int W, int H) Next((int W, int H)[] shape, int w, int h)
    {
        var i = Array.IndexOf(shape, (w, h));
        if (i >= 0) return shape[(i + 1) % shape.Length];
        foreach (var s in shape) if (s.W >= w && s.H >= h) return s;
        return shape[^1];
    }

    /// <summary>True for the 12 hardware sprite sizes.</summary>
    public static bool IsHardware(int w, int h) => Gba.OamSize(w, h) != null;
}
