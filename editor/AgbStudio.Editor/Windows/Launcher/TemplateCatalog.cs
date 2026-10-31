using AgbStudio.Core.Project;

namespace AgbStudio.Editor.Windows.Launcher;

/// <summary>Genre filter of Launcher · Templates (T-P20): All / Action / Adventure / RPG / Puzzle / Story / Racing.</summary>
public enum TemplateCategory { Action, Adventure, Rpg, Puzzle, Story, Racing }

/// <summary>
/// One genre template (D-09). Texts are the Figma texts: <see cref="Description"/> on the cards of Launcher · Templates
/// and New Project, <see cref="HomeDescription"/> on the Launcher home ("Start from a template"), the details panel
/// and the New Project footer hint. <see cref="Content"/> says what the template adds to a new project.
/// </summary>
public sealed record TemplateInfo(
    string Id, string Title, TemplateCategory Category, string Preview, string Thumbnail,
    string Description, string HomeDescription, string Details, IReadOnlyList<string> Includes, string Stats, string Adds,
    TemplateContent Content);

/// <summary>What a template puts into a new project without its AgbStudio.Build template (or without "Sample level").</summary>
public enum TemplateContent
{
    /// <summary>Only the start scene; the genre content is not available yet.</summary>
    None,
    /// <summary>Player, enemy, coin and goal sprites, music and a jump sound (AgbStudio.Build.Content.SampleProjects).</summary>
    PlatformerKit,
}

/// <summary>
/// The 8 templates of Launcher · Templates and New Project (D-09), in the Figma order. The list is data: the cards,
/// filters, details panel and the New Project dialog are generated from it. A template's content comes from a prepared
/// project in <c>templates/&lt;id&gt;/</c> when that folder exists, else from the AgbStudio.Build template of the same id
/// (ProjectCreator.BuildTemplate), else from <see cref="TemplateInfo.Content"/>.
/// </summary>
public static class TemplateCatalog
{
    public static readonly IReadOnlyList<TemplateInfo> All =
    [
        new("platformer", "Platformer", TemplateCategory.Action, "art/genre/platformer", "art/scene/day",
            "Run, jump, collect. Slopes and one-way platforms.", "Run, jump, collect. Slopes and one-way platforms.",
            "A side-view action game: the player runs and jumps through levels, collects coins and stomps enemies.",
            ["Player controller with jump and coyote time", "Enemy patrol and stomp", "Coins, score and HUD", "Checkpoints and lives", "Title screen and pause menu"],
            "Mode 0 · 3 BG layers · 9 actors · 4 scripts", "player controller, enemies, coins, HUD and a title screen", TemplateContent.PlatformerKit),
        new("topdown", "Top-down Adventure", TemplateCategory.Adventure, "art/genre/topdown", "art/genre/topdown",
            "Explore, talk, push blocks, open doors.", "Explore, talk to NPCs, push blocks, open doors.",
            "A top-down adventure: the player explores rooms, talks to people, pushes blocks and opens locked doors.",
            ["8-way movement", "NPCs with dialogue", "Push blocks", "Doors and keys", "Room transitions"],
            "Mode 0 · 3 BG layers · 7 actors · 3 scripts", "8-way movement, NPCs, push blocks and doors", TemplateContent.None),
        new("rpg", "Turn-based RPG", TemplateCategory.Rpg, "art/genre/rpg", "art/scene/cave",
            "Party, stats, battles, items and shops.", "Party, stats, battles, items and shops from the Database.",
            "A turn-based role-playing game: a party with stats fights battles, finds items and buys from shops.",
            ["Party and stats from the Database", "Turn-based battles", "Items and inventory", "Shops", "Save points"],
            "Mode 0 · 3 BG layers · 6 actors · 5 scripts", "a party, battles, items and shops from the Database", TemplateContent.None),
        new("shmup", "Shoot 'em up", TemplateCategory.Action, "art/genre/shmup", "art/genre/shmup",
            "Scrolling stages, bullet patterns, power-ups.", "Scrolling stages, bullet patterns, power-ups.",
            "A scrolling shooter: the ship flies through stages, dodges bullet patterns and collects power-ups.",
            ["Scrolling stages", "Bullet patterns", "Power-ups", "Score and lives"],
            "Mode 0 · 2 BG layers · 8 actors · 4 scripts", "a ship, bullet patterns, power-ups and a score", TemplateContent.None),
        new("puzzle", "Puzzle", TemplateCategory.Puzzle, "art/genre/puzzle", "art/genre/puzzle",
            "Falling blocks or grids with match rules.", "Falling blocks or grids with match rules.",
            "A puzzle game: blocks fall into a grid and clear when the match rules are met.",
            ["Falling blocks", "Grid with match rules", "Score and next piece", "Speed levels"],
            "Mode 0 · 2 BG layers · 2 actors · 3 scripts", "a block grid, match rules, score and next piece", TemplateContent.None),
        new("novel", "Visual Novel", TemplateCategory.Story, "art/genre/novel", "art/scene/dusk",
            "Portraits, choices, flags and Thai text.", "Portraits, choices, flags and Thai text.",
            "A visual novel: portraits and text boxes tell the story, and the player's choices set flags.",
            ["Portraits and text boxes", "Choices and flags", "Thai text", "Save and load"],
            "Mode 0 · 2 BG layers · 3 actors · 2 scripts", "portraits, dialogue, choices and flags", TemplateContent.None),
        new("racing", "Racing (Mode 7)", TemplateCategory.Racing, "art/genre/racing", "art/scene/snow",
            "Rotating ground plane, laps, timer.", "Rotating ground plane, laps, timer.",
            "A racing game: a rotating ground plane in Mode 1, laps and a race timer.",
            ["Rotating ground plane", "Laps and checkpoints", "Race timer", "Opponent cars"],
            "Mode 1 · 2 BG layers · 4 actors · 3 scripts", "a rotating ground plane, laps and a timer", TemplateContent.None),
        new("brawler", "Beat 'em up", TemplateCategory.Action, "art/genre/brawler", "art/genre/brawler",
            "Side-scrolling fights, combos, enemy waves.", "Side-scrolling fights, combos, enemy waves.",
            "A side-scrolling brawler: the player fights waves of enemies with combos.",
            ["Side-scrolling fights", "Combos", "Enemy waves", "Health and lives"],
            "Mode 0 · 3 BG layers · 8 actors · 4 scripts", "fighters, combos and enemy waves", TemplateContent.None),
    ];

    public static TemplateInfo Find(string? id) => All.FirstOrDefault(t => t.Id == id) ?? All[0];

    /// <summary>The filter chips in Figma order with their label ("All · 8" first).</summary>
    public static IReadOnlyList<(TemplateCategory? Category, string Label)> Filters =>
    [
        (null, $"All · {All.Count}"), (TemplateCategory.Action, "Action"), (TemplateCategory.Adventure, "Adventure"), (TemplateCategory.Rpg, "RPG"),
        (TemplateCategory.Puzzle, "Puzzle"), (TemplateCategory.Story, "Story"), (TemplateCategory.Racing, "Racing"),
    ];

    /// <summary>Templates of a category that match a search text (title, description, details or includes).</summary>
    public static IEnumerable<TemplateInfo> Filter(TemplateCategory? category, string? query)
    {
        var q = query?.Trim() ?? "";
        return All.Where(t => (category is null || t.Category == category) && (q.Length == 0
            || t.Title.Contains(q, StringComparison.OrdinalIgnoreCase) || t.Description.Contains(q, StringComparison.OrdinalIgnoreCase)
            || t.Details.Contains(q, StringComparison.OrdinalIgnoreCase) || t.Includes.Any(i => i.Contains(q, StringComparison.OrdinalIgnoreCase))));
    }
}

/// <summary>A sample project of Launcher · Learn: name, meta, preview and how its content is made.</summary>
public sealed record SampleInfo(string Name, string Meta, string Preview, Func<string, GameProject>? Create);

public static class SampleCatalog
{
    /// <summary>The 4 sample projects of Launcher · Learn (P15.11), generated by AgbStudio.Build (Sky Hopper and
    /// AgbStudio.Build.Content.Samples); a prepared copy in <c>samples/&lt;name&gt;/</c> is used instead when it exists.</summary>
    public static readonly IReadOnlyList<SampleInfo> All =
    [
        new("Sky Hopper demo", "platformer", "art/scene/day", AgbStudio.Build.Content.SkyHopper.Create),
        new("Cave Quest", "top-down RPG", "art/scene/cave", AgbStudio.Build.Content.Samples.CaveQuest),
        new("Snow Run", "racing", "art/scene/snow", AgbStudio.Build.Content.Samples.SnowRun),
        new("Dusk Story", "visual novel", "art/scene/dusk", AgbStudio.Build.Content.Samples.DuskStory),
    ];
}
