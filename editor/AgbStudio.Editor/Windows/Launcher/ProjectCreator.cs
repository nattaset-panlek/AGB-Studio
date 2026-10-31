using System.ComponentModel;
using System.Text.RegularExpressions;
using AgbStudio.Build;
using AgbStudio.Build.Content;
using AgbStudio.Core.Project;
using AgbStudio.Scripting.Graph;

namespace AgbStudio.Editor.Windows.Launcher;

/// <summary>Everything the New Project dialog (T-D01) asks for.</summary>
public sealed record NewProjectOptions
{
    public required TemplateInfo Template { get; init; }
    public required string Name { get; init; }
    /// <summary>The project folder to create (Location).</summary>
    public required string Folder { get; init; }
    public required string RomTitle { get; init; }
    public required string GameCode { get; init; }
    public SaveType SaveType { get; init; } = SaveType.Sram32K;
    public bool SampleLevel { get; init; } = true;
    public bool ThaiFont { get; init; } = true;
    public bool LogicGraphExamples { get; init; }
    public bool InitGit { get; init; } = true;
}

public sealed record CreatedProject(string ProjectPath, IReadOnlyList<string> Warnings);

/// <summary>Validation and derived defaults of the New Project fields (P11.08).</summary>
public static partial class NewProjectRules
{
    public const int RomTitleMax = 12;
    public const int GameCodeLength = 4;

    [GeneratedRegex("^[A-Z0-9 _-]{1,12}$")] private static partial Regex RomTitleRx();
    [GeneratedRegex("^[A-Z0-9]{4}$")] private static partial Regex GameCodeRx();

    /// <summary>ROM title of the cartridge header: 1 to 12 upper-case letters, digits, space, _ or -.</summary>
    public static bool IsValidRomTitle(string? s) => s != null && RomTitleRx().IsMatch(s);

    /// <summary>Game code of the cartridge header: exactly 4 upper-case letters or digits.</summary>
    public static bool IsValidGameCode(string? s) => s != null && GameCodeRx().IsMatch(s);

    public static bool IsValidName(string? s) => !string.IsNullOrWhiteSpace(s) && GameProject.FileName(s.Trim()).Length > 0;

    /// <summary>"Sky Hopper" -> "SKYHOPPER".</summary>
    public static string RomTitleFrom(string name)
    {
        var s = new string(name.ToUpperInvariant().Where(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9').ToArray());
        return s.Length > RomTitleMax ? s[..RomTitleMax] : s;
    }

    /// <summary>"SKYHOPPER" -> "SKYH" (padded with X).</summary>
    public static string GameCodeFrom(string romTitle)
    {
        var s = new string(romTitle.Where(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9').ToArray());
        return (s + "XXXX")[..GameCodeLength];
    }

    /// <summary>Location for a name in a base folder: D:\GBAProjects + "Sky Hopper" -> D:\GBAProjects\SkyHopper.</summary>
    public static string FolderFrom(string baseFolder, string name) => Path.Combine(baseFolder, GameProject.FileName(name.Trim()));

    /// <summary>True when the folder is inside the devkitPro folder (the "safer for updates" warning).</summary>
    public static bool IsInside(string folder, string devkitPro)
    {
        if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(devkitPro)) return false;
        try
        {
            var f = Path.GetFullPath(folder).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            var d = Path.GetFullPath(devkitPro).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            return f.StartsWith(d, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
    }

    /// <summary>A usable project folder: an absolute path that does not exist yet or is an empty folder.</summary>
    public static bool IsUsableFolder(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder)) return false;
        try
        {
            if (!Path.IsPathFullyQualified(folder)) return false;
            if (File.Exists(folder)) return false;
            return !Directory.Exists(folder) || !Directory.EnumerateFileSystemEntries(folder).Any();
        }
        catch (Exception e) when (e is ArgumentException or IOException or UnauthorizedAccessException) { return false; }
    }

    public static string SaveTypeLabel(SaveType t) => t switch
    {
        SaveType.None => "None",
        SaveType.Sram32K => "SRAM · 32 KB",
        SaveType.Flash64K => "Flash · 64 KB",
        SaveType.Flash128K => "Flash · 128 KB",
        _ => "EEPROM · 8 KB",
    };

    public static IReadOnlyList<SaveType> SaveTypes { get; } = Enum.GetValues<SaveType>();
}

/// <summary>
/// Creates a project from <see cref="NewProjectOptions"/> (P11.08). The project is assembled in a hidden staging folder
/// next to the target and moved into place at the end, so a failure never leaves a half-made project behind.
/// </summary>
public static class ProjectCreator
{
    public const string StartScene = "Scene_01";
    public const string SampleScene = "Forest_01";

    /// <summary>Folders searched for prepared templates (<c>templates/&lt;id&gt;/*.agbproj</c>, Phase 15).</summary>
    public static IEnumerable<string> TemplateRoots()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "templates");
        string? repo = null;
        try { repo = NativeBuild.RepoRoot; } catch (InvalidOperationException) { }
        if (repo != null) yield return Path.Combine(repo, "templates");
    }

    public static string? TemplateFolder(TemplateInfo t) =>
        TemplateRoots().Select(r => Path.Combine(r, t.Id)).FirstOrDefault(d => Directory.Exists(d) && Directory.EnumerateFiles(d, "*.agbproj").Any());

    /// <summary>The bundled Thai font (third_party/fonts, D-17), next to the executable or in the repository.</summary>
    public static string? ThaiFontSource()
    {
        var names = new List<string> { Path.Combine(AppContext.BaseDirectory, "third_party", "fonts", "NotoSansThai-Regular.ttf") };
        try { names.Add(Path.Combine(NativeBuild.RepoRoot, "third_party", "fonts", "NotoSansThai-Regular.ttf")); } catch (InvalidOperationException) { }
        return names.FirstOrDefault(File.Exists);
    }

    public static CreatedProject Create(NewProjectOptions o, Func<string, string, ProcResult>? git = null)
    {
        var name = o.Name.Trim();
        if (!NewProjectRules.IsValidName(name)) throw new ArgumentException("The project needs a name.");
        if (!NewProjectRules.IsValidRomTitle(o.RomTitle)) throw new ArgumentException("ROM title: 12 characters max (A-Z, 0-9).");
        if (!NewProjectRules.IsValidGameCode(o.GameCode)) throw new ArgumentException("Game code: 4 characters (A-Z, 0-9).");
        var target = Path.GetFullPath(o.Folder);
        if (!NewProjectRules.IsUsableFolder(target)) throw new IOException($"{target} is not empty.");
        var parent = Path.GetDirectoryName(target) ?? throw new IOException("The location needs a parent folder.");
        Directory.CreateDirectory(parent);
        var staging = Path.Combine(parent, $".{Path.GetFileName(target)}.creating-{Guid.NewGuid().ToString("N")[..8]}");
        var warnings = new List<string>();
        try
        {
            var p = Assemble(o, name, staging, warnings);
            var file = Path.GetFileName(p.ProjectPath);
            if (Directory.Exists(target)) Directory.Delete(target);
            Directory.Move(staging, target);
            var path = Path.Combine(target, file);
            if (o.InitGit) InitGit(target, git, warnings);
            return new CreatedProject(path, warnings);
        }
        catch
        {
            try { if (Directory.Exists(staging)) Directory.Delete(staging, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            throw;
        }
    }

    /// <summary>The genre template of AgbStudio.Build that writes a complete project for an editor template, or null.</summary>
    public static AgbStudio.Build.Content.Templates.TemplateInfo? BuildTemplate(TemplateInfo t) =>
        AgbStudio.Build.Content.Templates.TemplateCatalog.Find(t.Id == "novel" ? "visualnovel" : t.Id);

    static GameProject Assemble(NewProjectOptions o, string name, string dir, List<string> warnings)
    {
        GameProject p;
        // With "Sample level" a genre template of AgbStudio.Build writes the whole game (scenes, scripts, art, fonts and its
        // title screen as start scene): that is the template's sample. Without it the project starts empty (T-P22).
        var complete = true;
        if (TemplateFolder(o.Template) is { } prepared)
        {
            CopyDirectory(prepared, dir);
            p = GameProject.Open(dir);
        }
        else if (o.SampleLevel && BuildTemplate(o.Template) is { } template)
            p = template.Create(dir, name);
        else
        {
            complete = false;
            p = GameProject.Create(dir, name);
            if (o.Template.Content == TemplateContent.PlatformerKit) SampleProjects.WriteCommonArt(p);
        }
        var s = p.Settings;
        s.Name = name;
        s.General.GameTitle = name;
        s.General.RomTitle = o.RomTitle;
        s.General.GameCode = o.GameCode;
        s.Save.Type = o.SaveType;
        var ns = GameProject.FileName(name).Replace("-", "_", StringComparison.Ordinal);
        s.Scripting.Namespace = ns.Length > 0 && char.IsDigit(ns[0]) ? "_" + ns : ns;

        var sample = o.SampleLevel && !complete;
        if (sample) AddSampleLevel(p);
        if (!p.SceneFiles().Any()) AddStartScene(p);
        var start = sample ? SampleScene : p.SceneFiles().Select(GameProject.AssetName).Contains(s.General.StartScene) ? s.General.StartScene : GameProject.AssetName(p.SceneFiles().First());
        s.General.StartScene = start;
        s.LastScene = start;
        // a template that has the Thai font already keeps its own font settings
        if (o.ThaiFont && !File.Exists(Path.Combine(p.FontsDir, "thai_vwf.font"))) AddThaiFont(p, warnings);
        if (o.LogicGraphExamples) AddLogicGraph(p);

        // the project file is named after the project (Sky Hopper -> SkyHopper.agbproj)
        var wanted = Path.Combine(p.Root, GameProject.FileName(name) + ".agbproj");
        GameProject.Save(wanted, s);
        if (!string.Equals(wanted, p.ProjectPath, StringComparison.OrdinalIgnoreCase)) File.Delete(p.ProjectPath);
        return GameProject.Open(wanted);
    }

    /// <summary>The empty start scene of a new project: one GBA screen, a fixed camera, nothing else (T-P22).</summary>
    public static void AddStartScene(GameProject p) =>
        GameProject.Save(Path.Combine(p.ScenesDir, StartScene + ".scene"), new SceneFile
        {
            Name = StartScene, Width = 15, Height = 10, Camera = new CameraSettings { Follow = "" },
        });

    /// <summary>The Forest_01 sample level: forest tileset, a ground map and the player, an enemy, a coin and the goal.</summary>
    public static void AddSampleLevel(GameProject p)
    {
        if (!File.Exists(Path.Combine(p.AssetsDir, "sprites", "hero.png"))) SampleProjects.WriteCommonArt(p);
        var tiles = Path.Combine(p.AssetsDir, "tilesets", "forest_tiles.png");
        PixelArt.ForestTiles().Save(tiles);
        GameProject.Save(GameProject.MetaPath(tiles), SampleProjects.ForestTilesetMeta());
        GameProject.Save(Path.Combine(p.MapsDir, SampleScene + ".map"), SampleProjects.GroundMap(SampleScene, 30, 10, gapFrom: 12, gapTo: 13, wallX: 20));
        GameProject.Save(Path.Combine(p.ScenesDir, SampleScene + ".scene"), new SceneFile
        {
            Name = SampleScene, Width = 30, Height = 10, Map = SampleScene,
            Actors =
            {
                new ActorData
                {
                    Name = "Player", Kind = ActorKind.Player, Sprite = "hero", Animation = "idle", X = 40, Y = 100, Tag = "Player",
                    Components = { new ComponentData { Type = "PlatformerBody" }, new ComponentData { Type = "Health", Fields = { ["hearts"] = "3" } } },
                },
                new ActorData
                {
                    Name = "Slime", Kind = ActorKind.Enemy, Sprite = "slime", X = 260, Y = 144, Tag = "Enemy", Priority = 2, Group = "Enemies",
                    Components = { new ComponentData { Type = "PlatformerBody" }, new ComponentData { Type = "Patrol", Fields = { ["speed"] = "0.5" } }, new ComponentData { Type = "DamageOnTouch" } },
                },
                new ActorData { Name = "Coin", Kind = ActorKind.Pickup, Sprite = "coin", X = 90, Y = 140, Tag = "Coin", Priority = 2, Group = "Coins" },
                new ActorData { Name = "Goal Flag", Kind = ActorKind.TriggerZone, Sprite = "flag", X = 440, Y = 144, ZoneW = 16, ZoneH = 32 },
            },
        });
    }

    /// <summary>Noto Sans Thai (D-17) as the variable-width font thai_vwf, made the default font.</summary>
    public static void AddThaiFont(GameProject p, List<string> warnings)
    {
        if (ThaiFontSource() is not { } ttf)
        {
            warnings.Add("Noto Sans Thai was not found, so the Thai font was not added.");
            return;
        }
        Directory.CreateDirectory(p.FontsDir);
        File.Copy(ttf, Path.Combine(p.FontsDir, Path.GetFileName(ttf)), true);
        GameProject.Save(Path.Combine(p.FontsDir, "thai_vwf.font"), new FontFile
        {
            Name = "thai_vwf", Source = Path.GetFileName(ttf), Size = 12, CharacterSets = { "Basic Latin", "Thai" },
            LineHeight = 14, Baseline = 10, Bpp = 2, DropShadow = true,
        });
        p.Settings.General.DefaultFont = "thai_vwf";
    }

    /// <summary>A Logic Graph example (scripts/PlayerLogic.graph): jump when A is pressed on the ground.</summary>
    public static void AddLogicGraph(GameProject p)
    {
        var g = new AgbStudio.Scripting.Graph.LogicGraph { Class = "PlayerLogic" };
        g.Nodes.Add(new GraphNode { Id = "n1", Type = "OnUpdate", X = 40, Y = 60 });
        g.Nodes.Add(new GraphNode { Id = "n2", Type = "ButtonPressed", X = 40, Y = 180, Props = { ["button"] = "A" } });
        g.Nodes.Add(new GraphNode { Id = "n3", Type = "IsOnGround", X = 40, Y = 260 });
        g.Nodes.Add(new GraphNode { Id = "n4", Type = "And", X = 260, Y = 200 });
        g.Nodes.Add(new GraphNode { Id = "n5", Type = "Branch", X = 300, Y = 60 });
        g.Nodes.Add(new GraphNode { Id = "n6", Type = "SetVelocityY", X = 520, Y = 60, Props = { ["value"] = "-3.50" } });
        g.Nodes.Add(new GraphNode { Id = "n7", Type = "PlaySound", X = 740, Y = 60, Props = { ["sound"] = "jump", ["volume"] = "1" } });
        foreach (var (from, to) in new[] { ("n1.out", "n5.in"), ("n2.value", "n4.a"), ("n3.value", "n4.b"), ("n4.value", "n5.condition"), ("n5.true", "n6.in"), ("n6.out", "n7.in") })
            g.Links.Add(new GraphLink(from, to));
        Directory.CreateDirectory(p.ScriptsDir);
        File.WriteAllText(Path.Combine(p.ScriptsDir, "PlayerLogic.graph"), g.ToJson());
    }

    static void InitGit(string folder, Func<string, string, ProcResult>? git, List<string> warnings)
    {
        try
        {
            var r = (git ?? ((args, dir) => Proc.Run("git", args, dir, TimeSpan.FromSeconds(30))))("init", folder);
            if (r.ExitCode != 0)
                warnings.Add($"The project was made, but without a Git repository (git init failed: {r.Output.Trim().Split('\n')[0].Trim()}). Run git init in the project folder to add one.");
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException or IOException)
        {
            warnings.Add("Git was not found, so no repository was made.");
        }
    }

    public static void CopyDirectory(string from, string to, Func<string, bool>? skip = null)
    {
        Directory.CreateDirectory(to);
        foreach (var dir in Directory.EnumerateDirectories(from))
        {
            if (skip?.Invoke(dir) == true) continue;
            CopyDirectory(dir, Path.Combine(to, Path.GetFileName(dir)), skip);
        }
        foreach (var file in Directory.EnumerateFiles(from))
            if (skip?.Invoke(file) != true) File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
    }
}
