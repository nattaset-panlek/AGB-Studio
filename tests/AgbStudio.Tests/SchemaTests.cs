using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using AgbStudio.Build;
using AgbStudio.Build.Content;
using AgbStudio.Build.Content.Templates;
using AgbStudio.Build.Pipeline;
using AgbStudio.Core.Project;
using AgbStudio.Scripting.Dialogue;
using AgbStudio.Scripting.Graph;

namespace AgbStudio.Tests;

/// <summary>
/// P1.04: a JSON schema per project file format (docs/schemas, D-03). Schemas of the formats written by the C# model are
/// generated from the classes with the serializer settings the files are written with, so they cannot drift: the
/// committed files must equal the generated ones (set AGB_UPDATE_SCHEMAS=1 to rewrite them). The Logic Graph schema is
/// written by hand (its writer is hand-made). Every file of the fixture, the samples and the eight templates, and a
/// filled-in instance of every format, must validate.
/// </summary>
public sealed class SchemaTests
{
    public static readonly string SchemaDir = Path.Combine(NativeBuild.RepoRoot, "docs", "schemas");

    static readonly JsonSerializerOptions DialogueJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // File name, model type, title, serializer settings (null = GameProject.Json).
    public static readonly (string File, Type Type, string Title, JsonSerializerOptions? Options)[] Generated =
    [
        ("agbproj.schema.json", typeof(ProjectFile), "AGB Studio project (<Name>.agbproj)", null),
        ("scene.schema.json", typeof(SceneFile), "AGB Studio scene (scenes/<Name>.scene)", null),
        ("map.schema.json", typeof(MapFile), "AGB Studio map (maps/<Name>.map)", null),
        ("sprite-meta.schema.json", typeof(SpriteMeta), "AGB Studio sprite meta (assets/sprites/<file>.meta.json)", null),
        ("tileset-meta.schema.json", typeof(TilesetMeta), "AGB Studio tileset meta (assets/tilesets/<file>.meta.json)", null),
        ("music-meta.schema.json", typeof(MusicMeta), "AGB Studio music meta (assets/audio/music/<file>.meta.json)", null),
        ("sound-meta.schema.json", typeof(SoundMeta), "AGB Studio sound meta (assets/audio/sfx/<file>.meta.json)", null),
        ("sfx.schema.json", typeof(SfxFile), "AGB Studio SFX Designer effect (audio/sfx/<name>.sfx)", null),
        ("font.schema.json", typeof(FontFile), "AGB Studio game font (fonts/<name>.font)", null),
        ("db.schema.json", typeof(TableFile), "AGB Studio Database table (data/<Table>.db)", null),
        ("strings.schema.json", typeof(StringTable), "AGB Studio string table (text/strings.<lang>.json)", null),
        ("strings-meta.schema.json", typeof(StringMeta), "AGB Studio string notes (text/strings.meta.json)", null),
        ("prefab.schema.json", typeof(ActorData), "AGB Studio prefab (prefabs/<Name>.prefab)", null),
        ("dlg.schema.json", typeof(DialogueFile), "AGB Studio dialogue (dialogue/<name>.dlg)", DialogueJson),
        ("palettes.schema.json", typeof(PaletteLibraryFile), "AGB Studio Palette Manager library (assets/palettes/palettes.json)", null),
        // P7.12: build output read by the editor (Build Report, Budget Details) and by tools outside AGB Studio.
        ("report.schema.json", typeof(BuildReport), "AGB Studio build report (build/report.json)", BuildReport.Json),
        ("report-history.schema.json", typeof(List<BuildReport.HistoryEntry>), "AGB Studio build history (build/report-history.json)", BuildReport.Json),
    ];

    public static string Generate(Type type, string title, JsonSerializerOptions? options)
    {
        var o = new JsonSerializerOptions(options ?? GameProject.Json) { TypeInfoResolver = new DefaultJsonTypeInfoResolver(), RespectNullableAnnotations = true };
        var node = JsonSchemaExporter.GetJsonSchemaAsNode(o, type, new JsonSchemaExporterOptions { TreatNullObliviousAsNonNullable = true });
        var root = new JsonObject
        {
            ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
            ["title"] = title,
        };
        foreach (var (k, v) in node.AsObject().ToList())
        {
            node.AsObject().Remove(k);
            root[k] = v;
        }
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }).Replace("\r\n", "\n") + "\n";
    }

    [Fact]
    public void SchemasMatchTheFileModel()
    {
        var update = Environment.GetEnvironmentVariable("AGB_UPDATE_SCHEMAS") == "1";
        var stale = new List<string>();
        foreach (var (file, type, title, options) in Generated)
        {
            var path = Path.Combine(SchemaDir, file);
            var text = Generate(type, title, options);
            if (File.Exists(path) && File.ReadAllText(path).Replace("\r\n", "\n") == text) continue;
            if (update || !File.Exists(path))
            {
                Directory.CreateDirectory(SchemaDir);
                File.WriteAllText(path, text);
            }
            stale.Add(file);
        }
        Assert.True(update || stale.Count == 0, $"Schemas differ from the file model (rewritten; review and commit them): {string.Join(", ", stale)}");
        Assert.True(File.Exists(Path.Combine(SchemaDir, "graph.schema.json")));
        // Every format names its version (a prefab is an actor of a scene; the build history is a list).
        foreach (var (file, type, _, _) in Generated.Where(g => g.Type != typeof(ActorData) && g.Type != typeof(List<BuildReport.HistoryEntry>)))
            Assert.True(Schema(file)["properties"]?["format"] != null, $"{file} has no format field");
    }

    static JsonObject Schema(string file) => JsonNode.Parse(File.ReadAllText(Path.Combine(SchemaDir, file)))!.AsObject();

    /// <summary>The schema of a project file by where it lives, or null for files that are not JSON formats.</summary>
    public static string? SchemaFor(string projectRoot, string path)
    {
        var rel = Path.GetRelativePath(projectRoot, path).Replace('\\', '/');
        var name = Path.GetFileName(rel);
        if (rel.StartsWith("build/", StringComparison.Ordinal)) return null;
        if (name.EndsWith(".agbproj", StringComparison.Ordinal)) return "agbproj.schema.json";
        if (name.EndsWith(".scene", StringComparison.Ordinal)) return "scene.schema.json";
        if (name.EndsWith(".map", StringComparison.Ordinal)) return "map.schema.json";
        if (name.EndsWith(".sfx", StringComparison.Ordinal)) return "sfx.schema.json";
        if (name.EndsWith(".font", StringComparison.Ordinal)) return "font.schema.json";
        if (name.EndsWith(".db", StringComparison.Ordinal)) return "db.schema.json";
        if (name.EndsWith(".prefab", StringComparison.Ordinal)) return "prefab.schema.json";
        if (name.EndsWith(".dlg", StringComparison.Ordinal)) return "dlg.schema.json";
        if (name.EndsWith(".graph", StringComparison.Ordinal)) return "graph.schema.json";
        if (rel == "text/strings.meta.json") return "strings-meta.schema.json";
        if (rel == "assets/palettes/palettes.json") return "palettes.schema.json";
        if (rel.StartsWith("text/strings.", StringComparison.Ordinal) && name.EndsWith(".json", StringComparison.Ordinal)) return "strings.schema.json";
        if (!name.EndsWith(".meta.json", StringComparison.Ordinal)) return null;
        if (rel.StartsWith("assets/sprites/", StringComparison.Ordinal)) return "sprite-meta.schema.json";
        if (rel.StartsWith("assets/tilesets/", StringComparison.Ordinal)) return "tileset-meta.schema.json";
        if (rel.StartsWith("assets/audio/music/", StringComparison.Ordinal)) return "music-meta.schema.json";
        if (rel.StartsWith("assets/audio/sfx/", StringComparison.Ordinal)) return "sound-meta.schema.json";
        return null;
    }

    [Fact]
    public void EveryProjectFileOfTheFixtureSamplesAndTemplatesValidates()
    {
        var root = Path.Combine(NativeBuild.RepoRoot, "build", "test-projects", "schemas");
        if (Directory.Exists(root)) Directory.Delete(root, true);
        var projects = new List<GameProject> { SkyHopper.Create(Path.Combine(root, "fixture")), Samples.CaveQuest(Path.Combine(root, "cave")) };
        foreach (var t in TemplateCatalog.All) projects.Add(t.Create(Path.Combine(root, t.Id), t.Name));
        var checkedFiles = new Dictionary<string, int>();
        var errors = new List<string>();
        foreach (var p in projects)
            foreach (var f in Directory.EnumerateFiles(p.Root, "*", SearchOption.AllDirectories))
            {
                if (SchemaFor(p.Root, f) is not { } schema) continue;
                errors.AddRange(Validate(schema, File.ReadAllText(f)).Select(e => $"{Path.GetRelativePath(root, f)}: {e}"));
                checkedFiles[schema] = checkedFiles.GetValueOrDefault(schema) + 1;
            }
        Assert.True(errors.Count == 0, string.Join("\n", errors.Take(30)));
        foreach (var s in new[] { "agbproj", "scene", "map", "sprite-meta", "tileset-meta", "sfx", "font", "db", "strings", "strings-meta" })
            Assert.True(checkedFiles.GetValueOrDefault(s + ".schema.json") > 0, $"no {s} file was checked");
    }

    [Fact]
    public void FilledInInstancesOfEveryFormatValidate()
    {
        void Check<T>(string schema, T value)
        {
            var errors = Validate(schema, JsonSerializer.Serialize(value, GameProject.Json));
            Assert.True(errors.Count == 0, $"{schema}: {string.Join("; ", errors)}");
        }
        Check("music-meta.schema.json", new MusicMeta { Volume = 0.5, LoopStart = 2, FadeOnSceneChange = false });
        Check("sound-meta.schema.json", new SoundMeta { Rate = 11025, Volume = 0.75 });
        Check("strings-meta.schema.json", new StringMeta { Keys = { ["dlg.forest.hopper_01"] = new StringInfo { Group = "Dialogue", Speaker = "Hopper", Notes = "first line", BoxWidth = 200, MaxLines = 2 } } });
        Check("prefab.schema.json", new ActorData { Name = "Slime", Kind = ActorKind.Enemy, Sprite = "slime", Collider = new ColliderData { W = 12, H = 10 },
            Components = { new ComponentData { Type = "Patrol", Fields = { ["speed"] = "0.5" } } } });
        Check("sprite-meta.schema.json", new SpriteMeta { Animations = { new AnimationData { Name = "run", Frames = { 0, 1 }, FrameTicks = [4, 8], PingPong = true } } });
        Check("tileset-meta.schema.json", new TilesetMeta
        {
            Tiles = { [3] = new TileProperties { Collision = CollisionType.SlopeUp, Animation = [3, 4], Tag = "ground" } },
            AutoTiles = { new AutoTileSet { Rules = { new AutoTileRule { Name = "Top edge", Mask = [2, 0, 2, 1, 1, 1, 2, 1, 2], Tile = 5 } } } },
        });
        Check("palettes.schema.json", new PaletteLibraryFile { Banks = { new PaletteBankData { Kind = BankKind.Obj, Slot = 15, Colors = { "none", "#FFFFFF" } } } });
        Check("font.schema.json", new FontFile { Name = "f", SourceKind = FontSourceKind.Bdf, Glyphs = { ["U+0E01"] = new GlyphOverride { Advance = 6, Rows = { "0123" } } } });
        // fields the editor writes (P1.04): Tilemap Editor BG size / Colours, Font Manager Draw to
        Check("map.schema.json", new MapFile { Name = "Forest_01", BgSize = "32x64", Bpp = 8, Layers = { new MapLayerData { Name = "Ground", Cells = [0, 1] } } });
        Check("font.schema.json", new FontFile { Name = "thai_vwf", DrawTo = 2 });
        T RoundTrip<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, GameProject.Json), GameProject.Json)!;
        var map = RoundTrip(new MapFile { BgSize = "32x64", Bpp = 8 });
        Assert.Equal(("32x64", 8, 2), (map.BgSize, map.Bpp, RoundTrip(new FontFile { DrawTo = 2 }).DrawTo));
        Assert.DoesNotContain("bgSize", JsonSerializer.Serialize(new MapFile(), GameProject.Json));   // left out at the defaults
        Assert.DoesNotContain("drawTo", JsonSerializer.Serialize(new FontFile(), GameProject.Json));
        Check("scene.schema.json", new SceneFile
        {
            Name = "Title", Menu = new SceneMenu { Items = { new SceneMenuItem { Text = "ui.menu.start", Command = MenuCommand.LoadScene, Scene = "Forest_01" } } },
            Links = { new LinkData { From = "Door", To = "Cave_01", Kind = LinkKind.Door, Transition = Transition.Fade, OnlyIf = "Game.coins >= 10" } },
        });

        var dialogue = DialogueFile.Parse("""
            { "format": 1, "name": "forest_intro", "textSpeed": 2, "blip": "blip",
              "speakers": [ { "name": "Elder", "portrait": "elder", "blip": "blip_low", "textSpeed": 1, "names": { "en": "Elder", "th": "ผู้เฒ่า" }, "color": "#B79CFF" } ],
              "conversations": [ { "id": "intro", "lines": [
                { "id": "l1", "speaker": "Elder", "key": "dlg.forest.help_me", "text": { "en": "Help!", "th": "ช่วยด้วย!" },
                  "portrait": "elder_worried", "speed": 1, "blip": "blip_low", "showIf": "!Flags.MetElder", "thenSet": [ "Flags.MetElder = true" ], "next": "l2" },
                { "id": "l2", "speaker": "Elder", "key": "dlg.forest.ask",
                  "choices": [ { "key": "dlg.forest.yes", "goto": "l1", "showIf": "Game.coins > 0", "thenSet": [ "Game.coins = 0" ] },
                               { "key": "dlg.forest.no", "scene": "Forest_02" } ] },
                { "command": "setFlag", "flag": "MetElder", "value": true },
                { "command": "giveItem", "table": "Items", "item": "Potion", "count": 1 },
                { "command": "playSound", "sound": "jump", "volume": 0.5 },
                { "command": "wait", "frames": 30 }, { "command": "wait", "seconds": 0.5 },
                { "command": "shakeCamera", "amplitude": 2, "frames": 20 },
                { "command": "goToScene", "scene": "Forest_02" },
                { "command": "end" } ] } ] }
            """);
        var dlgText = dialogue.ToJson();
        Assert.DoesNotContain("lineOf", dlgText);
        Assert.Empty(Validate("dlg.schema.json", dlgText));
        Assert.Equal(dlgText, DialogueFile.Parse(dlgText).ToJson());  // round trip
        Assert.Equal(("ผู้เฒ่า", "#B79CFF"), (dialogue.Speakers[0].Names!["th"], dialogue.Speakers[0].Color));

        var graph = LogicGraph.Parse("""
            { "format": 1, "class": "PlayerLogic",
              "nodes": [ { "id": "n1", "type": "OnUpdate", "x": 40, "y": 60 },
                         { "id": "n2", "type": "SetVelocityY", "x": 520, "y": 60, "props": { "value": -3.50 } } ],
              "links": [ { "from": "n1.out", "to": "n2.in" } ] }
            """);
        var graphText = graph.ToJson();
        Assert.Empty(Validate("graph.schema.json", graphText));
        Assert.Equal(graphText, LogicGraph.Parse(graphText).ToJson());
        // the Logic Graph editor adds comment boxes, collapsed nodes and a disabled part (GraphDocument.ToJson)
        Assert.Empty(Validate("graph.schema.json", """
            { "format": 1, "class": "PlayerLogic", "nodes": [], "links": [],
              "comments": [ { "title": "Jump", "x": 20, "y": 30, "w": 300, "h": 160 } ],
              "collapsed": [ "n1" ],
              "disabled": { "format": 1, "class": "PlayerLogic", "nodes": [ { "id": "n9", "type": "PlaySound", "x": 0, "y": 0 } ], "links": [ { "from": "n1.out", "to": "n9.in" } ] } }
            """));
        Assert.Contains(Validate("graph.schema.json", """{ "format": 1, "disabled": { "nodes": [ { "x": 1 } ] } }"""), e => e.Contains("/disabled/nodes/0/id"));
        // A broken file is caught: a wrong type and a field the format does not have.
        Assert.Contains(Validate("music-meta.schema.json", """{ "format": 1, "volume": "loud" }"""), e => e.Contains("volume"));
        Assert.Contains(Validate("sfx.schema.json", """{ "format": 1, "chanel": "Wave" }"""), e => e.Contains("chanel"));
        Assert.Contains(Validate("sfx.schema.json", """{ "format": 1, "channel": "Square3" }"""), e => e.Contains("channel"));
        Assert.Contains(Validate("palettes.schema.json", """{ "format": 1, "banks": [ { "kind": "Sprite", "slot": 0 } ] }"""), e => e.Contains("kind"));
        Assert.Contains(Validate("palettes.schema.json", """{ "format": 1, "effects": { "fade": { "frames": "slow" } } }"""), e => e.Contains("frames"));
    }

    public static List<string> Validate(string schemaFile, string json) => JsonSchemaCheck.Validate(Path.Combine(SchemaDir, schemaFile), json);
}
