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
        // The scene the title menu starts (the first playable scene) is the one the template is about.
        var first = scenes.FirstOrDefault(s => s.Name == start)?.Menu?.Items.FirstOrDefault(i => i.Command == MenuCommand.LoadScene)?.Scene;
        var main = scenes.FirstOrDefault(s => s.Name == first) ?? scenes.FirstOrDefault(s => s.Name != start && s.Menu == null) ?? scenes.FirstOrDefault();
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
        new("topdown", "Top-down Adventure", "Adventure", "Explore, talk, push blocks, open doors.",
            "A top-down adventure: the player explores a village, talks to its people, pushes crates out of the way and unlocks doors.",
            ["Player movement in 8 directions", "Talk to NPCs with dialogue", "Push blocks", "Doors and keys", "Title screen and pause menu"],
            TopDownTemplate.Create),
        new("visualnovel", "Visual Novel", "Story", "Portraits, choices, flags and Thai text.",
            "A story told in pictures and words: portraits that change expression, choices that set flags and lead to different endings, in Thai and English.",
            ["Portraits with expressions", "Speaker name tag", "Choices that set saved flags", "Two endings", "Thai and English text"],
            VisualNovelTemplate.Create),
        new("puzzle", "Puzzle", "Puzzle", "Falling blocks or grids with match rules.",
            "A falling-gem puzzle: pairs of gems drop into a well, lines of three or more of one colour clear, and chains score more.",
            ["Falling pairs you move and turn", "Match three or more to clear", "Chains and score", "Best score saved", "Title screen and pause menu"],
            PuzzleTemplate.Create),
    ];

    public static TemplateInfo? Find(string id) => All.FirstOrDefault(t => t.Id == id);
}
