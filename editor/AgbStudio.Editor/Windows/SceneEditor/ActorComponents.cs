using AgbStudio.Core.Project;

namespace AgbStudio.Editor.Windows.SceneEditor;

/// <summary>One thing an actor is made of, as the Hierarchy badges and the Inspector's Components list show it.</summary>
/// <param name="Title">"Sprite", "Collider", "Platformer Body", "PlayerController" ...</param>
/// <param name="Glyph">Icon name (image, film, square, rocket, sword, bug, sound, code, type, link, camera).</param>
/// <param name="Kind">"sprite", "animation", "collider", "built-in", "script", "text", "zone", "link" or "camera".</param>
/// <param name="Section">Name of the Inspector section that edits it, or null.</param>
public sealed record ComponentBadge(string Title, string Glyph, string Kind, string? Section);

/// <summary>
/// The parts of an actor (T-P01): its sprite, animation and collider, every behaviour in <see cref="ActorData.Components"/>
/// (built-in or a project script), a text label or trigger zone, and what the scene wires to it (the camera follows
/// it, a link starts from it). The Hierarchy shows them as small badges after the name and the Inspector lists them
/// under the actor header, so a template's objects show what they use at a glance.
/// </summary>
public static class ActorComponents
{
    public static IReadOnlyList<ComponentBadge> Of(ActorData a, SceneFile? scene)
    {
        var list = new List<ComponentBadge>();
        if (a.Sprite.Length > 0) list.Add(new("Sprite", "image", "sprite", "Section_Sprite"));
        if (a.Sprite.Length > 0 && a.Animation.Length > 0) list.Add(new("Animation " + a.Animation, "film", "animation", "Section_Sprite"));
        if (a.Kind == ActorKind.TextLabel) list.Add(new("Text label", "type", "text", null));
        if (a.Kind is ActorKind.TriggerZone or ActorKind.CameraZone)
            list.Add(new(a.Kind == ActorKind.TriggerZone ? "Trigger zone" : "Camera zone", a.Kind == ActorKind.TriggerZone ? "flag" : "camera", "zone", null));
        foreach (var c in a.Components)
        {
            var builtin = Behaviours.Builtins.FirstOrDefault(b => b.Name == c.Type);
            list.Add(builtin != null
                ? new(builtin.Title, builtin.Glyph, "built-in", "Section_" + c.Type)
                : new(c.Type, "code", "script", "Section_" + c.Type));
        }
        if (a.Sprite.Length > 0 || a.Collider != null) list.Add(new("Collider", "square", "collider", null));
        if (scene != null)
        {
            if (scene.Camera.Follow.Length > 0 && scene.Camera.Follow == a.Name) list.Add(new("Camera follows", "camera", "camera", null));
            foreach (var l in scene.Links.Where(l => l.From == a.Name))
                list.Add(new("Link to " + l.To, "link", "link", null));
        }
        return list;
    }

    /// <summary>The badge glyphs of a Hierarchy row: behaviours first (what the actor does), then sprite, text, zone,
    /// link and camera; at most <paramref name="max"/>, without repeats.</summary>
    public static IReadOnlyList<string> Glyphs(IReadOnlyList<ComponentBadge> badges, int max = 4)
    {
        static int Order(ComponentBadge b) => b.Kind switch { "script" or "built-in" => 0, "sprite" => 1, "text" or "zone" => 2, "link" => 3, "camera" => 4, _ => 9 };
        return badges.Where(b => b.Kind is not ("animation" or "collider")).OrderBy(Order).Select(b => b.Glyph).Distinct().Take(max).ToList();
    }

    /// <summary>The row tooltip: "Components: Sprite, Platformer Body, PlayerController (script)".</summary>
    public static string Tooltip(IReadOnlyList<ComponentBadge> badges) =>
        badges.Count == 0 ? "No components" : "Components: " + string.Join(", ", badges.Select(b => b.Kind == "script" ? b.Title + " (script)" : b.Title));
}
