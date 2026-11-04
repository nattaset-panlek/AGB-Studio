using System.Text.Json.Serialization;

namespace AgbStudio.Core.Project;

// scenes/<Name>.scene (Scene Editor, Scene Settings, World Map, Link Settings)
public sealed class SceneFile
{
    public int Format { get; set; } = 1;
    public string Name { get; set; } = "";
    public int Width { get; set; } = 30;    // 16 px tiles
    public int Height { get; set; } = 20;
    public int VideoMode { get; set; }
    public string Map { get; set; } = "";   // maps/<Name>.map
    public CameraSettings Camera { get; set; } = new();
    public MusicSettings Music { get; set; } = new();
    public SceneEvents Events { get; set; } = new();
    public List<ActorData> Actors { get; set; } = new();
    public List<LinkData> Links { get; set; } = new();
    public bool AlphaBlending { get; set; }
    public bool Mosaic { get; set; }
    public string Backdrop { get; set; } = "#000000";
    public SceneMenu? Menu { get; set; }    // title screens (Menu / UI kit)
    public string Page { get; set; } = "";  // a UI page (ui/<Name>.uipage) that opens when the scene has entered: a title screen
    public string Hud { get; set; } = "";   // a UI page that stays over the scene while it plays: scores, lives, a timer
    // World Map card position and the template it started from.
    public int MapX { get; set; }
    public int MapY { get; set; }
    public string Template { get; set; } = "";
}

public sealed class CameraSettings
{
    public string Follow { get; set; } = "Player";
    public int DeadZoneW { get; set; } = 32;
    public int DeadZoneH { get; set; } = 24;
    public bool ClampToMap { get; set; } = true;
}

public sealed class MusicSettings
{
    public string Track { get; set; } = "";
    public double Volume { get; set; } = 0.8;
    public bool FadeOnSceneChange { get; set; } = true;
}

public sealed class SceneEvents
{
    public string OnEnter { get; set; } = "";  // C# statements, e.g. FadeIn(0.5s)
    public string OnExit { get; set; } = "";   // e.g. Save.Checkpoint()
    public string OnLoad { get; set; } = "";
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ActorKind { Empty, Player, Enemy, Pickup, TriggerZone, CameraZone, TextLabel, Panel, Canvas }

public sealed record UiPadding(int Left, int Top, int Right, int Bottom);
public sealed record UiMargin(int Left, int Top, int Right, int Bottom);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PanelBackground { None, SolidPaper, NineSlice, Transparent }

public sealed class ActorData
{
    public string Name { get; set; } = "Actor";
    public ActorKind Kind { get; set; }
    public string Sprite { get; set; } = "";      // sprite asset name
    public string Animation { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public string Tag { get; set; } = "";
    public int Priority { get; set; } = 1;          // OBJ priority (Inspector "Layer OBJ · Prio 1")
    public bool Hidden { get; set; }
    public string Prefab { get; set; } = "";
    public string Group { get; set; } = "";         // Hierarchy folder (Enemies, Coins ...)
    public string Parent { get; set; } = "";        // Parent actor name (Unity GameObject hierarchy), empty = scene root
    public ColliderData? Collider { get; set; }     // null = from the sprite
    public int ZoneW { get; set; } = 16;            // Trigger / Camera zones / UI Panel width
    public int ZoneH { get; set; } = 16;
    public string Text { get; set; } = "";          // Text Label: string key
    public UiPadding Padding { get; set; } = new(4, 4, 4, 4);
    public UiMargin Margin { get; set; } = new(0, 0, 0, 0);
    public PanelBackground Background { get; set; } = PanelBackground.SolidPaper;
    public byte PaperColorIndex { get; set; } = 5;
    public byte BorderColorIndex { get; set; } = 1;
    public bool TileSnap { get; set; } = true;
    public List<ComponentData> Components { get; set; } = new();

    [JsonIgnore]
    public bool IsUi => Kind is ActorKind.Canvas or ActorKind.Panel or ActorKind.TextLabel;

    public (int X, int Y) GetWorldPosition(IReadOnlyList<ActorData>? allActors)
    {
        var x = X;
        var y = Y;
        if (string.IsNullOrEmpty(Parent) || allActors == null) return (x, y);

        var current = Parent;
        var visited = new HashSet<string>(StringComparer.Ordinal) { Name };

        while (!string.IsNullOrEmpty(current) && visited.Add(current))
        {
            var p = allActors.FirstOrDefault(a => a.Name == current);
            if (p == null) break;
            x += p.X;
            y += p.Y;
            current = p.Parent;
        }

        return (x, y);
    }
}

public sealed class ColliderData
{
    public int X { get; set; }
    public int Y { get; set; }
    public int W { get; set; }
    public int H { get; set; }
}

// A behaviour on an actor: a built-in (PlatformerBody, Health ...) or a script class name, with the
// Inspector field values as strings ("1.5", "true", "3").
public sealed class ComponentData
{
    public string Type { get; set; } = "";
    public Dictionary<string, string> Fields { get; set; } = new();
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LinkKind { OnTouch, Door, Respawn }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Transition { Cut, Fade, Slide }

// Link Settings popover: From, To scene, Spawn at, Transition, Duration, Only if.
public sealed class LinkData
{
    public string From { get; set; } = "";          // actor name in this scene
    public LinkKind Kind { get; set; }
    public string To { get; set; } = "";            // scene name
    public string SpawnAt { get; set; } = "Start";  // spawn point name or "x,y"
    public int SpawnX { get; set; } = -1;
    public int SpawnY { get; set; } = -1;
    public Transition Transition { get; set; } = Transition.Fade;
    public double Duration { get; set; } = 0.5;     // seconds
    public string OnlyIf { get; set; } = "";        // C# condition, empty = always
}

// maps/<Name>.map (Tilemap Editor)
public sealed class MapFile
{
    public int Format { get; set; } = 1;
    public string Name { get; set; } = "";
    public string Tileset { get; set; } = "";       // tileset asset name
    public int Width { get; set; } = 30;            // 16 px tiles
    public int Height { get; set; } = 20;
    public List<MapLayerData> Layers { get; set; } = new();
    public int CollisionLayer { get; set; } = -1;   // index into Layers
    /// <summary>Tilemap Editor "BG size": "32x32", "64x32", "32x64" or "64x64" screen blocks; omitted = 64x32.</summary>
    public string? BgSize { get; set; }
    /// <summary>Tilemap Editor "Colours": 4 (16 per tile) or 8 (256 colours); omitted = 4.</summary>
    public int? Bpp { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LayerType { Tile, Image, Affine, CollisionOnly }

public sealed class MapLayerData
{
    public string Name { get; set; } = "";
    public int Bg { get; set; }
    public LayerType Type { get; set; }
    public int Priority { get; set; }
    public double ParallaxX { get; set; } = 1;
    public double ParallaxY { get; set; } = 1;
    public bool Wrap { get; set; }
    public string Image { get; set; } = "";         // Image layers: png asset
    public List<int> Cells { get; set; } = new();   // metatile per cell, row-major (Width x Height)
}

// A menu opened when the scene has entered (title screens): rows with the triangle cursor on the text layer.
public sealed class SceneMenu
{
    public int X { get; set; } = 88;      // panel position and size in pixels (rounded to 8 px tiles)
    public int Y { get; set; } = 72;
    public int Width { get; set; } = 96;
    public int Height { get; set; } = 48;
    public int Spacing { get; set; } = 14;
    public List<SceneMenuItem> Items { get; set; } = new();
}

public sealed class SceneMenuItem
{
    public string Text { get; set; } = "";     // string key in the Localization tables, or plain ASCII text
    public MenuCommand Command { get; set; }
    public string Scene { get; set; } = "";    // LoadScene: target scene name
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MenuCommand { LoadScene, Continue, Options, QuitToTitle }
