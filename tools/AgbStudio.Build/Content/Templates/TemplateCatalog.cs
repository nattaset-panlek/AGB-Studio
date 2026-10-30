using AgbStudio.Core.Project;

namespace AgbStudio.Build.Content.Templates;

/// <summary>A New Project template (Launcher · Templates and New Project, D-09).</summary>
/// <param name="Id">Stable id used by the New Project dialog and tests.</param>
/// <param name="Name">Card title as in Figma.</param>
/// <param name="Genre">Filter chip: Action, Adventure, RPG, Puzzle, Story or Racing.</param>
/// <param name="Summary">Card text as in Figma.</param>
/// <param name="Description">Details panel paragraph.</param>
/// <param name="Includes">Details panel "Includes" list.</param>
/// <param name="Create">Writes the project into an empty folder and returns it.</param>
public sealed record TemplateInfo(string Id, string Name, string Genre, string Summary, string Description, IReadOnlyList<string> Includes,
    Func<string, string, GameProject> Create)
{
    /// <summary>The details footer ("Mode 0 · 3 BG layers · 9 actors · 4 scripts"), counted from a created project.</summary>
    public static string Stats(GameProject p)
    {
        var start = p.Settings.General.StartScene;
        var scenes = p.SceneFiles().Select(GameProject.Load<SceneFile>).ToList();
        // The first playable scene (not the title) is the one the template is about.
        var main = scenes.FirstOrDefault(s => s.Name != start && s.Menu == null) ?? scenes.FirstOrDefault();
        var layers = 0;
        if (main != null && main.Map.Length > 0)
        {
            var mapPath = Path.Combine(p.MapsDir, main.Map + ".map");
            if (File.Exists(mapPath)) layers = GameProject.Load<MapFile>(mapPath).Layers.Count(l => l.Type != LayerType.CollisionOnly);
        }
        return $"Mode {main?.VideoMode ?? 0} · {layers} BG layers · {main?.Actors.Count ?? 0} actors · {p.ScriptFiles().Count()} scripts";
    }
}

/// <summary>The eight templates of Launcher · Templates, in the build order of D-09.</summary>
public static class TemplateCatalog
{
    public static readonly IReadOnlyList<TemplateInfo> All =
    [
        new("platformer", "Platformer", "Action", "Run, jump, collect. Slopes and one-way platforms.",
            "A side-view action game: the player runs and jumps through levels, collects coins and stomps enemies.",
            ["Player controller with jump and coyote time", "Enemy patrol and stomp", "Coins, score and HUD", "Checkpoints and lives", "Title screen and pause menu"],
            PlatformerTemplate.Create),
    ];

    public static TemplateInfo? Find(string id) => All.FirstOrDefault(t => t.Id == id);
}
